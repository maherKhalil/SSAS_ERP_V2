using System.Reflection;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Architecture.Tests;

// TS-1A/TS-1B guards (ADR-017). These protect the boundaries the slice deliberately did NOT cross, so a
// later slice cannot quietly widen it: no routing runtime, no customer-managed connectivity, no tenant
// DbContext, no HTTP surface, and no credential material in the registry.
public sealed class TenantStorageRegistryArchitectureTests
{
  private static readonly Assembly DomainAssembly = typeof(TenantDatabase).Assembly;
  private static readonly Assembly InfrastructureAssembly = typeof(PlatformDbContext).Assembly;

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Registry_entities_are_platform_metadata_not_tenant_owned()
  {
    // Registry rows describe physical databases and routing, not tenant business data. If either became
    // ITenantOwnedEntity the global filter would hide routing from the very infrastructure that resolves
    // it, and bootstrap could not run without an ambient tenant.
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(TenantDatabase).GetInterfaces());
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(TenantDatabaseAssignment).GetInterfaces());
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Registry_entities_persist_no_credential_or_endpoint_material()
  {
    // ADR-017: only trusted routing metadata is persisted. Complete connection strings, passwords,
    // certificates, private keys, and customer endpoint/credential-reference fields must never appear.
    //
    // Substring terms are chosen to be unambiguous; "Host" and "Port" are matched as EXACT names instead,
    // because HostingMode is a legitimate routing dimension and "Port" collides with ordinary words.
    string[] forbiddenSubstrings =
    [
      "ConnectionString", "Password", "Secret", "Credential", "Certificate", "PrivateKey",
      "Endpoint", "AuthenticationMode"
    ];
    string[] forbiddenExactNames = ["Host", "Port", "ServerInstanceName", "Address"];

    foreach (var type in new[] { typeof(TenantDatabase), typeof(TenantDatabaseAssignment) })
    {
      foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        Assert.DoesNotContain(forbiddenSubstrings, term =>
          property.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(forbiddenExactNames, name =>
          string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));
      }
    }
  }

  [Fact]
  [Trait("Decision", "ADR-021")]
  public void No_customer_managed_runtime_path_exists()
  {
    // CustomerManaged is an architecture-ready enum value only. Nothing may connect to, resolve secrets
    // for, or validate connectivity to a customer-managed database in this slice.
    var offenders = InfrastructureAssembly.GetTypes()
      .Where(type => type.Name.Contains("CustomerManaged", StringComparison.Ordinal) ||
        type.Name.Contains("CustomerDatabase", StringComparison.Ordinal))
      .Select(type => type.FullName)
      .ToArray();

    Assert.Empty(offenders);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void No_tenant_dbcontext_is_introduced_yet()
  {
    // TS-1C adds the resolver and connection factory; TenantDbContext and the separate tenant migration
    // stream remain a later, separately reviewed slice.
    var types = DomainAssembly.GetTypes()
      .Concat(InfrastructureAssembly.GetTypes())
      .Concat(typeof(ITenantDatabaseResolver).Assembly.GetTypes())
      .Select(type => type.Name)
      .ToArray();

    Assert.DoesNotContain("TenantDbContext", types);
    Assert.DoesNotContain("TenantDbContextFactory", types);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_resolver_is_application_level_and_free_of_ambient_request_context()
  {
    // The resolver must be equally usable from a background worker, so it may not depend on HTTP context.
    // Taking the tenant as an explicit parameter is what keeps that true.
    Assert.Equal(typeof(ITenantDatabaseResolver).Assembly, typeof(TenantDatabaseResolver).Assembly);

    var dependencies = typeof(TenantDatabaseResolver).GetConstructors()
      .SelectMany(constructor => constructor.GetParameters())
      .Select(parameter => parameter.ParameterType.Name)
      .ToArray();
    Assert.DoesNotContain("IHttpContextAccessor", dependencies);

    var resolveMethod = typeof(ITenantDatabaseResolver).GetMethod(nameof(ITenantDatabaseResolver.ResolveAsync));
    Assert.NotNull(resolveMethod);
    Assert.Contains(resolveMethod!.GetParameters(), parameter => parameter.ParameterType == typeof(Guid));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_route_carries_only_non_secret_metadata()
  {
    // The route crosses into the Application layer, so anything on it may reach logs and diagnostics.
    string[] forbidden =
    [
      "ConnectionString", "Password", "Username", "Secret", "Credential", "Certificate", "PrivateKey",
      "Endpoint", "Token", "AuthenticationMode"
    ];

    foreach (var property in typeof(TenantDatabaseRoute).GetProperties())
    {
      Assert.DoesNotContain(forbidden, term =>
        property.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Connection_construction_stays_in_infrastructure()
  {
    // Credential material must not travel through the Application layer; the factory therefore lives in
    // Infrastructure and returns an open-able connection rather than a credentialed string.
    Assert.Equal(InfrastructureAssembly, typeof(ITenantDatabaseConnectionFactory).Assembly);
    Assert.Equal(InfrastructureAssembly, typeof(TenantDatabaseConnectionFactory).Assembly);

    var createMethod = typeof(ITenantDatabaseConnectionFactory).GetMethod(
      nameof(ITenantDatabaseConnectionFactory.Create));
    Assert.NotNull(createMethod);
    Assert.DoesNotContain("String", createMethod!.ReturnType.GenericTypeArguments.Select(type => type.Name));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void No_routing_cache_is_introduced_yet()
  {
    // TS-1C is deliberately uncached: correctness first, and RoutingVersion semantics proven before any
    // cache exists. A cache added later must key validity to RoutingVersion (ADR-020).
    var routingTypes = typeof(ITenantDatabaseResolver).Assembly.GetTypes()
      .Concat(InfrastructureAssembly.GetTypes())
      .Where(type => type.Name.Contains("TenantDatabase", StringComparison.Ordinal) ||
        type.Name.Contains("TenantStorage", StringComparison.Ordinal))
      .ToArray();

    Assert.DoesNotContain(routingTypes, type =>
      type.Name.Contains("Cache", StringComparison.OrdinalIgnoreCase));
    Assert.DoesNotContain(routingTypes, type => type.GetConstructors()
      .SelectMany(constructor => constructor.GetParameters())
      .Any(parameter => parameter.ParameterType.Name.Contains("Cache", StringComparison.OrdinalIgnoreCase)));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void No_tenant_storage_http_surface_or_permission_is_introduced_yet()
  {
    // No administrative API exists, so no storage permission may exist either — a permission granting
    // access to nothing is worse than no permission at all.
    var apiDirectory = Path.Combine(RepositoryRoot(), "src", "Platform", "SSAS.Platform.API");
    Assert.False(Directory.Exists(Path.Combine(apiDirectory, "TenantStorage")));

    var permissionNames = File.ReadAllText(Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Application", "Permissions", "PlatformPermissionNames.cs"));
    Assert.DoesNotContain("TenantStorage", permissionNames, StringComparison.Ordinal);
  }

  private static string RepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
    {
      directory = directory.Parent;
    }

    Assert.NotNull(directory);
    return directory!.FullName;
  }
}
