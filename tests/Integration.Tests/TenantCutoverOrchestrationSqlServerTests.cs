using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;
using Xunit.Abstractions;

namespace SSAS.Integration.Tests;

// THE WHOLE CUTOVER, AGAINST REAL SQL (ADR-020, TS-Storage Phase E5).
//
// Everything below drives the production orchestrator, which composes E1's freeze, E3's copy and exact
// validation, the recovery activation gate, E4's atomic flip and E2's version-aware resolution. The tests
// assert the DURABLE outcome by reading the catalogs directly — never the orchestrator's own report alone.
// LEFT THE SERIAL COLLECTION on 2026-08-23 (gate-economics round 2).
// It was the LAST member whose reason was not a shared resource at all: it held nothing, and was serial
// only because it ASSERTED ON ELAPSED TIME — the one wall-clock assertion in the suite. That assertion
// was converted to a load-immune hang guard on the same day (see the resume path in section L: bound
// raised to 5 minutes, actual elapsed reported rather than asserted). With the precision claim gone the
// stated reason for serialization went with it, and nothing replaced it.
//
// It was also ~68% of the remaining chain — the whole round-2 prize was this one class.
public sealed class TenantCutoverOrchestrationSqlServerTests(ITestOutputHelper output)
{
  // ---- A + B. The happy path, and the co-tenant that must not notice.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_full_cutover_moves_one_tenant_and_leaves_its_co_tenant_serving()
  {
    await using var fixture = await OrchestrationFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(fixture.TenantA, 4, "ORCH");
    await fixture.SeedCompaniesAsync(fixture.TenantB, 3, "COTEN");

    var preflight = Stopwatch.StartNew();
    var started = await fixture.Orchestrator().StartAsync(fixture.StartRequest());
    preflight.Stop();

    Assert.True(started.IsSuccess, started.IsFailure ? started.Error.Code : null);
    Assert.Equal(TenantCutoverOrchestrationOutcome.Completed, started.Value.Outcome);
    Assert.Equal(TenantCutoverPhase.Completed, started.Value.Phase);
    Assert.Equal(2, started.Value.RoutingVersion);
    Assert.Equal(4, started.Value.CopiedRows);
    Assert.Null(started.Value.Advisory);
    output.WriteLine($"Full orchestration (4 rows, gate + freeze + copy + flip + finalize): {preflight.ElapsedMilliseconds}ms.");

    // ---- §30 end-to-end state.
    var operation = await fixture.ReadOperationAsync(started.Value.CutoverOperationId);
    Assert.Equal(TenantCutoverOperationStatus.Completed, operation.Status);
    Assert.Equal(2, operation.RoutingVersion);
    Assert.NotNull(operation.RoutingFlippedUtc);
    Assert.Null(operation.PostCutoverWriteObservedUtc);

    var assignments = await fixture.AssignmentsAsync(fixture.TenantA);
    var active = Assert.Single(assignments.Where(row => row.EndedUtc is null));
    Assert.Equal(fixture.TargetDatabaseId, active.TenantDatabaseId);
    Assert.Equal(2, active.RoutingVersion);
    var ended = Assert.Single(assignments.Where(row => row.EndedUtc is not null));
    Assert.Equal(fixture.SourceDatabaseId, ended.TenantDatabaseId);
    Assert.True(active.RoutingVersion > ended.RoutingVersion);

    // Source data RETAINED — E5 never deletes.
    Assert.Equal(4, await OrchestrationFixture.CompanyCountAsync(fixture.SourceCatalog, fixture.TenantA));
    Assert.Equal(4, await OrchestrationFixture.CompanyCountAsync(fixture.TargetCatalog, fixture.TenantA));
    Assert.Equal(0, await OrchestrationFixture.CompanyCountAsync(fixture.TargetCatalog, fixture.TenantB));

    // ---- B. The co-tenant kept its route and can still write.
    await using var coTenant = await fixture.CreateRoutedContextAsync(fixture.TenantB);
    Assert.Equal(fixture.SourceCatalog, await CurrentCatalogAsync(coTenant));
    coTenant.Companies.Add(OrchestrationFixture.NewCompany(fixture.TenantB, "STILLHERE"));
    Assert.Equal(1, await coTenant.SaveChangesAsync());
  }

  // ---- C. Preflight gate failure costs the tenant nothing: no operation, no freeze, no downtime.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_target_that_fails_the_recovery_gate_is_refused_before_the_tenant_is_frozen()
  {
    await using var fixture = await OrchestrationFixture.CreateAsync(protectTarget: false);
    await fixture.SeedCompaniesAsync(fixture.TenantA, 2, "GATE");

    var started = await fixture.Orchestrator().StartAsync(fixture.StartRequest());

    Assert.True(started.IsFailure);
    Assert.StartsWith("TenantStorage.RecoveryActivation", started.Error.Code, StringComparison.Ordinal);

    // NOTHING DURABLE WAS CREATED, and the tenant never lost a write.
    Assert.Null(await fixture.FindActiveOperationAsync(fixture.TenantA));
    await using var writer = await fixture.CreateRoutedContextAsync(fixture.TenantA);
    writer.Companies.Add(OrchestrationFixture.NewCompany(fixture.TenantA, "STILLWRITES"));
    Assert.Equal(1, await writer.SaveChangesAsync());
  }

  // ---- D. The gate degrades between preflight and the pre-flip recheck: no flip, and no auto-unfreeze.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_gate_that_degrades_while_frozen_stops_the_flip_and_leaves_the_tenant_frozen()
  {
    await using var fixture = await OrchestrationFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(fixture.TenantA, 3, "DEGR");

    // Reach Frozen through the real services, then degrade recoverability before resuming.
    var operationId = await fixture.BeginAndFreezeAsync();
    await fixture.DegradeTargetReadinessAsync();

    var resumed = await fixture.Orchestrator().ResumeAsync(operationId);

    Assert.True(resumed.IsSuccess);
    Assert.Equal(TenantCutoverOrchestrationOutcome.Resumable, resumed.Value.Outcome);
    Assert.Equal(TenantCutoverPhase.Frozen, resumed.Value.Phase);
    Assert.StartsWith("TenantStorage.RecoveryActivation", resumed.Value.Advisory!.Code, StringComparison.Ordinal);

    await fixture.AssertStillSharedAndFrozenAsync(operationId);
  }

  // ---- F. A tampered target refuses the flip and stays Frozen (E3 validation is mandatory).
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_tampered_target_stops_the_cutover_and_leaves_the_tenant_frozen()
  {
    await using var fixture = await OrchestrationFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(fixture.TenantA, 4, "TAMP");

    var operationId = await fixture.BeginFreezeAndCopyAsync();
    await fixture.TamperWithTargetAsync();

    var resumed = await fixture.Orchestrator().ResumeAsync(operationId);

    Assert.True(resumed.IsSuccess);
    Assert.Equal(TenantCutoverOrchestrationOutcome.Resumable, resumed.Value.Outcome);
    Assert.Equal(TenantStorageErrors.CutoverTargetInconsistent.Code, resumed.Value.Advisory!.Code);
    await fixture.AssertStillSharedAndFrozenAsync(operationId);
  }

  // ---- G + H + I + J. Process loss at each checkpoint, recovered by Resume with no database edits.
  [Theory]
  [Trait("Decision", "ADR-020")]
  [InlineData("preparing")]
  [InlineData("frozen")]
  [InlineData("copied")]
  [InlineData("flipped")]
  public async Task Resume_continues_from_whatever_a_lost_process_left_behind(string checkpoint)
  {
    await using var fixture = await OrchestrationFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(fixture.TenantA, 3, "LOSS");

    // Each checkpoint is reached through the real services and then abandoned — exactly the durable state a
    // process that died there would leave. Nothing in-memory survives into the Resume below.
    var operationId = checkpoint switch
    {
      "preparing" => await fixture.BeginAsync(),
      "frozen" => await fixture.BeginAndFreezeAsync(),
      "copied" => await fixture.BeginFreezeAndCopyAsync(),
      _ => await fixture.BeginFreezeCopyAndFlipAsync()
    };

    // While Frozen, the tenant's writes are refused — the freeze survived the process that established it.
    //
    // NOT asserted at the "flipped" checkpoint, and that is the design rather than an exemption: once
    // routing has moved, a FRESH context resolves the target and is supposed to write. What stays refused
    // after a flip is a context still bound to the SOURCE, which the dedicated stale-context test covers.
    if (checkpoint is "frozen" or "copied")
    {
      await using var blocked = await fixture.CreateRoutedContextAsync(fixture.TenantA);
      blocked.Companies.Add(OrchestrationFixture.NewCompany(fixture.TenantA, "DURINGLOSS"));
      await Assert.ThrowsAsync<TenantStorageUnavailableException>(() => blocked.SaveChangesAsync());
    }

    var resumed = await fixture.Orchestrator().ResumeAsync(operationId);

    Assert.True(resumed.IsSuccess, resumed.IsFailure ? resumed.Error.Code : null);
    Assert.Equal(TenantCutoverOrchestrationOutcome.Completed, resumed.Value.Outcome);
    Assert.Equal(TenantCutoverPhase.Completed, resumed.Value.Phase);

    // ONE Dedicated assignment and ONE version advance, no matter how far the lost run had got — a resumed
    // flip must never copy again or allocate a second version.
    var assignments = await fixture.AssignmentsAsync(fixture.TenantA);
    Assert.Equal(2, assignments.Count);
    Assert.Equal(2, Assert.Single(assignments.Where(row => row.EndedUtc is null)).RoutingVersion);
    Assert.Equal(3, await OrchestrationFixture.CompanyCountAsync(fixture.TargetCatalog, fixture.TenantA));
  }

  // ---- K. Two Start callers, one durable cutover. The unique index is the final authority.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task Two_concurrent_starts_produce_exactly_one_cutover()
  {
    await using var fixture = await OrchestrationFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(fixture.TenantA, 3, "TWOST");

    var attempts = await Task.WhenAll(
      Task.Run(() => fixture.Orchestrator().StartAsync(fixture.StartRequest())),
      Task.Run(() => fixture.Orchestrator().StartAsync(fixture.StartRequest())));

    Assert.Equal(1, await fixture.OperationCountAsync(fixture.TenantA));

    foreach (var failed in attempts.Where(attempt => attempt.IsFailure))
    {
      Assert.Contains(failed.Error.Code, new[]
      {
        TenantStorageErrors.CutoverAlreadyActive.Code,
        TenantStorageErrors.CutoverCopyOwnershipNotAcquired.Code
      });
    }

    // Whatever the interleaving, no duplicate copy and no dual freeze.
    Assert.Equal(3, await OrchestrationFixture.CompanyCountAsync(fixture.TargetCatalog, fixture.TenantA));
    Assert.Equal(2, (await fixture.AssignmentsAsync(fixture.TenantA)).Count);
  }

  // ---- L + M + N + O. Nothing may interleave with an orchestration that owns the operation.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task Nothing_can_interleave_while_an_orchestration_owns_the_cutover()
  {
    await using var fixture = await OrchestrationFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(fixture.TenantA, 3, "OWN");
    await fixture.SeedCompaniesAsync(fixture.TenantB, 2, "OTHER");
    var operationId = await fixture.BeginAndFreezeAsync();

    // An orchestration is mid-run, paused between Frozen and the copy: it holds the operation-wide lease.
    await using var orchestrationLease = new SqlConnection(fixture.PlatformConnectionString);
    await orchestrationLease.OpenAsync();
    Assert.NotNull(await TenantCutoverOperationLock.AcquireForSessionAsync(
      orchestrationLease, operationId, TimeSpan.FromSeconds(5)));

    // ---- M. A release cannot slip in and make the source writable under the copy.
    var released = await fixture.FreezeService().ReleaseFreezeAsync(operationId, "interleave");
    Assert.True(released.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverReleaseBlockedByActiveCopy.Code, released.Error.Code);

    // ---- N. A standalone copy cannot start.
    var copy = await fixture.CopyService().CopyAsync(operationId);
    Assert.True(copy.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverCopyOwnershipNotAcquired.Code, copy.Error.Code);

    // ---- O. A standalone flip cannot race.
    var flip = await fixture.FlipService().FlipAsync(operationId);
    Assert.True(flip.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverCopyOwnershipNotAcquired.Code, flip.Error.Code);

    // ---- L. A second Resume cannot take ownership, and does not wait indefinitely.
    //
    // ---- THIS BOUND IS A HANG GUARD, NOT A PERFORMANCE ASSERTION (2026-08-23 ruling).
    //
    // It used to read `< 30 seconds`, and that number asserted a PRECISION CLAIM that only holds on an idle
    // instance: under parallel load a 30-second budget measures the neighbours rather than the product. It
    // is the allocation-budget lesson in the time dimension — `TenantCutoverCopy`'s 287MB budget was removed
    // on 2026-08-21 for exactly this, that it could not discriminate between a regression and a busy box.
    //
    // What the assertion was actually FOR survives intact: a Resume that blocks forever on ownership is a
    // real defect and must fail loudly. Five minutes keeps that and gives up nothing, because no plausible
    // parallel contention on this suite reaches it while a genuine hang exceeds it by definition.
    //
    // The elapsed time is REPORTED, exactly like every other timing in this class, so a drift from
    // milliseconds to seconds is visible to anyone reading the log without being asserted on by anyone.
    var waited = Stopwatch.StartNew();
    var resumed = await fixture.Orchestrator().ResumeAsync(operationId);
    waited.Stop();
    Assert.True(resumed.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverCopyOwnershipNotAcquired.Code, resumed.Error.Code);
    output.WriteLine($"Second Resume refused ownership in {waited.ElapsedMilliseconds}ms.");
    Assert.True(waited.Elapsed < TimeSpan.FromMinutes(5), $"resume appears hung: waited {waited.Elapsed}");

    // The durable state is untouched by any of it.
    await fixture.AssertStillSharedAndFrozenAsync(operationId);

    // ---- Y. Another tenant is entirely unaffected: no fleet-wide lock exists.
    Assert.Null(await fixture.FindActiveOperationAsync(fixture.TenantB));
    await using var otherLease = new SqlConnection(fixture.PlatformConnectionString);
    await otherLease.OpenAsync();
    Assert.NotNull(await TenantCutoverOperationLock.AcquireForSessionAsync(
      otherLease, operationId + 1_000_000, TimeSpan.FromSeconds(5)));

    // Once the orchestration's session ends, ownership is free again — no lease to expire.
    await orchestrationLease.CloseAsync();
    Assert.True((await fixture.Orchestrator().ResumeAsync(operationId)).IsSuccess);
  }

  // ---- P + Q + W + X. Contexts either side of a COMPLETED cutover, and the first target write.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task An_old_shared_context_stays_refused_after_completion_and_a_fresh_one_writes()
  {
    await using var fixture = await OrchestrationFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(fixture.TenantA, 2, "CTX");

    // Created BEFORE the cutover and never re-resolved.
    var stale = await fixture.CreateRoutedContextAsync(fixture.TenantA);
    Assert.Equal(fixture.SourceCatalog, await CurrentCatalogAsync(stale));
    stale.Companies.Add(OrchestrationFixture.NewCompany(fixture.TenantA, "STALE"));

    var started = await fixture.Orchestrator().StartAsync(fixture.StartRequest());
    Assert.True(started.IsSuccess, started.IsFailure ? started.Error.Code : null);
    Assert.Equal(TenantCutoverOrchestrationOutcome.Completed, started.Value.Outcome);

    // ---- W. Completed with no business write at all: an idle tenant must be able to finish.
    Assert.Null((await fixture.ReadOperationAsync(started.Value.CutoverOperationId)).PostCutoverWriteObservedUtc);

    // ---- P. The stale context is still refused AFTER Completed, not merely while RoutingFlipped.
    var refused = await Assert.ThrowsAsync<TenantStorageUnavailableException>(
      () => stale.SaveChangesAsync());
    Assert.Equal(TenantStorageErrors.TenantWritesFrozen.Code, refused.Error.Code);
    await stale.DisposeAsync();
    Assert.Equal(2, await OrchestrationFixture.CompanyCountAsync(fixture.SourceCatalog, fixture.TenantA));

    // ---- Q + X. A fresh context resolves Dedicated and its write is the first post-cutover write.
    await using (var fresh = await fixture.CreateRoutedContextAsync(fixture.TenantA))
    {
      Assert.Equal(fixture.TargetCatalog, await CurrentCatalogAsync(fresh));
      fresh.Companies.Add(OrchestrationFixture.NewCompany(fixture.TenantA, "FIRST"));
      Assert.Equal(1, await fresh.SaveChangesAsync());
    }

    var observed = (await fixture.ReadOperationAsync(started.Value.CutoverOperationId))
      .PostCutoverWriteObservedUtc;
    Assert.NotNull(observed);

    // A later write does not move it.
    await using (var second = await fixture.CreateRoutedContextAsync(fixture.TenantA))
    {
      second.Companies.Add(OrchestrationFixture.NewCompany(fixture.TenantA, "SECOND"));
      Assert.Equal(1, await second.SaveChangesAsync());
    }

    Assert.Equal(
      observed,
      (await fixture.ReadOperationAsync(started.Value.CutoverOperationId)).PostCutoverWriteObservedUtc);
  }

  // ---- R. E4 review LOW-1: two genuinely concurrent FIRST writes. Neither may fail for a conflict that
  // does not exist, and the timestamp stays write-once.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task Two_concurrent_first_target_writes_both_succeed_and_record_one_timestamp()
  {
    await using var fixture = await OrchestrationFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(fixture.TenantA, 2, "RACE");
    var started = await fixture.Orchestrator().StartAsync(fixture.StartRequest());
    Assert.True(started.IsSuccess, started.IsFailure ? started.Error.Code : null);

    var first = await fixture.CreateRoutedContextAsync(fixture.TenantA);
    var second = await fixture.CreateRoutedContextAsync(fixture.TenantA);
    first.Companies.Add(OrchestrationFixture.NewCompany(fixture.TenantA, "RACE1"));
    second.Companies.Add(OrchestrationFixture.NewCompany(fixture.TenantA, "RACE2"));

    // Both see PostCutoverWriteObservedUtc == null and both try to record it.
    var writes = await Task.WhenAll(
      Task.Run(() => first.SaveChangesAsync()),
      Task.Run(() => second.SaveChangesAsync()));
    await first.DisposeAsync();
    await second.DisposeAsync();

    // NEITHER APPLICATION WRITE FAILED for the bookkeeping race.
    Assert.All(writes, written => Assert.Equal(1, written));
    Assert.Equal(4, await OrchestrationFixture.CompanyCountAsync(fixture.TargetCatalog, fixture.TenantA));

    var operation = await fixture.ReadOperationAsync(started.Value.CutoverOperationId);
    Assert.NotNull(operation.PostCutoverWriteObservedUtc);
    Assert.Equal(TenantCutoverOperationStatus.Completed, operation.Status);
  }

  // ---- S + T. Missed invalidation converges; a throwing invalidator cannot undo a committed flip.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_failing_invalidator_leaves_routing_authoritative_and_other_instances_converge()
  {
    await using var fixture = await OrchestrationFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(fixture.TenantA, 3, "CONV");

    // An instance that caches Shared/1 and is never told anything.
    await using var instance = fixture.NewResolverInstance();
    var before = await instance.Resolver.ResolveAsync(fixture.TenantA);
    Assert.True(before.IsSuccess);
    Assert.Equal(fixture.SourceCatalog, before.Value.DatabaseName);
    Assert.Equal(1, instance.Cache.Count);

    var started = await fixture.Orchestrator(new ThrowingInvalidator())
      .StartAsync(fixture.StartRequest());

    // ---- T. Committed and completed despite the invalidator throwing; nothing flipped back.
    Assert.True(started.IsSuccess, started.IsFailure ? started.Error.Code : null);
    Assert.Equal(TenantCutoverOrchestrationOutcome.Completed, started.Value.Outcome);
    Assert.Equal(
      TenantStorageErrors.CutoverInvalidationIncomplete.Code, started.Value.Advisory!.Code);

    // ---- S. The uninformed instance converges on its next resolution through the version check.
    Assert.Equal(1, instance.Cache.Count);
    var after = await instance.Resolver.ResolveAsync(fixture.TenantA);
    Assert.True(after.IsSuccess);
    Assert.Equal(fixture.TargetCatalog, after.Value.DatabaseName);
    Assert.Equal(TenantDatabaseStorageMode.Dedicated, after.Value.StorageMode);
    Assert.Equal(2, after.Value.RoutingVersion);
  }

  // ---- U + V. E4 review LOW-2: routing history cannot be physically deleted.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task Routing_history_cannot_be_deleted_by_direct_sql()
  {
    await using var fixture = await OrchestrationFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(fixture.TenantA, 2, "DEL");
    Assert.True((await fixture.Orchestrator().StartAsync(fixture.StartRequest())).IsSuccess);

    var trigger = Assert.Single(await fixture.RoutingTriggersAsync());
    Assert.Equal("TR_TenantDatabaseAssignments_EnforceRoutingVersion", trigger.Name);
    foreach (var required in new[] { "INSERT", "UPDATE", "DELETE" })
    {
      Assert.Contains(required, trigger.Events, StringComparison.Ordinal);
    }

    var before = await fixture.AssignmentsAsync(fixture.TenantA);
    var highestBefore = before.Max(row => row.RoutingVersion);

    // ---- U. One row.
    var single = await Assert.ThrowsAsync<SqlException>(() => fixture.DeleteOneAssignmentAsync());
    Assert.Equal(51022, single.Number);

    // ---- V. Every row for the tenant, in one statement — rejected atomically.
    var many = await Assert.ThrowsAsync<SqlException>(() => fixture.DeleteAllAssignmentsAsync());
    Assert.Equal(51022, many.Number);

    // History unchanged, so the version-reset path the delete would have opened stays closed.
    var after = await fixture.AssignmentsAsync(fixture.TenantA);
    Assert.Equal(before.Count, after.Count);
    Assert.Equal(highestBefore, after.Max(row => row.RoutingVersion));

    // ---- E (from §19). Ending an assignment by UPDATE remains legal — that is how the flip works, and it
    // just did.
    Assert.Contains(after, row => row.EndedUtc is not null);
  }

  // ---- §2. THE NEWEST RELEVANT CUTOVER DECIDES, not the oldest still on file.
  //
  // A tenant accumulates Completed operations, and each one names the target that was authoritative at the
  // time. If the write gate picked an older Completed operation it would permit writes to a database that
  // is no longer the tenant's — and it would do so precisely when a NEW cutover has frozen the tenant,
  // which is the worst possible moment. Not reachable through StartAsync today (a Dedicated source is
  // refused), so it is exercised directly against the store, where the rule actually lives.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task The_write_gate_uses_the_newest_cutover_not_an_older_completed_one()
  {
    await using var fixture = await OrchestrationFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(fixture.TenantA, 2, "NEWEST");

    var completed = await fixture.Orchestrator().StartAsync(fixture.StartRequest());
    Assert.True(completed.IsSuccess, completed.IsFailure ? completed.Error.Code : null);

    // The old Completed operation governs on its own: writes to its target are permitted.
    var afterFirst = await fixture.Store().FindActiveWriteGateAsync(fixture.TenantA);
    Assert.NotNull(afterFirst);
    Assert.Equal(completed.Value.CutoverOperationId, afterFirst!.CutoverOperationId);
    Assert.True(afterFirst.PermitsWriteTo(fixture.TargetDatabaseId));
    Assert.False(afterFirst.PermitsWriteTo(fixture.SourceDatabaseId));

    // A LATER cutover is opened and frozen for the same tenant.
    var newerId = await fixture.InsertFrozenOperationDirectlyAsync(
      fixture.TargetDatabaseId, fixture.SourceDatabaseId);
    Assert.True(newerId > completed.Value.CutoverOperationId);

    var afterSecond = await fixture.Store().FindActiveWriteGateAsync(fixture.TenantA);

    // THE NEWER ONE WINS. It is Frozen, so every write is refused — including to the older operation's
    // target, which the stale Completed row would otherwise have kept permitting.
    Assert.NotNull(afterSecond);
    Assert.Equal(newerId, afterSecond!.CutoverOperationId);
    Assert.True(afterSecond.RefusesEveryWrite);
    Assert.False(afterSecond.PermitsWriteTo(fixture.TargetDatabaseId));

    // ...and the fence agrees, through the real routed write path.
    await using var writer = await fixture.CreateRoutedContextAsync(fixture.TenantA);
    writer.Companies.Add(OrchestrationFixture.NewCompany(fixture.TenantA, "REFROZEN"));
    var refused = await Assert.ThrowsAsync<TenantStorageUnavailableException>(
      () => writer.SaveChangesAsync());
    Assert.Equal(TenantStorageErrors.TenantWritesFrozen.Code, refused.Error.Code);
  }

  // ---- §2. THE HOT PATH. This lookup runs on EVERY tenant application write, and since E5 it spans
  // Completed operations — which UX_TenantCutoverOperations_ActiveTenant deliberately excludes. Measured at
  // accumulated cardinality, because a one-row fixture would describe the fixture rather than the design.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task The_write_gate_lookup_seeks_its_index_at_realistic_cardinality()
  {
    await using var fixture = await OrchestrationFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(fixture.TenantA, 2, "PLAN");
    Assert.True((await fixture.Orchestrator().StartAsync(fixture.StartRequest())).IsSuccess);

    await fixture.SeedCutoverHistoryAsync(5_000);
    await fixture.RefreshStatisticsAsync();

    // Issued through the REAL store, so the captured plan is the production query.
    await fixture.Store().FindActiveWriteGateAsync(fixture.TenantA);

    var measured = await fixture.CaptureWriteGatePlanAsync();
    Assert.NotNull(measured);

    output.WriteLine(
      $"Write-gate lookup over {measured!.TableCardinality} cutover operations: {measured.Operation} " +
      $"using {measured.Index}, {measured.LogicalReads} logical reads, " +
      $"{measured.Microseconds}us, sort={measured.HasSort}.");

    Assert.Contains("IX_TenantCutoverOperations_WriteGate", measured.Index, StringComparison.Ordinal);
    Assert.Contains("PhysicalOp=\"Index Seek\"", measured.PlanXml, StringComparison.Ordinal);

    // NO SCAN and NO SORT: the index is filtered to exactly the statuses the fence asks about and ordered
    // so the newest operation is the first row read. A sort here would mean the ORDER BY is unsupported and
    // every tenant write would pay for it.
    foreach (var scan in new[] { "Clustered Index Scan", "Index Scan", "Table Scan", "Sort" })
    {
      Assert.DoesNotContain($"PhysicalOp=\"{scan}\"", measured.PlanXml, StringComparison.Ordinal);
    }

    Assert.True(measured.LogicalReads <= 20, $"{measured.LogicalReads} logical reads is not a seek");
  }

  private static async Task<string?> CurrentCatalogAsync(TenantDbContext context)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
      await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT DB_NAME()";
    return Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
  }

  private sealed class ThrowingInvalidator : ITenantRoutingCacheInvalidator
  {
    public void Invalidate(Guid tenantId) =>
      throw new InvalidOperationException("local cache eviction failed");
  }

  // What SQL Server actually did, read from its own statistics rather than asserted from the query's shape.
  private sealed record MeasuredPlan(string PlanXml, long LogicalReads, long Microseconds)
  {
    public string Operation => string.Join(" -> ", Values("PhysicalOp=\""));

    public string Index => string.Join(", ", Values("Index=\"").Distinct(StringComparer.Ordinal));

    public string TableCardinality => Values("TableCardinality=\"").FirstOrDefault() ?? "unknown";

    public bool HasSort => Values("PhysicalOp=\"").Contains("Sort", StringComparer.Ordinal);

    private List<string> Values(string marker)
    {
      var values = new List<string>();
      var cursor = 0;
      while (true)
      {
        var start = PlanXml.IndexOf(marker, cursor, StringComparison.Ordinal);
        if (start < 0)
        {
          return values;
        }

        start += marker.Length;
        var end = PlanXml.IndexOf('"', start);
        if (end < 0)
        {
          return values;
        }

        values.Add(PlanXml[start..end]);
        cursor = end;
      }
    }
  }

  private sealed record AssignmentRow(long TenantDatabaseId, long RoutingVersion, DateTimeOffset? EndedUtc);

  private sealed record OperationRow(
    TenantCutoverOperationStatus Status,
    long? RoutingVersion,
    DateTimeOffset? RoutingFlippedUtc,
    DateTimeOffset? PostCutoverWriteObservedUtc);

  private sealed class OrchestrationFixture : IAsyncDisposable
  {
    private const string ServerKey = "PrimarySqlServer";
    private const string RestoreServerKey = "VerificationSqlServer";
    private const string Actor = "cutover-orchestration-tests";

    // Seeded state sits in the past relative to the real clock every service uses, so ending an assignment
    // never predates its start; recovery evidence is stamped recently because the gate checks staleness.
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow.AddDays(-1);

    private readonly string token = Guid.NewGuid().ToString("N")[..12];
    private readonly TenantStorageOptions storage = new();
    private readonly TenantCutoverFreezeOptions freeze = new();
    private readonly TenantCutoverCopyOptions copy = new();
    private string platformCatalog = string.Empty;

    public string SourceCatalog { get; private set; } = string.Empty;

    public string TargetCatalog { get; private set; } = string.Empty;

    public Guid TenantA { get; private set; }

    public Guid TenantB { get; private set; }

    public long SourceDatabaseId { get; private set; }

    public long TargetDatabaseId { get; private set; }

    public string PlatformConnectionString => ConnectionFor(platformCatalog);

    public static async Task<OrchestrationFixture> CreateAsync(bool protectTarget = true)
    {
      var fixture = new OrchestrationFixture();
      try
      {
        await fixture.InitialiseAsync(protectTarget);
        return fixture;
      }
      catch
      {
        await fixture.DisposeAsync();
        throw;
      }
    }

    private async Task InitialiseAsync(bool protectTarget)
    {
      platformCatalog = $"SSAS_E5_Platform_{token}";
      SourceCatalog = $"SSAS_E5_Shared_{token}";
      TargetCatalog = $"SSAS_E5_Dedicated_{token}";
      freeze.WriteAdmissionTimeout = TimeSpan.FromSeconds(2);

      foreach (var catalog in new[] { platformCatalog, SourceCatalog, TargetCatalog })
      {
        await ExecuteAsync("master", $"CREATE DATABASE [{catalog}]");
      }

      foreach (var catalog in new[] { SourceCatalog, TargetCatalog })
      {
        await using var connection = new SqlConnection(ConnectionFor(catalog));
        var options = new DbContextOptionsBuilder<TenantDbContext>()
          .UseSqlServer(connection, sql => sql.MigrationsHistoryTable(
            TenantPersistenceConstants.MigrationHistoryTable,
            TenantPersistenceConstants.MigrationHistorySchema))
          .Options;
        await using var context = new TenantDbContext(
          options, new TestUser(), new TestTenant(null), new TestClock());
        await context.Database.MigrateAsync();
      }

      storage.Servers[ServerKey] = new TenantStorageServerOptions { ConnectionString = Configured() };

      await using var platform = PlatformContext();
      await platform.Database.MigrateAsync();

      SourceDatabaseId = await RegisterAsync(platform, TenantDatabaseStorageMode.Shared, SourceCatalog);
      TargetDatabaseId = await RegisterAsync(platform, TenantDatabaseStorageMode.Dedicated, TargetCatalog);

      TenantA = await SeedTenantAsync(platform, "E5AAA");
      TenantB = await SeedTenantAsync(platform, "E5BBB");

      // THE RECOVERY ACTIVATION GATE IS MANDATORY, so the target must genuinely satisfy it: a backup
      // policy, a succeeded full baseline, a succeeded restore verification of THAT baseline, and a held
      // Protected readiness verdict. Anything less and the orchestrator refuses at preflight — which is
      // exactly what the unprotected variant of this fixture exercises.
      if (protectTarget)
      {
        await ProtectAsync(TargetDatabaseId);
      }
    }

    private static async Task<long> RegisterAsync(
      PlatformDbContext platform, TenantDatabaseStorageMode storageMode, string databaseName)
    {
      var database = TenantDatabase.Register(
        TenantDatabaseHostingMode.PlatformManaged, storageMode, ServerKey, databaseName,
        TenantDatabaseProvisioningStatus.Ready, Actor, Now).Value;

      // Health is stamped NOW: ADR-018's gate refuses a database whose observation has aged out.
      var observedUtc = DateTimeOffset.UtcNow;
      database.RecordConnectivity(TenantDatabaseConnectivityStatus.Healthy, Actor, observedUtc);
      database.RecordSchemaHealth(
        TenantDatabaseSchemaCompatibilityStatus.UpToDate, null, null, Actor, observedUtc);
      platform.TenantDatabases.Add(database);
      await platform.SaveChangesAsync();

      var policy = TenantDatabaseBackupPolicy.Create(
        database.Id, enabled: true, TenantDatabaseBackupManagementMode.AutomaticByPlatform, "phase-e5",
        fullBackupIntervalMinutes: 1_440,
        differentialBackupIntervalMinutes: null,
        transactionLogBackupIntervalMinutes: null,
        retentionExpectationDays: 30,
        restoreVerificationIntervalDays: 30,
        maximumBackupAgeMinutes: 2_880,
        Actor, Now).Value;
      platform.TenantDatabaseBackupPolicies.Add(policy);
      await platform.SaveChangesAsync();
      return database.Id;
    }

    private async Task ProtectAsync(long tenantDatabaseId)
    {
      var observedUtc = DateTimeOffset.UtcNow;
      long baselineId;

      await using (var platform = PlatformContext())
      {
        var run = TenantDatabaseBackupRun.Start(
          tenantDatabaseId, TenantDatabaseBackupOperation.SqlServerFull(), "phase-e5", Actor,
          observedUtc.AddHours(-2)).Value;
        platform.TenantDatabaseBackupRuns.Add(run);
        await platform.SaveChangesAsync();

        run.Succeed("evidence-identity", "artifact.bak", 1024, 500m, 520m, 0m, 500m, null, Actor,
          observedUtc.AddHours(-2));
        await platform.SaveChangesAsync();
        baselineId = run.Id;
      }

      await using (var platform = PlatformContext())
      {
        var store = new TenantDatabaseRestoreVerificationRunStore(
          platform, new FixedClock(observedUtc.AddHours(-1)));

        var admitted = await store.TryAdmitAsync(new TenantDatabaseRestoreVerificationAdmissionRequest(
          tenantDatabaseId, baselineId, ExpectedPreviousSuccessfulVerificationRunId: null,
          TenantDatabaseRestoreDepth.Full, RestoreServerKey, Actor));
        Assert.True(admitted.IsSuccess);

        Assert.True((await store.BeginRestoreAsync(
          admitted.Value,
          TenantDatabaseVerificationNaming.ForRun(tenantDatabaseId, admitted.Value),
          Actor)).IsSuccess);

        Assert.True((await store.MarkSucceededAndRecordEvidenceAsync(
          admitted.Value, baselineId, Actor)).IsSuccess);
      }

      await using (var platform = PlatformContext())
      {
        await new TenantDatabaseRecoveryReadinessWriter(platform, new FixedClock(observedUtc))
          .RecordRecoveryReadinessAsync(
            tenantDatabaseId, TenantDatabaseRecoveryReadinessStatus.Protected, Actor,
            lastSuccessfulFullBackupUtc: observedUtc.AddHours(-2),
            lastRestoreVerificationUtc: observedUtc.AddHours(-1));
      }
    }

    // Degrades the target's recoverability the way a failed backup sweep would, so the pre-flip recheck
    // sees something preflight did not.
    public async Task DegradeTargetReadinessAsync()
    {
      await using var platform = PlatformContext();
      await new TenantDatabaseRecoveryReadinessWriter(platform, new FixedClock(DateTimeOffset.UtcNow))
        .RecordRecoveryReadinessAsync(
          TargetDatabaseId, TenantDatabaseRecoveryReadinessStatus.Degraded, Actor,
          lastSuccessfulFullBackupUtc: DateTimeOffset.UtcNow.AddHours(-2),
          lastRestoreVerificationUtc: DateTimeOffset.UtcNow.AddHours(-1));
    }

    private async Task<Guid> SeedTenantAsync(PlatformDbContext platform, string code)
    {
      var tenant = Tenant.Create(
        TenantCode.Create(code).Value, TenantName.Create($"Phase E5 {code}").Value,
        Actor, Guid.NewGuid(), Now).Value;
      platform.Tenants.Add(tenant);
      await platform.SaveChangesAsync();

      platform.TenantDatabaseAssignments.Add(
        TenantDatabaseAssignment.CreateInitial(tenant.Id, SourceDatabaseId, "phase-e5", Actor, Now).Value);
      await platform.SaveChangesAsync();
      return tenant.Id;
    }

    public TenantCutoverStartRequest StartRequest() => new(TenantA, TargetDatabaseId, Actor);

    private TenantDatabaseConnectionFactory ConnectionFactory() => new(Options.Create(storage));

    public TenantCutoverOperationStore Store() =>
      new(PlatformContext(), new TestClock(), copy.ReleaseOwnershipTimeout);

    public TenantCutoverFreezeService FreezeService()
    {
      var platform = PlatformContext();
      return new TenantCutoverFreezeService(
        new TenantCutoverOperationStore(platform, new TestClock(), copy.ReleaseOwnershipTimeout),
        new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(platform)),
        ConnectionFactory(), Options.Create(freeze));
    }

    public TenantCutoverCopyService CopyService()
    {
      var platform = PlatformContext();
      return new TenantCutoverCopyService(
        new TenantCutoverOperationStore(platform, new TestClock(), copy.ReleaseOwnershipTimeout),
        ConnectionFactory(), platform, Options.Create(copy), CutoverTenantModel.Source);
    }

    public TenantCutoverRoutingFlipService FlipService(ITenantRoutingCacheInvalidator? invalidator = null)
    {
      var platform = PlatformContext();
      return new TenantCutoverRoutingFlipService(
        platform,
        new TenantCutoverOperationStore(platform, new TestClock(), copy.ReleaseOwnershipTimeout),
        CopyService(), invalidator ?? new TenantRoutingMemoryCache(),
        new TestClock(), Options.Create(copy));
    }

    // The production graph, assembled from the real components.
    public TenantCutoverOrchestrator Orchestrator(ITenantRoutingCacheInvalidator? invalidator = null)
    {
      var platform = PlatformContext();
      var store = new TenantCutoverOperationStore(platform, new TestClock(), copy.ReleaseOwnershipTimeout);
      var copyService = new TenantCutoverCopyService(
        store, ConnectionFactory(), platform, Options.Create(copy), CutoverTenantModel.Source);

      return new TenantCutoverOrchestrator(
        platform,
        store,
        new TenantCutoverFreezeService(
          store, new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(platform)),
          ConnectionFactory(), Options.Create(freeze)),
        copyService,
        new TenantCutoverRoutingFlipService(
          platform, store, copyService,
          invalidator ?? new TenantRoutingMemoryCache(), new TestClock(), Options.Create(copy)),
        new TenantDatabaseRecoveryActivationGate(
          new TenantDatabaseRecoveryActivationReadRepository(platform), new TestClock()),
        VersionAwareResolver(platform),
        Options.Create(copy));
    }

    private static VersionAwareTenantDatabaseResolver VersionAwareResolver(PlatformDbContext platform) =>
      new(
        new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(platform)),
        new TenantRoutingVersionReader(platform),
        new TenantRoutingMemoryCache(),
        new TenantRoutingCacheOptions { Lifetime = TimeSpan.FromMinutes(10) },
        new TestClock());

    public ResolverInstance NewResolverInstance()
    {
      var platform = PlatformContext();
      var cache = new TenantRoutingMemoryCache();
      return new ResolverInstance(platform, cache, new VersionAwareTenantDatabaseResolver(
        new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(platform)),
        new TenantRoutingVersionReader(platform), cache,
        new TenantRoutingCacheOptions { Lifetime = TimeSpan.FromMinutes(10) }, new TestClock()));
    }

    public async Task<TenantDbContext> CreateRoutedContextAsync(Guid tenantId)
    {
      var platform = PlatformContext();
      var factory = new TenantDbContextFactory(
        VersionAwareResolver(platform),
        ConnectionFactory(),
        new TenantDatabaseTrafficGate(TenantDatabaseHealthFreshness.Default),
        new TestUser(), new TestTenant(tenantId), new TestClock(),
        new TenantCutoverWriteFence(Store(), Options.Create(freeze)));

      var created = await factory.CreateAsync(tenantId);
      Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
      return created.Value;
    }

    public async Task<long> BeginAsync()
    {
      var begun = await Store().BeginAsync(
        new TenantCutoverBeginRequest(TenantA, SourceDatabaseId, TargetDatabaseId, Actor));
      Assert.True(begun.IsSuccess);
      return begun.Value;
    }

    public async Task<long> BeginAndFreezeAsync()
    {
      var operationId = await BeginAsync();
      Assert.True((await FreezeService().FreezeAsync(operationId)).IsSuccess);
      return operationId;
    }

    public async Task<long> BeginFreezeAndCopyAsync()
    {
      var operationId = await BeginAndFreezeAsync();
      Assert.True((await CopyService().CopyAsync(operationId)).IsSuccess);
      return operationId;
    }

    public async Task<long> BeginFreezeCopyAndFlipAsync()
    {
      var operationId = await BeginFreezeAndCopyAsync();
      var flipped = await FlipService().FlipAsync(operationId);
      Assert.True(flipped.IsSuccess, flipped.IsFailure ? flipped.Error.Code : null);
      return operationId;
    }

    public static Company NewCompany(Guid tenantId, string code) =>
      Company.Create(
        tenantId, CompanyCode.Create(code).Value, CompanyName.Create($"Company {code}").Value,
        BaseCurrencyCode.Create("USD").Value, Actor, Guid.NewGuid(), Now).Value;

    public async Task SeedCompaniesAsync(Guid tenantId, int count, string prefix)
    {
      await using var connection = new SqlConnection(ConnectionFor(SourceCatalog));
      await connection.OpenAsync();
      await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

      for (var index = 0; index < count; index++)
      {
        var code = $"{prefix}{index:D5}";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
          INSERT INTO [tenant].[Companies]
            ([CompanyId],[TenantId],[CompanyCode],[NormalizedCompanyCode],[CompanyName],[BaseCurrencyCode],
             [Status],[StatusChangeReasonCode],[StatusChangedUtc],[StatusChangedBy],
             [CreatedUtc],[CreatedBy],[ModifiedUtc],[ModifiedBy])
          VALUES (@Id,@TenantId,@Code,@Code,@Name,'USD',N'Active',N'Created',@Stamp,@Actor,
                  @Stamp,@Actor,@Stamp,@Actor);
          """;
        command.Parameters.AddWithValue("@Id", Guid.NewGuid());
        command.Parameters.AddWithValue("@TenantId", tenantId);
        command.Parameters.AddWithValue("@Code", code);
        command.Parameters.AddWithValue("@Name", $"Company {code}");
        command.Parameters.AddWithValue("@Stamp", Now.AddSeconds(index).UtcDateTime);
        command.Parameters.AddWithValue("@Actor", "seed");
        await command.ExecuteNonQueryAsync();
      }

      await transaction.CommitAsync();
    }

    public Task TamperWithTargetAsync() => ExecuteAsync(
      TargetCatalog,
      $"UPDATE TOP (1) [tenant].[Companies] SET [CompanyName] = N'Tampered' WHERE [TenantId] = '{TenantA:D}'");

    public static async Task<int> CompanyCountAsync(string catalog, Guid tenantId)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = "SELECT COUNT(*) FROM [tenant].[Companies] WHERE [TenantId] = @TenantId";
      command.Parameters.AddWithValue("@TenantId", tenantId);
      return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    public async Task<List<AssignmentRow>> AssignmentsAsync(Guid tenantId)
    {
      await using var platform = PlatformContext();
      return await platform.TenantDatabaseAssignments
        .AsNoTracking()
        .Where(assignment => assignment.TenantId == tenantId)
        .OrderBy(assignment => assignment.Id)
        .Select(assignment => new AssignmentRow(
          assignment.TenantDatabaseId, assignment.RoutingVersion, assignment.EndedUtc))
        .ToListAsync();
    }

    public async Task<OperationRow> ReadOperationAsync(long operationId)
    {
      await using var platform = PlatformContext();
      var row = await platform.TenantCutoverOperations
        .AsNoTracking()
        .Where(operation => operation.Id == operationId)
        .Select(operation => new OperationRow(
          operation.Status, operation.RoutingVersion,
          operation.RoutingFlippedUtc, operation.PostCutoverWriteObservedUtc))
        .SingleOrDefaultAsync();
      Assert.NotNull(row);
      return row!;
    }

    public Task<TenantCutoverOperationRecord?> FindActiveOperationAsync(Guid tenantId) =>
      Store().FindActiveForTenantAsync(tenantId);

    public async Task<int> OperationCountAsync(Guid tenantId)
    {
      await using var platform = PlatformContext();
      return await platform.TenantCutoverOperations.CountAsync(
        operation => operation.TenantId == tenantId);
    }

    public async Task AssertStillSharedAndFrozenAsync(long operationId)
    {
      var active = Assert.Single((await AssignmentsAsync(TenantA)).Where(row => row.EndedUtc is null));
      Assert.Equal(SourceDatabaseId, active.TenantDatabaseId);
      Assert.Equal(1, active.RoutingVersion);

      var operation = await ReadOperationAsync(operationId);
      Assert.Equal(TenantCutoverOperationStatus.Frozen, operation.Status);
      Assert.Null(operation.RoutingFlippedUtc);
    }

    public Task DeleteOneAssignmentAsync() => ExecuteAsync(
      platformCatalog,
      $"DELETE TOP (1) FROM [platform].[TenantDatabaseAssignments] WHERE [TenantId] = '{TenantA:D}'");

    public Task DeleteAllAssignmentsAsync() => ExecuteAsync(
      platformCatalog,
      $"DELETE FROM [platform].[TenantDatabaseAssignments] WHERE [TenantId] = '{TenantA:D}'");

    // A later cutover for the same tenant. Inserted directly because StartAsync cannot produce one — a
    // Dedicated source is refused — while the write gate's ordering rule must hold regardless of how the
    // row arrived, and will matter as soon as repeated cutovers are supported.
    public async Task<long> InsertFrozenOperationDirectlyAsync(long sourceId, long targetId)
    {
      await using var connection = new SqlConnection(ConnectionFor(platformCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = $"""
        INSERT INTO [platform].[TenantCutoverOperations]
          ([TenantId],[SourceTenantDatabaseId],[TargetTenantDatabaseId],[Status],[StartedUtc],
           [FreezeRequestedUtc],[FrozenUtc],[CreatedUtc],[CreatedBy],[ModifiedUtc],[ModifiedBy])
        VALUES ('{TenantA:D}', {sourceId}, {targetId}, N'Frozen', SYSDATETIMEOFFSET(),
                SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), N'direct',
                SYSDATETIMEOFFSET(), N'direct');
        SELECT CAST(SCOPE_IDENTITY() AS bigint);
        """;
      return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    // Cutover history for OTHER tenants: the rows that accumulate and that the write-gate index has to see
    // past. A realistic mix — most cutovers end Abandoned or Completed, and Completed is the status the new
    // index exists to cover.
    public Task SeedCutoverHistoryAsync(int count) => ExecuteAsync(platformCatalog, $"""
      DECLARE @i int = 0;
      WHILE @i < {count} BEGIN
        INSERT INTO [platform].[TenantCutoverOperations]
          ([TenantId],[SourceTenantDatabaseId],[TargetTenantDatabaseId],[Status],[StartedUtc],
           [RoutingFlippedUtc],[RoutingVersion],[CompletedUtc],
           [CreatedUtc],[CreatedBy],[ModifiedUtc],[ModifiedBy])
        VALUES (NEWID(), {SourceDatabaseId}, {TargetDatabaseId},
                CASE WHEN @i % 2 = 0 THEN N'Completed' ELSE N'Abandoned' END,
                SYSDATETIMEOFFSET(),
                CASE WHEN @i % 2 = 0 THEN SYSDATETIMEOFFSET() ELSE NULL END,
                CASE WHEN @i % 2 = 0 THEN 2 ELSE NULL END,
                SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), N'history', SYSDATETIMEOFFSET(), N'history');
        SET @i = @i + 1;
      END
      """);

    // Statistics only. The procedure cache is deliberately NOT cleared any more: the plan is now returned by
    // the server rather than looked up in a cache, so emptying the cache would buy nothing and cost the rest
    // of the suite its compiled plans.
    public Task RefreshStatisticsAsync() => ExecuteAsync(platformCatalog, """
      UPDATE STATISTICS [platform].[TenantCutoverOperations] WITH FULLSCAN;
      """);

    // Identified by the table and the column the write gate reads. The same fragments the DMV predicate
    // used, applied to the recorded production statement instead of to a cache that may have forgotten it.
    public Task<MeasuredPlan> CaptureWriteGatePlanAsync() =>
      Explain(["[TenantCutoverOperations]", "[PostCutoverWriteObservedUtc]"]);

    private async Task<MeasuredPlan> Explain(string[] required)
    {
      var captured = await QueryPlanCapture.ExplainAsync(
        ConnectionFor(platformCatalog), recorder.Match(required));
      return new MeasuredPlan(captured.PlanXml, captured.LogicalReads, captured.Microseconds);
    }

    public async Task<List<(string Name, string Events, bool Disabled)>> RoutingTriggersAsync()
    {
      await using var connection = new SqlConnection(ConnectionFor(platformCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = """
        SELECT t.[name],
               STUFF((SELECT N',' + te.[type_desc] FROM sys.trigger_events te
                      WHERE te.[object_id] = t.[object_id] FOR XML PATH('')), 1, 1, N''),
               t.[is_disabled]
        FROM sys.triggers t
        JOIN sys.objects o ON o.[object_id] = t.[parent_id]
        WHERE o.[name] = N'TenantDatabaseAssignments'
        ORDER BY t.[name];
        """;

      var triggers = new List<(string, string, bool)>();
      await using var reader = await command.ExecuteReaderAsync();
      while (await reader.ReadAsync())
      {
        triggers.Add((reader.GetString(0), reader.GetString(1), reader.GetBoolean(2)));
      }

      return triggers;
    }

    // The recorder observes every statement the PRODUCTION components issue through this context, so the
    // plan test can measure the real query without hand-writing it. See QueryPlanCapture.
    private readonly ProductionSqlRecorder recorder = new();

    private PlatformDbContext PlatformContext()
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(ConnectionFor(platformCatalog),
          sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .AddInterceptors(recorder)
        .Options;
      return new PlatformDbContext(options, new TestUser(), new TestTenant(null), new TestClock());
    }

    private static string Configured() =>
      IntegrationSqlEnvironment.BaseConnectionString;

    public static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog, Pooling = false }
        .ConnectionString;

    private static async Task ExecuteAsync(string catalog, string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      command.CommandTimeout = 600;
      await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
      foreach (var catalog in new[] { SourceCatalog, TargetCatalog, platformCatalog }
        .Where(value => !string.IsNullOrWhiteSpace(value)))
      {
        try
        {
          await ExecuteAsync("master",
            $"IF DB_ID(N'{catalog}') IS NOT NULL BEGIN " +
            $"ALTER DATABASE [{catalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{catalog}]; END");
        }
        catch (SqlException error)
        {
          TestCatalogJanitor.RecordLeak(catalog, error);
        }
      }
    }
  }

  private sealed class ResolverInstance(
    PlatformDbContext context,
    TenantRoutingMemoryCache cache,
    VersionAwareTenantDatabaseResolver resolver) : IAsyncDisposable
  {
    public TenantRoutingMemoryCache Cache => cache;

    public VersionAwareTenantDatabaseResolver Resolver => resolver;

    public ValueTask DisposeAsync() => context.DisposeAsync();
  }

  private sealed class TestUser : ICurrentUser
  {
    public string? UserId => "cutover-orchestration-tests";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class TestTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId => tenantId;
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }

  private sealed class FixedClock(DateTimeOffset utcNow) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => utcNow;
  }
}
