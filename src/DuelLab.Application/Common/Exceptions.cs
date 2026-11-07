namespace DuelLab.Application.Common;

/// <summary>The requested resource does not exist. The API maps it to HTTP 404.</summary>
public sealed class NotFoundException(string resource, object id)
    : Exception($"{resource} '{id}' was not found.");

/// <summary>The request is well-formed but not acceptable. The API maps it to HTTP 400.</summary>
public sealed class RequestValidationException : Exception
{
    /// <summary>Creates the exception with a code and a message.</summary>
    public RequestValidationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>A stable, machine-readable code.</summary>
    public string Code { get; }
}

/// <summary>An external dependency (such as the card-data provider) failed. The API maps it to HTTP 502/503.</summary>
public sealed class ExternalServiceException(string message, Exception? inner = null) : Exception(message, inner);
