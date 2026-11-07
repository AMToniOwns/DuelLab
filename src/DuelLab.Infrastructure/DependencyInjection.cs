using DuelLab.Application.Common;
using DuelLab.Infrastructure.ExternalData;
using DuelLab.Infrastructure.Persistence;
using DuelLab.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DuelLab.Infrastructure;

/// <summary>Registers persistence and external-data services.</summary>
public static class DependencyInjection
{
    /// <summary>The user agent sent to external services.</summary>
    public const string UserAgent = "DuelLab/1.0 (personal deck analysis tool)";

    /// <summary>Adds EF Core (PostgreSQL), repositories, the card provider and the image store.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DuelLab")
            ?? throw new InvalidOperationException("Connection string 'DuelLab' is not configured.");

        // "PostgreSQL" (default: server and Docker) or "Sqlite" (the Windows desktop app).
        var provider = configuration["Database:Provider"] ?? "PostgreSQL";
        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<DuelLabDbContext, SqliteDuelLabDbContext>(options => options.UseSqlite(connectionString));
        }
        else if (string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<DuelLabDbContext>(options => options.UseNpgsql(connectionString));
        }
        else
        {
            throw new InvalidOperationException($"Unknown Database:Provider '{provider}'. Use PostgreSQL or Sqlite.");
        }
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<DuelLabDbContext>());
        services.AddScoped<IDeckRepository, DeckRepository>();
        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<ISimulationRunRepository, SimulationRunRepository>();
        services.AddScoped<IBanlistRepository, BanlistRepository>();

        services.Configure<CardCatalogOptions>(configuration.GetSection(CardCatalogOptions.SectionName));
        var catalog = configuration.GetSection(CardCatalogOptions.SectionName).Get<CardCatalogOptions>() ?? new CardCatalogOptions();

        if (string.Equals(catalog.Provider, "File", StringComparison.OrdinalIgnoreCase))
        {
            var path = catalog.FilePath ?? throw new InvalidOperationException("CardCatalog:FilePath is required for the File provider.");
            services.AddSingleton<ICardDataProvider>(new FileCardProvider(path));
        }
        else
        {
            services.AddHttpClient<ICardDataProvider, YgoProDeckCardProvider>((sp, http) =>
                {
                    http.BaseAddress = sp.GetRequiredService<IOptions<CardCatalogOptions>>().Value.YgoProDeckBaseAddress;
                    http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                })
                .AddStandardResilienceHandler(o =>
                {
                    // One large download: allow time for it, and retry only a little to stay polite.
                    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(180);
                    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
                    o.Retry.MaxRetryAttempts = 2;
                    o.Retry.Delay = TimeSpan.FromSeconds(5);
                });
        }

        services.AddHttpClient<ICardImageStore, CardImageStore>(http =>
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            http.Timeout = TimeSpan.FromSeconds(20);
        });

        services.AddHostedService<CatalogImportService>();
        return services;
    }
}
