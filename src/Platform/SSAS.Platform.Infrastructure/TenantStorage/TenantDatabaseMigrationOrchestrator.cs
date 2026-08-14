using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Fleet migration orchestration for physical tenant databases (ADR-018).
//
// This replaces the manual per-database command as the normal path for a platform-managed estate. It is
// invoked explicitly by deployment tooling or an operator — never from a request, never blindly at host
// startup.
//
// The per-database sequence is fixed and each step exists for a reason:
//   authority -> connect -> acquire ownership -> re-read state -> decide -> migrate -> verify -> record.
//
// Authority is checked FIRST so a database we may never touch is never even connected to for migration
// purposes. Ownership is acquired BEFORE any DDL and released only AFTER post-verification, so no
// concurrent writer can slip between applying and verifying.
public sealed class TenantDatabaseMigrationOrchestrator(
  ITenantDatabaseRegistryReadRepository readRepository,
  ITenantDatabaseConnectionFactory connectionFactory,
  ITenantDatabaseHealthWriter healthWriter,
  IDateTimeProvider clock) : ITenantDatabaseMigrationOrchestrator
{
  private const string MigrationActor = "tenant-storage-migration";

  private static readonly TimeSpan DefaultOwnershipTimeout = TimeSpan.FromSeconds(30);

  private const int PageSize = 50;

  public async Task<Result<TenantDatabaseMigrationOutcome>> MigrateAsync(
    long tenantDatabaseId,
    TenantMigrationRunOptions options,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(options);

    var page = await readRepository.ListPhysicalDatabasesAsync(tenantDatabaseId - 1, 1, cancellationToken);
    var descriptor = page.FirstOrDefault(item => item.TenantDatabaseId == tenantDatabaseId);
    return descriptor is null
      ? Result.Failure<TenantDatabaseMigrationOutcome>(TenantStorageErrors.TenantDatabaseNotReady)
      : Result.Success(await MigrateOneAsync(descriptor, options, cancellationToken));
  }

  public async Task<Result<TenantMigrationRunSummary>> RunAsync(
    TenantMigrationRunOptions options,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(options);

    var outcomes = new List<TenantDatabaseMigrationOutcome>();
    var afterId = 0L;

    // Keyset paging over PHYSICAL databases: one shared database is one target however many tenants it
    // hosts, and the estate is never materialised in memory.
    while (outcomes.Count < options.MaximumDatabases)
    {
      var page = await readRepository.ListPhysicalDatabasesAsync(
        afterId, Math.Min(PageSize, options.MaximumDatabases - outcomes.Count), cancellationToken);
      if (page.Count == 0)
      {
        break;
      }

      foreach (var descriptor in page)
      {
        cancellationToken.ThrowIfCancellationRequested();
        afterId = descriptor.TenantDatabaseId;

        // One database's failure never aborts the run: ADR-018 wants a complete report, and a release is
        // not judged failed because one customer database is unreachable.
        outcomes.Add(await MigrateOneAsync(descriptor, options, cancellationToken));
      }
    }

    return Result.Success(Summarise(outcomes));
  }

  private async Task<TenantDatabaseMigrationOutcome> MigrateOneAsync(
    TenantDatabaseDescriptor descriptor,
    TenantMigrationRunOptions options,
    CancellationToken cancellationToken)
  {
    // CustomerManaged has no runtime connectivity path (ADR-021). Report, never attempt.
    if (descriptor.HostingMode != TenantDatabaseHostingMode.PlatformManaged)
    {
      return Outcome(descriptor, TenantDatabaseMigrationOutcomeKind.NotVerifiable,
        "Customer-managed hosting has no runtime connectivity path; nothing was attempted.");
    }

    // AUTHORITY BEFORE ACTION. CustomerDba is never migrated by the platform; PlatformAfterApproval
    // without an explicit approval is denial, not default-allow.
    if (descriptor.MigrationManagementMode == TenantDatabaseMigrationManagementMode.CustomerDba ||
      (descriptor.MigrationManagementMode == TenantDatabaseMigrationManagementMode.PlatformAfterApproval &&
        !options.ApprovalGranted))
    {
      return await BlockAsync(descriptor, cancellationToken);
    }

    var connectionResult = connectionFactory.Create(new TenantDatabaseConnectionTarget(
      descriptor.ServerKey, descriptor.DatabaseName, descriptor.HostingMode));
    if (connectionResult.IsFailure)
    {
      return await UnreachableAsync(descriptor, connectionResult.Error.Code, cancellationToken);
    }

    await using var connection = connectionResult.Value;
    TenantDatabaseMigrationOwnership? ownership = null;
    try
    {
      await connection.OpenAsync(cancellationToken);

      // Bounded acquisition; failure is a clean skip that leaves state untouched.
      ownership = await TenantDatabaseMigrationOwnership.TryAcquireAsync(
        connection, options.OwnershipTimeout ?? DefaultOwnershipTimeout, cancellationToken);
      if (ownership is null)
      {
        return Outcome(descriptor, TenantDatabaseMigrationOutcomeKind.SkippedOwnershipHeld,
          "Migration ownership is held by another run.");
      }

      await using var context = TenantDbContextBuilder.ForConnection(connection);

      // State is re-read UNDER OWNERSHIP. Whatever the registry cached before we acquired the lock may
      // already be stale — another run may have just finished migrating this database.
      var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
      var known = context.Database.GetMigrations().ToArray();
      var compatibility = TenantDatabaseSchemaHealthService.Classify(applied, known);

      switch (compatibility)
      {
        case TenantDatabaseSchemaCompatibilityStatus.UpToDate:
          await RecordUpToDateAsync(descriptor, applied.LastOrDefault(), known.LastOrDefault(), cancellationToken);
          return Outcome(descriptor, TenantDatabaseMigrationOutcomeKind.AlreadyUpToDate, null, applied.LastOrDefault());

        case TenantDatabaseSchemaCompatibilityStatus.AheadOfApplication:
          // Never downgrade. An older application must not rewrite a newer database's schema.
          await RecordCompatibilityAsync(descriptor, compatibility, applied.LastOrDefault(), known.LastOrDefault(), cancellationToken);
          return Outcome(descriptor, TenantDatabaseMigrationOutcomeKind.AheadOfApplication,
            "The database has migrations this application does not know.");

        case TenantDatabaseSchemaCompatibilityStatus.MigrationHistoryMismatch:
          // Never append to an unrecognised lineage.
          await RecordCompatibilityAsync(descriptor, compatibility, applied.LastOrDefault(), known.LastOrDefault(), cancellationToken);
          return Outcome(descriptor, TenantDatabaseMigrationOutcomeKind.MigrationHistoryMismatch,
            "The database's migration history diverges from the deployed catalog.");
      }

      return await ApplyAsync(descriptor, context, ownership, known.LastOrDefault(), cancellationToken);
    }
    catch (SqlException exception)
    {
      return await FailAsync(descriptor, Summarise(exception), cancellationToken);
    }
    catch (DbUpdateException exception)
    {
      return await FailAsync(descriptor, Summarise(exception), cancellationToken);
    }
    catch (InvalidOperationException exception)
    {
      return await FailAsync(descriptor, Summarise(exception), cancellationToken);
    }
    finally
    {
      // Released only here — after apply AND verify (invariants 3 and 8).
      if (ownership is not null)
      {
        await ownership.DisposeAsync();
      }
    }
  }

  private async Task<TenantDatabaseMigrationOutcome> ApplyAsync(
    TenantDatabaseDescriptor descriptor,
    TenantDbContext context,
    TenantDatabaseMigrationOwnership ownership,
    string? targetMigration,
    CancellationToken cancellationToken)
  {
    await healthWriter.RecordHealthAsync(
      descriptor.TenantDatabaseId,
      database => database.BeginMigration(MigrationActor, clock.UtcNow),
      cancellationToken);

    // Each physical database migrates independently. EF's own per-migration transaction semantics are
    // preserved; wrapping the fleet — or even one database's whole stream — in an outer transaction would
    // fight the provider and make partial recovery worse, not better.
    await context.Database.MigrateAsync(cancellationToken);

    // POST-VERIFICATION under the same ownership. A successful call is not evidence; observed history is.
    if (!await ownership.IsStillHeldAsync(cancellationToken))
    {
      return await FailAsync(descriptor,
        TenantStorageErrors.MigrationOwnershipLost.Code, cancellationToken);
    }

    var verified = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
    var known = context.Database.GetMigrations().ToArray();
    if (TenantDatabaseSchemaHealthService.Classify(verified, known)
      != TenantDatabaseSchemaCompatibilityStatus.UpToDate)
    {
      return await FailAsync(descriptor,
        TenantStorageErrors.MigrationVerificationFailed.Code, cancellationToken);
    }

    var head = verified.LastOrDefault() ?? targetMigration;
    await healthWriter.RecordHealthAsync(
      descriptor.TenantDatabaseId,
      database =>
      {
        if (head is not null)
        {
          database.CompleteMigration(head, MigrationActor, clock.UtcNow);
        }

        database.RecordSchemaHealth(
          TenantDatabaseSchemaCompatibilityStatus.UpToDate, head, known.LastOrDefault(),
          MigrationActor, clock.UtcNow);
      },
      cancellationToken);

    return Outcome(descriptor, TenantDatabaseMigrationOutcomeKind.Migrated, null, head);
  }

  private async Task<TenantDatabaseMigrationOutcome> BlockAsync(
    TenantDatabaseDescriptor descriptor,
    CancellationToken cancellationToken)
  {
    const string reason = "Pending migrations exist but the migration management mode does not permit the platform to apply them.";
    await healthWriter.RecordHealthAsync(
      descriptor.TenantDatabaseId,
      database => database.BlockPendingCustomer(reason, MigrationActor, clock.UtcNow),
      cancellationToken);

    return Outcome(descriptor, TenantDatabaseMigrationOutcomeKind.BlockedPendingCustomer, reason);
  }

  private async Task<TenantDatabaseMigrationOutcome> UnreachableAsync(
    TenantDatabaseDescriptor descriptor,
    string detail,
    CancellationToken cancellationToken)
  {
    await healthWriter.RecordHealthAsync(
      descriptor.TenantDatabaseId,
      database => database.RecordConnectivity(
        TenantDatabaseConnectivityStatus.Unreachable, MigrationActor, clock.UtcNow),
      cancellationToken);

    return Outcome(descriptor, TenantDatabaseMigrationOutcomeKind.Unreachable, detail);
  }

  private async Task<TenantDatabaseMigrationOutcome> FailAsync(
    TenantDatabaseDescriptor descriptor,
    string detail,
    CancellationToken cancellationToken)
  {
    await healthWriter.RecordHealthAsync(
      descriptor.TenantDatabaseId,
      database => database.FailMigration(detail, MigrationActor, clock.UtcNow),
      cancellationToken);

    return Outcome(descriptor, TenantDatabaseMigrationOutcomeKind.Failed, detail);
  }

  private Task RecordUpToDateAsync(
    TenantDatabaseDescriptor descriptor, string? applied, string? target, CancellationToken cancellationToken) =>
    healthWriter.RecordHealthAsync(
      descriptor.TenantDatabaseId,
      database =>
      {
        database.RecordConnectivity(TenantDatabaseConnectivityStatus.Healthy, MigrationActor, clock.UtcNow);
        database.RecordSchemaHealth(
          TenantDatabaseSchemaCompatibilityStatus.UpToDate, applied, target, MigrationActor, clock.UtcNow);
      },
      cancellationToken);

  private Task RecordCompatibilityAsync(
    TenantDatabaseDescriptor descriptor,
    TenantDatabaseSchemaCompatibilityStatus status,
    string? applied,
    string? target,
    CancellationToken cancellationToken) =>
    healthWriter.RecordHealthAsync(
      descriptor.TenantDatabaseId,
      database =>
      {
        database.RecordConnectivity(TenantDatabaseConnectivityStatus.Healthy, MigrationActor, clock.UtcNow);
        database.RecordSchemaHealth(status, applied, target, MigrationActor, clock.UtcNow);
      },
      cancellationToken);

  private static TenantDatabaseMigrationOutcome Outcome(
    TenantDatabaseDescriptor descriptor,
    TenantDatabaseMigrationOutcomeKind kind,
    string? detail,
    string? appliedMigration = null) =>
    new(descriptor.TenantDatabaseId, kind, appliedMigration, detail);

  // Exception TYPE and message only. A connection string, credential or endpoint must never reach a
  // persisted operator-facing field.
  private static string Summarise(Exception exception) =>
    $"{exception.GetType().Name}: {exception.Message}";

  private static TenantMigrationRunSummary Summarise(IReadOnlyCollection<TenantDatabaseMigrationOutcome> outcomes) =>
    new(
      outcomes.Count,
      outcomes.Count(item => item.Kind == TenantDatabaseMigrationOutcomeKind.AlreadyUpToDate),
      outcomes.Count(item => item.Kind == TenantDatabaseMigrationOutcomeKind.Migrated),
      outcomes.Count(item => item.Kind == TenantDatabaseMigrationOutcomeKind.Failed),
      outcomes.Count(item => item.Kind == TenantDatabaseMigrationOutcomeKind.SkippedOwnershipHeld),
      outcomes.Count(item => item.Kind == TenantDatabaseMigrationOutcomeKind.BlockedPendingCustomer),
      outcomes.Count(item => item.Kind == TenantDatabaseMigrationOutcomeKind.Unreachable),
      outcomes.Count(item => item.Kind == TenantDatabaseMigrationOutcomeKind.AheadOfApplication),
      outcomes.Count(item => item.Kind == TenantDatabaseMigrationOutcomeKind.MigrationHistoryMismatch),
      outcomes.Count(item => item.Kind == TenantDatabaseMigrationOutcomeKind.NotVerifiable),
      outcomes);
}
