using System.Net;
using DuelLab.Application.Decks;
using DuelLab.Application.Simulations;
using DuelLab.Domain.Decks;
using DuelLab.Simulator.Analysis;

namespace DuelLab.Api.Tests;

public sealed class ComboLinesApiTests(DuelLabApiFactory factory) : ApiTestBase(factory)
{
    private static int Id(VersionDetailDto v, string name) => v.Cards.First(c => c.Card.Name == name).Card.Id;

    // P(at least one of an a-copy group AND at least one of a disjoint b-copy group), by inclusion–exclusion:
    // 1 − P(no A) − P(no B) + P(neither).
    private static double BothOf(int a, int b, int deck = 42, int hand = 5)
    {
        double None(int k) => Combinatorics.Ratio(Combinatorics.Choose(deck - k, hand), Combinatorics.Choose(deck, hand));
        return 1 - None(a) - None(b) + None(a + b);
    }

    [Fact]
    public async Task ComboLines_RoundTripAndAreSimulatedWithExactOdds()
    {
        var (_, version) = await CreateLegalDeckAsync();
        var ash = Id(version, "Ash Blossom & Joyous Spring");
        var veiler = Id(version, "Effect Veiler");
        var imperm = Id(version, "Infinite Impermanence");

        var saved = await PutAsync<VersionDetailDto>($"/api/versions/{version.Id}/combos", new UpdateComboLinesRequest(
        [
            new("Ash + Veiler", "Both hand traps", [new([ash], []), new([veiler], [])]),
            new("Imperm + any starter", null, [new([imperm], []), new([], ["Starter"])]),
        ]));
        Assert.Equal(["Ash + Veiler", "Imperm + any starter"], saved.ComboLines.Select(l => l.Name));

        var run = await PostAsync<SimulationRunDto>($"/api/versions/{version.Id}/simulate", new SimulationConfigDto(SampleCount: 2_000, Seed: 8));

        var names = run.Result.Scenarios.Select(s => s.Name).ToList();
        Assert.Equal(SimulationService.DefaultScenarios.Count + 3, names.Count);
        Assert.Equal(["Combo: Ash + Veiler", "Combo: Imperm + any starter", "Any combo line"], names[^3..]);

        var ashVeiler = run.Result.Scenarios.Single(s => s.Name == "Combo: Ash + Veiler");
        Assert.Equal(BothOf(3, 3), ashVeiler.Exact!.Value, precision: 12);
        // Imperm + a starter: starters are Ash and Veiler (6 copies), none of which is Imperm.
        Assert.Equal(BothOf(3, 6), run.Result.Scenarios.Single(s => s.Name == "Combo: Imperm + any starter").Exact!.Value, precision: 12);
        // "Any" is at least as likely as each line and at most their sum.
        var any = run.Result.Scenarios.Single(s => s.Name == "Any combo line").Exact!.Value;
        Assert.InRange(any, BothOf(3, 6), BothOf(3, 6) + BothOf(3, 3));

        // The run is reproducible from its stored, expanded scenarios.
        Assert.True((await PostAsync<ReproductionDto>($"/api/simulations/{run.Id}/reproduce", null)).Identical);
    }

    [Fact]
    public async Task ComboLines_CanBeLeftOut()
    {
        var (_, version) = await CreateLegalDeckAsync();
        await PutAsync<VersionDetailDto>($"/api/versions/{version.Id}/combos", new UpdateComboLinesRequest(
            [new("Line", null, [new([Id(version, "Raigeki")], [])])]));

        var run = await PostAsync<SimulationRunDto>($"/api/versions/{version.Id}/simulate", new SimulationConfigDto(SampleCount: 100, IncludeComboLines: false));

        Assert.DoesNotContain(run.Result.Scenarios, s => s.Name.StartsWith("Combo:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvalidComboLine_IsRejected()
    {
        var (_, version) = await CreateLegalDeckAsync();

        var (status, code, _) = await FailAsync(HttpMethod.Put, $"/api/versions/{version.Id}/combos",
            new UpdateComboLinesRequest([new("Bad", null, [new([123456], [])])]));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("combo.card_not_in_version", code);
    }

    [Fact]
    public async Task DuplicatedVersion_KeepsComboLines_AndCompareMeasuresTheSameLinesOnBoth()
    {
        var (deck, v1) = await CreateLegalDeckAsync();
        var ash = Id(v1, "Ash Blossom & Joyous Spring");
        var veiler = Id(v1, "Effect Veiler");
        await PutAsync<VersionDetailDto>($"/api/versions/{v1.Id}/combos", new UpdateComboLinesRequest([new("Ash + Veiler", null, [new([ash], []), new([veiler], [])])]));

        var v2 = await PostAsync<VersionDetailDto>($"/api/decks/{deck.Id}/versions", new CreateVersionRequest("v2", v1.Id));
        Assert.Equal("Ash + Veiler", Assert.Single(v2.ComboLines).Name);

        // v2: one fewer Veiler, plus a line only v2 has.
        var cards = v2.Cards.Select(c => new CardEntryRequest(c.Card.Id, c.Zone, c.Card.Id == veiler ? 2 : c.Quantity)).ToList();
        cards.Add(new((await CardAsync("Pot of Prosperity")).Id, DeckZone.Main, 1));
        await PutAsync<VersionDetailDto>($"/api/versions/{v2.Id}/cards", new UpdateCardsRequest(cards));
        await PutAsync<VersionDetailDto>($"/api/versions/{v2.Id}/combos", new UpdateComboLinesRequest(
        [
            new("Ash + Veiler", null, [new([ash], []), new([veiler], [])]),
            new("Ash alone", null, [new([ash], [])]),
        ]));

        var comparison = await PostAsync<ComparisonDto>("/api/versions/compare", new CompareRequest(v1.Id, v2.Id, new SimulationConfigDto(SampleCount: 1_000)));

        Assert.Equal(comparison.Base.Result.Scenarios.Select(s => s.Name), comparison.Candidate.Result.Scenarios.Select(s => s.Name));
        var line = comparison.Scenarios.Single(s => s.Name == "Combo: Ash + Veiler");
        Assert.Equal(BothOf(3, 3), line.BaseExact!.Value, precision: 12);
        Assert.Equal(BothOf(3, 2), line.CandidateExact!.Value, precision: 12);
        Assert.True(line.ExactDifferencePp < 0);
        Assert.Contains(comparison.Scenarios, s => s.Name == "Combo: Ash alone");
        Assert.Equal([new ComboLineChangeDto("Ash alone", "added")], comparison.ComboLineChanges);
    }

    [Fact]
    public async Task OrScenario_AndMultiTargetRequirement_ViaApi()
    {
        var (_, version) = await CreateLegalDeckAsync();
        var raigeki = Id(version, "Raigeki");

        var run = await PostAsync<SimulationRunDto>($"/api/versions/{version.Id}/simulate", new SimulationConfigDto(
            SampleCount: 500,
            Seed: 2,
            IncludeComboLines: false,
            Scenarios:
            [
                new("Starter or Raigeki", [new RequirementDto(Roles: ["Starter"], CardIds: [raigeki])]),
                new("Starter, or else Raigeki", AnyOf: [new ClauseDto([new("Starter")]), new ClauseDto([new(CardIds: [raigeki])])]),
            ]));

        // 6 starters + 3 Raigeki = 9 matching copies; the OR of clauses is the same event.
        var expected = Combinatorics.HypergeometricAtLeast(42, 9, 5, 1);
        Assert.Equal(expected, run.Result.Scenarios[0].Exact!.Value, precision: 12);
        Assert.Equal(expected, run.Result.Scenarios[1].Exact!.Value, precision: 12);
        Assert.Contains("Raigeki", run.Result.Scenarios[0].Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScenarioWithBothShapes_IsRejected()
    {
        var (_, version) = await CreateLegalDeckAsync();

        var (_, code, _) = await FailAsync(HttpMethod.Post, $"/api/versions/{version.Id}/simulate", new SimulationConfigDto(
            Scenarios: [new("both", [new("Starter")], AnyOf: [new ClauseDto([new("Starter")])])]));

        Assert.Equal("simulation.scenario_shape", code);
    }
}
