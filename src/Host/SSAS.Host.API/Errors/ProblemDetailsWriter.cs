using Microsoft.AspNetCore.Mvc;
using SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;
using SSAS.Host.API.Diagnostics;

namespace SSAS.Host.API.Errors;

public static class ProblemDetailsWriter
{
  public const string CorrelationIdExtensionName = "correlationId";

  public static async Task WriteAsync(
    HttpContext context,
    int statusCode,
    string title,
    string type,
    string? code = null,
    string? resourceKey = null)
  {
    var problemDetails = new ProblemDetails
    {
      Status = statusCode,
      Title = title,
      Type = type
    };
    problemDetails.Extensions[CorrelationIdExtensionName] = GetCorrelationId(context);
    if (!string.IsNullOrWhiteSpace(code)) problemDetails.Extensions["code"] = code;
    if (!string.IsNullOrWhiteSpace(resourceKey)) problemDetails.Extensions["resourceKey"] = resourceKey;
    context.Response.StatusCode = statusCode;

    var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
    if (!await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
    {
      HttpContext = context,
      ProblemDetails = problemDetails
    }))
    {
      await context.Response.WriteAsJsonAsync(problemDetails);
    }
  }

  public static string GetCorrelationId(HttpContext context)
  {
    var correlationContext = context.RequestServices.GetService<ICorrelationContext>();

    return !string.IsNullOrWhiteSpace(correlationContext?.CorrelationId)
      ? correlationContext.CorrelationId
      : context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
  }
}
