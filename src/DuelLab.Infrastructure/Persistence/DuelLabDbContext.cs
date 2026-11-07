using DuelLab.Application.Common;
using DuelLab.Domain.Cards;
using DuelLab.Domain.Decks;
using DuelLab.Domain.Simulations;
using Microsoft.EntityFrameworkCore;

namespace DuelLab.Infrastructure.Persistence;

/// <summary>
/// The EF Core context. It runs on PostgreSQL (server and Docker) or SQLite (the Windows desktop app);
/// the few provider-specific mappings are applied in <see cref="OnModelCreating"/>. SQLite uses
/// <see cref="SqliteDuelLabDbContext"/>, which has its own migrations.
/// </summary>
public class DuelLabDbContext : DbContext, IUnitOfWork
{
    /// <summary>Creates the context (PostgreSQL).</summary>
    public DuelLabDbContext(DbContextOptions<DuelLabDbContext> options)
        : base(options)
    {
    }

    /// <summary>Creates a derived context with its own options type.</summary>
    protected DuelLabDbContext(DbContextOptions options)
        : base(options)
    {
    }

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

        if (Database.IsNpgsql())
        {
            // PostgreSQL-only column types and indexes. Everything else is shared.
            modelBuilder.Entity<Card>().Property(c => c.Id).UseIdentityAlwaysColumn();
            modelBuilder.Entity<Card>().Property(c => c.AlternateExternalIds).HasColumnType("bigint[]");
            modelBuilder.Entity<Card>().HasIndex(c => c.AlternateExternalIds).HasMethod("gin");
            modelBuilder.Entity<DeckVersionComboLine>().Property(l => l.Pieces).HasColumnType("jsonb");
            modelBuilder.Entity<SimulationRun>().Property(r => r.ConfigurationJson).HasColumnType("jsonb");
        }
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

        if (Database.IsSqlite())
        {
            // SQLite cannot compare or sort DateTimeOffset values; store them as sortable 64-bit numbers.
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<Microsoft.EntityFrameworkCore.Storage.ValueConversion.DateTimeOffsetToBinaryConverter>();
        }
    }
}

/// <summary>The SQLite variant used by the Windows desktop app, with its own migrations.</summary>
public sealed class SqliteDuelLabDbContext(DbContextOptions<SqliteDuelLabDbContext> options) : DuelLabDbContext(options);

/// <summary>
/// Lets the EF Core tools create the PostgreSQL context for its migrations. Generating migrations
/// needs only the provider, not a reachable server.
/// </summary>
internal sealed class PostgresDesignTimeFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<DuelLabDbContext>
{
    public DuelLabDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<DuelLabDbContext>().UseNpgsql("Host=localhost;Database=duellab_design").Options);
}

/// <summary>Lets the EF Core tools create the SQLite context to generate its migrations.</summary>
internal sealed class SqliteDesignTimeFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<SqliteDuelLabDbContext>
{
    public SqliteDuelLabDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteDuelLabDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
