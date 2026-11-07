using DuelLab.Application.Cards;
using DuelLab.Application.Common;
using DuelLab.Infrastructure.ExternalData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DuelLab.Infrastructure;

/// <summary>Imports the card catalogue in the background on first start, so the app starts at once.</summary>
internal sealed partial class CatalogImportService(
    IServiceScopeFactory scopes,
    IOptions<CardCatalogOptions> options,
    ILogger<CatalogImportService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.ImportOnStartup)
        {
            return;
        }

        // Let the host finish starting (and apply migrations) first.
        await Task.Yield();

        try
        {
            using var scope = scopes.CreateScope();
            var catalog = scope.ServiceProvider.GetRequiredService<CardCatalogService>();
            var status = await catalog.GetStatusAsync(stoppingToken);
            if (status.CardCount == 0)
            {
                await catalog.SyncAsync(force: true, stoppingToken);
            }
            else
            {
                // Installs that imported cards before list history existed get a first snapshot from
                // the cards' current statuses, without calling the provider.
                await scope.ServiceProvider.GetRequiredService<Application.Banlists.BanlistService>().RecordIfChangedAsync(null, stoppingToken);
            }
        }
        catch (Exception ex) when (ex is ExternalServiceException or RequestValidationException)
        {
            LogImportFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Startup card import failed; import manually with POST /api/cards/sync")]
    private static partial void LogImportFailed(ILogger logger, Exception exception);
}
