using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.GL.Application.Calendar;
using SSAS.GL.Domain.Calendar;

namespace SSAS.GL.Infrastructure.Persistence;

// THE COMPANY FISCAL-CALENDAR LOCK, ON SQL SERVER (T-184).
//
// `sys.sp_getapplock` on the tenant connection, following `SqlServerDepartmentHierarchyLock` exactly —
// the same primitive with its own prefix so it cannot collide. A company may legitimately be having its
// departments reorganised while its calendar is defined.
//
// ---- TRANSACTION-OWNED, WHICH MAKES SQL SERVER ENFORCE THE SEQUENCING RATHER THAN THE CALLER.
//
// `@LockOwner = 'Transaction'` releases on COMMIT or ROLLBACK, so there is no path where the work commits
// and the lock outlives it, and no cleanup to forget. **And `sp_getapplock` with Transaction ownership
// FAILS when there is no open transaction** — a handler that took this outside its transaction would not
// silently get an ineffective lock, it would be refused.
//
// ---- IT WORKS ACROSS INSTANCES BECAUSE THE LOCK LIVES IN THE DATABASE.
//
// Two nodes defining years for one company contend on the same named resource inside SQL Server. An
// in-process lock would close the race on one node and leave it open on two.
//
// ---- THE KEY IS TENANT AND COMPANY, NEVER WIDER.
//
// Fiscal years in different companies cannot overlap each other — a year belongs to one company — so
// serialising per company is exactly as narrow as correctness allows. Locking per tenant would make two
// companies' unrelated calendar work wait on each other.
internal sealed class SqlServerFiscalYearDefinitionLock(ITenantDbContextAccessor contextAccessor)
  : IFiscalYearDefinitionLock
{
  public const string Prefix = "SSAS.GL.FiscalCalendar.Define.";

  // Five seconds, matching the hierarchy lock. Long enough to sit through another node's overlap scan and
  // commit, short enough that a caller is told to retry rather than left hanging. Defining a year is rare,
  // so contention is rare — which is an argument about this lock's COST, not about the gap's acceptability.
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
      // A sequencing bug in the caller, not a busy system. Refusing is what keeps it from presenting as an
      // intermittent overlap much later.
      return Result.Failure(CalendarErrors.CalendarDefinitionBusy);
    }

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
    // `sp_getapplock` exists to give.
    command.CommandTimeout = (int)AcquisitionTimeout.TotalSeconds + 30;

    await command.ExecuteNonQueryAsync(cancellationToken);

    // 0 granted, 1 granted after waiting. Anything else — timeout, deadlock victim, error — means we do not
    // hold it, and a caller that does not hold it does not get to decide whether a year overlaps.
    var granted = Convert.ToInt32(result.Value, CultureInfo.InvariantCulture) is 0 or 1;
    return granted ? Result.Success() : Result.Failure(CalendarErrors.CalendarDefinitionBusy);
  }

  private static void AddParameter(DbCommand command, string name, object value)
  {
    var parameter = command.CreateParameter();
    parameter.ParameterName = name;
    parameter.Value = value;
    command.Parameters.Add(parameter);
  }
}
