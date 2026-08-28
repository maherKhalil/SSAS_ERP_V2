using SSAS.BuildingBlocks.Domain;

namespace SSAS.Payroll.Domain.Compensation;

// ==================================================================================================
// A ONE-OFF PAY INSTRUCTION (T-110). AN EVENT, NOT A RATE.
// ==================================================================================================
//
// ---- WHY THIS IS NOT A FOURTH `SalaryType`.
//
// `Monthly`, `Daily` and `Hourly` are RATES applied to a period: given the period, each answers "how much".
// A one-off payment answers nothing about a period — it happens once and then it is over. Same enum would
// have meant one kind of thing wearing two.
//
// **And `EmployeeCompensation` could not have carried it.** That aggregate deliberately has no
// `EffectiveToUtc` and no `IsCurrent` — *"the end of one record is the start of the next"* — and a one-off
// HAS no next record. Forcing it in means adding the exact field that design refused, which is how a
// deliberate absence becomes an accident.
//
// **Nor could a `FixedAmount` assignment.** An assignment hangs off a compensation record, and that record
// also carries `BaseAmount` — so adding a one-off through it would permanently restate what the employee is
// paid, in the history `DEC-POS-0023` exists to keep honest.
//
// ---- WHAT IT IS FOR, AND THE DEFECT IT CLOSES.
//
// **An employee with a one-off and NO compensation record was omitted from every payroll run** — no line, no
// error, no payslip (`PayrollRunCommandHandlers`, the two skips). That is the owner's case: a contractor
// paid once for a job has no monthly, daily or hourly rate and therefore no compensation record.
//
// **`OD-SS-0003` settles who the payee is:** an external accountant who is paid **is an employee**, with an
// employment type of part-time or freelance. The owner rejected the alternative explicitly. So a one-off is
// paid to an `Employee` who appears in HR's employment roster, and paying a non-employee is a different
// question that ruling already answered.
//
// ---- IT NEEDS NOTHING FROM ANOTHER MODULE, AND THAT IS WORTH KEEPING.
//
// Nothing from Attendance — no quantity prices it. Nothing from HR beyond the employment fact Payroll
// already receives through the roster contract. It references a `PayElement` for its KIND and GL account,
// which is Payroll's own. **The first construct in this sequence that adds no boundary crossing.**
public sealed class OneOffPayment
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  private OneOffPayment(
    Guid id,
    Guid companyId,
    Guid employeeId,
    Guid payrollPeriodId,
    Guid payElementId,
    decimal amount,
    string? reason)
    : base(id)
  {
    CompanyId = companyId;
    EmployeeId = employeeId;
    PayrollPeriodId = payrollPeriodId;
    PayElementId = payElementId;
    Amount = amount;
    Reason = reason;
  }

  // EF materialization only.
  private OneOffPayment(Guid id)
    : base(id)
  {
  }

  public Guid OneOffPaymentId => Id;

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public Guid EmployeeId { get; private set; }

  // ---- SCOPED TO THE PERIOD, NOT THE PAY DATE, AND THAT IS DELIBERATE (T-110).
  //
  // The run INCLUDES employees by period (`PayrollPeriod.Includes`) and selects their compensation by PAY
  // DATE (`EmployeeCompensation.InForceOn(history, period.PayDateUtc)`). **Those are different questions and
  // they can disagree** — an employee terminated inside the period is included, while the compensation in
  // force on a pay date after their termination is whatever the history happens to say.
  //
  // That asymmetry is live today and is not this construct's to fix. **But a new construct must not inherit
  // an inconsistency it has no reason to carry**, so this binds to the period an operator names.
  public Guid PayrollPeriodId { get; private set; }

  // ---- IT REFERENCES A PAY ELEMENT FOR ITS KIND AND ITS GL ACCOUNT.
  //
  // A line needs a `PayElementKind` and a `GlAccountId`, and an element already carries both. The element
  // says WHAT KIND of pay this is and where it posts; this instruction says how much, to whom, and when.
  // Inventing a second way to name an account would give Payroll two answers to "where does this post".
  public Guid PayElementId { get; private set; }

  public decimal Amount { get; private set; }

  public string? Reason { get; private set; }

  // ---- CONSUMPTION IS A REFERENCE, NOT A FLAG, AND IT IS WRITTEN AT APPROVAL.
  //
  // **A reference, because every payroll question about a payment is "which run".** A boolean records THAT
  // it was paid and never BY WHAT, and reconciliation, a reversal and a dispute all need the second.
  //
  // **At APPROVAL rather than at calculation, because calculation is repeatable by design.** `SetCalculation`
  // refuses only `Approved` and `Posted`, so a draft may be recalculated or abandoned — an instruction
  // consumed by a draft would be consumed by something that might never pay it. Approval is already the
  // once-only act (`BR-PLT-0103`, `OD-PAY-0009`).
  //
  // **So a re-run before approval re-includes it and produces the same line: idempotence for free, with no
  // flag to reset.**
  //
  // ---- ⚠ WHAT HAPPENS ON A REVERSAL IS UNRESOLVED, AND THE REASON IS NOT "WE DID NOT DECIDE" (T-110).
  //
  // The ruling was that reversing a run should restore the obligation, expressed as *consumed = there exists
  // an APPROVED, UNREVERSED run holding this*. **It is not implemented, and it is not implementable usefully
  // yet, for two reasons found by reading the reversal path:**
  //
  //   1. A reversal does NOT mark the run. `ReversePayrollRunCommandHandler` leaves it `Posted` deliberately
  //      — GL derives reversal from the reversing entry's existence rather than flagging the original — and
  //      GL's `IsReversed` lives on its READ models, not on the posting contract Payroll holds.
  //   2. **A reversed run cannot be corrected at all.** `ExistsForPeriodAsync` matches any run for the period
  //      with no status filter, and `ExistsForFiscalPeriodAsync` blocks a second period. `OD-PAY-0011` ruled
  //      reverse-and-rerun; the code refuses it.
  //
  // **So there is nothing for a one-off to survive INTO.** Consuming unconditionally makes it exactly as
  // un-restorable as every other line in a reversed run — salary, elements, all of it — which is consistent
  // rather than uniquely bad. **Revisit when reverse-and-rerun exists (T-111), not before:** until then the
  // correct predicate would be a cross-module read serving a scenario the product cannot reach.
  public Guid? ConsumedByPayrollRunId { get; private set; }

  public bool IsConsumed => ConsumedByPayrollRunId is not null;

  public DateTimeOffset CreatedUtc { get; set; }

  public string? CreatedBy { get; set; }

  public DateTimeOffset ModifiedUtc { get; set; }

  public string? ModifiedBy { get; set; }

  public static Result<OneOffPayment> Create(
    Guid companyId,
    Guid employeeId,
    Guid payrollPeriodId,
    Guid payElementId,
    decimal amount,
    string? reason = null)
  {
    if (companyId == Guid.Empty)
    {
      return Result.Failure<OneOffPayment>(OneOffPaymentErrors.CompanyRequired);
    }

    if (employeeId == Guid.Empty)
    {
      return Result.Failure<OneOffPayment>(OneOffPaymentErrors.EmployeeRequired);
    }

    if (payrollPeriodId == Guid.Empty)
    {
      return Result.Failure<OneOffPayment>(OneOffPaymentErrors.PeriodRequired);
    }

    if (payElementId == Guid.Empty)
    {
      return Result.Failure<OneOffPayment>(OneOffPaymentErrors.PayElementRequired);
    }

    // ZERO IS REFUSED, not merely negatives. A zero one-off is an instruction to pay nothing, which the
    // zero-line rule would then suppress — leaving a record somebody created and no line anywhere,
    // indistinguishable from an instruction that was never written.
    if (amount <= 0m)
    {
      return Result.Failure<OneOffPayment>(OneOffPaymentErrors.AmountNotPositive);
    }

    return Result.Success(new OneOffPayment(
      Guid.NewGuid(), companyId, employeeId, payrollPeriodId, payElementId, amount,
      string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));
  }

  // ---- IT NAMES THE RUN THAT PAID IT, AND A REVERSED RUN DID NOT PAY IT (T-123).
  //
  // **T-110 refused any second consumption outright, and that was right while it stood** — a reversal wrote
  // nothing on the run, so "paid, then unpaid" was not a state Payroll could express, and refusing was the
  // only way to prevent a double payment.
  //
  // **T-112 gave the run a `ReversedUtc`, so the ruled predicate is now expressible:** *consumed = an
  // APPROVED, UNREVERSED run holds this*. A correcting run legitimately takes over an instruction whose
  // first run was reversed, and refusing that would strand an unpaid obligation — **extinguishing a debt by
  // an accounting action, which the ruling rejected.**
  //
  // ---- SO WHERE DOES THE DOUBLE-PAYMENT INVARIANT LIVE NOW? NOT HERE, AND THAT IS DELIBERATE.
  //
  // **It is structural, in two places that cannot drift:**
  //
  //   1. `IOneOffPaymentRepository.GetUnconsumedForPeriodAsync` returns an instruction only when it is
  //      unconsumed OR the run naming it is reversed. **A live run's instructions are never offered again.**
  //   2. `PayrollRunConfiguration`'s filtered unique index permits **one UNREVERSED run per period**, so two
  //      live runs cannot both reach the same instruction.
  //
  // **An aggregate check here would be a third statement of a rule those two already enforce**, and the
  // failure it guarded against — two live runs consuming one instruction — is now impossible at the
  // database rather than merely refused in memory.
  //
  // **What is still refused is a run consuming an instruction it already holds**, which is not a correction
  // but a defect in the approval path repeating itself.
  public Result MarkConsumedBy(Guid payrollRunId)
  {
    if (payrollRunId == Guid.Empty)
    {
      return Result.Failure(OneOffPaymentErrors.ConsumingRunRequired);
    }

    if (ConsumedByPayrollRunId == payrollRunId)
    {
      return Result.Failure(OneOffPaymentErrors.AlreadyConsumed);
    }

    ConsumedByPayrollRunId = payrollRunId;
    return Result.Success();
  }
}
