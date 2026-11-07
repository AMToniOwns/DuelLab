using System.Net;
using DuelLab.Application.Decks;
using DuelLab.Domain.Decks;
using Microsoft.EntityFrameworkCore;

namespace DuelLab.Api.Tests;

public sealed class DecksApiTests(DuelLabApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task CreateRenameListAndDelete()
    {
        var created = await PostAsync<DeckSummaryDto>("/api/decks", new CreateDeckRequest("  Blue-Eyes  "));
        Assert.Equal("Blue-Eyes", created.Name);
        Assert.Single(created.Versions);

        var renamed = await PutAsync<DeckSummaryDto>($"/api/decks/{created.Id}", new RenameDeckRequest("Blue-Eyes 2026"));
        Assert.Equal("Blue-Eyes 2026", renamed.Name);

        var all = await GetAsync<List<DeckSummaryDto>>("/api/decks");
        Assert.Contains(all, d => d.Id == created.Id);

        var delete = await Client.DeleteAsync($"/api/decks/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await FailAsync(HttpMethod.Get, $"/api/decks/{created.Id}")).Status);
    }

    [Fact]
    public async Task BlankName_IsRejectedWithDomainCode()
    {
        var (status, code, _) = await FailAsync(HttpMethod.Post, "/api/decks", new CreateDeckRequest(" "));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("deck.name_required", code);
    }

    [Fact]
    public async Task SavedContents_RoundTripWithRolesAndValidation()
    {
        var (_, version) = await CreateLegalDeckAsync();

        var loaded = await GetAsync<VersionDetailDto>($"/api/versions/{version.Id}");

        Assert.Equal(42, loaded.Validation.MainCount);
        Assert.True(loaded.Validation.IsValid);
        var ash = loaded.Cards.Single(c => c.Card.Name == "Ash Blossom & Joyous Spring");
        Assert.Equal(["HandTrap", "Starter"], ash.Roles);
    }

    [Fact]
    public async Task IncompleteDeck_SavesButIsInvalid()
    {
        var deck = await PostAsync<DeckSummaryDto>("/api/decks", new CreateDeckRequest("WIP"));
        var raigeki = await CardAsync("Raigeki");

        var version = await PutAsync<VersionDetailDto>(
            $"/api/versions/{deck.Versions[0].Id}/cards",
            new UpdateCardsRequest([new(raigeki.Id, DeckZone.Main, 1)]));

        Assert.False(version.Validation.IsValid);
        Assert.Contains(version.Validation.Issues, i => i.Code == "deck.main_too_small");
    }

    [Fact]
    public async Task ExtraDeckPlacementAndBanlist_AreReported()
    {
        var (_, version) = await CreateLegalDeckAsync();
        var link = await CardAsync("Accesscode Talker");
        var called = await CardAsync("Called by the Grave");
        var entries = version.Cards.Select(c => new CardEntryRequest(c.Card.Id, c.Zone, c.Quantity)).ToList();

        var updated = await PutAsync<VersionDetailDto>(
            $"/api/versions/{version.Id}/cards",
            new UpdateCardsRequest([.. entries, new(link.Id, DeckZone.Main, 1), new(called.Id, DeckZone.Side, 1)]));

        Assert.Contains(updated.Validation.Issues, i => i.Code == "deck.extra_card_in_main" && i.Severity == ValidationSeverity.Error);
        Assert.Contains(updated.Validation.Issues, i => i.Code == "deck.banlist" && i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public async Task CopyLimit_IsEnforced()
    {
        var deck = await PostAsync<DeckSummaryDto>("/api/decks", new CreateDeckRequest("Copies"));
        var ash = await CardAsync("Ash Blossom & Joyous Spring");

        var (status, code, _) = await FailAsync(
            HttpMethod.Put,
            $"/api/versions/{deck.Versions[0].Id}/cards",
            new UpdateCardsRequest([new(ash.Id, DeckZone.Main, 3), new(ash.Id, DeckZone.Side, 1)]));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("deck.copy_limit", code);
    }

    [Fact]
    public async Task UnknownCardAndRole_AreRejected()
    {
        var (_, version) = await CreateLegalDeckAsync();
        var ash = version.Cards[0].Card;

        var unknownCard = await FailAsync(HttpMethod.Put, $"/api/versions/{version.Id}/cards", new UpdateCardsRequest([new(987654, DeckZone.Main, 1)]));
        var unknownRole = await FailAsync(HttpMethod.Put, $"/api/versions/{version.Id}/roles", new UpdateRolesRequest([new(ash.Id, ["Wizard"])]));

        Assert.Equal("deck.unknown_card", unknownCard.Code);
        Assert.Equal("role.unknown", unknownRole.Code);
    }

    [Fact]
    public async Task DuplicateVersion_CopiesContentsAndKeepsSourceUnchanged()
    {
        var (deck, v1) = await CreateLegalDeckAsync();

        var v2 = await PostAsync<VersionDetailDto>($"/api/decks/{deck.Id}/versions", new CreateVersionRequest("v2", v1.Id));
        await PutAsync<VersionDetailDto>(
            $"/api/versions/{v2.Id}/cards",
            new UpdateCardsRequest(v2.Cards.Skip(1).Select(c => new CardEntryRequest(c.Card.Id, c.Zone, c.Quantity)).ToList()));

        var source = await GetAsync<VersionDetailDto>($"/api/versions/{v1.Id}");
        Assert.Equal(v1.Id, v2.ParentVersionId);
        Assert.Equal(42, source.Validation.MainCount);
        Assert.Equal(v1.Cards.Select(c => (c.Card.Id, c.Quantity, string.Join(',', c.Roles))), source.Cards.Select(c => (c.Card.Id, c.Quantity, string.Join(',', c.Roles))));
    }

    [Fact]
    public async Task DeletingDeck_RemovesVersionsCardsRolesAndRuns()
    {
        var (deck, version) = await CreateLegalDeckAsync();
        await PostAsync<object>($"/api/versions/{version.Id}/simulate", new { sampleCount = 100 });

        await Client.DeleteAsync($"/api/decks/{deck.Id}");

        await Factory.WithDbAsync(async db =>
        {
            Assert.False(await db.DeckVersions.AnyAsync(v => v.DeckId == deck.Id));
            Assert.False(await db.SimulationRuns.AnyAsync(r => r.DeckId == deck.Id));
        });
    }

    [Fact]
    public async Task LastVersion_CannotBeDeleted()
    {
        var deck = await PostAsync<DeckSummaryDto>("/api/decks", new CreateDeckRequest("One version"));

        var (status, code, _) = await FailAsync(HttpMethod.Delete, $"/api/versions/{deck.Versions[0].Id}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("deck.last_version", code);
    }
}
