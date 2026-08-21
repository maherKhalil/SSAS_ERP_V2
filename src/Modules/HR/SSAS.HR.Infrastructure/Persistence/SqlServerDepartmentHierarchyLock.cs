using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.Departments;
using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Infrastructure.Persistence;

// THE COMPANY HIERARCHY LOCK, ON SQL SERVER (FP-007 Phase 2, ADR-026 decision 4).
//
// `sys.sp_getapplock` on the TENANT connection, following the precedent already set by
// `BranchTopologyLock` and the cutover locks — the same primitive, named differently so it cannot collide
// with them. A tenant may legitimately be mid-cutover while its departments are administered.
//
// ---- TRANSACTION-OWNED, NOT SESSION-OWNED, AND THAT IS THE DIFFERENCE THAT MATTERS.
//
// `BranchTopologyLock` takes a Session-owned lock on a dedicated connection, because the invariant it
// protects spans two databases and cannot live inside one transaction. This one can: the whole hierarchy —
// the ancestry walk and the row being written — is in the tenant database. `@LockOwner = 'Transaction'`
// means the lock is released by COMMIT or ROLLBACK, so there is no path where the work commits and the lock
// outlives it, and no cleanup to forget.
//
// It also means SQL Server enforces the requirement rather than the caller remembering it: sp_getapplock
// with Transaction ownership FAILS if there is no open transaction. A handler that took this lock outside
// its transaction would not silently get an ineffective lock — it would be refused.
//
// ---- IT WORKS ACROSS INSTANCES BECAUSE THE LOCK LIVES IN THE DATABASE.
//
// Two application nodes handling two moves for the same company contend on the same named resource inside
// SQL Server. An in-process lock — a static, a semaphore, a keyed mutex — would close the race on one node
// and leave it wide open on two, which is the failure mode this deliberately avoids.
//
// ---- THE KEY IS TENANT AND COMPANY, NEVER WIDER.
//
// Departments in different companies cannot form a cycle with each other — a parent must be in the same
// company — so serialising per company is exactly as narrow as correctness allows. Locking per tenant would
// make two companies' unrelated org changes wait on each other.
internal sealed class SqlServerDepartmentHierarchyLock(ITenantDbContextAccessor contextAccessor)
  : IDepartmentHierarchyLock
{
  public const string Prefix = "SSAS.HR.Department.Hierarchy.";

  // Five seconds. Long enough to sit through another node's ancestry walk and commit, short enough that a
  // caller is told to retry rather than left hanging. Hierarchy moves are rare, so contention is rare.
  private static readonly TimeSpan AcquisitionTimeout = TimeSpan.FromSeconds(5);

  // Deterministic from tenant and company alone, so two instances compute the same string for the same
  // pair. Invariant culture and the `D` format make that true regardless of the node's locale.
  public static string ForCompany(Guid tenantId, Guid companyId) => string.Create(
    CultureInfo.InvariantCulture, $"{Prefix}{tenantId:D}.{companyId:D}");

  public async Task<Result> AcquireAsync(
    Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The lock must be taken on the connection the transaction is open on, and enlisted in that
    // transaction — otherwise it protects a different scope than the one doing the writing.
    var transaction = context.Database.CurrentTransaction;
    if (transaction is null)
    {
      // A caller that reached here without a transaction has a bug in its sequencing, not a busy system.
      // Refusing is what keeps that bug from presenting as an intermittent cycle much later.
      return Result.Failure(DepartmentErrors.HierarchyMutationBusy);
    }

    // Written against System.Data.Common rather than the SQL Server client types. `sp_getapplock` is
    // SQL Server's, so this class is provider-specific either way — but expressing it through DbCommand
    // keeps HR.Infrastructure from taking a direct dependency on the SQL client purely to name two types.
    var connection = context.Database.GetDbConnection();

    await using var command = connection.CreateCommand();
    command.CommandText = "sys.sp_getapplock";
    command.CommandType = CommandType.StoredProcedure;
    command.Transaction = transaction.GetDbTransaction();

    AddParameter(command, "@Resource", ForCompany(tenantId, companyId));
    AddParameter(command, "@LockMode", "Exclusive");
    AddParameter(command, "@LockOwner", "Transaction");
    AddParameter(command, "@LockTimeout", (int)AcquisitionTimeout.TotalMilliseconds);

    var result = command.CreateParameter();
    result.ParameterName = "@Result";
    result.DbType = DbType.Int32;
    result.Direction = ParameterDirection.ReturnValue;
    command.Parameters.Add(result);

    // The command must outlive the lock wait, or a transport timeout surfaces instead of the clean negative
    // sp_getapplock exists to give.
    command.CommandTimeout = (int)AcquisitionTimeout.TotalSeconds + 30;
    await command.ExecuteNonQueryAsync(cancellationToken);

    // 0 granted, 1 granted after waiting. Anything else — timeout, deadlock victim, error — means we do not
    // hold it, and a caller that does not hold it does not get to decide whether a move is acyclic.
    var granted = Convert.ToInt32(result.Value, CultureInfo.InvariantCulture) is 0 or 1;

    return granted ? Result.Success() : Result.Failure(DepartmentErrors.HierarchyMutationBusy);
  }

  private static void AddParameter(DbCommand command, string name, object value)
  {
    var parameter = command.CreateParameter();
    parameter.ParameterName = name;
    parameter.Value = value;
    command.Parameters.Add(parameter);
  }
}
