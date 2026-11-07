using DuelLab.Application.Common;
using DuelLab.Domain.Cards;

namespace DuelLab.Infrastructure.ExternalData;

/// <summary>Translates YGOPRODeck responses into DuelLab's provider-neutral <see cref="ExternalCard"/>.</summary>
internal static class YgoProDeckMapper
{
    private static readonly HashSet<string> ExtraDeckFrames = new(StringComparer.OrdinalIgnoreCase)
    {
        "fusion", "synchro", "xyz", "link", "fusion_pendulum", "synchro_pendulum", "xyz_pendulum",
    };

    // Skill Cards (Speed Duel) and Tokens are not deck cards in the Advanced Format.
    private static readonly HashSet<string> SkippedFrames = new(StringComparer.OrdinalIgnoreCase) { "skill", "token" };

    /// <summary>Maps every usable card; skips Skills, Tokens and malformed entries.</summary>
    public static IReadOnlyList<ExternalCard> Map(YgoProDeckResponse? response) =>
        (response?.Data ?? []).Select(TryMap).OfType<ExternalCard>().ToList();

    internal static ExternalCard? TryMap(YgoProDeckCard card)
    {
        if (card.Id <= 0 || string.IsNullOrWhiteSpace(card.Name) || string.IsNullOrWhiteSpace(card.Type)
            || card.FrameType is null || SkippedFrames.Contains(card.FrameType))
        {
            return null;
        }

        var kind = card.Type switch
        {
            "Spell Card" => CardKind.Spell,
            "Trap Card" => CardKind.Trap,
            _ when card.Type.Contains("Monster", StringComparison.OrdinalIgnoreCase) => (CardKind?)CardKind.Monster,
            _ => null,
        };
        if (kind is null)
        {
            return null;
        }

        var isMonster = kind == CardKind.Monster;
        var isLink = string.Equals(card.FrameType, "link", StringComparison.OrdinalIgnoreCase);

        // The first image is the card's own artwork (later entries are alternate arts).
        var image = card.CardImages?.FirstOrDefault(i => i.Id == card.Id) ?? card.CardImages?.FirstOrDefault();

        return new ExternalCard(card.Id, new CardData(
            Name: card.Name.Trim(),
            Description: card.Desc,
            Kind: kind.Value,
            TypeLine: card.HumanReadableCardType ?? card.Type,
            IsExtraDeckCard: ExtraDeckFrames.Contains(card.FrameType),
            Attribute: isMonster ? card.Attribute : null,
            Race: card.Race,
            Archetype: card.Archetype,
            Level: isMonster && !isLink ? card.Level : null,
            Attack: isMonster ? Stat(card.Atk) : null,
            Defense: isMonster && !isLink ? Stat(card.Def) : null,
            LinkValue: isLink ? card.LinkVal : null,
            ImageUrl: image?.ImageUrl,
            SmallImageUrl: image?.ImageUrlSmall,
            TcgBanStatus: card.BanlistInfo?.BanTcg switch
            {
                "Forbidden" => BanStatus.Forbidden,
                "Limited" => BanStatus.Limited,
                "Semi-Limited" => BanStatus.SemiLimited,
                _ => BanStatus.Unlimited,
            }));
    }

    // YGOPRODeck reports "?" ATK/DEF as -1.
    private static int? Stat(int? value) => value is null or < 0 ? null : value;
}
