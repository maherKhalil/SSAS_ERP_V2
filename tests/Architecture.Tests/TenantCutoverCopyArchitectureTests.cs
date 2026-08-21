using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Architecture.Tests;

// THE BOUNDARIES OF THE COPY ENGINE (ADR-020, TS-Storage Phase E3).
//
// A copy engine is the component with the most reach in the whole cutover: it holds credentials to two
// databases, reads every tenant-owned table, and writes freely into one of them. These guards keep it doing
// exactly one thing — reproduce a tenant's data and prove it — and keep the decisions that belong to the
// routing flip out of it.
public sealed class TenantCutoverCopyArchitectureTests
{
  private static readonly Assembly InfrastructureAssembly = typeof(TenantCutoverWriteFence).Assembly;

  private static readonly Type CopyService = InfrastructureAssembly.GetType(
    "SSAS.Platform.Infrastructure.TenantStorage.TenantCutoverCopyService")!;

  // ---- E3 COPIES; IT DOES NOT FLIP. No assignment write, no RoutingVersion write, no cache invalidation.
  // The target stays unroutable, which is what makes the flip a separate, atomic decision.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_copy_slice_never_mutates_routing()
  {
    foreach (var type in CopyTypes())
    {
      var reachable = Dependencies(type);

      // It cannot even express an assignment change: the aggregate is not reachable from its surface.
      Assert.DoesNotContain(typeof(TenantDatabaseAssignment), reachable);
      Assert.DoesNotContain(typeof(ITenantRoutingCacheInvalidator), reachable);
      Assert.DoesNotContain(typeof(ITenantRoutingCache), reachable);
    }

    // The engine's source text never writes RoutingVersion or an assignment, and never invalidates.
    var source = SourceOf("TenantCutoverCopyService.cs");
    foreach (var forbidden in new[]
    {
      "RoutingVersion =", "TenantDatabaseAssignments.Add", "Invalidate(", "RoutingFlipped", "Completed"
    })
    {
      Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
    }
  }

  // ---- EVERY SOURCE READ IS TENANT-FILTERED, IN THE SQL. On a shared source the co-tenants' rows are in
  // the same table, so a read that filtered afterwards would still have pulled another tenant's data into
  // this process — and one that forgot entirely would copy it.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void Every_copy_and_validation_query_filters_by_tenant()
  {
    foreach (var file in new[] { "TenantCutoverTableCopier.cs", "TenantCutoverCopyValidator.cs" })
    {
      var source = SourceOf(file);
      var selects = source.Split("FROM {table.QualifiedName}", StringSplitOptions.None);

      // Every FROM against a tenant table is followed by a TenantId predicate in the same statement.
      for (var index = 1; index < selects.Length; index++)
      {
        var statement = selects[index];
        var terminator = statement.IndexOf(';', StringComparison.Ordinal);
        var clause = terminator < 0 ? statement : statement[..terminator];
        Assert.Contains("[{table.TenantIdColumn}]", clause, StringComparison.Ordinal);
      }

      // The predicate is parameterised, never interpolated from a caller's value.
      Assert.Contains("@TenantId", source, StringComparison.Ordinal);
      Assert.DoesNotContain("= '{tenantId", source, StringComparison.Ordinal);
    }
  }

  // ---- TRIGGERS ARE NOT FIRED BY THE COPY. SqlBulkCopy does not fire them unless asked, and asking would
  // let an audit or history trigger re-stamp rows the copy is contractually required to reproduce verbatim.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_copy_never_enables_trigger_firing_and_never_disables_constraints()
  {
    var source = SourceOf("TenantCutoverTableCopier.cs");

    Assert.DoesNotContain("FireTriggers", source, StringComparison.Ordinal);
    Assert.DoesNotContain("NOCHECK", source, StringComparison.Ordinal);
    Assert.DoesNotContain("DISABLE TRIGGER", source, StringComparison.Ordinal);

    // Constraints are ENFORCED during the copy rather than trusted afterwards.
    Assert.Contains("SqlBulkCopyOptions.CheckConstraints", source, StringComparison.Ordinal);

    // ...and the exact-preservation options are present rather than defaulted.
    Assert.Contains("SqlBulkCopyOptions.KeepNulls", source, StringComparison.Ordinal);
    Assert.Contains("SqlBulkCopyOptions.KeepIdentity", source, StringComparison.Ordinal);
  }

  // ---- CROSS-INSTANCE CAPABLE. No three-part name, no linked server, no cross-database INSERT..SELECT:
  // any of those would silently require source and target to live on the same SQL Server instance.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_copy_mechanism_does_not_require_one_instance()
  {
    foreach (var file in new[] { "TenantCutoverTableCopier.cs", "TenantCutoverCopyValidator.cs" })
    {
      var source = SourceOf(file);
      Assert.DoesNotContain("OPENQUERY", source, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("OPENROWSET", source, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("sp_addlinkedserver", source, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("INSERT INTO", source, StringComparison.OrdinalIgnoreCase);
    }
  }

  // ---- NO DESTRUCTION, ANYWHERE. A copy that could delete is a copy that can lose data to make itself
  // succeed, and the LOW-A cleanup gate is still open besides.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_copy_slice_contains_no_destructive_or_cleanup_path()
  {
    foreach (var file in new[]
    {
      "TenantCutoverCopyService.cs", "TenantCutoverTableCopier.cs",
      "TenantCutoverCopyValidator.cs", "TenantCutoverCopyPlan.cs"
    })
    {
      var source = SourceOf(file);
      foreach (var destructive in new[]
      {
        "DROP DATABASE", "DROP TABLE", "TRUNCATE", "DELETE FROM", "SINGLE_USER", "MERGE "
      })
      {
        Assert.DoesNotContain(destructive, source, StringComparison.OrdinalIgnoreCase);
      }
    }
  }

  // ================================================================================================
  // THE COPIER STREAMS THE READER IT OPENS. THIS IS THE BUFFERING HALF OF THE STREAMING CLAIM.
  // ================================================================================================
  //
  // Its counterpart is Integration.Tests'
  // A_large_tenant_copies_by_streaming_and_every_query_seeks, which defends the OTHER half: that the copy
  // walks the clustered index in key order and never sorts, so a large tenant does not spill to tempdb.
  //
  // That test used to assert an allocation budget as well. It was removed on 2026-08-21 because it could
  // not discriminate: `GC.GetTotalAllocatedBytes` is cumulative, the transient reader buffers of a
  // STREAMING copy (74MB for 20000 rows) already exceed the cost of RETAINING the entities (~12-36MB), and
  // the ratio is row-count-invariant. Server statement metrics cannot see it either — the copier issues one
  // unbounded SELECT and `BatchSize` is a write-side SqlBulkCopy option, so the statement shape is
  // identical for both designs.
  //
  // So the property is guarded where it actually lives: in the SOURCE. Streaming here means the live reader
  // reaches WriteToServerAsync and nothing drains it into a collection on the way.
  //
  // ---- TOKEN GUARDS ARE CRUDE, DELIBERATELY, AND THIS ONE HAS A SPECIFIC FAILURE INSTRUCTION.
  //
  // If it fails, the fix is to RE-ESTABLISH the streaming structure and then update this guard and its
  // comment together. It is NOT to delete the offending token: a `ToList` between the reader and the bulk
  // copy would load a whole tenant into this process's memory, which is the defect the copier's own header
  // comment says it exists to avoid.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_table_copier_streams_the_reader_it_opens()
  {
    var source = SourceOf("TenantCutoverTableCopier.cs");

    // PRESENT: the reader is opened, and handed to the bulk copy alive, with buffering switched off.
    foreach (var required in new[]
    {
      "EnableStreaming = true",
      "await command.ExecuteReaderAsync(cancellationToken)",
      "WriteToServerAsync(reader"
    })
    {
      Assert.Contains(required, source, StringComparison.Ordinal);
    }

    // ABSENT: every shape that would put the rows in memory first. `DataTable` and the LINQ materialisers
    // are the obvious ones; the manual read loop is the subtle one, because draining a reader by hand into
    // anything is exactly the design this forbids while looking like ordinary ADO.NET.
    foreach (var buffering in new[]
    {
      "DataTable",
      "ToList",
      "ToArray",
      "while (await reader.ReadAsync"
    })
    {
      Assert.DoesNotContain(buffering, source, StringComparison.Ordinal);
    }
  }

  // ---- A PRIMITIVE, NOT A SERVICE SURFACE. No HTTP, no scheduler: orchestration is the next slice, and a
  // copy that could start itself would start during an incident.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_copy_is_neither_exposed_over_http_nor_scheduled()
  {
    foreach (var type in CopyTypes())
    {
      Assert.All(Dependencies(type), parameter => Assert.DoesNotContain(
        "Microsoft.AspNetCore", parameter.Namespace ?? string.Empty, StringComparison.Ordinal));

      Assert.False(
        typeof(Microsoft.Extensions.Hosting.IHostedService).IsAssignableFrom(type),
        $"{type.Name} is a hosted service, so the copy can run unattended");
    }
  }

  // ---- V1 PROMOTES PLATFORM-MANAGED SHARED TO PLATFORM-MANAGED DEDICATED. The refusal lives in the engine
  // rather than in a caller that might forget.
  [Fact]
  [Trait("Decision", "ADR-021")]
  public void The_copy_refuses_anything_but_platform_managed_shared_to_dedicated()
  {
    var source = SourceOf("TenantCutoverCopyService.cs");

    Assert.Contains("TenantDatabaseHostingMode.PlatformManaged", source, StringComparison.Ordinal);
    Assert.Contains("TenantDatabaseStorageMode.Shared", source, StringComparison.Ordinal);
    Assert.Contains("TenantDatabaseStorageMode.Dedicated", source, StringComparison.Ordinal);

    // The enum-qualified token is what a code path permitting CustomerManaged would have to contain. The
    // bare word appears in the type comment explaining WHY it is excluded, and a guard that banned the word
    // would be a guard against documenting the decision.
    Assert.DoesNotContain(
      "TenantDatabaseHostingMode.CustomerManaged", source, StringComparison.Ordinal);
  }

  // ---- LOW-1: EVERY EF WRITE ENTRY POINT IS COVERED, AND EXACTLY ONCE.
  //
  // EF Core routes `SaveChangesAsync(ct)` to `SaveChangesAsync(bool, ct)` and `SaveChanges()` to
  // `SaveChanges(bool)`. Hooking the inner pair covers all four; ALSO hooking the outer pair would fence the
  // same write twice, taking the application lock twice for one save. This asserts both halves.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_tenant_write_fence_covers_every_save_changes_entry_point_exactly_once()
  {
    var context = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.Persistence.TenantErp.TenantDbContext")!;

    Assert.NotNull(Declared(context, nameof(DbContext.SaveChangesAsync), typeof(bool), typeof(CancellationToken)));
    Assert.NotNull(Declared(context, nameof(DbContext.SaveChanges), typeof(bool)));

    // The convenience overloads are deliberately NOT overridden here.
    Assert.Null(Declared(context, nameof(DbContext.SaveChangesAsync), typeof(CancellationToken)));
    Assert.Null(Declared(context, nameof(DbContext.SaveChanges)));

    // The synchronous path fails closed rather than saving unfenced.
    var source = SourceOf("Persistence/TenantErp/TenantDbContext.cs");
    Assert.Contains("Synchronous SaveChanges is not supported", source, StringComparison.Ordinal);

    // Auditing and the tenant-ownership guard hook the same inner pair, so no entry point writes unstamped.
    var persistence = typeof(PersistenceDbContext);
    Assert.NotNull(Declared(persistence, nameof(DbContext.SaveChangesAsync), typeof(bool), typeof(CancellationToken)));
    Assert.NotNull(Declared(persistence, nameof(DbContext.SaveChanges), typeof(bool)));
    Assert.Null(Declared(persistence, nameof(DbContext.SaveChangesAsync), typeof(CancellationToken)));
  }

  // ---- E1 AND E2 SURVIVE E3. The fence is still mandatory on the routed context, and the version-aware
  // resolver is still what routing consumers receive.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_write_fence_and_the_version_aware_resolver_are_still_in_place()
  {
    var context = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.Persistence.TenantErp.TenantDbContext")!;
    Assert.Contains(
      context.GetConstructors().SelectMany(constructor => constructor.GetParameters()),
      parameter => parameter.ParameterType == typeof(ITenantWriteFence));

    var factory = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.Persistence.TenantErp.TenantDbContextFactory")!;
    Assert.Contains(Dependencies(factory), parameter => parameter == typeof(ITenantWriteFence));

    // Only the version-aware resolver holds the route cache — E3 introduced no second routing path.
    var cacheConsumers = new[] { typeof(VersionAwareTenantDatabaseResolver).Assembly, InfrastructureAssembly }
      .SelectMany(assembly => assembly.GetTypes())
      .Where(type => !type.IsNested && Dependencies(type).Contains(typeof(ITenantRoutingCache)))
      .ToArray();
    Assert.Equal([typeof(VersionAwareTenantDatabaseResolver)], cacheConsumers);
  }

  // ---- THE COPY AND THE FREEZE USE DIFFERENT LOCKS, ON PURPOSE. The freeze fences tenant writes in the
  // SOURCE database; the copy owns an operation in the PLATFORM database. Sharing a resource name would
  // make a copy block ordinary writes for a different reason than the freeze does.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_operation_ownership_lock_is_distinct_from_the_tenant_write_fence_lock()
  {
    var operationLock = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.TenantStorage.TenantCutoverOperationLock")!;
    var prefix = (string)operationLock.GetField("Prefix", BindingFlags.Public | BindingFlags.Static)!
      .GetValue(null)!;

    Assert.NotEqual(TenantCutoverLockResource.Prefix, prefix);
    Assert.DoesNotContain(prefix, TenantCutoverLockResource.Prefix, StringComparison.Ordinal);

    // Keyed per operation, never fleet-wide.
    var forOperation = operationLock.GetMethod("ForOperation", BindingFlags.Public | BindingFlags.Static)!;
    Assert.NotEqual(
      (string)forOperation.Invoke(null, [1L])!,
      (string)forOperation.Invoke(null, [2L])!);
  }

  private static MethodInfo? Declared(Type type, string name, params Type[] parameters) =>
    type.GetMethod(
      name,
      BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
      binder: null,
      parameters,
      modifiers: null);

  private static IEnumerable<Type> CopyTypes() =>
    InfrastructureAssembly.GetTypes()
      .Where(type => !type.IsNested &&
        type.Namespace?.Contains("TenantStorage", StringComparison.Ordinal) == true &&
        type.Name.Contains("Copy", StringComparison.Ordinal));

  private static Type[] Dependencies(Type type) =>
    type.GetConstructors()
      .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
      .Distinct()
      .ToArray();

  private static string SourceOf(string relativePath)
  {
    var full = Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure",
      relativePath.Contains('/', StringComparison.Ordinal)
        ? relativePath.Replace('/', Path.DirectorySeparatorChar)
        : Path.Combine("TenantStorage", relativePath));

    Assert.True(File.Exists(full), $"expected source file not found: {full}");
    return File.ReadAllText(full);
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

  static TenantCutoverCopyArchitectureTests() => Assert.NotNull(CopyService);
}
