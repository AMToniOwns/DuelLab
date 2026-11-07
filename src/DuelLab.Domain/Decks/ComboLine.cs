using DuelLab.Domain.Common;

namespace DuelLab.Domain.Decks;

/// <summary>
/// One piece a combo line needs in the opening hand: <see cref="Count"/> cards, each of which is any of
/// <see cref="CardIds"/> or has any of <see cref="Roles"/>. For example "Snake-Eye Ash or a card that
/// searches it".
/// </summary>
public sealed record ComboPiece(IReadOnlyList<int> CardIds, IReadOnlyList<string> Roles, int Count = 1);

/// <summary>
/// A named combo line for a deck version: the pieces the player needs in hand to start it. A hand
/// "has the line" when it holds every piece, each from a different card. DuelLab checks the cards in the
/// hand; it does not play the combo out or check game rules.
/// </summary>
public sealed class DeckVersionComboLine
{
    /// <summary>Most combo lines per version.</summary>
    public const int MaxPerVersion = 12;

    /// <summary>Most pieces per line.</summary>
    public const int MaxPieces = 10;

    /// <summary>Most alternatives (cards plus roles) per piece.</summary>
    public const int MaxOptionsPerPiece = 20;

    /// <summary>Most copies one piece may require.</summary>
    public const int MaxPieceCount = 3;

    private DeckVersionComboLine()
    {
        Name = string.Empty;
        Pieces = [];
    }

    internal DeckVersionComboLine(Guid deckVersionId, int position, string name, string? notes, IReadOnlyList<ComboPiece> pieces, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7(now);
        DeckVersionId = deckVersionId;
        Position = position;
        Name = name;
        Notes = notes;
        Pieces = pieces;
    }

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The owning version.</summary>
    public Guid DeckVersionId { get; private set; }

    /// <summary>Display order within the version.</summary>
    public int Position { get; private set; }

    /// <summary>The line's name, e.g. "Ash into Oak".</summary>
    public string Name { get; private set; }

    /// <summary>Optional notes, e.g. what the line ends on.</summary>
    public string? Notes { get; private set; }

    /// <summary>The pieces, all of which are needed.</summary>
    public IReadOnlyList<ComboPiece> Pieces { get; private set; }

    internal DeckVersionComboLine CopyTo(Guid versionId, DateTimeOffset now) => new(versionId, Position, Name, Notes, Pieces, now);

    /// <summary>Checks and normalises a requested line. Card ids must be in <paramref name="cardsInVersion"/>.</summary>
    internal static (string Name, string? Notes, IReadOnlyList<ComboPiece> Pieces) Validate(ComboLineDraft draft, IReadOnlySet<int> cardsInVersion)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var name = Deck.ValidateName(draft.Name, "combo.name");
        var notes = string.IsNullOrWhiteSpace(draft.Notes) ? null : draft.Notes.Trim();
        if (notes?.Length > DeckRules.MaxNotesLength)
        {
            throw new DomainException("combo.notes_too_long", $"Notes can be at most {DeckRules.MaxNotesLength} characters.");
        }

        var pieces = draft.Pieces ?? [];
        if (pieces.Count == 0 || pieces.Count > MaxPieces)
        {
            throw new DomainException("combo.piece_count", $"Combo line '{name}' needs between 1 and {MaxPieces} pieces.");
        }

        var normalised = new List<ComboPiece>();
        foreach (var piece in pieces)
        {
            var cardIds = (piece?.CardIds ?? []).Distinct().ToList();
            var roles = (piece?.Roles ?? []).Select(r => CardRole.Parse(r).Key).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (cardIds.Count + roles.Count == 0)
            {
                throw new DomainException("combo.empty_piece", $"Every piece of '{name}' needs at least one card or role.");
            }

            if (cardIds.Count + roles.Count > MaxOptionsPerPiece)
            {
                throw new DomainException("combo.too_many_options", $"A piece of '{name}' can have at most {MaxOptionsPerPiece} options.");
            }

            var missing = cardIds.FirstOrDefault(id => !cardsInVersion.Contains(id));
            if (missing != 0 || cardIds.Contains(0))
            {
                throw new DomainException("combo.card_not_in_version", $"Combo line '{name}' uses card {missing}, which is not in this version.");
            }

            if (piece!.Count < 1 || piece.Count > MaxPieceCount)
            {
                throw new DomainException("combo.piece_quantity", $"A piece of '{name}' must need between 1 and {MaxPieceCount} cards.");
            }

            normalised.Add(new ComboPiece(cardIds, roles, piece.Count));
        }

        return (name, notes, normalised);
    }
}

/// <summary>A requested combo line, before validation.</summary>
public sealed record ComboLineDraft(string Name, string? Notes, IReadOnlyList<ComboPiece> Pieces);
