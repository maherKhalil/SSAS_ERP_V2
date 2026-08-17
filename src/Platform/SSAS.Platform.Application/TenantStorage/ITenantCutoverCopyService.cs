using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.TenantStorage;

// The Shared → Dedicated tenant-data copy primitive (ADR-020, TS-Storage Phase E3).
//
// A PRIMITIVE, NOT AN ORCHESTRATOR. It copies and validates one already-frozen cutover operation's tenant
// data and reports what it found. It does not decide what happens next: it never flips routing, never
// increments RoutingVersion, never invalidates a cache, and never releases the freeze — including on
// failure, because "retry" and "abandon" are decisions that need the durable state to still be true when
// they are made.
//
// IT IS SAFE TO CALL AGAIN. A copy that died partway leaves committed tables exactly copied and the
// interrupted table not copied at all; a retry re-validates what is there, skips what already matches
// exactly, and continues. Nothing is deleted to make that work.
public interface ITenantCutoverCopyService
{
  Task<Result<TenantCutoverCopyReport>> CopyAsync(
    long cutoverOperationId,
    CancellationToken cancellationToken = default);
}

// STRUCTURED EVIDENCE, NOT A BOOL. A cutover is a one-way operation performed on customer data; what an
// operator needs afterwards is which tables were copied, which were already present and exact, and how many
// rows each holds — a true tells them none of that.
public sealed record TenantCutoverCopyReport(
  long CutoverOperationId,
  Guid TenantId,
  long SourceTenantDatabaseId,
  long TargetTenantDatabaseId,
  IReadOnlyList<TenantCutoverTableReport> Tables)
{
  public long TotalRows => Tables.Sum(table => table.Rows);

  public int TablesCopied =>
    Tables.Count(table => table.Disposition == TenantCutoverTableDisposition.Copied);

  public int TablesAlreadyComplete =>
    Tables.Count(table => table.Disposition == TenantCutoverTableDisposition.AlreadyComplete);
}

public sealed record TenantCutoverTableReport(
  string EntityName,
  string TableName,
  long Rows,
  TenantCutoverTableDisposition Disposition);

public enum TenantCutoverTableDisposition
{
  // Copied by this execution, and validated exactly before its insert was committed.
  Copied = 1,

  // Already present on the target and proven an exact copy of the source, so this execution left it alone.
  // This is the resume path: the table was committed by an execution that later died.
  AlreadyComplete = 2
}
