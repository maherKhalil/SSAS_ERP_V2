using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.Domain.Leave;

// ================================================================================================
// AN ADMINISTERED BALANCE (REQ-ATT-0011, REQ-ATT-0015; OD-ATT-0006).
// ================================================================================================
//
// `OD-ATT-0006` ruled **ADMINISTERED, accrual deferred.** An administrator sets the entitlement; approved
// requests consume it. No accrual engine, no carry-over caps, no expiry, no seniority tiers.
//
// **The deferral is safe in one direction only, and that asymmetry is why it was chosen:** an administered
// balance is a STRICT SUBSET of an accrued one, so accrual can be added later — as a dated ledger plus this
// as its projection — without invalidating a single stored row. The reverse is not true. Shipping accrual
// now and simplifying later would strand data nobody could reinterpret.
//
// COMPANY-OWNED, NOT branch-owned. Entitlement is company policy; the employee's branch is HR's fact and
// does not meter their leave. Asserted negatively per `DEC-ATT-0014`.
public sealed class LeaveBalance
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  private LeaveBalance(
    Guid id, Guid companyId, Guid employeeId, Guid leaveTypeId, int periodYear, decimal entitlementQuantity)
    : base(id)
  {
    CompanyId = companyId;
    EmployeeId = employeeId;
    LeaveTypeId = leaveTypeId;
    PeriodYear = periodYear;
    EntitlementQuantity = entitlementQuantity;
    ConsumedQuantity = 0m;
  }

  // EF materialization only.
  private LeaveBalance(Guid id)
    : base(id)
  {
  }

  public Guid LeaveBalanceId => Id;

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public Guid EmployeeId { get; private set; }

  public Guid LeaveTypeId { get; private set; }

  // The entitlement year. An `int` rather than a date range because an administered entitlement is granted
  // per year and nothing here computes across the boundary — `OD-ATT-0006` deferred carry-over along with
  // the rest of accrual, and modelling a range would imply a carry-over story that does not exist.
  public int PeriodYear { get; private set; }

  public decimal EntitlementQuantity { get; private set; }

  // ---- NEVER DIRECTLY SETTABLE (AC-ATT-0040).
  //
  // The private setter is the whole point. An administrator sets ENTITLEMENT; consumption is a consequence
  // of approvals and nothing else. A settable consumed figure would let someone reconcile a balance by
  // typing over it, and the leave that produced the discrepancy would still be sitting in the request table
  // saying otherwise.
  public decimal ConsumedQuantity { get; private set; }

  public decimal RemainingQuantity => EntitlementQuantity - ConsumedQuantity;

  public DateTimeOffset CreatedUtc { get; set; }

  public string? CreatedBy { get; set; }

  public DateTimeOffset ModifiedUtc { get; set; }

  public string? ModifiedBy { get; set; }

  public byte[] RowVersion { get; private set; } = [];

  public static Result<LeaveBalance> Create(
    Guid companyId, Guid employeeId, Guid leaveTypeId, int periodYear, decimal entitlementQuantity)
  {
    if (companyId == Guid.Empty)
    {
      return Result.Failure<LeaveBalance>(LeaveErrors.CompanyRequired);
    }

    if (employeeId == Guid.Empty || leaveTypeId == Guid.Empty)
    {
      return Result.Failure<LeaveBalance>(LeaveErrors.BalanceSubjectRequired);
    }

    if (periodYear is < 2000 or > 2999)
    {
      return Result.Failure<LeaveBalance>(LeaveErrors.InvalidPeriodYear);
    }

    if (entitlementQuantity < 0m)
    {
      return Result.Failure<LeaveBalance>(LeaveErrors.NegativeEntitlement);
    }

    return Result.Success(new LeaveBalance(Guid.NewGuid(), companyId, employeeId, leaveTypeId, periodYear, entitlementQuantity));
  }

  public Result SetEntitlement(decimal entitlementQuantity)
  {
    if (entitlementQuantity < 0m)
    {
      return Result.Failure(LeaveErrors.NegativeEntitlement);
    }

    // ---- AN ENTITLEMENT MAY BE REDUCED BELOW WHAT IS ALREADY CONSUMED, AND THAT IS DELIBERATE.
    //
    // Refusing it would be worse. Leave has genuinely been taken; the entitlement was genuinely wrong. The
    // honest outcome is a negative remaining balance that somebody can see and act on — not a refusal that
    // leaves the wrong entitlement standing because the right one is inconvenient to record.
    EntitlementQuantity = entitlementQuantity;
    return Result.Success();
  }

  // Called only from `LeaveRequest` approval, through the handler. `OD-ATT-0006` put the movement at
  // APPROVAL rather than submission: submission reserves nothing, so two requests can be submitted against
  // a balance covering one, and the second APPROVAL is what fails.
  //
  // The friendlier alternative — reserve on submission — needs a reservation released on rejection, on
  // cancellation and on expiry, which is three more paths to get wrong for a better error message.
  public Result Consume(decimal quantity)
  {
    if (quantity <= 0m)
    {
      return Result.Failure(LeaveErrors.InvalidConsumption);
    }

    if (ConsumedQuantity + quantity > EntitlementQuantity)
    {
      return Result.Failure(LeaveErrors.InsufficientBalance);
    }

    ConsumedQuantity += quantity;
    return Result.Success();
  }

  // Cancelling an approved request returns what it consumed. Guarded against going below zero, which would
  // mean the same request was released twice.
  public Result Release(decimal quantity)
  {
    if (quantity <= 0m)
    {
      return Result.Failure(LeaveErrors.InvalidConsumption);
    }

    if (quantity > ConsumedQuantity)
    {
      return Result.Failure(LeaveErrors.ReleaseExceedsConsumption);
    }

    ConsumedQuantity -= quantity;
    return Result.Success();
  }
}
