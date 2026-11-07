using DuelLab.Domain.Decks;
using DuelLab.Simulator.Analysis;

namespace DuelLab.Application.Simulations;

/// <summary>How to run a simulation.</summary>
/// <param name="SampleCount">Simulated hands (1–100,000).</param>
/// <param name="OpeningHandSize">Cards per hand (default 5).</param>
/// <param name="Seed">Seed for reproducibility. Omit it and the server picks one and records it.</param>
/// <param name="Scenarios">Conditions to measure. Omit them to use the defaults.</param>
/// <param name="SampleHandCount">How many simulated hands to keep for inspection (default 20).</param>
/// <param name="IncludeComboLines">
/// Also measure the version's combo lines: one scenario per line, plus "Any combo line" when there are two
/// or more. The expanded scenarios are stored with the run.
/// </param>
public sealed record SimulationConfigDto(
    int SampleCount = 10_000,
    int OpeningHandSize = 5,
    long? Seed = null,
    IReadOnlyList<ScenarioDto>? Scenarios = null,
    int? SampleHandCount = null,
    bool IncludeComboLines = true);

/// <summary>
/// A condition. Either <paramref name="Requirements"/> (all must hold) or <paramref name="AnyOf"/>
/// (alternatives: at least one must hold).
/// </summary>
public sealed record ScenarioDto(
    string Name,
    IReadOnlyList<RequirementDto>? Requirements = null,
    bool RequireDistinctCards = true,
    IReadOnlyList<ClauseDto>? AnyOf = null);

/// <summary>One alternative of a scenario: every requirement must hold.</summary>
public sealed record ClauseDto(IReadOnlyList<RequirementDto> Requirements, bool RequireDistinctCards = true);

/// <summary>
/// A bound on matching cards, e.g. at least 1 "Starter" or at most 0 "Brick". A card matches when it has
/// <paramref name="Role"/> or any of <paramref name="Roles"/>, or is one of <paramref name="CardIds"/>.
/// </summary>
public sealed record RequirementDto(
    string? Role = null,
    int MinimumCount = 1,
    int? MaximumCount = null,
    IReadOnlyList<string>? Roles = null,
    IReadOnlyList<int>? CardIds = null);

/// <summary>The Main Deck as it was when a run happened, and the format and list it was checked against.</summary>
public sealed record DeckSnapshot(
    Guid DeckId,
    string DeckName,
    Guid VersionId,
    string VersionName,
    IReadOnlyList<SnapshotCard> MainDeck,
    string? Format = null,
    string? Banlist = null);

/// <summary>One card of a <see cref="DeckSnapshot"/>.</summary>
public sealed record SnapshotCard(int CardId, string Name, int Quantity, IReadOnlyList<string> Roles);

/// <summary>What a stored run keeps so it can be reproduced.</summary>
public sealed record StoredConfiguration(SimulationConfigDto Configuration, DeckSnapshot Snapshot);

/// <summary>A full simulation run.</summary>
public sealed record SimulationRunDto(
    Guid Id,
    Guid DeckId,
    Guid DeckVersionId,
    string DeckName,
    string VersionName,
    DateTimeOffset CreatedAt,
    string EngineVersion,
    long Seed,
    int SampleCount,
    int OpeningHandSize,
    SimulationConfigDto Configuration,
    DeckSnapshot Snapshot,
    AnalysisResult Result,
    bool? VersionChangedSinceRun);

/// <summary>A run in a history list.</summary>
public sealed record SimulationRunSummaryDto(
    Guid Id,
    Guid DeckId,
    Guid DeckVersionId,
    string DeckName,
    string VersionName,
    DateTimeOffset CreatedAt,
    long Seed,
    int SampleCount,
    int OpeningHandSize,
    IReadOnlyList<ScenarioHeadlineDto> Scenarios);

/// <summary>A scenario's headline numbers.</summary>
public sealed record ScenarioHeadlineDto(string Name, double Estimate, double? Exact);

/// <summary>The outcome of re-running a stored simulation.</summary>
/// <param name="Identical">True when the new results match the stored ones exactly.</param>
/// <param name="EngineVersionMatches">False when the run was made by a different engine version.</param>
/// <param name="StoredEngineVersion">The engine version that made the stored run.</param>
/// <param name="CurrentEngineVersion">The engine version now running.</param>
/// <param name="Reproduced">The new results.</param>
public sealed record ReproductionDto(
    bool Identical,
    bool EngineVersionMatches,
    string StoredEngineVersion,
    string CurrentEngineVersion,
    AnalysisResult Reproduced);

/// <summary>Request to compare two versions under the same configuration.</summary>
public sealed record CompareRequest(Guid BaseVersionId, Guid CandidateVersionId, SimulationConfigDto? Configuration);

/// <summary>A comparison of two versions.</summary>
public sealed record ComparisonDto(
    SimulationRunDto Base,
    SimulationRunDto Candidate,
    IReadOnlyList<ScenarioComparisonDto> Scenarios,
    IReadOnlyList<CardChangeDto> CardChanges,
    IReadOnlyList<RoleChangeDto> RoleChanges,
    IReadOnlyList<ComboLineChangeDto> ComboLineChanges);

/// <summary>A combo line that differs between the versions: "added", "removed" or "changed".</summary>
public sealed record ComboLineChangeDto(string Name, string Change);

/// <summary>One scenario in both versions. Differences are candidate − base, in percentage points.</summary>
public sealed record ScenarioComparisonDto(
    string Name,
    string Description,
    double? BaseExact,
    double? CandidateExact,
    double? ExactDifferencePp,
    double BaseEstimate,
    double CandidateEstimate,
    double EstimateDifferencePp);

/// <summary>A card whose quantity in a zone differs between the versions.</summary>
public sealed record CardChangeDto(int CardId, string Name, DeckZone Zone, int BaseQuantity, int CandidateQuantity, int Delta);

/// <summary>A card whose roles differ between the versions.</summary>
public sealed record RoleChangeDto(int CardId, string Name, IReadOnlyList<string> Added, IReadOnlyList<string> Removed);
