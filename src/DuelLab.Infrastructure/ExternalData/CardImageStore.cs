using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using DuelLab.Application.Common;
using DuelLab.Domain.Cards;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DuelLab.Infrastructure.ExternalData;

/// <summary>
/// Stores card images on disk. Each image is downloaded from the provider at most once and then served
/// by DuelLab, as YGOPRODeck requires (no hotlinking).
/// </summary>
internal sealed partial class CardImageStore : ICardImageStore, IDisposable
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new();

    private readonly HttpClient _http;
    private readonly ILogger<CardImageStore> _logger;
    private readonly string _root;
    private readonly RateLimiter _limiter;

    public CardImageStore(HttpClient http, IOptions<CardCatalogOptions> options, ILogger<CardImageStore> logger)
    {
        _http = http;
        _logger = logger;
        _root = Path.GetFullPath(options.Value.ImageCachePath);
        _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = Math.Max(1, options.Value.MaxImageDownloadsPerSecond),
            TokensPerPeriod = Math.Max(1, options.Value.MaxImageDownloadsPerSecond),
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = 500,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    public async Task<CardImageFile?> GetAsync(Card card, CardImageSize size, CancellationToken cancellationToken)
    {
        var source = size == CardImageSize.Small ? card.SmallImageUrl : card.ImageUrl;
        var path = Path.Combine(_root, size == CardImageSize.Small ? "small" : "full", $"{card.ExternalId}.jpg");
        if (File.Exists(path))
        {
            return new CardImageFile(path, "image/jpeg");
        }

        if (string.IsNullOrEmpty(source) || !Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var fileLock = FileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(path))
            {
                return new CardImageFile(path, "image/jpeg");
            }

            using var lease = await _limiter.AcquireAsync(1, cancellationToken);
            if (!lease.IsAcquired)
            {
                return null;
            }

            using var response = await _http.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.Ordinal) != true)
            {
                LogDownloadFailed(_logger, card.ExternalId, (int)response.StatusCode);
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            await using (var file = File.Create(temp))
            {
                await response.Content.CopyToAsync(file, cancellationToken);
            }

            File.Move(temp, path, overwrite: true);
            return new CardImageFile(path, "image/jpeg");
        }
        catch (HttpRequestException ex)
        {
            LogDownloadError(_logger, card.ExternalId, ex);
            return null;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public void Dispose() => _limiter.Dispose();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image download for card {ExternalId} failed with status {Status}")]
    private static partial void LogDownloadFailed(ILogger logger, long externalId, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image download for card {ExternalId} failed")]
    private static partial void LogDownloadError(ILogger logger, long externalId, Exception exception);
}
