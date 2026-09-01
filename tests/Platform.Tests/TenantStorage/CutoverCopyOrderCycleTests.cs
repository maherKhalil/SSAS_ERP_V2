using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// ==================================================================================================
// THE ERROR THAT JUSTIFIES FIVE DESIGN DECISIONS, ASSERTED RATHER THAN READ (244, ADR-020, ADR-026).
// ==================================================================================================
//
// `CutoverCopyOrderUndecidable` is cited as the REASON for five production decisions:
//
//   DepartmentManager.cs               -- why the manager association is a table, not a column
//   DepartmentManagerConfiguration.cs  -- the same, at the mapping
//   Position.cs                        -- why Position holds no DepartmentId
//   JobGradeConfiguration.cs           -- the grade chain's direction
//   PositionConfiguration.cs           -- the same
//
// Before this file, NOTHING ASSERTED IT. Every occurrence of the identifier in `tests/` was a comment,
// and `Position.cs:30` says the method outright -- *"verified in source for Department's naive manager"*.
// A design decision whose justification has only ever been READ is a decision resting on an argument.
//
// ---- ⚠⚠ AND THE ERROR IS OVERLOADED ACROSS THREE UNRELATED CONDITIONS, WHICH IS WHY THE CONTROL BELOW
// ---- IS NOT OPTIONAL.
//
// `TenantCutoverCopyPlan` returns this same `Error` value from three places:
//
//   :99   the entity has no primary key, or no copyable columns  -- NOT a cycle
//   :111  the entity has no TenantId column                      -- NOT a cycle
//   :153  no table is ready, so the order cannot be settled      -- THE CYCLE
//
// Only `:153` is what the five comments rely on. A test that merely asserts `Build` returned
// `CutoverCopyOrderUndecidable` CANNOT TELL THE THREE APART: they share one `Error`, so the code and the
// message are identical. Asserting on the error alone would prove "something was undecidable" and be
// read as "the cycle was detected".
//
// So the cycle is isolated BY DIFFERENCING instead. The two probes are identical in every respect that
// `:99` and `:111` test -- both have a primary key, copyable columns and a TenantId -- and the ONLY
// difference between the two tests is the second foreign key. The acyclic control proving SUCCESS is
// what makes the cyclic failure attributable to the cycle: if `:99` or `:111` could fire for these
// entities, the control would fail too.
//
// ⚠ `:99` AND `:111` ARE NOW EXERCISED, AND THE SENTENCE THAT SAID OTHERWISE IS REPLACED RATHER THAN
// LEFT TO GO QUIETLY FALSE (261). They no longer share this error value: `:99` returns
// `CutoverTableNotCopyable` and `:111` returns `CutoverTableNotTenantScoped`, each asserted below by its
// own probe model. The old value's MESSAGE — *the tenant model contains a foreign-key cycle* — was FALSE
// at both sites, so an operator was handed a wrong diagnosis rather than a vague one.
//
// ⚠⚠ WHAT IS STILL NOT COVERED, STATED SO THIS FILE IS NOT READ AS HAVING CLOSED MORE THAN IT HAS:
// `:99` fires on `key is null || columns.Count == 0` and only the FIRST disjunct is exercised. A mapped
// table with a key and NO COPYABLE COLUMNS is not constructed here.
//
// ⚠⚠⚠ AND THE DIFFERENCING ARGUMENT ABOVE STAYS, BECAUSE A DISTINCT ERROR VALUE DOES NOT REPLACE A
// MATCHED CONTROL. The acyclic control is what makes the cyclic failure attributable to the cycle rather
// than to a probe that could not be described; distinguishing the values narrows what a failure can mean,
// it does not prove the probes were well formed.
// ---- ⚠⚠ WHY THIS LIVES IN `Platform.Tests` AND NOT IN `Architecture.Tests`. 244, 2026-09-01.
//
// It was written in `Architecture.Tests` first and turned that suite RED — not through any fault of its
// own. `TenantModelEntityCountArchitectureTests` DISCOVERS contributors by enumerating every
// `SSAS.*.dll` in its own output directory and instantiating each `ITenantModelContributor` it finds,
// and `SSAS.Architecture.Tests.dll` MATCHES THAT GLOB. The two probe contributors below were discovered,
// their entities joined the composed model, and a census expecting 36 entities found 38.
//
// So ANY test in that assembly which defines a contributor silently enters the model other tests take a
// census of. That census had been correct only because nobody had ever defined one there -- its
// correctness rested on an absence rather than on its logic.
//
// `TenantCutoverCopyPlan` is Platform infrastructure and its tests already live here, beside
// `TenantCutoverCopyPlanTests` and its own `ProbeContributor`, which has coexisted with that census for
// its whole life because the two assemblies are isolated. This file needed no change to a shared guard;
// it needed to be in the right place. The census defect is real and is recorded separately -- it is not
// fixed here, because fixing it was only ever a way to unblock this file.
public sealed class CutoverCopyOrderCycleTests
{
  // Two tenant-owned probes, deliberately minimal: `ITenantOwnedEntity` is one property, and everything
  // else here exists only to satisfy the checks at :99 and :111 so they cannot be the cause.
  private sealed class CopyCycleProbeLeft : ITenantOwnedEntity
  {
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid RightId { get; set; }
  }

  private sealed class CopyCycleProbeRight : ITenantOwnedEntity
  {
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid LeftId { get; set; }
  }

  // ---- ⚠⚠ TWO CONTRIBUTOR TYPES, NOT ONE TYPE WITH A FLAG, AND THE FIRST VERSION OF THIS FILE GOT IT
  // ---- WRONG IN THE EXACT WAY THE DESIGN GUARDS AGAINST.
  //
  // `TenantModelCacheKeyFactory` keys EF's model cache on the ORDERED SET OF CONTRIBUTOR TYPES -- "not
  // their instances: contributors are required to be deterministic, so two contexts with the same
  // contributor types have the same model by construction". A single `ProbeContributor(bool)` therefore
  // produced ONE signature for TWO different models, and the second test was handed the first's cached
  // model.
  //
  // It failed exactly as that factory's own comment predicts -- SILENT AND ORDER-DEPENDENT. The cyclic
  // test passed when run alone and failed when run after the control, and the symptom read as "the
  // planner did not detect the cycle" rather than "these two models are the same model".
  //
  // `ITenantModelContributor` requires a contributor not to vary its mapping by ambient state. A
  // constructor flag is ambient state. Two types, one mapping each.
  private sealed class AcyclicProbeContributor : ITenantModelContributor
  {
    public void Configure(ModelBuilder modelBuilder) => ConfigureProbes(modelBuilder, closeTheCycle: false);
  }

  private sealed class CyclicProbeContributor : ITenantModelContributor
  {
    public void Configure(ModelBuilder modelBuilder) => ConfigureProbes(modelBuilder, closeTheCycle: true);
  }

  private static void ConfigureProbes(ModelBuilder modelBuilder, bool closeTheCycle)
  {
    modelBuilder.Entity<CopyCycleProbeLeft>(entity =>
    {
      entity.ToTable("CopyCycleProbeLeft", "tenant");
      entity.HasKey(probe => probe.Id);
    });

    modelBuilder.Entity<CopyCycleProbeRight>(entity =>
    {
      entity.ToTable("CopyCycleProbeRight", "tenant");
      entity.HasKey(probe => probe.Id);
    });

    // Right depends on Left. On its own this is an ordinary principal/dependent pair and orders fine.
    modelBuilder.Entity<CopyCycleProbeRight>()
      .HasOne<CopyCycleProbeLeft>()
      .WithMany()
      .HasForeignKey(probe => probe.LeftId);

    // ---- AND THE EDGE THAT CLOSES THE LOOP. This is the shape `Department.ManagerEmployeeId` plus
    // `Employee.DepartmentId` would have had, reduced to its mechanism: a mutual reference in which
    // neither table can be copied first.
    if (closeTheCycle)
    {
      modelBuilder.Entity<CopyCycleProbeLeft>()
        .HasOne<CopyCycleProbeRight>()
        .WithMany()
        .HasForeignKey(probe => probe.RightId);
    }
  }

  private static IModel AcyclicModel() =>
    new ComposedTenantModelSource([new AcyclicProbeContributor()]).Model;

  private static IModel CyclicModel() =>
    new ComposedTenantModelSource([new CyclicProbeContributor()]).Model;

  // ---- THE CONTROL, AND IT IS LOAD-BEARING. See the header: without a passing acyclic case, a failure
  // in the cyclic case is consistent with the probes being undescribable for reasons :99 and :111 test.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void Two_tenant_owned_tables_with_one_foreign_key_produce_a_plan()
  {
    var plan = TenantCutoverCopyPlan.Build(AcyclicModel());

    Assert.True(plan.IsSuccess, plan.IsFailure ? plan.Error.Code : null);

    // ANTI-VACUITY: the plan must actually contain the probes. A plan that silently omitted them would
    // succeed here and would make the cyclic test below prove nothing at all.
    var names = plan.Value.Select(table => table.EntityName).ToArray();
    Assert.Contains(nameof(CopyCycleProbeLeft), names);
    Assert.Contains(nameof(CopyCycleProbeRight), names);

    // And the principal precedes its dependent, which is the ordering the cyclic case cannot produce.
    Assert.True(
      Array.IndexOf(names, nameof(CopyCycleProbeLeft)) < Array.IndexOf(names, nameof(CopyCycleProbeRight)),
      "the principal must be copied before its dependent");
  }

  // ---- THE CLAIM. One added foreign key, nothing else changed.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public void A_foreign_key_cycle_makes_the_copy_order_undecidable()
  {
    var plan = TenantCutoverCopyPlan.Build(CyclicModel());

    Assert.True(plan.IsFailure, "a mutual foreign key must not produce a copy order");
    Assert.Equal(TenantStorageErrors.CutoverCopyOrderUndecidable, plan.Error);
  }

  // ================================================================================================
  // THE OTHER TWO SITES, EACH WITH ITS OWN MODEL (261).
  // ================================================================================================
  //
  // ⚠ ONE CONTRIBUTOR TYPE PER MODEL, for the reason recorded above: `TenantModelCacheKeyFactory` keys
  // EF's model cache on the ORDERED SET OF CONTRIBUTOR TYPES, so two models sharing a contributor type
  // are one model, silently and order-dependently.

  // Keyless: `Describe` needs a primary key to define an ordered walk, and this has none.
  private sealed class KeylessProbe : ITenantOwnedEntity
  {
    public Guid TenantId { get; set; }

    public string Payload { get; set; } = string.Empty;
  }

  // Tenant-owned by CONTRACT but with `TenantId` left unmapped. ⚠ This is the only way `:111` is
  // reachable at all: `Build` already restricts itself to `ITenantOwnedEntity` implementers, so every
  // table reaching that check HAS the property — it fires only when a model declines to map it.
  private sealed class UnmappedTenantProbe : ITenantOwnedEntity
  {
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
  }

  private sealed class KeylessProbeContributor : ITenantModelContributor
  {
    public void Configure(ModelBuilder modelBuilder) =>
      modelBuilder.Entity<KeylessProbe>(entity =>
      {
        entity.HasNoKey();
        entity.ToTable("KeylessProbe", "tenant");
      });
  }

  private sealed class UnmappedTenantProbeContributor : ITenantModelContributor
  {
    public void Configure(ModelBuilder modelBuilder) =>
      modelBuilder.Entity<UnmappedTenantProbe>(entity =>
      {
        entity.ToTable("UnmappedTenantProbe", "tenant");
        entity.HasKey(probe => probe.Id);
        entity.Ignore(probe => probe.TenantId);
      });
  }

  [Fact]
  [Trait("Decision", "ADR-020")]
  public void A_tenant_owned_table_without_a_primary_key_is_not_copyable()
  {
    var model = new ComposedTenantModelSource([new KeylessProbeContributor()]).Model;

    // ANTI-VACUITY, AND IT IS THE WHOLE ATTRIBUTION. If the probe never entered the model, `Build` would
    // fail — or succeed — for reasons that have nothing to do with a missing key, and this test would
    // report the right error for the wrong cause.
    var probe = model.FindEntityType(typeof(KeylessProbe));
    Assert.NotNull(probe);
    Assert.Null(probe!.FindPrimaryKey());
    Assert.NotNull(probe.GetTableName());

    var plan = TenantCutoverCopyPlan.Build(model);

    Assert.True(plan.IsFailure, "a table with no primary key has no deterministic copy order");

    // ⚠ THE POINT OF 261: this is NOT the cycle value. Before the split both answered
    // `CutoverCopyOrderUndecidable`, whose message sent the operator to look for a foreign-key cycle in a
    // model that has exactly one table and no foreign keys at all.
    Assert.Equal(TenantStorageErrors.CutoverTableNotCopyable, plan.Error);
    Assert.NotEqual(TenantStorageErrors.CutoverCopyOrderUndecidable, plan.Error);
  }

  [Fact]
  [Trait("Decision", "ADR-020")]
  public void A_tenant_owned_table_with_no_mapped_tenant_column_is_not_tenant_scoped()
  {
    var model = new ComposedTenantModelSource([new UnmappedTenantProbeContributor()]).Model;

    // ANTI-VACUITY: the probe must be present, keyed, and genuinely missing its tenant column — otherwise
    // the failure below is attributable to something else.
    var probe = model.FindEntityType(typeof(UnmappedTenantProbe));
    Assert.NotNull(probe);
    Assert.NotNull(probe!.FindPrimaryKey());
    Assert.Null(probe.FindProperty(nameof(ITenantOwnedEntity.TenantId)));

    var plan = TenantCutoverCopyPlan.Build(model);

    Assert.True(plan.IsFailure, "a tenant-owned table with no tenant column cannot be filtered to a tenant");
    Assert.Equal(TenantStorageErrors.CutoverTableNotTenantScoped, plan.Error);
    Assert.NotEqual(TenantStorageErrors.CutoverCopyOrderUndecidable, plan.Error);
  }
}
