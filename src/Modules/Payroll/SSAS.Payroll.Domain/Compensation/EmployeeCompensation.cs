using SSAS.BuildingBlocks.Domain;

namespace SSAS.Payroll.Domain.Compensation;

// ================================================================================================
// THE DEC-POS-0023 SLOT. WHAT AN INDIVIDUAL IS PAID LIVES HERE AND NOWHERE ELSE.
// ================================================================================================
//
// `DEC-POS-0023` left a deliberate vacancy: FP-008 added no salary, wage, rate or pay column to `Employee`,
// because *"a Salary Grade is a band attached to a job; what an individual is paid is Payroll."* This type
// is that vacancy filled, **on the Payroll side of the line**. `DEC-PAY-0014` and `DEC-PAY-0015` keep the
// line where it was: Payroll reads HR and writes nothing back, and no compensation value ever lands on an
// HR record.
//
// ---- DATED HISTORY, NOT A CURRENT VALUE (OD-PAY-0003, RULED option 2).
//
// A compensation record is a POINT IN A SERIES. The value in force on a date is **derived** by selecting the
// record with the greatest `EffectiveFromUtc` not after that date — and that is the whole reason a past
// payroll run can be reproduced. `OD-PAY-0003` option 1 (one overwritten amount) was refused because it
// destroys history that cannot be reconstructed once it was never written.
//
// **There is no `EffectiveToUtc` and no `IsCurrent` flag.** The end of one record is the start of the next.
// Both a stored end date and a stored current-flag are derived state that drifts, and both must be
// maintained transactionally on every insert — the shape this codebase has refused before (`Account`'s
// immutable code, `FiscalYear`'s contiguity validated as a set).
//
// **There is no `RowVersion` either**, and that is not an omission. A history row is never updated, so there
// is no concurrent update for a version to detect. `RowVersion` belongs on mutable aggregates
// (`DEC-PAY-0009`), and putting one here would advertise an update path that does not exist.
public sealed class EmployeeCompensation
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  private readonly List<PayElementAssignment> assignments = [];

  private EmployeeCompensation(
    Guid id,
    Guid companyId,
    Guid employeeId,
    DateTimeOffset effectiveFromUtc,
    decimal baseAmount)
    : base(id)
  {
    CompanyId = companyId;
    EmployeeId = employeeId;
    EffectiveFromUtc = effectiveFromUtc;
    BaseAmount = baseAmount;
  }

  // EF materialization only.
  private EmployeeCompensation(Guid id)
    : base(id)
  {
  }

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  // ---- THE HR EMPLOYEE, BY IDENTIFIER ONLY.
  //
  // No navigation property, and no reference to `SSAS.HR.Domain`. `ADR-012` forbids a module referencing
  // another module's assemblies, and the same discipline applies in the domain: a Payroll aggregate holding
  // an HR `Employee` would make the two modules one module.
  public Guid EmployeeId { get; private set; }

  public DateTimeOffset EffectiveFromUtc { get; private set; }

  // `decimal(19,4)` at rest (`ADR-027`, `DEC-PAY-0004`). Always positive.
  public decimal BaseAmount { get; private set; }

  // ---- THE BAND OBSERVATION (REQ-PAY-0006, OD-PAY-0004, RULED option 1: INFORMATIONAL).
  //
  // `OD-PAY-0004` ruled that an amount outside the employee's salary grade band is **recorded and warned,
  // never refused**. That ruling is why these two properties exist and why there is no validation method:
  // promoting a band into a control would change what `DEC-POS-0027` said a band is, and would immediately
  // require an override path, an override permission and an override audit — three things nobody asked for.
  //
  // The observation is stored rather than recomputed on read, because the band could later change and the
  // honest record is *what was true when the amount was set*. A recomputed warning would silently rewrite
  // history's opinion of a decision.
  public bool WasOutsideGradeBand { get; private set; }

  public string? GradeBandObservation { get; private set; }

  public IReadOnlyCollection<PayElementAssignment> Assignments => assignments.AsReadOnly();

  public DateTimeOffset CreatedUtc { get; set; }

  public string? CreatedBy { get; set; }

  public DateTimeOffset ModifiedUtc { get; set; }

  public string? ModifiedBy { get; set; }

  public static Result<EmployeeCompensation> Create(
    Guid companyId,
    Guid employeeId,
    DateTimeOffset effectiveFromUtc,
    decimal baseAmount,
    IReadOnlyList<(Guid PayElementId, decimal? RateOrAmount)>? assignments = null)
  {
    if (companyId == Guid.Empty)
    {
      return Result.Failure<EmployeeCompensation>(CompensationErrors.CompanyRequired);
    }

    if (employeeId == Guid.Empty)
    {
      return Result.Failure<EmployeeCompensation>(CompensationErrors.EmployeeRequired);
    }

    if (baseAmount < 0m)
    {
      return Result.Failure<EmployeeCompensation>(CompensationErrors.NegativeBaseAmount);
    }

    var compensation = new EmployeeCompensation(
      Guid.NewGuid(), companyId, employeeId, effectiveFromUtc.ToUniversalTime(), baseAmount);

    foreach (var (payElementId, rateOrAmount) in assignments ?? [])
    {
      if (payElementId == Guid.Empty)
      {
        return Result.Failure<EmployeeCompensation>(CompensationErrors.AssignmentElementRequired);
      }

      if (rateOrAmount is < 0m)
      {
        return Result.Failure<EmployeeCompensation>(CompensationErrors.NegativeAssignmentAmount);
      }

      // A duplicate assignment of the same element would double-count it in every run, silently.
      if (compensation.assignments.Any(a => a.PayElementId == payElementId))
      {
        return Result.Failure<EmployeeCompensation>(CompensationErrors.DuplicateAssignment);
      }

      compensation.assignments.Add(new PayElementAssignment(Guid.NewGuid(), compensation.Id, payElementId, rateOrAmount));
    }

    return Result.Success(compensation);
  }

  // Recorded, never enforced — see the band observation note above. Called by the handler after it has read
  // the employee's grade band, which is HR data Payroll receives rather than reaches for.
  public void RecordGradeBandObservation(bool wasOutside, string? observation)
  {
    WasOutsideGradeBand = wasOutside;
    GradeBandObservation = observation;
  }

  // ---- DERIVATION, IN ONE PLACE (REQ-PAY-0001, AC-PAY-0002).
  //
  // The value in force on a date is the record with the greatest `EffectiveFromUtc` **not after** it. A date
  // before the first record resolves to NOTHING rather than to the earliest record — an employee had no
  // compensation before one was recorded, and answering with the earliest would invent a fact.
  //
  // It lives on the type rather than in a repository so that every caller derives it the same way. A second
  // implementation of this rule is a second answer to "what were they paid", and the two would drift.
  public static EmployeeCompensation? InForceOn(
    IEnumerable<EmployeeCompensation> history,
    DateTimeOffset onUtc)
  {
    ArgumentNullException.ThrowIfNull(history);

    var target = onUtc.ToUniversalTime();

    return history
      .Where(record => record.EffectiveFromUtc <= target)
      .OrderByDescending(record => record.EffectiveFromUtc)
      // Two records effective at the same instant is a data defect rather than a business case, but the
      // ordering must still be total or the answer would depend on row order. Id breaks the tie.
      .ThenByDescending(record => record.Id)
      .FirstOrDefault();
  }
}

// A STANDING INSTRUCTION that an employee receives an element — a recurring allowance or deduction.
//
// A child of the dated compensation record rather than a free-standing thing, so that changing an allowance
// is a NEW dated compensation record rather than a mutation. That keeps `BR-PAY-0002` true for the whole
// compensation picture instead of only for base pay.
//
// `RateOrAmount` is nullable: null means "use the element's default". Storing a copy of the default instead
// would freeze it, and a later change to the element would then silently not apply to anyone.
//
// `ITenantOwnedEntity` is **required, not optional**. FP-011 shipped `FiscalPeriod` and `JournalDraftLine`
// without it and both would have been silently absent from tenant cutover: `TenantCutoverCopyPlan.Build`
// derives its manifest by REFLECTING over this interface. Being an owned child is a domain fact; being
// copied is a reflection fact, and only the interface expresses the second.
public sealed class PayElementAssignment : Entity<Guid>, ITenantOwnedEntity
{
  internal PayElementAssignment(Guid id, Guid employeeCompensationId, Guid payElementId, decimal? rateOrAmount)
    : base(id)
  {
    EmployeeCompensationId = employeeCompensationId;
    PayElementId = payElementId;
    RateOrAmount = rateOrAmount;
  }

  // EF materialization only.
  private PayElementAssignment(Guid id)
    : base(id)
  {
  }

  public Guid TenantId { get; set; }

  public Guid EmployeeCompensationId { get; private set; }

  public Guid PayElementId { get; private set; }

  public decimal? RateOrAmount { get; private set; }
}
