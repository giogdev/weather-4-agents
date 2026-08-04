using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Weather4Agents.Domain.Exceptions;

namespace Weather4Agents.API.Errors;

/// <summary>
/// Translates unhandled exceptions into consistent <see cref="ProblemDetails"/> responses.
/// Known domain exceptions map to meaningful status codes; anything else becomes an
/// opaque <c>500</c> so internal details never reach the client.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = Map(exception);
        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        // Only unexpected failures are worth an error-level log with the full exception;
        // the mapped domain exceptions are expected client errors.
        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception processing {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    private static ProblemDetails Map(Exception exception) => exception switch
    {
        ProviderNotFoundException ex => new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Unknown weather provider",
            Detail = ex.Message,
            Extensions = { ["availableProviders"] = ex.AvailableProviders }
        },
        LocationNotFoundException ex => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Location not found",
            Detail = ex.Message
        },
        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred",
            Detail = "An unexpected error occurred while processing the request."
        }
    };
}
