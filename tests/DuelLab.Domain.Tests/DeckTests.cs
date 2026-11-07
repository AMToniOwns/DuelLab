using DuelLab.Domain.Common;
using DuelLab.Domain.Decks;

namespace DuelLab.Domain.Tests;

public sealed class DeckTests
{
    private static readonly DateTimeOffset Now = TestData.Now;

    [Fact]
    public void Create_TrimsNameAndStartsWithOneEmptyVersion()
    {
        var deck = Deck.Create("  Snake-Eye  ", Now);

        Assert.Equal("Snake-Eye", deck.Name);
        Assert.Equal(DeckRules.DefaultFormat, deck.Format);
        var version = Assert.Single(deck.Versions);
        Assert.Equal(deck.Id, version.DeckId);
        Assert.Empty(version.Cards);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_RejectsBlankName(string? name)
    {
        var ex = Assert.Throws<DomainException>(() => Deck.Create(name!, Now));

        Assert.Equal("deck.name_required", ex.Code);
    }

    [Fact]
    public void Create_RejectsOverlongName()
    {
        Assert.Throws<DomainException>(() => Deck.Create(new string('x', DeckRules.MaxNameLength + 1), Now));
    }

    [Fact]
    public void Rename_UpdatesNameAndTimestamp()
    {
        var deck = Deck.Create("Old", Now);

        deck.Rename("New", Now.AddHours(1));

        Assert.Equal("New", deck.Name);
        Assert.Equal(Now.AddHours(1), deck.UpdatedAt);
    }

    [Fact]
    public void DuplicateVersion_CopiesCardsAndRolesWithoutChangingTheSource()
    {
        var deck = Deck.Create("Deck", Now);
        var source = deck.Versions[0];
        source.ReplaceCards([new(1, DeckZone.Main, 3), new(2, DeckZone.Side, 2)], Now);
        source.ReplaceRoles([(1, new CardRole(CardRoleKind.Starter)), (1, new CardRole(CardRoleKind.Searcher))], Now);

        var copy = deck.DuplicateVersion(source.Id, "v2", Now.AddMinutes(5));

        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal(source.Id, copy.ParentVersionId);
        Assert.Equal("v2", copy.VersionName);
        Assert.Equal(
            source.Cards.Select(c => (c.CardId, c.Zone, c.Quantity)),
            copy.Cards.Select(c => (c.CardId, c.Zone, c.Quantity)));
        Assert.All(copy.Cards, c => Assert.Equal(copy.Id, c.DeckVersionId));
        Assert.Equal(source.RolesOf(1), copy.RolesOf(1));

        // Editing the copy leaves the source alone.
        copy.ReplaceCards([new(1, DeckZone.Main, 1)], Now.AddMinutes(6));
        Assert.Equal(3, source.CountIn(DeckZone.Main));
        Assert.Equal(2, source.CountIn(DeckZone.Side));
        Assert.Equal(2, source.RolesOf(1).Count);
    }

    [Fact]
    public void RemoveVersion_KeepsAtLeastOneVersion()
    {
        var deck = Deck.Create("Deck", Now);

        var ex = Assert.Throws<DomainException>(() => deck.RemoveVersion(deck.Versions[0].Id, Now));

        Assert.Equal("deck.last_version", ex.Code);
    }

    [Fact]
    public void RemoveVersion_RemovesTheRequestedVersion()
    {
        var deck = Deck.Create("Deck", Now);
        var v2 = deck.AddEmptyVersion("v2", Now);

        deck.RemoveVersion(deck.Versions[0].Id, Now);

        Assert.Equal(v2.Id, Assert.Single(deck.Versions).Id);
    }
}
