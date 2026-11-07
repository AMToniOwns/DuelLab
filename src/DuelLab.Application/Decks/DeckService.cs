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

    /// <summary>Creates a deck with one version from a .ydk file.</summary>
    public async Task<DeckImportResultDto> ImportYdkAsync(ImportYdkRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (entries, report) = await ReadYdkAsync(request.Content, cancellationToken);

        var now = time.GetUtcNow();
        var deck = Deck.Create(request.Name, now);
        if (request.Format is not null)
        {
            deck.SetFormat(request.Format, null, now);
        }

        var version = deck.Versions[0];
        version.ReplaceCards(entries, now);
        decks.Add(deck);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DeckImportResultDto(ToSummary(deck), await ToDetailAsync(deck, version, cancellationToken), report);
    }

    /// <summary>Adds a version to an existing deck from a .ydk file. Other versions are not changed.</summary>
    public async Task<DeckImportResultDto> ImportYdkVersionAsync(Guid deckId, ImportYdkVersionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var deck = await LoadDeckAsync(deckId, cancellationToken);
        var (entries, report) = await ReadYdkAsync(request.Content, cancellationToken);

        var now = time.GetUtcNow();
        var version = deck.AddEmptyVersion(request.VersionName, now);
        version.ReplaceCards(entries, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DeckImportResultDto(ToSummary(deck), await ToDetailAsync(deck, version, cancellationToken), report);
    }

    /// <summary>
    /// Reads a .ydk file and returns its cards and the import report without saving anything, so the
    /// editor can load it into a draft for the user to review.
    /// </summary>
    public async Task<YdkResolutionDto> ResolveYdkAsync(ResolveYdkRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (entries, report) = await ReadYdkAsync(request.Content, cancellationToken);
        var used = await cards.GetByIdsAsync(entries.Select(e => e.CardId), cancellationToken);
        return new YdkResolutionDto(
            entries.Select(e => new ResolvedCardDto(CardDto.From(used[e.CardId]), e.Zone, e.Quantity)).ToList(),
            report);
    }

    /// <summary>A version as a .ydk file, using each card's main passcode.</summary>
    public async Task<YdkFileDto> ExportYdkAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var (deck, version) = await LoadVersionAsync(versionId, cancellationToken);
        var used = await cards.GetByIdsAsync(version.Cards.Select(c => c.CardId), cancellationToken);
        var content = Ydk.Write(version.Cards
            .Where(c => used.ContainsKey(c.CardId))
            .OrderBy(c => used[c.CardId].Kind)
            .ThenBy(c => used[c.CardId].Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => (used[c.CardId].ExternalId, c.Zone, c.Quantity)));

        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var name = new string($"{deck.Name} - {version.VersionName}".Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return new YdkFileDto($"{name}.ydk", content);
    }

    /// <summary>
    /// Turns a .ydk file into card entries: resolves passcodes (including alternate artworks), skips
    /// unknown ones, and cuts any card listed more than 3 times to 3 (keeping Main, then Extra, then
    /// Side copies). Everything else, including banlist and Extra Deck problems, is kept for the
    /// normal deck check to report.
    /// </summary>
    private async Task<(List<DeckCardEntry> Entries, ImportReportDto Report)> ReadYdkAsync(string content, CancellationToken cancellationToken)
    {
        var file = Ydk.Parse(content ?? string.Empty);
        var zones = new[] { DeckZone.Main, DeckZone.Extra, DeckZone.Side };
        var found = await cards.FindByPasscodesAsync(zones.SelectMany(file.In).ToList(), cancellationToken);

        var unknown = zones
            .SelectMany(z => file.In(z).Where(p => !found.ContainsKey(p)).GroupBy(p => p).Select(g => new UnknownPasscodeDto(g.Key, z, g.Count())))
            .ToList();
        var alternate = zones.SelectMany(file.In).Count(p => found.TryGetValue(p, out var c) && c.ExternalId != p);

        var remaining = new Dictionary<int, int>();
        var listed = new Dictionary<int, int>();
        var entries = new List<DeckCardEntry>();
        foreach (var zone in zones)
        {
            foreach (var group in file.In(zone).Where(found.ContainsKey).GroupBy(p => found[p].Id))
            {
                var wanted = group.Count();
                listed[group.Key] = listed.GetValueOrDefault(group.Key) + wanted;
                var left = remaining.TryGetValue(group.Key, out var r) ? r : DeckRules.MaxCopiesPerCard;
                var kept = Math.Min(wanted, left);
                remaining[group.Key] = left - kept;
                if (kept > 0)
                {
                    entries.Add(new DeckCardEntry(group.Key, zone, kept));
                }
            }
        }

        var names = found.Values.DistinctBy(c => c.Id).ToDictionary(c => c.Id, c => c.Name);
        var trimmed = listed
            .Where(kv => kv.Value > DeckRules.MaxCopiesPerCard)
            .Select(kv => new TrimmedCardDto(kv.Key, names[kv.Key], kv.Value, DeckRules.MaxCopiesPerCard))
            .ToList();

        foreach (var zone in zones)
        {
            var count = entries.Where(e => e.Zone == zone).Sum(e => e.Quantity);
            if (count > DeckRules.MaxSize(zone))
            {
                throw new RequestValidationException(
                    "import.zone_too_large",
                    $"The file's {zone} Deck has {count} cards; the maximum is {DeckRules.MaxSize(zone)}.");
            }
        }

        if (entries.Count == 0)
        {
            throw new RequestValidationException("import.no_known_cards", "None of the file's passcodes are in the card database.");
        }

        int Count(DeckZone zone) => entries.Where(e => e.Zone == zone).Sum(e => e.Quantity);
        var imported = new ZoneCountsDto(Count(DeckZone.Main), Count(DeckZone.Extra), Count(DeckZone.Side));
        return (entries, new ImportReportDto(imported, unknown, trimmed, alternate, file.Warnings));
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
