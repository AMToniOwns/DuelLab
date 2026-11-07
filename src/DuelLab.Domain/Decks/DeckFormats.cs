namespace DuelLab.Domain.Decks;

/// <summary>The formats a deck can be built for.</summary>
public static class DeckFormats
{
    /// <summary>TCG Advanced Format: the Forbidden &amp; Limited list is enforced.</summary>
    public const string TcgAdvanced = "TCG Advanced";

    /// <summary>Casual: no Forbidden &amp; Limited list; the 3-copy rule and deck sizes still apply.</summary>
    public const string Casual = "Casual";

    /// <summary>All supported formats.</summary>
    public static IReadOnlyList<string> All { get; } = [TcgAdvanced, Casual];

    /// <summary>The supported spelling of <paramref name="format"/>, or null if it is not supported.</summary>
    public static string? Normalize(string? format) =>
        All.FirstOrDefault(f => string.Equals(f, format?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// The Forbidden &amp; Limited list that applies when validating a deck: none (Casual), or a status per
/// card with a label saying which list it is.
/// </summary>
/// <param name="Enforced">Whether the list applies at all.</param>
/// <param name="StatusOf">A card's status on the list.</param>
/// <param name="Label">Which list this is, for messages, e.g. "TCG list first seen 2026-11-07".</param>
public sealed record BanlistRules(bool Enforced, Func<int, Cards.BanStatus> StatusOf, string Label)
{
    /// <summary>No list (Casual).</summary>
    public static BanlistRules None { get; } = new(false, _ => Cards.BanStatus.Unlimited, "no Forbidden & Limited list");
}
