using DuelLab.Simulator.Algorithms;
using DuelLab.Simulator.Randomness;

namespace DuelLab.Simulator.Analysis;

/// <summary>
/// Runs an opening-hand analysis: a seeded Monte Carlo simulation plus exact probabilities for the same
/// scenarios, so every estimate can be checked against the true value.
/// </summary>
public sealed class SimulationEngine
{
    /// <summary>
    /// The version of the sampling and evaluation logic. Bump it whenever a change could alter the
    /// results of a seeded run, so stored runs are never silently reinterpreted.
    /// </summary>
    public const string EngineVersion = "1.0.0";

    /// <summary>Largest supported sample count.</summary>
    public const int MaxSampleCount = 1_000_000;

    /// <summary>Largest supported opening hand.</summary>
    public const int MaxHandSize = 20;

    /// <summary>Largest number of scenarios in one run.</summary>
    public const int MaxScenarios = 20;

    /// <summary>Largest number of sample hands returned.</summary>
    public const int MaxSampleHands = 100;

    /// <summary>
    /// Exact enumeration is skipped when it would visit more than this many distinct hand compositions;
    /// the exact value is then reported as unavailable. Typical decks need a few hundred.
    /// </summary>
    public const long MaxExactCompositions = 2_000_000;

    private readonly HandSimulator _handSimulator;

    /// <summary>Creates the engine.</summary>
    public SimulationEngine(HandSimulator handSimulator)
    {
        _handSimulator = handSimulator ?? throw new ArgumentNullException(nameof(handSimulator));
    }

    /// <summary>Runs the analysis described by <paramref name="request"/>.</summary>
    /// <exception cref="ArgumentException">The request is invalid; the message says why.</exception>
    public AnalysisResult Run(AnalysisRequest request)
    {
        Validate(request);

        var cards = request.Deck;
        var deckSize = cards.Sum(c => c.Quantity);

        // Give each role used by a scenario a bit. Roles no scenario mentions do not matter here.
        var roleBits = request.Scenarios
            .SelectMany(s => s.Requirements)
            .Select(r => r.Role)
            .Distinct(StringComparer.Ordinal)
            .Select((role, index) => (role, index))
            .ToDictionary(x => x.role, x => x.index, StringComparer.Ordinal);

        // Group cards by the set of relevant roles they carry.
        var cardMasks = cards.Select(c => c.Roles.Where(roleBits.ContainsKey).Aggregate(0UL, (m, r) => m | (1UL << roleBits[r]))).ToArray();
        var groupMasks = cardMasks.Distinct().ToArray();
        var groupOfCard = cardMasks.Select(m => Array.IndexOf(groupMasks, m)).ToArray();
        var groupSizes = new int[groupMasks.Length];
        for (var i = 0; i < cards.Count; i++)
        {
            groupSizes[groupOfCard[i]] += cards[i].Quantity;
        }

        var evaluator = new HandEvaluator(groupMasks, request.Scenarios, roleBits);
        var exact = ExactProbabilities(evaluator, groupSizes, deckSize, request.HandSize);

        // Expand the deck into physical copies, identified by their card's index.
        var copies = new int[deckSize];
        var position = 0;
        for (var i = 0; i < cards.Count; i++)
        {
            copies.AsSpan(position, cards[i].Quantity).Fill(i);
            position += cards[i].Quantity;
        }

        var random = new Xoshiro256StarStarRandom(request.Seed);
        var scenarioSuccesses = new int[evaluator.ScenarioCount];
        var cardHits = new int[cards.Count];
        var lastSeen = new int[cards.Count];
        Array.Fill(lastSeen, -1);
        var groupCounts = new int[groupMasks.Length];
        var sampleHands = new List<SampleHand>(Math.Min(request.SampleHandCount, request.SampleCount));

        for (var sample = 0; sample < request.SampleCount; sample++)
        {
            var hand = _handSimulator.Draw(copies, request.HandSize, random);

            Array.Clear(groupCounts);
            foreach (var cardIndex in hand)
            {
                groupCounts[groupOfCard[cardIndex]]++;
                if (lastSeen[cardIndex] != sample)
                {
                    lastSeen[cardIndex] = sample;
                    cardHits[cardIndex]++;
                }
            }

            var keep = sample < request.SampleHandCount;
            var outcomes = keep ? new bool[evaluator.ScenarioCount] : null;
            for (var s = 0; s < evaluator.ScenarioCount; s++)
            {
                var met = evaluator.Meets(s, groupCounts);
                if (met)
                {
                    scenarioSuccesses[s]++;
                }

                if (outcomes is not null)
                {
                    outcomes[s] = met;
                }
            }

            if (outcomes is not null)
            {
                sampleHands.Add(new SampleHand(sample, hand.Select(i => cards[i].Key).ToArray(), outcomes));
            }
        }

        var scenarioResults = request.Scenarios.Select((scenario, s) => new ScenarioResult(
            scenario.Name,
            HandEvaluator.Describe(scenario),
            scenarioSuccesses[s],
            request.SampleCount,
            (double)scenarioSuccesses[s] / request.SampleCount,
            Statistics.Wilson95(scenarioSuccesses[s], request.SampleCount),
            exact?[s])).ToList();

        var frequencies = cards.Select((card, i) => new CardFrequency(
            card.Key,
            card.Name,
            card.Quantity,
            cardHits[i],
            (double)cardHits[i] / request.SampleCount,
            Combinatorics.HypergeometricAtLeast(deckSize, card.Quantity, request.HandSize, 1))).ToList();

        return new AnalysisResult(
            EngineVersion,
            Xoshiro256StarStarRandom.AlgorithmName,
            request.Seed,
            deckSize,
            request.HandSize,
            request.SampleCount,
            scenarioResults,
            frequencies,
            sampleHands);
    }

    /// <summary>
    /// Exact probability of each scenario, by enumerating every possible count of each role group in the
    /// hand (a multivariate hypergeometric distribution). Returns null if there are too many compositions.
    /// </summary>
    private static double[]? ExactProbabilities(HandEvaluator evaluator, int[] groupSizes, int deckSize, int handSize)
    {
        if (CountCompositions(groupSizes, handSize) > MaxExactCompositions)
        {
            return null;
        }

        var favourable = new UInt128[evaluator.ScenarioCount];
        var counts = new int[groupSizes.Length];

        void Enumerate(int group, int remaining, UInt128 ways)
        {
            if (group == groupSizes.Length - 1)
            {
                if (remaining > groupSizes[group])
                {
                    return;
                }

                counts[group] = remaining;
                ways *= Combinatorics.Choose(groupSizes[group], remaining);
                for (var s = 0; s < favourable.Length; s++)
                {
                    if (evaluator.Meets(s, counts))
                    {
                        favourable[s] += ways;
                    }
                }

                return;
            }

            for (var take = 0; take <= Math.Min(remaining, groupSizes[group]); take++)
            {
                counts[group] = take;
                Enumerate(group + 1, remaining - take, ways * Combinatorics.Choose(groupSizes[group], take));
            }
        }

        Enumerate(0, handSize, UInt128.One);

        var total = Combinatorics.Choose(deckSize, handSize);
        return favourable.Select(f => Combinatorics.Ratio(f, total)).ToArray();
    }

    /// <summary>
    /// The number of vectors (x₀..x_T) with 0 ≤ xᵢ ≤ groupSizes[i] and Σxᵢ = handSize, i.e. how many
    /// compositions exact enumeration would visit. Saturates at <see cref="long.MaxValue"/>.
    /// </summary>
    private static long CountCompositions(int[] groupSizes, int handSize)
    {
        var ways = new long[handSize + 1];
        ways[0] = 1;
        foreach (var size in groupSizes)
        {
            var next = new long[handSize + 1];
            for (var total = 0; total <= handSize; total++)
            {
                for (var take = 0; take <= Math.Min(size, total); take++)
                {
                    next[total] = SaturatingAdd(next[total], ways[total - take]);
                }
            }

            ways = next;
        }

        return ways[handSize];

        static long SaturatingAdd(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
    }

    private static void Validate(AnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Deck is null || request.Deck.Count == 0)
        {
            throw new ArgumentException("The deck is empty.", nameof(request));
        }

        if (request.Deck.Any(c => c is null || c.Quantity < 1 || string.IsNullOrEmpty(c.Key) || c.Roles is null))
        {
            throw new ArgumentException("Every card needs a key, roles and a quantity of at least 1.", nameof(request));
        }

        if (request.Deck.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count() != request.Deck.Count)
        {
            throw new ArgumentException("Card keys must be unique.", nameof(request));
        }

        var deckSize = request.Deck.Sum(c => c.Quantity);
        if (deckSize > Combinatorics.MaxN)
        {
            throw new ArgumentException($"The deck can have at most {Combinatorics.MaxN} cards.", nameof(request));
        }

        if (request.HandSize < 1 || request.HandSize > Math.Min(MaxHandSize, deckSize))
        {
            throw new ArgumentException(
                $"Hand size must be between 1 and {Math.Min(MaxHandSize, deckSize)} for a {deckSize}-card deck.",
                nameof(request));
        }

        if (request.SampleCount < 1 || request.SampleCount > MaxSampleCount)
        {
            throw new ArgumentException($"Sample count must be between 1 and {MaxSampleCount:N0}.", nameof(request));
        }

        if (request.SampleHandCount < 0 || request.SampleHandCount > MaxSampleHands)
        {
            throw new ArgumentException($"Sample hand count must be between 0 and {MaxSampleHands}.", nameof(request));
        }

        if (request.Scenarios is null || request.Scenarios.Count > MaxScenarios)
        {
            throw new ArgumentException($"Provide at most {MaxScenarios} scenarios.", nameof(request));
        }

        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scenario in request.Scenarios)
        {
            if (scenario is null || string.IsNullOrWhiteSpace(scenario.Name) || scenario.Requirements is null)
            {
                throw new ArgumentException("Every scenario needs a name and requirements.", nameof(request));
            }

            if (scenario.Requirements.Count == 0 || scenario.Requirements.Count > HandEvaluator.MaxRequirementsPerScenario)
            {
                throw new ArgumentException(
                    $"Scenario '{scenario.Name}' needs between 1 and {HandEvaluator.MaxRequirementsPerScenario} requirements.",
                    nameof(request));
            }

            foreach (var requirement in scenario.Requirements)
            {
                if (requirement is null || string.IsNullOrEmpty(requirement.Role))
                {
                    throw new ArgumentException($"Scenario '{scenario.Name}' has a requirement without a role.", nameof(request));
                }

                if (requirement.MinimumCount < 0 || requirement.MinimumCount > request.HandSize)
                {
                    throw new ArgumentException(
                        $"In scenario '{scenario.Name}', the minimum for {requirement.Role} must be between 0 and the hand size.",
                        nameof(request));
                }

                if (requirement.MaximumCount is { } max && (max < requirement.MinimumCount || max > request.HandSize))
                {
                    throw new ArgumentException(
                        $"In scenario '{scenario.Name}', the maximum for {requirement.Role} must be between the minimum and the hand size.",
                        nameof(request));
                }

                if (requirement.MinimumCount == 0 && requirement.MaximumCount is null)
                {
                    throw new ArgumentException(
                        $"In scenario '{scenario.Name}', the requirement for {requirement.Role} has no effect; set a minimum above 0 or a maximum.",
                        nameof(request));
                }

                roles.Add(requirement.Role);
            }
        }

        if (roles.Count > 64)
        {
            throw new ArgumentException("A run can refer to at most 64 different roles.", nameof(request));
        }
    }
}
