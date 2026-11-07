namespace DuelLab.Simulator.Analysis;

/// <summary>One distinct card in the deck being analysed, with its quantity and analytical roles.</summary>
/// <param name="Key">A stable identifier for the card, unique within the deck.</param>
/// <param name="Name">The display name.</param>
/// <param name="Quantity">Copies in the deck (at least 1).</param>
/// <param name="Roles">The role keys assigned to the card, e.g. <c>Starter</c> or <c>Custom:Fusion line</c>.</param>
public sealed record AnalysisCard(string Key, string Name, int Quantity, IReadOnlyCollection<string> Roles);

/// <summary>
/// A bound on how many cards with a given role a hand contains.
/// </summary>
/// <param name="Role">The role key.</param>
/// <param name="MinimumCount">The hand needs at least this many cards with the role.</param>
/// <param name="MaximumCount">If set, the hand may hold at most this many (e.g. 0 bricks).</param>
public sealed record RoleRequirement(string Role, int MinimumCount = 1, int? MaximumCount = null);

/// <summary>
/// A named condition that a hand either meets or does not: every requirement must hold (logical AND).
/// </summary>
/// <param name="Name">A short label, e.g. "Starter + extender".</param>
/// <param name="Requirements">The requirements; all must hold.</param>
/// <param name="RequireDistinctCards">
/// When true (the default), minimum counts must be met by <em>different physical cards</em>: a card that
/// is both a Starter and an Extender can count as one or the other, not both. When false, one card
/// counts toward every role it has. Maximum counts always count every card with the role.
/// </param>
public sealed record Scenario(string Name, IReadOnlyList<RoleRequirement> Requirements, bool RequireDistinctCards = true);

/// <summary>Everything needed to run (and later re-run) an analysis.</summary>
/// <param name="Deck">The Main Deck. Opening hands are drawn from these cards only.</param>
/// <param name="HandSize">Cards per opening hand.</param>
/// <param name="SampleCount">Number of Monte Carlo hands.</param>
/// <param name="Seed">The seed for the random generator.</param>
/// <param name="Scenarios">The conditions to measure.</param>
/// <param name="SampleHandCount">How many of the simulated hands to return for inspection.</param>
public sealed record AnalysisRequest(
    IReadOnlyList<AnalysisCard> Deck,
    int HandSize,
    int SampleCount,
    long Seed,
    IReadOnlyList<Scenario> Scenarios,
    int SampleHandCount = 10);

/// <summary>The result for one scenario.</summary>
/// <param name="Name">The scenario's name.</param>
/// <param name="Description">A plain-language statement of exactly what was measured.</param>
/// <param name="Successes">Simulated hands that met the scenario.</param>
/// <param name="Samples">Simulated hands in total.</param>
/// <param name="Estimate">Successes / Samples.</param>
/// <param name="ConfidenceInterval">The 95% Wilson interval around <paramref name="Estimate"/>.</param>
/// <param name="Exact">The exact probability, or null when it could not be computed.</param>
public sealed record ScenarioResult(
    string Name,
    string Description,
    int Successes,
    int Samples,
    double Estimate,
    ConfidenceInterval ConfidenceInterval,
    double? Exact)
{
    /// <summary>Estimate minus exact value, when the exact value is known.</summary>
    public double? EstimateMinusExact => Exact is { } exact ? Estimate - exact : null;
}

/// <summary>How often a card appears in an opening hand.</summary>
/// <param name="Key">The card key.</param>
/// <param name="Name">The display name.</param>
/// <param name="Quantity">Copies in the deck.</param>
/// <param name="HandsWithCard">Simulated hands containing at least one copy.</param>
/// <param name="Estimate">HandsWithCard / samples.</param>
/// <param name="Exact">The exact probability of opening at least one copy.</param>
public sealed record CardFrequency(string Key, string Name, int Quantity, int HandsWithCard, double Estimate, double Exact);

/// <summary>One simulated hand, kept so users can inspect real examples.</summary>
/// <param name="Index">The hand's position in the run (0-based).</param>
/// <param name="CardKeys">The cards drawn, in draw order.</param>
/// <param name="ScenarioOutcomes">Whether the hand met each scenario, in request order.</param>
public sealed record SampleHand(int Index, IReadOnlyList<string> CardKeys, IReadOnlyList<bool> ScenarioOutcomes);

/// <summary>The output of <see cref="SimulationEngine.Run"/>.</summary>
public sealed record AnalysisResult(
    string EngineVersion,
    string RandomAlgorithm,
    long Seed,
    int DeckSize,
    int HandSize,
    int SampleCount,
    IReadOnlyList<ScenarioResult> Scenarios,
    IReadOnlyList<CardFrequency> CardFrequencies,
    IReadOnlyList<SampleHand> SampleHands);
