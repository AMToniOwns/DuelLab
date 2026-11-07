using DuelLab.Domain.Cards;
using DuelLab.Domain.Decks;
using DuelLab.Domain.Simulations;

namespace DuelLab.Application.Common;

/// <summary>Stores and loads decks with their versions, cards and roles.</summary>
public interface IDeckRepository
{
    /// <summary>All decks with their versions, most recently updated first.</summary>
    Task<IReadOnlyList<Deck>> ListAsync(CancellationToken cancellationToken);

    /// <summary>A deck with its versions (including cards and roles), or null.</summary>
    Task<Deck?> GetAsync(Guid deckId, CancellationToken cancellationToken);

    /// <summary>The deck that owns a version, with all its versions loaded, or null.</summary>
    Task<Deck?> GetByVersionIdAsync(Guid versionId, CancellationToken cancellationToken);

    /// <summary>Adds a new deck.</summary>
    void Add(Deck deck);

    /// <summary>Deletes a deck with its versions and simulation runs.</summary>
    void Remove(Deck deck);
}

/// <summary>Reads and writes the local card catalogue.</summary>
public interface ICardRepository
{
    /// <summary>Cards by internal id. Missing ids are left out.</summary>
    Task<Dictionary<int, Card>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken);

    /// <summary>A page of cards whose names contain <paramref name="search"/>, best matches first.</summary>
    Task<(IReadOnlyList<Card> Items, int Total)> SearchAsync(string? search, CardKind? kind, int skip, int take, CancellationToken cancellationToken);

    /// <summary>Every card, keyed by provider id (used by the import).</summary>
    Task<Dictionary<long, Card>> GetAllByExternalIdAsync(CancellationToken cancellationToken);

    /// <summary>Number of cards and the most recent sync time.</summary>
    Task<(int Count, DateTimeOffset? LastSyncedAt)> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>Adds new cards.</summary>
    void AddRange(IEnumerable<Card> cards);
}

/// <summary>Stores simulation runs.</summary>
public interface ISimulationRunRepository
{
    /// <summary>A run, or null.</summary>
    Task<SimulationRun?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Recent runs, newest first, optionally for one deck version.</summary>
    Task<IReadOnlyList<SimulationRun>> ListAsync(Guid? deckVersionId, int take, CancellationToken cancellationToken);

    /// <summary>Adds a run.</summary>
    void Add(SimulationRun run);
}

/// <summary>Commits pending changes as one transaction.</summary>
public interface IUnitOfWork
{
    /// <summary>Saves all pending changes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>A card as delivered by an external card-data provider, already mapped to DuelLab's terms.</summary>
public sealed record ExternalCard(long ExternalId, CardData Data);

/// <summary>Fetches card data from an external provider.</summary>
public interface ICardDataProvider
{
    /// <summary>A short name for logs and status, e.g. "YGOPRODeck".</summary>
    string Name { get; }

    /// <summary>Downloads every card the provider offers for the supported format.</summary>
    Task<IReadOnlyList<ExternalCard>> FetchAllCardsAsync(CancellationToken cancellationToken);
}

/// <summary>Image sizes the catalogue serves.</summary>
public enum CardImageSize
{
    /// <summary>Thumbnail size, for lists and hands.</summary>
    Small,

    /// <summary>Full card size, for detail views.</summary>
    Full,
}

/// <summary>A locally stored card image.</summary>
public sealed record CardImageFile(string Path, string ContentType);

/// <summary>Keeps local copies of card artwork, so the provider's images are never hotlinked.</summary>
public interface ICardImageStore
{
    /// <summary>The local image for a card, downloading it once if needed. Null if unavailable.</summary>
    Task<CardImageFile?> GetAsync(Card card, CardImageSize size, CancellationToken cancellationToken);
}
