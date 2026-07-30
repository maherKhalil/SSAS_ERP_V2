using System.ComponentModel.DataAnnotations;
using System.Security;
using Microsoft.AspNetCore.Diagnostics;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Host.API.Errors;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
  private static readonly Action<ILogger, Exception?> LogUnhandledRequestException = LoggerMessage.Define(
    LogLevel.Error,
    new EventId(1, nameof(LogUnhandledRequestException)),
    "Unhandled request exception");
  private static readonly Action<ILogger, int, Exception?> LogRequestFailure = LoggerMessage.Define<int>(
    LogLevel.Warning,
    new EventId(2, nameof(LogRequestFailure)),
    "Request failed with status code {StatusCode}");

  public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext,
    Exception exception,
    CancellationToken cancellationToken)
  {
    var (statusCode, title, type) = exception switch
    {
      ValidationException => (StatusCodes.Status400BadRequest, "Validation failed.", "https://httpstatuses.com/400"),
      UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication is required.", "https://httpstatuses.com/401"),
      SecurityException => (StatusCodes.Status403Forbidden, "You are not permitted to perform this action.", "https://httpstatuses.com/403"),
      KeyNotFoundException => (StatusCodes.Status404NotFound, "The requested resource was not found.", "https://httpstatuses.com/404"),
      BusinessRuleViolationException => (StatusCodes.Status409Conflict, "A business rule was violated.", "https://httpstatuses.com/409"),
      _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "https://httpstatuses.com/500")
    };

    if (statusCode >= StatusCodes.Status500InternalServerError)
    {
      LogUnhandledRequestException(logger, exception);
    }
    else
    {
      LogRequestFailure(logger, statusCode, exception);
    }

    await ProblemDetailsWriter.WriteAsync(httpContext, statusCode, title, type);

    return true;
  }
}
