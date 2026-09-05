using SSAS.Attendance.Application.Abstractions;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Application.Records;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Contracts.Employment;

namespace SSAS.Attendance.Tests.Records;

// ==================================================================================================
// RECORDING ATTENDANCE AGAINST THE EMPLOYMENT WINDOW (AC-ATT-0006, AC-ATT-0007, AC-ATT-0008).
// ==================================================================================================
//
// ---- ⚠⚠⚠ WHY THIS FILE DID NOT EXIST, WHICH IS A MEASURED ASYMMETRY RATHER THAN AN IMPRESSION.
//
// The check is real and has been all along: `EmploymentWindow.CheckAsync` runs against `IEmployeeRoster` at
// WRITE time, and its own comment says so. ***NOTHING DROVE IT.*** Both populations that could witness it
// were enumerated and both were empty:
//
//   domain   `AttendanceRecordErrors.BeforeEmployment` / `.AfterTermination`   named by NO test
//   wire     `Attendance.RecordBeforeEmployment` → `attendance.employment_window`
//            ***one occurrence tree-wide, and it is a COMMENT in a stub explaining why a roster is seeded***
//
// **Every `BeforeEmployment`/`AfterTermination` hit in `tests/` belongs to the LEAVE twin** — `LeaveErrors.*`,
// with a dedicated `LeaveEmploymentWindowTests`. *Leave's employment window had its own file; attendance's
// had nothing.* This is that file, modelled on its sibling deliberately.
//
// ⚠⚠ AND IT HAS TO LIVE HERE RATHER THAN AT THE ENDPOINT. `AttendanceApiErrorMapper` folds SIX domain
// errors onto the single wire code `attendance.employment_window` — the three record refusals and the three
// leave ones. ***SO AN API TEST CANNOT SAY WHICH BOUNDARY REFUSED; THE DISTINCTION EXISTS ONLY AT THIS
// LAYER.*** One observable, many causes: the address of the test is decided by where the causes are still
// separable.
//
// ---- ⚠⚠⚠ THE TERMINATION DATE ITSELF IS THE ONLY DATE THAT DISCRIMINATES, AND THE AUTHOR SAID SO.
//
// The source carries the reason in the comment on the branch: *"Inclusive on the termination date: somebody
// who left on the 14th worked the 14th."* **A test recording comfortably inside the window passes whether
// the boundary is inclusive or exclusive.** Only the boundary day separates `>` from `>=`, so the employment
// window below ENDS on a 14th and the success case records on it.
//
// ---- ⚠⚠ THE ROSTER MODELS THE CONTRACT, NOT THE TEST'S WISHES.
//
// `IEmployeeRoster.GetEmploymentAsync` takes a window and returns employees whose employment OVERLAPS it. A
// stub answering unconditionally would let this file assert refusals the product cannot actually reach —
// an arranged failure rather than a constructible one.
//
// **Modelling it honestly is also what makes the two boundary refusals reachable at all.** For a date
// outside the window the narrow point-query returns nothing, and `CheckAsync` then asks a SECOND, wider
// question to tell "outside their window" from "not this company's employee". *That fallback is the reason
// attendance can name the boundary where leave cannot* — leave has no second query, which is why its own
// file records that `RequestAfterTermination` is reachable only by a straddle.
public sealed class AttendanceRecordEmploymentWindowTests
{
  private static readonly Guid Company = Guid.NewGuid();
  private static readonly Guid Employee = Guid.NewGuid();
  private static readonly Guid Stranger = Guid.NewGuid();

  // Employment runs 2026-03-01 to 2026-06-14. The 14th is load-bearing: see the note above.
  private static readonly DateOnly Employed = new(2026, 3, 1);
  private static readonly DateOnly Terminated = new(2026, 6, 14);

  [Fact]
  [Trait("Criterion", "AC-ATT-0006")]
  public async Task Recording_inside_the_window_succeeds_including_on_the_termination_date_itself()
  {
    var records = new RecordingRepository();
    var unitOfWork = new CountingUnitOfWork();

    // The ordinary case: a date comfortably inside the window.
    Assert.True((await Record(new DateOnly(2026, 4, 20), records, unitOfWork)).IsSuccess);

    // ***AND THE BOUNDARY DAY, WHICH IS THE ONLY ONE THAT SEPARATES `>` FROM `>=`.*** Somebody who left on
    // the 14th worked the 14th. The assertion above passes under either reading; this one does not.
    var onLastDay = await Record(Terminated, records, unitOfWork);
    Assert.True(onLastDay.IsSuccess, onLastDay.IsFailure ? onLastDay.Error.Code : string.Empty);

    // Both reached persistence, which is what "succeeds" means here — a `Result` alone would not show that
    // the write path ran rather than being short-circuited into a benign answer.
    Assert.Equal(2, records.Added.Count);
    Assert.Equal(2, unitOfWork.Saves);
  }

  [Fact]
  [Trait("Criterion", "AC-ATT-0007")]
  public async Task Recording_the_day_after_termination_is_refused_naming_that_boundary()
  {
    var records = new RecordingRepository();
    var unitOfWork = new CountingUnitOfWork();

    var result = await Record(Terminated.AddDays(1), records, unitOfWork);

    Assert.True(result.IsFailure);
    Assert.Equal(AttendanceRecordErrors.AfterTermination.Code, result.Error.Code);

    // ---- ⚠ *"WITH AN ERROR THAT NAMES THE EMPLOYEE, NOT THE RECORD"* — THE CLAUSE WITH NO VALUE TO ASSERT.
    //
    // There is no field on the error to inspect, so the claim is carried STRUCTURALLY: the refusal happens
    // before any record is constructed, added or saved. **Nothing was addressed, so the error cannot be
    // about a record.** These two assertions are that clause, not decoration.
    Assert.Empty(records.Added);
    Assert.Equal(0, unitOfWork.Saves);
  }

  [Fact]
  [Trait("Criterion", "AC-ATT-0008")]
  public async Task Recording_the_day_before_employment_is_refused_naming_that_boundary()
  {
    var records = new RecordingRepository();
    var unitOfWork = new CountingUnitOfWork();

    var result = await Record(Employed.AddDays(-1), records, unitOfWork);

    Assert.True(result.IsFailure);
    Assert.Equal(AttendanceRecordErrors.BeforeEmployment.Code, result.Error.Code);

    Assert.Empty(records.Added);
    Assert.Equal(0, unitOfWork.Saves);
  }

  // ---- ⚠⚠⚠ THE CONTROL, AND WITHOUT IT THE TWO REFUSALS ABOVE PROVE MUCH LESS THAN THEY APPEAR TO.
  //
  // Both boundary refusals are reached through the SAME branch — the employee is absent from the point
  // query, so `CheckAsync` asks a wider question and chooses a code. ***A PRODUCT THAT ANSWERED
  // `AfterTermination` FOR EVERY ABSENT EMPLOYEE WOULD PASS THE AFTER-TERMINATION TEST PERFECTLY.***
  //
  // This names a member of the set those two must EXCLUDE: an employee who is not this company's at all.
  // He is absent from both queries, and the answer must be a third code rather than either boundary. That
  // is what makes "names that boundary" in the two test names above a claim rather than a label.
  [Fact]
  public async Task An_employee_of_another_company_is_refused_as_unknown_rather_than_as_out_of_window()
  {
    var records = new RecordingRepository();
    var unitOfWork = new CountingUnitOfWork();

    var result = await Record(new DateOnly(2026, 4, 20), records, unitOfWork, employeeId: Stranger);

    Assert.True(result.IsFailure);
    Assert.Equal(AttendanceRecordErrors.EmployeeNotInCompany.Code, result.Error.Code);

    // Explicitly NEITHER boundary code, because that is the confusion this control exists to rule out.
    Assert.NotEqual(AttendanceRecordErrors.AfterTermination.Code, result.Error.Code);
    Assert.NotEqual(AttendanceRecordErrors.BeforeEmployment.Code, result.Error.Code);
  }

  private static Task<Result<Guid>> Record(
    DateOnly date,
    RecordingRepository records,
    CountingUnitOfWork unitOfWork,
    Guid? employeeId = null)
  {
    var handler = new RecordAttendanceCommandHandler(
      records, new OpenPeriods(), new OverlapRoster(), new PermissiveScope(), unitOfWork);

    return handler.HandleAsync(new RecordAttendanceCommand(
      Company, employeeId ?? Employee, date,
      WorkedQuantity: 8m, OvertimeQuantity: 0m, OvertimeTier: null,
      PaidAbsenceQuantity: 0m, UnpaidAbsenceQuantity: 0m, Note: null));
  }

  // Returns the employee only when the requested window overlaps [Employed, Terminated], which is what the
  // real roster does — and the wide MinValue..MaxValue question `CheckAsync` falls back to overlaps it too,
  // which is how the boundary codes become reachable. `Stranger` is in no answer at all.
  private sealed class OverlapRoster : IEmployeeRoster
  {
    public Task<IReadOnlyList<EmploymentRecord>> GetEmploymentAsync(
      Guid companyId, DateTimeOffset fromUtc, DateTimeOffset toUtc,
      CancellationToken cancellationToken = default)
    {
      var employed = new DateTimeOffset(Employed.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
      var terminated = new DateTimeOffset(Terminated.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

      IReadOnlyList<EmploymentRecord> answer = fromUtc <= terminated && toUtc >= employed
        ? [new EmploymentRecord(Employee, companyId, employed, terminated)]
        : [];

      return Task.FromResult(answer);
    }
  }

  // One open period spanning the whole year, so no case below is decided by the period rather than by the
  // employment window. A closed period is `AC-ATT-0012`'s subject and is refused one step earlier.
  private sealed class OpenPeriods : IAttendancePeriodRepository
  {
    private static readonly AttendancePeriod Period = AttendancePeriod.Create(
      Company, "2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)).Value;

    public Task<AttendancePeriod?> GetByIdAsync(
      Guid attendancePeriodId, CancellationToken cancellationToken = default) =>
      Task.FromResult<AttendancePeriod?>(Period);

    public Task<AttendancePeriod?> GetCoveringAsync(
      Guid companyId, DateOnly onDate, CancellationToken cancellationToken = default) =>
      Task.FromResult<AttendancePeriod?>(Period);

    public Task<AttendancePeriod?> GetCurrentOpenAsync(
      Guid companyId, DateOnly asOf, CancellationToken cancellationToken = default) =>
      Task.FromResult<AttendancePeriod?>(Period);

    public Task<bool> OverlapsAsync(
      Guid companyId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
      Task.FromResult(true);

    public Task AddAsync(AttendancePeriod period, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("These tests record; they do not open periods.");
  }

  // Collects rather than throwing, because the refusal tests assert that NOTHING was written and the
  // success test asserts that something was. A throwing stub could carry only the first of those.
  private sealed class RecordingRepository : IAttendanceRecordRepository
  {
    public List<AttendanceRecord> Added { get; } = [];

    public Task<AttendanceRecord?> GetByIdAsync(
      Guid attendanceRecordId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Recording addresses no existing record — that is the point.");

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
      throw new NotSupportedException("Recording a single observation opens no transaction.");
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
