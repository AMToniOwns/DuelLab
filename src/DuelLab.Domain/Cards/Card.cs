namespace DuelLab.Domain.Cards;

/// <summary>
/// A card in DuelLab's local catalogue. Its data comes from an external provider, but this type has
/// no dependency on the provider's response format.
/// </summary>
public sealed class Card
{
    private Card()
    {
        Name = string.Empty;
        Description = string.Empty;
        TypeLine = string.Empty;
    }

    /// <summary>Creates a card. Use <see cref="UpdateFrom"/> to refresh it from a later sync.</summary>
    public Card(long externalId, CardData data, DateTimeOffset syncedAt)
        : this()
    {
        ExternalId = externalId;
        UpdateFrom(data, syncedAt);
    }

    /// <summary>Internal primary key.</summary>
    public int Id { get; private set; }

    /// <summary>The provider's identifier. For Yu-Gi-Oh! this is the card's 8-digit passcode.</summary>
    public long ExternalId { get; private set; }

    /// <summary>The card name.</summary>
    public string Name { get; private set; }

    /// <summary>The card text.</summary>
    public string Description { get; private set; }

    /// <summary>Monster, Spell or Trap.</summary>
    public CardKind Kind { get; private set; }

    /// <summary>A readable type line, e.g. "Tuner Effect Monster" or "Quick-Play Spell".</summary>
    public string TypeLine { get; private set; }

    /// <summary>
    /// True for Fusion, Synchro, Xyz and Link Monsters, which belong in the Extra Deck, not the Main Deck.
    /// </summary>
    public bool IsExtraDeckCard { get; private set; }

    /// <summary>The monster's Attribute (e.g. DARK). Null for Spells and Traps.</summary>
    public string? Attribute { get; private set; }

    /// <summary>The monster's Type (e.g. Spellcaster), or the Spell/Trap property (e.g. Quick-Play).</summary>
    public string? Race { get; private set; }

    /// <summary>The archetype, when the provider reports one.</summary>
    public string? Archetype { get; private set; }

    /// <summary>Level or Rank. Null for Spells, Traps and Link Monsters.</summary>
    public int? Level { get; private set; }

    /// <summary>ATK. Null for Spells and Traps, and for "?" values.</summary>
    public int? Attack { get; private set; }

    /// <summary>DEF. Null for Spells, Traps, Link Monsters and "?" values.</summary>
    public int? Defense { get; private set; }

    /// <summary>Link Rating, for Link Monsters.</summary>
    public int? LinkValue { get; private set; }

    /// <summary>The provider's URL for the full-size artwork. DuelLab downloads and serves it itself.</summary>
    public string? ImageUrl { get; private set; }

    /// <summary>The provider's URL for the small artwork.</summary>
    public string? SmallImageUrl { get; private set; }

    /// <summary>TCG Forbidden &amp; Limited status at the last sync.</summary>
    public BanStatus TcgBanStatus { get; private set; }

    /// <summary>When this card's data was last refreshed from the provider.</summary>
    public DateTimeOffset LastSyncedAt { get; private set; }

    /// <summary>Overwrites the card's data with a newer copy from the provider.</summary>
    public void UpdateFrom(CardData data, DateTimeOffset syncedAt)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.Name);

        Name = data.Name.Trim();
        Description = data.Description ?? string.Empty;
        Kind = data.Kind;
        TypeLine = data.TypeLine ?? string.Empty;
        IsExtraDeckCard = data.IsExtraDeckCard;
        Attribute = data.Attribute;
        Race = data.Race;
        Archetype = data.Archetype;
        Level = data.Level;
        Attack = data.Attack;
        Defense = data.Defense;
        LinkValue = data.LinkValue;
        ImageUrl = data.ImageUrl;
        SmallImageUrl = data.SmallImageUrl;
        TcgBanStatus = data.TcgBanStatus;
        LastSyncedAt = syncedAt;
    }
}

/// <summary>The provider-neutral data needed to create or refresh a <see cref="Card"/>.</summary>
public sealed record CardData(
    string Name,
    string? Description,
    CardKind Kind,
    string? TypeLine,
    bool IsExtraDeckCard,
    string? Attribute = null,
    string? Race = null,
    string? Archetype = null,
    int? Level = null,
    int? Attack = null,
    int? Defense = null,
    int? LinkValue = null,
    string? ImageUrl = null,
    string? SmallImageUrl = null,
    BanStatus TcgBanStatus = BanStatus.Unlimited);
