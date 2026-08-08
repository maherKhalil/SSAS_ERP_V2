using System.Reflection;
using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Tenants;

namespace SSAS.Architecture.Tests;

public sealed class TenantLifecycleArchitectureTests
{
  [Fact]
  [Trait("NonFunctional", "NFR-TEN-0302")]
  [Trait("Scenario", "TS-TEN-0030")]
  public void Tenant_domain_and_application_are_framework_and_module_independent()
  {
    var forbidden = new[]
    {
      "Microsoft.EntityFrameworkCore",
      "Microsoft.Data.SqlClient",
      "Microsoft.AspNetCore",
      "SSAS.HR",
      "SSAS.GL"
    };
    var assemblies = new[] { typeof(Tenant).Assembly, typeof(CreateTenantCommandHandler).Assembly };

    var violations = assemblies.SelectMany(assembly => assembly.GetReferencedAssemblies()
      .Where(reference => forbidden.Any(prefix => reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true))
      .Select(reference => $"{assembly.GetName().Name} -> {reference.Name}")).ToArray();

    Assert.Empty(violations);
  }

  [Fact]
  [Trait("NonFunctional", "NFR-TEN-0301")]
  [Trait("Scenario", "TS-TEN-0018")]
  public void Every_tenant_handler_exposes_async_cancellation_boundary()
  {
    var handlers = new[]
    {
      typeof(CreateTenantCommandHandler),
      typeof(ActivateTenantCommandHandler),
      typeof(SuspendTenantCommandHandler),
      typeof(ReactivateTenantCommandHandler),
      typeof(ArchiveTenantCommandHandler),
      typeof(GetTenantQueryHandler),
      typeof(ListTenantsQueryHandler),
      typeof(GetTenantAuthenticationEligibilityQueryHandler)
    };

    Assert.All(handlers, handler =>
    {
      var method = Assert.Single(handler.GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Where(candidate => candidate.Name == "HandleAsync"));
      Assert.True(typeof(Task).IsAssignableFrom(method.ReturnType));
      Assert.Contains(method.GetParameters(), parameter => parameter.ParameterType == typeof(CancellationToken));
    });
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0007")]
  [Trait("Scenario", "TS-TEN-0031")]
  public void Tenant_repository_and_source_expose_no_generic_query_or_physical_delete_boundary()
  {
    Assert.False(typeof(ITenantRepository).IsGenericType);
    Assert.DoesNotContain(typeof(ITenantRepository).GetMethods(), method =>
      Regex.IsMatch(method.Name, "Delete|Remove", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    Assert.DoesNotContain(typeof(ITenantRepository).GetMethods(), method =>
      method.ReturnType.ToString().Contains("IQueryable", StringComparison.Ordinal));

    var tenantSource = PlatformSourceFiles()
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Where(path =>
      {
        var source = File.ReadAllText(path);
        return source.Contains("SSAS.Platform.Domain.Tenants;", StringComparison.Ordinal) ||
          source.Contains("SSAS.Platform.Application.Tenants;", StringComparison.Ordinal);
      })
      .ToArray();
    Assert.Empty(tenantSource.Where(path => Regex.IsMatch(
      File.ReadAllText(path),
      @"\bDeleteTenant(?:Command|Handler)?\b|\bTenants\.Remove\s*\(|IgnoreQueryFilters\s*\(",
      RegexOptions.CultureInvariant)));
  }

  [Fact]
  [Trait("Security", "SEC-TEN-0205")]
  [Trait("Scenario", "TS-TEN-0035")]
  public void Tenant_events_contain_only_safe_lifecycle_values()
  {
    var eventTypes = typeof(Tenant).Assembly.GetTypes()
      .Where(type => typeof(DomainEvent).IsAssignableFrom(type))
      .Where(type => type.Namespace == "SSAS.Platform.Domain.Events")
      .Where(type => type.Name.StartsWith("Tenant", StringComparison.Ordinal) &&
        type.Name is not "TenantUserActivated" and
        not "TenantUserDeactivated" and
        not "TenantUserInvited" and
        not "TenantUserReactivated" and
        not "TenantUserRoleAssigned" and
        not "TenantUserRoleRemoved")
      .ToArray();
    var unsafeProperties = eventTypes.SelectMany(type => type.GetProperties()
      .Where(property => Regex.IsMatch(
        property.Name,
        "Http|Claim|Credential|Token|Subscription|Billing|Company|ReasonText|Actor|Correlation|Request|Trace",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
      .Select(property => $"{type.Name}.{property.Name}")).ToArray();

    Assert.Equal(7, eventTypes.Length);
    Assert.Empty(unsafeProperties);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0020")]
  [Trait("Scenario", "TS-TEN-0032")]
  [Trait("Scenario", "TS-TEN-0034")]
  public void Milestone_contains_no_deferred_tenant_endpoint_or_post_session_implementation()
  {
    var files = PlatformSourceFiles().ToArray();
    var deferredDeclaration = new Regex(
      @"\b(?:class|record|interface)\s+\w*(?:TenantController|Subscription|Billing|CompanyProvision)\w*\b",
      RegexOptions.CultureInvariant);

    Assert.Empty(files.Where(path => deferredDeclaration.IsMatch(File.ReadAllText(path))));
    Assert.Empty(files.Where(path =>
      path.Contains($"{Path.DirectorySeparatorChar}SSAS.Platform.API{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
      File.ReadAllText(path).Contains("SSAS.Platform.Application.Tenants", StringComparison.Ordinal)));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0014")]
  [Trait("Scenario", "TS-TEN-0028")]
  public void Tenant_migration_creates_only_tenants_and_does_not_retrofit_legacy_foreign_keys()
  {
    var migration = Directory.EnumerateFiles(
        Path.Combine(FindRepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence", "Migrations"),
        "*AddTenantLifecycle.cs")
      .Single(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal));
    var source = File.ReadAllText(migration);

    Assert.Single(Regex.Matches(source, "migrationBuilder.CreateTable", RegexOptions.CultureInvariant).Cast<Match>());
    Assert.DoesNotContain("AddForeignKey", source, StringComparison.Ordinal);
    Assert.DoesNotContain("InsertData", source, StringComparison.Ordinal);
    Assert.DoesNotContain("TenantUsers", source, StringComparison.Ordinal);
    Assert.DoesNotContain("Roles", source, StringComparison.Ordinal);
    Assert.Contains("TR_Tenants_PreventDelete", source, StringComparison.Ordinal);
  }

  private static IEnumerable<string> PlatformSourceFiles() => Directory
    .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src", "Platform"), "*.cs", SearchOption.AllDirectories);

  private static string FindRepositoryRoot()
  {
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}
