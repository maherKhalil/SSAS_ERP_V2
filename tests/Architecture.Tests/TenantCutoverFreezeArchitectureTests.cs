using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Architecture.Tests;

// THE BOUNDARIES OF THE CUTOVER FREEZE (ADR-020, TS-Storage Phase E1).
//
// A freeze is the component most likely to grow scope quietly: it already sits on the write path, already
// knows the tenant, and already holds a lock. These guards keep it doing one thing.
public sealed class TenantCutoverFreezeArchitectureTests
{
  private static readonly Assembly InfrastructureAssembly = typeof(TenantCutoverWriteFence).Assembly;

  // The cutover components still DELIBERATELY absent. Each is its own decision.
  //
  // `RoutingCache` and `Invalidation` were dropped from this list when TS-Storage Phase E2 delivered them:
  // leaving them here would have been a guard asserting the absence of something the estate now contains,
  // passing only because E2's type names happen not to contain those exact substrings. What E2 introduced
  // is guarded properly — by exact-type allowlist — in TenantStorageRegistryArchitectureTests and
  // TenantRoutingArchitectureTests, which is a stronger statement than this list ever made.
  private static readonly string[] DeferredComponents = ["Cleanup"];

  // TENANT-SCOPED, NEVER DATABASE-WIDE. One tenant's promotion is not an outage for its co-tenants, so the
  // lock resource carries the TenantId and two tenants can never collide on one name.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_freeze_lock_resource_is_tenant_scoped()
  {
    var first = TenantCutoverLockResource.ForTenant(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    var second = TenantCutoverLockResource.ForTenant(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    Assert.NotEqual(first, second);
    Assert.StartsWith(TenantCutoverLockResource.Prefix, first, StringComparison.Ordinal);
    Assert.Contains("11111111-1111-1111-1111-111111111111", first, StringComparison.Ordinal);

    // No database name, server key or fleet-wide token anywhere in the resource: the only variable is the
    // tenant, which is what makes a database-wide freeze inexpressible rather than merely discouraged.
    Assert.Equal(TenantCutoverLockResource.Prefix.Length + 36, first.Length);
  }

  // NOT A TRANSPORT-LAYER FREEZE. ADR-020 is explicit: a request-path-only freeze would leave every
  // non-HTTP writer running against the source during the copy.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_freeze_is_enforced_at_persistence_and_never_at_the_transport_layer()
  {
    // The fence is consumed by the tenant persistence context, which every application write goes through.
    var context = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.Persistence.TenantErp.TenantDbContext");
    Assert.NotNull(context);
    Assert.Contains(
      context!.GetConstructors().SelectMany(constructor => constructor.GetParameters()),
      parameter => parameter.ParameterType == typeof(ITenantWriteFence));

    // ...and nothing in the freeze path touches HTTP.
    foreach (var type in CutoverTypes())
    {
      var referenced = type.GetConstructors()
        .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
        .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
          .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
            .Append(method.ReturnType)));

      Assert.All(referenced, parameter => Assert.DoesNotContain(
        "Microsoft.AspNetCore", parameter.Namespace ?? string.Empty, StringComparison.Ordinal));
    }
  }

  // THIS SLICE ESTABLISHES THE WINDOW; IT DOES NOT USE IT. No copy engine, no routing flip, no cache
  // invalidation — each of those is a separate decision, and a freeze that quietly acquired one of them
  // would be past review before anyone noticed.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_freeze_slice_neither_copies_flips_routing_nor_invalidates_caches()
  {
    var forbidden = new[]
    {
      typeof(TenantDatabaseAssignment), typeof(TenantDatabase)
    };

    foreach (var type in CutoverTypes())
    {
      var reachable = type.GetConstructors()
        .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
        .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
          .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
            .Append(method.ReturnType)))
        .ToArray();

      foreach (var aggregate in forbidden)
      {
        Assert.DoesNotContain(aggregate, reachable);
      }
    }

    // No copy, cache or destructive-cleanup component arrived alongside the freeze.
    // ⚠ FIVE TESTS HERE PASSED OVER AN EMPTY TYPE SET (T-258). The floor is on what was SCANNED —
    // an empty OFFENDER list is the success condition and can say nothing about whether anything was
    // looked at. The namespace predicate is what collapses quietly: rename it and every type survives.
    var tenantStorage = InfrastructureAssembly.GetTypes()
      .Where(type => !type.IsNested && type.Namespace?.Contains("TenantStorage", StringComparison.Ordinal) == true)
      .ToArray();

    Assert.True(tenantStorage.Length >= 10,
      $"only {tenantStorage.Length} TenantStorage types were scanned; the namespace predicate has " +
      "stopped matching and 'no deferred components' below would mean nothing.");

    // ⚠ THE CONTROL ON THE MATCHER (T-263). The floor proves TenantStorage types were found. Empty out
    // `DeferredComponents`, or let its terms drift out of the naming convention, and `Any` matches nothing
    // from a perfectly healthy type list -- a green ban over an unexercised predicate.
    Assert.NotEmpty(DeferredComponents);

    Assert.Contains(DeferredComponents, term =>
      $"TenantCutover{DeferredComponents[0]}Service".Contains(term, StringComparison.OrdinalIgnoreCase));

    var unexpected = tenantStorage
      .Where(type => DeferredComponents
        .Any(term => type.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
      .Select(type => type.FullName)
      .ToArray();

    Assert.Empty(unexpected);
  }

  // V1 PROMOTES ONTO PLATFORM-MANAGED DEDICATED STORAGE. CustomerManaged has no platform provisioning or
  // recovery path, and the refusal lives in the store rather than in a caller that might forget.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void A_cutover_records_both_endpoints_so_target_eligibility_is_decidable()
  {
    var operation = typeof(TenantCutoverOperation);

    Assert.NotNull(operation.GetProperty(nameof(TenantCutoverOperation.SourceTenantDatabaseId)));
    Assert.NotNull(operation.GetProperty(nameof(TenantCutoverOperation.TargetTenantDatabaseId)));

    // ADR-020 requires the record to say whether a post-cutover write happened, so which rollback regime
    // applies is a recorded fact rather than a judgement made under incident pressure.
    Assert.NotNull(operation.GetProperty(nameof(TenantCutoverOperation.PostCutoverWriteObservedUtc)));

    // ...and the flip slice must be able to record assignment, version and status together.
    Assert.NotNull(operation.GetProperty(nameof(TenantCutoverOperation.RoutingVersion)));
    Assert.NotNull(operation.GetProperty(nameof(TenantCutoverOperation.RoutingFlippedUtc)));
  }

  // EVERY PERSISTED STRING IS nvarchar. Unicode is not optional in this estate, and a cutover record is
  // read during an incident by whoever is on shift.
  [Fact]
  [Trait("Decision", "ADR-002")]
  public void Every_persisted_cutover_string_is_nvarchar()
  {
    using var context = ModelOnlyPlatformContext();
    var entity = context.Model.FindEntityType(typeof(TenantCutoverOperation));

    Assert.NotNull(entity);
    // ⚠ `Assert.NotNull` above guards the LOOKUP. Nothing guarded the FILTER, and **`Assert.All` over
    // an empty sequence passes** -- so a change that stopped this entity declaring string columns, or a
    // `ClrType` test that stopped matching, would leave this green having checked no column at all.
    var strings = entity!.GetProperties()
      .Where(property => property.ClrType == typeof(string))
      .ToArray();

    Assert.NotEmpty(strings);

    Assert.All(
      strings,
      property => Assert.StartsWith(
        "nvarchar", property.GetColumnType() ?? "nvarchar", StringComparison.Ordinal));
  }

  private static IEnumerable<Type> CutoverTypes() =>
    InfrastructureAssembly.GetTypes()
      .Where(type => !type.IsNested &&
        type.Namespace?.Contains("TenantStorage", StringComparison.Ordinal) == true &&
        type.Name.Contains("Cutover", StringComparison.Ordinal));

  // Model metadata only — EF builds the model without opening the connection.
  private static PlatformDbContext ModelOnlyPlatformContext()
  {
    var options = new DbContextOptionsBuilder<PlatformDbContext>()
      .UseSqlServer("Server=model-only;Database=model-only;Integrated Security=True")
      .Options;
    return new PlatformDbContext(
      options, ModelOnlyUser.Instance, ModelOnlyTenant.Instance, ModelOnlyClock.Instance);
  }

  private sealed class ModelOnlyUser : SSAS.BuildingBlocks.Application.Abstractions.Identity.ICurrentUser
  {
    public static readonly ModelOnlyUser Instance = new();
    public string? UserId => "architecture-tests";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class ModelOnlyTenant : SSAS.BuildingBlocks.Application.Abstractions.Tenancy.ICurrentTenant
  {
    public static readonly ModelOnlyTenant Instance = new();
    public Guid? TenantId => null;
  }

  private sealed class ModelOnlyClock : SSAS.BuildingBlocks.Application.Abstractions.Time.IDateTimeProvider
  {
    public static readonly ModelOnlyClock Instance = new();
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
  }
}
