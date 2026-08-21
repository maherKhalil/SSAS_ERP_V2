using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.BuildingBlocks.Tenancy.Permissions;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.HR.API;
using SSAS.HR.API.Employees;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Infrastructure;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.API;
using SSAS.Platform.Infrastructure;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.Employees;

// ==================================================================================================
// THE COMPOSITION TESTS THE C1 DEFECT WOULD HAVE FAILED (FP-006C5, H1-H8).
// ==================================================================================================
//
// The 140-failure API regression was a service graph that could not be built, and nothing asserted that it
// could. It surfaced only when some other test happened to call BuildServiceProvider — which is to say, by
// accident.
//
// These build the PRODUCTION registrations with validation on. Nothing here is stubbed away: the whole point
// is to exercise what the Host actually composes, so a missing or mis-scoped registration fails here rather
// than in production.
[Collection(EmployeeApiEndpointGroup.Name)]
public sealed class EmployeeHostCompositionTests
{
  // ---- H1. THE PRODUCTION HOST GRAPH BUILDS, WITH VALIDATION ON.
  //
  // ValidateOnBuild plus ValidateScopes is what turns "a service is missing" and "a singleton captures a
  // scoped dependency" from runtime surprises into a startup failure. This is the assertion whose absence
  // let the C1 defect live through three slices.
  [Fact]
  public void H1_The_production_api_host_service_provider_builds_under_validation()
  {
    using var provider = BuildProductionProvider();

    Assert.NotNull(provider);
  }

  // ---- H2. EVERY EMPLOYEE ENDPOINT'S DEPENDENCIES RESOLVE.
  //
  // Building the provider proves the descriptors are satisfiable; this proves the specific graphs the nine
  // routes need can actually be constructed, in a real request scope.
  [Fact]
  public async Task H2_Every_employee_endpoint_dependency_graph_resolves()
  {
    using var provider = BuildProductionProvider();
    // Disposed ASYNCHRONOUSLY: the tenant context provider is IAsyncDisposable, and a synchronous dispose
    // throws rather than releasing it — the same trap a request scope would hit in production.
    await using var scope = provider.CreateAsyncScope();

    Assert.NotNull(scope.ServiceProvider.GetRequiredService<CreateEmployeeCommandHandler>());
    Assert.NotNull(scope.ServiceProvider.GetRequiredService<UpdateEmployeeProfileCommandHandler>());
    Assert.NotNull(scope.ServiceProvider.GetRequiredService<ActivateEmployeeCommandHandler>());
    Assert.NotNull(scope.ServiceProvider.GetRequiredService<DeactivateEmployeeCommandHandler>());
    Assert.NotNull(scope.ServiceProvider.GetRequiredService<TerminateEmployeeCommandHandler>());
    Assert.NotNull(scope.ServiceProvider.GetRequiredService<TransferEmployeeCommandHandler>());
    Assert.NotNull(scope.ServiceProvider.GetRequiredService<GetEmployeeQueryHandler>());
    Assert.NotNull(scope.ServiceProvider.GetRequiredService<SearchEmployeesQueryHandler>());
    Assert.NotNull(scope.ServiceProvider.GetRequiredService<GetEmployeeBranchHistoryQueryHandler>());
    Assert.NotNull(scope.ServiceProvider.GetRequiredService<CompanyContextEndpointFilter>());
  }

  // ---- H3. ICurrentCompany RESOLVES THROUGH ITS REAL CHAIN.
  //
  // THE EXACT THING THAT WAS BROKEN. CurrentCompany needs ICompanyContextResolver, which needs
  // ITenantCompanyAccessResolver, which reads two databases. Resolving it here proves the chain is composed,
  // not merely declared.
  [Fact]
  public async Task H3_The_current_company_resolves_with_its_production_chain()
  {
    using var provider = BuildProductionProvider();
    // Disposed ASYNCHRONOUSLY: the tenant context provider is IAsyncDisposable, and a synchronous dispose
    // throws rather than releasing it — the same trap a request scope would hit in production.
    await using var scope = provider.CreateAsyncScope();

    Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICurrentCompany>());
    Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICompanyContextEstablisher>());
    Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICompanyContextResolver>());
    Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITenantCompanyAccessResolver>());

    // The selection is intent-only and carries no authority, so it lives with the request accessors and must
    // still be there.
    Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICompanySelection>());
  }

  // ---- H4. ONE COMPANY CONTEXT PER REQUEST, AND ONE ANSWER.
  //
  // Establishing and reporting must be the SAME instance. Two instances would let a request establish a
  // company and then read a different one — the kind of split-brain that is invisible until it matters.
  [Fact]
  public async Task H4_The_company_context_is_one_scoped_instance_answering_both_roles()
  {
    using var provider = BuildProductionProvider();
    // Disposed ASYNCHRONOUSLY: the tenant context provider is IAsyncDisposable, and a synchronous dispose
    // throws rather than releasing it — the same trap a request scope would hit in production.
    await using var scope = provider.CreateAsyncScope();

    var current = scope.ServiceProvider.GetRequiredService<ICurrentCompany>();
    var establisher = scope.ServiceProvider.GetRequiredService<ICompanyContextEstablisher>();

    Assert.Same(current, establisher);
    Assert.Same(current, scope.ServiceProvider.GetRequiredService<ICurrentCompany>());
  }

  // ---- H5. NO DUPLICATE REGISTRATION.
  //
  // AddPlatformCompanyContext is idempotent and is reached both directly and through the platform
  // registration. A second descriptor would make the resolved implementation depend on registration order.
  [Fact]
  public void H5_The_company_context_and_access_resolver_are_registered_exactly_once()
  {
    var services = ProductionServices();

    Assert.Single(services.Where(descriptor => descriptor.ServiceType == typeof(ICurrentCompany)));
    Assert.Single(services.Where(descriptor => descriptor.ServiceType == typeof(ICompanyContextEstablisher)));
    Assert.Single(services.Where(descriptor => descriptor.ServiceType == typeof(ITenantCompanyAccessResolver)));
    Assert.Single(services.Where(descriptor => descriptor.ServiceType == typeof(ICompanyContextResolver)));
  }

  // ---- H6. THE SUPPORTED ORDERING IS ACCEPTED, AND SO IS THE OTHER ONE.
  //
  // TryAdd means composing the company context before or after the platform registration produces the same
  // graph. Asserting both orders is what makes the idempotency a fact rather than an intention.
  [Fact]
  public void H6_Registration_order_does_not_change_the_composed_graph()
  {
    var hostOrder = ProductionServices();
    var reversed = ProductionServices(composeCompanyContextFirst: true);

    Assert.Single(reversed.Where(descriptor => descriptor.ServiceType == typeof(ICurrentCompany)));

    var hostLifetime = hostOrder.Single(descriptor => descriptor.ServiceType == typeof(ICurrentCompany)).Lifetime;
    var reversedLifetime = reversed.Single(descriptor => descriptor.ServiceType == typeof(ICurrentCompany)).Lifetime;

    Assert.Equal(ServiceLifetime.Scoped, hostLifetime);
    Assert.Equal(hostLifetime, reversedLifetime);
  }

  // ---- H7. ONE REQUEST'S COMPANY DOES NOT BECOME ANOTHER'S.
  //
  // The company context is per-request state. A singleton — or a scoped service captured by one — would
  // carry the company established for one caller into the next caller's request, which is a cross-tenant
  // data leak with no error to notice it by.
  [Fact]
  public async Task H7_The_company_context_does_not_leak_between_request_scopes()
  {
    using var provider = BuildProductionProvider();

    await using var first = provider.CreateAsyncScope();
    await using var second = provider.CreateAsyncScope();

    Assert.NotSame(
      first.ServiceProvider.GetRequiredService<ICurrentCompany>(),
      second.ServiceProvider.GetRequiredService<ICurrentCompany>());
  }

  // ---- H8. AND NEITHER DOES AN OPEN TRANSFER.
  //
  // The sanctioned transfer channel authorizes exactly one declaration for the request that opened it. A
  // declaration surviving into another request would authorize a BranchId change nobody asked for.
  [Fact]
  public async Task H8_The_branch_transfer_scope_does_not_leak_between_request_scopes()
  {
    using var provider = BuildProductionProvider();

    await using var first = provider.CreateAsyncScope();
    await using var second = provider.CreateAsyncScope();

    var firstScope = first.ServiceProvider.GetRequiredService<IBranchTransferScope>();
    var secondScope = second.ServiceProvider.GetRequiredService<IBranchTransferScope>();

    Assert.NotSame(firstScope, secondScope);
    Assert.Null(firstScope.Current);
    Assert.Null(secondScope.Current);
  }

  // ================================================================================================
  // H9 — THE PRODUCTION CUTOVER SEES THE MODULE-CONTRIBUTED TENANT ENTITIES (FP-006C6).
  // ================================================================================================
  //
  // ---- THE HALF THAT IS EASY TO GET WRONG.
  //
  // Fixing the cutover model in the tests and leaving production contributor-free would look identical from
  // every cutover test, because those tests construct the copy service themselves. This resolves the model
  // source from the REAL Host composition — the same registration chain Program.cs builds — and asserts the
  // HR entities are in it.
  //
  // If AddHrInfrastructure ever stopped registering the contributor, or the model source stopped resolving
  // the registered set, this fails here rather than during somebody's tenant promotion.
  [Fact]
  public void H9_The_host_composed_tenant_model_contains_the_contributed_hr_entities()
  {
    using var provider = BuildProductionProvider();

    var model = provider.GetRequiredService<ITenantModelSource>().Model;

    var tenantOwned = model.GetEntityTypes()
      .Where(entity => !entity.IsOwned())
      .Where(entity => typeof(ITenantOwnedEntity).IsAssignableFrom(entity.ClrType))
      .Where(entity => entity.GetTableName() is not null)
      .Select(entity => entity.ClrType.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    // ---- AN EXACT INVENTORY, NOT A CONTAINS-CHECK, AND THAT IS THE POINT.
    //
    // The cutover engine DERIVES its manifest from this model, so it can never miss a table. This list is
    // the other half: it guarantees a human SEES a new one, because a new tenant-owned entity may need
    // ordering, identity or column decisions that "it compiles" does not settle. FP-007 Phase 1 added three,
    // and this assertion is where they were noticed. FP-008 Phase 1 added four more.
    //
    // ---- THIS ASSERTION WAS RED FROM FP-008 PHASE 1 UNTIL PHASE 3, AND NOTHING REPORTED IT.
    //
    // The phase exit gate is the full Debug INTEGRATION suite, and `API.Tests` is a different project that
    // no gate ran. So the four position entities were added, the manifest inventory in
    // `TenantCutoverCopySqlServerTests` was updated as one deliberate act, `DEC-POS-0022`'s nine-site map
    // was written — and this tenth site sat outside all of it, failing quietly for two phases.
    //
    // It is fixed here and recorded rather than tidied, because the interesting fact is not the list: it is
    // that an exact-inventory guard is only as good as the suite that runs it.
    Assert.Equal(
      [
        "Branch",
        "Company",
        "Department",
        "DepartmentManager",
        "Employee",
        "EmployeeBranchAssignment",
        "EmployeeDepartmentAssignment",
        "EmployeePositionAssignment",
        "JobGrade",
        "Position",
        "SalaryGrade"
      ],
      tenantOwned);
  }

  // ---- H10. ONE CONTRIBUTOR SET, NOT THREE.
  //
  // Runtime persistence, the migration tool and the cutover must all compose the same tenant model. They
  // reach it by different routes — DI for the first and third, an explicit list for the tool, which has no
  // container — so this asserts the routes agree rather than assuming they do.
  [Fact]
  public void H10_The_registered_contributor_set_is_the_one_the_cutover_and_runtime_share()
  {
    using var provider = BuildProductionProvider();

    var registered = provider.GetServices<ITenantModelContributor>()
      .Select(contributor => contributor.GetType().FullName)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    // Exactly HR today. A second contributing module must be added here deliberately, which is the prompt
    // to check the migration tool's list at the same time.
    Assert.Equal([typeof(HrTenantModelContributor).FullName], registered);

    // The model source resolves that same set rather than a list of its own.
    var model = provider.GetRequiredService<ITenantModelSource>().Model;
    Assert.NotNull(model.FindEntityType(typeof(SSAS.HR.Domain.Employees.Employee)));
  }

  // ================================================================================================
  // H11 — THE PRODUCTION CONTAINER HANDS OUT A COMPOSED PERMISSION CATALOG (FP-006P, ADR-012 r1.2).
  // ================================================================================================
  //
  // ---- THE HALF THAT WAS MISSING, AND THE REASON NOTHING CAUGHT IT.
  //
  // A role may only be granted a permission the catalog defines. HR's five names were constants no catalog
  // knew, so nothing could grant them and every Employee endpoint refused every caller — while every test
  // passed, because tests supply permissions directly and never ask whether one is grantable.
  //
  // This resolves IPermissionCatalog from the REAL container. If the composition regressed to Platform's
  // own catalog, or the contributor stopped being registered, this fails here rather than in production.
  [Fact]
  public void H11_The_host_permission_catalog_contains_the_contributed_hr_permissions()
  {
    using var provider = BuildProductionProvider();

    var catalog = provider.GetRequiredService<IPermissionCatalog>();

    Assert.IsType<ComposedPermissionCatalog>(catalog);

    foreach (var permission in new HrPermissionCatalogContributor().Permissions)
    {
      Assert.True(catalog.TryGet(permission.Name, out var definition), permission.Name);

      // TENANT SCOPE, so it is assignable to an ordinary tenant role. A PlatformSupport-scoped definition
      // would be refused by Role.AssignPermission and the permission would still be ungrantable.
      Assert.Equal(PermissionScope.Tenant, definition.Scope);
    }

    // ---- AND THE CONTRIBUTOR ACTUALLY OFFERS WHAT IT IS SUPPOSED TO.
    //
    // The loop above iterates the contributor, so it would pass just as happily against an EMPTY one — it
    // proves "everything offered is grantable", not "the right things are offered". Naming the permissions
    // closes that: FP-006P's failure was constants defined nowhere the role path could see, and a
    // contributor that silently stopped offering a name would reproduce it exactly.
    string[] expected =
    [
      HrPermissionNames.ViewEmployees,
      HrPermissionNames.CreateEmployees,
      HrPermissionNames.UpdateEmployees,
      HrPermissionNames.TransferEmployees,
      HrPermissionNames.TerminateEmployees,
      HrPermissionNames.ViewDepartments,
      HrPermissionNames.CreateDepartments,
      HrPermissionNames.UpdateDepartments,
      HrPermissionNames.DeactivateDepartments,
      // FP-008 Phase 2. Twelve more, taking the HR plane to twenty-one — and red here from the moment they
      // were added until FP-008 Phase 3, for the reason H9 above records: no gate runs this project.
      HrPermissionNames.ViewPositions,
      HrPermissionNames.CreatePositions,
      HrPermissionNames.UpdatePositions,
      HrPermissionNames.DeactivatePositions,
      HrPermissionNames.ViewJobGrades,
      HrPermissionNames.CreateJobGrades,
      HrPermissionNames.UpdateJobGrades,
      HrPermissionNames.DeactivateJobGrades,
      HrPermissionNames.ViewSalaryGrades,
      HrPermissionNames.CreateSalaryGrades,
      HrPermissionNames.UpdateSalaryGrades,
      HrPermissionNames.DeactivateSalaryGrades
    ];

    Assert.Equal(
      expected.OrderBy(name => name, StringComparer.Ordinal),
      new HrPermissionCatalogContributor().Permissions
        .Select(permission => permission.Name)
        .OrderBy(name => name, StringComparer.Ordinal));

    // NO Delete AND NO Manage. Deletion does not exist, so a permission for it would authorize nothing, and
    // a catch-all whose description cannot say what it permits is one nobody can grant responsibly.
    Assert.False(catalog.TryGet("HR.Departments.Delete", out _));
    Assert.False(catalog.TryGet("HR.Departments.Manage", out _));
  }

  // ---- H12. AND PLATFORM'S OWN CATALOG IS UNCHANGED BY THE COMPOSITION.
  //
  // Composing must ADD. If a module could displace or reshape a platform permission, this slice would have
  // quietly changed the platform authorization surface for every tenant.
  [Fact]
  public void H12_Composing_module_permissions_leaves_the_platform_catalog_intact()
  {
    using var provider = BuildProductionProvider();

    var catalog = provider.GetRequiredService<IPermissionCatalog>();
    var platformOnly = new PlatformPermissionCatalog();

    foreach (var expected in platformOnly.All)
    {
      Assert.True(catalog.TryGet(expected.Name.Value, out var actual), expected.Name.Value);
      Assert.Equal(expected.Scope, actual.Scope);
      Assert.Equal(expected.Description, actual.Description);
    }

    // ---- AND THE ADMINISTRATOR PERMISSION STILL GRANTS NO HR AUTHORITY.
    //
    // Platform.Tenant.Administer widens the company and branch SCOPE dimensions and confers no functional
    // permission (ADR-025 decision 8). Putting HR's names in the same catalog must not have blurred that:
    // they remain five separate definitions a role has to be granted individually.
    Assert.True(catalog.TryGet(PlatformPermissionNames.AdministerTenant, out var administer));
    Assert.Equal(PermissionScope.Tenant, administer.Scope);

    Assert.DoesNotContain(
      new HrPermissionCatalogContributor().Permissions,
      permission => StringComparer.Ordinal.Equals(
        permission.Name, PlatformPermissionNames.AdministerTenant));
  }

  // The Host's own composition, minus the HTTP pipeline. Connection strings point at a server that is never
  // contacted: building and validating a graph resolves no DbContext, and these tests deliberately never
  // execute a query.
  private static ServiceProvider BuildProductionProvider() =>
    ProductionServices().BuildServiceProvider(new ServiceProviderOptions
    {
      ValidateOnBuild = true,
      ValidateScopes = true
    });

  private static IServiceCollection ProductionServices(bool composeCompanyContextFirst = false)
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
      EnvironmentName = Environments.Development
    });

    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["Jwt:Issuer"] = EmployeeApiTestHost.Issuer,
      ["Jwt:Audience"] = EmployeeApiTestHost.Audience,
      ["Jwt:ClockSkewSeconds"] = "30",
      ["ConnectionStrings:Platform"] = "Server=composition-only;Database=composition-only;Integrated Security=True",
      ["TenantStorage:Servers:PrimarySqlServer:ConnectionString"] = "Server=composition-only;Integrated Security=True"
    });

    if (composeCompanyContextFirst)
    {
      builder.Services.AddPlatformCompanyContext();
    }

    builder.Services
      .AddHostApplicationOptions()
      .AddPlatformRequestContext()
      .AddPlatformInfrastructure(builder.Configuration)
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostProblemDetails()
      .AddPlatformModule()
      .AddHrModule()
      .AddHrInfrastructure();

    // ---- MIRRORS THE ONE LINE Program.cs USES TO REGISTER HR'S PERMISSION DEFINITIONS (FP-006P).
    //
    // Program.cs is top-level statements, so this graph is assembled rather than shared with it. That
    // divergence is exactly how a test proves a composition production does not have, which is why
    // ModulePermissionContributionArchitectureTests pins the literal registration line in Program.cs as
    // well. Both are needed: one proves the line exists, this one proves the graph it produces resolves.
    builder.Services.AddSingleton<IPermissionCatalogContributor, HrPermissionCatalogContributor>();

    return builder.Services;
  }
}
