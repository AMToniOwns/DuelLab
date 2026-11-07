namespace DuelLab.Domain.Decks;

/// <summary>The three parts of a deck.</summary>
public enum DeckZone
{
    /// <summary>The Main Deck (40–60 cards). Opening hands are drawn from here.</summary>
    Main,

    /// <summary>The Extra Deck (0–15 cards).</summary>
    Extra,

    /// <summary>The Side Deck (0–15 cards).</summary>
    Side,
}
