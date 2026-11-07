using System.Net;
using DuelLab.Application.Decks;
using DuelLab.Application.Simulations;
using DuelLab.Domain.Decks;
using DuelLab.Simulator.Analysis;
using Microsoft.EntityFrameworkCore;

namespace DuelLab.Api.Tests;

public sealed class SimulationsApiTests(DuelLabApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Simulate_WithDefaults_ReportsExactAndEstimatedValues()
    {
        var (_, version) = await CreateLegalDeckAsync();

        var run = await PostAsync<SimulationRunDto>($"/api/versions/{version.Id}/simulate", new SimulationConfigDto(SampleCount: 10_000, Seed: 99));

        Assert.Equal(SimulationService.DefaultScenarios.Select(s => s.Name), run.Result.Scenarios.Select(s => s.Name));
        Assert.Equal(42, run.Result.DeckSize);
        Assert.Equal(SimulationService.DefaultSampleHandCount, run.Result.SampleHands.Count);

        // 6 starter copies (Ash + Veiler) in 42 cards: exact P(≥1) from the hypergeometric formula.
        var starter = run.Result.Scenarios.Single(s => s.Name == "Starter");
        Assert.Equal(Combinatorics.HypergeometricAtLeast(42, 6, 5, 1), starter.Exact!.Value, precision: 12);
        Assert.InRange(starter.Exact.Value, starter.ConfidenceInterval.Lower - 0.01, starter.ConfidenceInterval.Upper + 0.01);
    }

    [Fact]
    public async Task Simulate_WithoutSeed_RecordsAJavaScriptSafeSeed()
    {
        var (_, version) = await CreateLegalDeckAsync();

        var run = await PostAsync<SimulationRunDto>($"/api/versions/{version.Id}/simulate", new SimulationConfigDto(SampleCount: 100));

        Assert.InRange(run.Seed, 0, SimulationService.MaxSeed);
        Assert.Equal(run.Seed, run.Configuration.Seed);
    }

    [Fact]
    public async Task StoredRun_ReproducesIdentically()
    {
        var (_, version) = await CreateLegalDeckAsync();
        var run = await PostAsync<SimulationRunDto>($"/api/versions/{version.Id}/simulate", new SimulationConfigDto(SampleCount: 5_000, Seed: 4242));

        var reproduction = await PostAsync<ReproductionDto>($"/api/simulations/{run.Id}/reproduce", null);

        Assert.True(reproduction.Identical);
        Assert.True(reproduction.EngineVersionMatches);
    }

    [Fact]
    public async Task StoredRun_SurvivesLaterEditsAndFlagsThem()
    {
        var (_, version) = await CreateLegalDeckAsync();
        var run = await PostAsync<SimulationRunDto>($"/api/versions/{version.Id}/simulate", new SimulationConfigDto(SampleCount: 1_000, Seed: 1));

        // Remove every role: later runs would differ, but the stored run must not change meaning.
        await PutAsync<VersionDetailDto>($"/api/versions/{version.Id}/roles", new UpdateRolesRequest([]));

        var stored = await GetAsync<SimulationRunDto>($"/api/simulations/{run.Id}");
        var reproduction = await PostAsync<ReproductionDto>($"/api/simulations/{run.Id}/reproduce", null);

        Assert.True(stored.VersionChangedSinceRun);
        Assert.Contains(stored.Snapshot.MainDeck, c => c.Roles.Contains("Starter"));
        Assert.True(reproduction.Identical);
    }

    [Fact]
    public async Task CustomScenarios_DistinctVsOverlappingSemantics()
    {
        var (_, version) = await CreateLegalDeckAsync();
        RequirementDto[] requirements = [new("Starter"), new("HandTrap")];

        var run = await PostAsync<SimulationRunDto>($"/api/versions/{version.Id}/simulate", new SimulationConfigDto(
            SampleCount: 1_000,
            Seed: 5,
            Scenarios:
            [
                new("distinct", requirements, RequireDistinctCards: true),
                new("overlap", requirements, RequireDistinctCards: false),
            ]));

        // Ash is the only hand trap and is also a starter. Overlap: one Ash suffices (any Ash).
        // Distinct: Ash as hand trap plus another starter (a second Ash or a Veiler).
        var overlap = run.Result.Scenarios[1].Exact!.Value;
        var distinct = run.Result.Scenarios[0].Exact!.Value;
        Assert.Equal(Combinatorics.HypergeometricAtLeast(42, 3, 5, 1), overlap, precision: 12);
        Assert.True(distinct < overlap);
    }

    [Fact]
    public async Task InvalidDeck_CannotBeSimulated()
    {
        var deck = await PostAsync<DeckSummaryDto>("/api/decks", new CreateDeckRequest("Too small"));

        var (status, code, detail) = await FailAsync(HttpMethod.Post, $"/api/versions/{deck.Versions[0].Id}/simulate", new SimulationConfigDto());

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("simulation.invalid_deck", code);
        Assert.Contains("at least 40", detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 5, 1L, "simulation.sample_count")]
    [InlineData(100_001, 5, 1L, "simulation.sample_count")]
    [InlineData(100, 0, 1L, "simulation.hand_size")]
    [InlineData(100, 5, -1L, "simulation.seed")]
    [InlineData(100, 5, 9_007_199_254_740_992L, "simulation.seed")]
    public async Task InvalidConfiguration_IsRejected(int samples, int handSize, long seed, string expectedCode)
    {
        var (_, version) = await CreateLegalDeckAsync();

        var (status, code, _) = await FailAsync(HttpMethod.Post, $"/api/versions/{version.Id}/simulate", new SimulationConfigDto(samples, handSize, seed));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(expectedCode, code);
    }

    [Fact]
    public async Task EngineValidationErrors_BecomeBadRequests()
    {
        var (_, version) = await CreateLegalDeckAsync();

        var (status, code, detail) = await FailAsync(HttpMethod.Post, $"/api/versions/{version.Id}/simulate",
            new SimulationConfigDto(100, 5, 1, [new("too many", [new RequirementDto("Starter", 6)])]));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("simulation.invalid", code);
        Assert.DoesNotContain("Parameter", detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compare_UsesTheSameSeedAndReportsDifferences()
    {
        var (deck, v1) = await CreateLegalDeckAsync();
        var v2 = await PostAsync<VersionDetailDto>($"/api/decks/{deck.Id}/versions", new CreateVersionRequest("no bricks", v1.Id));
        var darkMagician = v2.Cards.Single(c => c.Card.Name == "Dark Magician").Card.Id;
        var neos = (await CardAsync("Pot of Prosperity")).Id;
        await PutAsync<VersionDetailDto>($"/api/versions/{v2.Id}/cards", new UpdateCardsRequest(
            [.. v2.Cards.Where(c => c.Card.Id != darkMagician).Select(c => new CardEntryRequest(c.Card.Id, c.Zone, c.Quantity)), new(neos, DeckZone.Main, 1)]));

        var comparison = await PostAsync<ComparisonDto>("/api/versions/compare", new CompareRequest(v1.Id, v2.Id, new SimulationConfigDto(SampleCount: 2_000)));

        Assert.Equal(comparison.Base.Seed, comparison.Candidate.Seed);
        var brick = comparison.Scenarios.Single(s => s.Name == "Brick");
        Assert.Equal(-100 * brick.BaseExact!.Value, brick.ExactDifferencePp!.Value, precision: 9); // no bricks left
        Assert.Contains(comparison.CardChanges, c => c.CardId == darkMagician && c.Delta == -3);
        Assert.Contains(comparison.CardChanges, c => c.CardId == neos && c.Delta == 1);
        Assert.Contains(comparison.RoleChanges, c => c.CardId == darkMagician && c.Removed.Contains("Brick"));

        var history = await GetAsync<List<SimulationRunSummaryDto>>($"/api/versions/{v1.Id}/simulations");
        Assert.Contains(history, h => h.Id == comparison.Base.Id);
    }

    [Fact]
    public async Task Compare_SameVersionIsRejected()
    {
        var (_, v1) = await CreateLegalDeckAsync();

        var (_, code, _) = await FailAsync(HttpMethod.Post, "/api/versions/compare", new CompareRequest(v1.Id, v1.Id, null));

        Assert.Equal("compare.same_version", code);
    }

    [Fact]
    public async Task RecentRunsAndDefaultScenarios_AreListed()
    {
        var (_, version) = await CreateLegalDeckAsync("Recent");
        var run = await PostAsync<SimulationRunDto>($"/api/versions/{version.Id}/simulate", new SimulationConfigDto(SampleCount: 100));

        var recent = await GetAsync<List<SimulationRunSummaryDto>>("/api/simulations?take=50");
        var defaults = await GetAsync<List<ScenarioDto>>("/api/simulations/default-scenarios");

        Assert.Contains(recent, r => r.Id == run.Id && r.DeckName == "Recent");
        Assert.Equal(SimulationService.DefaultScenarios.Count, defaults.Count);
    }

    [Fact]
    public async Task Health_ReportsDatabaseConnectivity()
    {
        var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public sealed class ReproductionTamperTests(DuelLabApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Reproduce_DetectsChangedOutcomesButIgnoresDescriptionWording()
    {
        var (_, version) = await CreateLegalDeckAsync();
        var run = await PostAsync<SimulationRunDto>($"/api/versions/{version.Id}/simulate", new SimulationConfigDto(SampleCount: 500, Seed: 3));

        // Reword a description: still identical.
        await Factory.WithDbAsync(db => db.Database.ExecuteSqlRawAsync(
            "UPDATE simulation_runs SET \"ResultsJson\" = replace(\"ResultsJson\", 'Hand contains', 'The hand holds') WHERE \"Id\" = {0}", run.Id));
        Assert.True((await PostAsync<ReproductionDto>($"/api/simulations/{run.Id}/reproduce", null)).Identical);

        // Change an outcome: detected.
        var successes = run.Result.Scenarios[0].Successes;
        await Factory.WithDbAsync(db => db.Database.ExecuteSqlRawAsync(
            "UPDATE simulation_runs SET \"ResultsJson\" = replace(\"ResultsJson\", {1}, {2}) WHERE \"Id\" = {0}",
            run.Id, $"\"successes\":{successes},", $"\"successes\":{successes + 1},"));
        Assert.False((await PostAsync<ReproductionDto>($"/api/simulations/{run.Id}/reproduce", null)).Identical);
    }
}
