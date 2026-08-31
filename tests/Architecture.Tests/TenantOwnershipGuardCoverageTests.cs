using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence;
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
// ---- ⚠ WHAT IT COVERS: BOTH MODELS, AND THE COUNTS ARE MEASURED RATHER THAN ROUNDED (item 231).
//
// The first version walked the composed TENANT model alone — 35 of the 42 declaring types — and named
// the seven it did not reach. **They are all in `PlatformDbContext`**: `Role`,
// `RolePermissionAssignment`, `TenantUser`, `TenantUserRoleAssignment` and the three localization types.
//
// ⚠ **`PlatformDbContext` derives from the SAME `PersistenceDbContext`, so the guard is one method and
// the CLASS-shaped risk was already closed by the 35.** What the second walk adds is narrow: **a
// NAME-shaped exclusion naming one of the seven.**
//
// ---- ⚠⚠ AND THE SECOND WALK FOUND SOMETHING THE FIRST COULD NOT: 4 OF THE 7 ARE NOT GUARDED AT ALL.
//
// **Measured — composed tenant model: 35 types, 35 guarded, 0 key-immutable. Platform model: 7 types,
// 3 guarded, 4 key-immutable.** The four are `Role`, `TenantUser`, `TenantLocalizationOverride` and
// `TenantLocalizationSettings`, and ⚠ **they carry `TenantId` IN A KEY, so the row cannot be re-tenanted
// at all — a guarantee STRONGER than the guard and enforced before `SaveChanges` is reached.**
//
// **So the two highest-consequence rows named when this was queued — `Role` and `TenantUser` — turn out
// not to need the guard**, and the three that do (`RolePermissionAssignment`,
// `TenantUserRoleAssignment`, `TenantLocalizationOverrideVersion`) are asserted here for the first time.
//
// **Two contexts, one loop.** Neither is the production context: both are model-only shells on the
// shared base, for the reason in the section above.
public sealed class TenantOwnershipGuardCoverageTests
{
  [Fact]
  [Trait("Criterion", "AC-EMP-0002")]
  public async Task Every_tenant_owned_entity_in_the_tenant_model_is_refused_a_post_creation_tenant_change()
  {
    await using var context = TenantModelOnly();

    await AssertEveryTypeIsRefusedAsync(context, floor: 30, model: "composed tenant model");
  }

  // ---- ⚠ THE SEVEN THE TENANT MODEL DOES NOT REACH (item 231).
  //
  // `PlatformDbContext` needs no contributors — it configures itself from its own assembly, excluding the
  // tenant namespace — so the second walk is the same loop against a second model shell and cost almost
  // nothing. **A floor of 5 against the seven declaring types: low enough to survive one of them moving
  // to the tenant plane, high enough to fail if the model stops building.**
  [Fact]
  [Trait("Criterion", "AC-EMP-0002")]
  public async Task Every_tenant_owned_entity_in_the_platform_model_is_refused_a_post_creation_tenant_change()
  {
    await using var context = PlatformModelOnly();

    await AssertEveryTypeIsRefusedAsync(context, floor: 5, model: "platform model");
  }

  private static async Task AssertEveryTypeIsRefusedAsync(
    PersistenceDbContext context, int floor, string model)
  {
    var types = context.Model.GetEntityTypes()
      .Where(entity => !entity.IsOwned())
      .Where(entity => entity.ClrType is not null
        && typeof(ITenantOwnedEntity).IsAssignableFrom(entity.ClrType))
      .GroupBy(entity => entity.ClrType!)
      .Select(group => group.First())
      .OrderBy(entity => entity.ClrType!.Name, StringComparer.Ordinal)
      .ToArray();

    // ⚠ THE ANTI-VACUITY FLOOR. The selection is four links long — built model, not owned, has a CLR
    // type, implements the interface — and the offender list below is empty if ANY of them stops
    // matching. Without this the test passes loudest when it is judging nothing.
    Assert.True(types.Length >= floor,
      $"only {types.Length} tenant-owned entity types were found in the {model}; the selection " +
      "chain has stopped matching and the check below would judge nothing.");

    var unguarded = new List<string>();
    var guarded = 0;
    var keyed = 0;

    foreach (var entityType in types)
    {
      var type = entityType.ClrType!;

      // No constructor is called: these aggregates assign keys and invariants in factories with
      // different signatures, and the guard cares about a tracked entry, not a valid aggregate.
      var entity = (ITenantOwnedEntity)RuntimeHelpers.GetUninitializedObject(type);

      // ---- ⚠ KEY PROPERTIES ARE FILLED FROM THE MODEL, NOT FROM PER-TYPE KNOWLEDGE.
      //
      // An uninitialized object leaves reference-typed key properties null, and EF refuses to track an
      // entity whose ALTERNATE key is null — `TenantLocalizationOverride.ResourceKey` is the one that
      // found this. **The properties to fill come from `GetKeys()`, so this stays set-driven: no list of
      // types, no list of property names, and a new key on any entity is filled the day it is declared.**
      FillKeyProperties(entityType, entity);

      var entry = context.Entry(entity);
      entry.State = EntityState.Unchanged;
      entity.TenantId = Guid.NewGuid();

      // ---- ⚠⚠ TWO WAYS TO BE IMMUTABLE, AND THE SECOND IS STRONGER THAN THE GUARD.
      //
      // Some tenant-owned rows carry `TenantId` IN A KEY — `Role` and the role/tenant-user link rows.
      // **EF refuses to mark a key property modified at all**, so for those the change is impossible
      // before `SaveChanges` is ever called and `ApplyPersistenceRules` never sees it.
      //
      // ⚠ **Forcing them through the same branch would have meant asserting a refusal that cannot
      // happen.** They are asserted on the guarantee they actually have: the attempt itself throws.
      //
      // ⚠⚠ **CLASSIFIED BY WHAT HAPPENS, NOT BY A METADATA PREDICTION.** The first version asked
      // `FindPrimaryKey()` and still hit the throw — *part of a key* covers alternate and identifying
      // foreign keys too, so a predicate over the PRIMARY key missed one and the test failed on a
      // property it had already decided was ordinary. **Observing the refusal cannot miss a kind of key
      // nobody thought of.**
      var markRefusal = Record.Exception(() =>
        entry.Property(nameof(ITenantOwnedEntity.TenantId)).IsModified = true);

      if (markRefusal is not null)
      {
        if (markRefusal is InvalidOperationException keyRefusal &&
          keyRefusal.Message.Contains("part of a key", StringComparison.Ordinal))
        {
          keyed++;
        }
        else
        {
          unguarded.Add($"{type.Name} -> marking TenantId modified threw {markRefusal.GetType().Name}");
        }

        context.ChangeTracker.Clear();
        continue;
      }

      var refusal = await Record.ExceptionAsync(() => context.SaveChangesAsync());

      // ⚠ THE MESSAGE, NOT THE THROW. A different refusal — or a connection failure from the unusable
      // string — means the tenant guard did NOT fire and something else did, which is exactly the
      // false green this test exists to avoid.
      if (refusal is not InvalidOperationException invalid ||
        !invalid.Message.Contains("Tenant ownership cannot be changed", StringComparison.Ordinal))
      {
        unguarded.Add($"{type.Name} -> {refusal?.GetType().Name ?? "no refusal"}");
      }
      else
      {
        guarded++;
      }

      context.ChangeTracker.Clear();
    }

    // ⚠ THE PARTITION IS REPORTED, NOT ONLY THE TOTAL. A model whose types all drifted into the
    // key-carrying branch would assert nothing about `ApplyPersistenceRules` while still passing, so the
    // count that reaches the guard is stated on every run.
    Assert.True(guarded + keyed == types.Length,
      $"{model}: {types.Length} types, {guarded} guarded, {keyed} key-immutable — the partition lost one.");

    Assert.True(
      unguarded.Count == 0,
      "These tenant-owned entities were not refused a post-creation TenantId change by " +
      "PersistenceDbContext.ApplyPersistenceRules. The guard iterates " +
      "ChangeTracker.Entries<ITenantOwnedEntity>(), so membership is decided by the type system and the " +
      $"only way to lose a type is a hand-written exclusion. Model: {model}. Offenders: " +
      string.Join(", ", unguarded));
  }

  // Placeholder values by CLR type, for key participants only. Nothing here is asserted on — the values
  // exist so the entity can be TRACKED, which is the precondition for the question this test asks.
  private static void FillKeyProperties(IEntityType entityType, object entity)
  {
    foreach (var property in entityType.GetKeys()
      .SelectMany(key => key.Properties)
      .Select(property => property.PropertyInfo)
      .Where(info => info is not null && info.CanWrite)
      .Distinct())
    {
      if (property!.GetValue(entity) is not null)
      {
        continue;
      }

      var target = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

      // ⚠ `GetUninitializedObject` for reference types, for the same reason it builds the entity itself:
      // `ResourceKey` is a value object with no parameterless constructor, and `Activator` cannot make
      // one. Nothing reads these values, so an uninitialized instance is as good as a constructed one.
      property.SetValue(entity, target == typeof(string)
        ? "model-only"
        : target.IsValueType
          ? Activator.CreateInstance(target)
          : RuntimeHelpers.GetUninitializedObject(target));
    }
  }

  private const string UnusableConnection =
    "Server=unused;Database=model-only;Trusted_Connection=True;TrustServerCertificate=True";

  private static TenantModelOnlyContext TenantModelOnly() =>
    new(
      new DbContextOptionsBuilder<TenantModelOnlyContext>().UseSqlServer(UnusableConnection).Options,
      new ModelOnlyUser(), new ModelOnlyTenant(), new ModelOnlyClock());

  private static PlatformModelOnlyContext PlatformModelOnly() =>
    new(
      new DbContextOptionsBuilder<PlatformModelOnlyContext>().UseSqlServer(UnusableConnection).Options,
      new ModelOnlyUser(), new ModelOnlyTenant(), new ModelOnlyClock());

  // The platform model on the base context. `PlatformDbContext` is sealed and runs eight write rules of
  // its own before `base.SaveChangesAsync`, so this mirrors its ONE model step and nothing else — the
  // same reason the tenant shell exists.
  private sealed class PlatformModelOnlyContext(
    DbContextOptions options,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IDateTimeProvider dateTimeProvider)
    : PersistenceDbContext(options, currentUser, currentTenant, dateTimeProvider)
  {
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      modelBuilder.ApplyConfigurationsFromAssembly(
        typeof(PlatformDbContext).Assembly,
        type => type.Namespace != TenantPersistenceConstants.ConfigurationNamespace);

      base.OnModelCreating(modelBuilder);
    }
  }

  // The composed tenant model on the base context, so the rule under test is the only one on the path.
  private sealed class TenantModelOnlyContext(
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
