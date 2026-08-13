using System.Globalization;
using Serilog;
using SSAS.GL.API;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Host.API.Diagnostics;
using SSAS.Host.API.Errors;
using SSAS.HR.API;
using SSAS.Platform.API;
using SSAS.Platform.API.Authentication;
using SSAS.Platform.API.Companies;
using SSAS.Platform.API.IdentityAccess;
using SSAS.Platform.API.Localization;
using SSAS.Platform.API.PlatformSupport;
using SSAS.Platform.Infrastructure;
using SSAS.Platform.Infrastructure.RequestContext;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
  .CreateBootstrapLogger();

try
{
  var builder = WebApplication.CreateBuilder(args);

  builder.ConfigureHostSerilog();

  builder.Services
    .AddHostApplicationOptions()
    .AddPlatformRequestContext()
    .AddPlatformInfrastructure(builder.Configuration)
    .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
    .AddHostAuthenticationTransport(builder.Configuration, builder.Environment)
    .AddHostPermissionAuthorization()
    .AddHostProblemDetails()
    .AddHostApiInfrastructure()
    .AddPlatformModule()
    .AddHrModule()
    .AddGlModule();

  var app = builder.Build();

  app.ConfigureTrustedForwarding(builder.Configuration);
  app.UseCorrelationId();
  app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    diagnosticContext.Set("CorrelationId", httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString()));
  app.UseExceptionHandler();
  app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api/platform/auth") &&
      !context.Request.Path.StartsWithSegments("/api/platform/support/auth"),
    branch => branch.UseHttpsRedirection());
  app.UseCors(AuthenticationTransportServiceCollectionExtensions.CorsPolicy);
  app.UseAuthentication();
  app.UseAuthorization();
  app.UseSwagger();
  app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "SSAS ERP API v1"));
  app.MapHostEndpoints();
  app.MapPlatformAuthenticationEndpoints();
  app.MapPlatformSupportAuthenticationEndpoints();
  app.MapPlatformLocalizationEndpoints();
  app.MapPlatformIdentityAccessEndpoints();
  app.MapPlatformSupportAuthorityEndpoints();
  app.MapPlatformCompanyEndpoints();

  app.Run();
}
catch (Exception exception)
{
  Log.Fatal(exception, "Host terminated unexpectedly");
  throw;
}
finally
{
  Log.CloseAndFlush();
}

public partial class Program
{
}
