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

// THE ATOMIC ROUTING FLIP, AGAINST REAL SQL (ADR-020, TS-Storage Phase E4).
//
// The claim is that one call moves three facts together and that nothing observable sits between them: the
// Shared assignment ends, the Dedicated assignment begins at the next RoutingVersion, and the operation
// records that routing moved. Everything else here is about what happens to writers on both sides of that
// instant — the stale context still bound to the source, the fresh one that resolves the target, and the
// instance that was never told anything at all.
[Collection(TenantBackupSerialSuites.Name)]
public sealed class TenantCutoverRoutingFlipSqlServerTests(ITestOutputHelper output)
{
  // ---- A. The flip itself.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_frozen_validated_cutover_flips_routing_atomically()
  {
    await using var fixture = await FlipFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(3, "FLIP");
    var operationId = await fixture.BeginFreezeAndCopyAsync();

    var flipped = await fixture.FlipService().FlipAsync(operationId);

    Assert.True(flipped.IsSuccess, flipped.IsFailure ? flipped.Error.Code : null);
    Assert.Equal(TenantCutoverFlipOutcome.Flipped, flipped.Value.Outcome);
    Assert.Equal(1, flipped.Value.PreviousRoutingVersion);
    Assert.Equal(2, flipped.Value.RoutingVersion);
    Assert.True(flipped.Value.LocalInvalidationSucceeded);

    // EXACTLY ONE ACTIVE ASSIGNMENT, and it is the dedicated target at the advanced version.
    var assignments = await fixture.AssignmentsAsync();
    var active = Assert.Single(assignments.Where(assignment => assignment.EndedUtc is null));
    Assert.Equal(fixture.TargetDatabaseId, active.TenantDatabaseId);
    Assert.Equal(2, active.RoutingVersion);

    // ...and the Shared assignment is retained as history rather than deleted.
    var ended = Assert.Single(assignments.Where(assignment => assignment.EndedUtc is not null));
    Assert.Equal(fixture.SourceDatabaseId, ended.TenantDatabaseId);
    Assert.Equal(1, ended.RoutingVersion);

    // The operation agrees, and carries the SAME authoritative version the assignment does.
    var operation = await fixture.ReadOperationAsync(operationId);
    Assert.Equal(TenantCutoverOperationStatus.RoutingFlipped, operation.Status);
    Assert.Equal(2, operation.RoutingVersion);
    Assert.NotNull(operation.RoutingFlippedUtc);

    // Routing has not been claimed to have been written to yet.
    Assert.Null(operation.PostCutoverWriteObservedUtc);
  }

  // ---- B + C. Preconditions, refused without touching routing.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_cutover_that_is_not_frozen_cannot_flip()
  {
    await using var fixture = await FlipFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(2, "NFZ");
    var operationId = await fixture.BeginAsync();

    var preparing = await fixture.FlipService().FlipAsync(operationId);

    Assert.True(preparing.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverOperationNotFrozen.Code, preparing.Error.Code);
    await fixture.AssertStillSharedAsync(operationId, TenantCutoverOperationStatus.Preparing);
  }

  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_tenant_that_no_longer_routes_to_the_recorded_source_cannot_flip()
  {
    await using var fixture = await FlipFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(2, "DRF");
    var operationId = await fixture.BeginFreezeAndCopyAsync();

    // Routing moved out of band, so this operation's premise is gone.
    await fixture.RepointAssignmentAsync(fixture.SecondSharedDatabaseId);

    var flipped = await fixture.FlipService().FlipAsync(operationId);

    Assert.True(flipped.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverSourceNotEligible.Code, flipped.Error.Code);
    Assert.Equal(
      TenantCutoverOperationStatus.Frozen, (await fixture.ReadOperationAsync(operationId)).Status);
  }

  // ---- D + F. A target that no longer validates refuses the flip, and leaves Shared/Frozen intact.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_target_changed_after_the_copy_refuses_the_flip_and_leaves_routing_alone()
  {
    await using var fixture = await FlipFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(4, "TMP");
    var operationId = await fixture.BeginFreezeAndCopyAsync();

    // Out-of-band tampering between copy and flip. A validation that passed minutes ago says nothing now.
    await fixture.TamperWithTargetAsync();

    var flipped = await fixture.FlipService().FlipAsync(operationId);

    Assert.True(flipped.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverTargetInconsistent.Code, flipped.Error.Code);

    // FAILURE BEFORE COMMIT LEAVES EVERYTHING AS IT WAS.
    await fixture.AssertStillSharedAsync(operationId, TenantCutoverOperationStatus.Frozen);
  }

  // ---- E. Two instances racing. One authoritative flip, and the loser is told which.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task Two_concurrent_flips_produce_exactly_one_authoritative_flip()
  {
    await using var fixture = await FlipFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(3, "RACE");
    var operationId = await fixture.BeginFreezeAndCopyAsync();

    var attempts = await Task.WhenAll(
      Task.Run(() => fixture.FlipService().FlipAsync(operationId)),
      Task.Run(() => fixture.FlipService().FlipAsync(operationId)));

    // At most one may report having MOVED routing; a second may legitimately report it was already done.
    Assert.Equal(1, attempts.Count(attempt =>
      attempt.IsSuccess && attempt.Value.Outcome == TenantCutoverFlipOutcome.Flipped));

    foreach (var failed in attempts.Where(attempt => attempt.IsFailure))
    {
      Assert.Contains(failed.Error.Code, new[]
      {
        TenantStorageErrors.CutoverCopyOwnershipNotAcquired.Code,
        TenantStorageErrors.CutoverConcurrencyConflict.Code,
        TenantStorageErrors.CutoverOperationNotFrozen.Code
      });
    }

    // ONE Dedicated assignment, never two. This is the assertion the whole race matters for.
    var assignments = await fixture.AssignmentsAsync();
    Assert.Single(assignments.Where(assignment => assignment.EndedUtc is null));
    Assert.Single(assignments.Where(assignment =>
      assignment.TenantDatabaseId == fixture.TargetDatabaseId));
    Assert.Equal(2, (await fixture.ReadOperationAsync(operationId)).RoutingVersion);
  }

  // ---- Retry after a committed flip is idempotent, and never creates a second assignment.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task Retrying_a_committed_flip_reports_already_flipped()
  {
    await using var fixture = await FlipFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(2, "RTY");
    var operationId = await fixture.BeginFreezeAndCopyAsync();

    AssertFlipped(await fixture.FlipService().FlipAsync(operationId));
    var retried = await fixture.FlipService().FlipAsync(operationId);

    Assert.True(retried.IsSuccess);
    Assert.Equal(TenantCutoverFlipOutcome.AlreadyFlipped, retried.Value.Outcome);
    Assert.Equal(2, retried.Value.RoutingVersion);

    Assert.Equal(2, (await fixture.AssignmentsAsync()).Count);
  }

  // ---- G. Invalidation is an optimisation: its failure cannot unmake a committed flip.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_failing_local_invalidation_leaves_the_flip_committed()
  {
    await using var fixture = await FlipFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(3, "INV");
    var operationId = await fixture.BeginFreezeAndCopyAsync();

    var flipped = await fixture.FlipService(new ThrowingInvalidator()).FlipAsync(operationId);

    // REPORTED, NOT FAILED. Routing moved; only the local cache eviction did not.
    Assert.True(flipped.IsSuccess, flipped.IsFailure ? flipped.Error.Code : null);
    Assert.Equal(TenantCutoverFlipOutcome.Flipped, flipped.Value.Outcome);
    Assert.False(flipped.Value.LocalInvalidationSucceeded);
    Assert.Equal(
      TenantStorageErrors.CutoverInvalidationIncomplete.Code, flipped.Value.InvalidationError!.Code);

    var active = Assert.Single((await fixture.AssignmentsAsync())
      .Where(assignment => assignment.EndedUtc is null));
    Assert.Equal(fixture.TargetDatabaseId, active.TenantDatabaseId);
    Assert.Equal(
      TenantCutoverOperationStatus.RoutingFlipped, (await fixture.ReadOperationAsync(operationId)).Status);
  }

  // ---- H. THE CROSS-INSTANCE PROOF, exercising E4's flip and E2's resolver together.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task An_instance_that_receives_no_invalidation_converges_after_a_real_flip()
  {
    await using var fixture = await FlipFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(3, "CONV");
    var operationId = await fixture.BeginFreezeAndCopyAsync();

    // Instance A caches Shared/1 through the real version-aware resolver.
    await using var instanceA = fixture.NewResolverInstance();
    var before = await instanceA.Resolver.ResolveAsync(fixture.TenantId);
    Assert.True(before.IsSuccess);
    Assert.Equal(fixture.SourceCatalog, before.Value.DatabaseName);
    Assert.Equal(1, before.Value.RoutingVersion);
    Assert.Equal(1, instanceA.Cache.Count);

    // Instance B flips. Nothing is delivered to A — its own invalidator is never called.
    AssertFlipped(await fixture.FlipService().FlipAsync(operationId));
    Assert.Equal(1, instanceA.Cache.Count);

    var after = await instanceA.Resolver.ResolveAsync(fixture.TenantId);

    Assert.True(after.IsSuccess);
    Assert.Equal(fixture.TargetCatalog, after.Value.DatabaseName);
    Assert.Equal(TenantDatabaseStorageMode.Dedicated, after.Value.StorageMode);
    Assert.Equal(2, after.Value.RoutingVersion);
  }

  // ---- I + J + K + L + M. THE MANDATORY IN-FLIGHT TEST: what happens to writers on both sides.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_context_created_before_the_flip_is_refused_and_a_fresh_one_succeeds()
  {
    await using var fixture = await FlipFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(2, "STALE");
    var operationId = await fixture.BeginFreezeAndCopyAsync();

    // ---- 1 + 2. A source-routed context created BEFORE the flip, holding work it has not saved.
    var stale = await fixture.CreateRoutedContextAsync();
    Assert.Equal(fixture.SourceCatalog, await CurrentCatalogAsync(stale));
    stale.Companies.Add(FlipFixture.NewCompany(fixture.TenantId, "STALEWRITE"));

    // ---- 3. The flip happens underneath it.
    AssertFlipped(await fixture.FlipService().FlipAsync(operationId));

    // ---- 4. REFUSED. This context never re-resolves, so E2's version check cannot help here — the
    // route-aware write fence is the only thing standing between a stale writer and the database its tenant
    // was just moved off.
    var refused = await Assert.ThrowsAsync<TenantStorageUnavailableException>(
      () => stale.SaveChangesAsync());
    Assert.Equal(TenantStorageErrors.TenantWritesFrozen.Code, refused.Error.Code);
    await stale.DisposeAsync();

    // Nothing landed on the source.
    Assert.Equal(2, await fixture.CompanyCountAsync(fixture.SourceCatalog));

    // The write attempt against the SOURCE must not have been recorded as a post-cutover write.
    Assert.Null((await fixture.ReadOperationAsync(operationId)).PostCutoverWriteObservedUtc);

    // ---- M. A read against the target does not count as a write either.
    await using (var reader = await fixture.CreateRoutedContextAsync())
    {
      Assert.Equal(fixture.TargetCatalog, await CurrentCatalogAsync(reader));
      Assert.Equal(2, await reader.Companies.CountAsync());
    }

    Assert.Null((await fixture.ReadOperationAsync(operationId)).PostCutoverWriteObservedUtc);

    // ---- 5 + 6 + 7. A fresh context resolves Dedicated/2 and its write succeeds.
    await using (var fresh = await fixture.CreateRoutedContextAsync())
    {
      Assert.Equal(fixture.TargetCatalog, await CurrentCatalogAsync(fresh));
      fresh.Companies.Add(FlipFixture.NewCompany(fixture.TenantId, "AFTERFLIP"));
      Assert.Equal(1, await fresh.SaveChangesAsync());
    }

    Assert.Equal(3, await fixture.CompanyCountAsync(fixture.TargetCatalog));

    // ---- 8 + K. The first genuine target write is recorded.
    var afterWrite = await fixture.ReadOperationAsync(operationId);
    Assert.NotNull(afterWrite.PostCutoverWriteObservedUtc);
    var firstObservation = afterWrite.PostCutoverWriteObservedUtc;

    // ---- L. A second write does not move it.
    await using (var second = await fixture.CreateRoutedContextAsync())
    {
      second.Companies.Add(FlipFixture.NewCompany(fixture.TenantId, "SECOND"));
      Assert.Equal(1, await second.SaveChangesAsync());
    }

    Assert.Equal(
      firstObservation, (await fixture.ReadOperationAsync(operationId)).PostCutoverWriteObservedUtc);
  }

  // ---- N. No release after the flip. This is the no-simple-flipback boundary.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_flipped_cutover_can_never_be_released_or_moved_backwards()
  {
    await using var fixture = await FlipFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(2, "NOBACK");
    var operationId = await fixture.BeginFreezeAndCopyAsync();
    AssertFlipped(await fixture.FlipService().FlipAsync(operationId));

    var released = await fixture.FreezeService().ReleaseFreezeAsync(operationId, "changed my mind");

    Assert.True(released.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverAlreadyFlipped.Code, released.Error.Code);
    Assert.Equal(
      TenantCutoverOperationStatus.RoutingFlipped, (await fixture.ReadOperationAsync(operationId)).Status);

    // Re-freezing is equally impossible: the domain admits no transition out of RoutingFlipped except
    // forward, so there is no reachable path back to a Shared assignment.
    Assert.True((await fixture.FreezeService().FreezeAsync(operationId)).IsFailure);

    var active = Assert.Single((await fixture.AssignmentsAsync())
      .Where(assignment => assignment.EndedUtc is null));
    Assert.Equal(fixture.TargetDatabaseId, active.TenantDatabaseId);
  }

  // ---- O + P + Q. THE DATABASE GUARD, tested against direct SQL rather than through the service.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task The_routing_version_guard_rejects_direct_sql_that_does_not_advance_the_version()
  {
    await using var fixture = await FlipFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(1, "GUARD");

    // The trigger exists, and is the only one on the routing table.
    var triggers = await fixture.RoutingTriggersAsync();
    var trigger = Assert.Single(triggers);
    Assert.Equal("TR_TenantDatabaseAssignments_EnforceRoutingVersion", trigger.Name);
    Assert.Contains("INSERT", trigger.Events, StringComparison.Ordinal);
    Assert.Contains("UPDATE", trigger.Events, StringComparison.Ordinal);
    // DELETE joined the guard in TS-Storage Phase E5. E4 shipped it as INSERT/UPDATE only, on the reasoning
    // that assignments are retained as history so there was no routing-significant delete to guard — but
    // that reasoning assumed the retention it was meant to enforce. A direct actor could delete a tenant's
    // assignment history and re-insert at version 1, and the insert check compares only against rows that
    // still exist, so with the history gone the reset looked legal. Asserted here against the LIVE trigger,
    // so this test tracks the database that actually ships rather than the one E4 described.
    Assert.Contains("DELETE", trigger.Events, StringComparison.Ordinal);
    Assert.False(trigger.Disabled);

    // ---- O. A new assignment that REUSES the tenant's current version is refused by the trigger. This is
    // the case declarative constraints cannot see: 1 is a perfectly legal value, and only comparing it
    // against the tenant's other rows reveals that it does not advance.
    var reused = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.InsertAssignmentDirectlyAsync(fixture.TargetDatabaseId, 1, ended: true));
    Assert.Equal(51020, reused.Number);

    // A non-positive version never reaches the trigger: CK_TenantDatabaseAssignments_RoutingVersion refuses
    // it first. Asserted as the CHECK it is (547) rather than folded in with the trigger — the two protect
    // different things, and pretending one covers the other's case would misdescribe both.
    var nonPositive = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.InsertAssignmentDirectlyAsync(fixture.TargetDatabaseId, 0, ended: true));
    Assert.Equal(547, nonPositive.Number);

    // Re-pointing a live assignment in place — a routing change with NO version change at all, which is
    // exactly what would leave every cached route valid.
    var repointed = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.RepointAssignmentDirectlyAsync(fixture.TargetDatabaseId));
    Assert.Equal(51021, repointed.Number);

    // Reviving an ended assignment, which would resurrect a superseded route. Refused by the FILTERED
    // UNIQUE INDEX (2601) rather than by the trigger: the tenant already has an active assignment, so
    // clearing EndedUtc produces a second one and the index is checked before AFTER triggers run. Asserted
    // as the mechanism that actually fires — claiming the trigger caught this would misdescribe which
    // guard is load-bearing here, and the trigger's own revive clause is proven by the flip's own path
    // staying legal below.
    await fixture.InsertAssignmentDirectlyAsync(fixture.TargetDatabaseId, 5, ended: true);
    var revived = await Assert.ThrowsAsync<SqlException>(() => fixture.ReviveEndedAssignmentAsync());
    Assert.Equal(2601, revived.Number);

    // ---- Q. MULTI-ROW SAFE: a set-based insert where only one row violates is rejected as a whole.
    var multiRow = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.InsertTwoAssignmentsDirectlyAsync(good: 6, bad: 2));
    Assert.Equal(51020, multiRow.Number);
    Assert.DoesNotContain(await fixture.AssignmentsAsync(), assignment => assignment.RoutingVersion == 6);

    // ---- P. And the legitimate transition the flip performs passes the guard.
    var operationId = await fixture.BeginFreezeAndCopyAsync();
    var flipped = await fixture.FlipService().FlipAsync(operationId);
    Assert.True(flipped.IsSuccess, flipped.IsFailure ? flipped.Error.Code : null);
    Assert.Equal(6, flipped.Value.RoutingVersion);
  }

  // ---- §17 index review for the queries this slice introduces.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task The_flip_and_fence_queries_seek_existing_indexes()
  {
    await using var fixture = await FlipFixture.CreateAsync();
    await fixture.SeedCompaniesAsync(200, "PLAN");
    var operationId = await fixture.BeginFreezeAndCopyAsync();
    AssertFlipped(await fixture.FlipService().FlipAsync(operationId));

    // Realistic accumulation on both routing tables before measuring.
    await fixture.SeedRoutingHistoryAsync(5_000);
    await fixture.RefreshStatisticsAsync();
    await fixture.ReissueMeasuredQueriesAsync();

    var plans = await fixture.CapturePlansAsync();
    Assert.True(plans.Count >= 2, $"captured only: {string.Join(", ", plans.Select(plan => plan.Label))}");

    foreach (var plan in plans)
    {
      output.WriteLine(
        $"{plan.Label}: {plan.Operation} using {plan.Index}, " +
        $"{plan.LogicalReadsPerExecution} logical reads/execution, {plan.MicrosecondsPerExecution}us.");

      Assert.DoesNotContain("PhysicalOp=\"Table Scan\"", plan.PlanXml, StringComparison.Ordinal);
      Assert.DoesNotContain("PhysicalOp=\"Clustered Index Scan\"", plan.PlanXml, StringComparison.Ordinal);
    }
  }

  // Reports the refusal code rather than a bare false: a flip has a dozen ways to be refused, and which one
  // fired is the whole diagnosis.
  private static void AssertFlipped(SSAS.BuildingBlocks.Domain.Result<TenantCutoverFlipReport> result) =>
    Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);

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

  private sealed record MeasuredPlan(
    string Label, string PlanXml, long LogicalReadsPerExecution, long MicrosecondsPerExecution)
  {
    public string Operation => string.Join(" -> ", Values("PhysicalOp=\""));

    public string Index => Values("Index=\"") is { Count: > 0 } indexes
      ? string.Join(", ", indexes.Distinct(StringComparer.Ordinal))
      : "(none)";

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

  private sealed record AssignmentRow(long Id, long TenantDatabaseId, long RoutingVersion, DateTimeOffset? EndedUtc);

  private sealed record OperationRow(
    TenantCutoverOperationStatus Status,
    long? RoutingVersion,
    DateTimeOffset? RoutingFlippedUtc,
    DateTimeOffset? PostCutoverWriteObservedUtc);

  private sealed class FlipFixture : IAsyncDisposable
  {
    private const string ServerKey = "PrimarySqlServer";
    private const string Actor = "cutover-flip-tests";

    // Seeded state is stamped in the PAST relative to the real clock the flip runs on. A fixed literal was
    // wrong here in a way that only shows up some hours of the day: ending an assignment refuses an end
    // timestamp earlier than its start, so a fixture that seeded "now" at a fixed future-of-real time made
    // the flip fail with AssignmentEndBeforeStart depending on when the suite happened to run.
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow.AddDays(-1);

    private readonly string token = Guid.NewGuid().ToString("N")[..12];
    private readonly TenantStorageOptions storage = new();
    private readonly TenantCutoverFreezeOptions freeze = new();
    private readonly TenantCutoverCopyOptions copy = new();
    private string platformCatalog = string.Empty;

    public string SourceCatalog { get; private set; } = string.Empty;

    public string TargetCatalog { get; private set; } = string.Empty;

    public Guid TenantId { get; private set; }

    public long SourceDatabaseId { get; private set; }

    public long TargetDatabaseId { get; private set; }

    public long SecondSharedDatabaseId { get; private set; }

    public static async Task<FlipFixture> CreateAsync()
    {
      var fixture = new FlipFixture();
      try
      {
        await fixture.InitialiseAsync();
        return fixture;
      }
      catch
      {
        await fixture.DisposeAsync();
        throw;
      }
    }

    private async Task InitialiseAsync()
    {
      platformCatalog = $"SSAS_E4_Platform_{token}";
      SourceCatalog = $"SSAS_E4_Shared_{token}";
      TargetCatalog = $"SSAS_E4_Dedicated_{token}";
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
      SecondSharedDatabaseId = await RegisterAsync(
        platform, TenantDatabaseStorageMode.Shared, $"SSAS_E4_SharedTwo_{token}");

      var tenant = Tenant.Create(
        TenantCode.Create("E4AAA").Value, TenantName.Create("Phase E4").Value,
        Actor, Guid.NewGuid(), Now).Value;
      platform.Tenants.Add(tenant);
      await platform.SaveChangesAsync();
      TenantId = tenant.Id;

      platform.TenantDatabaseAssignments.Add(
        TenantDatabaseAssignment.CreateInitial(TenantId, SourceDatabaseId, "phase-e4", Actor, Now).Value);
      await platform.SaveChangesAsync();
    }

    private static async Task<long> RegisterAsync(
      PlatformDbContext platform, TenantDatabaseStorageMode storageMode, string databaseName)
    {
      var database = TenantDatabase.Register(
        TenantDatabaseHostingMode.PlatformManaged, storageMode, ServerKey, databaseName,
        TenantDatabaseProvisioningStatus.Ready, Actor, Now).Value;

      // HEALTH IS STAMPED NOW, not at the seeded past instant: ADR-018's traffic gate refuses a database
      // whose schema observation has aged past the freshness window, and a routed context is exactly what
      // these tests need to obtain. Registration and assignment history stay in the past; only the
      // observation that must be recent is recent.
      var observedUtc = DateTimeOffset.UtcNow;
      database.RecordConnectivity(TenantDatabaseConnectivityStatus.Healthy, Actor, observedUtc);
      database.RecordSchemaHealth(
        TenantDatabaseSchemaCompatibilityStatus.UpToDate, null, null, Actor, observedUtc);
      platform.TenantDatabases.Add(database);
      await platform.SaveChangesAsync();
      return database.Id;
    }

    public TenantCutoverOperationStore Store() =>
      new(PlatformContext(), new TestClock(), copy.ReleaseOwnershipTimeout);

    public TenantCutoverFreezeService FreezeService()
    {
      var platform = PlatformContext();
      return new TenantCutoverFreezeService(
        new TenantCutoverOperationStore(platform, new TestClock(), copy.ReleaseOwnershipTimeout),
        new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(platform)),
        ConnectionFactory(),
        Options.Create(freeze));
    }

    public TenantCutoverCopyService CopyService()
    {
      var platform = PlatformContext();
      return new TenantCutoverCopyService(
        new TenantCutoverOperationStore(platform, new TestClock(), copy.ReleaseOwnershipTimeout),
        ConnectionFactory(), platform, Options.Create(copy));
    }

    public TenantCutoverRoutingFlipService FlipService(ITenantRoutingCacheInvalidator? invalidator = null)
    {
      var platform = PlatformContext();
      return new TenantCutoverRoutingFlipService(
        platform,
        new TenantCutoverOperationStore(platform, new TestClock(), copy.ReleaseOwnershipTimeout),
        CopyService(),
        invalidator ?? new TenantRoutingMemoryCache(),
        new TestClock(),
        Options.Create(copy));
    }

    private TenantDatabaseConnectionFactory ConnectionFactory() => new(Options.Create(storage));

    // A separate "process" with its own version-aware resolver and its own cache.
    public ResolverInstance NewResolverInstance()
    {
      var context = PlatformContext();
      var cache = new TenantRoutingMemoryCache();
      var resolver = new VersionAwareTenantDatabaseResolver(
        new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(context)),
        new TenantRoutingVersionReader(context),
        cache,
        new TenantRoutingCacheOptions { Lifetime = TimeSpan.FromMinutes(10) },
        new TestClock());
      return new ResolverInstance(context, cache, resolver);
    }

    // THE REAL ROUTED PATH: version-aware resolver, real connection factory, real write fence.
    public async Task<TenantDbContext> CreateRoutedContextAsync()
    {
      var platform = PlatformContext();
      var factory = new TenantDbContextFactory(
        new VersionAwareTenantDatabaseResolver(
          new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(platform)),
          new TenantRoutingVersionReader(platform),
          new TenantRoutingMemoryCache(),
          new TenantRoutingCacheOptions { Lifetime = TimeSpan.FromMinutes(10) },
          new TestClock()),
        ConnectionFactory(),
        new TenantDatabaseTrafficGate(TenantDatabaseHealthFreshness.Default),
        new TestUser(),
        new TestTenant(TenantId),
        new TestClock(),
        new TenantCutoverWriteFence(Store(), Options.Create(freeze)));

      var created = await factory.CreateAsync(TenantId);
      Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
      return created.Value;
    }

    public async Task<long> BeginAsync()
    {
      var begun = await Store().BeginAsync(
        new TenantCutoverBeginRequest(TenantId, SourceDatabaseId, TargetDatabaseId, Actor));
      Assert.True(begun.IsSuccess);
      return begun.Value;
    }

    public async Task<long> BeginFreezeAndCopyAsync()
    {
      var operationId = await BeginAsync();
      Assert.True((await FreezeService().FreezeAsync(operationId)).IsSuccess);
      Assert.True((await CopyService().CopyAsync(operationId)).IsSuccess);
      return operationId;
    }

    public static Company NewCompany(Guid tenantId, string code) =>
      Company.Create(
        tenantId, CompanyCode.Create(code).Value, CompanyName.Create($"Company {code}").Value,
        BaseCurrencyCode.Create("USD").Value, Actor, Guid.NewGuid(), Now).Value;

    public async Task SeedCompaniesAsync(int count, string prefix)
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
        command.Parameters.AddWithValue("@TenantId", TenantId);
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
      $"UPDATE TOP (1) [tenant].[Companies] SET [CompanyName] = N'Tampered' WHERE [TenantId] = '{TenantId:D}'");

    public async Task<int> CompanyCountAsync(string catalog)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = "SELECT COUNT(*) FROM [tenant].[Companies] WHERE [TenantId] = @TenantId";
      command.Parameters.AddWithValue("@TenantId", TenantId);
      return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    public async Task<List<AssignmentRow>> AssignmentsAsync()
    {
      await using var platform = PlatformContext();
      return await platform.TenantDatabaseAssignments
        .AsNoTracking()
        .Where(assignment => assignment.TenantId == TenantId)
        .OrderBy(assignment => assignment.Id)
        .Select(assignment => new AssignmentRow(
          assignment.Id, assignment.TenantDatabaseId, assignment.RoutingVersion, assignment.EndedUtc))
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

    public async Task AssertStillSharedAsync(long operationId, TenantCutoverOperationStatus expected)
    {
      var active = Assert.Single((await AssignmentsAsync()).Where(row => row.EndedUtc is null));
      Assert.Equal(SourceDatabaseId, active.TenantDatabaseId);
      Assert.Equal(1, active.RoutingVersion);

      var operation = await ReadOperationAsync(operationId);
      Assert.Equal(expected, operation.Status);
      Assert.Null(operation.RoutingFlippedUtc);
      Assert.Null(operation.RoutingVersion);
    }

    // Through the domain, so it satisfies the guard trigger — this is arranging drift, not testing it.
    public async Task RepointAssignmentAsync(long tenantDatabaseId)
    {
      await using var platform = PlatformContext();
      await using var transaction = await platform.Database.BeginTransactionAsync();
      var current = await platform.TenantDatabaseAssignments
        .SingleAsync(assignment => assignment.TenantId == TenantId && assignment.EndedUtc == null);
      Assert.True(current.End(Actor, Now.AddMinutes(1)).IsSuccess);
      await platform.SaveChangesAsync();

      platform.TenantDatabaseAssignments.Add(TenantDatabaseAssignment.Create(
        TenantId, tenantDatabaseId, current.RoutingVersion + 1, "drift", Actor, Now.AddMinutes(1)).Value);
      await platform.SaveChangesAsync();
      await transaction.CommitAsync();
    }

    // ---- Direct SQL, deliberately bypassing the application, to prove the DATABASE enforces the invariant.
    public Task InsertAssignmentDirectlyAsync(long tenantDatabaseId, long routingVersion, bool ended) =>
      ExecuteAsync(platformCatalog, $"""
        INSERT INTO [platform].[TenantDatabaseAssignments]
          ([TenantId],[TenantDatabaseId],[RoutingVersion],[AssignedUtc],[EndedUtc],[Reason],
           [CreatedUtc],[CreatedBy],[ModifiedUtc],[ModifiedBy])
        VALUES ('{TenantId:D}', {tenantDatabaseId}, {routingVersion}, SYSDATETIMEOFFSET(),
                {(ended ? "SYSDATETIMEOFFSET()" : "NULL")}, N'direct',
                SYSDATETIMEOFFSET(), N'direct', SYSDATETIMEOFFSET(), N'direct');
        """);

    public Task RepointAssignmentDirectlyAsync(long tenantDatabaseId) => ExecuteAsync(
      platformCatalog,
      $"UPDATE [platform].[TenantDatabaseAssignments] SET [TenantDatabaseId] = {tenantDatabaseId} " +
      $"WHERE [TenantId] = '{TenantId:D}' AND [EndedUtc] IS NULL");

    public Task ReviveEndedAssignmentAsync() => ExecuteAsync(
      platformCatalog, $"""
        UPDATE [platform].[TenantDatabaseAssignments]
        SET [EndedUtc] = NULL
        WHERE [TenantDatabaseAssignmentId] = (
          SELECT TOP (1) [TenantDatabaseAssignmentId] FROM [platform].[TenantDatabaseAssignments]
          WHERE [TenantId] = '{TenantId:D}' AND [EndedUtc] IS NOT NULL ORDER BY [RoutingVersion] DESC);
        """);

    // One statement, two rows, one of them illegal: the whole set must be rejected.
    public Task InsertTwoAssignmentsDirectlyAsync(long good, long bad) =>
      ExecuteAsync(platformCatalog, $"""
        INSERT INTO [platform].[TenantDatabaseAssignments]
          ([TenantId],[TenantDatabaseId],[RoutingVersion],[AssignedUtc],[EndedUtc],[Reason],
           [CreatedUtc],[CreatedBy],[ModifiedUtc],[ModifiedBy])
        SELECT '{TenantId:D}', {TargetDatabaseId}, v.[Version], SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(),
               N'direct', SYSDATETIMEOFFSET(), N'direct', SYSDATETIMEOFFSET(), N'direct'
        FROM (VALUES ({good}), ({bad})) AS v([Version]);
        """);

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

    // Other tenants' routing and cutover history, so the hot lookups are measured against ACCUMULATED
    // tables rather than a two-row fixture — which would describe the fixture rather than the design.
    //
    // Seeded through EF because assignments carry a foreign key to Tenants: inventing tenant ids in raw SQL
    // produces rows the schema correctly refuses.
    public async Task SeedRoutingHistoryAsync(int count)
    {
      await using var platform = PlatformContext();
      platform.ChangeTracker.AutoDetectChangesEnabled = false;

      var tenantIds = new List<Guid>(count);
      for (var index = 0; index < count; index++)
      {
        var tenant = Tenant.Create(
          TenantCode.Create($"HIST{index:D8}").Value,
          TenantName.Create($"History {index}").Value, Actor, Guid.NewGuid(), Now).Value;
        platform.Tenants.Add(tenant);
        tenantIds.Add(tenant.Id);
      }

      await platform.SaveChangesAsync();

      foreach (var tenantId in tenantIds)
      {
        var assignment = TenantDatabaseAssignment
          .CreateInitial(tenantId, SourceDatabaseId, "history", Actor, Now).Value;
        Assert.True(assignment.End(Actor, Now.AddMinutes(1)).IsSuccess);
        platform.TenantDatabaseAssignments.Add(assignment);
      }

      await platform.SaveChangesAsync();

      // Cutover history has no tenant foreign key, so it seeds set-based. Abandoned, because the filtered
      // unique index admits only one ACTIVE operation per tenant — which is the property keeping the write
      // fence's lookup small no matter how much history accrues.
      await ExecuteAsync(platformCatalog, $"""
        INSERT INTO [platform].[TenantCutoverOperations]
          ([TenantId],[SourceTenantDatabaseId],[TargetTenantDatabaseId],[Status],[StartedUtc],
           [CompletedUtc],[CreatedUtc],[CreatedBy],[ModifiedUtc],[ModifiedBy])
        SELECT [TenantId], {SourceDatabaseId}, {TargetDatabaseId}, N'Abandoned', SYSDATETIMEOFFSET(),
               SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), N'history', SYSDATETIMEOFFSET(), N'history'
        FROM [platform].[TenantDatabaseAssignments]
        WHERE [Reason] = N'history';
        """);
    }

    public Task RefreshStatisticsAsync() => ExecuteAsync(platformCatalog, """
      UPDATE STATISTICS [platform].[TenantDatabaseAssignments] WITH FULLSCAN;
      UPDATE STATISTICS [platform].[TenantCutoverOperations] WITH FULLSCAN;
      ALTER DATABASE SCOPED CONFIGURATION CLEAR PROCEDURE_CACHE;
      """);

    // Runs the two hot shapes through the real components so the captured plans are production queries.
    public async Task ReissueMeasuredQueriesAsync()
    {
      await Store().FindActiveWriteGateAsync(TenantId);

      await using var platform = PlatformContext();
      await new TenantRoutingVersionReader(platform).ReadCurrentRoutingVersionAsync(TenantId);
    }

    public async Task<IReadOnlyList<MeasuredPlan>> CapturePlansAsync()
    {
      var plans = new List<MeasuredPlan>();

      var gate = await PlanAsync(
        "cutover write-fence gate lookup",
        "CHARINDEX(N'[TenantCutoverOperations]', st.text) > 0 AND " +
        "CHARINDEX(N'[PostCutoverWriteObservedUtc]', st.text) > 0");
      if (gate is not null)
      {
        plans.Add(gate);
      }

      var version = await PlanAsync(
        "authoritative RoutingVersion read",
        "CHARINDEX(N'[TenantDatabaseAssignments]', st.text) > 0 AND " +
        "CHARINDEX(N'[RoutingVersion]', st.text) > 0 AND " +
        "CHARINDEX(N'[TenantDatabases]', st.text) = 0");
      if (version is not null)
      {
        plans.Add(version);
      }

      return plans;
    }

    private async Task<MeasuredPlan?> PlanAsync(string label, string predicate)
    {
      await using var connection = new SqlConnection(ConnectionFor(platformCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = $"""
        SELECT TOP (1) CAST(qp.query_plan AS nvarchar(max)),
               qs.total_logical_reads, qs.total_elapsed_time, qs.execution_count
        FROM sys.dm_exec_query_stats AS qs
        CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
        CROSS APPLY sys.dm_exec_query_plan(qs.plan_handle) AS qp
        WHERE {predicate}
        ORDER BY qs.last_execution_time DESC;
        """;

      await using var reader = await command.ExecuteReaderAsync();
      if (!await reader.ReadAsync())
      {
        return null;
      }

      var executions = Math.Max(reader.GetInt64(3), 1);
      return new MeasuredPlan(
        label, reader.GetString(0), reader.GetInt64(1) / executions, reader.GetInt64(2) / executions);
    }

    private PlatformDbContext PlatformContext()
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(ConnectionFor(platformCatalog),
          sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestUser(), new TestTenant(null), new TestClock());
    }

    private static string Configured() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

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
        catch (SqlException)
        {
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
    public string? UserId => "cutover-flip-tests";
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
}
