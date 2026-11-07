using DuelLab.Application.Common;
using DuelLab.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DuelLab.Api.Middleware;

/// <summary>
/// Turns known exceptions into RFC 9457 problem responses with a stable <c>code</c>, so the UI can show
/// a useful message. Anything else becomes a generic 500 without internal details.
/// </summary>
internal sealed partial class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, code, detail) = exception switch
        {
            NotFoundException e => (StatusCodes.Status404NotFound, "Not found", "not_found", e.Message),
            DomainException e => (StatusCodes.Status400BadRequest, "Invalid request", e.Code, e.Message),
            RequestValidationException e => (StatusCodes.Status400BadRequest, "Invalid request", e.Code, e.Message),
            ExternalServiceException e => (StatusCodes.Status503ServiceUnavailable, "External service unavailable", "external_unavailable", e.Message),
            BadHttpRequestException e => (e.StatusCode, "Bad request", "bad_request", e.Message),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", "internal_error", "Something went wrong. The error has been logged."),
        };

        if (status >= 500)
        {
            LogUnhandled(logger, exception);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Extensions = { ["code"] = code },
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Request failed")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
