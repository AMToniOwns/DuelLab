using DuelLab.Domain.Common;
using DuelLab.Domain.Decks;

namespace DuelLab.Domain.Tests;

public sealed class CardRoleTests
{
    [Fact]
    public void Custom_RequiresName()
    {
        var ex = Assert.Throws<DomainException>(() => new CardRole(CardRoleKind.Custom, "  "));

        Assert.Equal("role.custom_name_required", ex.Code);
    }

    [Fact]
    public void BuiltIn_RejectsName()
    {
        Assert.Throws<DomainException>(() => new CardRole(CardRoleKind.Starter, "Name"));
    }

    [Fact]
    public void CustomNames_CompareCaseInsensitively()
    {
        var a = new CardRole(CardRoleKind.Custom, "Fusion line");
        var b = new CardRole(CardRoleKind.Custom, "fusion LINE");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Theory]
    [InlineData("Starter")]
    [InlineData("HandTrap")]
    [InlineData("Custom:Fusion line")]
    public void Parse_RoundTripsKey(string key)
    {
        Assert.Equal(key, CardRole.Parse(key).Key);
    }

    [Theory]
    [InlineData("Wizard")]
    [InlineData("Custom")]
    [InlineData("Custom:")]
    public void Parse_RejectsUnknownKeys(string key)
    {
        Assert.Throws<DomainException>(() => CardRole.Parse(key));
    }
}
