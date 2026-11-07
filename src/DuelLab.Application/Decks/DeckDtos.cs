using DuelLab.Application.Cards;
using DuelLab.Domain.Decks;

namespace DuelLab.Application.Decks;

/// <summary>A deck with a summary of each version.</summary>
public sealed record DeckSummaryDto(
    Guid Id,
    string Name,
    string Format,
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
    string VersionName,
    string? Notes,
    Guid? ParentVersionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<VersionCardDto> Cards,
    ValidationDto Validation);

/// <summary>One (card, zone) entry with the card's roles in this version.</summary>
public sealed record VersionCardDto(CardDto Card, DeckZone Zone, int Quantity, IReadOnlyList<string> Roles);

/// <summary>Validation outcome.</summary>
public sealed record ValidationDto(bool IsValid, int MainCount, int ExtraCount, int SideCount, IReadOnlyList<DeckValidationIssue> Issues);

/// <summary>Request to create a deck.</summary>
public sealed record CreateDeckRequest(string Name);

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
