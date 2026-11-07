using DuelLab.Domain.Common;

namespace DuelLab.Domain.Decks;

/// <summary>
/// A named deck. Its contents live in one or more <see cref="DeckVersion"/>s, so experiments never
/// overwrite earlier configurations.
/// </summary>
public sealed class Deck
{
    private readonly List<DeckVersion> _versions = [];

    private Deck()
    {
        Name = string.Empty;
        Format = DeckRules.DefaultFormat;
    }

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The deck name.</summary>
    public string Name { get; private set; }

    /// <summary>The play format: <see cref="DeckFormats.TcgAdvanced"/> or <see cref="DeckFormats.Casual"/>.</summary>
    public string Format { get; private set; }

    /// <summary>
    /// For TCG Advanced: the Forbidden &amp; Limited list snapshot the deck is pinned to, or null to follow
    /// the current list.
    /// </summary>
    public Guid? BanlistId { get; private set; }

    /// <summary>When the deck was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the deck or any of its versions last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The deck's versions, oldest first.</summary>
    public IReadOnlyList<DeckVersion> Versions => _versions;

    /// <summary>Creates a deck with one empty version.</summary>
    public static Deck Create(string name, DateTimeOffset now, string firstVersionName = "v1")
    {
        var deck = new Deck
        {
            Id = Guid.CreateVersion7(now),
            Name = ValidateName(name, "deck.name"),
            CreatedAt = now,
            UpdatedAt = now,
        };
        deck._versions.Add(DeckVersion.CreateEmpty(deck.Id, firstVersionName, now));
        return deck;
    }

    /// <summary>Renames the deck.</summary>
    public void Rename(string name, DateTimeOffset now)
    {
        Name = ValidateName(name, "deck.name");
        Touch(now);
    }

    /// <summary>
    /// Sets the format. <paramref name="banlistId"/> pins a TCG Advanced deck to a list snapshot (null
    /// follows the current list); it is not allowed for Casual.
    /// </summary>
    public void SetFormat(string format, Guid? banlistId, DateTimeOffset now)
    {
        var normalized = DeckFormats.Normalize(format)
            ?? throw new DomainException("deck.unknown_format", $"Unknown format '{format}'. Use one of: {string.Join(", ", DeckFormats.All)}.");
        if (banlistId is not null && normalized != DeckFormats.TcgAdvanced)
        {
            throw new DomainException("deck.banlist_needs_tcg", "Only TCG Advanced decks can be pinned to a Forbidden & Limited list.");
        }

        Format = normalized;
        BanlistId = banlistId;
        Touch(now);
    }

    /// <summary>Adds a new, empty version.</summary>
    public DeckVersion AddEmptyVersion(string versionName, DateTimeOffset now)
    {
        var version = DeckVersion.CreateEmpty(Id, versionName, now);
        _versions.Add(version);
        Touch(now);
        return version;
    }

    /// <summary>
    /// Adds a new version that copies the contents and roles of <paramref name="sourceVersionId"/>.
    /// The source version is not changed.
    /// </summary>
    public DeckVersion DuplicateVersion(Guid sourceVersionId, string versionName, DateTimeOffset now)
    {
        var source = FindVersion(sourceVersionId);
        var copy = source.Duplicate(versionName, now);
        _versions.Add(copy);
        Touch(now);
        return copy;
    }

    /// <summary>Removes a version. A deck must keep at least one version.</summary>
    public void RemoveVersion(Guid versionId, DateTimeOffset now)
    {
        var version = FindVersion(versionId);
        if (_versions.Count == 1)
        {
            throw new DomainException("deck.last_version", "A deck must keep at least one version.");
        }

        _versions.Remove(version);
        Touch(now);
    }

    /// <summary>Records that the deck (or one of its versions) changed.</summary>
    public void Touch(DateTimeOffset now) => UpdatedAt = now;

    private DeckVersion FindVersion(Guid versionId) =>
        _versions.FirstOrDefault(v => v.Id == versionId)
        ?? throw new DomainException("deck.version_not_found", "That version does not belong to this deck.");

    internal static string ValidateName(string? name, string codePrefix)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new DomainException($"{codePrefix}_required", "A name is required.");
        }

        if (trimmed.Length > DeckRules.MaxNameLength)
        {
            throw new DomainException(
                $"{codePrefix}_too_long",
                $"A name can be at most {DeckRules.MaxNameLength} characters.");
        }

        return trimmed;
    }
}
