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
// ⚠ `:99` AND `:111` REMAIN UNEXERCISED. This file asserts one of the three sites. The other two are
// still reachable only by reading, and nobody should read this file as having closed them.
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
}
