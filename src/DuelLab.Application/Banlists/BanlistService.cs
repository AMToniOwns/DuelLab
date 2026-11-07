using DuelLab.Application.Common;
using DuelLab.Domain.Cards;
using DuelLab.Domain.Decks;

namespace DuelLab.Application.Banlists;

/// <summary>A list snapshot without its entries.</summary>
public sealed record BanlistSummaryDto(
    Guid Id,
    string Label,
    DateTimeOffset FirstSeenAt,
    string? ProviderVersion,
    DateTimeOffset? ProviderUpdatedAt,
    int Forbidden,
    int Limited,
    int SemiLimited,
    bool IsCurrent);

/// <summary>A list snapshot with its entries.</summary>
public sealed record BanlistDto(BanlistSummaryDto Summary, IReadOnlyList<BanlistEntryDto> Entries);

/// <summary>One card on a list.</summary>
public sealed record BanlistEntryDto(int CardId, string Name, BanStatus Status);

/// <summary>Forbidden &amp; Limited list snapshots, and which rules apply to a deck.</summary>
public sealed class BanlistService(IBanlistRepository banlists, ICardRepository cards, IUnitOfWork unitOfWork, TimeProvider time)
{
    /// <summary>All snapshots, newest first.</summary>
    public async Task<IReadOnlyList<BanlistSummaryDto>> ListAsync(CancellationToken cancellationToken)
    {
        var all = await banlists.ListAsync(cancellationToken);
        return all.Select((b, i) => Summary(b, isCurrent: i == 0)).ToList();
    }

    /// <summary>One snapshot with card names. <paramref name="id"/> null means the current list.</summary>
    public async Task<BanlistDto> GetAsync(Guid? id, CancellationToken cancellationToken)
    {
        var latest = await banlists.GetLatestAsync(cancellationToken);
        var list = id is null ? latest : await banlists.GetAsync(id.Value, cancellationToken);
        if (list is null)
        {
            throw new NotFoundException("Forbidden & Limited list", id?.ToString() ?? "current");
        }

        var names = await cards.GetByIdsAsync(list.Entries.Select(e => e.CardId), cancellationToken);
        var entries = list.Entries
            .Select(e => new BanlistEntryDto(e.CardId, names.TryGetValue(e.CardId, out var c) ? c.Name : $"Card {e.CardId}", e.Status))
            .OrderByDescending(e => e.Status)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new BanlistDto(Summary(list, isCurrent: list.Id == latest?.Id), entries);
    }

    /// <summary>
    /// Stores a new snapshot if the cards' current TCG statuses differ from the latest one. Returns the
    /// new snapshot, or null if nothing changed. Changes are saved.
    /// </summary>
    public async Task<Banlist?> RecordIfChangedAsync(ProviderDataVersion? version, CancellationToken cancellationToken)
    {
        var statuses = await cards.GetListedStatusesAsync(cancellationToken);
        var latest = await banlists.GetLatestAsync(cancellationToken);
        if (latest is not null && latest.ContentHash == Banlist.HashOf(statuses))
        {
            return null;
        }

        var snapshot = Banlist.Create(statuses, time.GetUtcNow(), version?.Version, version?.UpdatedAt);
        banlists.Add(snapshot);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    /// <summary>The Forbidden &amp; Limited rules for a deck: none, its pinned list, or the current list.</summary>
    public async Task<(BanlistRules Rules, Banlist? List)> RulesForAsync(Deck deck, IReadOnlyDictionary<int, Card> cardsUsed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deck);
        if (deck.Format == DeckFormats.Casual)
        {
            return (BanlistRules.None, null);
        }

        var list = deck.BanlistId is { } pinned
            ? await banlists.GetAsync(pinned, cancellationToken)
            : await banlists.GetLatestAsync(cancellationToken);

        // Before the first snapshot exists, the cards' own (current) statuses are the list.
        return list is null
            ? (DeckValidator.CurrentListOnCards(cardsUsed), null)
            : (new BanlistRules(true, list.StatusOf, deck.BanlistId is null ? $"current {list.Label}" : list.Label), list);
    }

    /// <summary>Throws <see cref="RequestValidationException"/> unless the snapshot exists.</summary>
    public async Task EnsureExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await banlists.GetAsync(id, cancellationToken) is null)
        {
            throw new RequestValidationException("deck.unknown_banlist", "That Forbidden & Limited list does not exist.");
        }
    }

    private static BanlistSummaryDto Summary(Banlist b, bool isCurrent) => new(
        b.Id,
        b.Label,
        b.FirstSeenAt,
        b.ProviderVersion,
        b.ProviderUpdatedAt,
        b.Entries.Count(e => e.Status == BanStatus.Forbidden),
        b.Entries.Count(e => e.Status == BanStatus.Limited),
        b.Entries.Count(e => e.Status == BanStatus.SemiLimited),
        isCurrent);
}
