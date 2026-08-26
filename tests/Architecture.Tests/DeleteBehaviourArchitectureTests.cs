using Xunit.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;

using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// DELETE BEHAVIOUR: REFERENCES RESTRICT, OWNERSHIP DOES NOT CARRY ONE AT ALL (T-047).
// ==================================================================================================
//
// `PersistenceDbContext.OnModelCreating` forces `Restrict` onto every foreign key **except ownership
// keys**. This asserts both halves as a **property of the model**, so the rule survives the next person
// who edits that loop.
//
// ---- WHY THE EXEMPTION EXISTS, IN ONE PARAGRAPH.
//
// The migrations snapshot format **serialises no delete behaviour for an owned relationship** — ownership
// implies `Cascade` on rehydration. A model that says `Restrict` therefore disagrees with its own
// snapshot **permanently and unfixably**, and the differ is right to report it: every migration
// scaffolded in this repository carried six spurious foreign-key operations, and it cost two wrong
// diagnoses (T-041, T-043) before anyone read the loop.
//
// ---- WHY THIS IS A GUARD RATHER THAN A COMMENT.
//
// The defect was invisible in code review for months and looked like plausible foreign-key work in every
// migration it produced. **The next edit to that loop will be a one-line change to a `Where` clause**, and
// nothing else in the repository would notice.
public sealed class DeleteBehaviourArchitectureTests(ITestOutputHelper output)
{
  // Model building needs options and services, not a connection: `.Model` is built without ever opening
  // one, which is why this belongs in Architecture.Tests rather than Integration.
  private static PlatformDbContext PlatformContext() =>
    new(new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer("Server=architecture-tests;Database=none")
        .Options,
      new NoUser(), new NoTenant(), new FixedClock());

  private static IReadOnlyList<IForeignKey> ForeignKeys(IModel model) =>
    [.. model.GetEntityTypes().SelectMany(entityType => entityType.GetForeignKeys())];

  // ==================================================================================================
  // 1. NO OWNERSHIP FOREIGN KEY CARRIES `Restrict`.
  // ==================================================================================================
  [Fact]
  public void Ownership_foreign_keys_keep_the_conventional_cascade()
  {
    using var context = PlatformContext();
    var ownership = ForeignKeys(context.Model).Where(key => key.IsOwnership).ToList();

    output.WriteLine($"ownership foreign keys: {ownership.Count}");
    foreach (var key in ownership)
    {
      output.WriteLine(
        $"  {key.DeclaringEntityType.DisplayName()} -> " +
        $"{key.PrincipalEntityType.DisplayName()} : {key.DeleteBehavior}");
    }

    // The tripwire, and it is not decoration: if the model ever stops reporting owned relationships —
    // a configuration change, a table-splitting decision — this test would pass while asserting nothing.
    Assert.NotEmpty(ownership);

    var offenders = ownership
      .Where(key => key.DeleteBehavior != DeleteBehavior.Cascade)
      .Select(key => $"{key.DeclaringEntityType.DisplayName()} ({key.DeleteBehavior})")
      .ToList();

    Assert.True(
      offenders.Count == 0,
      "An ownership foreign key must keep EF's conventional Cascade. The migrations snapshot cannot " +
      "serialise a delete behaviour for an owned relationship, so any other value makes the model " +
      "disagree with its own snapshot permanently — six spurious operations in every migration " +
      $"scaffolded thereafter. Offenders: {string.Join(", ", offenders)}.");
  }

  // ==================================================================================================
  // 2. AND THE LOOP STILL DOES ITS REAL WORK ON EVERY REFERENCE.
  // ==================================================================================================
  //
  // The exemption must not be read as a relaxation. `Employee` -> `Department`, `TenantSubscription` ->
  // `SubscriptionPlan` and every other cross-aggregate reference still refuses to cascade, which is the
  // thing the loop was written for and the reason it cannot simply be deleted.
  [Fact]
  public void Every_reference_foreign_key_still_restricts()
  {
    using var context = PlatformContext();
    var references = ForeignKeys(context.Model).Where(key => !key.IsOwnership).ToList();

    output.WriteLine($"reference foreign keys: {references.Count}");

    Assert.NotEmpty(references);

    var offenders = references
      .Where(key => key.DeleteBehavior != DeleteBehavior.Restrict)
      .Select(key =>
        $"{key.DeclaringEntityType.DisplayName()} -> {key.PrincipalEntityType.DisplayName()} " +
        $"({key.DeleteBehavior})")
      .ToList();

    Assert.True(
      offenders.Count == 0,
      "Every reference between aggregates must refuse a cascading delete. This is an ERP of record: a " +
      "row disappearing because something upstream was removed is the failure the archive-rather-than-" +
      $"delete posture exists to prevent. Offenders: {string.Join(", ", offenders)}.");
  }

  // ==================================================================================================
  // 3. THE GUARD FIRES — DEMONSTRATED OUT OF BAND, AND THE FAILED ATTEMPT IS RECORDED WITH IT.
  // ==================================================================================================
  //
  // `DEC-L-016` asks for a guard to be seen failing. **It was**, and here is exactly how:
  //
  //   1. `git stash push -- src/BuildingBlocks/.../PersistenceDbContext.cs`  (removes the exemption)
  //   2. `dotnet test --filter Ownership_foreign_keys_keep`
  //   3. Test 1 FAILS:
  //        `Offenders: PlanLimit (Restrict), PlanModuleGrant (Restrict), PlanPrice (Restrict),
  //         SubscriptionTerm (Restrict).`
  //   4. `git stash pop` — green again.
  //
  // **So test 1 discriminates.** It is green because the exemption holds, not because it cannot fail.
  //
  // ---- WHY THIS IS A RECORDED EXPERIMENT RATHER THAN AN EXECUTING TEST, WHICH IS WEAKER AND IS SAID SO.
  //
  // An in-process version needs a second context carrying the defect. **I built one and it did not
  // reproduce**: a two-entity `PersistenceDbContext` with one owned collection reported `Cascade` on its
  // ownership key whether the pre-T-047 loop was applied or not, so it would have asserted nothing while
  // looking like a demonstration — the exact failure `DEC-L-016` is about, one level up.
  //
  // The difference is in when the mutation survives model finalisation, and it depends on how the owned
  // type was configured. **Rather than tune a probe until it went red — which is fitting a fixture to a
  // desired outcome — the reproduction is recorded and the attempt reported.** A green in-process test
  // that could not fail would have been worse than this paragraph.
  //
  // ---- WHAT DOES EXECUTE, AND WHY IT IS NOT NOTHING.
  //
  // Test 1's `Assert.NotEmpty(ownership)` is the tripwire: the model reports **four** owned relationships
  // — three `OwnsMany` on `SubscriptionPlan` plus `TenantSubscription.Term`, which shares its owner's
  // table and so has no schema foreign key at all. If ownership ever stops being reported, test 1 fails
  // rather than passing vacuously.
  [Fact]
  public void The_owned_relationships_the_guard_covers_are_the_ones_the_model_declares()
  {
    using var context = PlatformContext();

    var ownership = ForeignKeys(context.Model)
      .Where(key => key.IsOwnership)
      .Select(key => key.DeclaringEntityType.ClrType.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToList();

    output.WriteLine("owned relationships: " + string.Join(", ", ownership));

    // Named rather than counted. A count says the scan was not empty; the names say it is the right set,
    // and a new owned type must be looked at rather than absorbed silently.
    Assert.Equal(
      ["PlanLimit", "PlanModuleGrant", "PlanPrice", "SubscriptionTerm"],
      ownership);
  }

  private sealed class NoUser : ICurrentUser
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

  private sealed class NoTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class FixedClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
  }
}
