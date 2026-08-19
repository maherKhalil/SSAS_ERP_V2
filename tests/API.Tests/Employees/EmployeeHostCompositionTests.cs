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
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.HR.API;
using SSAS.HR.API.Employees;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Infrastructure;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.API;
using SSAS.Platform.Infrastructure;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
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

    Assert.Equal(["Branch", "Company", "Employee", "EmployeeBranchAssignment"], tenantOwned);
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

    return builder.Services;
  }
}
