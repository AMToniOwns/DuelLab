using System.Net;
using DuelLab.Application.Cards;
using DuelLab.Domain.Cards;

namespace DuelLab.Api.Tests;

public sealed class CardsApiTests(DuelLabApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Import_SkipsSkillCardsAndReportsStatus()
    {
        var status = await GetAsync<CatalogStatusDto>("/api/cards/status");

        Assert.Equal(27, status.CardCount); // 28 fixture entries minus one Skill Card
        Assert.NotNull(status.LastSyncedAt);
        Assert.StartsWith("File", status.Provider, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_IsCaseInsensitiveAndRanksPrefixMatchesFirst()
    {
        var page = await GetAsync<PagedResult<CardDto>>("/api/cards?search=dark%20magician");

        Assert.Equal(["Dark Magician", "Dark Magician Girl"], page.Items.Take(2).Select(c => c.Name));
        Assert.Contains(page.Items, c => c.Name == "Dark Paladin" || c.Name.Contains("Dark Magician", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Search_TreatsWildcardsLiterally()
    {
        var page = await GetAsync<PagedResult<CardDto>>("/api/cards?search=%25");

        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Search_FiltersByKindAndPages()
    {
        var traps = await GetAsync<PagedResult<CardDto>>("/api/cards?kind=Trap&pageSize=2&page=1");
        var page2 = await GetAsync<PagedResult<CardDto>>("/api/cards?kind=Trap&pageSize=2&page=2");

        Assert.Equal(4, traps.Total);
        Assert.All(traps.Items.Concat(page2.Items), c => Assert.Equal(CardKind.Trap, c.Kind));
        Assert.Empty(traps.Items.Select(c => c.Id).Intersect(page2.Items.Select(c => c.Id)));
    }

    [Fact]
    public async Task Card_MapsProviderFieldsAndNeverExposesProviderImageUrls()
    {
        var link = await CardAsync("Accesscode Talker");
        var ash = await CardAsync("Ash Blossom & Joyous Spring");
        var called = await CardAsync("Called by the Grave");

        Assert.True(link.IsExtraDeckCard);
        Assert.Null(link.Defense);
        Assert.Null(link.Level);
        Assert.Equal(4, link.LinkValue);
        Assert.Equal(2300, link.Attack);

        Assert.False(ash.IsExtraDeckCard);
        Assert.Equal(CardKind.Monster, ash.Kind);
        Assert.Equal(3, ash.Level);
        Assert.Equal("FIRE", ash.Attribute);
        Assert.Equal($"/api/cards/{ash.Id}/image?size=small", ash.SmallImageUrl);

        Assert.Equal(BanStatus.Forbidden, called.TcgBanStatus);
    }

    [Fact]
    public async Task Image_WithoutSourceReturnsNotFoundInsteadOfFailing()
    {
        var ash = await CardAsync("Ash Blossom & Joyous Spring");

        var response = await Client.GetAsync($"/api/cards/{ash.Id}/image");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnknownCard_IsNotFoundProblem()
    {
        var (status, code, _) = await FailAsync(HttpMethod.Get, "/api/cards/999999");

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("not_found", code);
    }

    [Theory]
    [InlineData("/api/cards?pageSize=0", "cards.page_size")]
    [InlineData("/api/cards?pageSize=101", "cards.page_size")]
    [InlineData("/api/cards?page=0", "cards.page")]
    public async Task InvalidPaging_IsRejected(string url, string expectedCode)
    {
        var (status, code, _) = await FailAsync(HttpMethod.Get, url);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(expectedCode, code);
    }

    [Fact]
    public async Task Sync_IsLimitedToOncePerHour()
    {
        var (status, code, _) = await FailAsync(HttpMethod.Post, "/api/cards/sync");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("cards.sync_too_soon", code);
    }
}

public sealed class RoutingTests(DuelLabApiFactory factory) : ApiTestBase(factory)
{
    [Theory]
    [InlineData("/api/nope")]
    [InlineData("/api/decks/not-a-guid")]
    public async Task UnknownApiRoutes_AreNotFoundProblems(string url)
    {
        var (status, code, _) = await FailAsync(HttpMethod.Get, url);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("not_found", code);
    }
}

public sealed class CompressionTests(DuelLabApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task JsonResponses_AreCompressedWhenTheClientAccepts()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/cards?pageSize=50");
        request.Headers.AcceptEncoding.ParseAdd("br");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("br", response.Content.Headers.ContentEncoding);
    }
}
