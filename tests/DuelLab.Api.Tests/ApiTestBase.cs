using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DuelLab.Application.Cards;
using DuelLab.Application.Common;
using DuelLab.Application.Decks;
using DuelLab.Domain.Decks;

namespace DuelLab.Api.Tests;

/// <summary>Shared helpers: typed HTTP calls and a quick way to build a legal 40-card deck.</summary>
public abstract class ApiTestBase(DuelLabApiFactory factory) : IClassFixture<DuelLabApiFactory>
{
    protected DuelLabApiFactory Factory { get; } = factory;

    protected HttpClient Client { get; } = factory.CreateClient();

    protected static JsonSerializerOptions Json => JsonDefaults.Options;

    protected async Task<T> GetAsync<T>(string url)
    {
        var response = await Client.GetAsync(url);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    protected async Task<T> SendAsync<T>(HttpMethod method, string url, object? body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body, options: Json) };
        var response = await Client.SendAsync(request);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    protected Task<T> PostAsync<T>(string url, object? body) => SendAsync<T>(HttpMethod.Post, url, body);

    protected Task<T> PutAsync<T>(string url, object? body) => SendAsync<T>(HttpMethod.Put, url, body);

    /// <summary>Sends a request expected to fail and returns its status and problem <c>code</c>.</summary>
    protected async Task<(HttpStatusCode Status, string? Code, string? Detail)> FailAsync(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body, options: Json) };
        var response = await Client.SendAsync(request);
        Assert.False(response.IsSuccessStatusCode, $"{method} {url} unexpectedly succeeded.");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (
            response.StatusCode,
            problem.TryGetProperty("code", out var code) ? code.GetString() : null,
            problem.TryGetProperty("detail", out var detail) ? detail.GetString() : null);
    }

    protected async Task<CardDto> CardAsync(string name)
    {
        var page = await GetAsync<PagedResult<CardDto>>($"/api/cards?search={Uri.EscapeDataString(name)}");
        return page.Items.First(c => c.Name == name);
    }

    /// <summary>
    /// Creates a deck whose first version holds a legal 42-card Main Deck from unlimited fixture cards,
    /// with Ash Blossom and Effect Veiler as starters and Infinite Impermanence as an extender.
    /// </summary>
    protected async Task<(DeckSummaryDto Deck, VersionDetailDto Version)> CreateLegalDeckAsync(string name = "Test deck")
    {
        var deck = await PostAsync<DeckSummaryDto>("/api/decks", new CreateDeckRequest(name));
        var versionId = deck.Versions[0].Id;

        string[] names =
        [
            "Ash Blossom & Joyous Spring", "Effect Veiler", "Infinite Impermanence", "Dark Magician",
            "Blue-Eyes White Dragon", "Mystical Space Typhoon", "Raigeki", "Polymerization", "Mirror Force",
            "Nibiru, the Primal Being", "Dark Magician Girl", "Elemental HERO Neos",
            "Black Luster Soldier - Envoy of the Beginning", "Swords of Revealing Light",
        ];
        var ids = new List<int>();
        foreach (var n in names)
        {
            ids.Add((await CardAsync(n)).Id);
        }

        await PutAsync<VersionDetailDto>(
            $"/api/versions/{versionId}/cards",
            new UpdateCardsRequest(ids.Select(id => new CardEntryRequest(id, DeckZone.Main, 3)).ToList()));

        var version = await PutAsync<VersionDetailDto>(
            $"/api/versions/{versionId}/roles",
            new UpdateRolesRequest(
            [
                new(ids[0], ["Starter", "HandTrap"]),
                new(ids[1], ["Starter"]),
                new(ids[2], ["Extender"]),
                new(ids[3], ["Brick"]),
            ]));

        return (deck, version);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            Assert.Fail($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }
}
