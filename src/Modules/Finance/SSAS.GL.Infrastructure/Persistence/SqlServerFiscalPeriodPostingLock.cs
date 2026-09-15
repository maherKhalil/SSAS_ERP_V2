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

// `sys.sp_getapplock` on the tenant connection, following `SqlServerFiscalYearDefinitionLock` exactly —
// same resource-key construction, same transaction ownership, same treatment of the return code. The only
// difference is the MODE, and that difference is the whole point: see `IFiscalPeriodPostingLock`.
//
// Transaction ownership means the lock is released by commit or rollback. There is no lease to renew, no
// path on which the caller's transaction ends and the lock outlives it, and no cleanup to forget.
internal sealed class SqlServerFiscalPeriodPostingLock(ITenantDbContextAccessor contextAccessor)
  : IFiscalPeriodPostingLock
{
  private const string Prefix = "SSAS.Gl.FiscalPeriodPosting.";

  // ---- POSTERS: five seconds, matching every other lock in this module.
  //
  // A poster waits only for a period-state change to finish, which is a single short write. Shared is
  // compatible with Shared, so a poster never waits for another poster.
  private static readonly TimeSpan PostingTimeout = TimeSpan.FromSeconds(5);

  // ---- THE STATE WRITER: ten seconds, and the number is chosen against a MEASURED fact rather than a
  // ---- guess about payroll.
  //
  // WHAT IS HELD, AND FOR HOW LONG: `GlJournalPoster.PostAsync` opens its transaction at one place and
  // commits at one place — ONE TRANSACTION PER POSTING. A payroll run that posts many journals takes and
  // releases this lock once per journal; IT DOES NOT HOLD IT FOR THE WHOLE RUN. So a close waits for the
  // in-flight POSTING to drain, not for a batch.
  //
  // ⚠ WHAT IS NOT MEASURED: the wall-clock duration of a single posting under load. Ten seconds is many
  // multiples of a single-row insert path and is stated as a REVIEWABLE CHOICE rather than a derived one.
  // If a posting is ever observed to take longer, this number is the thing to revisit — not the pattern.
  //
  // ⚠⚠ AND IT IS BOUNDED ON PURPOSE. An unbounded wait would make "can a period close while posting is in
  // flight" a product question about acceptable hanging. A bounded wait makes it an ordinary refusal: the
  // operator is told POSTING IS IN PROGRESS and can retry, which is strictly better than a request that
  // never returns and better than silent corruption.
  private static readonly TimeSpan StateChangeTimeout = TimeSpan.FromSeconds(10);

  // Deterministic from tenant and company alone, so two instances compute the same string for the same
  // pair. Invariant culture and the `D` format make that true regardless of the node's locale.
  public static string ForCompany(Guid tenantId, Guid companyId) => string.Create(
    CultureInfo.InvariantCulture, $"{Prefix}{tenantId:D}.{companyId:D}");

  public Task<Result> AcquireForPostingAsync(
    Guid tenantId, Guid companyId, CancellationToken cancellationToken = default) =>
    AcquireAsync(
      tenantId, companyId, "Shared", PostingTimeout, CalendarErrors.PeriodStateChangeInProgress,
      cancellationToken);

  public Task<Result> AcquireForStateChangeAsync(
    Guid tenantId, Guid companyId, CancellationToken cancellationToken = default) =>
    AcquireAsync(
      tenantId, companyId, "Exclusive", StateChangeTimeout, CalendarErrors.PostingInProgress,
      cancellationToken);

  private async Task<Result> AcquireAsync(
    Guid tenantId,
    Guid companyId,
    string mode,
    TimeSpan timeout,
    Error busy,
    CancellationToken cancellationToken)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The lock must be taken on the connection the transaction is open on, and enlisted in that
    // transaction — otherwise it protects a different scope than the one doing the writing.
    var transaction = context.Database.CurrentTransaction;

    if (transaction is null)
    {
      // A sequencing bug in the caller, not a busy system. Refusing is what keeps it from presenting as
      // an intermittent posting-into-a-closed-period much later.
      return Result.Failure(busy);
    }

    var connection = context.Database.GetDbConnection();

    await using var command = connection.CreateCommand();
    command.CommandText = "sys.sp_getapplock";
    command.CommandType = CommandType.StoredProcedure;
    command.Transaction = transaction.GetDbTransaction();

    AddParameter(command, "@Resource", ForCompany(tenantId, companyId));
    AddParameter(command, "@LockMode", mode);
    AddParameter(command, "@LockOwner", "Transaction");
    AddParameter(command, "@LockTimeout", (int)timeout.TotalMilliseconds);

    var result = command.CreateParameter();
    result.ParameterName = "@Result";
    result.DbType = DbType.Int32;
    result.Direction = ParameterDirection.ReturnValue;
    command.Parameters.Add(result);

    // The command must outlive the lock wait, or a transport timeout surfaces instead of the clean
    // negative `sp_getapplock` exists to give.
    command.CommandTimeout = (int)timeout.TotalSeconds + 30;

    await command.ExecuteNonQueryAsync(cancellationToken);

    // 0 granted, 1 granted after waiting. Anything else — timeout, deadlock victim, error — means we do
    // not hold it, and a caller that does not hold it does not get to decide whether a period is open.
    var granted = Convert.ToInt32(result.Value, CultureInfo.InvariantCulture) is 0 or 1;

    return granted ? Result.Success() : Result.Failure(busy);
  }

  private static void AddParameter(DbCommand command, string name, object value)
  {
    var parameter = command.CreateParameter();
    parameter.ParameterName = name;
    parameter.Value = value;
    command.Parameters.Add(parameter);
  }
}
