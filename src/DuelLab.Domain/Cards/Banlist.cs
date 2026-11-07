using System.Security.Cryptography;
using System.Text;

namespace DuelLab.Domain.Cards;

/// <summary>
/// A snapshot of the TCG Forbidden &amp; Limited list as DuelLab received it from the card-data
/// provider. A new snapshot is stored whenever an import finds the list has changed, so DuelLab builds
/// its own history from the day it was installed.
/// </summary>
/// <remarks>
/// The provider reports each card's current status but not the list's official effective date, so a
/// snapshot is dated by when DuelLab first saw it (and the provider's own last-update time, when known).
/// </remarks>
public sealed class Banlist
{
    private readonly List<BanlistEntry> _entries = [];

    private Banlist()
    {
        ContentHash = string.Empty;
    }

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>When DuelLab first saw this list.</summary>
    public DateTimeOffset FirstSeenAt { get; private set; }

    /// <summary>The provider's database version at that time, if it reports one.</summary>
    public string? ProviderVersion { get; private set; }

    /// <summary>When the provider says its data was last updated, if it reports it.</summary>
    public DateTimeOffset? ProviderUpdatedAt { get; private set; }

    /// <summary>A hash of the entries, to detect whether a later import changed the list.</summary>
    public string ContentHash { get; private set; }

    /// <summary>Every card that is not Unlimited.</summary>
    public IReadOnlyList<BanlistEntry> Entries => _entries;

    /// <summary>Creates a snapshot from (card id, status) pairs; Unlimited entries are dropped.</summary>
    public static Banlist Create(
        IEnumerable<(int CardId, BanStatus Status)> entries,
        DateTimeOffset firstSeenAt,
        string? providerVersion = null,
        DateTimeOffset? providerUpdatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = new Banlist
        {
            Id = Guid.CreateVersion7(firstSeenAt),
            FirstSeenAt = firstSeenAt,
            ProviderVersion = providerVersion,
            ProviderUpdatedAt = providerUpdatedAt,
        };
        list._entries.AddRange(entries
            .Where(e => e.Status != BanStatus.Unlimited)
            .DistinctBy(e => e.CardId)
            .OrderBy(e => e.CardId)
            .Select(e => new BanlistEntry(list.Id, e.CardId, e.Status)));
        list.ContentHash = HashOf(list._entries.Select(e => (e.CardId, e.Status)));
        return list;
    }

    /// <summary>The hash <see cref="Create"/> would give these entries (Unlimited ignored).</summary>
    public static string HashOf(IEnumerable<(int CardId, BanStatus Status)> entries)
    {
        var canonical = string.Join(';', entries.Where(e => e.Status != BanStatus.Unlimited).DistinctBy(e => e.CardId).OrderBy(e => e.CardId).Select(e => $"{e.CardId}:{e.Status}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>A card's status on this list.</summary>
    public BanStatus StatusOf(int cardId) => _entries.FirstOrDefault(e => e.CardId == cardId)?.Status ?? BanStatus.Unlimited;

    /// <summary>A short description for messages, e.g. "TCG list first seen 2026-11-07".</summary>
    public string Label => $"TCG list first seen {FirstSeenAt:yyyy-MM-dd}";
}

/// <summary>One card's status on a <see cref="Banlist"/>.</summary>
public sealed class BanlistEntry
{
    internal BanlistEntry(Guid banlistId, int cardId, BanStatus status)
    {
        BanlistId = banlistId;
        CardId = cardId;
        Status = status;
    }

    /// <summary>The owning list.</summary>
    public Guid BanlistId { get; private set; }

    /// <summary>The card's internal id.</summary>
    public int CardId { get; private set; }

    /// <summary>Forbidden, Limited or Semi-Limited.</summary>
    public BanStatus Status { get; private set; }
}
