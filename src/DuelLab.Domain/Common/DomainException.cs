namespace DuelLab.Domain.Common;

/// <summary>
/// Thrown when an operation would break a domain rule (an invariant). The API maps it to HTTP 400.
/// </summary>
public sealed class DomainException : Exception
{
    /// <summary>Creates the exception with a machine-readable code and a human-readable message.</summary>
    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>A stable, machine-readable identifier for the broken rule, e.g. <c>deck.copy_limit</c>.</summary>
    public string Code { get; }
}
