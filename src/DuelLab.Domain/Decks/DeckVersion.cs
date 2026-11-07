using DuelLab.Domain.Common;

namespace DuelLab.Domain.Decks;

/// <summary>
/// One configuration of a deck: its card quantities per zone and the analytical roles of its cards.
/// </summary>
/// <remarks>
/// Versions can be edited. Simulation runs store a full snapshot of the version they analysed, so
/// later edits never change the meaning of past results.
/// </remarks>
public sealed class DeckVersion
{
    private readonly List<DeckVersionCard> _cards = [];
    private readonly List<DeckVersionCardRole> _roles = [];
    private readonly List<DeckVersionComboLine> _comboLines = [];

    private DeckVersion()
    {
        VersionName = string.Empty;
    }

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The deck this version belongs to.</summary>
    public Guid DeckId { get; private set; }

    /// <summary>The version's display name, e.g. "v2 – 3 Ash".</summary>
    public string VersionName { get; private set; }

    /// <summary>Optional free-text notes.</summary>
    public string? Notes { get; private set; }

    /// <summary>The version this one was duplicated from, if any.</summary>
    public Guid? ParentVersionId { get; private set; }

    /// <summary>When the version was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the version's contents, roles, name or notes last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Card quantities, one entry per (card, zone).</summary>
    public IReadOnlyList<DeckVersionCard> Cards => _cards;

    /// <summary>Role assignments, one entry per (card, role).</summary>
    public IReadOnlyList<DeckVersionCardRole> Roles => _roles;

    /// <summary>Combo lines, in display order.</summary>
    public IReadOnlyList<DeckVersionComboLine> ComboLines => _comboLines;

    /// <summary>The number of cards in a zone.</summary>
    public int CountIn(DeckZone zone) => _cards.Where(c => c.Zone == zone).Sum(c => c.Quantity);

    /// <summary>The roles assigned to a card in this version.</summary>
    public IReadOnlyList<CardRole> RolesOf(int cardId) =>
        _roles.Where(r => r.CardId == cardId).Select(r => r.Role).ToList();

    internal static DeckVersion CreateEmpty(Guid deckId, string versionName, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        DeckId = deckId,
        VersionName = Deck.ValidateName(versionName, "version.name"),
        CreatedAt = now,
        UpdatedAt = now,
    };

    internal DeckVersion Duplicate(string versionName, DateTimeOffset now)
    {
        var copy = CreateEmpty(DeckId, versionName, now);
        copy.ParentVersionId = Id;
        copy.Notes = Notes;
        copy._cards.AddRange(_cards.Select(c => new DeckVersionCard(copy.Id, c.CardId, c.Zone, c.Quantity)));
        copy._roles.AddRange(_roles.Select(r => new DeckVersionCardRole(copy.Id, r.CardId, r.Role)));
        copy._comboLines.AddRange(_comboLines.Select(l => l.CopyTo(copy.Id, now)));
        return copy;
    }

    /// <summary>Changes the name and notes.</summary>
    public void UpdateDetails(string versionName, string? notes, DateTimeOffset now)
    {
        var trimmedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (trimmedNotes?.Length > DeckRules.MaxNotesLength)
        {
            throw new DomainException(
                "version.notes_too_long",
                $"Notes can be at most {DeckRules.MaxNotesLength} characters.");
        }

        VersionName = Deck.ValidateName(versionName, "version.name");
        Notes = trimmedNotes;
        UpdatedAt = now;
    }

    /// <summary>
    /// Replaces the version's card list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rejects data that can never be part of a legal deck: a quantity below 1, the same card listed
    /// twice in one zone, more than <see cref="DeckRules.MaxCopiesPerCard"/> copies of a card across
    /// all zones, or a zone over its maximum size.
    /// </para>
    /// <para>
    /// Allows work in progress, such as a 25-card Main Deck. <see cref="DeckValidator"/> reports those
    /// problems instead, and simulation refuses to run until they are fixed.
    /// </para>
    /// <para>Roles of cards that are no longer in the version are removed.</para>
    /// </remarks>
    public void ReplaceCards(IEnumerable<DeckCardEntry> entries, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToList();

        foreach (var entry in list)
        {
            if (!Enum.IsDefined(entry.Zone))
            {
                throw new DomainException("deck.unknown_zone", $"Unknown deck zone '{entry.Zone}'.");
            }

            if (entry.Quantity < 1)
            {
                throw new DomainException("deck.quantity_too_low", "Each card entry needs a quantity of at least 1.");
            }
        }

        var duplicate = list.GroupBy(e => (e.CardId, e.Zone)).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new DomainException(
                "deck.duplicate_entry",
                $"Card {duplicate.Key.CardId} is listed more than once in the {duplicate.Key.Zone} Deck.");
        }

        var overLimit = list.GroupBy(e => e.CardId).FirstOrDefault(g => g.Sum(e => e.Quantity) > DeckRules.MaxCopiesPerCard);
        if (overLimit is not null)
        {
            throw new DomainException(
                "deck.copy_limit",
                $"Card {overLimit.Key} has {overLimit.Sum(e => e.Quantity)} copies. " +
                $"A deck can hold at most {DeckRules.MaxCopiesPerCard} copies of a card across Main, Extra and Side Deck.");
        }

        foreach (var zone in Enum.GetValues<DeckZone>())
        {
            var count = list.Where(e => e.Zone == zone).Sum(e => e.Quantity);
            if (count > DeckRules.MaxSize(zone))
            {
                throw new DomainException(
                    "deck.zone_too_large",
                    $"The {zone} Deck has {count} cards; the maximum is {DeckRules.MaxSize(zone)}.");
            }
        }

        _cards.Clear();
        _cards.AddRange(list.Select(e => new DeckVersionCard(Id, e.CardId, e.Zone, e.Quantity)));

        var remaining = list.Select(e => e.CardId).ToHashSet();
        _roles.RemoveAll(r => !remaining.Contains(r.CardId));
        UpdatedAt = now;
    }

    /// <summary>
    /// Replaces all role assignments. Every card must be in the version; a card may have several roles.
    /// </summary>
    public void ReplaceRoles(IEnumerable<(int CardId, CardRole Role)> assignments, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        var list = assignments.Distinct().ToList();

        var inVersion = _cards.Select(c => c.CardId).ToHashSet();
        var missing = list.FirstOrDefault(a => !inVersion.Contains(a.CardId));
        if (missing.Role is not null)
        {
            throw new DomainException(
                "role.card_not_in_version",
                $"Card {missing.CardId} is not in this version, so it cannot have a role.");
        }

        _roles.Clear();
        _roles.AddRange(list.Select(a => new DeckVersionCardRole(Id, a.CardId, a.Role)));
        UpdatedAt = now;
    }

    /// <summary>
    /// Replaces all combo lines. Names must be unique, and every named card must be in the version or
    /// already be used by the current lines. (Removing a card keeps the lines that use it unchanged, and
    /// <see cref="DeckValidator"/> warns about it; re-saving those lines must still work.)
    /// </summary>
    public void ReplaceComboLines(IEnumerable<ComboLineDraft> lines, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var list = lines.ToList();
        if (list.Count > DeckVersionComboLine.MaxPerVersion)
        {
            throw new DomainException("combo.too_many", $"A version can have at most {DeckVersionComboLine.MaxPerVersion} combo lines.");
        }

        var allowed = _cards.Select(c => c.CardId)
            .Concat(_comboLines.SelectMany(l => l.Pieces).SelectMany(p => p.CardIds))
            .ToHashSet();
        var validated = list.Select(l => DeckVersionComboLine.Validate(l, allowed)).ToList();
        var duplicate = validated.GroupBy(l => l.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new DomainException("combo.duplicate_name", $"There is more than one combo line called '{duplicate.Key}'.");
        }

        _comboLines.Clear();
        _comboLines.AddRange(validated.Select((l, i) => new DeckVersionComboLine(Id, i, l.Name, l.Notes, l.Pieces, now)));
        UpdatedAt = now;
    }
}

/// <summary>A requested card quantity in one zone.</summary>
public readonly record struct DeckCardEntry(int CardId, DeckZone Zone, int Quantity);

/// <summary>A card's quantity in one zone of a <see cref="DeckVersion"/>.</summary>
public sealed class DeckVersionCard
{
    internal DeckVersionCard(Guid deckVersionId, int cardId, DeckZone zone, int quantity)
    {
        DeckVersionId = deckVersionId;
        CardId = cardId;
        Zone = zone;
        Quantity = quantity;
    }

    /// <summary>The owning version.</summary>
    public Guid DeckVersionId { get; private set; }

    /// <summary>The card's internal id.</summary>
    public int CardId { get; private set; }

    /// <summary>Main, Extra or Side.</summary>
    public DeckZone Zone { get; private set; }

    /// <summary>Number of copies in this zone.</summary>
    public int Quantity { get; private set; }
}

/// <summary>One analytical role of one card in a <see cref="DeckVersion"/>.</summary>
public sealed class DeckVersionCardRole
{
    private DeckVersionCardRole()
    {
        CustomName = string.Empty;
    }

    internal DeckVersionCardRole(Guid deckVersionId, int cardId, CardRole role)
    {
        DeckVersionId = deckVersionId;
        CardId = cardId;
        Kind = role.Kind;
        CustomName = role.CustomName;
    }

    /// <summary>The owning version.</summary>
    public Guid DeckVersionId { get; private set; }

    /// <summary>The card's internal id.</summary>
    public int CardId { get; private set; }

    /// <summary>The role type.</summary>
    public CardRoleKind Kind { get; private set; }

    /// <summary>Name of a Custom role; empty for built-in roles.</summary>
    public string CustomName { get; private set; }

    /// <summary>The role as a value object.</summary>
    public CardRole Role => new(Kind, Kind == CardRoleKind.Custom ? CustomName : null);
}
