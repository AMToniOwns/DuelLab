using System.Net;
using System.Net.Http.Json;
using DuelLab.Application.Common;
using DuelLab.Application.Decks;
using DuelLab.Domain.Decks;

namespace DuelLab.Api.Tests;

public sealed class YdkParserTests
{
    [Fact]
    public void Parse_HandlesRealWorldQuirks()
    {
        // Byte-order mark, CRLF, trailing spaces after headers, a "#created by" comment, passcodes
        // without leading zeros, a blank line and a stray word.
        const string content = "﻿#created by someone\r\n#main \r\n14558127\r\n9726840\r\n\r\nhello\r\n#extra \r\n41999284\r\n!side \r\n05318639\r\n";

        var deck = Ydk.Parse(content);

        Assert.Equal([14558127L, 9726840L], deck.Main);
        Assert.Equal([41999284L], deck.Extra);
        Assert.Equal([5318639L], deck.Side);
        Assert.Equal(["Line 6 ('hello') is not a card passcode and was skipped."], deck.Warnings);
    }

    [Fact]
    public void Parse_TreatsCardsBeforeAnyHeaderAsMain()
    {
        Assert.Equal([1L, 2L], Ydk.Parse("1\n2\n").Main);
    }

    [Theory]
    [InlineData("", "import.empty")]
    [InlineData("#main\n#extra\n!side\n", "import.empty")]
    public void Parse_RejectsFilesWithoutCards(string content, string code)
    {
        Assert.Equal(code, Assert.Throws<RequestValidationException>(() => Ydk.Parse(content)).Code);
    }

    [Fact]
    public void Parse_RejectsHugeFiles()
    {
        Assert.Equal("import.too_large", Assert.Throws<RequestValidationException>(() => Ydk.Parse(new string('1', Ydk.MaxLength + 1))).Code);
        Assert.Equal("import.too_many", Assert.Throws<RequestValidationException>(() => Ydk.Parse(string.Join('\n', Enumerable.Repeat("1", 201)))).Code);
    }

    [Fact]
    public void Write_ProducesTheStandardLayoutWithLeadingZeros()
    {
        var text = Ydk.Write([(5318639, DeckZone.Main, 2), (41999284, DeckZone.Extra, 1)]);

        Assert.Equal("#created by DuelLab\n#main\n05318639\n05318639\n#extra\n41999284\n!side\n", text);
        Assert.Equal([5318639L, 5318639L], Ydk.Parse(text).Main);
    }
}

public sealed class YdkImportApiTests(DuelLabApiFactory factory) : ApiTestBase(factory)
{
    private async Task<long> Passcode(string name) => (await CardAsync(name)).ExternalId;

    [Fact]
    public async Task Import_CreatesADeckAndReportsWhatNeededAttention()
    {
        var ash = await Passcode("Ash Blossom & Joyous Spring");
        var raigeki = await Passcode("Raigeki");
        var talker = await Passcode("Accesscode Talker");
        var lines = new List<string> { "#created by test", "#main " };
        lines.AddRange(Enumerable.Repeat("14558128", 2));     // Ash, alternate artwork
        lines.Add(ash.ToString(System.Globalization.CultureInfo.InvariantCulture)); // Ash, main artwork
        lines.AddRange(Enumerable.Repeat(raigeki.ToString(System.Globalization.CultureInfo.InvariantCulture), 3));
        lines.Add("99999999");                                 // not a card
        lines.Add("#extra ");
        lines.Add(talker.ToString(System.Globalization.CultureInfo.InvariantCulture));
        lines.Add("!side ");
        lines.Add(raigeki.ToString(System.Globalization.CultureInfo.InvariantCulture)); // 4th Raigeki: trimmed

        var result = await PostAsync<DeckImportResultDto>("/api/decks/import", new ImportYdkRequest("Imported deck", string.Join("\r\n", lines), "Casual"));

        Assert.Equal("Imported deck", result.Deck.Name);
        Assert.Equal(DeckFormats.Casual, result.Deck.Format);
        Assert.Equal(new ZoneCountsDto(6, 1, 0), result.Report.Imported);
        Assert.Equal(2, result.Report.AlternateArtworks);
        Assert.Equal(new UnknownPasscodeDto(99999999, DeckZone.Main, 1), Assert.Single(result.Report.Unknown));
        var trimmed = Assert.Single(result.Report.Trimmed);
        Assert.Equal(("Raigeki", 4, 3), (trimmed.Name, trimmed.Listed, trimmed.Kept));

        var ashEntry = result.Version.Cards.Single(c => c.Card.Name == "Ash Blossom & Joyous Spring");
        Assert.Equal(3, ashEntry.Quantity);
        Assert.Contains(result.Version.Cards, c => c.Card.Name == "Accesscode Talker" && c.Zone == DeckZone.Extra);
        Assert.Contains(result.Version.Validation.Issues, i => i.Code == "deck.main_too_small");
    }

    [Fact]
    public async Task Import_KeepsBanlistProblemsForTheDeckCheck()
    {
        var called = await Passcode("Called by the Grave"); // Forbidden

        var result = await PostAsync<DeckImportResultDto>("/api/decks/import", new ImportYdkRequest("TCG", $"#main\n{called}\n"));

        Assert.Equal(DeckFormats.TcgAdvanced, result.Deck.Format);
        Assert.Contains(result.Version.Validation.Issues, i => i.Code == "deck.banlist");
    }

    [Fact]
    public async Task Export_RoundTripsThroughImport()
    {
        var (deck, version) = await CreateLegalDeckAsync("Round trip");
        var talker = (await CardAsync("Accesscode Talker")).Id;
        version = await PutAsync<VersionDetailDto>($"/api/versions/{version.Id}/cards", new UpdateCardsRequest(
            [.. version.Cards.Select(c => new CardEntryRequest(c.Card.Id, c.Zone, c.Quantity)), new(talker, DeckZone.Extra, 1)]));

        var response = await Client.GetAsync($"/api/versions/{version.Id}/export.ydk");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Round trip - v1.ydk", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var imported = await PostAsync<DeckImportResultDto>($"/api/decks/{deck.Id}/versions/import", new ImportYdkVersionRequest("re-imported", text));
        Assert.Equal(
            version.Cards.Select(c => (c.Card.Id, c.Zone, c.Quantity)).OrderBy(x => x.Id).ThenBy(x => x.Zone),
            imported.Version.Cards.Select(c => (c.Card.Id, c.Zone, c.Quantity)).OrderBy(x => x.Id).ThenBy(x => x.Zone));
        Assert.Empty(imported.Report.Unknown);
        Assert.Equal(2, imported.Deck.Versions.Count);
    }

    [Fact]
    public async Task ImportReport_UsesTheDocumentedJsonShape()
    {
        var raigeki = await Passcode("Raigeki");

        var response = await Client.PostAsJsonAsync("/api/decks/import", new { name = "Shape", content = $"#main\n{raigeki}\n" });
        var json = System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        Assert.Equal(1, json["report"]!["imported"]!["main"]!.GetValue<int>());
        Assert.Equal(0, json["report"]!["imported"]!["side"]!.GetValue<int>());
    }

    [Fact]
    public async Task Resolve_ReturnsCardsAndReportWithoutSavingAnything()
    {
        var before = (await GetAsync<List<DeckSummaryDto>>("/api/decks")).Count;
        var raigeki = await Passcode("Raigeki");

        var resolved = await PostAsync<YdkResolutionDto>("/api/decks/ydk/resolve", new ResolveYdkRequest($"#main\n{raigeki}\n{raigeki}\n14558128\n!side\n99999999\n"));

        Assert.Equal(before, (await GetAsync<List<DeckSummaryDto>>("/api/decks")).Count);
        Assert.Equal(2, resolved.Cards.Single(c => c.Card.Name == "Raigeki").Quantity);
        Assert.Contains(resolved.Cards, c => c.Card.Name == "Ash Blossom & Joyous Spring" && c.Zone == DeckZone.Main);
        Assert.Equal(1, resolved.Report.AlternateArtworks);
        Assert.Single(resolved.Report.Unknown);
        Assert.Equal("import.empty", (await FailAsync(HttpMethod.Post, "/api/decks/ydk/resolve", new ResolveYdkRequest("nothing"))).Code);
    }

    [Theory]
    [InlineData("#main\n99999999\n", "import.no_known_cards")]
    [InlineData("just some text", "import.empty")]
    public async Task Import_RejectsUnusableFiles(string content, string code)
    {
        var (status, actual, _) = await FailAsync(HttpMethod.Post, "/api/decks/import", new ImportYdkRequest("Bad", content));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(code, actual);
    }

    [Fact]
    public async Task Import_RejectsAZoneOverItsMaximum()
    {
        var names = new[] { "Raigeki", "Dark Magician", "Mirror Force", "Polymerization", "Effect Veiler", "Infinite Impermanence" };
        var side = new List<string> { "!side" };
        foreach (var n in names)
        {
            side.AddRange(Enumerable.Repeat((await Passcode(n)).ToString(System.Globalization.CultureInfo.InvariantCulture), 3));
        }

        var (_, code, detail) = await FailAsync(HttpMethod.Post, "/api/decks/import", new ImportYdkRequest("Too big", string.Join('\n', side)));

        Assert.Equal("import.zone_too_large", code);
        Assert.Contains("18 cards", detail, StringComparison.Ordinal);
    }
}
