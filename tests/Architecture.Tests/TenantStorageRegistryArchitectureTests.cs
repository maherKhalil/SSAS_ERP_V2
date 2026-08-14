using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
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
  public void The_tenant_context_is_distinct_from_the_platform_context()
  {
    // TenantDbContext must be a SEPARATE context, not PlatformDbContext renamed or subclassed. Both derive
    // from the shared persistence base so the tenant guard and audit rules are one implementation, but
    // neither may derive from the other — that would make the two planes one model again.
    Assert.NotEqual(typeof(TenantDbContext), typeof(PlatformDbContext));
    Assert.False(typeof(PlatformDbContext).IsAssignableFrom(typeof(TenantDbContext)));
    Assert.False(typeof(TenantDbContext).IsAssignableFrom(typeof(PlatformDbContext)));
    Assert.True(typeof(PersistenceDbContext).IsAssignableFrom(typeof(TenantDbContext)));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_two_contexts_own_disjoint_entities()
  {
    // The boundary is asserted on the built EF models rather than on file locations, so moving a
    // configuration file cannot silently move an entity across the plane boundary.
    var tenantEntities = TenantModelEntities();
    var platformEntities = PlatformModelEntities();

    Assert.Contains(typeof(Company), tenantEntities);
    Assert.DoesNotContain(typeof(Company), platformEntities);
    Assert.Empty(tenantEntities.Intersect(platformEntities));

    // Platform authority and identity data must never appear in a tenant database: it is what keeps
    // authentication independent of tenant-storage availability (ADR-017 platform database boundary).
    foreach (var platformOnly in new[]
      {
        typeof(Tenant), typeof(TenantUser), typeof(Role), typeof(PlatformSupportPrincipal),
        typeof(TenantDatabase), typeof(TenantDatabaseAssignment)
      })
    {
      Assert.DoesNotContain(platformOnly, tenantEntities);
      Assert.Contains(platformOnly, platformEntities);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Tenant_entities_are_tenant_owned_and_retain_their_tenant_id()
  {
    // Every entity in the tenant model must be tenant-owned, so the global filter and write guard apply to
    // all of it. TenantId is retained even in a dedicated database (ADR-017 "TenantId retention").
    foreach (var entity in TenantModelEntities())
    {
      Assert.Contains(typeof(ITenantOwnedEntity), entity.GetInterfaces());
    }

    var company = TenantModel().FindEntityType(typeof(Company));
    Assert.NotNull(company);
    Assert.NotNull(company!.FindProperty(nameof(ITenantOwnedEntity.TenantId)));
    Assert.NotNull(company.GetQueryFilter());
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_tenant_model_has_no_cross_database_relationship_to_the_platform_plane()
  {
    // A foreign key or navigation from Company to Tenant would be a cross-database reference the moment a
    // tenant is moved to a dedicated catalog — prohibited, and impossible to satisfy.
    var company = TenantModel().FindEntityType(typeof(Company));
    Assert.NotNull(company);
    Assert.Empty(company!.GetForeignKeys());
    Assert.Empty(company.GetNavigations());
    Assert.DoesNotContain(TenantModelEntities(), entity => entity == typeof(Tenant));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_tenant_model_is_tenant_invariant()
  {
    // Two models built for different tenants must be identical in shape. EF caches the model per options,
    // so any tenant-conditional configuration would let one tenant's model serve another (ADR-017 rule 3).
    static string[] Describe(Guid tenantId) =>
      [.. BuildTenantContext(tenantId).Model.GetEntityTypes()
        .SelectMany(entity => entity.GetProperties()
          .Select(property => $"{entity.ClrType.Name}.{property.Name}:{property.GetColumnName()}"))
        .OrderBy(value => value, StringComparer.Ordinal)];

    Assert.Equal(Describe(Guid.NewGuid()), Describe(Guid.NewGuid()));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_tenant_context_is_never_pooled_and_never_captures_a_connection_at_registration()
  {
    // Pooled contexts could carry connection identity across tenants (ADR-017 rule 4), and options built
    // once at registration would pin every tenant to the first route resolved (rule 2). The factory is
    // therefore the only construction path, and it takes the tenant per call.
    var registration = File.ReadAllText(Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence",
      "PlatformPersistenceServiceCollectionExtensions.cs"));

    Assert.DoesNotContain("AddDbContextPool<TenantDbContext>", registration, StringComparison.Ordinal);
    Assert.DoesNotContain("PooledDbContextFactory", registration, StringComparison.Ordinal);
    Assert.DoesNotContain("AddDbContext<TenantDbContext>", registration, StringComparison.Ordinal);

    var create = typeof(ITenantDbContextFactory).GetMethod(nameof(ITenantDbContextFactory.CreateAsync));
    Assert.NotNull(create);
    Assert.Contains(create!.GetParameters(), parameter => parameter.ParameterType == typeof(Guid));
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void The_tenant_migration_stream_is_separate_from_the_platform_stream()
  {
    // Separate history table AND separate schema, so neither stream's applied migrations can be read as
    // the other's. Migration files live under the tenant folder for the same reason.
    Assert.Equal(TenantPersistenceConstants.Schema, TenantPersistenceConstants.MigrationHistorySchema);
    Assert.NotEqual(PlatformPersistenceConstants.Schema, TenantPersistenceConstants.MigrationHistorySchema);

    var tenantMigrations = InfrastructureAssembly.GetTypes()
      .Where(type => typeof(Migration).IsAssignableFrom(type) && !type.IsAbstract)
      .Where(type => type.Namespace?.Contains("TenantErp", StringComparison.Ordinal) == true)
      .ToArray();
    Assert.NotEmpty(tenantMigrations);

    // No migration type is shared between the streams.
    var platformMigrations = InfrastructureAssembly.GetTypes()
      .Where(type => typeof(Migration).IsAssignableFrom(type) && !type.IsAbstract)
      .Where(type => type.Namespace?.Contains("TenantErp", StringComparison.Ordinal) != true)
      .ToArray();
    Assert.NotEmpty(platformMigrations);
    Assert.Empty(tenantMigrations.Intersect(platformMigrations));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Tenant_context_creation_cannot_bypass_the_routed_connection_factory()
  {
    // The factory composes resolver + connection factory. If it built provider options directly it could
    // reach a database the connection factory would have refused — CustomerManaged, or an unconfigured
    // ServerKey — so the dependency on ITenantDatabaseConnectionFactory is itself the guard.
    var dependencies = typeof(TenantDbContextFactory).GetConstructors()
      .SelectMany(constructor => constructor.GetParameters())
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.Contains(typeof(ITenantDatabaseResolver), dependencies);
    Assert.Contains(typeof(ITenantDatabaseConnectionFactory), dependencies);
    Assert.DoesNotContain(typeof(PlatformDbContext), dependencies);
  }

  private static IModel TenantModel() => BuildTenantContext(Guid.NewGuid()).Model;

  private static Type[] TenantModelEntities() =>
    [.. TenantModel().GetEntityTypes().Select(entity => entity.ClrType)];

  private static Type[] PlatformModelEntities()
  {
    var options = new DbContextOptionsBuilder<PlatformDbContext>()
      .UseSqlServer("Server=architecture-test;Database=model-only;Integrated Security=True")
      .Options;
    using var context = new PlatformDbContext(options, new ModelUser(), new ModelTenant(null), new ModelClock());
    return [.. context.Model.GetEntityTypes().Select(entity => entity.ClrType)];
  }

  // Model construction only — no connection is ever opened.
  private static TenantDbContext BuildTenantContext(Guid tenantId)
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=architecture-test;Database=model-only;Integrated Security=True")
      .Options;
    return new TenantDbContext(options, new ModelUser(), new ModelTenant(tenantId), new ModelClock());
  }

  private sealed class ModelUser : ICurrentUser
  {
    public string? UserId => "architecture-tests";

    public string? UserName => null;

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class ModelTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId => tenantId;
  }

  private sealed class ModelClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => new(2026, 8, 14, 11, 0, 0, TimeSpan.Zero);
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
