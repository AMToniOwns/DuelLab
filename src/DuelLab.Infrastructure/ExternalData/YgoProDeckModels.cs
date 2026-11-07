using System.Text.Json.Serialization;

namespace DuelLab.Infrastructure.ExternalData;

// Response models for the YGOPRODeck v7 API. They are internal to this adapter: nothing outside
// Infrastructure sees them (see YgoProDeckMapper).
#pragma warning disable CA1812 // Instantiated by System.Text.Json.

internal sealed class YgoProDeckResponse
{
    [JsonPropertyName("data")]
    public List<YgoProDeckCard>? Data { get; set; }
}

internal sealed class YgoProDeckCard
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("humanReadableCardType")]
    public string? HumanReadableCardType { get; set; }

    [JsonPropertyName("frameType")]
    public string? FrameType { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }

    [JsonPropertyName("race")]
    public string? Race { get; set; }

    [JsonPropertyName("attribute")]
    public string? Attribute { get; set; }

    [JsonPropertyName("archetype")]
    public string? Archetype { get; set; }

    [JsonPropertyName("level")]
    public int? Level { get; set; }

    [JsonPropertyName("atk")]
    public int? Atk { get; set; }

    [JsonPropertyName("def")]
    public int? Def { get; set; }

    [JsonPropertyName("linkval")]
    public int? LinkVal { get; set; }

    [JsonPropertyName("card_images")]
    public List<YgoProDeckImage>? CardImages { get; set; }

    [JsonPropertyName("banlist_info")]
    public YgoProDeckBanlist? BanlistInfo { get; set; }
}

internal sealed class YgoProDeckImage
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("image_url_small")]
    public string? ImageUrlSmall { get; set; }
}

internal sealed class YgoProDeckDbVersion
{
    [JsonPropertyName("database_version")]
    public string? DatabaseVersion { get; set; }

    [JsonPropertyName("last_update")]
    public string? LastUpdate { get; set; }
}

internal sealed class YgoProDeckBanlist
{
    [JsonPropertyName("ban_tcg")]
    public string? BanTcg { get; set; }
}
