using DuelLab.Domain.Cards;

namespace DuelLab.Application.Cards;

/// <summary>Card details returned by the API.</summary>
public sealed record CardDto(
    int Id,
    long ExternalId,
    string Name,
    string Description,
    CardKind Kind,
    string TypeLine,
    bool IsExtraDeckCard,
    string? Attribute,
    string? Race,
    string? Archetype,
    int? Level,
    int? Attack,
    int? Defense,
    int? LinkValue,
    BanStatus TcgBanStatus,
    DateTimeOffset LastSyncedAt,
    string ImageUrl,
    string SmallImageUrl)
{
    /// <summary>Maps a domain card. Image URLs point at DuelLab's own image endpoint, never the provider.</summary>
    public static CardDto From(Card card) => new(
        card.Id,
        card.ExternalId,
        card.Name,
        card.Description,
        card.Kind,
        card.TypeLine,
        card.IsExtraDeckCard,
        card.Attribute,
        card.Race,
        card.Archetype,
        card.Level,
        card.Attack,
        card.Defense,
        card.LinkValue,
        card.TcgBanStatus,
        card.LastSyncedAt,
        $"/api/cards/{card.Id}/image?size=full",
        $"/api/cards/{card.Id}/image?size=small");
}

/// <summary>A page of search results.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>The state of the local catalogue.</summary>
public sealed record CatalogStatusDto(int CardCount, DateTimeOffset? LastSyncedAt, string Provider);

/// <summary>The outcome of an import from the provider.</summary>
public sealed record CatalogSyncResultDto(int Added, int Updated, int Total, DateTimeOffset SyncedAt, bool BanlistChanged);
