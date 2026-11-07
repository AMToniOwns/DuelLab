using DuelLab.Simulator.Algorithms;

namespace DuelLab.Simulator.Tests.Algorithms;

public sealed class HandSimulatorTests
{
    private const int DefaultSeed = 12345;
    private readonly HandSimulator _simulator = new();

    /// <summary>One physical copy of a card: the card's identity plus which copy it is.</summary>
    private sealed record CardCopy(string CardName, int CopyNumber);

    /// <summary>
    /// A legal-sized 40-card Main Deck: 13 different cards at 3 copies each (39) plus one single copy.
    /// </summary>
    private static List<CardCopy> Build40CardDeck()
    {
        var deck = new List<CardCopy>();
        for (var card = 1; card <= 13; card++)
        {
            for (var copy = 1; copy <= 3; copy++)
            {
                deck.Add(new CardCopy($"Card {card:00}", copy));
            }
        }

        deck.Add(new CardCopy("Card 14", 1));
        return deck;
    }

    // ---- Basic behaviour --------------------------------------------------------------------

    [Fact]
    public void Draw_FiveFromFortyCardDeck_ReturnsExactlyFiveCards()
    {
        var hand = _simulator.Draw(Build40CardDeck(), 5, new Random(DefaultSeed));

        Assert.Equal(5, hand.Count);
    }

    [Fact]
    public void Draw_ZeroCards_ReturnsEmptyHand()
    {
        var hand = _simulator.Draw(Build40CardDeck(), 0, new Random(DefaultSeed));

        Assert.Empty(hand);
    }

    [Fact]
    public void Draw_ZeroCardsFromEmptyDeck_ReturnsEmptyHand()
    {
        var hand = _simulator.Draw(Array.Empty<CardCopy>(), 0, new Random(DefaultSeed));

        Assert.Empty(hand);
    }

    [Fact]
    public void Draw_EntireDeck_ReturnsAPermutationOfTheDeck()
    {
        var deck = Build40CardDeck();

        var hand = _simulator.Draw(deck, deck.Count, new Random(DefaultSeed));

        Assert.Equal(deck.OrderBy(Key), hand.OrderBy(Key));
    }

    // ---- Argument validation ----------------------------------------------------------------

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Draw_NegativeHandSize_Throws(int handSize)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => _simulator.Draw(Build40CardDeck(), handSize, new Random(DefaultSeed)));

        Assert.Equal("handSize", ex.ParamName);
    }

    [Theory]
    [InlineData(41)]
    [InlineData(int.MaxValue)]
    public void Draw_HandSizeLargerThanDeck_Throws(int handSize)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => _simulator.Draw(Build40CardDeck(), handSize, new Random(DefaultSeed)));

        Assert.Equal("handSize", ex.ParamName);
    }

    [Fact]
    public void Draw_NullDeck_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => _simulator.Draw<CardCopy>(null!, 5, new Random(DefaultSeed)));

        Assert.Equal("deck", ex.ParamName);
    }

    [Fact]
    public void Draw_NullRandom_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => _simulator.Draw(Build40CardDeck(), 5, null!));

        Assert.Equal("random", ex.ParamName);
    }

    // ---- Source deck is never mutated -------------------------------------------------------

    [Fact]
    public void Draw_DoesNotMutateSourceDeck()
    {
        var deck = Build40CardDeck();
        var snapshot = deck.ToList();

        // Many draws from one deck, including a full-deck draw, which touches every position.
        var random = new Random(DefaultSeed);
        for (var i = 0; i < 100; i++)
        {
            _simulator.Draw(deck, 5, random);
        }

        _simulator.Draw(deck, deck.Count, random);

        Assert.Equal(snapshot, deck); // same elements in the same order
    }

    [Fact]
    public void Draw_ReturnsNewCollectionIndependentOfSourceDeck()
    {
        var deck = Build40CardDeck();

        var hand = _simulator.Draw(deck, deck.Count, new Random(DefaultSeed));

        Assert.NotSame(deck, hand);
    }

    // ---- Draw-without-replacement invariants ------------------------------------------------

    [Fact]
    public void Draw_NeverReturnsTheSamePhysicalCopyTwice()
    {
        var deck = Build40CardDeck();
        var random = new Random(DefaultSeed);

        for (var i = 0; i < 10_000; i++)
        {
            var hand = _simulator.Draw(deck, 5, random);

            Assert.Equal(hand.Count, hand.Distinct().Count());
        }
    }

    [Fact]
    public void Draw_AllCardsComeFromTheSourceDeck()
    {
        var deck = Build40CardDeck();
        var deckSet = deck.ToHashSet();
        var random = new Random(DefaultSeed);

        for (var i = 0; i < 1_000; i++)
        {
            Assert.All(_simulator.Draw(deck, 5, random), card => Assert.Contains(card, deckSet));
        }
    }

    [Fact]
    public void Draw_IdenticalCopies_NeverExceedTheirQuantityInTheDeck()
    {
        // Copies modelled as plain names, so the three copies of a card are equal values.
        // A hand must still never contain more copies of a card than the deck holds.
        var deck = Build40CardDeck().Select(c => c.CardName).ToList();
        var deckCounts = deck.CountBy(name => name).ToDictionary();
        var random = new Random(DefaultSeed);

        for (var i = 0; i < 10_000; i++)
        {
            var hand = _simulator.Draw(deck, 5, random);

            foreach (var (name, count) in hand.CountBy(name => name))
            {
                Assert.True(
                    count <= deckCounts[name],
                    $"Hand contained {count} copies of {name}; the deck only has {deckCounts[name]}.");
            }
        }
    }

    [Fact]
    public void Draw_IdenticalCopies_CanAllBeDrawnTogether()
    {
        // With a full-deck draw, all three equal-valued copies must come out.
        var deck = new[] { "Ash", "Ash", "Ash", "Other" };

        var hand = _simulator.Draw(deck, deck.Length, new Random(DefaultSeed));

        Assert.Equal(3, hand.Count(card => card == "Ash"));
    }

    // ---- Reproducibility --------------------------------------------------------------------

    [Fact]
    public void Draw_SameSeedAndInput_ProducesSameHand()
    {
        var deck = Build40CardDeck();

        var first = _simulator.Draw(deck, 5, new Random(DefaultSeed));
        var second = _simulator.Draw(deck, 5, new Random(DefaultSeed));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Draw_SameSeed_ProducesSameSequenceOfHands()
    {
        // A simulation run draws many hands from one Random. The whole sequence must repeat.
        var deck = Build40CardDeck();

        var first = DrawSequence(deck, new Random(DefaultSeed), count: 1_000);
        var second = DrawSequence(deck, new Random(DefaultSeed), count: 1_000);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Draw_DeliberatelyChosenDifferentSeeds_ProduceDifferentHands()
    {
        // Two different seeds do not have to produce different hands. Seeds 1 and 2 are a fixed
        // pair checked to differ, so this test is deterministic.
        var deck = Build40CardDeck();

        var handA = _simulator.Draw(deck, 5, new Random(1));
        var handB = _simulator.Draw(deck, 5, new Random(2));

        Assert.NotEqual(handA, handB);
    }

    // ---- Statistical correctness (fixed seeds, so the results are deterministic) ------------
    //
    // The bounds below are set so a correct sampler would fail them with probability below 0.1%
    // under random seeds. A biased shuffle (for example, the classic random.Next(n) mistake)
    // fails them by a wide margin.

    [Fact]
    public void Draw_FullShuffleOfFourCards_AllPermutationsAreEquallyLikely()
    {
        const int samples = 240_000; // 24 permutations x 10,000 expected each
        var deck = new[] { 'A', 'B', 'C', 'D' };
        var random = new Random(2024);

        var observed = new Dictionary<string, int>();
        for (var i = 0; i < samples; i++)
        {
            var key = new string(_simulator.Draw(deck, 4, random).ToArray());
            observed[key] = observed.GetValueOrDefault(key) + 1;
        }

        Assert.Equal(24, observed.Count);
        // Chi-square critical value: 23 degrees of freedom, alpha = 0.001, is 49.728.
        Assert.InRange(ChiSquare(observed.Values, expectedPerBin: samples / 24.0), 0, 49.728);
    }

    [Fact]
    public void Draw_TwoFromFive_AllUnorderedHandsAreEquallyLikely()
    {
        const int samples = 100_000; // C(5,2) = 10 hands x 10,000 expected each
        var deck = new[] { 'A', 'B', 'C', 'D', 'E' };
        var random = new Random(2025);

        var observed = new Dictionary<string, int>();
        for (var i = 0; i < samples; i++)
        {
            var key = new string(_simulator.Draw(deck, 2, random).Order().ToArray());
            observed[key] = observed.GetValueOrDefault(key) + 1;
        }

        Assert.Equal(10, observed.Count);
        // Chi-square critical value: 9 degrees of freedom, alpha = 0.001, is 27.877.
        Assert.InRange(ChiSquare(observed.Values, expectedPerBin: samples / 10.0), 0, 27.877);
    }

    [Fact]
    public void Draw_AtLeastOneStarterRate_MatchesExactHypergeometricProbability()
    {
        // Worked example from spec section 6.1: 40-card deck, 9 starters, 5-card hand.
        // P(X >= 1) = 1 - C(31,5) / C(40,5) = 1 - 169,911 / 658,008 ≈ 0.741775.
        const int deckSize = 40, starters = 9, handSize = 5, samples = 100_000;
        var exact = 1.0 - (169_911.0 / 658_008.0);

        var deck = Enumerable.Range(0, deckSize).Select(i => i < starters).ToArray(); // true = starter
        var random = new Random(DefaultSeed);

        var successes = 0;
        for (var i = 0; i < samples; i++)
        {
            if (_simulator.Draw(deck, handSize, random).Any(isStarter => isStarter))
            {
                successes++;
            }
        }

        var estimate = (double)successes / samples;
        var standardError = Math.Sqrt(exact * (1 - exact) / samples); // ≈ 0.00138
        Assert.InRange(estimate, exact - (4 * standardError), exact + (4 * standardError));
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static string Key(CardCopy card) => $"{card.CardName}#{card.CopyNumber}";

    private List<IReadOnlyList<CardCopy>> DrawSequence(List<CardCopy> deck, Random random, int count)
    {
        var hands = new List<IReadOnlyList<CardCopy>>(count);
        for (var i = 0; i < count; i++)
        {
            hands.Add(_simulator.Draw(deck, 5, random));
        }

        return hands;
    }

    private static double ChiSquare(IEnumerable<int> observedCounts, double expectedPerBin) =>
        observedCounts.Sum(o => (o - expectedPerBin) * (o - expectedPerBin) / expectedPerBin);
}
