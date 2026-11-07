using System.Text.Json;
using DuelLab.Simulator.Algorithms;
using DuelLab.Simulator.Analysis;

namespace DuelLab.Simulator.Tests.Analysis;

public sealed class SimulationEngineTests
{
    private readonly SimulationEngine _engine = new(new HandSimulator());

    private static AnalysisCard Card(string key, int quantity, params string[] roles) => new(key, key, quantity, roles);

    /// <summary>Fills a deck up to <paramref name="size"/> with role-less filler cards (3 copies each).</summary>
    private static List<AnalysisCard> Deck(int size, params AnalysisCard[] cards)
    {
        var deck = cards.ToList();
        var filler = 0;
        while (deck.Sum(c => c.Quantity) < size)
        {
            deck.Add(Card($"Filler {++filler}", Math.Min(3, size - deck.Sum(c => c.Quantity))));
        }

        return deck;
    }

    private static Scenario AtLeast(string role, int count = 1) => Scenario.AllOf($"{count}+ {role}", [Requirement.ForRole(role, count)]);

    private static AnalysisRequest Request(IReadOnlyList<AnalysisCard> deck, IReadOnlyList<Scenario> scenarios, int samples = 100_000, long seed = 12345, int handSize = 5) =>
        new(deck, handSize, samples, seed, scenarios);

    // ---- Exact probabilities ----------------------------------------------------------------

    [Fact]
    public void SingleRole_ExactMatchesSpecFormula()
    {
        var deck = Deck(40, Card("S1", 3, "Starter"), Card("S2", 3, "Starter"), Card("S3", 3, "Starter"));

        var result = _engine.Run(Request(deck, [AtLeast("Starter")]));

        var expected = 1.0 - (169_911.0 / 658_008.0); // 1 - C(31,5)/C(40,5)
        Assert.Equal(expected, result.Scenarios[0].Exact!.Value, precision: 12);
    }

    [Fact]
    public void TwoOrMoreStarters_ExactMatchesHypergeometric()
    {
        var deck = Deck(40, Card("S1", 3, "Starter"), Card("S2", 3, "Starter"), Card("S3", 3, "Starter"));

        var result = _engine.Run(Request(deck, [AtLeast("Starter", 2)]));

        Assert.Equal(Combinatorics.HypergeometricAtLeast(40, 9, 5, 2), result.Scenarios[0].Exact!.Value, precision: 12);
    }

    [Fact]
    public void DistinctSemantics_OneDualRoleCardCannotSatisfyBothRequirements()
    {
        // The only starters/extenders are 3 copies of one card that is both.
        var deck = Deck(40, Card("Both", 3, "Starter", "Extender"));
        var requirements = new[] { Requirement.ForRole("Starter"), Requirement.ForRole("Extender") };

        var result = _engine.Run(Request(deck,
        [
            Scenario.AllOf("distinct", requirements, RequireDistinctCards: true),
            Scenario.AllOf("overlapping", requirements, RequireDistinctCards: false),
        ]));

        // Distinct: needs two copies of "Both". Overlapping: one copy is enough.
        Assert.Equal(Combinatorics.HypergeometricAtLeast(40, 3, 5, 2), result.Scenarios[0].Exact!.Value, precision: 12);
        Assert.Equal(Combinatorics.HypergeometricAtLeast(40, 3, 5, 1), result.Scenarios[1].Exact!.Value, precision: 12);
    }

    [Fact]
    public void DistinctSemantics_ExactMatchesBruteForceOverEveryHand()
    {
        // 6 starters, 4 extenders, 3 dual-role cards, 2 bricks, 25 others = 40 cards.
        var deck = Deck(40,
            Card("S1", 3, "Starter"), Card("S2", 3, "Starter"),
            Card("E1", 2, "Extender"), Card("E2", 2, "Extender"),
            Card("B", 3, "Starter", "Extender"),
            Card("Brick", 2, "Brick"));
        var scenarios = new[]
        {
            Scenario.AllOf("S+E distinct", [Requirement.ForRole("Starter"), Requirement.ForRole("Extender")], true),
            Scenario.AllOf("S+E overlap", [Requirement.ForRole("Starter"), Requirement.ForRole("Extender")], false),
            Scenario.AllOf("2 starters, no brick", [Requirement.ForRole("Starter", 2), Requirement.ForRole("Brick", 0, 0)], true),
        };

        var result = _engine.Run(Request(deck, scenarios, samples: 1_000));

        // Reference: check all C(40,5) = 658,008 hands with a deliberately simple algorithm.
        var copies = deck.SelectMany(c => Enumerable.Repeat(c.Roles.ToHashSet(), c.Quantity)).ToArray();
        var hits = new long[3];
        long total = 0;
        foreach (var hand in Combinations(copies.Length, 5))
        {
            total++;
            var cards = hand.Select(i => copies[i]).ToArray();
            var anyPair = cards.Where((c, i) => c.Contains("Starter") && cards.Where((_, j) => j != i).Any(d => d.Contains("Extender"))).Any();
            var both = cards.Any(c => c.Contains("Starter")) && cards.Any(c => c.Contains("Extender"));
            var twoStartersNoBrick = cards.Count(c => c.Contains("Starter")) >= 2 && !cards.Any(c => c.Contains("Brick"));
            if (anyPair) hits[0]++;
            if (both) hits[1]++;
            if (twoStartersNoBrick) hits[2]++;
        }

        Assert.Equal(658_008, total);
        for (var s = 0; s < 3; s++)
        {
            Assert.Equal((double)hits[s] / total, result.Scenarios[s].Exact!.Value, precision: 12);
        }
    }

    [Fact]
    public void MaximumCount_NoBrickIsComplementOfAtLeastOneBrick()
    {
        var deck = Deck(40, Card("Brick", 3, "Brick"), Card("Brick2", 2, "Brick"));

        var result = _engine.Run(Request(deck,
        [
            Scenario.AllOf("no brick", [Requirement.ForRole("Brick", 0, 0)]),
            AtLeast("Brick"),
        ]));

        Assert.Equal(1.0, result.Scenarios[0].Exact!.Value + result.Scenarios[1].Exact!.Value, precision: 12);
        Assert.Equal(result.SampleCount, result.Scenarios[0].Successes + result.Scenarios[1].Successes);
    }

    [Fact]
    public void RoleThatNoCardHas_HasZeroProbability()
    {
        var result = _engine.Run(Request(Deck(40), [AtLeast("HandTrap")], samples: 1_000));

        Assert.Equal(0.0, result.Scenarios[0].Exact);
        Assert.Equal(0, result.Scenarios[0].Successes);
    }

    [Fact]
    public void TooManyCompositions_ReportsExactAsUnavailableInsteadOfHanging()
    {
        // 60 single cards, each with its own role, and a 10-card hand: C(60,10) ≈ 7.5e10 compositions.
        var deck = Enumerable.Range(0, 60).Select(i => Card($"C{i}", 1, $"Custom:R{i}")).ToList();
        var scenarios = Enumerable.Range(0, 20)
            .Select(s => Scenario.AllOf($"S{s}", Enumerable.Range(0, 3).Select(r => Requirement.ForRole($"Custom:R{s * 3 + r}")).ToList()))
            .ToList();

        var result = _engine.Run(Request(deck, scenarios, samples: 100, handSize: 10));

        Assert.All(result.Scenarios, s => Assert.Null(s.Exact));
    }

    // ---- Monte Carlo agrees with exact ------------------------------------------------------

    [Fact]
    public void Estimates_AreWithinFourStandardErrorsOfExact()
    {
        var deck = Deck(40,
            Card("S1", 3, "Starter"), Card("S2", 3, "Starter"), Card("S3", 2, "Starter", "Extender"),
            Card("E1", 3, "Extender"), Card("Brick", 2, "Brick"));
        var scenarios = new[]
        {
            AtLeast("Starter"), AtLeast("Starter", 2), AtLeast("Extender"), AtLeast("Brick"),
            Scenario.AllOf("S+E", [Requirement.ForRole("Starter"), Requirement.ForRole("Extender")]),
        };

        var result = _engine.Run(Request(deck, scenarios));

        Assert.All(result.Scenarios, s =>
        {
            var p = s.Exact!.Value;
            var se = Math.Sqrt(p * (1 - p) / s.Samples);
            Assert.InRange(s.Estimate, p - (4 * se), p + (4 * se));
            Assert.InRange(p, s.ConfidenceInterval.Lower - (2 * se), s.ConfidenceInterval.Upper + (2 * se));
        });
        Assert.All(result.CardFrequencies, f =>
        {
            var se = Math.Sqrt(f.Exact * (1 - f.Exact) / result.SampleCount);
            Assert.InRange(f.Estimate, f.Exact - (4 * se), f.Exact + (4 * se));
        });
    }

    [Fact]
    public void CardFrequency_ExactIsHypergeometric()
    {
        var deck = Deck(40, Card("Ash", 3, "HandTrap"), Card("Single", 1));

        var result = _engine.Run(Request(deck, [], samples: 100));

        Assert.Equal(Combinatorics.HypergeometricAtLeast(40, 3, 5, 1), result.CardFrequencies.Single(f => f.Key == "Ash").Exact, precision: 12);
        Assert.Equal(5.0 / 40.0, result.CardFrequencies.Single(f => f.Key == "Single").Exact, precision: 12);
    }

    // ---- Reproducibility --------------------------------------------------------------------

    [Fact]
    public void SameRequest_GivesIdenticalResults()
    {
        var deck = Deck(40, Card("S", 3, "Starter"));
        var request = Request(deck, [AtLeast("Starter")], samples: 10_000);

        var first = JsonSerializer.Serialize(_engine.Run(request));
        var second = JsonSerializer.Serialize(_engine.Run(request));

        Assert.Equal(first, second);
    }

    [Fact]
    public void DeliberatelyChosenDifferentSeeds_GiveDifferentEstimates()
    {
        var deck = Deck(40, Card("S", 3, "Starter"));

        var a = _engine.Run(Request(deck, [AtLeast("Starter")], samples: 10_000, seed: 1));
        var b = _engine.Run(Request(deck, [AtLeast("Starter")], samples: 10_000, seed: 2));

        Assert.NotEqual(a.Scenarios[0].Successes, b.Scenarios[0].Successes);
    }

    [Fact]
    public void FirstSampleHand_IsPinnedForEngineVersion()
    {
        // Pins the engine's seeded output. If this fails, a change altered seeded results: bump
        // SimulationEngine.EngineVersion and update the expected hand.
        var deck = Enumerable.Range(1, 40).Select(i => Card($"C{i:00}", 1)).ToList();

        var result = _engine.Run(Request(deck, [], samples: 1, seed: 12345));

        Assert.Equal("1.1.0", SimulationEngine.EngineVersion);
        Assert.Equal(PinnedHand, result.SampleHands[0].CardKeys);
    }

    // Confirmed independently with a Python port of xoshiro256**, Lemire's bounded sampling and the
    // partial Fisher–Yates loop.
    private static readonly string[] PinnedHand = ["C30", "C07", "C39", "C05", "C24"];

    // ---- Output shape and validation ----------------------------------------------------------

    [Fact]
    public void SampleHands_AreLimitedAndCarryOutcomes()
    {
        var deck = Deck(40, Card("S", 3, "Starter"));

        var result = _engine.Run(Request(deck, [AtLeast("Starter"), AtLeast("Starter", 2)], samples: 1_000) with { SampleHandCount = 7 });

        Assert.Equal(7, result.SampleHands.Count);
        Assert.All(result.SampleHands, h =>
        {
            Assert.Equal(5, h.CardKeys.Count);
            Assert.Equal(2, h.ScenarioOutcomes.Count);
            Assert.Equal(h.CardKeys.Contains("S"), h.ScenarioOutcomes[0]);
        });
    }

    [Fact]
    public void Description_StatesTheSemantics()
    {
        var result = _engine.Run(Request(Deck(40), [
            Scenario.AllOf("x", [Requirement.ForRole("Starter"), Requirement.ForRole("Extender")], RequireDistinctCards: true),
            Scenario.AllOf("y", [Requirement.ForRole("Brick", 0, 0)]),
            Scenario.AllOf("z", [Requirement.ForRole("Starter", 2), Requirement.ForRole("Custom:Fusion line", 0, 1)], RequireDistinctCards: false),
        ], samples: 10));

        Assert.Equal("Hand contains at least 2 starters AND at most 1 \"Fusion line\" card", result.Scenarios[2].Description);

        Assert.Equal("Hand contains at least 1 starter AND at least 1 extender (each minimum met by different cards)", result.Scenarios[0].Description);
        Assert.Equal("Hand contains no bricks", result.Scenarios[1].Description);
    }

    public static TheoryData<string, AnalysisRequest> InvalidRequests()
    {
        var deck = Deck(40, Card("S", 3, "Starter"));
        var ok = Request(deck, [AtLeast("Starter")], samples: 10);
        return new()
        {
            { "hand too big", ok with { HandSize = 41 } },
            { "hand zero", ok with { HandSize = 0 } },
            { "no samples", ok with { SampleCount = 0 } },
            { "too many samples", ok with { SampleCount = SimulationEngine.MaxSampleCount + 1 } },
            { "empty deck", ok with { Deck = [] } },
            { "duplicate keys", ok with { Deck = [Card("A", 1), Card("A", 1)] } },
            { "zero quantity", ok with { Deck = [Card("A", 0)] } },
            { "min above hand", ok with { Scenarios = [AtLeast("Starter", 6)] } },
            { "max below min", ok with { Scenarios = [Scenario.AllOf("x", [Requirement.ForRole("Starter", 2, 1)])] } },
            { "no-op requirement", ok with { Scenarios = [Scenario.AllOf("x", [Requirement.ForRole("Starter", 0)])] } },
            { "no requirements", ok with { Scenarios = [Scenario.AllOf("x", [])] } },
            { "negative sample hands", ok with { SampleHandCount = -1 } },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void InvalidRequests_AreRejected(string reason, AnalysisRequest request)
    {
        Assert.NotNull(reason);
        Assert.Throws<ArgumentException>(() => _engine.Run(request));
    }

    private static IEnumerable<int[]> Combinations(int n, int k)
    {
        var indices = Enumerable.Range(0, k).ToArray();
        while (true)
        {
            yield return indices;
            var i = k - 1;
            while (i >= 0 && indices[i] == n - k + i)
            {
                i--;
            }

            if (i < 0)
            {
                yield break;
            }

            indices[i]++;
            for (var j = i + 1; j < k; j++)
            {
                indices[j] = indices[j - 1] + 1;
            }
        }
    }
}
