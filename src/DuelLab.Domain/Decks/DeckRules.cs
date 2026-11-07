namespace DuelLab.Domain.Decks;

/// <summary>Deck-construction limits for the TCG Advanced Format.</summary>
public static class DeckRules
{
    /// <summary>Minimum Main Deck size.</summary>
    public const int MainDeckMin = 40;

    /// <summary>Maximum Main Deck size.</summary>
    public const int MainDeckMax = 60;

    /// <summary>Maximum Extra Deck size.</summary>
    public const int ExtraDeckMax = 15;

    /// <summary>Maximum Side Deck size.</summary>
    public const int SideDeckMax = 15;

    /// <summary>Maximum copies of one card across Main, Extra and Side Deck combined.</summary>
    public const int MaxCopiesPerCard = 3;

    /// <summary>The format DuelLab currently supports.</summary>
    public const string DefaultFormat = DeckFormats.TcgAdvanced;

    /// <summary>Maximum length of deck, version and custom-role names.</summary>
    public const int MaxNameLength = 100;

    /// <summary>Maximum length of version notes.</summary>
    public const int MaxNotesLength = 2000;

    /// <summary>The maximum number of cards a zone may hold.</summary>
    public static int MaxSize(DeckZone zone) => zone switch
    {
        DeckZone.Main => MainDeckMax,
        DeckZone.Extra => ExtraDeckMax,
        DeckZone.Side => SideDeckMax,
        _ => throw new ArgumentOutOfRangeException(nameof(zone)),
    };
}
