using DuelLab.Domain.Cards;
using DuelLab.Domain.Common;
using DuelLab.Domain.Decks;

namespace DuelLab.Domain.Tests;

public sealed class BanlistTests
{
    private static readonly DateTimeOffset Now = TestData.Now;

    [Fact]
    public void Create_DropsUnlimitedAndDuplicates()
    {
        var list = Banlist.Create([(3, BanStatus.Limited), (1, BanStatus.Forbidden), (2, BanStatus.Unlimited), (3, BanStatus.SemiLimited)], Now, "147.67", Now.AddDays(-3));

        Assert.Equal([1, 3], list.Entries.Select(e => e.CardId));
        Assert.Equal(BanStatus.Limited, list.StatusOf(3));
        Assert.Equal(BanStatus.Unlimited, list.StatusOf(2));
        Assert.Equal("147.67", list.ProviderVersion);
        Assert.Equal("TCG list first seen 2026-11-07", list.Label);
    }

    [Fact]
    public void Hash_IgnoresOrderAndUnlimitedButNoticesChanges()
    {
        var a = Banlist.HashOf([(1, BanStatus.Forbidden), (2, BanStatus.Limited), (3, BanStatus.Unlimited)]);
        var b = Banlist.HashOf([(2, BanStatus.Limited), (1, BanStatus.Forbidden)]);
        var c = Banlist.HashOf([(2, BanStatus.SemiLimited), (1, BanStatus.Forbidden)]);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(a, Banlist.Create([(1, BanStatus.Forbidden), (2, BanStatus.Limited)], Now).ContentHash);
    }

    [Fact]
    public void Deck_DefaultsToTcgAdvancedFollowingTheCurrentList()
    {
        var deck = Deck.Create("Deck", Now);

        Assert.Equal(DeckFormats.TcgAdvanced, deck.Format);
        Assert.Null(deck.BanlistId);
    }

    [Fact]
    public void SetFormat_NormalisesAndPins()
    {
        var deck = Deck.Create("Deck", Now);
        var list = Guid.NewGuid();

        deck.SetFormat("tcg advanced", list, Now);
        Assert.Equal(DeckFormats.TcgAdvanced, deck.Format);
        Assert.Equal(list, deck.BanlistId);

        deck.SetFormat("Casual", null, Now);
        Assert.Equal(DeckFormats.Casual, deck.Format);
        Assert.Null(deck.BanlistId);
    }

    [Fact]
    public void SetFormat_RejectsUnknownFormatsAndPinnedCasual()
    {
        var deck = Deck.Create("Deck", Now);

        Assert.Equal("deck.unknown_format", Assert.Throws<DomainException>(() => deck.SetFormat("Goat", null, Now)).Code);
        Assert.Equal("deck.banlist_needs_tcg", Assert.Throws<DomainException>(() => deck.SetFormat(DeckFormats.Casual, Guid.NewGuid(), Now)).Code);
    }
}
