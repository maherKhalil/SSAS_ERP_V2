using SSAS.HR.API.Departments;
using SSAS.HR.API.Employees;
using System.Globalization;
using Serilog;
using SSAS.GL.API;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Host.API.Diagnostics;
using SSAS.Host.API.Errors;
using SSAS.HR.API;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Infrastructure;
using SSAS.BuildingBlocks.Tenancy.Permissions;
using SSAS.Platform.API;
using SSAS.Platform.API.Authentication;
using SSAS.Platform.API.Companies;
using SSAS.Platform.API.IdentityAccess;
using SSAS.Platform.API.Localization;
using SSAS.Platform.API.PlatformSupport;
using SSAS.Platform.Infrastructure;
using SSAS.Platform.Application.Permissions;
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
    // HR persistence and its contribution to the single tenant model (ADR-012: the Host is the one place
    // permitted to see a module's Infrastructure, and module registration is explicit, never discovered).
    .AddHrInfrastructure()
    .AddGlModule();

  // ---- MODULE PERMISSION DEFINITIONS, REGISTERED EXPLICITLY (ADR-012 r1.2, FP-006P).
  //
  // A role may only be granted a permission the composed catalog defines, so a module that is not named
  // here contributes nothing and its endpoints refuse everyone. That is a loud, reviewable omission rather
  // than a silent one, and it is registration -- never reflection-based discovery.
  //
  // GL adds its line here when it defines permissions of its own.
  builder.Services.AddSingleton<IPermissionCatalogContributor, HrPermissionCatalogContributor>();

  var app = builder.Build();

  // ---- COMPOSE THE PERMISSION CATALOG NOW, NOT ON THE FIRST REQUEST THAT NEEDS IT.
  //
  // A duplicate or malformed module contribution is a composition defect. The catalog is a singleton, so
  // without this it would be built lazily and the failure would surface as a 500 on whichever request
  // happened to authorize first. A host that refuses to start is the correct answer.
  _ = app.Services.GetRequiredService<IPermissionCatalog>();

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
  // HR module transport (ADR-012: the Host maps each module's own endpoints; modules never map each other's).
  app.MapHrEmployeeEndpoints();
  app.MapHrDepartmentEndpoints();
  app.MapHrEmployeeDepartmentEndpoints();

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
