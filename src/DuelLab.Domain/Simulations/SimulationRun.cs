namespace DuelLab.Domain.Simulations;

/// <summary>
/// A stored simulation run. It keeps the full configuration and a snapshot of the deck it analysed,
/// so the run can be reproduced later even if the deck version has been edited since.
/// </summary>
public sealed class SimulationRun
{
    private SimulationRun()
    {
        EngineVersion = string.Empty;
        ConfigurationJson = string.Empty;
        ResultsJson = string.Empty;
        DeckSnapshotHash = string.Empty;
    }

    /// <summary>Creates a run record.</summary>
    public SimulationRun(
        Guid deckId,
        Guid deckVersionId,
        DateTimeOffset createdAt,
        long randomSeed,
        int sampleCount,
        int openingHandSize,
        string engineVersion,
        string deckSnapshotHash,
        string configurationJson,
        string resultsJson)
    {
        Id = Guid.CreateVersion7(createdAt);
        DeckId = deckId;
        DeckVersionId = deckVersionId;
        CreatedAt = createdAt;
        RandomSeed = randomSeed;
        SampleCount = sampleCount;
        OpeningHandSize = openingHandSize;
        EngineVersion = engineVersion;
        DeckSnapshotHash = deckSnapshotHash;
        ConfigurationJson = configurationJson;
        ResultsJson = resultsJson;
    }

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The deck that was analysed.</summary>
    public Guid DeckId { get; private set; }

    /// <summary>The deck version that was analysed.</summary>
    public Guid DeckVersionId { get; private set; }

    /// <summary>When the run happened.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The seed that makes the run reproducible.</summary>
    public long RandomSeed { get; private set; }

    /// <summary>Number of simulated hands.</summary>
    public int SampleCount { get; private set; }

    /// <summary>Cards per opening hand.</summary>
    public int OpeningHandSize { get; private set; }

    /// <summary>The simulator version that produced the results.</summary>
    public string EngineVersion { get; private set; }

    /// <summary>A hash of the analysed deck contents and roles, to detect later edits.</summary>
    public string DeckSnapshotHash { get; private set; }

    /// <summary>The configuration and deck snapshot, as JSON.</summary>
    public string ConfigurationJson { get; private set; }

    /// <summary>The results, as JSON.</summary>
    public string ResultsJson { get; private set; }
}
