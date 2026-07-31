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
    .AddHostPermissionAuthorization()
    .AddHostProblemDetails()
    .AddHostApiInfrastructure()
    .AddPlatformModule()
    .AddHrModule()
    .AddGlModule();

  var app = builder.Build();

  app.UseCorrelationId();
  app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    diagnosticContext.Set("CorrelationId", httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString()));
  app.UseExceptionHandler();
  app.UseHttpsRedirection();
  app.UseAuthentication();
  app.UseAuthorization();
  app.UseSwagger();
  app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "SSAS ERP API v1"));
  app.MapHostEndpoints();

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
