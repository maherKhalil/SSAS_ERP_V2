using SSAS.HR.API.Departments;
using SSAS.HR.API.Employees;
using SSAS.HR.API.Positions;
using System.Globalization;
using Serilog;
using SSAS.GL.API;
using SSAS.Attendance.API;
using SSAS.Payroll.API;
using SSAS.GL.Application.Permissions;
using SSAS.Attendance.Application.Permissions;
using SSAS.Payroll.Application.Permissions;
using SSAS.GL.Infrastructure;
using SSAS.Attendance.Infrastructure;
using SSAS.Payroll.Infrastructure;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Host.API.Diagnostics;
using SSAS.Host.API.Errors;
using SSAS.HR.API;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Infrastructure;
using SSAS.BuildingBlocks.Tenancy.Permissions;
using SSAS.Platform.API;
using SSAS.Platform.API.Authentication;
using SSAS.Platform.API.Tenants;
using SSAS.Platform.API.Companies;
using SSAS.Platform.API.IdentityAccess;
using SSAS.Platform.API.Localization;
using SSAS.Platform.API.PlatformSupport;
using SSAS.Platform.Infrastructure;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Infrastructure.RequestContext;
using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Infrastructure.Subscriptions;
using SSAS.Platform.API.Subscriptions;
using SSAS.Platform.API.TenantUsers;

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
    .AddGlModule()
    // GL persistence and its contribution to the single tenant model, on the same terms as HR's: the Host
    // is the one place permitted to see a module's Infrastructure, and registration is explicit. Without
    // this line GL's seven entities are absent from the tenant model, from the migration stream, and --
    // silently -- from Shared to Dedicated cutover.
    .AddGlInfrastructure()
    // Payroll (FP-012), on the same terms as HR's and GL's: the Host registers the module and its
    // persistence EXPLICITLY. Without AddPayrollInfrastructure, Payroll's six entities are absent from the
    // tenant model, from the migration stream, and -- because TenantCutoverCopyPlan derives its manifest
    // from the model -- from Shared-to-Dedicated cutover, which fails SILENTLY.
    .AddPayrollModule()
    .AddPayrollInfrastructure()
    // Attendance (FP-013), on the same terms as the three before it. Without AddAttendanceInfrastructure,
    // Attendance's seven entities are absent from the tenant model, from the migration stream, and --
    // because TenantCutoverCopyPlan derives its manifest from the model -- from Shared-to-Dedicated
    // cutover, which fails SILENTLY.
    //
    // It also registers IAttendanceSummary, which Payroll now consumes at calculation and at approval.
    // Without it, every payroll approval would fail to resolve a dependency at REQUEST time rather than at
    // startup -- which is precisely the class of failure the eager composition below exists to prevent.
    .AddAttendanceModule()
    .AddAttendanceInfrastructure();

  // ---- MODULE PERMISSION DEFINITIONS, REGISTERED EXPLICITLY (ADR-012 r1.2, FP-006P).
  //
  // A role may only be granted a permission the composed catalog defines, so a module that is not named
  // here contributes nothing and its endpoints refuse everyone. That is a loud, reviewable omission rather
  // than a silent one, and it is registration -- never reflection-based discovery.
  //
  builder.Services.AddSingleton<IPermissionCatalogContributor, HrPermissionCatalogContributor>();

  // GL's line, added by FP-011. Thirteen definitions; without this every GL endpoint refuses every caller,
  // which is precisely the FP-006P failure -- the constants existed, no catalog defined them, and no role
  // could hold one.
  builder.Services.AddSingleton<IPermissionCatalogContributor, GlPermissionCatalogContributor>();

  // Payroll's line (FP-012). Nine definitions; without this every payroll endpoint refuses every caller --
  // FP-006P's incident, where HR's constants existed, no catalog defined them, and no role could hold one.
  builder.Services.AddSingleton<IPermissionCatalogContributor, PayrollPermissionCatalogContributor>();

  // Attendance's line (FP-013). Fourteen definitions; without this every attendance endpoint refuses every
  // caller -- FP-006P's incident, where HR's constants existed, no catalog defined them, and no role could
  // hold one.
  builder.Services.AddSingleton<IPermissionCatalogContributor, AttendancePermissionCatalogContributor>();

  // ---- MODULE ENABLEMENT: THE SEAM NOW READS REAL DATA (FP-014, T-040).
  //
  // Every module route group passes through `RequireModule`, an architecture guard asserts no module
  // group can be added without it, and each module's own mapping refuses to start without this contract
  // registered (T-034). What answers it is now the Platform database.
  //
  // **This is the cutover, and it is deliberately not lenient.** The transitional resolver that granted
  // every module to every tenant is DELETED, not left unregistered. A tenant with no subscription record
  // now reaches no gated module -- correct under `CON-0001`, which forbids a default plan, and the
  // interim state until T-041 seeds the 14-day trial `DEC-L-034` ruled.
  //
  // ---- WHY THE PIECES SIT WHERE THEY DO.
  //
  // The READ is Platform infrastructure; the CACHE is a singleton because it outlives a request by
  // design; the RESOLVER is scoped because it reads the request's tenant. `Platform.API` owns the
  // adapter because it is the one project referencing both the transport contract and Platform's
  // application layer -- Infrastructure must not take a dependency on `BuildingBlocks.Api`.
  //
  // The cache holds FACTS, not the answer, so expiry needs no invalidation event: `OD-SUB-0004` ruled
  // invalidation-on-change and never a TTL, and a lapsing term writes nothing to invalidate on.
  builder.Services.AddScoped<ITenantEntitlementReader, TenantEntitlementReader>();
  builder.Services.AddSingleton<ITenantEntitlementCache, InMemoryTenantEntitlementCache>();
  builder.Services.AddScoped<ITenantModuleEntitlement, TenantModuleEntitlement>();

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

  // T-091. Two lifecycle routes over handlers that already existed and were reachable from nothing —
  // deactivation is what termination now invokes, reactivation is the repair for its one half-state.
  app.MapPlatformTenantUserEndpoints();
  app.MapPlatformSupportAuthorityEndpoints();
  app.MapPlatformCompanyEndpoints();

  // T-155. The tenant registry: seven handlers that had existed since the platform shipped and were
  // reachable only from a test. Transport only — no command, handler or domain rule changed.
  app.MapPlatformTenantEndpoints();
  // HR module transport (ADR-012: the Host maps each module's own endpoints; modules never map each other's).
  app.MapHrEmployeeEndpoints();
  app.MapHrDepartmentEndpoints();
  app.MapHrEmployeeDepartmentEndpoints();
  app.MapHrPositionEndpoints();
  app.MapHrJobGradeEndpoints();
  app.MapHrSalaryGradeEndpoints();
  app.MapHrEmployeePositionEndpoints();

  // GL's surface: nineteen routes across accounts, the fiscal calendar, drafts, posted journals and
  // reporting. Mapped after HR so the route inventory reads in module order.
  app.MapGlEndpoints();

  // Payroll's surface: twenty routes across compensation, pay elements, periods, the run lifecycle and
  // payslips. Nothing responds to DELETE.
  app.MapPayrollEndpoints();

  // Attendance's surface (FP-013): twenty-five routes across the working calendar, attendance periods,
  // records and their adjustments, leave types, leave requests and administered balances.
  app.MapAttendanceEndpoints();

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
