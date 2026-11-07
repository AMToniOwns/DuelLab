using System.Net.Http.Json;
using System.Text.Json;
using DuelLab.Application.Common;

namespace DuelLab.Infrastructure.ExternalData;

/// <summary>Settings for the card catalogue (section "CardCatalog").</summary>
public sealed class CardCatalogOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "CardCatalog";

    /// <summary>"YgoProDeck" (default) or "File".</summary>
    public string Provider { get; set; } = "YgoProDeck";

    /// <summary>The YGOPRODeck API base address.</summary>
    public Uri YgoProDeckBaseAddress { get; set; } = new("https://db.ygoprodeck.com/api/v7/");

    /// <summary>For the File provider: a JSON file in YGOPRODeck's response format.</summary>
    public string? FilePath { get; set; }

    /// <summary>Import the catalogue at startup when it is empty.</summary>
    public bool ImportOnStartup { get; set; } = true;

    /// <summary>Where downloaded card images are stored.</summary>
    public string ImageCachePath { get; set; } = "data/card-images";

    /// <summary>Most image downloads per second (YGOPRODeck allows 20 requests per second in total).</summary>
    public int MaxImageDownloadsPerSecond { get; set; } = 10;
}

/// <summary>
/// Imports cards from the YGOPRODeck API with a single request for all TCG cards.
/// </summary>
/// <remarks>
/// YGOPRODeck's terms (checked 2026-11-07): data should be stored locally to keep calls to a minimum;
/// the rate limit is 20 requests per second, and exceeding it blocks the IP for an hour; images must
/// not be hotlinked. DuelLab imports in bulk, searches its own database, and re-hosts images.
/// </remarks>
internal sealed class YgoProDeckCardProvider(HttpClient http) : ICardDataProvider
{
    public string Name => "YGOPRODeck";

    public async Task<IReadOnlyList<ExternalCard>> FetchAllCardsAsync(CancellationToken cancellationToken)
    {
        // Network errors, timeouts (including the resilience pipeline's), an open circuit breaker
        // and malformed JSON all mean the same thing to the caller: the provider is unavailable.
        try
        {
            var response = await http.GetFromJsonAsync<YgoProDeckResponse>("cardinfo.php?format=tcg", cancellationToken);
            var cards = YgoProDeckMapper.Map(response);
            return cards.Count > 0
                ? cards
                : throw new ExternalServiceException("YGOPRODeck returned no cards.");
        }
        catch (Exception ex) when (ex is not ExternalServiceException && !cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceException("Could not download card data from YGOPRODeck. Try again later.", ex);
        }
    }
}

/// <summary>
/// Reads cards from a local JSON file in YGOPRODeck's format. Used for offline development and
/// end-to-end tests, so they never depend on the network.
/// </summary>
internal sealed class FileCardProvider(string path) : ICardDataProvider
{
    public string Name => $"File ({Path.GetFileName(path)})";

    public async Task<IReadOnlyList<ExternalCard>> FetchAllCardsAsync(CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var response = await JsonSerializer.DeserializeAsync<YgoProDeckResponse>(stream, cancellationToken: cancellationToken);
        return YgoProDeckMapper.Map(response);
    }
}
