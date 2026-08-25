using SSAS.Attendance.Application.Abstractions;
using SSAS.Attendance.Application.Permissions;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Contracts.Employment;

namespace SSAS.Attendance.Application.Records;

public sealed record RecordAttendanceCommand(
  Guid CompanyId,
  Guid EmployeeId,
  DateOnly AttendanceDate,
  decimal WorkedQuantity,
  decimal OvertimeQuantity,
  string? OvertimeTier,
  decimal PaidAbsenceQuantity,
  decimal UnpaidAbsenceQuantity,
  string? Note);

public sealed record AdjustAttendanceCommand(
  Guid AdjustedRecordId,
  decimal WorkedDelta,
  decimal OvertimeDelta,
  string? OvertimeTier,
  decimal PaidAbsenceDelta,
  decimal UnpaidAbsenceDelta,
  string? Note);

// ================================================================================================
// THE EMPLOYMENT-WINDOW CHECK, AND WHY IT IS A CONTRACT CALL RATHER THAN A CONSTRAINT.
// ================================================================================================
//
// `REQ-ATT-0006` and `BR-ATT-0004`: attendance may not be recorded outside an employee's employment window.
//
// The dates live in HR's tables, and `ADR-012` forbids Attendance reaching them directly. The shared Tenant
// DB makes it *possible* — HR and Attendance are both tenant modules — which is exactly why the rule has to
// be deliberate rather than incidental. So the check runs against `IEmployeeRoster` at write time.
//
// **The errors describe THE EMPLOYEE named in the body, never the record addressed in the route.** That is
// precisely the miscoding `DepartmentApiErrorMapper` was written to fix, where a department-manager error
// surfaced under an employee code. Two errors rather than one — before employment and after termination —
// because the remedies differ and a caller can act on the distinction.
//
// `BR-HR-0004` as read by `OD-PAY-0010` bars NEW obligations to a terminated employee, not the settlement of
// obligations already incurred. Here that means: attendance dated after termination is refused; attendance
// already recorded stays readable forever (`AC-ATT-0009`). Recording ON the termination date is ACCEPTED —
// the boundary is inclusive, stated because inclusive-versus-exclusive is where this goes wrong.
internal static class EmploymentWindow
{
  public static async Task<Result> CheckAsync(
    IEmployeeRoster roster, Guid companyId, Guid employeeId, DateOnly date, CancellationToken cancellationToken)
  {
    var instant = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    // The roster answers per company and window. Asking for the single day is the narrowest honest question,
    // and it returns the employee if their employment overlaps that day at all.
    var employment = await roster.GetEmploymentAsync(companyId, instant, instant, cancellationToken);
    var record = employment.FirstOrDefault(candidate => candidate.EmployeeId == employeeId);

    if (record is null)
    {
      // Absent from the overlap set for this date. Distinguish the two reasons rather than reporting one:
      // an employee of this company whose window excludes the date is a DIFFERENT problem from an employee
      // who is not in this company at all, and the second is usually a mis-keyed identifier.
      var everything = await roster.GetEmploymentAsync(
        companyId, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, cancellationToken);
      var anywhere = everything.FirstOrDefault(candidate => candidate.EmployeeId == employeeId);

      if (anywhere is null)
      {
        return Result.Failure(AttendanceRecordErrors.EmployeeNotInCompany);
      }

      return instant < anywhere.EmploymentDateUtc
        ? Result.Failure(AttendanceRecordErrors.BeforeEmployment)
        : Result.Failure(AttendanceRecordErrors.AfterTermination);
    }

    if (instant < record.EmploymentDateUtc)
    {
      return Result.Failure(AttendanceRecordErrors.BeforeEmployment);
    }

    // Inclusive on the termination date: somebody who left on the 14th worked the 14th.
    if (record.TerminationDateUtc is { } terminated && instant > terminated)
    {
      return Result.Failure(AttendanceRecordErrors.AfterTermination);
    }

    return Result.Success();
  }
}

public sealed class RecordAttendanceCommandHandler(
  IAttendanceRecordRepository records,
  IAttendancePeriodRepository periods,
  IEmployeeRoster roster,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result<Guid>> HandleAsync(
    RecordAttendanceCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ManageRecords, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var period = await periods.GetCoveringAsync(command.CompanyId, command.AttendanceDate, cancellationToken);
    if (period is null)
    {
      return Result.Failure<Guid>(AttendancePeriodErrors.NoOpenPeriod);
    }

    // A closed period refuses an ordinary observation. The remedy is an ADJUSTMENT in the current open
    // period (`OD-ATT-0012`), and the error says so rather than leaving the caller to guess.
    if (period.IsClosed)
    {
      return Result.Failure<Guid>(AttendancePeriodErrors.PeriodClosed);
    }

    var window = await EmploymentWindow.CheckAsync(
      roster, command.CompanyId, command.EmployeeId, command.AttendanceDate, cancellationToken);
    if (window.IsFailure)
    {
      return Result.Failure<Guid>(window.Error);
    }

    var record = AttendanceRecord.Observe(
      command.CompanyId, period.Id, command.EmployeeId, command.AttendanceDate,
      command.WorkedQuantity, command.OvertimeQuantity, command.OvertimeTier,
      command.PaidAbsenceQuantity, command.UnpaidAbsenceQuantity, command.Note);
    if (record.IsFailure)
    {
      return Result.Failure<Guid>(record.Error);
    }

    // ---- BranchId IS NOT SET HERE, AND ITS ABSENCE IS THE DESIGN.
    //
    // `IBranchOwnedEntity` is stamped by the WRITE BOUNDARY from the execution context during save, via
    // `ICurrentBranchResolver`. A handler that set it would be a second opinion about which branch this
    // operation is in, and two independent resolutions is exactly how a recorded branch comes to disagree
    // with the boundary that authorized the write.
    await records.AddAsync(record.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saved.IsFailure ? Result.Failure<Guid>(saved.Error) : Result.Success(record.Value.Id);
  }
}

// ================================================================================================
// THE CORRECTION PATH (REQ-ATT-0019, OD-ATT-0012).
// ================================================================================================
//
// A correction is a NEW RECORD carrying deltas, never an edit. It keeps the ORIGINAL's date and lands in the
// CURRENT OPEN period — the date says when it happened, the period says when it was recorded.
//
// That separation is what lets a closed period stay closed while the record of what actually happened stays
// correct, and it is why `AttendanceRecords` carries no unique index on (Tenant, Employee, Date): a second
// row for the same employee-date IS an adjustment, and an index chosen from the happy path would make the
// ruling unimplementable.
public sealed class AdjustAttendanceCommandHandler(
  IAttendanceRecordRepository records,
  IAttendancePeriodRepository periods,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result<Guid>> HandleAsync(
    AdjustAttendanceCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var original = await records.GetByIdAsync(command.AdjustedRecordId, cancellationToken);
    if (original is null)
    {
      return Result.Failure<Guid>(AttendanceRecordErrors.NotFound);
    }

    // Authorized against the ORIGINAL's company, read from the entity. The command carries no company for
    // exactly this reason: a caller who could name one would authorize themselves against a company they
    // hold and then adjust a record belonging to one they do not.
    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ManageRecords, original.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    // ---- AN ADJUSTMENT CANNOT ADJUST AN ADJUSTMENT.
    //
    // Corrections chain to the OBSERVATION, not to each other. Allowing a chain would make "what is the
    // truth for this employee-date" a graph walk rather than a sum, and the sum is the whole reason the
    // model is simple enough to be right.
    if (original.Kind != AttendanceRecordKind.Observation)
    {
      return Result.Failure<Guid>(AttendanceRecordErrors.AdjustedRecordMismatch);
    }

    // Today's date in UTC. The period an adjustment lands in is the one open NOW, not the one covering the
    // date being corrected — that period may well be closed, which is why the adjustment exists.
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var open = await periods.GetCurrentOpenAsync(original.CompanyId, today, cancellationToken);
    if (open is null)
    {
      return Result.Failure<Guid>(AttendancePeriodErrors.NoOpenPeriod);
    }

    var adjustment = AttendanceRecord.Adjust(
      original.CompanyId, open.Id, original.EmployeeId, original.AttendanceDate, original.Id,
      command.WorkedDelta, command.OvertimeDelta, command.OvertimeTier,
      command.PaidAbsenceDelta, command.UnpaidAbsenceDelta, command.Note);
    if (adjustment.IsFailure)
    {
      return Result.Failure<Guid>(adjustment.Error);
    }

    await records.AddAsync(adjustment.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saved.IsFailure ? Result.Failure<Guid>(saved.Error) : Result.Success(adjustment.Value.Id);
  }
}
