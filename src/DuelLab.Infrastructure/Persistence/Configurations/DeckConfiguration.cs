using DuelLab.Domain.Decks;
using DuelLab.Domain.Simulations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DuelLab.Infrastructure.Persistence.Configurations;

internal sealed class DeckConfiguration : IEntityTypeConfiguration<Deck>
{
    public void Configure(EntityTypeBuilder<Deck> builder)
    {
        builder.ToTable("decks");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Name).HasMaxLength(DeckRules.MaxNameLength).IsRequired();
        builder.Property(d => d.Format).HasMaxLength(50).IsRequired();
        builder.HasMany(d => d.Versions).WithOne().HasForeignKey(v => v.DeckId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class DeckVersionConfiguration : IEntityTypeConfiguration<DeckVersion>
{
    public void Configure(EntityTypeBuilder<DeckVersion> builder)
    {
        builder.ToTable("deck_versions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.VersionName).HasMaxLength(DeckRules.MaxNameLength).IsRequired();
        builder.Property(v => v.Notes).HasMaxLength(DeckRules.MaxNotesLength);

        // Lineage only: deleting a parent version must not delete or block its copies.
        builder.HasOne<DeckVersion>().WithMany().HasForeignKey(v => v.ParentVersionId).OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(v => v.Cards).WithOne().HasForeignKey(c => c.DeckVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Cards).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(v => v.Roles).WithOne().HasForeignKey(r => r.DeckVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany<SimulationRun>().WithOne().HasForeignKey(r => r.DeckVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DeckVersionCardConfiguration : IEntityTypeConfiguration<DeckVersionCard>
{
    public void Configure(EntityTypeBuilder<DeckVersionCard> builder)
    {
        builder.ToTable("deck_version_cards", t => t.HasCheckConstraint("ck_deck_version_cards_quantity", "\"Quantity\" BETWEEN 1 AND 3"));
        builder.HasKey(c => new { c.DeckVersionId, c.CardId, c.Zone });
        builder.HasOne<Domain.Cards.Card>().WithMany().HasForeignKey(c => c.CardId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DeckVersionCardRoleConfiguration : IEntityTypeConfiguration<DeckVersionCardRole>
{
    public void Configure(EntityTypeBuilder<DeckVersionCardRole> builder)
    {
        builder.ToTable("deck_version_card_roles");
        builder.Ignore(r => r.Role);
        builder.Property(r => r.CustomName).HasMaxLength(DeckRules.MaxNameLength).IsRequired();

        // CustomName is "" for built-in roles (never NULL), so this key is unique per (card, role).
        builder.HasKey(r => new { r.DeckVersionId, r.CardId, r.Kind, r.CustomName });
        builder.HasOne<Domain.Cards.Card>().WithMany().HasForeignKey(r => r.CardId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SimulationRunConfiguration : IEntityTypeConfiguration<SimulationRun>
{
    public void Configure(EntityTypeBuilder<SimulationRun> builder)
    {
        builder.ToTable("simulation_runs");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.EngineVersion).HasMaxLength(20).IsRequired();
        builder.Property(r => r.DeckSnapshotHash).HasMaxLength(64).IsRequired();
        builder.Property(r => r.ConfigurationJson).HasColumnType("jsonb").IsRequired();

        // Plain text, not jsonb: results are stored exactly as the engine serialised them.
        builder.Property(r => r.ResultsJson).HasColumnType("text").IsRequired();
        builder.HasIndex(r => new { r.DeckVersionId, r.CreatedAt });
        builder.HasIndex(r => r.CreatedAt);
    }
}
