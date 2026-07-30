using System.Text.RegularExpressions;
using Serilog.Context;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.Host.API.Diagnostics;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
  public const string HeaderName = "X-Correlation-ID";

  public async Task InvokeAsync(HttpContext context, CorrelationContext correlationContext)
  {
    var correlationId = GetCorrelationId(context);
    correlationContext.SetCorrelationId(correlationId);
    context.Response.Headers[HeaderName] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
      await next(context);
    }
  }

  private static string GetCorrelationId(HttpContext context)
  {
    var incomingValue = context.Request.Headers[HeaderName];

    return incomingValue.Count == 1 && IsValid(incomingValue[0])
      ? incomingValue[0]!
      : Guid.NewGuid().ToString("N");
  }

  private static bool IsValid(string? value)
  {
    return value is not null && Regex.IsMatch(
      value,
      "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$",
      RegexOptions.CultureInvariant,
      TimeSpan.FromMilliseconds(100));
  }
}
