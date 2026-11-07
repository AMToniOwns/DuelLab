namespace DuelLab.Domain.Cards;

/// <summary>
/// A card's TCG Forbidden &amp; Limited status, as reported by the card-data provider at the last sync.
/// </summary>
/// <remarks>
/// The value is a snapshot. Real tournament legality depends on the list in force on a given date,
/// so DuelLab reports it as a warning, never as proof that a deck is legal.
/// </remarks>
public enum BanStatus
{
    /// <summary>Up to three copies.</summary>
    Unlimited,

    /// <summary>Up to two copies.</summary>
    SemiLimited,

    /// <summary>Up to one copy.</summary>
    Limited,

    /// <summary>No copies.</summary>
    Forbidden,
}

/// <summary>Helpers for <see cref="BanStatus"/>.</summary>
public static class BanStatusExtensions
{
    /// <summary>The maximum number of copies the status allows across Main, Extra and Side Deck.</summary>
    public static int MaxCopies(this BanStatus status) => status switch
    {
        BanStatus.Forbidden => 0,
        BanStatus.Limited => 1,
        BanStatus.SemiLimited => 2,
        _ => 3,
    };
}
