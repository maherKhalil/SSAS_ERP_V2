using SSAS.BuildingBlocks.Domain;

namespace SSAS.Payroll.Domain.Runs;

// THE PAY PERIOD (REQ-PAY-0007, OD-PAY-0002 — RULED: monthly, company-scoped, ALIGNED).
//
// ================================================================================================
// ONE PAYROLL PERIOD MAPS TO EXACTLY ONE GL FISCAL PERIOD, AND THAT IS THE WHOLE POINT.
// ================================================================================================
//
// `OD-PAY-0002` ruled monthly and company-scoped, and closed the sub-question the package raised by making
// alignment **mandatory**: a payroll period maps to exactly one fiscal period. That single decision is what
// makes `OD-PAY-0014`'s closed-period check an unambiguous lookup instead of a straddle.
//
// Without alignment, a period spanning two fiscal periods would force an answer to "which one is closed?"
// and "which one do we post into?", and every answer is arbitrary. With it, the question cannot arise — the
// preferred way to handle a hard case in this codebase is to make it unrepresentable.
//
// **`FiscalPeriodId` is a bare `Guid`, not a reference.** The fiscal calendar lives in GL and Payroll holds
// only an identifier (`ADR-012`). There is no navigation property and no database foreign key into GL's
// tables — an architecture guard asserts the absence in both directions.
//
// ---- WHY THE PAY DATE IS ITS OWN COLUMN.
//
// `PayDateUtc` is separate from `EndUtc` because the date that determines the fiscal period for posting is
// the PAY date, and the two are routinely in different months — a period ending 31 January paid on
// 5 February is ordinary. Conflating them is a defect that only appears at a month boundary, which is to
// say it appears in production and not in a demo.
public sealed class PayrollPeriod
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  private PayrollPeriod(
    Guid id,
    Guid companyId,
    Guid fiscalPeriodId,
    string name,
    DateTimeOffset startUtc,
    DateTimeOffset endUtc,
    DateTimeOffset payDateUtc)
    : base(id)
  {
    CompanyId = companyId;
    FiscalPeriodId = fiscalPeriodId;
    Name = name;
    StartUtc = startUtc;
    EndUtc = endUtc;
    PayDateUtc = payDateUtc;
  }

  // EF materialization only.
  private PayrollPeriod(Guid id)
    : base(id)
  {
    Name = string.Empty;
  }

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public Guid FiscalPeriodId { get; private set; }

  public string Name { get; private set; }

  public DateTimeOffset StartUtc { get; private set; }

  public DateTimeOffset EndUtc { get; private set; }

  public DateTimeOffset PayDateUtc { get; private set; }

  public byte[]? RowVersion { get; set; }

  public DateTimeOffset CreatedUtc { get; set; }

  public string? CreatedBy { get; set; }

  public DateTimeOffset ModifiedUtc { get; set; }

  public string? ModifiedBy { get; set; }

  // Generated FROM the fiscal calendar rather than authored independently — that is how alignment is
  // guaranteed by construction rather than validated after the fact. The caller supplies the fiscal period's
  // identity and bounds; this type does not get to disagree with them.
  public static Result<PayrollPeriod> CreateAlignedTo(
    Guid companyId,
    Guid fiscalPeriodId,
    string? name,
    DateTimeOffset fiscalPeriodStartUtc,
    DateTimeOffset fiscalPeriodEndUtc,
    DateTimeOffset payDateUtc)
  {
    if (companyId == Guid.Empty)
    {
      return Result.Failure<PayrollPeriod>(PayrollErrors.PeriodCompanyRequired);
    }

    if (fiscalPeriodId == Guid.Empty)
    {
      return Result.Failure<PayrollPeriod>(PayrollErrors.PeriodFiscalPeriodRequired);
    }

    if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
    {
      return Result.Failure<PayrollPeriod>(PayrollErrors.PeriodNameInvalid);
    }

    var start = fiscalPeriodStartUtc.ToUniversalTime();
    var end = fiscalPeriodEndUtc.ToUniversalTime();

    if (end <= start)
    {
      return Result.Failure<PayrollPeriod>(PayrollErrors.PeriodBoundsInvalid);
    }

    // ---- THE PAY DATE MAY FALL OUTSIDE THE PERIOD, BUT NOT BEFORE IT STARTS.
    //
    // Paying after the period ends is normal. Paying before it begins is not a schedule, it is a mistake —
    // there is nothing yet to pay for. Refused rather than warned, because unlike the grade band
    // (`OD-PAY-0004`) there is no legitimate business case on the other side.
    if (payDateUtc.ToUniversalTime() < start)
    {
      return Result.Failure<PayrollPeriod>(PayrollErrors.PayDateBeforePeriod);
    }

    return Result.Success(new PayrollPeriod(
      Guid.NewGuid(), companyId, fiscalPeriodId, name.Trim(), start, end, payDateUtc.ToUniversalTime()));
  }

  // ---- INCLUSION (REQ-PAY-0008, BR-PAY-0003, OD-PAY-0010 — RULED option 1).
  //
  // An employee is included if they were employed for **at least one day** of the period. Terminated
  // employees included; employees terminated before the period begins excluded.
  //
  // **The `BR-HR-0004` reading is recorded on the ruling and restated here** because this method is where it
  // takes effect: *a terminated employee cannot be assigned new business transactions* bars **new
  // obligations**, not the settlement of obligations already incurred. Final pay is a settlement. The literal
  // reading would mean people do not receive their final pay, which is unlawful in most jurisdictions.
  //
  // Deliberately a pure function of dates: no repository, no ambient state, so `TS-PAY-0008` can assert both
  // boundaries without a database.
  public bool Includes(DateTimeOffset hiredUtc, DateTimeOffset? terminatedUtc)
  {
    var hired = hiredUtc.ToUniversalTime();

    // Hired after the period ended — nothing to pay for yet.
    if (hired > EndUtc)
    {
      return false;
    }

    // Terminated strictly before the period began — the employment did not overlap it at all.
    if (terminatedUtc is { } terminated && terminated.ToUniversalTime() < StartUtc)
    {
      return false;
    }

    return true;
  }
}
