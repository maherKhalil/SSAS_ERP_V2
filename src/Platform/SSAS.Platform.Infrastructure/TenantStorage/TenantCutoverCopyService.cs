using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// THE SHARED → DEDICATED COPY, END TO END (ADR-020, TS-Storage Phase E3).
//
// THE ORDER OF THE GATES IS THE SAFETY ARGUMENT, and each one is refused rather than repaired:
//
//   1. the operation is FROZEN                    — Preparing means the source is still writable
//   2. endpoints still match, and the tenant still routes to the recorded source
//   3. OWNERSHIP of the operation is acquired     — one copy per cutover, and no release underneath it
//   4. the operation is re-read UNDER ownership   — it may have been released between 1 and 3
//   5. both schemas are current                   — copying across a schema difference validates nothing
//   6. the target holds no other tenant's rows    — a "dedicated" database that is not
//   7. per table: copy or prove already-copied, inside a target transaction
//   8. the operation is still Frozen at the end   — the source did not move while we read it
//
// TRIGGERS ARE NOT FIRED, and that is a decision rather than a default.
//
// The tenant schema carries exactly ONE DML trigger: `tenant.TR_Companies_PreventDelete`, an INSTEAD OF
// DELETE trigger that refuses physical deletion of Company rows. It is therefore NOT on this path at all —
// the copy only ever INSERTs — and SqlBulkCopy does not fire triggers unless asked, which it is not.
//
// Both halves matter. The trigger's invariant is UNAFFECTED: it protects the target exactly as it protects
// any other tenant database, and it independently makes the engine's "never delete to make progress" rule
// unbreakable rather than merely intended. And FireTriggers stays off because this copy's contract is to
// reproduce audit values VERBATIM — an audit or history trigger firing on insert would do exactly what it
// is designed to do and stamp the copy with today's timestamp and this process's identity, destroying the
// property the copy exists to preserve. A future insert trigger carrying a durable invariant would have to
// be handled explicitly here rather than by turning FireTriggers on and hoping.
//
// NOTHING HERE TOUCHES ROUTING. No assignment write, no RoutingVersion increment, no cache invalidation, no
// resolver interaction beyond reading where the tenant currently routes. The target stays unroutable.
internal sealed class TenantCutoverCopyService(
  ITenantCutoverOperationStore operations,
  ITenantDatabaseConnectionFactory connectionFactory,
  Persistence.PlatformDbContext platform,
  IOptions<TenantCutoverCopyOptions> optionsAccessor) : ITenantCutoverCopyService
{
  public async Task<Result<TenantCutoverCopyReport>> CopyAsync(
    long cutoverOperationId,
    CancellationToken cancellationToken = default)
  {
    var options = optionsAccessor.Value;

    var eligibility = await ResolveEligibleEndpointsAsync(cutoverOperationId, cancellationToken);
    if (eligibility.IsFailure)
    {
      return Result.Failure<TenantCutoverCopyReport>(eligibility.Error);
    }

    // ---- OWNERSHIP. Session-owned on a connection this run holds for its whole duration, so a dead
    // process releases it without a lease to expire. A release attempt contends for the same resource.
    await using var ownership = new SqlConnection(platform.Database.GetConnectionString());
    await ownership.OpenAsync(cancellationToken);
    if (!await TenantCutoverOperationLock.TryAcquireForSessionAsync(
      ownership, cutoverOperationId, options.OwnershipTimeout, cancellationToken))
    {
      return Result.Failure<TenantCutoverCopyReport>(TenantStorageErrors.CutoverCopyOwnershipNotAcquired);
    }

    // ---- RE-READ UNDER OWNERSHIP. Between the first check and acquiring the lock, a release could have
    // committed. Everything after this point is protected by the lock; this closes the window before it.
    var confirmed = await operations.FindAsync(cutoverOperationId, cancellationToken);
    if (confirmed is null || confirmed.Status != TenantCutoverOperationStatus.Frozen)
    {
      return Result.Failure<TenantCutoverCopyReport>(TenantStorageErrors.CutoverOperationNotFrozen);
    }

    return await ExecuteAsync(eligibility.Value, cutoverOperationId, copyMissing: true, cancellationToken);
  }

  // THE SAME COPY, FOR A CALLER THAT ALREADY OWNS THE OPERATION (TS-Storage Phase E5).
  //
  // The orchestrator holds one session-scoped ownership lease across freeze, copy, flip and finalisation.
  // Calling the public CopyAsync from inside that would try to take the same resource on a second
  // connection and deadlock against itself — the exact defect E4 review found. Requiring the ownership
  // token in the signature makes this path impossible to enter without ownership and impossible to confuse
  // with the standalone one.
  //
  // IT SHARES THE CORE. Eligibility, the schema gate, contamination and the per-table copy/validate loop
  // are the same code as the public path; only the acquisition differs, so the two can never drift on
  // anything that decides correctness.
  public async Task<Result<TenantCutoverCopyReport>> CopyUnderOwnershipAsync(
    TenantCutoverOwnership ownership,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(ownership);

    var eligibility = await ResolveEligibleEndpointsAsync(
      ownership.CutoverOperationId, cancellationToken);
    return eligibility.IsFailure
      ? Result.Failure<TenantCutoverCopyReport>(eligibility.Error)
      : await ExecuteAsync(
        eligibility.Value, ownership.CutoverOperationId, copyMissing: true, cancellationToken);
  }

  // Deliberately acquires NO ownership — see the interface comment. The flip calls this while already
  // holding the operation, and taking the same resource on a second connection would deadlock it.
  public async Task<Result<TenantCutoverCopyReport>> ValidateAsync(
    long cutoverOperationId,
    CancellationToken cancellationToken = default)
  {
    var eligibility = await ResolveEligibleEndpointsAsync(cutoverOperationId, cancellationToken);
    return eligibility.IsFailure
      ? Result.Failure<TenantCutoverCopyReport>(eligibility.Error)
      : await ExecuteAsync(eligibility.Value, cutoverOperationId, copyMissing: false, cancellationToken);
  }

  private async Task<Result<TenantCutoverCopyReport>> ExecuteAsync(
    CutoverEndpoints endpoints,
    long cutoverOperationId,
    bool copyMissing,
    CancellationToken cancellationToken)
  {
    var options = optionsAccessor.Value;

    var source = connectionFactory.Create(endpoints.Source);
    if (source.IsFailure)
    {
      return Result.Failure<TenantCutoverCopyReport>(source.Error);
    }

    var target = connectionFactory.Create(endpoints.Target);
    if (target.IsFailure)
    {
      return Result.Failure<TenantCutoverCopyReport>(target.Error);
    }

    await using var sourceConnection = source.Value;
    await using var targetConnection = target.Value;
    await sourceConnection.OpenAsync(cancellationToken);
    await targetConnection.OpenAsync(cancellationToken);

    // ---- SCHEMA GATE. Both sides must be at the full deployed tenant catalog. Deliberately does NOT
    // migrate the target as a side effect: applying DDL to a database because a copy wanted to proceed is
    // not a copy's decision to make (§16).
    var schema = await BothSchemasCurrentAsync(sourceConnection, targetConnection, cancellationToken);
    if (schema.IsFailure)
    {
      return Result.Failure<TenantCutoverCopyReport>(schema.Error);
    }

    var plan = TenantCutoverCopyPlan.Build(TenantDbContextBuilder.TenantModel);
    if (plan.IsFailure)
    {
      return Result.Failure<TenantCutoverCopyReport>(plan.Error);
    }

    var validator = new TenantCutoverCopyValidator(options);
    var copier = new TenantCutoverTableCopier(options);

    // ---- CONTAMINATION, CHECKED ACROSS EVERY TABLE BEFORE ANY IS WRITTEN. Discovering a foreign tenant's
    // rows halfway through would mean having already written into a database that is not what it claims.
    foreach (var table in plan.Value)
    {
      if (await validator.CountForeignTenantRowsAsync(
        table, endpoints.TenantId, targetConnection, cancellationToken) > 0)
      {
        return Result.Failure<TenantCutoverCopyReport>(TenantStorageErrors.CutoverTargetContaminated);
      }
    }

    var tables = new List<TenantCutoverTableReport>(plan.Value.Count);
    foreach (var table in plan.Value)
    {
      var outcome = await CopyOrVerifyAsync(
        table, endpoints.TenantId, sourceConnection, targetConnection, copier, validator,
        copyMissing, cancellationToken);
      if (outcome.IsFailure)
      {
        return Result.Failure<TenantCutoverCopyReport>(outcome.Error);
      }

      tables.Add(outcome.Value);
    }

    // ---- THE SOURCE WAS FROZEN THROUGHOUT. Ownership already makes a successful release impossible while
    // this ran; re-reading says so from the durable record rather than from that argument.
    var final = await operations.FindAsync(cutoverOperationId, cancellationToken);
    if (final is null || final.Status != TenantCutoverOperationStatus.Frozen)
    {
      return Result.Failure<TenantCutoverCopyReport>(TenantStorageErrors.CutoverOperationNotFrozen);
    }

    return Result.Success(new TenantCutoverCopyReport(
      cutoverOperationId, endpoints.TenantId,
      endpoints.SourceTenantDatabaseId, endpoints.TargetTenantDatabaseId, tables));
  }

  // ONE TABLE: either it is not there and we copy it, or it is there and we prove it exact. There is no
  // third branch that writes over existing rows.
  private static async Task<Result<TenantCutoverTableReport>> CopyOrVerifyAsync(
    TenantCutoverTablePlan table,
    Guid tenantId,
    SqlConnection source,
    SqlConnection target,
    TenantCutoverTableCopier copier,
    TenantCutoverCopyValidator validator,
    bool copyMissing,
    CancellationToken cancellationToken)
  {
    // VALIDATION-ONLY NEVER WRITES. An absent or incomplete table is a refusal here, not something to
    // finish: the flip is asking "is the target already exactly this tenant's data", and copying to make
    // the answer yes would be answering a different question.
    //
    // It does not count first: the lockstep walk below decides emptiness, divergence and completeness
    // alike, so a preceding COUNT was a per-table round trip whose result nothing read.
    if (!copyMissing)
    {
      var proof = await validator.ValidateAsync(table, tenantId, source, target, null, cancellationToken);
      return proof.IsExact
        ? Result.Success(new TenantCutoverTableReport(
          table.EntityName, table.TableName, proof.Rows, TenantCutoverTableDisposition.AlreadyComplete))
        : Result.Failure<TenantCutoverTableReport>(TenantStorageErrors.CutoverTargetInconsistent);
    }

    var existing = await validator.CountTenantRowsAsync(table, tenantId, target, null, cancellationToken);

    if (existing > 0)
    {
      // THE RESUME PATH. Rows are already here; the only acceptable explanation is that a previous
      // execution committed this table. Anything else — partial, extra, divergent — fails closed, because
      // "make it match" would mean deleting or overwriting customer data on the strength of a guess.
      var revalidated = await validator.ValidateAsync(
        table, tenantId, source, target, null, cancellationToken);

      return revalidated.IsExact
        ? Result.Success(new TenantCutoverTableReport(
          table.EntityName, table.TableName, revalidated.Rows,
          TenantCutoverTableDisposition.AlreadyComplete))
        : Result.Failure<TenantCutoverTableReport>(TenantStorageErrors.CutoverTargetInconsistent);
    }

    // ---- ATOMIC PER TABLE. The insert and the validation share one transaction, so a table is committed
    // only once it has been proven exact — and a process that dies mid-table leaves NO rows behind rather
    // than a partial table a later run might mistake for a complete one.
    await using var transaction = (SqlTransaction)await target.BeginTransactionAsync(cancellationToken);

    try
    {
      await copier.CopyAsync(table, tenantId, source, target, transaction, cancellationToken);
    }
    catch (SqlException)
    {
      await transaction.RollbackAsync(CancellationToken.None);
      return Result.Failure<TenantCutoverTableReport>(TenantStorageErrors.CutoverCopyFailed);
    }

    var validation = await validator.ValidateAsync(
      table, tenantId, source, target, transaction, cancellationToken);
    if (!validation.IsExact)
    {
      await transaction.RollbackAsync(CancellationToken.None);
      return Result.Failure<TenantCutoverTableReport>(TenantStorageErrors.CutoverCopyValidationFailed);
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success(new TenantCutoverTableReport(
      table.EntityName, table.TableName, validation.Rows, TenantCutoverTableDisposition.Copied));
  }

  private async Task<Result<CutoverEndpoints>> ResolveEligibleEndpointsAsync(
    long cutoverOperationId,
    CancellationToken cancellationToken)
  {
    var operation = await operations.FindAsync(cutoverOperationId, cancellationToken);
    if (operation is null)
    {
      return Result.Failure<CutoverEndpoints>(TenantStorageErrors.CutoverOperationNotFound);
    }

    // Preparing (source still writable) and every post-flip state (source no longer authoritative) are
    // equally wrong to copy from, and are reported as the same fact: this is not a frozen operation.
    if (operation.Status != TenantCutoverOperationStatus.Frozen)
    {
      return Result.Failure<CutoverEndpoints>(TenantStorageErrors.CutoverOperationNotFrozen);
    }

    var databases = await platform.TenantDatabases
      .AsNoTracking()
      .Where(database => database.Id == operation.SourceTenantDatabaseId ||
        database.Id == operation.TargetTenantDatabaseId)
      .Select(database => new
      {
        database.Id, database.ServerKey, database.DatabaseName, database.HostingMode, database.StorageMode
      })
      .ToListAsync(cancellationToken);

    var source = databases.SingleOrDefault(database => database.Id == operation.SourceTenantDatabaseId);
    var target = databases.SingleOrDefault(database => database.Id == operation.TargetTenantDatabaseId);

    // E3 SUPPORTS EXACTLY ONE SHAPE: platform-managed Shared to platform-managed Dedicated. CustomerManaged
    // has no platform copy path at either end (ADR-021).
    if (source is null ||
      source.HostingMode != TenantDatabaseHostingMode.PlatformManaged ||
      source.StorageMode != TenantDatabaseStorageMode.Shared)
    {
      return Result.Failure<CutoverEndpoints>(TenantStorageErrors.CutoverSourceNotEligible);
    }

    if (target is null ||
      target.HostingMode != TenantDatabaseHostingMode.PlatformManaged ||
      target.StorageMode != TenantDatabaseStorageMode.Dedicated)
    {
      return Result.Failure<CutoverEndpoints>(TenantStorageErrors.CutoverTargetNotEligible);
    }

    // The tenant must still route to the recorded source. If routing already moved, this operation's
    // premise is gone and copying from a database the tenant no longer uses would copy stale data.
    var activeAssignment = await platform.TenantDatabaseAssignments
      .AsNoTracking()
      .Where(assignment => assignment.TenantId == operation.TenantId && assignment.EndedUtc == null)
      .Select(assignment => (long?)assignment.TenantDatabaseId)
      .SingleOrDefaultAsync(cancellationToken);

    if (activeAssignment != operation.SourceTenantDatabaseId)
    {
      return Result.Failure<CutoverEndpoints>(TenantStorageErrors.CutoverSourceNotEligible);
    }

    return Result.Success(new CutoverEndpoints(
      operation.TenantId,
      operation.SourceTenantDatabaseId,
      operation.TargetTenantDatabaseId,
      new TenantDatabaseConnectionTarget(source.ServerKey, source.DatabaseName, source.HostingMode),
      new TenantDatabaseConnectionTarget(target.ServerKey, target.DatabaseName, target.HostingMode)));
  }

  // Both databases must hold the FULL deployed tenant migration catalog. Compared against EF's own shipped
  // list, so it cannot drift from what this build actually contains.
  private static async Task<Result> BothSchemasCurrentAsync(
    SqlConnection source,
    SqlConnection target,
    CancellationToken cancellationToken)
  {
    var expected = TenantDbContextBuilder.KnownMigrations;

    foreach (var connection in new[] { source, target })
    {
      var applied = await AppliedMigrationsAsync(connection, cancellationToken);
      if (applied is null || !expected.All(migration => applied.Contains(migration)))
      {
        return Result.Failure(TenantStorageErrors.CutoverSchemaIncompatible);
      }
    }

    return Result.Success();
  }

  private static async Task<HashSet<string>?> AppliedMigrationsAsync(
    SqlConnection connection,
    CancellationToken cancellationToken)
  {
    try
    {
      await using var command = connection.CreateCommand();
      command.CommandText =
        $"SELECT [MigrationId] FROM [{TenantPersistenceConstants.MigrationHistorySchema}]." +
        $"[{TenantPersistenceConstants.MigrationHistoryTable}]";

      var applied = new HashSet<string>(StringComparer.Ordinal);
      await using var reader = await command.ExecuteReaderAsync(cancellationToken);
      while (await reader.ReadAsync(cancellationToken))
      {
        applied.Add(reader.GetString(0));
      }

      return applied;
    }
    catch (SqlException)
    {
      // No history table, or unreadable. Either way the schema cannot be shown to be current.
      return null;
    }
  }

  private sealed record CutoverEndpoints(
    Guid TenantId,
    long SourceTenantDatabaseId,
    long TargetTenantDatabaseId,
    TenantDatabaseConnectionTarget Source,
    TenantDatabaseConnectionTarget Target);
}
