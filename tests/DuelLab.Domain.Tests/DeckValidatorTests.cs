using DuelLab.Domain.Cards;
using DuelLab.Domain.Decks;

namespace DuelLab.Domain.Tests;

public sealed class DeckValidatorTests
{
    private static readonly DateTimeOffset Now = TestData.Now;

    private static (DeckVersion Version, Dictionary<int, Card> Cards) FortyCardDeck()
    {
        var version = Deck.Create("Deck", Now).Versions[0];
        var cards = new Dictionary<int, Card>();
        var entries = new List<DeckCardEntry>();
        for (var id = 1; id <= 14; id++)
        {
            cards[id] = TestData.Card(id, $"Card {id}");
            entries.Add(new(id, DeckZone.Main, id <= 13 ? 3 : 1)); // 13*3 + 1 = 40
        }

        version.ReplaceCards(entries, Now);
        return (version, cards);
    }

    [Fact]
    public void FortyCardMainDeck_IsValid()
    {
        var (version, cards) = FortyCardDeck();

        var result = DeckValidator.Validate(version, cards);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void MainDeckUnderForty_IsAnError()
    {
        var version = Deck.Create("Deck", Now).Versions[0];
        version.ReplaceCards([new(1, DeckZone.Main, 3)], Now);

        var result = DeckValidator.Validate(version, new Dictionary<int, Card> { [1] = TestData.Card(1, "A") });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "deck.main_too_small");
    }

    [Fact]
    public void ExtraDeckMonsterInMain_IsAnError()
    {
        var (version, cards) = FortyCardDeck();
        cards[1] = TestData.Card(1, "Link Monster", extra: true);

        var result = DeckValidator.Validate(version, cards);

        Assert.Contains(result.Issues, i => i.Code == "deck.extra_card_in_main" && i.CardId == 1);
    }

    [Fact]
    public void MainDeckCardInExtra_IsAnError()
    {
        var version = Deck.Create("Deck", Now).Versions[0];
        version.ReplaceCards([new(1, DeckZone.Extra, 1)], Now);

        var result = DeckValidator.Validate(version, new Dictionary<int, Card> { [1] = TestData.Card(1, "Spell") });

        Assert.Contains(result.Issues, i => i.Code == "deck.main_card_in_extra");
    }

    [Fact]
    public void ExtraDeckMonsterInSide_IsAllowed()
    {
        var (version, cards) = FortyCardDeck();
        cards[99] = TestData.Card(99, "Link", extra: true);
        version.ReplaceCards([.. version.Cards.Select(c => new DeckCardEntry(c.CardId, c.Zone, c.Quantity)), new(99, DeckZone.Side, 1)], Now);

        Assert.True(DeckValidator.Validate(version, cards).IsValid);
    }

    [Fact]
    public void ExceedingBanlistSnapshot_IsOnlyAWarning()
    {
        var (version, cards) = FortyCardDeck();
        cards[1] = TestData.Card(1, "Limited card", ban: BanStatus.Limited); // the deck has 3

        var result = DeckValidator.Validate(version, cards);

        Assert.True(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal("deck.banlist", issue.Code);
    }

    [Fact]
    public void UnknownCard_IsAnError()
    {
        var (version, cards) = FortyCardDeck();
        cards.Remove(2);

        Assert.Contains(DeckValidator.Validate(version, cards).Issues, i => i.Code == "deck.unknown_card");
    }
}
