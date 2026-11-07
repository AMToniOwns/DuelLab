using DuelLab.Application.Cards;
using DuelLab.Application.Common;
using DuelLab.Domain.Cards;
using DuelLab.Domain.Decks;

namespace DuelLab.Application.Decks;

/// <summary>Use cases for decks and deck versions.</summary>
public sealed class DeckService(
    IDeckRepository decks,
    ICardRepository cards,
    Banlists.BanlistService banlists,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    /// <summary>All decks, most recently updated first.</summary>
    public async Task<IReadOnlyList<DeckSummaryDto>> ListAsync(CancellationToken cancellationToken) =>
        (await decks.ListAsync(cancellationToken)).Select(ToSummary).ToList();

    /// <summary>One deck.</summary>
    public async Task<DeckSummaryDto> GetAsync(Guid deckId, CancellationToken cancellationToken) =>
        ToSummary(await LoadDeckAsync(deckId, cancellationToken));

    /// <summary>Creates a deck with an empty first version.</summary>
    public async Task<DeckSummaryDto> CreateAsync(CreateDeckRequest request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var deck = Deck.Create(request.Name, now);
        if (request.Format is not null)
        {
            deck.SetFormat(request.Format, null, now);
        }

        decks.Add(deck);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToSummary(deck);
    }

    /// <summary>Renames a deck.</summary>
    public async Task<DeckSummaryDto> RenameAsync(Guid deckId, RenameDeckRequest request, CancellationToken cancellationToken)
    {
        var deck = await LoadDeckAsync(deckId, cancellationToken);
        deck.Rename(request.Name, time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToSummary(deck);
    }

    /// <summary>Changes a deck's format, optionally pinning TCG Advanced to a Forbidden &amp; Limited list.</summary>
    public async Task<DeckSummaryDto> UpdateFormatAsync(Guid deckId, UpdateFormatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var deck = await LoadDeckAsync(deckId, cancellationToken);
        if (request.BanlistId is { } id)
        {
            await banlists.EnsureExistsAsync(id, cancellationToken);
        }

        deck.SetFormat(request.Format, request.BanlistId, time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToSummary(deck);
    }

    /// <summary>Deletes a deck, its versions and their simulation history.</summary>
    public async Task DeleteAsync(Guid deckId, CancellationToken cancellationToken)
    {
        decks.Remove(await LoadDeckAsync(deckId, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Creates a version: empty, or a copy of an existing version of the same deck.</summary>
    public async Task<VersionDetailDto> CreateVersionAsync(Guid deckId, CreateVersionRequest request, CancellationToken cancellationToken)
    {
        var deck = await LoadDeckAsync(deckId, cancellationToken);
        var now = time.GetUtcNow();
        var version = request.SourceVersionId is { } sourceId
            ? deck.DuplicateVersion(sourceId, request.VersionName, now)
            : deck.AddEmptyVersion(request.VersionName, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(deck, version, cancellationToken);
    }

    /// <summary>A version with full contents and validation.</summary>
    public async Task<VersionDetailDto> GetVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var (deck, version) = await LoadVersionAsync(versionId, cancellationToken);
        return await ToDetailAsync(deck, version, cancellationToken);
    }

    /// <summary>Changes a version's name and notes.</summary>
    public async Task<VersionDetailDto> UpdateVersionAsync(Guid versionId, UpdateVersionRequest request, CancellationToken cancellationToken)
    {
        var (deck, version) = await LoadVersionAsync(versionId, cancellationToken);
        var now = time.GetUtcNow();
        version.UpdateDetails(request.VersionName, request.Notes, now);
        deck.Touch(now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(deck, version, cancellationToken);
    }

    /// <summary>Deletes a version (a deck keeps at least one).</summary>
    public async Task DeleteVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var (deck, version) = await LoadVersionAsync(versionId, cancellationToken);
        deck.RemoveVersion(version.Id, time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Replaces a version's card list. Unknown card ids are rejected.</summary>
    public async Task<VersionDetailDto> ReplaceCardsAsync(Guid versionId, UpdateCardsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request?.Cards);
        var (deck, version) = await LoadVersionAsync(versionId, cancellationToken);

        var ids = request.Cards.Select(c => c.CardId).Distinct().ToList();
        var known = await cards.GetByIdsAsync(ids, cancellationToken);
        var unknown = ids.FirstOrDefault(id => !known.ContainsKey(id));
        if (unknown != 0 || ids.Contains(0))
        {
            throw new RequestValidationException("deck.unknown_card", $"Card {unknown} is not in the catalogue.");
        }

        var now = time.GetUtcNow();
        version.ReplaceCards(request.Cards.Select(c => new DeckCardEntry(c.CardId, c.Zone, c.Quantity)), now);
        deck.Touch(now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(deck, version, cancellationToken);
    }

    /// <summary>Replaces a version's role assignments.</summary>
    public async Task<VersionDetailDto> ReplaceRolesAsync(Guid versionId, UpdateRolesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request?.Cards);
        var (deck, version) = await LoadVersionAsync(versionId, cancellationToken);

        var assignments = request.Cards
            .SelectMany(c => (c.Roles ?? []).Select(key => (c.CardId, CardRole.Parse(key))))
            .ToList();

        var now = time.GetUtcNow();
        version.ReplaceRoles(assignments, now);
        deck.Touch(now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(deck, version, cancellationToken);
    }

    /// <summary>Replaces a version's combo lines.</summary>
    public async Task<VersionDetailDto> ReplaceComboLinesAsync(Guid versionId, UpdateComboLinesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request?.Lines);
        var (deck, version) = await LoadVersionAsync(versionId, cancellationToken);

        var now = time.GetUtcNow();
        version.ReplaceComboLines(
            request.Lines.Select(l => new ComboLineDraft(
                l.Name,
                l.Notes,
                (l.Pieces ?? []).Select(p => new ComboPiece(p?.CardIds ?? [], p?.Roles ?? [], p?.Count ?? 1)).ToList())),
            now);
        deck.Touch(now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(deck, version, cancellationToken);
    }

    /// <summary>Loads a version and its deck, or throws <see cref="NotFoundException"/>.</summary>
    internal async Task<(Deck Deck, DeckVersion Version)> LoadVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var deck = await decks.GetByVersionIdAsync(versionId, cancellationToken) ?? throw new NotFoundException("Deck version", versionId);
        return (deck, deck.Versions.Single(v => v.Id == versionId));
    }

    /// <summary>Validates a version against the catalogue and the deck's Forbidden &amp; Limited rules.</summary>
    internal async Task<(DeckValidationResult Result, Dictionary<int, Card> Cards, BanlistRules Rules)> ValidateAsync(
        Deck deck,
        DeckVersion version,
        CancellationToken cancellationToken)
    {
        var used = await cards.GetByIdsAsync(version.Cards.Select(c => c.CardId).Distinct(), cancellationToken);
        var (rules, _) = await banlists.RulesForAsync(deck, used, cancellationToken);
        return (DeckValidator.Validate(version, used, rules), used, rules);
    }

    private async Task<Deck> LoadDeckAsync(Guid deckId, CancellationToken cancellationToken) =>
        await decks.GetAsync(deckId, cancellationToken) ?? throw new NotFoundException("Deck", deckId);

    private async Task<VersionDetailDto> ToDetailAsync(Deck deck, DeckVersion version, CancellationToken cancellationToken)
    {
        var (validation, used, rules) = await ValidateAsync(deck, version, cancellationToken);
        var entries = version.Cards
            .Where(c => used.ContainsKey(c.CardId))
            .OrderBy(c => c.Zone)
            .ThenBy(c => used[c.CardId].Kind)
            .ThenBy(c => used[c.CardId].Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new VersionCardDto(
                CardDto.From(used[c.CardId]),
                c.Zone,
                c.Quantity,
                version.RolesOf(c.CardId).Select(r => r.Key).Order(StringComparer.Ordinal).ToList()))
            .ToList();

        return new VersionDetailDto(
            version.Id,
            deck.Id,
            deck.Name,
            deck.Format,
            deck.BanlistId,
            rules.Label,
            version.VersionName,
            version.Notes,
            version.ParentVersionId,
            version.CreatedAt,
            version.UpdatedAt,
            entries,
            version.ComboLines.OrderBy(l => l.Position).Select(ToDto).ToList(),
            new ValidationDto(
                validation.IsValid,
                version.CountIn(DeckZone.Main),
                version.CountIn(DeckZone.Extra),
                version.CountIn(DeckZone.Side),
                validation.Issues));
    }

    internal static ComboLineDto ToDto(DeckVersionComboLine line) => new(
        line.Id,
        line.Name,
        line.Notes,
        line.Pieces.Select(p => new ComboPieceDto(p.CardIds, p.Roles, p.Count)).ToList());

    private static DeckSummaryDto ToSummary(Deck deck) => new(
        deck.Id,
        deck.Name,
        deck.Format,
        deck.BanlistId,
        deck.CreatedAt,
        deck.UpdatedAt,
        deck.Versions
            .OrderBy(v => v.CreatedAt)
            .Select(v => new VersionSummaryDto(
                v.Id,
                v.VersionName,
                v.Notes,
                v.ParentVersionId,
                v.CreatedAt,
                v.UpdatedAt,
                v.CountIn(DeckZone.Main),
                v.CountIn(DeckZone.Extra),
                v.CountIn(DeckZone.Side)))
            .ToList());
}
