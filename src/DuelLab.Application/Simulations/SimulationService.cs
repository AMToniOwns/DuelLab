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
        if (config.IncludeComboLines)
        {
            config = config with { Scenarios = WithComboLines(config.Scenarios!, version.ComboLines) };
        }

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
            changed = Hash(await CreateSnapshotAsync(deck, version, cancellationToken, requireValid: false)) != run.DeckSnapshotHash;
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

        var (_, baseVersion) = await deckService.LoadVersionAsync(request.BaseVersionId, cancellationToken);
        var (_, candidateVersion) = await deckService.LoadVersionAsync(request.CandidateVersionId, cancellationToken);

        // Resolve the seed once so both versions use the same random stream, and measure one shared
        // set of combo lines on both (the base version's, then any only the candidate has), so every
        // scenario lines up.
        var config = Normalize(request.Configuration ?? new SimulationConfigDto());
        if (config.IncludeComboLines)
        {
            var shared = baseVersion.ComboLines
                .Concat(candidateVersion.ComboLines.Where(c => !baseVersion.ComboLines.Any(b => string.Equals(b.Name, c.Name, StringComparison.OrdinalIgnoreCase))))
                .ToList();
            config = config with { Scenarios = WithComboLines(config.Scenarios!, shared), IncludeComboLines = false };
        }

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

        var (cardChanges, roleChanges) = await DiffAsync(baseVersion, candidateVersion, cancellationToken);

        return new ComparisonDto(baseRun, candidateRun, scenarios, cardChanges, roleChanges, ComboDiff(baseVersion, candidateVersion));
    }

    /// <summary>Scenario name prefix for combo lines.</summary>
    public const string ComboPrefix = "Combo: ";

    /// <summary>Name of the scenario that is met when any combo line is.</summary>
    public const string AnyComboName = "Any combo line";

    /// <summary>
    /// Adds one scenario per combo line (all pieces, from different cards) and, with two or more lines,
    /// one that is met when any line is. Scenarios whose names are already present are left alone.
    /// </summary>
    private static List<ScenarioDto> WithComboLines(IReadOnlyList<ScenarioDto> scenarios, IReadOnlyList<DeckVersionComboLine> lines)
    {
        static List<RequirementDto> Pieces(DeckVersionComboLine line) =>
            line.Pieces.Select(p => new RequirementDto(MinimumCount: p.Count, Roles: p.Roles, CardIds: p.CardIds)).ToList();

        var result = scenarios.ToList();
        bool Has(string name) => result.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        var ordered = lines.OrderBy(l => l.Position).ToList();

        foreach (var line in ordered)
        {
            if (!Has(ComboPrefix + line.Name))
            {
                result.Add(new ScenarioDto(ComboPrefix + line.Name, Pieces(line), RequireDistinctCards: true));
            }
        }

        if (ordered.Count >= 2 && !Has(AnyComboName))
        {
            result.Add(new ScenarioDto(AnyComboName, AnyOf: ordered.Select(l => new ClauseDto(Pieces(l), RequireDistinctCards: true)).ToList()));
        }

        return result;
    }

    private static List<ComboLineChangeDto> ComboDiff(DeckVersion a, DeckVersion b)
    {
        static string Shape(DeckVersionComboLine l) => JsonSerializer.Serialize(l.Pieces, JsonDefaults.Options);
        var before = a.ComboLines.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);
        var after = b.ComboLines.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

        return before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase)
            .Select(name => (name, change: (before.TryGetValue(name, out var x), after.TryGetValue(name, out var y)) switch
            {
                (false, true) => "added",
                (true, false) => "removed",
                _ when Shape(before[name]) != Shape(after[name]) => "changed",
                _ => null,
            }))
            .Where(c => c.change is not null)
            .OrderBy(c => c.name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new ComboLineChangeDto(after.TryGetValue(c.name, out var l) ? l.Name : before[c.name].Name, c.change!))
            .ToList();
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
        foreach (var scenario in scenarios)
        {
            if ((scenario.Requirements is { Count: > 0 }) == (scenario.AnyOf is { Count: > 0 }))
            {
                throw new RequestValidationException(
                    "simulation.scenario_shape",
                    $"Scenario '{scenario.Name}' needs either a list of requirements or a list of alternatives (anyOf), not both or neither.");
            }
        }

        static RequirementDto NormalizeRequirement(RequirementDto r) => r with
        {
            // Parsing rejects unknown role keys with a clear message and normalises their spelling.
            Role = r.Role is null ? null : CardRole.Parse(r.Role).Key,
            Roles = r.Roles?.Select(role => CardRole.Parse(role).Key).ToList(),
        };

        return config with
        {
            Seed = config.Seed ?? Random.Shared.NextInt64(0, MaxSeed + 1),
            Scenarios = scenarios
                .Select(s => s with
                {
                    Requirements = s.Requirements?.Select(NormalizeRequirement).ToList(),
                    AnyOf = s.AnyOf?.Select(c => c with { Requirements = (c.Requirements ?? []).Select(NormalizeRequirement).ToList() }).ToList(),
                })
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
            config.Scenarios!.Select(ToScenario).ToList(),
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

    /// <summary>Maps an API scenario onto the engine's OR-of-AND model.</summary>
    private static Scenario ToScenario(ScenarioDto s)
    {
        static Requirement ToRequirement(RequirementDto r) => new(
            (r.Role is null ? [] : new[] { r.Role }).Concat(r.Roles ?? []).Distinct(StringComparer.Ordinal).ToList(),
            (r.CardIds ?? []).Distinct().Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList(),
            r.MinimumCount,
            r.MaximumCount);

        return s.AnyOf is { Count: > 0 } alternatives
            ? new Scenario(s.Name, alternatives.Select(c => new Clause((c.Requirements ?? []).Select(ToRequirement).ToList(), c.RequireDistinctCards)).ToList())
            : Scenario.AllOf(s.Name, (s.Requirements ?? []).Select(ToRequirement).ToList(), s.RequireDistinctCards);
    }

    /// <summary>
    /// Snapshots a version's Main Deck. With <paramref name="requireValid"/> (when about to simulate),
    /// an invalid deck is refused; without it (when only checking for changes), any deck is accepted.
    /// </summary>
    private async Task<DeckSnapshot> CreateSnapshotAsync(Deck deck, DeckVersion version, CancellationToken cancellationToken, bool requireValid = true)
    {
        var (validation, used, rules) = await deckService.ValidateAsync(deck, version, cancellationToken);
        if (requireValid && !validation.IsValid)
        {
            var errors = validation.Issues.Where(i => i.Severity == ValidationSeverity.Error).Select(i => i.Message);
            throw new RequestValidationException("simulation.invalid_deck", "Fix the deck before simulating: " + string.Join(" ", errors));
        }

        var main = version.Cards
            .Where(c => c.Zone == DeckZone.Main)
            .OrderBy(c => c.CardId)
            .Select(c => new SnapshotCard(
                c.CardId,
                used.TryGetValue(c.CardId, out var card) ? card.Name : $"Card {c.CardId}",
                c.Quantity,
                version.RolesOf(c.CardId).Select(r => r.Key).Order(StringComparer.Ordinal).ToList()))
            .ToList();

        return new DeckSnapshot(deck.Id, deck.Name, version.Id, version.VersionName, main, deck.Format, rules.Label);
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
