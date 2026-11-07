using DuelLab.Application.Common;
using DuelLab.Domain.Cards;
using DuelLab.Domain.Decks;
using DuelLab.Domain.Simulations;
using Microsoft.EntityFrameworkCore;

namespace DuelLab.Infrastructure.Persistence;

/// <summary>The EF Core context for DuelLab's PostgreSQL database.</summary>
public sealed class DuelLabDbContext(DbContextOptions<DuelLabDbContext> options) : DbContext(options), IUnitOfWork
{
    /// <summary>The card catalogue.</summary>
    public DbSet<Card> Cards => Set<Card>();

    /// <summary>Decks.</summary>
    public DbSet<Deck> Decks => Set<Deck>();

    /// <summary>Deck versions.</summary>
    public DbSet<DeckVersion> DeckVersions => Set<DeckVersion>();

    /// <summary>Forbidden &amp; Limited list snapshots.</summary>
    public DbSet<Banlist> Banlists => Set<Banlist>();

    /// <summary>Stored simulation runs.</summary>
    public DbSet<SimulationRun> SimulationRuns => Set<SimulationRun>();

    /// <inheritdoc />
    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DuelLabDbContext).Assembly);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Store enums as readable text, not numbers, so the database is self-explanatory and
        // reordering an enum can never corrupt data.
        configurationBuilder.Properties<CardKind>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<BanStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<DeckZone>().HaveConversion<string>().HaveMaxLength(10);
        configurationBuilder.Properties<CardRoleKind>().HaveConversion<string>().HaveMaxLength(20);
    }
}
