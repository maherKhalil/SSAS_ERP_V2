using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SSAS.Attendance.Application.Leave;
using SSAS.Attendance.Domain.Leave;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;

namespace SSAS.Attendance.Infrastructure.Persistence;

// THE LEAVE SUBMISSION LOCK, ON SQL SERVER (T-151).
//
// **The fourth instance of a primitive this product already relies on**, not a new capability:
// `BranchTopologyLock`, the cutover write fence, and `SqlServerDepartmentHierarchyLock` all take
// `sys.sp_getapplock` on the tenant connection. **This one is modelled on the department lock**, which is
// the transaction-owned shape, and is named differently so it cannot collide with any of them.
//
// ---- SCOPED PER EMPLOYEE, WHICH IS AS NARROW AS CORRECTNESS ALLOWS.
//
// The invariant is *"one employee's approved and submitted requests do not overlap"*. It says nothing about
// two employees, so serialising per employee is exactly the width of the rule. **Locking per company would
// make one person's leave request wait on an unrelated colleague's**, and locking per tenant would make it
// wait on another company's.
internal sealed class SqlServerLeaveSubmissionLock(ITenantDbContextAccessor contextAccessor)
  : ILeaveSubmissionLock
{
  public const string Prefix = "SSAS.Attendance.Leave.Submission.";

  // Five seconds, matching the hierarchy lock. The work it waits on is one overlap query and one insert, so
  // a wait this long means genuine contention rather than a slow operation — and a caller told to retry is
  // better served than one left hanging.
  private static readonly TimeSpan AcquisitionTimeout = TimeSpan.FromSeconds(5);

  // Deterministic from tenant and employee alone, so two application nodes compute the same string for the
  // same pair. Invariant culture and `D` make that independent of the node's locale.
  public static string ForEmployee(Guid tenantId, Guid employeeId) => string.Create(
    CultureInfo.InvariantCulture, $"{Prefix}{tenantId:D}.{employeeId:D}");

  public async Task<Result> AcquireAsync(
    Guid tenantId, Guid employeeId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The lock must be enlisted in the transaction doing the writing, or it protects a different scope than
    // the one being protected.
    var transaction = context.Database.CurrentTransaction;
    if (transaction is null)
    {
      // Reaching here without a transaction is a sequencing bug, not a busy system. Refusing is what stops
      // it presenting later as an intermittent double-booking nobody can reproduce.
      return Result.Failure(LeaveErrors.SubmissionBusy);
    }

    var connection = context.Database.GetDbConnection();

    await using var command = connection.CreateCommand();
    command.CommandText = "sys.sp_getapplock";
    command.CommandType = CommandType.StoredProcedure;
    command.Transaction = transaction.GetDbTransaction();

    AddParameter(command, "@Resource", ForEmployee(tenantId, employeeId));
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
    // hold it, and a caller that does not hold it does not get to decide whether a range is free.
    var granted = Convert.ToInt32(result.Value, CultureInfo.InvariantCulture) is 0 or 1;

    return granted ? Result.Success() : Result.Failure(LeaveErrors.SubmissionBusy);
  }

  private static void AddParameter(DbCommand command, string name, object value)
  {
    var parameter = command.CreateParameter();
    parameter.ParameterName = name;
    parameter.Value = value;
    command.Parameters.Add(parameter);
  }
}
