using DuelLab.Simulator.Algorithms;
using DuelLab.Simulator.Analysis;

namespace DuelLab.Simulator.Tests.Analysis;

/// <summary>OR alternatives and card-specific requirements (engine 1.1).</summary>
public sealed class OrAndCardScenarioTests
{
    private readonly SimulationEngine _engine = new(new HandSimulator());

    private static AnalysisCard Card(string key, int quantity, params string[] roles) => new(key, key, quantity, roles);

    // 40 cards: named combo pieces, role-only cards, and filler.
    private static readonly List<AnalysisCard> Deck =
    [
        Card("Ash", 3, "Starter", "HandTrap"),
        Card("Oak", 3, "Starter"),
        Card("Poplar", 2, "Starter", "Extender"),
        Card("Seeker", 3, "Extender"),
        Card("Diabell", 2, "Searcher"),
        Card("Brick", 2, "Brick"),
        .. Enumerable.Range(1, 9).Select(i => Card($"Filler {i}", i <= 7 ? 3 : 2)),
    ];

    private AnalysisResult Run(params Scenario[] scenarios) =>
        _engine.Run(new AnalysisRequest(Deck, 5, 2_000, 99, scenarios));

    [Fact]
    public void Deck_HasFortyCards()
    {
        Assert.Equal(40, Deck.Sum(c => c.Quantity));
    }

    [Fact]
    public void ExactValues_MatchAnIndependentBruteForceOverEveryHand()
    {
        Scenario[] scenarios =
        [
            // (starter AND extender, different cards) OR (2+ starters)
            new("or", [
                new Clause([Requirement.ForRole("Starter"), Requirement.ForRole("Extender")]),
                new Clause([Requirement.ForRole("Starter", 2)]),
            ]),
            // Combo line: (Ash or Oak) AND (Seeker or any searcher), different cards.
            Scenario.AllOf("combo", [Requirement.ForCards(["Ash", "Oak"]), new Requirement(["Searcher"], ["Seeker"])]),
            // At least 2 cards that are starters or searchers, overlapping allowed.
            Scenario.AllOf("multi-role", [new Requirement(["Starter", "Searcher"], [], 2)], RequireDistinctCards: false),
            // A starter and no "Brick" card, OR exactly one Poplar.
            new("max", [
                new Clause([Requirement.ForRole("Starter"), Requirement.ForCards(["Brick"], 0, 0)]),
                new Clause([Requirement.ForCards(["Poplar"], 1, 1)]),
            ]),
            // Same card named twice: needs two different copies.
            Scenario.AllOf("two ash pieces", [Requirement.ForCards(["Ash"]), Requirement.ForCards(["Ash"])]),
        ];

        var result = Run(scenarios);

        var copies = Deck.SelectMany(c => Enumerable.Repeat(c, c.Quantity)).ToArray();
        var hits = new long[scenarios.Length];
        long total = 0;
        foreach (var hand in Combinations(copies.Length, 5))
        {
            total++;
            var cards = hand.Select(i => copies[i]).ToArray();
            for (var s = 0; s < scenarios.Length; s++)
            {
                if (scenarios[s].AnyOf.Any(clause => ReferenceMeets(clause, cards)))
                {
                    hits[s]++;
                }
            }
        }

        Assert.Equal(658_008, total);
        for (var s = 0; s < scenarios.Length; s++)
        {
            Assert.Equal((double)hits[s] / total, result.Scenarios[s].Exact!.Value, precision: 12);
        }
    }

    [Fact]
    public void OrOfTwoAlternatives_IsInclusionExclusionOfThem()
    {
        var a = new Clause([Requirement.ForCards(["Ash"])]);
        var b = new Clause([Requirement.ForCards(["Oak"])]);

        var result = Run(new("A", [a]), new("B", [b]), new("A or B", [a, b]), Scenario.AllOf("A and B", [Requirement.ForCards(["Ash"]), Requirement.ForCards(["Oak"])]));

        var p = result.Scenarios.Select(s => s.Exact!.Value).ToArray();
        Assert.Equal(p[0] + p[1] - p[3], p[2], precision: 12);
        // "Ash or Oak" as one requirement is the same event as the OR of two clauses.
        var single = Run(Scenario.AllOf("one requirement", [Requirement.ForCards(["Ash", "Oak"])]));
        Assert.Equal(p[2], single.Scenarios[0].Exact!.Value, precision: 12);
    }

    [Fact]
    public void CardNotInDeck_MatchesNothing()
    {
        var result = Run(Scenario.AllOf("missing", [Requirement.ForCards(["Not in deck"])]));

        Assert.Equal(0.0, result.Scenarios[0].Exact);
        Assert.Equal(0, result.Scenarios[0].Successes);
    }

    [Fact]
    public void NamingACard_DoesNotChangeOtherScenariosOutcomes()
    {
        var baseline = Run(Scenario.AllOf("starter", [Requirement.ForRole("Starter")]));
        var withCards = Run(Scenario.AllOf("starter", [Requirement.ForRole("Starter")]), Scenario.AllOf("ash", [Requirement.ForCards(["Ash"])]));

        Assert.Equal(baseline.Scenarios[0].Successes, withCards.Scenarios[0].Successes);
        Assert.Equal(baseline.SampleHands.Select(h => string.Join(',', h.CardKeys)), withCards.SampleHands.Select(h => string.Join(',', h.CardKeys)));
    }

    [Fact]
    public void Descriptions_NameCardsAndAlternatives()
    {
        var result = Run(
            new("x", [
                new Clause([Requirement.ForRole("Starter"), Requirement.ForRole("Extender")]),
                new Clause([Requirement.ForRole("Starter", 2)]),
            ]),
            Scenario.AllOf("y", [Requirement.ForCards(["Ash", "Oak"]), new Requirement(["Searcher"], ["Seeker", "Gone"])]),
            Scenario.AllOf("z", [Requirement.ForCards(["Brick"], 0, 0)]));

        Assert.Equal(
            "Hand contains either (at least 1 starter AND at least 1 extender, from different cards), or at least 2 starters",
            result.Scenarios[0].Description);
        Assert.Equal(
            "Hand contains at least 1 of Ash or Oak AND at least 1 of Seeker, a card not in the Main Deck or any searcher (each minimum met by different cards)",
            result.Scenarios[1].Description);
        Assert.Equal("Hand contains none of Brick", result.Scenarios[2].Description);
    }

    [Fact]
    public void MoreThanSixtyFourAtoms_AreSupported()
    {
        // 20 cards × 2 copies, each with its own role. Scenarios refer to 60 roles (40 absent from the
        // deck) and 12 named cards: 72 atoms, beyond a 64-bit mask, while exact enumeration stays small.
        var deck = Enumerable.Range(0, 20).Select(i => Card($"C{i}", 2, $"Custom:R{i}")).ToList();
        var scenarios = Enumerable.Range(0, 6)
            .Select(s => new Scenario($"S{s}", [
                new Clause(Enumerable.Range(0, 10).Select(r => Requirement.ForRole($"Custom:R{s * 10 + r}")).ToList()),
                new Clause([Requirement.ForCards([$"C{s}", $"C{s + 10}"])]),
            ]))
            .ToList();

        var result = _engine.Run(new AnalysisRequest(deck, 5, 500, 1, scenarios));

        // Each scenario: its 10-role clause cannot be met by a 5-card hand, so only "C{s} or C{s+10}"
        // (4 copies of 40) counts.
        var expected = 1 - Combinatorics.Ratio(Combinatorics.Choose(36, 5), Combinatorics.Choose(40, 5));
        Assert.All(result.Scenarios, r => Assert.Equal(expected, r.Exact!.Value, precision: 12));
    }

    public static TheoryData<string, Scenario> InvalidScenarios() => new()
    {
        { "no alternatives", new Scenario("x", []) },
        { "empty alternative", new Scenario("x", [new Clause([])]) },
        { "nothing to look for", Scenario.AllOf("x", [new Requirement([], [])]) },
        { "blank card key", Scenario.AllOf("x", [Requirement.ForCards([""])]) },
        { "too many alternatives", new Scenario("x", Enumerable.Range(0, 17).Select(_ => new Clause([Requirement.ForRole("Starter")])).ToList()) },
    };

    [Theory]
    [MemberData(nameof(InvalidScenarios))]
    public void InvalidScenarios_AreRejected(string reason, Scenario scenario)
    {
        Assert.NotNull(reason);
        Assert.Throws<ArgumentException>(() => Run(scenario));
    }

    /// <summary>
    /// Reference check that does not use Hall's theorem: tries every way of assigning distinct hand cards
    /// to the clause's minimums.
    /// </summary>
    private static bool ReferenceMeets(Clause clause, AnalysisCard[] hand)
    {
        bool Matches(AnalysisCard card, Requirement r) => r.Cards.Contains(card.Key) || r.Roles.Any(card.Roles.Contains);

        foreach (var r in clause.Requirements.Where(r => r.MaximumCount is not null))
        {
            if (hand.Count(c => Matches(c, r)) > r.MaximumCount)
            {
                return false;
            }
        }

        var minimums = clause.Requirements.Where(r => r.MinimumCount > 0).ToList();
        if (!clause.RequireDistinctCards)
        {
            return minimums.All(r => hand.Count(c => Matches(c, r)) >= r.MinimumCount);
        }

        var used = new bool[hand.Length];
        bool Assign(int requirement, int needed)
        {
            if (requirement == minimums.Count)
            {
                return true;
            }

            if (needed == 0)
            {
                return Assign(requirement + 1, requirement + 1 < minimums.Count ? minimums[requirement + 1].MinimumCount : 0);
            }

            for (var i = 0; i < hand.Length; i++)
            {
                if (!used[i] && Matches(hand[i], minimums[requirement]))
                {
                    used[i] = true;
                    if (Assign(requirement, needed - 1))
                    {
                        used[i] = false;
                        return true;
                    }

                    used[i] = false;
                }
            }

            return false;
        }

        return minimums.Count == 0 || Assign(0, minimums[0].MinimumCount);
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
