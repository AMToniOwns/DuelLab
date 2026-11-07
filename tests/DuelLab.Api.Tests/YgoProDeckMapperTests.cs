using DuelLab.Domain.Cards;
using DuelLab.Infrastructure.ExternalData;

namespace DuelLab.Api.Tests;

public sealed class YgoProDeckMapperTests
{
    private static YgoProDeckCard Card(string type, string frame, int? atk = 1000, int? def = 1000) => new()
    {
        Id = 12345678,
        Name = "Test",
        Type = type,
        FrameType = frame,
        Atk = atk,
        Def = def,
        Level = 4,
        LinkVal = 2,
        CardImages = [new() { Id = 12345678, ImageUrl = "https://example.test/full.jpg", ImageUrlSmall = "https://example.test/small.jpg" }],
    };

    [Theory]
    [InlineData("Fusion Monster", "fusion", true)]
    [InlineData("Synchro Tuner Monster", "synchro", true)]
    [InlineData("XYZ Pendulum Effect Monster", "xyz_pendulum", true)]
    [InlineData("Link Monster", "link", true)]
    [InlineData("Ritual Effect Monster", "ritual", false)]
    [InlineData("Pendulum Effect Monster", "effect_pendulum", false)]
    [InlineData("Effect Monster", "effect", false)]
    public void ExtraDeckFrames_AreDetected(string type, string frame, bool extra)
    {
        Assert.Equal(extra, YgoProDeckMapper.TryMap(Card(type, frame))!.Data.IsExtraDeckCard);
    }

    [Theory]
    [InlineData("Skill Card", "skill")]
    [InlineData("Token", "token")]
    public void SkillsAndTokens_AreSkipped(string type, string frame)
    {
        Assert.Null(YgoProDeckMapper.TryMap(Card(type, frame)));
    }

    [Fact]
    public void QuestionMarkStats_BecomeNull()
    {
        var mapped = YgoProDeckMapper.TryMap(Card("Effect Monster", "effect", atk: -1, def: -1))!;

        Assert.Null(mapped.Data.Attack);
        Assert.Null(mapped.Data.Defense);
    }

    [Fact]
    public void SpellsHaveNoMonsterStats()
    {
        var mapped = YgoProDeckMapper.TryMap(Card("Spell Card", "spell"))!;

        Assert.Equal(CardKind.Spell, mapped.Data.Kind);
        Assert.Null(mapped.Data.Attack);
        Assert.Null(mapped.Data.Level);
    }

    [Fact]
    public void LinkMonsters_HaveLinkValueButNoLevelOrDefense()
    {
        var mapped = YgoProDeckMapper.TryMap(Card("Link Monster", "link"))!;

        Assert.Equal(2, mapped.Data.LinkValue);
        Assert.Null(mapped.Data.Level);
        Assert.Null(mapped.Data.Defense);
    }

    [Fact]
    public void MalformedEntries_AreSkipped()
    {
        Assert.Null(YgoProDeckMapper.TryMap(new YgoProDeckCard { Id = 1, Name = "", Type = "Spell Card", FrameType = "spell" }));
        Assert.Null(YgoProDeckMapper.TryMap(new YgoProDeckCard { Id = 0, Name = "X", Type = "Spell Card", FrameType = "spell" }));
        Assert.Null(YgoProDeckMapper.TryMap(new YgoProDeckCard { Id = 1, Name = "X", Type = "Mystery", FrameType = "spell" }));
    }

    [Theory]
    [InlineData("Forbidden", BanStatus.Forbidden)]
    [InlineData("Limited", BanStatus.Limited)]
    [InlineData("Semi-Limited", BanStatus.SemiLimited)]
    [InlineData(null, BanStatus.Unlimited)]
    public void TcgBanStatus_IsMapped(string? status, BanStatus expected)
    {
        var card = Card("Spell Card", "spell");
        card.BanlistInfo = new() { BanTcg = status };

        Assert.Equal(expected, YgoProDeckMapper.TryMap(card)!.Data.TcgBanStatus);
    }
}
