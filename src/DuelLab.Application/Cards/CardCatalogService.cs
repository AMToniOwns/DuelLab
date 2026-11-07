using DuelLab.Application.Common;
using DuelLab.Domain.Cards;
using Microsoft.Extensions.Logging;

namespace DuelLab.Application.Cards;

/// <summary>Search, details and import for the local card catalogue.</summary>
/// <remarks>
/// Card data is imported in bulk from the provider and searched locally. The provider (YGOPRODeck)
/// asks clients to store its data locally and keep calls to a minimum; one import is a single request.
/// </remarks>
public sealed partial class CardCatalogService(
    CatalogSyncGate syncGate,
    Banlists.BanlistService banlists,
    ICardRepository cards,
    ICardDataProvider provider,
    ICardImageStore images,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    ILogger<CardCatalogService> logger)
{
    /// <summary>Largest page size for search.</summary>
    public const int MaxPageSize = 100;

    /// <summary>The minimum time between two imports, to respect the provider.</summary>
    public static readonly TimeSpan MinimumSyncInterval = TimeSpan.FromHours(1);

    /// <summary>Searches cards by name.</summary>
    public async Task<PagedResult<CardDto>> SearchAsync(string? search, CardKind? kind, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            throw new RequestValidationException("cards.page", "Page must be 1 or more.");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new RequestValidationException("cards.page_size", $"Page size must be between 1 and {MaxPageSize}.");
        }

        var (items, total) = await cards.SearchAsync(search?.Trim(), kind, (page - 1) * pageSize, pageSize, cancellationToken);
        return new PagedResult<CardDto>(items.Select(CardDto.From).ToList(), total, page, pageSize);
    }

    /// <summary>One card's details.</summary>
    public async Task<CardDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var found = await cards.GetByIdsAsync([id], cancellationToken);
        return found.TryGetValue(id, out var card) ? CardDto.From(card) : throw new NotFoundException("Card", id);
    }

    /// <summary>A card's locally stored image, downloading it once if needed.</summary>
    public async Task<CardImageFile?> GetImageAsync(int id, CardImageSize size, CancellationToken cancellationToken)
    {
        var found = await cards.GetByIdsAsync([id], cancellationToken);
        if (!found.TryGetValue(id, out var card))
        {
            throw new NotFoundException("Card", id);
        }

        return await images.GetAsync(card, size, cancellationToken);
    }

    /// <summary>Card count and last sync time.</summary>
    public async Task<CatalogStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var (count, last) = await cards.GetStatusAsync(cancellationToken);
        return new CatalogStatusDto(count, last, provider.Name);
    }

    /// <summary>
    /// Imports every card from the provider, adding new cards and refreshing existing ones. Cards are
    /// never deleted, because decks may refer to them.
    /// </summary>
    /// <param name="force">Ignore <see cref="MinimumSyncInterval"/> (used for the first import).</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    public async Task<CatalogSyncResultDto> SyncAsync(bool force, CancellationToken cancellationToken)
    {
        if (!await syncGate.Lock.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            throw new RequestValidationException("cards.sync_running", "A card import is already running.");
        }

        try
        {
            var now = time.GetUtcNow();
            var (_, lastSynced) = await cards.GetStatusAsync(cancellationToken);
            if (!force && lastSynced is { } last && now - last < MinimumSyncInterval)
            {
                throw new RequestValidationException(
                    "cards.sync_too_soon",
                    $"Cards were imported at {last:u}. To respect the provider's limits, wait until {last + MinimumSyncInterval:u}.");
            }

            LogSyncStarting(logger, provider.Name);
            var version = await provider.GetDataVersionAsync(cancellationToken);
            var incoming = await provider.FetchAllCardsAsync(cancellationToken);
            var existing = await cards.GetAllByExternalIdAsync(cancellationToken);

            var added = new List<Card>();
            var updated = 0;
            foreach (var external in incoming.DistinctBy(c => c.ExternalId))
            {
                if (existing.TryGetValue(external.ExternalId, out var card))
                {
                    card.UpdateFrom(external.Data, now);
                    updated++;
                }
                else
                {
                    added.Add(new Card(external.ExternalId, external.Data, now));
                }
            }

            cards.AddRange(added);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            var newList = await banlists.RecordIfChangedAsync(version, cancellationToken);
            LogSyncFinished(logger, added.Count, updated, newList is not null);

            return new CatalogSyncResultDto(added.Count, updated, existing.Count + added.Count, now, newList is not null);
        }
        finally
        {
            syncGate.Lock.Release();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Importing cards from {Provider}")]
    private static partial void LogSyncStarting(ILogger logger, string provider);

    [LoggerMessage(Level = LogLevel.Information, Message = "Card import finished: {Added} added, {Updated} updated, new Forbidden & Limited list: {NewList}")]
    private static partial void LogSyncFinished(ILogger logger, int added, int updated, bool newList);
}

/// <summary>Ensures only one catalogue import runs at a time within an application instance.</summary>
public sealed class CatalogSyncGate : IDisposable
{
    /// <summary>The lock taken for the duration of an import.</summary>
    public SemaphoreSlim Lock { get; } = new(1, 1);

    /// <inheritdoc />
    public void Dispose() => Lock.Dispose();
}
