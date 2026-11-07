using DuelLab.Domain.Cards;

namespace DuelLab.Domain.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Creates a card with a fixed internal id (normally assigned by the database).</summary>
    public static Card Card(int id, string name, bool extra = false, BanStatus ban = BanStatus.Unlimited)
    {
        var card = new Card(
            externalId: 10_000_000 + id,
            new CardData(name, "text", extra ? CardKind.Monster : CardKind.Spell, extra ? "Link Monster" : "Spell", extra, TcgBanStatus: ban),
            Now);
        typeof(Card).GetProperty(nameof(Domain.Cards.Card.Id))!.SetValue(card, id);
        return card;
    }
}
