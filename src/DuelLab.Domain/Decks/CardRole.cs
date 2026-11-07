using DuelLab.Domain.Common;

namespace DuelLab.Domain.Decks;

/// <summary>The built-in analytical roles a card can have in a deck version.</summary>
public enum CardRoleKind
{
    /// <summary>A card that starts the deck's main play on its own.</summary>
    Starter,

    /// <summary>A card that extends a play once it has started.</summary>
    Extender,

    /// <summary>A card that adds another card from the deck to the hand.</summary>
    Searcher,

    /// <summary>A card used from the hand during the opponent's turn.</summary>
    HandTrap,

    /// <summary>A card that answers an established board.</summary>
    BoardBreaker,

    /// <summary>A card the player would rather not open.</summary>
    Brick,

    /// <summary>A user-defined role; the name is in <see cref="CardRole.CustomName"/>.</summary>
    Custom,
}

/// <summary>
/// A user-assigned analytical label, such as Starter or a custom "Combo piece A".
/// Roles are the player's own classification, not game rules.
/// </summary>
public sealed record CardRole
{
    /// <summary>Creates a role. <paramref name="customName"/> is required for, and only allowed with, Custom.</summary>
    public CardRole(CardRoleKind kind, string? customName = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new DomainException("role.unknown", $"Unknown card role '{kind}'.");
        }

        var trimmed = customName?.Trim() ?? string.Empty;
        if (kind == CardRoleKind.Custom)
        {
            if (trimmed.Length == 0)
            {
                throw new DomainException("role.custom_name_required", "A custom role needs a name.");
            }

            if (trimmed.Length > DeckRules.MaxNameLength)
            {
                throw new DomainException(
                    "role.custom_name_too_long",
                    $"A custom role name can be at most {DeckRules.MaxNameLength} characters.");
            }
        }
        else if (trimmed.Length > 0)
        {
            throw new DomainException("role.unexpected_custom_name", "Only Custom roles can have a name.");
        }

        Kind = kind;
        CustomName = trimmed;
    }

    /// <summary>The role type.</summary>
    public CardRoleKind Kind { get; }

    /// <summary>The name of a Custom role; an empty string for built-in roles.</summary>
    public string CustomName { get; }

    /// <summary>
    /// A stable text key that identifies the role, e.g. <c>Starter</c> or <c>Custom:Combo piece A</c>.
    /// Custom names are compared case-insensitively.
    /// </summary>
    public string Key => Kind == CardRoleKind.Custom ? $"Custom:{CustomName}" : Kind.ToString();

    /// <summary>A display label, e.g. "Hand trap" or "Combo piece A".</summary>
    public string Label => Kind switch
    {
        CardRoleKind.HandTrap => "Hand trap",
        CardRoleKind.BoardBreaker => "Board breaker",
        CardRoleKind.Custom => CustomName,
        _ => Kind.ToString(),
    };

    /// <summary>Equality ignores case in custom names.</summary>
    public bool Equals(CardRole? other) =>
        other is not null
        && Kind == other.Kind
        && string.Equals(CustomName, other.CustomName, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(Kind, StringComparer.OrdinalIgnoreCase.GetHashCode(CustomName));

    /// <summary>Parses a key produced by <see cref="Key"/>.</summary>
    public static CardRole Parse(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        const string customPrefix = "Custom:";
        if (key.StartsWith(customPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new CardRole(CardRoleKind.Custom, key[customPrefix.Length..]);
        }

        return Enum.TryParse<CardRoleKind>(key, ignoreCase: true, out var kind) && kind != CardRoleKind.Custom
            ? new CardRole(kind)
            : throw new DomainException("role.unknown", $"Unknown card role '{key}'.");
    }
}
