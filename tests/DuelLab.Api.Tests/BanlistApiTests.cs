using System.Net;
using System.Text.Json.Nodes;
using DuelLab.Application.Banlists;
using DuelLab.Application.Cards;
using DuelLab.Application.Decks;
using DuelLab.Application.Simulations;
using DuelLab.Domain.Cards;
using DuelLab.Domain.Decks;
using Microsoft.Extensions.DependencyInjection;

namespace DuelLab.Api.Tests;

public sealed class BanlistApiTests(DuelLabApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Import_RecordsTheCurrentList()
    {
        var lists = await GetAsync<List<BanlistSummaryDto>>("/api/banlists");
        var current = await GetAsync<BanlistDto>("/api/banlists/current");

        var summary = Assert.Single(lists);
        Assert.True(summary.IsCurrent);
        // Fixture: 4 Forbidden (e.g. Called by the Grave, Pot of Greed), 5 Limited, 0 Semi-Limited.
        Assert.Equal((4, 5, 0), (summary.Forbidden, summary.Limited, summary.SemiLimited));
        Assert.Equal(summary.Id, current.Summary.Id);
        Assert.Equal(BanStatus.Forbidden, current.Entries.Single(e => e.Name == "Pot of Greed").Status);
        Assert.Equal(BanStatus.Forbidden, current.Entries[0].Status); // most restricted first
    }

    [Fact]
    public async Task UnchangedImport_DoesNotAddASnapshot()
    {
        using var scope = Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<CardCatalogService>().SyncAsync(force: true, CancellationToken.None);

        Assert.False(result.BanlistChanged);
        Assert.Single(await GetAsync<List<BanlistSummaryDto>>("/api/banlists"));
    }

    [Fact]
    public async Task TcgAdvanced_EnforcesTheListAndBlocksSimulation_CasualDoesNot()
    {
        var (deck, version) = await CreateLegalDeckAsync();
        var prosperity = await CardAsync("Pot of Prosperity"); // Limited
        var entries = version.Cards.Select(c => new CardEntryRequest(c.Card.Id, c.Zone, c.Quantity)).ToList();

        var illegal = await PutAsync<VersionDetailDto>($"/api/versions/{version.Id}/cards", new UpdateCardsRequest([.. entries, new(prosperity.Id, DeckZone.Side, 2)]));

        Assert.Equal(DeckFormats.TcgAdvanced, illegal.Format);
        var error = Assert.Single(illegal.Validation.Issues, i => i.Code == "deck.banlist");
        Assert.Equal(ValidationSeverity.Error, error.Severity);
        Assert.Contains("Limited", error.Message, StringComparison.Ordinal);
        Assert.Equal("simulation.invalid_deck", (await FailAsync(HttpMethod.Post, $"/api/versions/{version.Id}/simulate", new SimulationConfigDto(SampleCount: 100))).Code);

        var casual = await PutAsync<DeckSummaryDto>($"/api/decks/{deck.Id}/format", new UpdateFormatRequest(DeckFormats.Casual));
        Assert.Equal(DeckFormats.Casual, casual.Format);
        var checkedAgain = await GetAsync<VersionDetailDto>($"/api/versions/{version.Id}");
        Assert.True(checkedAgain.Validation.IsValid);

        var run = await PostAsync<SimulationRunDto>($"/api/versions/{version.Id}/simulate", new SimulationConfigDto(SampleCount: 100));
        Assert.Equal(DeckFormats.Casual, run.Snapshot.Format);
    }

    [Fact]
    public async Task FormatRequests_AreValidated()
    {
        var deck = await PostAsync<DeckSummaryDto>("/api/decks", new CreateDeckRequest("Formats", "casual"));
        Assert.Equal(DeckFormats.Casual, deck.Format);
        var list = (await GetAsync<List<BanlistSummaryDto>>("/api/banlists"))[0];

        Assert.Equal("deck.unknown_format", (await FailAsync(HttpMethod.Put, $"/api/decks/{deck.Id}/format", new UpdateFormatRequest("Goat"))).Code);
        Assert.Equal("deck.banlist_needs_tcg", (await FailAsync(HttpMethod.Put, $"/api/decks/{deck.Id}/format", new UpdateFormatRequest(DeckFormats.Casual, list.Id))).Code);
        Assert.Equal("deck.unknown_banlist", (await FailAsync(HttpMethod.Put, $"/api/decks/{deck.Id}/format", new UpdateFormatRequest(DeckFormats.TcgAdvanced, Guid.NewGuid()))).Code);

        var pinned = await PutAsync<DeckSummaryDto>($"/api/decks/{deck.Id}/format", new UpdateFormatRequest(DeckFormats.TcgAdvanced, list.Id));
        Assert.Equal(list.Id, pinned.BanlistId);
        Assert.Equal(HttpStatusCode.NotFound, (await FailAsync(HttpMethod.Get, $"/api/banlists/{Guid.NewGuid()}")).Status);
    }

    [Fact]
    public async Task OldRuns_StayViewableWhenTheDeckLaterBecomesIllegal()
    {
        var (_, version) = await CreateLegalDeckAsync();
        var run = await PostAsync<SimulationRunDto>($"/api/versions/{version.Id}/simulate", new SimulationConfigDto(SampleCount: 100));
        var called = await CardAsync("Called by the Grave"); // Forbidden
        await PutAsync<VersionDetailDto>($"/api/versions/{version.Id}/cards", new UpdateCardsRequest(
            [.. version.Cards.Select(c => new CardEntryRequest(c.Card.Id, c.Zone, c.Quantity)), new(called.Id, DeckZone.Main, 1)]));

        var stored = await GetAsync<SimulationRunDto>($"/api/simulations/{run.Id}");

        Assert.True(stored.VersionChangedSinceRun);
        Assert.StartsWith("current TCG list first seen", stored.Snapshot.Banlist, StringComparison.Ordinal);
    }
}

/// <summary>A catalogue whose card file can change between imports, to simulate a new list.</summary>
public sealed class MutableCatalogFactory : DuelLabApiFactory
{
    public string CardFile { get; } = Path.Combine(Path.GetTempPath(), $"duellab-cards-{Guid.NewGuid():N}.json");

    protected override string CardFilePath
    {
        get
        {
            if (!File.Exists(CardFile))
            {
                File.Copy(FixturePath, CardFile);
            }

            return CardFile;
        }
    }

    /// <summary>Changes a card's TCG status in the file (null makes it Unlimited).</summary>
    public void SetStatus(string cardName, string? status)
    {
        var json = JsonNode.Parse(File.ReadAllText(CardFile))!;
        var card = json["data"]!.AsArray().First(c => c!["name"]!.GetValue<string>() == cardName)!;
        card["banlist_info"] = status is null ? null : new JsonObject { ["ban_tcg"] = status };
        File.WriteAllText(CardFile, json.ToJsonString());
    }

    protected override Task CleanupAsync()
    {
        File.Delete(CardFile);
        return Task.CompletedTask;
    }
}

public sealed class BanlistHistoryTests(MutableCatalogFactory factory) : IClassFixture<MutableCatalogFactory>
{
    private readonly ApiCalls _api = new(factory);

    [Fact]
    public async Task ChangedList_AddsASnapshot_AndPinnedDecksKeepTheOldRules()
    {
        // A deck that uses 1 Pot of Greed in the Side Deck: Forbidden on the first list.
        var (deck, version) = await _api.CreateLegalDeckAsync("History");
        var greed = await _api.CardAsync("Pot of Greed");
        await _api.PutAsync<VersionDetailDto>($"/api/versions/{version.Id}/cards", new UpdateCardsRequest(
            [.. version.Cards.Select(c => new CardEntryRequest(c.Card.Id, c.Zone, c.Quantity)), new(greed.Id, DeckZone.Side, 1)]));
        var firstList = (await _api.GetAsync<List<BanlistSummaryDto>>("/api/banlists")).Single();
        await _api.PutAsync<DeckSummaryDto>($"/api/decks/{deck.Id}/format", new UpdateFormatRequest(DeckFormats.TcgAdvanced, firstList.Id));

        // The list changes: Pot of Greed becomes Limited. A forced import records a new snapshot.
        factory.SetStatus("Pot of Greed", "Limited");
        using (var scope = factory.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<CardCatalogService>().SyncAsync(force: true, CancellationToken.None);
            Assert.True(result.BanlistChanged);
        }

        var lists = await _api.GetAsync<List<BanlistSummaryDto>>("/api/banlists");
        Assert.Equal(2, lists.Count);
        Assert.True(lists[0].IsCurrent);
        Assert.Equal((3, 6), (lists[0].Forbidden, lists[0].Limited));

        // Pinned to the first list: still illegal, and the message names that list.
        var pinned = await _api.GetAsync<VersionDetailDto>($"/api/versions/{version.Id}");
        var issue = Assert.Single(pinned.Validation.Issues, i => i.Code == "deck.banlist");
        Assert.Contains("Forbidden", issue.Message, StringComparison.Ordinal);
        Assert.Equal(firstList.Label, pinned.BanlistLabel);

        // Following the current list: 1 copy of a Limited card is legal.
        await _api.PutAsync<DeckSummaryDto>($"/api/decks/{deck.Id}/format", new UpdateFormatRequest(DeckFormats.TcgAdvanced));
        var current = await _api.GetAsync<VersionDetailDto>($"/api/versions/{version.Id}");
        Assert.True(current.Validation.IsValid);
        Assert.StartsWith("current ", current.BanlistLabel, StringComparison.Ordinal);
    }
}

/// <summary>Lets a test class with its own factory reuse the shared helpers.</summary>
public sealed class ApiCalls(DuelLabApiFactory factory) : ApiTestBase(factory)
{
    public new Task<T> GetAsync<T>(string url) => base.GetAsync<T>(url);

    public new Task<T> PutAsync<T>(string url, object? body) => base.PutAsync<T>(url, body);

    public new Task<CardDto> CardAsync(string name) => base.CardAsync(name);

    public new Task<(DeckSummaryDto Deck, VersionDetailDto Version)> CreateLegalDeckAsync(string name) => base.CreateLegalDeckAsync(name);
}
