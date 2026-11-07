using DuelLab.Domain.Cards;
using DuelLab.Domain.Common;
using DuelLab.Domain.Decks;

namespace DuelLab.Domain.Tests;

public sealed class ComboLineTests
{
    private static readonly DateTimeOffset Now = TestData.Now;

    private static DeckVersion VersionWith(params int[] cardIds)
    {
        var version = Deck.Create("Deck", Now).Versions[0];
        version.ReplaceCards(cardIds.Select(id => new DeckCardEntry(id, DeckZone.Main, 3)), Now);
        return version;
    }

    private static ComboLineDraft Line(string name, params ComboPiece[] pieces) => new(name, null, pieces);

    [Fact]
    public void ReplaceComboLines_StoresNormalisedLinesInOrder()
    {
        var version = VersionWith(1, 2, 3);

        version.ReplaceComboLines(
        [
            Line(" Ash line ", new ComboPiece([1, 1], ["starter"]), new ComboPiece([2, 3], [], 2)),
            Line("Second", new ComboPiece([], ["Custom:Fusion line"])),
        ], Now);

        Assert.Equal(["Ash line", "Second"], version.ComboLines.Select(l => l.Name));
        var first = version.ComboLines[0].Pieces;
        Assert.Equal([1], first[0].CardIds);
        Assert.Equal(["Starter"], first[0].Roles);
        Assert.Equal(2, first[1].Count);
    }

    [Theory]
    [InlineData("combo.name_required", "  ")]
    [InlineData("combo.piece_count", "no pieces")]
    [InlineData("combo.empty_piece", "empty piece")]
    [InlineData("combo.card_not_in_version", "unknown card")]
    [InlineData("combo.piece_quantity", "too many copies")]
    [InlineData("role.unknown", "bad role")]
    public void InvalidLines_AreRejected(string expectedCode, string scenario)
    {
        var version = VersionWith(1, 2);
        var draft = scenario switch
        {
            "no pieces" => Line("x"),
            "empty piece" => Line("x", new ComboPiece([], [])),
            "unknown card" => Line("x", new ComboPiece([99], [])),
            "too many copies" => Line("x", new ComboPiece([1], [], 4)),
            "bad role" => Line("x", new ComboPiece([], ["Wizard"])),
            _ => Line(scenario, new ComboPiece([1], [])),
        };

        var ex = Assert.Throws<DomainException>(() => version.ReplaceComboLines([draft], Now));

        Assert.Equal(expectedCode, ex.Code);
    }

    [Fact]
    public void DuplicateNames_AreRejectedIgnoringCase()
    {
        var version = VersionWith(1);

        var ex = Assert.Throws<DomainException>(() => version.ReplaceComboLines(
            [Line("Line", new ComboPiece([1], [])), Line("LINE", new ComboPiece([1], []))], Now));

        Assert.Equal("combo.duplicate_name", ex.Code);
    }

    [Fact]
    public void TooManyLines_AreRejected()
    {
        var version = VersionWith(1);
        var lines = Enumerable.Range(0, DeckVersionComboLine.MaxPerVersion + 1).Select(i => Line($"L{i}", new ComboPiece([1], [])));

        Assert.Equal("combo.too_many", Assert.Throws<DomainException>(() => version.ReplaceComboLines(lines, Now)).Code);
    }

    [Fact]
    public void DuplicateVersion_CopiesComboLines()
    {
        var deck = Deck.Create("Deck", Now);
        var source = deck.Versions[0];
        source.ReplaceCards([new(1, DeckZone.Main, 3)], Now);
        source.ReplaceComboLines([Line("Line", new ComboPiece([1], []))], Now);

        var copy = deck.DuplicateVersion(source.Id, "v2", Now);

        var line = Assert.Single(copy.ComboLines);
        Assert.Equal(copy.Id, line.DeckVersionId);
        Assert.NotEqual(source.ComboLines[0].Id, line.Id);
        Assert.Equal("Line", line.Name);
    }

    [Fact]
    public void RemovingACard_KeepsTheLineAndValidationWarns()
    {
        var version = VersionWith(1, 2);
        version.ReplaceComboLines([Line("Line", new ComboPiece([1, 2], []))], Now);
        version.ReplaceCards([new(1, DeckZone.Main, 3), new(2, DeckZone.Side, 1)], Now);

        var result = DeckValidator.Validate(version, new Dictionary<int, Card> { [1] = TestData.Card(1, "A"), [2] = TestData.Card(2, "B") });

        Assert.Equal([1, 2], version.ComboLines[0].Pieces[0].CardIds);
        var warning = Assert.Single(result.Issues, i => i.Code == "combo.card_not_in_main");
        Assert.Equal(ValidationSeverity.Warning, warning.Severity);
        Assert.Contains("B", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LinesUsingARemovedCard_CanStillBeResaved_ButNewReferencesCannot()
    {
        var version = VersionWith(1, 2);
        version.ReplaceComboLines([Line("Line", new ComboPiece([1, 2], []))], Now);
        version.ReplaceCards([new(1, DeckZone.Main, 3)], Now); // card 2 leaves the version

        version.ReplaceComboLines([Line("Line renamed", new ComboPiece([1, 2], []))], Now);

        Assert.Equal("Line renamed", version.ComboLines[0].Name);
        Assert.Equal("combo.card_not_in_version",
            Assert.Throws<DomainException>(() => version.ReplaceComboLines([Line("Line", new ComboPiece([3], []))], Now)).Code);
    }
}
