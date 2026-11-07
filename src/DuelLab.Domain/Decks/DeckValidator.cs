using DuelLab.Domain.Cards;

namespace DuelLab.Domain.Decks;

/// <summary>How serious a validation issue is.</summary>
public enum ValidationSeverity
{
    /// <summary>The deck cannot be used until this is fixed. Simulation is refused.</summary>
    Error,

    /// <summary>Worth knowing, but it does not block anything.</summary>
    Warning,
}

/// <summary>One problem found by <see cref="DeckValidator"/>.</summary>
public sealed record DeckValidationIssue(ValidationSeverity Severity, string Code, string Message, int? CardId = null);

/// <summary>The outcome of validating a deck version.</summary>
public sealed record DeckValidationResult(IReadOnlyList<DeckValidationIssue> Issues)
{
    /// <summary>True when there are no errors (warnings are allowed).</summary>
    public bool IsValid => Issues.All(i => i.Severity != ValidationSeverity.Error);
}

/// <summary>
/// Checks a deck version against the deck-construction rules that need card data: zone sizes,
/// Extra Deck placement and, for TCG Advanced, the Forbidden &amp; Limited list.
/// </summary>
public static class DeckValidator
{
    /// <summary>
    /// Validates <paramref name="version"/> under the current list as recorded on each card.
    /// <paramref name="cards"/> must contain every card it uses.
    /// </summary>
    public static DeckValidationResult Validate(DeckVersion version, IReadOnlyDictionary<int, Card> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        return Validate(version, cards, CurrentListOnCards(cards));
    }

    /// <summary>The current list as recorded on the cards themselves (their last-synced status).</summary>
    public static BanlistRules CurrentListOnCards(IReadOnlyDictionary<int, Card> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        var synced = cards.Values.Select(c => c.LastSyncedAt).DefaultIfEmpty().Max();
        return new BanlistRules(
            true,
            id => cards.TryGetValue(id, out var card) ? card.TcgBanStatus : BanStatus.Unlimited,
            synced == default ? "current TCG list" : $"current TCG list, synced {synced:yyyy-MM-dd}");
    }

    /// <summary>Validates <paramref name="version"/> under the given Forbidden &amp; Limited rules.</summary>
    public static DeckValidationResult Validate(DeckVersion version, IReadOnlyDictionary<int, Card> cards, BanlistRules banlist)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(banlist);

        var issues = new List<DeckValidationIssue>();

        var main = version.CountIn(DeckZone.Main);
        if (main < DeckRules.MainDeckMin)
        {
            issues.Add(new(
                ValidationSeverity.Error,
                "deck.main_too_small",
                $"The Main Deck has {main} cards; it needs at least {DeckRules.MainDeckMin}."));
        }

        foreach (var entry in version.Cards)
        {
            if (!cards.TryGetValue(entry.CardId, out var card))
            {
                issues.Add(new(ValidationSeverity.Error, "deck.unknown_card", $"Card {entry.CardId} is not in the catalogue.", entry.CardId));
                continue;
            }

            if (entry.Zone == DeckZone.Main && card.IsExtraDeckCard)
            {
                issues.Add(new(
                    ValidationSeverity.Error,
                    "deck.extra_card_in_main",
                    $"{card.Name} is an Extra Deck monster and cannot be in the Main Deck.",
                    card.Id));
            }
            else if (entry.Zone == DeckZone.Extra && !card.IsExtraDeckCard)
            {
                issues.Add(new(
                    ValidationSeverity.Error,
                    "deck.main_card_in_extra",
                    $"{card.Name} is not an Extra Deck monster and cannot be in the Extra Deck.",
                    card.Id));
            }
        }

        if (banlist.Enforced)
        {
            foreach (var group in version.Cards.GroupBy(c => c.CardId))
            {
                var status = banlist.StatusOf(group.Key);
                var copies = group.Sum(c => c.Quantity);
                var allowed = status.MaxCopies();
                if (copies <= allowed)
                {
                    continue;
                }

                var name = cards.TryGetValue(group.Key, out var card) ? card.Name : $"Card {group.Key}";
                issues.Add(new(
                    ValidationSeverity.Error,
                    "deck.banlist",
                    status == BanStatus.Forbidden
                        ? $"{name} is Forbidden ({banlist.Label}); remove all {copies} {(copies == 1 ? "copy" : "copies")}, or switch the deck to Casual."
                        : $"{name} is {Describe(status)} ({banlist.Label}): at most {allowed} {(allowed == 1 ? "copy" : "copies")} across Main, Extra and Side Deck; this deck has {copies}.",
                    group.Key));
            }
        }

        var mainIds = version.Cards.Where(c => c.Zone == DeckZone.Main).Select(c => c.CardId).ToHashSet();
        foreach (var line in version.ComboLines)
        {
            foreach (var cardId in line.Pieces.SelectMany(p => p.CardIds).Distinct().Where(id => !mainIds.Contains(id)))
            {
                var name = cards.TryGetValue(cardId, out var card) ? card.Name : $"Card {cardId}";
                issues.Add(new(
                    ValidationSeverity.Warning,
                    "combo.card_not_in_main",
                    $"Combo line '{line.Name}' uses {name}, which is not in the Main Deck, so that option can never be in your opening hand.",
                    cardId));
            }
        }

        return new DeckValidationResult(issues);
    }

    private static string Describe(BanStatus status) => status switch
    {
        BanStatus.Forbidden => "Forbidden",
        BanStatus.Limited => "Limited",
        BanStatus.SemiLimited => "Semi-Limited",
        _ => "Unlimited",
    };
}
