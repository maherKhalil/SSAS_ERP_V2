namespace SSAS.Host.API.Diagnostics;

public static class CorrelationIdApplicationBuilderExtensions
{
  public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder applicationBuilder)
  {
    ArgumentNullException.ThrowIfNull(applicationBuilder);

    return applicationBuilder.UseMiddleware<CorrelationIdMiddleware>();
  }
}
