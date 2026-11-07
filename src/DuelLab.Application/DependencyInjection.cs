using DuelLab.Application.Cards;
using DuelLab.Application.Decks;
using DuelLab.Application.Simulations;
using DuelLab.Simulator.Algorithms;
using DuelLab.Simulator.Analysis;
using Microsoft.Extensions.DependencyInjection;

namespace DuelLab.Application;

/// <summary>Registers application services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds the use-case services and the simulator.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<HandSimulator>();
        services.AddSingleton<SimulationEngine>();
        services.AddSingleton<CatalogSyncGate>();
        services.AddScoped<Banlists.BanlistService>();
        services.AddScoped<CardCatalogService>();
        services.AddScoped<DeckService>();
        services.AddScoped<SimulationService>();
        return services;
    }
}
