using SSAS.BuildingBlocks.SharedKernel;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Attendance.Contracts.Summaries;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.GL.Contracts.Posting;
using SSAS.HR.Contracts.Employment;
using SSAS.Payroll.Application.Abstractions;
using SSAS.Payroll.Application.Permissions;
using SSAS.Payroll.Application.Reads;
using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Application.Runs;

public sealed record GeneratePayrollPeriodCommand(
  Guid CompanyId, string? Name, DateTimeOffset AnyDateInPeriodUtc, DateTimeOffset PayDateUtc);

public sealed record CreatePayrollRunCommand(Guid CompanyId, Guid PayrollPeriodId);

public sealed record CalculatePayrollRunCommand(Guid PayrollRunId);

public sealed record ApprovePayrollRunCommand(Guid PayrollRunId);

public sealed record PostPayrollRunCommand(Guid PayrollRunId);

public sealed record ReversePayrollRunCommand(
  Guid PayrollRunId, DateTimeOffset ReversalDateUtc, string Description);

// ---- PERIOD GENERATION, FROM THE FISCAL CALENDAR (OD-PAY-0002).
//
// A payroll period is GENERATED from a fiscal period rather than authored beside one, which is how 1:1
// alignment is guaranteed by construction. The caller names a date; GL resolves which fiscal period covers
// it and returns its identity and bounds; `PayrollPeriod.CreateAlignedTo` is not permitted to disagree.
//
// A caller cannot supply the bounds, and that absence is the ruling: bounds a caller could name are bounds
// a caller could misalign, and `OD-PAY-0014`'s closed-period check would then be guarding a straddle.
public sealed class GeneratePayrollPeriodCommandHandler(
  IPayrollPeriodRepository periods,
  IJournalPoster ledger,
  IPayrollScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result<Guid>> HandleAsync(
    GeneratePayrollPeriodCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var authorized = await scope.AuthorizeAsync(
      PayrollPermissionNames.ManageRuns, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var window = await ledger.InspectPostingWindowAsync(
      command.CompanyId, command.AnyDateInPeriodUtc, cancellationToken);

    if (window.Status == PostingWindowStatus.PeriodNotFound || window.FiscalPeriodId is null)
    {
      return Result.Failure<Guid>(PayrollErrors.FiscalPeriodNotFound);
    }

    // A CLOSED fiscal period still has identity and bounds, so a payroll period can be generated for it —
    // generating is not posting. The closed-period refusal belongs at APPROVAL (`OD-PAY-0014`), and moving
    // it here would stop an operator preparing next month's payroll while last month is being closed.

    if (await periods.ExistsForFiscalPeriodAsync(
      command.CompanyId, window.FiscalPeriodId.Value, cancellationToken))
    {
      return Result.Failure<Guid>(PayrollErrors.PeriodAlreadyExists);
    }

    var period = PayrollPeriod.CreateAlignedTo(
      command.CompanyId,
      window.FiscalPeriodId.Value,
      command.Name ?? window.PeriodName,
      window.StartUtc!.Value,
      window.EndUtc!.Value,
      command.PayDateUtc);
    if (period.IsFailure)
    {
      return Result.Failure<Guid>(period.Error);
    }

    await periods.AddAsync(period.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      // ---- THE PAYROLL-PERIOD RACE (T-178).
      //
      // `IPayrollPeriodRepository.ExistsForFiscalPeriodAsync` is a read, so two callers can pass it with the same value and both reach this
      // save. **The unique index on `(TenantId, CompanyId, FiscalPeriodId)` decides it at commit**, and the loser reached `PayrollApiErrorMapper` with
      // an unmapped `Persistence.UniqueConstraint` — answered 500 for a plain business conflict, while
      // `PayrollErrors.PeriodAlreadyExists` sat mapped to 409 and unreturned on this path.
      //
      // **The race and the pre-check produce an IDENTICAL caller-visible condition**, so one code serves
      // both honestly, and **retrying the identical request fails again** — the caller must change the
      // input rather than repeat it.
      //
      // ⚠ **EVERY INDEX THIS SAVE CAN REACH MEANS THE SAME THING TO THE CALLER, WHICH IS THE ACTUAL TEST.**
      // This writes a `PayrollPeriod` and nothing else.
      if (saved.Error.Code == PersistenceErrorCodes.UniqueConstraint)
      {
        return Result.Failure<Guid>(PayrollErrors.PeriodAlreadyExists);
      }

      return Result.Failure<Guid>(saved.Error);
    }

    return Result.Success(period.Value.Id);
  }
}

public sealed class CreatePayrollRunCommandHandler(
  IPayrollRunRepository runs,
  IPayrollPeriodRepository periods,
  IPayrollScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result<Guid>> HandleAsync(
    CreatePayrollRunCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var authorized = await scope.AuthorizeAsync(
      PayrollPermissionNames.ManageRuns, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var period = await periods.GetByIdAsync(command.PayrollPeriodId, cancellationToken);
    if (period is null || period.CompanyId != command.CompanyId)
    {
      return Result.Failure<Guid>(PayrollErrors.PeriodNotFound);
    }

    // One run per company per period (`OD-PAY-0011` ruled reverse-and-rerun rather than superseding runs).
    // The unique index is the authority; this is the courteous answer.
    if (await runs.ExistsForPeriodAsync(command.CompanyId, command.PayrollPeriodId, cancellationToken))
    {
      return Result.Failure<Guid>(PayrollErrors.RunAlreadyExistsForPeriod);
    }

    var run = PayrollRun.Create(command.CompanyId, command.PayrollPeriodId);
    if (run.IsFailure)
    {
      return Result.Failure<Guid>(run.Error);
    }

    await runs.AddAsync(run.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      // ---- THE PAYROLL-RUN RACE (T-178).
      //
      // `IPayrollRunRepository.ExistsForPeriodAsync` is a read, so two callers can pass it with the same value and both reach this
      // save. **The FILTERED unique index on `(TenantId, CompanyId, PayrollPeriodId)` decides it at commit**, and the loser reached `PayrollApiErrorMapper` with
      // an unmapped `Persistence.UniqueConstraint` — answered 500 for a plain business conflict, while
      // `PayrollErrors.RunAlreadyExistsForPeriod` sat mapped to 409 and unreturned on this path.
      //
      // **The race and the pre-check produce an IDENTICAL caller-visible condition**, so one code serves
      // both honestly, and **retrying the identical request fails again** — the caller must change the
      // input rather than repeat it.
      //
      // ⚠ **EVERY INDEX THIS SAVE CAN REACH MEANS THE SAME THING TO THE CALLER, WHICH IS THE ACTUAL TEST.**
      // This writes a `PayrollRun` and nothing else — a new run has no draft lines, because lines arrive
      // at calculation, a later step.
      //
      // ---- ⚠ THE CONDITION IS "AN UNREVERSED RUN EXISTS", NOT "A RUN EXISTS". REVERSE-AND-RERUN IS LEGAL.
      //
      // The index carries `HasFilter("[ReversedUtc] IS NULL")` and `ExistsForPeriodAsync` filters the same
      // predicate — **one rule stated in two languages**, which is what makes the guard and the constraint
      // agree instead of merely coexist.
      //
      // **Wording it as "a run already exists" would read as forbidding the rerun**, and that is not a
      // hypothetical: before T-112 the guard matched a run in ANY state while the rule meant only unreversed
      // ones, so *"the correction is a NEW run for the same period"* **was refused by the database from the
      // day the comment claiming it worked was written.** The filter is what repaired it, on both sides.
      if (saved.Error.Code == PersistenceErrorCodes.UniqueConstraint)
      {
        return Result.Failure<Guid>(PayrollErrors.RunAlreadyExistsForPeriod);
      }

      return Result.Failure<Guid>(saved.Error);
    }

    return Result.Success(run.Value.Id);
  }
}

// ---- CALCULATION. Free before approval, impossible after (OD-PAY-0011).
//
// The handler's job is to ASSEMBLE inputs and let `PayrollCalculator` — which touches no repository and no
// clock — do the arithmetic. That separation is what makes the determinism scenarios assertable without a
// database, and it is why the roster, the compensation history and the element set are all read here.
public sealed class CalculatePayrollRunCommandHandler(
  IPayrollRunRepository runs,
  IPayrollPeriodRepository periods,
  IPayElementRepository elements,
  IEmployeeCompensationRepository compensation,
  IOneOffPaymentRepository oneOffPayments,
  IEmployeeRoster roster,
  IAttendanceSummary attendanceSummary,
  IPayrollScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentUser currentUser)
{
  public async Task<Result> HandleAsync(
    CalculatePayrollRunCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var run = await runs.GetWithDraftLinesAsync(command.PayrollRunId, cancellationToken);
    if (run is null)
    {
      return Result.Failure(PayrollErrors.RunNotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      PayrollPermissionNames.ManageRuns, run.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    var period = await periods.GetByIdAsync(run.PayrollPeriodId, cancellationToken);
    if (period is null)
    {
      return Result.Failure(PayrollErrors.PeriodNotFound);
    }

    // HR's fact, through HR's contract. Payroll never sees `Employee` (`ADR-012`, `DEC-PAY-0017`).
    var employment = await roster.GetEmploymentAsync(
      run.CompanyId, period.StartUtc, period.EndUtc, cancellationToken);

    var history = await compensation.GetHistoryForCompanyAsync(run.CompanyId, cancellationToken);
    var byEmployee = history.GroupBy(record => record.EmployeeId)
      .ToDictionary(group => group.Key, group => group.ToList());

    // ---- ONE-OFF PAY INSTRUCTIONS FOR THIS PERIOD (T-110). UNCONSUMED ONLY.
    //
    // Loaded before the loop and grouped, because an employee may hold several and the run must not query
    // per employee.
    var oneOffs = await oneOffPayments.GetUnconsumedForPeriodAsync(
      run.CompanyId, run.PayrollPeriodId, cancellationToken);
    var oneOffsByEmployee = oneOffs.GroupBy(payment => payment.EmployeeId)
      .ToDictionary(
        group => group.Key,
        group => (IReadOnlyList<OneOffPaymentInput>)[.. group.Select(payment =>
          new OneOffPaymentInput(payment.Id, payment.PayElementId, payment.Amount))]);

    var inputs = new List<PayrollEmployeeInput>();
    foreach (var record in employment)
    {
      oneOffsByEmployee.TryGetValue(record.EmployeeId, out var employeeOneOffs);

      // ---- THE RECORD IN FORCE ON THE PAY DATE, OR NONE (`OD-PAY-0003`).
      //
      // Derived, never a stored "current" flag.
      EmployeeCompensation? inForce = null;
      if (byEmployee.TryGetValue(record.EmployeeId, out var employeeHistory))
      {
        inForce = EmployeeCompensation.InForceOn(employeeHistory, period.PayDateUtc);
      }

      // ---- AN EMPLOYEE WITH NEITHER IS SKIPPED. ONE WITH A ONE-OFF IS NOT (T-110).
      //
      // The original rule was right and was right about ONE case: *"an employee with no compensation on file
      // is SKIPPED, not defaulted to zero — a zero-pay line would look like a decision somebody made, and an
      // absence is the truth."* That holds for a salaried employee whose record is missing.
      //
      // **It was silently wrong for someone who was never meant to have one.** A contractor paid once for a
      // job has no monthly, daily or hourly rate, so no compensation record exists to find, so they were
      // omitted from every run — no line, no error, no payslip. `OD-SS-0003` says such a person IS an
      // employee, so the roster carries them and only the compensation lookup failed them.
      //
      // **Now the two are distinguishable:** no compensation AND no one-off is still an absence and still
      // skipped; no compensation WITH a one-off is a person to be paid.
      if (inForce is null && (employeeOneOffs is null || employeeOneOffs.Count == 0))
      {
        continue;
      }

      // ---- THE ATTENDANCE INPUT (FP-013, REQ-ATT-0022), THROUGH THE CONTRACT AND NOTHING ELSE.
      //
      // `DEC-ATT-0002` and `ADR-012`: Payroll references `SSAS.Attendance.Contracts` and NOTHING under
      // `SSAS.Attendance.Domain`, `.Application`, `.Infrastructure` or `.API`. An architecture guard asserts
      // that in both directions.
      //
      // The period is named by a DATE inside it (`OD-ATT-0009`) — the pay date, which by `OD-PAY-0002`'s 1:1
      // alignment falls inside the payroll period. Attendance resolves which of ITS periods covers that
      // date, so no caller can express a straddle.
      //
      // ---- AN OPEN OR ABSENT PERIOD YIELDS ZERO HERE, AND IS REFUSED AT APPROVAL.
      //
      // Calculation COMMITS NOTHING and may be repeated — `OD-PAY-0009`'s reasoning, which is why the
      // sensitivity sits at approval. So an open attendance period does not fail here; it produces a run
      // with no attendance-driven lines, and `ApprovePayrollRunCommandHandler` refuses to bless it.
      //
      // Getting this ordering backwards would be worse in both directions: refusing at calculation would
      // stop an operator previewing anything before attendance closed, and refusing NOWHERE would let a run
      // computed from an open period be approved and posted to the ledger.
      var attendance = await attendanceSummary.GetForPeriodAsync(
        run.CompanyId, record.EmployeeId, period.PayDateUtc, cancellationToken);

      // ---- CONTRADICTORY DATA REFUSES THE RUN BEFORE ANY QUANTITY IS READ (T-121).
      //
      // Placed before the four reads below, because every one of them would otherwise turn a contradiction
      // into a number — zero, in each case, which is indistinguishable from an employee who simply did
      // nothing. **A refusal names the employee; a zero does not.**
      if (attendance.Status == AttendanceSummaryStatus.EmploymentDataContradictory)
      {
        return Result.Failure(PayrollErrors.AttendanceContradictsEmployment);
      }

      var overtimeByTier = attendance.Status == AttendanceSummaryStatus.Available
        ? attendance.OvertimeQuantityByTier
        : null;

      var unpaidAbsence = attendance.Status == AttendanceSummaryStatus.Available
        ? attendance.UnpaidAbsenceQuantity
        : 0m;

      // ---- WORKED HOURS (T-107). Read by `SalaryType.Hourly` and by nothing else.
      //
      // Absent attendance yields ZERO on the same rule as the two above, and for an hourly employee zero
      // hours means zero base pay — which is the correct answer, not a failure. An hourly employee with no
      // attendance recorded worked no hours; inventing a fallback would pay them for time nobody reported.
      var workedQuantity = attendance.Status == AttendanceSummaryStatus.Available
        ? attendance.WorkedQuantity
        : 0m;

      // ---- THE WORKING DAYS THIS EMPLOYEE WAS EMPLOYED FOR (T-115). Read by `SalaryType.Daily` alone.
      //
      // **Bounded to the employment window, not the period.** A daily employee is paid for working days they
      // were EMPLOYED for: before T-115 this was the period's total, so a joiner hired on the 16th of a
      // 21-working-day month was paid all 21 — and a leaver was paid through the end of the period after
      // termination. The daily arm consults no dates itself, so the clamp has to happen here.
      //
      // The window is the same one `PayrollCalculator.ProrationFactor` computes for the monthly path:
      // start at the later of hire and period start, end at the earlier of termination and period end.
      //
      // ---- AND IT STAYS ON THE `Available` BRANCH, WHICH IS LOAD-BEARING RATHER THAN TIDY.
      //
      // `GetWorkingDaysAsync` reads the COMPANY's calendar. It knows nothing about whether this employee's
      // attendance arrived, and would happily answer 21 for someone whose summary was unavailable — while
      // `UnpaidAbsenceQuantity` stayed 0. **A daily employee would go from REFUSED to PAID IN FULL with no
      // absence deduction, silently**, which is the same failure class T-115 exists to remove.
      //
      // Zero here keeps the refusal: `PayrollCalculator` fails the run with
      // `PayrollErrors.DailySalaryHasNoWorkingDays` rather than paying nobody's idea of a number. **A
      // daily-salaried employee reported as zero days is VISIBLY wrong on a payslip; any other placeholder
      // is invisibly wrong.** That reasoning was written on `AttendanceSummaryResult.StandardWorkingDays`,
      // which T-115 removed, so it lives here now — the behaviour outlived the field it was written on.
      var employedFrom = record.EmploymentDateUtc.ToUniversalTime().Date > period.StartUtc.Date
        ? record.EmploymentDateUtc.ToUniversalTime().Date
        : period.StartUtc.Date;

      var employedTo = record.TerminationDateUtc is { } terminated
        && terminated.ToUniversalTime().Date < period.EndUtc.Date
        ? terminated.ToUniversalTime().Date
        : period.EndUtc.Date;

      var standardWorkingDays = attendance.Status == AttendanceSummaryStatus.Available
        ? await attendanceSummary.GetWorkingDaysAsync(
            run.CompanyId, DateOnly.FromDateTime(employedFrom), DateOnly.FromDateTime(employedTo),
            cancellationToken)
        : 0;

      inputs.Add(new PayrollEmployeeInput(
        record.EmployeeId, record.EmploymentDateUtc, record.TerminationDateUtc, inForce,
        overtimeByTier, unpaidAbsence, workedQuantity, standardWorkingDays, employeeOneOffs));
    }

    var active = await elements.GetActiveForCompanyAsync(run.CompanyId, cancellationToken);

    var calculated = PayrollCalculator.Calculate(run.Id, period, inputs, active);
    if (calculated.IsFailure)
    {
      return Result.Failure(calculated.Error);
    }

    // ---- THE PREVIOUS DRAFT LINES ARE DELETED EXPLICITLY, BEFORE THE NEW SET REPLACES THEM.
    //
    // `SetCalculation` clears the collection, and the platform forbids cascades: `PersistenceDbContext`
    // sets EVERY foreign key to `Restrict` after the module contributors run. So an orphan is a row nothing
    // deletes and a non-nullable foreign key EF cannot null — the save fails outright.
    //
    // **Recalculation was broken on main because of this**, on the ordinary path an operator takes when a
    // preview is wrong: fix the element, calculate again.
    //
    // It runs before `SetCalculation` rather than after, because the fixer reacts to the severance the
    // moment the collection is cleared.
    await runs.RemoveDraftLinesAsync(run, cancellationToken);

    var set = run.SetCalculation(calculated.Value, currentUser.UserId);
    if (set.IsFailure)
    {
      return set;
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}

// ---- APPROVAL. The sensitive act, and the last point at which anything can be refused.
//
// Two checks live here rather than at posting, both because `OD-PAY-0014` and `OD-PAY-0012` put them here,
// and both for the same underlying reason: a run that reached Approved and could not post would be in a
// state with no legitimate exit. It cannot return to Draft — approval already happened — and it cannot go
// forward.
public sealed class ApprovePayrollRunCommandHandler(
  IPayrollRunRepository runs,
  IPayrollPeriodRepository periods,
  IPayElementRepository elements,
  IOneOffPaymentRepository oneOffPayments,
  IJournalPoster ledger,
  IAttendanceSummary attendanceSummary,
  IPayrollScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentUser currentUser)
{
  public async Task<Result> HandleAsync(
    ApprovePayrollRunCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var run = await runs.GetWithDraftLinesAsync(command.PayrollRunId, cancellationToken);
    if (run is null)
    {
      return Result.Failure(PayrollErrors.RunNotFound);
    }

    // `Payroll.Runs.Approve`, NOT `ManageRuns`. `BR-PLT-0103` names payroll processing sensitive and
    // `OD-PAY-0009` placed the sensitive act here, so preparing and authorizing can be different people.
    var authorized = await scope.AuthorizeAsync(
      PayrollPermissionNames.ApproveRuns, run.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    var period = await periods.GetByIdAsync(run.PayrollPeriodId, cancellationToken);
    if (period is null)
    {
      return Result.Failure(PayrollErrors.PeriodNotFound);
    }

    // ---- MAPPING, CHECKED AT APPROVAL (OD-PAY-0012).
    //
    // Every element the run actually used must exist, be active, and be mapped. The refusal NAMES the
    // element, because "a pay element is unmapped" sends a user hunting through the whole element list.
    var usedIds = run.DraftLines.Select(line => line.PayElementId).Distinct().ToArray();
    var used = await elements.GetByIdsAsync(usedIds, cancellationToken);
    var byId = used.ToDictionary(element => element.Id);

    foreach (var id in usedIds)
    {
      if (!byId.TryGetValue(id, out var element))
      {
        return Result.Failure(PayElementErrors.NotFound);
      }

      if (!element.IsActive)
      {
        return Result.Failure(PayElementErrors.Inactive(element.Code.Value));
      }

      if (element.GlAccountId is null)
      {
        return Result.Failure(PayElementErrors.Unmapped(element.Code.Value));
      }
    }

    // ================================================================================================
    // THE ATTENDANCE GATE (FP-013, OD-ATT-0010) — THE InspectPostingWindowAsync PATTERN, INVERTED.
    // ================================================================================================
    //
    // `OD-ATT-0010` ruled (a): **attendance periods close, and Payroll refuses an open one.**
    //
    // The reason is not tidiness. A run calculated from a period still being edited is a snapshot of a
    // moving target — and approval is the assertion that these are the amounts these people will be paid,
    // after which the lines are append-only and the run posts to the ledger. **A wrong snapshot becomes a
    // posted journal entry, and reversing one of those is a business event rather than a fix.**
    //
    // Checked HERE and not at calculation, on `OD-PAY-0009`'s reasoning: calculation commits nothing and may
    // be repeated, so an operator can preview a run while attendance is still being recorded. Approval is
    // where the world stops moving, so approval is where it must have stopped.
    //
    // ---- WHY IT IS AN INSPECTION AND NOT AN EXCEPTION.
    //
    // `IJournalPoster.InspectPostingWindowAsync` let Payroll ask GL whether a period was open BEFORE
    // composing a journal, so the refusal arrived before the work rather than after it. This is the same
    // shape pointing the other way, and `AttendanceSummaryStatus` is a closed enum for the same reason
    // `JournalPostingStatus` is: every outcome is a value the compiler can see a caller ignoring.
    //
    // Named by the PAY DATE, which under `OD-PAY-0002`'s 1:1 alignment falls inside the payroll period —
    // the same date the calculation used, so the gate and the data cannot disagree about which attendance
    // period is meant.
    var attendance = await attendanceSummary.InspectPeriodAsync(
      run.CompanyId, period.PayDateUtc, cancellationToken);

    // `PeriodNotFound` is NOT refused. A company that records no attendance at all has no attendance period,
    // and refusing would make this feature a prerequisite for running payroll — which would break every
    // existing tenant on the day it shipped. The refusal is narrow on purpose: a period that EXISTS and is
    // still OPEN is the dangerous state, because it means somebody is recording into the numbers this run
    // was computed from.
    if (attendance.Status == AttendanceSummaryStatus.PeriodOpen)
    {
      return Result.Failure(PayrollErrors.AttendancePeriodOpen);
    }

    // The balancing credit must be mapped too, or the journal cannot balance and `BR-GL-0001` would refuse
    // it at posting — which is exactly the stranded-Approved state this check exists to prevent.
    var payable = (await elements.GetActiveForCompanyAsync(run.CompanyId, cancellationToken))
      .FirstOrDefault(element => element.Behaviour == PayElementBehaviour.NetPayPayable);

    if (payable is null || payable.GlAccountId is null)
    {
      return Result.Failure(PayElementErrors.Unmapped("net pay payable"));
    }

    // ---- THE CLOSED-PERIOD REFUSAL, NAMING THE PERIOD (OD-PAY-0014).
    var window = await ledger.InspectPostingWindowAsync(
      run.CompanyId, period.PayDateUtc, cancellationToken);

    if (window.Status == PostingWindowStatus.PeriodClosed)
    {
      return Result.Failure(PayrollErrors.PeriodClosedForPosting(window.PeriodName ?? period.Name));
    }

    if (!window.IsOpen)
    {
      return Result.Failure(PayrollErrors.FiscalPeriodNotFound);
    }

    var approved = run.Approve(currentUser.UserId);
    if (approved.IsFailure)
    {
      return approved;
    }

    // ---- APPROVAL IS WHAT CONSUMES A ONE-OFF PAY INSTRUCTION (T-110).
    //
    // **Not calculation.** `SetCalculation` refuses only `Approved` and `Posted`, so a draft may be
    // recalculated any number of times or abandoned entirely — an instruction consumed there would be
    // consumed by something that might never pay anybody. **Re-running before approval therefore re-includes
    // it and produces the same line: idempotence for free, with no flag to reset.**
    //
    // Consumed in the SAME transaction as the approval. The `Approve` above and these writes commit
    // together or not at all, so there is no window in which a run is approved and its instructions still
    // look payable.
    //
    // ---- IT REFUSES RATHER THAN SKIPPING, AND THE AGGREGATE IS WHAT REFUSES.
    //
    // `MarkConsumedBy` fails if the instruction already names a run. Reaching that would mean this run's
    // lines contain an instruction another run already paid, and continuing would pay it twice. Failing here
    // aborts the approval with nothing written.
    var payableOneOffs = await oneOffPayments.GetUnconsumedForPeriodAsync(
      run.CompanyId, run.PayrollPeriodId, cancellationToken);

    foreach (var payment in payableOneOffs)
    {
      var consumed = payment.MarkConsumedBy(run.Id, run.PayrollPeriodId);
      if (consumed.IsFailure)
      {
        return consumed;
      }
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}

// ---- POSTING. Synchronous, and a failure REFUSES THE TRANSITION (OD-PAY-0013).
//
// The run reaches `Posted` only after the ledger has actually accepted the journal. That is what makes "a
// run cannot claim it posted when it did not" true rather than aspirational, and it is the property that
// decided the contract's shape against an event.
public sealed class PostPayrollRunCommandHandler(
  IPayrollRunRepository runs,
  IPayrollPeriodRepository periods,
  IPayElementRepository elements,
  IJournalPoster ledger,
  IPayrollScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentUser currentUser)
{
  public async Task<Result> HandleAsync(
    PostPayrollRunCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var run = await runs.GetWithLinesAsync(command.PayrollRunId, cancellationToken);
    if (run is null)
    {
      return Result.Failure(PayrollErrors.RunNotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      PayrollPermissionNames.PostRuns, run.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    if (run.Status != PayrollRunStatus.Approved)
    {
      return Result.Failure(PayrollErrors.RunNotPostable(run.Status));
    }

    var period = await periods.GetByIdAsync(run.PayrollPeriodId, cancellationToken);
    if (period is null)
    {
      return Result.Failure(PayrollErrors.PeriodNotFound);
    }

    var companyElements = await elements.GetActiveForCompanyAsync(run.CompanyId, cancellationToken);
    var journalLines = ComposeJournal(run, companyElements);
    if (journalLines.IsFailure)
    {
      return Result.Failure(journalLines.Error);
    }

    var outcome = await ledger.PostAsync(
      new JournalPostingRequest(
        run.CompanyId,
        period.PayDateUtc,
        $"Payroll {period.Name}",
        run.Id.ToString(),
        journalLines.Value),
      cancellationToken);

    if (!outcome.IsPosted)
    {
      // The transition is refused, and the closed-period case still names its period even here — the window
      // inspected at approval is not a reservation, so a period can close in between and this is the honest
      // answer when it does.
      return outcome.Status == JournalPostingStatus.PeriodClosed
        ? Result.Failure(PayrollErrors.PeriodClosedForPosting(outcome.PeriodName ?? period.Name))
        : Result.Failure(PayrollErrors.LedgerRefusedPosting);
    }

    var posted = run.MarkPosted(outcome.JournalEntryId!.Value, currentUser.UserId);
    if (posted.IsFailure)
    {
      return posted;
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }

  // ---- HOW A PAYROLL BECOMES A BALANCED JOURNAL.
  //
  // Earnings DEBIT their mapped accounts (a cost the company incurred). Deductions CREDIT theirs (amounts
  // withheld and owed onward). Neither side balances the other: the difference is NET PAY, credited to the
  // payable account, which is what the company now owes its employees.
  //
  // Amounts are the STORED line amounts, already rounded (`OD-PAY-0008`), so the journal's two sides are the
  // same numbers the payslip shows. Recomputing here would risk a journal that balanced against figures no
  // employee could see.
  private static Result<IReadOnlyList<JournalPostingLine>> ComposeJournal(
    PayrollRun run, IReadOnlyList<PayElement> companyElements)
  {
    var byId = companyElements.ToDictionary(element => element.Id);
    var lines = new List<JournalPostingLine>();

    // Grouped per account so a journal has one line per account rather than one per employee — a hundred
    // employees on one salary-expense account is one ledger line, not a hundred. The payslip keeps the
    // per-employee detail; the ledger records the movement.
    foreach (var group in run.Lines.GroupBy(line => line.PayElementId))
    {
      if (!byId.TryGetValue(group.Key, out var element) || element.GlAccountId is null)
      {
        return Result.Failure<IReadOnlyList<JournalPostingLine>>(PayElementErrors.Unmapped(group.Key.ToString()));
      }

      var total = group.Sum(line => line.Amount);
      if (total == 0m)
      {
        continue;
      }

      lines.Add(element.Kind == PayElementKind.Earning
        ? new JournalPostingLine(element.GlAccountId.Value, total, 0m, element.Name.Value)
        : new JournalPostingLine(element.GlAccountId.Value, 0m, total, element.Name.Value));
    }

    var payable = companyElements.FirstOrDefault(
      element => element.Behaviour == PayElementBehaviour.NetPayPayable);

    if (payable?.GlAccountId is null)
    {
      return Result.Failure<IReadOnlyList<JournalPostingLine>>(PayElementErrors.Unmapped("net pay payable"));
    }

    var net = run.NetPay;
    if (net != 0m)
    {
      lines.Add(new JournalPostingLine(payable.GlAccountId.Value, 0m, net, "Net pay payable"));
    }

    return Result.Success<IReadOnlyList<JournalPostingLine>>(lines);
  }
}

// ---- CORRECTION IS A REVERSAL (DEC-PAY-0012, OD-PAY-0011).
//
// There is no edit path and there never will be: a posted run's lines are `IAppendOnlyEntity` and its
// journal is append-only. Correcting means reversing the journal and running the period again.
public sealed class ReversePayrollRunCommandHandler(
  IPayrollRunRepository runs,
  IJournalPoster ledger,
  IPayrollScopeResolver scope,
  // ---- NEW IN T-112, AND ITS ABSENCE UNTIL NOW WAS THE TELL.
  //
  // This handler took no unit of work because it wrote nothing: it called the ledger and returned. **A
  // command handler that persists nothing was the visible shape of the run never recording its own
  // reversal**, and the comment below claimed a correction workflow that the database refused.
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result<Guid>> HandleAsync(
    ReversePayrollRunCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var run = await runs.GetByIdAsync(command.PayrollRunId, cancellationToken);
    if (run is null)
    {
      return Result.Failure<Guid>(PayrollErrors.RunNotFound);
    }

    // Reversing a payroll unwinds a ledger posting, so it takes the POSTING permission rather than the
    // approval one — it is the same authority being exercised in the opposite direction.
    var authorized = await scope.AuthorizeAsync(
      PayrollPermissionNames.PostRuns, run.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    if (run.Status != PayrollRunStatus.Posted || run.JournalEntryId is null)
    {
      return Result.Failure<Guid>(PayrollErrors.RunNotReversible);
    }

    var outcome = await ledger.ReverseAsync(
      new JournalReversalRequest(run.JournalEntryId.Value, command.ReversalDateUtc, command.Description),
      cancellationToken);

    if (!outcome.IsPosted)
    {
      return outcome.Status == JournalPostingStatus.PeriodClosed
        ? Result.Failure<Guid>(PayrollErrors.PeriodClosedForPosting(outcome.PeriodName ?? "the reversal period"))
        : Result.Failure<Guid>(PayrollErrors.LedgerRefusedReversal);
    }

    // ---- THE RUN KEEPS ITS STATUS AND ITS JOURNAL, AND GAINS ONE FACT (T-112).
    //
    // **This block used to write nothing**, reasoning that marking the run reversed would duplicate a claim
    // GL makes better — and that reasoning was right for GL's purposes. It still is: `Status` stays
    // `Posted`, `JournalEntryId` still names the original, and nothing here restates what the ledger holds.
    //
    // **What changed is that PAYROLL needs the fact for a purpose of its own.** One run per period is
    // enforced by a unique index, and until T-112 that index could not tell a reversed period from a live
    // one — so *"the correction is a NEW run for the same period"*, which this comment asserted, **was
    // refused by the database from the day it was written.** `ExistsForPeriodAsync` matched any run in any
    // state, and no second run for the period could ever be created.
    //
    // **A filtered unique index cannot read GL's tables**, so the fact is stamped here and the index filters
    // on it. The correction really is a new run for the same period now.
    var reversed = run.MarkReversed();
    if (reversed.IsFailure)
    {
      return Result.Failure<Guid>(reversed.Error);
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saved.IsFailure
      ? Result.Failure<Guid>(saved.Error)
      : Result.Success(outcome.JournalEntryId!.Value);
  }
}
