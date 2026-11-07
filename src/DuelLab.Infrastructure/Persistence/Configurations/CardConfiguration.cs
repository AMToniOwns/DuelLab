using DuelLab.Domain.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DuelLab.Infrastructure.Persistence.Configurations;

internal sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("cards");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).UseIdentityAlwaysColumn();
        builder.HasIndex(c => c.ExternalId).IsUnique();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.Name);
        builder.Property(c => c.Description).IsRequired();
        builder.Property(c => c.TypeLine).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Attribute).HasMaxLength(20);
        builder.Property(c => c.Race).HasMaxLength(50);
        builder.Property(c => c.Archetype).HasMaxLength(100);
        builder.Property(c => c.ImageUrl).HasMaxLength(500);
        builder.Property(c => c.SmallImageUrl).HasMaxLength(500);
    }
}

internal sealed class BanlistConfiguration : IEntityTypeConfiguration<Banlist>
{
    public void Configure(EntityTypeBuilder<Banlist> builder)
    {
        builder.ToTable("banlists");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.ProviderVersion).HasMaxLength(50);
        builder.Property(b => b.ContentHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(b => b.FirstSeenAt);
        builder.Ignore(b => b.Label);
        builder.HasMany(b => b.Entries).WithOne().HasForeignKey(e => e.BanlistId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(b => b.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class BanlistEntryConfiguration : IEntityTypeConfiguration<BanlistEntry>
{
    public void Configure(EntityTypeBuilder<BanlistEntry> builder)
    {
        builder.ToTable("banlist_entries");
        builder.HasKey(e => new { e.BanlistId, e.CardId });
        builder.HasOne<Card>().WithMany().HasForeignKey(e => e.CardId).OnDelete(DeleteBehavior.Restrict);
    }
}
