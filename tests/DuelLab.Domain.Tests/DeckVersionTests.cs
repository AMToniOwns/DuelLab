using DuelLab.Domain.Common;
using DuelLab.Domain.Decks;

namespace DuelLab.Domain.Tests;

public sealed class DeckVersionTests
{
    private static readonly DateTimeOffset Now = TestData.Now;

    private static DeckVersion NewVersion() => Deck.Create("Deck", Now).Versions[0];

    [Fact]
    public void ReplaceCards_AllowsAnIncompleteDeck()
    {
        var version = NewVersion();

        version.ReplaceCards([new(1, DeckZone.Main, 3), new(2, DeckZone.Main, 2)], Now);

        Assert.Equal(5, version.CountIn(DeckZone.Main));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReplaceCards_RejectsQuantityBelowOne(int quantity)
    {
        var ex = Assert.Throws<DomainException>(() => NewVersion().ReplaceCards([new(1, DeckZone.Main, quantity)], Now));

        Assert.Equal("deck.quantity_too_low", ex.Code);
    }

    [Fact]
    public void ReplaceCards_RejectsMoreThanThreeCopiesAcrossZones()
    {
        // 2 in Main + 2 in Side = 4 copies of the same card.
        var ex = Assert.Throws<DomainException>(
            () => NewVersion().ReplaceCards([new(1, DeckZone.Main, 2), new(1, DeckZone.Side, 2)], Now));

        Assert.Equal("deck.copy_limit", ex.Code);
    }

    [Fact]
    public void ReplaceCards_AllowsThreeCopiesSplitAcrossZones()
    {
        var version = NewVersion();

        version.ReplaceCards([new(1, DeckZone.Main, 2), new(1, DeckZone.Side, 1)], Now);

        Assert.Equal(2, version.Cards.Count);
    }

    [Fact]
    public void ReplaceCards_RejectsTheSameCardTwiceInOneZone()
    {
        var ex = Assert.Throws<DomainException>(
            () => NewVersion().ReplaceCards([new(1, DeckZone.Main, 1), new(1, DeckZone.Main, 1)], Now));

        Assert.Equal("deck.duplicate_entry", ex.Code);
    }

    [Theory]
    [InlineData(DeckZone.Main, 61)]
    [InlineData(DeckZone.Extra, 16)]
    [InlineData(DeckZone.Side, 16)]
    public void ReplaceCards_RejectsZonesOverTheirMaximum(DeckZone zone, int size)
    {
        var entries = FillZone(zone, size);

        var ex = Assert.Throws<DomainException>(() => NewVersion().ReplaceCards(entries, Now));

        Assert.Equal("deck.zone_too_large", ex.Code);
    }

    [Theory]
    [InlineData(DeckZone.Main, 60)]
    [InlineData(DeckZone.Extra, 15)]
    [InlineData(DeckZone.Side, 15)]
    public void ReplaceCards_AllowsZonesAtTheirMaximum(DeckZone zone, int size)
    {
        var version = NewVersion();

        version.ReplaceCards(FillZone(zone, size), Now);

        Assert.Equal(size, version.CountIn(zone));
    }

    [Fact]
    public void ReplaceCards_RejectsUnknownZone()
    {
        var ex = Assert.Throws<DomainException>(() => NewVersion().ReplaceCards([new(1, (DeckZone)99, 1)], Now));

        Assert.Equal("deck.unknown_zone", ex.Code);
    }

    [Fact]
    public void ReplaceCards_FailureLeavesContentsUnchanged()
    {
        var version = NewVersion();
        version.ReplaceCards([new(1, DeckZone.Main, 3)], Now);

        Assert.Throws<DomainException>(() => version.ReplaceCards([new(2, DeckZone.Main, 4)], Now));

        Assert.Equal(1, Assert.Single(version.Cards).CardId);
    }

    [Fact]
    public void ReplaceCards_RemovesRolesOfCardsNoLongerInTheVersion()
    {
        var version = NewVersion();
        version.ReplaceCards([new(1, DeckZone.Main, 3), new(2, DeckZone.Main, 3)], Now);
        version.ReplaceRoles([(1, new CardRole(CardRoleKind.Starter)), (2, new CardRole(CardRoleKind.Brick))], Now);

        version.ReplaceCards([new(1, DeckZone.Main, 3)], Now);

        Assert.Single(version.RolesOf(1));
        Assert.Empty(version.RolesOf(2));
    }

    [Fact]
    public void ReplaceRoles_AllowsSeveralRolesPerCardAndIgnoresDuplicates()
    {
        var version = NewVersion();
        version.ReplaceCards([new(1, DeckZone.Main, 3)], Now);

        version.ReplaceRoles(
        [
            (1, new CardRole(CardRoleKind.Starter)),
            (1, new CardRole(CardRoleKind.Extender)),
            (1, new CardRole(CardRoleKind.Starter)),
            (1, new CardRole(CardRoleKind.Custom, "Combo piece")),
            (1, new CardRole(CardRoleKind.Custom, "COMBO PIECE")),
        ], Now);

        Assert.Equal(3, version.RolesOf(1).Count);
    }

    [Fact]
    public void ReplaceRoles_RejectsCardsNotInTheVersion()
    {
        var version = NewVersion();
        version.ReplaceCards([new(1, DeckZone.Main, 3)], Now);

        var ex = Assert.Throws<DomainException>(() => version.ReplaceRoles([(2, new CardRole(CardRoleKind.Starter))], Now));

        Assert.Equal("role.card_not_in_version", ex.Code);
    }

    [Fact]
    public void UpdateDetails_SetsNameAndNotes()
    {
        var version = NewVersion();

        version.UpdateDetails(" v2 ", "  Cut a brick ", Now);

        Assert.Equal("v2", version.VersionName);
        Assert.Equal("Cut a brick", version.Notes);
    }

    private static List<DeckCardEntry> FillZone(DeckZone zone, int size)
    {
        var entries = new List<DeckCardEntry>();
        var cardId = 1;
        while (size > 0)
        {
            var quantity = Math.Min(3, size);
            entries.Add(new(cardId++, zone, quantity));
            size -= quantity;
        }

        return entries;
    }
}
