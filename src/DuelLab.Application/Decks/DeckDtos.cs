using DuelLab.Application.Cards;
using DuelLab.Domain.Decks;

namespace DuelLab.Application.Decks;

/// <summary>A deck with a summary of each version.</summary>
public sealed record DeckSummaryDto(
    Guid Id,
    string Name,
    string Format,
    Guid? BanlistId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<VersionSummaryDto> Versions);

/// <summary>A version without its card list.</summary>
public sealed record VersionSummaryDto(
    Guid Id,
    string VersionName,
    string? Notes,
    Guid? ParentVersionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int MainCount,
    int ExtraCount,
    int SideCount);

/// <summary>A version with full contents, roles and validation.</summary>
public sealed record VersionDetailDto(
    Guid Id,
    Guid DeckId,
    string DeckName,
    string Format,
    Guid? BanlistId,
    string BanlistLabel,
    string VersionName,
    string? Notes,
    Guid? ParentVersionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<VersionCardDto> Cards,
    IReadOnlyList<ComboLineDto> ComboLines,
    ValidationDto Validation);

/// <summary>A combo line: the pieces needed in the opening hand, each from a different card.</summary>
public sealed record ComboLineDto(Guid Id, string Name, string? Notes, IReadOnlyList<ComboPieceDto> Pieces);

/// <summary>
/// One piece of a combo line: <paramref name="Count"/> cards, each being one of <paramref name="CardIds"/>
/// or having one of <paramref name="Roles"/>.
/// </summary>
public sealed record ComboPieceDto(IReadOnlyList<int> CardIds, IReadOnlyList<string> Roles, int Count = 1);

/// <summary>Request to replace a version's combo lines.</summary>
public sealed record UpdateComboLinesRequest(IReadOnlyList<ComboLineRequest> Lines);

/// <summary>One requested combo line.</summary>
public sealed record ComboLineRequest(string Name, string? Notes, IReadOnlyList<ComboPieceDto> Pieces);

/// <summary>One (card, zone) entry with the card's roles in this version.</summary>
public sealed record VersionCardDto(CardDto Card, DeckZone Zone, int Quantity, IReadOnlyList<string> Roles);

/// <summary>Validation outcome.</summary>
public sealed record ValidationDto(bool IsValid, int MainCount, int ExtraCount, int SideCount, IReadOnlyList<DeckValidationIssue> Issues);

/// <summary>Request to create a deck.</summary>
public sealed record CreateDeckRequest(string Name, string? Format = null);

/// <summary>Request to change a deck's format; <paramref name="BanlistId"/> pins TCG Advanced to a list.</summary>
public sealed record UpdateFormatRequest(string Format, Guid? BanlistId = null);

/// <summary>Request to rename a deck.</summary>
public sealed record RenameDeckRequest(string Name);

/// <summary>Request to create a version, empty or as a copy of <paramref name="SourceVersionId"/>.</summary>
public sealed record CreateVersionRequest(string VersionName, Guid? SourceVersionId);

/// <summary>Request to change a version's name and notes.</summary>
public sealed record UpdateVersionRequest(string VersionName, string? Notes);

/// <summary>Request to replace a version's cards.</summary>
public sealed record UpdateCardsRequest(IReadOnlyList<CardEntryRequest> Cards);

/// <summary>One card entry.</summary>
public sealed record CardEntryRequest(int CardId, DeckZone Zone, int Quantity);

/// <summary>Request to replace a version's role assignments.</summary>
public sealed record UpdateRolesRequest(IReadOnlyList<CardRolesRequest> Cards);

/// <summary>The roles of one card, as role keys (e.g. "Starter", "Custom:Fusion line").</summary>
public sealed record CardRolesRequest(int CardId, IReadOnlyList<string> Roles);

/// <summary>Request to create a deck from a .ydk file's text.</summary>
/// <param name="Name">The deck name; usually the file name.</param>
/// <param name="Content">The file's text.</param>
/// <param name="Format">"TCG Advanced" (default) or "Casual".</param>
public sealed record ImportYdkRequest(string Name, string Content, string? Format = null);

/// <summary>Request to add a version to an existing deck from a .ydk file's text.</summary>
public sealed record ImportYdkVersionRequest(string VersionName, string Content);

/// <summary>The result of an import: the deck, the new version, and what needed attention.</summary>
public sealed record DeckImportResultDto(DeckSummaryDto Deck, VersionDetailDto Version, ImportReportDto Report);

/// <summary>What happened to the file's contents.</summary>
/// <param name="Imported">Copies imported per zone.</param>
/// <param name="Unknown">Passcodes not in the card database (e.g. OCG-only or very new cards).</param>
/// <param name="Trimmed">Cards with more than 3 copies, cut to 3.</param>
/// <param name="AlternateArtworks">Copies listed under an alternate-artwork passcode.</param>
/// <param name="Warnings">Lines that were skipped.</param>
public sealed record ImportReportDto(
    ZoneCountsDto Imported,
    IReadOnlyList<UnknownPasscodeDto> Unknown,
    IReadOnlyList<TrimmedCardDto> Trimmed,
    int AlternateArtworks,
    IReadOnlyList<string> Warnings);

/// <summary>A number of cards per zone.</summary>
public sealed record ZoneCountsDto(int Main, int Extra, int Side);

/// <summary>A passcode that matched no card.</summary>
public sealed record UnknownPasscodeDto(long Passcode, DeckZone Zone, int Copies);

/// <summary>A card listed more than 3 times.</summary>
public sealed record TrimmedCardDto(int CardId, string Name, int Listed, int Kept);

/// <summary>A .ydk file to download.</summary>
public sealed record YdkFileDto(string FileName, string Content);
