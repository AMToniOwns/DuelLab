using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DuelLab.Application.Common;
using DuelLab.Application.Decks;
using DuelLab.Domain.Decks;
using DuelLab.Domain.Simulations;
using DuelLab.Simulator.Analysis;

namespace DuelLab.Application.Simulations;

/// <summary>Runs, stores, lists, reproduces and compares opening-hand simulations.</summary>
public sealed class SimulationService(
    DeckService deckService,
    ISimulationRunRepository runs,
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    SimulationEngine engine,
    TimeProvider time)
{
    /// <summary>Largest sample count accepted by the API.</summary>
    public const int MaxSampleCount = 100_000;

    /// <summary>Default number of sample hands kept.</summary>
    public const int DefaultSampleHandCount = 20;

    /// <summary>
    /// Largest seed accepted: 2^53 − 1, the largest integer a JavaScript number holds exactly, so seeds
    /// survive a round trip through the browser unchanged.
    /// </summary>
    public const long MaxSeed = 9_007_199_254_740_991;

    /// <summary>The scenarios used when a request does not name any (the MVP's core metrics).</summary>
    public static IReadOnlyList<ScenarioDto> DefaultScenarios { get; } =
    [
        new("Starter", [new("Starter")]),
        new("2+ starters", [new("Starter", 2)]),
        new("Extender", [new("Extender")]),
        new("Starter + extender", [new("Starter"), new("Extender")], RequireDistinctCards: true),
        new("Hand trap", [new("HandTrap")]),
        new("Brick", [new("Brick")]),
    ];

    /// <summary>Runs a simulation on a deck version and stores it.</summary>
    public async Task<SimulationRunDto> RunAsync(Guid versionId, SimulationConfigDto? request, CancellationToken cancellationToken)
    {
        var (deck, version) = await deckService.LoadVersionAsync(versionId, cancellationToken);
        var config = Normalize(request ?? new SimulationConfigDto());
        var snapshot = await CreateSnapshotAsync(deck, version, cancellationToken);
        var result = Execute(config, snapshot);

        var run = new SimulationRun(
            deck.Id,
            version.Id,
            time.GetUtcNow(),
            config.Seed!.Value,
            config.SampleCount,
            config.OpeningHandSize,
            result.EngineVersion,
            Hash(snapshot),
            JsonSerializer.Serialize(new StoredConfiguration(config, snapshot), JsonDefaults.Options),
            JsonSerializer.Serialize(result, JsonDefaults.Options));
        runs.Add(run);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(run, config, snapshot, result, versionChangedSinceRun: false);
    }

    /// <summary>A stored run.</summary>
    public async Task<SimulationRunDto> GetAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(runId, cancellationToken) ?? throw new NotFoundException("Simulation", runId);
        var stored = ReadConfiguration(run);
        var result = ReadResult(run);

        bool? changed = null;
        try
        {
            var (deck, version) = await deckService.LoadVersionAsync(run.DeckVersionId, cancellationToken);
            changed = Hash(await CreateSnapshotAsync(deck, version, cancellationToken)) != run.DeckSnapshotHash;
        }
        catch (NotFoundException)
        {
            // The version was deleted; the stored snapshot still describes the run.
        }

        return ToDto(run, stored.Configuration, stored.Snapshot, result, changed);
    }

    /// <summary>Recent runs, newest first, optionally for one version.</summary>
    public async Task<IReadOnlyList<SimulationRunSummaryDto>> ListAsync(Guid? versionId, int take, CancellationToken cancellationToken)
    {
        var list = await runs.ListAsync(versionId, Math.Clamp(take, 1, 100), cancellationToken);
        return list.Select(run =>
        {
            var stored = ReadConfiguration(run);
            var result = ReadResult(run);
            return new SimulationRunSummaryDto(
                run.Id,
                run.DeckId,
                run.DeckVersionId,
                stored.Snapshot.DeckName,
                stored.Snapshot.VersionName,
                run.CreatedAt,
                run.RandomSeed,
                run.SampleCount,
                run.OpeningHandSize,
                result.Scenarios.Select(s => new ScenarioHeadlineDto(s.Name, s.Estimate, s.Exact)).ToList());
        }).ToList();
    }

    /// <summary>
    /// Re-runs a stored simulation from its saved configuration and deck snapshot, and reports whether
    /// the results are identical. Nothing is stored.
    /// </summary>
    public async Task<ReproductionDto> ReproduceAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(runId, cancellationToken) ?? throw new NotFoundException("Simulation", runId);
        var stored = ReadConfiguration(run);
        var reproduced = Execute(stored.Configuration, stored.Snapshot);
        var identical = OutcomesJson(reproduced) == OutcomesJson(ReadResult(run));

        return new ReproductionDto(
            identical,
            run.EngineVersion == SimulationEngine.EngineVersion,
            run.EngineVersion,
            SimulationEngine.EngineVersion,
            reproduced);
    }

    /// <summary>
    /// Runs the same configuration (including the same seed) on two versions, stores both runs, and
    /// reports the differences.
    /// </summary>
    public async Task<ComparisonDto> CompareAsync(CompareRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.BaseVersionId == request.CandidateVersionId)
        {
            throw new RequestValidationException("compare.same_version", "Choose two different versions to compare.");
        }

        // Resolve the seed once so both versions use the same random stream.
        var config = Normalize(request.Configuration ?? new SimulationConfigDto());
        var baseRun = await RunAsync(request.BaseVersionId, config, cancellationToken);
        var candidateRun = await RunAsync(request.CandidateVersionId, config, cancellationToken);

        var scenarios = baseRun.Result.Scenarios.Zip(candidateRun.Result.Scenarios, (b, c) => new ScenarioComparisonDto(
            b.Name,
            b.Description,
            b.Exact,
            c.Exact,
            b.Exact is { } be && c.Exact is { } ce ? (ce - be) * 100 : null,
            b.Estimate,
            c.Estimate,
            (c.Estimate - b.Estimate) * 100)).ToList();

        var (_, baseVersion) = await deckService.LoadVersionAsync(request.BaseVersionId, cancellationToken);
        var (_, candidateVersion) = await deckService.LoadVersionAsync(request.CandidateVersionId, cancellationToken);
        var (cardChanges, roleChanges) = await DiffAsync(baseVersion, candidateVersion, cancellationToken);

        return new ComparisonDto(baseRun, candidateRun, scenarios, cardChanges, roleChanges);
    }

    private async Task<(List<CardChangeDto>, List<RoleChangeDto>)> DiffAsync(DeckVersion a, DeckVersion b, CancellationToken cancellationToken)
    {
        var ids = a.Cards.Concat(b.Cards).Select(c => c.CardId).Distinct().ToList();
        var names = (await cards.GetByIdsAsync(ids, cancellationToken)).ToDictionary(kv => kv.Key, kv => kv.Value.Name);
        string Name(int id) => names.GetValueOrDefault(id, $"Card {id}");

        int Quantity(DeckVersion v, int id, DeckZone zone) =>
            v.Cards.Where(c => c.CardId == id && c.Zone == zone).Sum(c => c.Quantity);

        var cardChanges = (
            from id in ids
            from zone in Enum.GetValues<DeckZone>()
            let before = Quantity(a, id, zone)
            let after = Quantity(b, id, zone)
            where before != after
            orderby zone, Name(id)
            select new CardChangeDto(id, Name(id), zone, before, after, after - before)).ToList();

        var roleChanges = (
            from id in ids
            let before = a.RolesOf(id).Select(r => r.Key).ToHashSet(StringComparer.OrdinalIgnoreCase)
            let after = b.RolesOf(id).Select(r => r.Key).ToHashSet(StringComparer.OrdinalIgnoreCase)
            let added = after.Except(before, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList()
            let removed = before.Except(after, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList()
            where added.Count > 0 || removed.Count > 0
            orderby Name(id)
            select new RoleChangeDto(id, Name(id), added, removed)).ToList();

        return (cardChanges, roleChanges);
    }

    /// <summary>Fills defaults, picks a seed if none was given, and checks API-level limits.</summary>
    private static SimulationConfigDto Normalize(SimulationConfigDto config)
    {
        if (config.SampleCount < 1 || config.SampleCount > MaxSampleCount)
        {
            throw new RequestValidationException("simulation.sample_count", $"Sample count must be between 1 and {MaxSampleCount:N0}.");
        }

        if (config.OpeningHandSize < 1 || config.OpeningHandSize > SimulationEngine.MaxHandSize)
        {
            throw new RequestValidationException("simulation.hand_size", $"Opening hand size must be between 1 and {SimulationEngine.MaxHandSize}.");
        }

        if (config.Seed is < 0 or > MaxSeed)
        {
            throw new RequestValidationException("simulation.seed", $"Seed must be between 0 and {MaxSeed}.");
        }

        var scenarios = config.Scenarios is { Count: > 0 } given ? given : DefaultScenarios;
        foreach (var requirement in scenarios.SelectMany(s => s.Requirements ?? []))
        {
            // Rejects unknown role keys with a clear message; also normalises their spelling.
            _ = CardRole.Parse(requirement.Role);
        }

        return config with
        {
            Seed = config.Seed ?? Random.Shared.NextInt64(0, MaxSeed + 1),
            Scenarios = scenarios
                .Select(s => s with { Requirements = (s.Requirements ?? []).Select(r => r with { Role = CardRole.Parse(r.Role).Key }).ToList() })
                .ToList(),
            SampleHandCount = config.SampleHandCount ?? DefaultSampleHandCount,
        };
    }

    private AnalysisResult Execute(SimulationConfigDto config, DeckSnapshot snapshot)
    {
        var request = new AnalysisRequest(
            snapshot.MainDeck.Select(c => new AnalysisCard(c.CardId.ToString(System.Globalization.CultureInfo.InvariantCulture), c.Name, c.Quantity, c.Roles)).ToList(),
            config.OpeningHandSize,
            config.SampleCount,
            config.Seed!.Value,
            config.Scenarios!.Select(s => new Scenario(
                s.Name,
                s.Requirements.Select(r => new RoleRequirement(r.Role, r.MinimumCount, r.MaximumCount)).ToList(),
                s.RequireDistinctCards)).ToList(),
            config.SampleHandCount!.Value);

        try
        {
            return engine.Run(request);
        }
        catch (ArgumentException ex)
        {
            throw new RequestValidationException("simulation.invalid", ex.Message.Split(" (Parameter")[0]);
        }
    }

    private async Task<DeckSnapshot> CreateSnapshotAsync(Deck deck, DeckVersion version, CancellationToken cancellationToken)
    {
        var (validation, used) = await deckService.ValidateAsync(version, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Issues.Where(i => i.Severity == ValidationSeverity.Error).Select(i => i.Message);
            throw new RequestValidationException("simulation.invalid_deck", "Fix the deck before simulating: " + string.Join(" ", errors));
        }

        var main = version.Cards
            .Where(c => c.Zone == DeckZone.Main)
            .OrderBy(c => c.CardId)
            .Select(c => new SnapshotCard(
                c.CardId,
                used[c.CardId].Name,
                c.Quantity,
                version.RolesOf(c.CardId).Select(r => r.Key).Order(StringComparer.Ordinal).ToList()))
            .ToList();

        return new DeckSnapshot(deck.Id, deck.Name, version.Id, version.VersionName, main);
    }

    /// <summary>A hash of what affects results: the Main Deck's cards, quantities and roles.</summary>
    private static string Hash(DeckSnapshot snapshot)
    {
        var canonical = string.Join(';', snapshot.MainDeck.Select(c => $"{c.CardId}x{c.Quantity}[{string.Join(',', c.Roles)}]"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// The results without their prose descriptions: reproduction checks that every count, probability
    /// and sample hand matches exactly, while wording improvements never invalidate old runs.
    /// </summary>
    private static string OutcomesJson(AnalysisResult result) =>
        JsonSerializer.Serialize(
            result with { Scenarios = result.Scenarios.Select(s => s with { Description = string.Empty }).ToList() },
            JsonDefaults.Options);

    private static StoredConfiguration ReadConfiguration(SimulationRun run) =>
        JsonSerializer.Deserialize<StoredConfiguration>(run.ConfigurationJson, JsonDefaults.Options)
        ?? throw new InvalidOperationException($"Simulation {run.Id} has no configuration.");

    private static AnalysisResult ReadResult(SimulationRun run) =>
        JsonSerializer.Deserialize<AnalysisResult>(run.ResultsJson, JsonDefaults.Options)
        ?? throw new InvalidOperationException($"Simulation {run.Id} has no results.");

    private static SimulationRunDto ToDto(SimulationRun run, SimulationConfigDto config, DeckSnapshot snapshot, AnalysisResult result, bool? versionChangedSinceRun) => new(
        run.Id,
        run.DeckId,
        run.DeckVersionId,
        snapshot.DeckName,
        snapshot.VersionName,
        run.CreatedAt,
        run.EngineVersion,
        run.RandomSeed,
        run.SampleCount,
        run.OpeningHandSize,
        config,
        snapshot,
        result,
        versionChangedSinceRun);
}
