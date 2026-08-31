using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.TestSupport.CutoverModel;

namespace SSAS.Architecture.Tests;

// ================================================================================================
// THE POST-CREATION TENANT GUARD, OVER THE WHOLE SET RATHER THAN THREE HAND-PICKED TYPES (item 230).
// ================================================================================================
//
// ---- ⚠ WHY A SET-DRIVEN TEST AND NOT A GRID.
//
// `ITenantOwnedEntity` is declared by 42 types. Three were asserted by name — `Company`, `TenantUser`,
// `Employee` — and item 228 added `DepartmentManager` because **all three were AGGREGATE ROOTS**, so a
// guard that walked only roots would have passed every one of them.
//
// **Per-type tests cannot close that.** Each proves one `if` statement again, and **none of them covers
// the type somebody adds tomorrow.** This walks the types the EF model already enumerates, so a new
// tenant-owned entity is covered on the day it joins the model and nobody has to remember.
//
// ---- ⚠⚠ WHY IT RUNS AGAINST `PersistenceDbContext` AND NOT `TenantDbContext`.
//
// **The guard lives in `PersistenceDbContext.ApplyPersistenceRules`,** and `TenantDbContext` runs
// `PreventCompanyDeletion`, `PreventAppendOnlyMutation` and `ApplyCompanyRulesAsync` **before** it. Most
// of these types are also `ICompanyOwnedEntity` or `IAppendOnlyEntity`, so through `TenantDbContext` an
// earlier boundary refuses first and **the tenant guard is never reached** — the test would go green on
// the wrong refusal, which is the trap item 228 hit on its first run.
//
// So this exercises the rule where the rule is written, over the composed tenant model.
//
// ---- NO DATABASE. `CutoverTenantModel.Contributors` builds the model from an unusable connection
// string, and `ApplyPersistenceRules` runs BEFORE `base.SaveChangesAsync` reaches SQL. A refusal is
// therefore proof the guard fired, and anything else means it did not — a type the guard skipped reaches
// the connection and fails with `SqlException`, which is how the plant for this test reports itself.
//
// ---- ⚠ WHAT IT COVERS, MEASURED RATHER THAN ASSUMED: 35 OF THE 42 DECLARING TYPES.
//
// **The composed TENANT model only.** The other seven live in Platform's own context — `Role`,
// `RolePermissionAssignment`, `TenantUser`, `TenantUserRoleAssignment` and the three localization types —
// which is a separate model this does not build, the same bound `ConstructorKeyedEntityModelTests` states
// for itself.
//
// ⚠ **`TenantUser` is therefore NOT covered here** and keeps its own named test. **Saying which seven
// is the point: 35 reads like 42 unless the gap is named.**
public sealed class TenantOwnershipGuardCoverageTests
{
  [Fact]
  [Trait("Criterion", "AC-EMP-0002")]
  public async Task Every_tenant_owned_entity_is_refused_a_post_creation_tenant_change()
  {
    await using var context = ModelOnlyContext();

    var types = context.Model.GetEntityTypes()
      .Where(entity => !entity.IsOwned())
      .Select(entity => entity.ClrType)
      .Where(type => type is not null && typeof(ITenantOwnedEntity).IsAssignableFrom(type))
      .Distinct()
      .OrderBy(type => type!.Name, StringComparer.Ordinal)
      .ToArray();

    // ⚠ THE ANTI-VACUITY FLOOR. The selection is four links long — built model, not owned, has a CLR
    // type, implements the interface — and the offender list below is empty if ANY of them stops
    // matching. Without this the test passes loudest when it is judging nothing.
    Assert.True(types.Length >= 30,
      $"only {types.Length} tenant-owned entity types were found in the composed model; the selection " +
      "chain has stopped matching and the check below would judge nothing.");

    var unguarded = new List<string>();

    foreach (var type in types)
    {
      // No constructor is called: these aggregates assign keys and invariants in factories with
      // different signatures, and the guard cares about a tracked entry, not a valid aggregate.
      var entity = (ITenantOwnedEntity)RuntimeHelpers.GetUninitializedObject(type!);

      var entry = context.Entry(entity);
      entry.State = EntityState.Unchanged;
      entity.TenantId = Guid.NewGuid();
      entry.Property(nameof(ITenantOwnedEntity.TenantId)).IsModified = true;

      var refusal = await Record.ExceptionAsync(() => context.SaveChangesAsync());

      // ⚠ THE MESSAGE, NOT THE THROW. A different refusal — or a connection failure from the unusable
      // string — means the tenant guard did NOT fire and something else did, which is exactly the
      // false green this test exists to avoid.
      if (refusal is not InvalidOperationException invalid ||
        !invalid.Message.Contains("Tenant ownership cannot be changed", StringComparison.Ordinal))
      {
        unguarded.Add($"{type!.Name} -> {refusal?.GetType().Name ?? "no refusal"}");
      }

      context.ChangeTracker.Clear();
    }

    Assert.True(
      unguarded.Count == 0,
      "These tenant-owned entities were not refused a post-creation TenantId change by " +
      "PersistenceDbContext.ApplyPersistenceRules. The guard iterates " +
      "ChangeTracker.Entries<ITenantOwnedEntity>(), so membership is decided by the type system and the " +
      "only way to lose a type is a hand-written exclusion: " + string.Join(", ", unguarded));
  }

  private static ModelOnlyPersistenceContext ModelOnlyContext()
  {
    var options = new DbContextOptionsBuilder<ModelOnlyPersistenceContext>()
      .UseSqlServer("Server=unused;Database=model-only;Trusted_Connection=True;TrustServerCertificate=True")
      .Options;

    return new ModelOnlyPersistenceContext(
      options, new ModelOnlyUser(), new ModelOnlyTenant(), new ModelOnlyClock());
  }

  // The composed tenant model on the base context, so the rule under test is the only one on the path.
  private sealed class ModelOnlyPersistenceContext(
    DbContextOptions options,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IDateTimeProvider dateTimeProvider)
    : PersistenceDbContext(options, currentUser, currentTenant, dateTimeProvider)
  {
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      // The same two steps `TenantDbContext.OnModelCreating` takes: Platform's own tenant-resident
      // entities are configured from that assembly, the modules' from their contributors. Without the
      // first, `Branch` reaches the model with no key and the model never builds.
      modelBuilder.ApplyConfigurationsFromAssembly(
        typeof(TenantDbContext).Assembly,
        type => type.Namespace == TenantPersistenceConstants.ConfigurationNamespace);

      foreach (var contributor in CutoverTenantModel.Contributors)
      {
        contributor.Configure(modelBuilder);
      }

      base.OnModelCreating(modelBuilder);
    }
  }

  private sealed class ModelOnlyUser : ICurrentUser
  {
    public string? UserId => "model-only";

    public string? UserName => "model-only";

    public string? Email => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class ModelOnlyTenant : ICurrentTenant
  {
    public Guid? TenantId => Guid.Parse("11111111-1111-1111-1111-111111111111");
  }

  private sealed class ModelOnlyClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
  }
}
