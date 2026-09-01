using System.Reflection;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Architecture.Tests;

// THE BOUNDARIES OF THE CUTOVER ORCHESTRATOR (ADR-020, TS-Storage Phase E5).
//
// The orchestrator's value is ORDER, OWNERSHIP and RESUMABILITY. Every correctness decision belongs to a
// component that already owns it and has already been reviewed, so the thing most worth guarding is that it
// keeps composing them rather than growing its own copy of a freeze, a copy engine, or a routing mutation.
// ---- PLANT RECORD (T-249): collapsing the file walk to `*.csx` reddens this file.
//
// Checked rather than assumed, after an audit found no recorded plant. The mutation leaves every
// directory in place and makes the pattern match nothing, which is the failure mode a missing
// directory does NOT produce -- that one throws.
public sealed class TenantCutoverOrchestrationArchitectureTests
{
  private static readonly Assembly InfrastructureAssembly = typeof(TenantCutoverWriteFence).Assembly;

  private static readonly Type Orchestrator = InfrastructureAssembly.GetType(
    "SSAS.Platform.Infrastructure.TenantStorage.TenantCutoverOrchestrator")!;

  // ---- IT COMPOSES E1-E4. Each phase arrives as an injected dependency, which is what makes "the
  // orchestrator did not reimplement the freeze" a structural fact rather than a reading of the code.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_orchestrator_composes_every_phase_rather_than_owning_one()
  {
    var dependencies = Dependencies(Orchestrator);

    Assert.Contains(typeof(ITenantCutoverFreezeService), dependencies);                        // E1
    Assert.Contains(typeof(ITenantDatabaseResolver), dependencies);                            // E2
    Assert.Contains(typeof(ITenantDatabaseRecoveryActivationGate), dependencies);              // Phase E
    Assert.Contains(dependencies, type => type.Name == "TenantCutoverCopyService");            // E3
    Assert.Contains(dependencies, type => type.Name == "TenantCutoverRoutingFlipService");     // E4
  }

  // ---- NO PHASE IS REIMPLEMENTED. The orchestrator holds no copy mechanism, mutates no assignment, and
  // computes no routing version — those are E3's and E4's, and a second implementation would be a second
  // set of rules to keep in agreement.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_orchestrator_implements_no_copy_and_mutates_no_routing()
  {
    var source = SourceOf("TenantCutoverOrchestrator.cs");

    foreach (var forbidden in ForbiddenInOrchestrator)
    {
      Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
    }

    // It cannot even express an assignment change: the aggregate is not on its surface.
    Assert.DoesNotContain(typeof(TenantDatabaseAssignment), Dependencies(Orchestrator));

    // ...and it holds no cache in any form, so it can never become a routing authority of its own.
    Assert.DoesNotContain(typeof(ITenantRoutingCache), Dependencies(Orchestrator));
    Assert.DoesNotContain(typeof(ITenantRoutingCacheInvalidator), Dependencies(Orchestrator));
  }

  // ---- ONE OWNERSHIP BOUNDARY, HELD ACROSS EVERY PHASE. If the lease were taken and released per phase, a
  // release, a standalone copy or a second resume could interleave in the gaps — which is exactly what
  // ADR-020 forbids between freeze and flip.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_orchestrator_acquires_operation_ownership_exactly_once()
  {
    var source = SourceOf("TenantCutoverOrchestrator.cs");

    // ⚠ COMPILE-CHECKED AGAINST THE LOCK THAT DECLARES THEM (252). As bare strings these asserted
    // nothing the day any of the three was renamed: the orchestrator would not mention the OLD name
    // either, so the search went quiet and the test stayed green while the rule stopped being enforced.
    // THE RENAME IS THE RISK HERE, NOT THE TYPO — a rename is routine, tool-driven and silent.
    Assert.Equal(1, Occurrences(source, nameof(TenantCutoverOperationLock.AcquireForSessionAsync)));
    Assert.DoesNotContain(nameof(TenantCutoverOperationLock.TryAcquireForSessionAsync), source, StringComparison.Ordinal);
    Assert.DoesNotContain(nameof(TenantCutoverOperationLock.TryAcquireForTransactionAsync), source, StringComparison.Ordinal);

    // Every phase after acquisition runs under it: the copy and flip are entered through their
    // under-ownership paths, never their standalone ones.
    Assert.Contains("CopyUnderOwnershipAsync", source, StringComparison.Ordinal);
    Assert.Contains("FlipUnderOwnershipAsync", source, StringComparison.Ordinal);
    Assert.DoesNotContain("copyService.CopyAsync", source, StringComparison.Ordinal);
    Assert.DoesNotContain("flipService.FlipAsync", source, StringComparison.Ordinal);
  }

  // ---- THE UNDER-OWNERSHIP PATHS CANNOT RE-ACQUIRE. This is the E4 self-deadlock made structurally
  // impossible: they demand proof of ownership in their signature, and the proof cannot be constructed
  // outside the lock helper that grants it.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void Owned_entry_points_require_proof_of_ownership_and_never_take_the_lock_again()
  {
    var ownership = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.TenantStorage.TenantCutoverOwnership")!;
    Assert.NotNull(ownership);

    // Not forgeable: no public constructor, and the only factory is internal to this assembly.
    Assert.Empty(ownership.GetConstructors(BindingFlags.Public | BindingFlags.Instance));

    foreach (var (file, method) in new[]
    {
      ("TenantCutoverCopyService.cs", "CopyUnderOwnershipAsync"),
      ("TenantCutoverRoutingFlipService.cs", "FlipUnderOwnershipAsync")
    })
    {
      var owned = InfrastructureAssembly.GetTypes()
        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        .Single(candidate => candidate.Name == method);

      Assert.Contains(owned.GetParameters(), parameter => parameter.ParameterType == ownership);

      // The owned path shares the core rather than duplicating it, so the two entry points cannot drift on
      // anything that decides correctness.
      Assert.Contains(
        method == "CopyUnderOwnershipAsync" ? "ExecuteAsync(" : "FlipCoreAsync(",
        SourceOf(file), StringComparison.Ordinal);
    }
  }

  // ---- EXACT VALIDATION AND THE RECOVERY GATE ARE BOTH MANDATORY, and both are rechecked while frozen
  // rather than trusted from preflight.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Validation_and_the_recovery_gate_are_both_rechecked_before_the_flip()
  {
    var source = SourceOf("TenantCutoverOrchestrator.cs");

    // The gate is consulted twice: once to avoid needless downtime, once as the authoritative check.
    Assert.Equal(2, Occurrences(source, "AuthorizeActivationAsync"));

    // Exact validation is not substituted by a flag, a timestamp or a checksum.
    foreach (var shortcut in ForbiddenValidationShortcuts)
    {
      Assert.DoesNotContain(shortcut, source, StringComparison.OrdinalIgnoreCase);
    }
  }

  // ---- FAILURE AFTER FROZEN DOES NOT UNFREEZE. Releasing on a failed copy would resume source writes
  // against an already-copied target and turn a retryable failure into an inconsistent one.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_orchestrator_never_releases_a_freeze_and_never_flips_back()
  {
    // CODE ONLY, NOT PROSE. The orchestrator's comments explain that it deliberately never unflips and
    // never unfreezes, so a raw text scan for the reversal verbs fails on the very sentences that state
    // the rule — punishing an accurate explanation and teaching the next author to delete it. Stripping
    // comments first keeps the guard exactly as strong against a real reversal call.
    var source = CodeOf("TenantCutoverOrchestrator.cs");

    Assert.DoesNotContain(nameof(TenantCutoverOperation.ReleaseFreeze), source, StringComparison.Ordinal);

    foreach (var reversal in ReversalVerbs)
    {
      Assert.DoesNotContain(reversal, source, StringComparison.OrdinalIgnoreCase);
    }

    foreach (var name in typeof(ITenantCutoverOrchestrator).GetMethods().Select(method => method.Name))
    {
      Assert.DoesNotContain("Abort", name, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("Cancel", name, StringComparison.OrdinalIgnoreCase);
    }
  }

  // ---- NO SOURCE CLEANUP, ANYWHERE. Retention is a separate operational capability that does not exist,
  // and a cutover that could delete the source is a cutover that can lose data to tidy up after itself.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_orchestrator_deletes_nothing()
  {
    var source = SourceOf("TenantCutoverOrchestrator.cs");

    foreach (var destructive in DestructiveTokens)
    {
      Assert.DoesNotContain(destructive, source, StringComparison.OrdinalIgnoreCase);
    }
  }

  // ---- A SERVICE, NOT A SURFACE. Activating a one-way operation on customer data is a separate
  // operational and security decision this slice does not take.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_orchestrator_is_neither_exposed_nor_self_starting()
  {
    Assert.All(Dependencies(Orchestrator), parameter => Assert.DoesNotContain(
      "Microsoft.AspNetCore", parameter.Namespace ?? string.Empty, StringComparison.Ordinal));

    Assert.False(typeof(Microsoft.Extensions.Hosting.IHostedService).IsAssignableFrom(Orchestrator));

    var source = SourceOf("TenantCutoverOrchestrator.cs");
    foreach (var activation in ActivationTokens)
    {
      Assert.DoesNotContain(activation, source, StringComparison.Ordinal);
    }
  }

  // ---- THE STALE-SOURCE FENCE SURVIVES COMPLETION. Marking orchestration finished must not readmit a
  // context bound to the database the tenant was moved off — the source is wrong forever, not just while
  // the cutover is in flight.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_write_gate_still_governs_after_the_cutover_is_completed()
  {
    var gate = typeof(TenantCutoverWriteGate);
    var permits = gate.GetMethod(nameof(TenantCutoverWriteGate.PermitsWriteTo))!;
    Assert.NotNull(permits);

    // Both post-flip statuses admit a TARGET write...
    foreach (var status in new[]
    {
      TenantCutoverOperationStatus.RoutingFlipped, TenantCutoverOperationStatus.Completed
    })
    {
      var subject = new TenantCutoverWriteGate(1, Guid.NewGuid(), 10, 20, status, null);
      Assert.True(subject.PermitsWriteTo(20));

      // ...and neither admits a SOURCE write.
      Assert.False(subject.PermitsWriteTo(10));
      Assert.False(subject.RefusesEveryWrite);
    }

    // Frozen refuses everything, target included: the copy is reading a source that must not move.
    var frozen = new TenantCutoverWriteGate(
      1, Guid.NewGuid(), 10, 20, TenantCutoverOperationStatus.Frozen, null);
    Assert.True(frozen.RefusesEveryWrite);

    // The lookup that feeds it spans Completed, or the gate above would never be consulted after
    // finalisation and a stale writer would be admitted straight into the old database.
    Assert.Contains(
      "TenantCutoverOperationStatus.Completed",
      SourceOf("TenantCutoverOperationStore.cs"),
      StringComparison.Ordinal);
  }

  // ---- THE ROUTING GUARD COVERS PHYSICAL DELETION (E4 review LOW-2).
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_routing_guard_trigger_rejects_physical_deletion_of_history()
  {
    var migration = MigrationSourceContaining("THROW 51022");
    Assert.NotNull(migration);

    var sql = Between(migration!, "CREATE TRIGGER", "/// <inheritdoc />");

    Assert.Contains("AFTER INSERT, UPDATE, DELETE", sql, StringComparison.Ordinal);
    Assert.Contains("THROW 51022", sql, StringComparison.Ordinal);

    // Still a guard, still set-based, still local.
    Assert.DoesNotContain("CURSOR", sql, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("WHILE ", sql, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("INSERT INTO", sql, StringComparison.OrdinalIgnoreCase);
    foreach (var forbidden in ForbiddenInTriggerSql)
    {
      Assert.DoesNotContain(forbidden, sql, StringComparison.OrdinalIgnoreCase);
    }
  }

  private static readonly string[] ForbiddenInOrchestrator =
  [
    "SqlBulkCopy", "WriteToServerAsync", "ColumnMappings",
    "TenantDatabaseAssignments.Add", "RoutingVersion =", "RecordRoutingFlip",
    "BeginTransactionAsync", "sp_getapplock"
  ];

  private static readonly string[] ForbiddenValidationShortcuts =
    ["CHECKSUM", "lastKnownGood", "SkipValidation", "AssumeValid", "TrustPreviousCopy"];

  private static readonly string[] ReversalVerbs =
    ["FlipBack", "Unflip", "Revert", "Rollback routing", "ReactivateShared"];

  private static readonly string[] DestructiveTokens =
    ["DROP DATABASE", "DROP TABLE", "TRUNCATE", "DELETE FROM", "SINGLE_USER", "Cleanup"];

  private static readonly string[] ActivationTokens =
    ["IHostedService", "BackgroundService", "CronExpression", "IEndpointRouteBuilder", "MapPost"];

  private static readonly string[] ForbiddenInTriggerSql =
    ["xp_cmdshell", "OPENQUERY", "sp_send_dbmail", "OPENROWSET"];

  private static int Occurrences(string source, string token)
  {
    var count = 0;
    var cursor = 0;
    while ((cursor = source.IndexOf(token, cursor, StringComparison.Ordinal)) >= 0)
    {
      count++;
      cursor += token.Length;
    }

    return count;
  }

  private static string Between(string source, string start, string end)
  {
    var from = source.IndexOf(start, StringComparison.Ordinal);
    Assert.True(from >= 0, $"expected marker not found: {start}");
    var to = source.IndexOf(end, from, StringComparison.Ordinal);
    return to < 0 ? source[from..] : source[from..to];
  }

  private static Type[] Dependencies(Type type) =>
    type.GetConstructors()
      .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
      .Distinct()
      .ToArray();

  private static string SourceOf(string fileName)
  {
    var full = Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "TenantStorage", fileName);
    Assert.True(File.Exists(full), $"expected source file not found: {full}");
    return File.ReadAllText(full);
  }

  // The same source with // comment bodies removed, for guards whose subject is what the code DOES.
  // Line comments only: this file's subjects are C# members, and the codebase writes explanation as //.
  private static string CodeOf(string fileName)
  {
    var lines = SourceOf(fileName).Split('\n');
    var stripped = lines.Select(line =>
    {
      var marker = line.IndexOf("//", StringComparison.Ordinal);
      return marker < 0 ? line : line[..marker];
    });

    return string.Join('\n', stripped);
  }

  private static string? MigrationSourceContaining(string token)
  {
    var directory = Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence", "Migrations");

    return Directory.EnumerateFiles(directory, "*.cs")
      .Select(File.ReadAllText)
      .FirstOrDefault(content => content.Contains(token, StringComparison.Ordinal));
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

  static TenantCutoverOrchestrationArchitectureTests() => Assert.NotNull(Orchestrator);
}
