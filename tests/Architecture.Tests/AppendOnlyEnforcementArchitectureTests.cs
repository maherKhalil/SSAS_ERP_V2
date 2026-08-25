using System.Reflection;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Architecture.Tests;

// APPEND-ONLY IS ENFORCED BY EVERY WRITE BOUNDARY, NOT BY WHICHEVER ONE SOMEBODY REMEMBERED (FP-014).
//
// ---- THE ASYMMETRY THESE GUARDS EXIST BECAUSE OF.
//
// `TenantDbContext` refused Modified and Deleted for `IAppendOnlyEntity` from FP-006C3 onward.
// `PlatformDbContext` did not, and nothing noticed for four modules — because nothing asserted the
// symmetry. It surfaced only when FP-014 ruled the subscription history append-only (`OD-SUB-0008`) and
// `ADR-017` placed that history in the Platform database, at which point the ruling turned out to rest on
// a mechanism absent from the side of the product where the data lives.
//
// **A marker interface whose enforcement is optional per context is not a rule, it is a suggestion.** The
// classification is declared once in `BuildingBlocks`, so it has to bind everywhere it can be persisted.
//
// ---- WHAT THIS ASSERTS, AND WHY IT IS SHAPE RATHER THAN BEHAVIOUR.
//
// Behaviour is asserted where behaviour can be exercised — `PlatformAppendOnlyGuardTests` writes a record
// and then mutates it, and fails if the guard is removed. Reflection cannot read a method body, so what it
// can protect is the thing that actually regressed: a context that simply does not have the rule at all.
// The next persistence context added to this repository fails here until it does.
public sealed class AppendOnlyEnforcementArchitectureTests
{
  private const string GuardMethodName = "PreventAppendOnlyMutation";

  private static readonly Assembly PlatformInfrastructure = typeof(PlatformDbContext).Assembly;

  // ---- BOTH CONTEXTS, NAMED EXPLICITLY.
  //
  // The discovery test below would pass vacuously if the type scan ever returned nothing — a renamed
  // namespace, a moved assembly, a reflection change. Naming the two contexts that exist today means the
  // asymmetry cannot come back silently even if discovery breaks.
  [Fact]
  public void Both_persistence_contexts_declare_the_append_only_guard()
  {
    Assert.NotNull(FindGuard(typeof(PlatformDbContext)));
    Assert.NotNull(FindGuard(typeof(TenantDbContext)));
  }

  // ---- AND EVERY CONTEXT, DISCOVERED.
  //
  // This is the half that binds the NEXT one. A third persistence context — a reporting store, a read
  // replica, whatever arrives — inherits `PersistenceDbContext` and therefore inherits the ability to
  // persist an `IAppendOnlyEntity`. If it does not carry the rule, it fails here on the day it is written
  // rather than on the day a record is quietly rewritten.
  [Fact]
  public void Every_persistence_context_declares_the_append_only_guard()
  {
    var contexts = PlatformInfrastructure.GetTypes()
      .Where(type => type is { IsClass: true, IsAbstract: false })
      .Where(type => typeof(PersistenceDbContext).IsAssignableFrom(type))
      .ToList();

    // The scan must actually find something, or this test asserts nothing at all.
    Assert.True(
      contexts.Count >= 2,
      $"Expected at least the two known persistence contexts, found {contexts.Count}. " +
      "If a context moved assembly, this guard has stopped protecting it.");

    var unguarded = contexts
      .Where(type => FindGuard(type) is null)
      .Select(type => type.Name)
      .ToList();

    Assert.True(
      unguarded.Count == 0,
      "Every persistence context must refuse Modified and Deleted for IAppendOnlyEntity. " +
      $"Missing {GuardMethodName}: {string.Join(", ", unguarded)}. " +
      "IAppendOnlyEntity without the guard is the appearance of immutability and none of it.");
  }

  // ---- THE GUARD MUST SIT WHERE IT CANNOT BE BYPASSED.
  //
  // EF Core routes `SaveChangesAsync(ct)` to `SaveChangesAsync(bool, ct)` and `SaveChanges()` to
  // `SaveChanges(bool)` by virtual dispatch, so a rule hung on a convenience overload alone leaves the
  // inner one able to commit straight past it. `PersistenceDbContext` states that lesson in its own
  // comment, having previously been on the wrong side of it.
  //
  // Each context must therefore fence both innermost overloads — by overriding them, or, as
  // `TenantDbContext` does for `SaveChanges(bool)`, by refusing that path outright.
  [Fact]
  public void Every_persistence_context_fences_the_innermost_save_overloads()
  {
    var contexts = PlatformInfrastructure.GetTypes()
      .Where(type => type is { IsClass: true, IsAbstract: false })
      .Where(type => typeof(PersistenceDbContext).IsAssignableFrom(type))
      .ToList();

    var unfenced = contexts
      .Where(type => !DeclaresInnerAsyncSave(type) || !DeclaresInnerSyncSave(type))
      .Select(type => type.Name)
      .ToList();

    Assert.True(
      unfenced.Count == 0,
      "A persistence context must override SaveChangesAsync(bool, CancellationToken) and " +
      "SaveChanges(bool) — the overloads EF Core routes every entry point through. Guarding only a " +
      $"convenience overload leaves the inner one reachable. Unfenced: {string.Join(", ", unfenced)}.");
  }

  // ---- AND NO RULE MAY SIT SOMEWHERE REACHABLE PAST. This is the strengthening T-015 called for.
  //
  // "The context fences both innermost overloads" was too weak, and the gap was live: `PlatformDbContext`
  // satisfied it while seven of its own guards hung on an override of `SaveChangesAsync(CancellationToken)`
  // that a caller naming the inner overload went straight round. A context can fence the right methods and
  // still keep its rules on the wrong one.
  //
  // ---- WHAT IS ASSERTED, AND WHY IT IS THE ABSENCE RATHER THAN THE PRESENCE.
  //
  // "Every guard sits at the fence" is not directly expressible by reflection — a method body is not
  // readable, so no test can see WHICH method calls a guard. What IS expressible is the only place a rule
  // could hide: **a convenience-overload override.** `SaveChangesAsync(ct)` and `SaveChanges()` exist to
  // delegate inward, so a context that declares neither has nowhere to put a bypassable rule.
  //
  // That turns an unprovable claim about method bodies into a provable one about method surface. It is
  // strictly stronger than what it replaces, and it is the assertion that would have failed on
  // `PlatformDbContext` before T-015 and passes after it.
  [Fact]
  public void No_persistence_context_declares_a_convenience_save_overload()
  {
    var contexts = PlatformInfrastructure.GetTypes()
      .Where(type => type is { IsClass: true, IsAbstract: false })
      .Where(type => typeof(PersistenceDbContext).IsAssignableFrom(type))
      .ToList();

    Assert.True(contexts.Count >= 2, $"Expected at least two persistence contexts, found {contexts.Count}.");

    var offenders = contexts
      .Where(type => DeclaresConvenienceAsyncSave(type) || DeclaresConvenienceSyncSave(type))
      .Select(type => type.Name)
      .ToList();

    Assert.True(
      offenders.Count == 0,
      "A persistence context must not override SaveChangesAsync(CancellationToken) or SaveChanges(). " +
      "EF Core routes both inward by virtual dispatch, so any rule placed on them is reachable past by a " +
      "caller who names the inner overload — which is exactly how seven PlatformDbContext guards were " +
      $"bypassable until T-015. Put write rules on the innermost overloads. Offenders: {string.Join(", ", offenders)}.");
  }

  private static MethodInfo? FindGuard(Type contextType) =>
    contextType.GetMethod(
      GuardMethodName,
      BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);

  private static bool DeclaresInnerAsyncSave(Type contextType) =>
    contextType.GetMethod(
      nameof(PersistenceDbContext.SaveChangesAsync),
      BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
      binder: null,
      [typeof(bool), typeof(CancellationToken)],
      modifiers: null) is not null;

  private static bool DeclaresInnerSyncSave(Type contextType) =>
    contextType.GetMethod(
      nameof(PersistenceDbContext.SaveChanges),
      BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
      binder: null,
      [typeof(bool)],
      modifiers: null) is not null;

  private static bool DeclaresConvenienceAsyncSave(Type contextType) =>
    contextType.GetMethod(
      nameof(PersistenceDbContext.SaveChangesAsync),
      BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
      binder: null,
      [typeof(CancellationToken)],
      modifiers: null) is not null;

  private static bool DeclaresConvenienceSyncSave(Type contextType) =>
    contextType.GetMethod(
      nameof(PersistenceDbContext.SaveChanges),
      BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
      binder: null,
      Type.EmptyTypes,
      modifiers: null) is not null;
}
