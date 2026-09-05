using SSAS.Attendance.Application.Abstractions;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Application.Records;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;

namespace SSAS.Attendance.Tests.Records;

// ==================================================================================================
// CORRECTING AN OBSERVATION AFTER ITS PERIOD CLOSED (AC-ATT-0014, OD-ATT-0012).
// ==================================================================================================
//
// *"A correction after close creates an adjustment record in the next period; the closed period is never
// edited, and the refusal comes from the persistence layer rather than from a status check."*
//
// ---- ⚠⚠⚠ WHAT WAS ALREADY ASSERTED WAS THE ENABLING CONDITION, NOT THE CRITERION.
//
// `AttendanceSchemaSqlServerTests.Attendance_records_carry_no_unique_index_on_employee_and_date` proves
// the model PERMITS a second row for one employee-date. **Permitting is not doing.** It says nothing about
// an adjustment being CREATED, nothing about WHICH period it lands in, and nothing about the original being
// left alone — and its author knew, which is why it carries only a `Decision` trait.
//
// ---- ⚠⚠ THE TWO PERIODS ARE DIFFERENT AND THAT IS THE WHOLE OF CLAUSE 1.
//
// The observation sits in a CLOSED period; the adjustment must land in the one open NOW. **With one period
// serving both roles the clause is unobservable** — every assertion passes whether the handler resolves the
// current open period or simply reuses the original's. *So the fixture holds two, and the assertion names
// the one the original is NOT in.*
//
// The handler's own comment states the rule: *"The period an adjustment lands in is the one open NOW, not
// the one covering the date being corrected — that period may well be closed, which is why the adjustment
// exists."*
//
// ---- ⚠⚠⚠ AND CLAUSE 3 IS ASSERTED BY WHAT THE PERIOD STUB REFUSES TO ANSWER.
//
// *"...the refusal comes from the persistence layer rather than from a status check."* **A handler that
// guarded corrections by reading the original's period and testing `IsClosed` would satisfy clauses 1 and 2
// perfectly** and violate this one — the guarantee would then rest on a check somebody could forget rather
// than on the write boundary.
//
// ***SO THE PERIOD STUB ANSWERS `GetCurrentOpenAsync` AND THROWS ON EVERY OTHER MEMBER.*** If the handler
// ever looked up the corrected date's period — to read its status or for any other reason — these tests
// would fail with a message naming the method it reached for. *The absence of a status check is asserted by
// making the status unreachable.*
//
// ⚠ AND THE PERSISTENCE HALF IS NOT HERE, DELIBERATELY. That an attempted EDIT is refused by the write
// boundary is `TenantAppendOnlyGuardTests`, which drives `TenantDbContext.PreventAppendOnlyMutation`
// behaviourally — **gated only since `69c2f0a`; before that it was Integration-only, green at a date.**
// This file asserts that the correction path does not rely on a status check; that file asserts what stops
// an edit when one is attempted. *Neither carries the other.*
public sealed class AttendanceCorrectionTests
{
  private static readonly Guid Company = Guid.NewGuid();
  private static readonly Guid Employee = Guid.NewGuid();

  // The date being corrected sits in a period that has since closed. `Today` is deliberately unrelated to
  // it — the handler resolves the open period from NOW, not from the date under correction.
  private static readonly DateOnly CorrectedDate = new(2026, 3, 10);

  [Fact]
  [Trait("Criterion", "AC-ATT-0014")]
  public async Task A_correction_lands_in_the_currently_open_period_and_not_the_closed_one()
  {
    var closed = ClosedPeriod();
    var open = OpenPeriod();
    var original = Observation(closed);

    var records = new RecordingRepository(original);
    var unitOfWork = new CountingUnitOfWork();

    // THE PREMISE. Two distinct periods, and the original really is in the closed one — without this the
    // assertion below could be satisfied by a fixture where both ids happened to agree.
    Assert.NotEqual(closed.Id, open.Id);
    Assert.Equal(closed.Id, original.AttendancePeriodId);

    var result = await Adjust(records, unitOfWork, open, original);

    Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : string.Empty);

    var adjustment = Assert.Single(records.Added);
    Assert.Equal(open.Id, adjustment.AttendancePeriodId);
    Assert.NotEqual(closed.Id, adjustment.AttendancePeriodId);
  }

  // ---- ⚠⚠ THE CLOSED PERIOD IS NEVER EDITED, AND "NEVER EDITED" IS NOT "STILL LOOKS THE SAME".
  //
  // A net observation of the original's fields cannot distinguish *never touched* from *changed and changed
  // back*. **What this asserts instead is that the correction is an ADD**: exactly one record reached the
  // repository, it is the adjustment rather than the original, and it carries a different identity. *The
  // original is not in the written set at all, which is a stronger statement than its values being equal.*
  [Fact]
  [Trait("Criterion", "AC-ATT-0014")]
  public async Task A_correction_adds_a_second_record_and_writes_nothing_to_the_original()
  {
    var original = Observation(ClosedPeriod());
    var records = new RecordingRepository(original);
    var unitOfWork = new CountingUnitOfWork();

    var result = await Adjust(records, unitOfWork, OpenPeriod(), original);

    Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : string.Empty);

    var written = Assert.Single(records.Added);
    Assert.NotEqual(original.Id, written.Id);
    Assert.Equal(AttendanceRecordKind.Adjustment, written.Kind);

    // The correction is one save of one new row. A handler that also re-saved the original would show here.
    Assert.Equal(1, unitOfWork.Saves);
  }

  private static Task<Result<Guid>> Adjust(
    RecordingRepository records,
    CountingUnitOfWork unitOfWork,
    AttendancePeriod open,
    AttendanceRecord original)
  {
    var handler = new AdjustAttendanceCommandHandler(
      records, new CurrentOpenOnlyPeriods(open), new PermissiveScope(), unitOfWork);

    return handler.HandleAsync(new AdjustAttendanceCommand(
      original.Id,
      WorkedDelta: -2m, OvertimeDelta: 0m, OvertimeTier: null,
      PaidAbsenceDelta: 0m, UnpaidAbsenceDelta: 0m, Note: "corrected"));
  }

  private static AttendancePeriod ClosedPeriod()
  {
    var period = AttendancePeriod.Create(
      Company, "March 2026", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)).Value;

    Assert.True(period.Close("the closer", DateTimeOffset.UtcNow).IsSuccess);
    return period;
  }

  private static AttendancePeriod OpenPeriod() =>
    AttendancePeriod.Create(
      Company, "The open one", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)).Value;

  private static AttendanceRecord Observation(AttendancePeriod period) =>
    AttendanceRecord.Observe(
      Company, period.Id, Employee, CorrectedDate,
      workedQuantity: 8m, overtimeQuantity: 0m, overtimeTier: null,
      paidAbsenceQuantity: 0m, unpaidAbsenceQuantity: 0m, note: null).Value;

  // ---- ANSWERS THE CURRENT OPEN PERIOD AND NOTHING ELSE.
  //
  // Every other member throws, which is how clause 3 is asserted: a handler consulting the corrected date's
  // period — for its status or for anything — reaches one of these and the failure names the method.
  private sealed class CurrentOpenOnlyPeriods(AttendancePeriod open) : IAttendancePeriodRepository
  {
    public Task<AttendancePeriod?> GetByIdAsync(
      Guid attendancePeriodId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException(
        "A correction must not look up the corrected date's period; the write boundary refuses an edit.");

    public Task<AttendancePeriod?> GetCoveringAsync(
      Guid companyId, DateOnly onDate, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException(
        "A correction must not resolve the period covering the corrected date, nor read its status.");

    public Task<AttendancePeriod?> GetCurrentOpenAsync(
      Guid companyId, DateOnly asOf, CancellationToken cancellationToken = default) =>
      Task.FromResult<AttendancePeriod?>(open);

    public Task<bool> OverlapsAsync(
      Guid companyId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Corrections open no periods.");

    public Task AddAsync(AttendancePeriod period, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Corrections open no periods.");
  }

  private sealed class RecordingRepository(AttendanceRecord original) : IAttendanceRecordRepository
  {
    public List<AttendanceRecord> Added { get; } = [];

    public Task<AttendanceRecord?> GetByIdAsync(
      Guid attendanceRecordId, CancellationToken cancellationToken = default) =>
      Task.FromResult<AttendanceRecord?>(attendanceRecordId == original.Id ? original : null);

    public Task<IReadOnlyList<AttendanceRecord>> GetForEmployeePeriodAsync(
      Guid attendancePeriodId, Guid employeeId, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<AttendanceRecord>>([]);

    public Task AddAsync(AttendanceRecord record, CancellationToken cancellationToken = default)
    {
      Added.Add(record);
      return Task.CompletedTask;
    }
  }

  private sealed class CountingUnitOfWork : ITenantUnitOfWork
  {
    public int Saves { get; private set; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      Saves++;
      return Task.FromResult(Result.Success(1));
    }

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("A correction is one append and opens no transaction.");
  }

  private sealed class PermissiveScope : IAttendanceScopeResolver
  {
    public Task<Result<AttendanceReadScope>> ResolveAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("These tests write; they do not read.");

    public Task<Result<AttendanceReadScope>> ResolveCompanyOnlyAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("These tests write; they do not read.");

    public Task<Result> AuthorizeAsync(
      string permissionName, Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success());

    public Result RequirePermission(string permissionName) => Result.Success();

    public bool HasPermission(string permissionName) => true;
  }
}
