using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.SharedKernel;

namespace SSAS.Payroll.Domain.Elements;

// WHAT A PAY ELEMENT IS (REQ-PAY-0003, REQ-PAY-0004, REQ-PAY-0005, OD-PAY-0006).
//
// `OD-PAY-0006` ruled option 3: a tenant DEFINES elements, the product IMPLEMENTS what each one does. So a
// tenant may create "Housing Allowance" and bind it to a behaviour, but it cannot author a calculation. That
// is what keeps the calculation model finite and `PayrollCalculator` a closed piece of code rather than an
// interpreter for user-supplied rules.
//
// ---- WHETHER IT EARNS OR DEDUCTS IS A PROPERTY OF THE ELEMENT, NOT OF THE SIGN OF AN AMOUNT.
//
// Every amount in this module is stored POSITIVE. A deduction is not a negative earning; it is an amount
// with a different `Kind`, mapped to a different side of the ledger. Encoding the distinction as a sign
// would make "is this element a deduction" a question about data rather than about definition, and a single
// mis-signed amount would silently turn a deduction into a pay rise.
public enum PayElementKind
{
  Earning = 0,
  Deduction = 1
}

// ---- THE CLOSED SET OF BEHAVIOURS.
//
// This enum is the boundary `OD-PAY-0006` drew. A tenant chooses one; nobody adds one without shipping code
// to implement it, which is precisely the point.
//
// **`DEC-PAY-0002` is why the set stopped where it did, and TWO OF ITS THREE NAMED ABSENCES HAVE SINCE
// ARRIVED.** It read: *"There is no `PerHour`, no `PerDayAbsent` and no `OvertimeMultiple`, because
// Attendance is unbuilt and none of them has an input."* Attendance shipped (FP-013), and with it:
//
//   PerHour        -> `OvertimeHourly` (5), quantity x rate
//   PerDayAbsent   -> `UnpaidAbsenceDeduction` (6), a derived daily rate
//   OvertimeMultiple  -- STILL ABSENT. A tier carries an AMOUNT, not a multiple of base.
//
// **`DEC-PAY-0002`'s reasoning was right and is what let them in**: each was refused for want of an INPUT,
// and each arrived the moment its input did. Amended in T-107 (`DEC-L-073`) because the sentence read as
// though all three were still absent while two were declared sixty lines below it.
//
// **T-108 completed the pattern.** `SalaryType.Daily` needed a count of working days, which existed in
// `WorkingCalendar` and did not cross the module boundary; T-108 put it on `AttendanceSummaryResult` and
// daily pay followed immediately. **Three refusals, three inputs, and each behaviour arrived the moment its
// input did** — which is `DEC-PAY-0002` working exactly as written rather than being overtaken.
//
// **`DEC-PAY-0016` is why there is no `StatutoryBracket`.** V1 is jurisdiction-neutral. A tenant can express
// a fixed or proportional deduction; it cannot have the product apply a tax table, because no jurisdiction is
// authored anywhere in the specification.
public enum PayElementBehaviour
{
  // A flat amount taken from the employee's assignment of this element.
  FixedAmount = 0,

  // ---- THE EMPLOYEE'S BASE SALARY, AND WHY IT IS AN ELEMENT AT ALL.
  //
  // Base pay draws its amount from `EmployeeCompensation.BaseAmount` rather than from an assignment, so it
  // needs no per-employee configuration. It is nevertheless a PAY ELEMENT and not a special case in the
  // calculator, for a reason that is easy to get wrong: **`REQ-PAY-0005` requires every line to map to a
  // ledger account, and only an element carries a mapping.** A base-salary line invented outside the element
  // model would have no `GlAccountId`, could never post, and would fail `BR-PAY-0005` at approval — or worse,
  // slip through and produce an unbalanced journal.
  //
  // So a company defines exactly one element with this behaviour, maps it to its salary-expense account, and
  // the calculator treats it like any other line. Uniformity here is what keeps posting total.
  BaseSalary = 3,

  // A percentage of the employee's base salary. Independent of every other element, so its position in the
  // evaluation order does not change its result.
  PercentageOfBaseSalary = 1,

  // A percentage of gross earnings ACCUMULATED SO FAR — that is, of the earning lines already evaluated.
  // This is the behaviour that makes `REQ-PAY-0004`'s explicit ordering load-bearing rather than decorative:
  // move this element earlier and its result legitimately changes, which is why the order is stored on the
  // element and the order actually used is stored on the line.
  PercentageOfGrossToDate = 2,

  // ---- THE BALANCING CREDIT, AND WHY IT IS AN ELEMENT TOO.
  //
  // A payroll journal debits each earning's account and credits each deduction's account. Those two do not
  // balance: the difference is NET PAY, which must credit a liability account — what the company now owes
  // its employees. Without somewhere to put it, `BR-GL-0001` refuses every payroll posting.
  //
  // No `OD-PAY` names that account, and rather than invent a new mechanism this applies the one already
  // established: **only an element carries a GL mapping**, which is the same reason `BaseSalary` is an
  // element rather than a special case in the calculator. A company defines exactly one element with this
  // behaviour and maps it to its net-pay payable account.
  //
  // **The calculator produces NO LINE for it.** Net pay is derived from the other lines, not computed
  // alongside them — a line would double-count. This behaviour exists ONLY to carry the mapping, and the
  // poster reads it when composing the journal. That asymmetry is stated here because it is the one place in
  // the model where an element is not a line.
  NetPayPayable = 4,

  // ================================================================================================
  // THE TWO ATTENDANCE-DRIVEN BEHAVIOURS (FP-013, REQ-ATT-0022) — DEC-PAY-0002 LIFTED.
  // ================================================================================================
  //
  // `DEC-PAY-0002` refused these **because the input did not exist**, not because they were out of scope:
  //
  //   > Overtime computed from worked hours, absence deductions derived from a register [...] each require a
  //   > source of truth this product does not have. This is not a scoping preference; it is a missing input.
  //
  // FP-013 built the register. `IAttendanceSummary` supplies per-period totals, so these two now have an
  // input and may exist.
  //
  // The ruling was deliberate about WHICH ones: **overtime by tier and unpaid-absence deduction**. Shift
  // differential and lateness are still absent, because `AttendanceRecord` records quantities and tiers and
  // has no concept of either. Same discipline, applied to what FP-013 actually built rather than to what it
  // might have.

  // ---- OVERTIME AT A TIER (OD-ATT-0008).
  //
  // Attendance records the QUANTITY of overtime and the TIER it was worked at; the MULTIPLIER is money and
  // lives here, as this element's `DefaultRateOrAmount`. The element names which tier it prices via
  // `OvertimeTier`, so a company defines one element per tier — NIGHT at one rate, HOLIDAY at another.
  //
  // An element whose tier matches no recorded overtime produces no line, on the same rule as any other zero
  // amount: absent and zero are different facts.
  OvertimeHourly = 5,

  // ---- UNPAID ABSENCE (OD-ATT-0008). **MONTHLY SALARIES ONLY** (T-109).
  //
  // A DEDUCTION whose amount is DERIVED rather than configured: the employee's daily rate multiplied by the
  // unpaid days `IAttendanceSummary` reports.
  //
  // ---- IT APPLIES TO `SalaryType.Monthly` AND TO NOTHING ELSE, AND THE TWO EXCLUSIONS DIFFER.
  //
  // This comment said nothing about salary type from T-107, when the first exclusion was made, until T-109
  // — a silence that read as "applies to everyone" while it did not (`DEC-L-073`).
  //
  //   HOURLY  excluded because an hourly employee is PAID ONLY FOR TIME ATTENDED. The absence never
  //           entered the pay, so there is nothing to take out.
  //   DAILY   excluded because its BASE is `working days - unpaid days`, which prices the absence in the
  //           same unit as the rate — one missed day, one day's rate, exactly. This element would take a
  //           second and cruder bite.
  //
  // **Neither reason implies the other**, and a reader who learned one would guess the other wrong.
  //
  // **It survives for MONTHLY because a monthly amount cannot name a day.** This is the only mechanism that
  // salary has for expressing one, and it is an approximation: `OD-ATT-0015` ruled the calendar-day divisor
  // and T-068 records that it under-deducts a part-timer. **An approximation is right where nothing exact
  // is available and wrong where something exact already ran** — which is the whole of T-109.
  //
  // **The daily rate uses the SAME calendar-day divisor as proration**, and that is a decision rather than a
  // convenience. `OD-ATT-0015` asked whether building a working calendar reopened `OD-PAY-0007`, and the
  // owner ruled **proration UNCHANGED — calendar days**. Deriving this deduction from WORKING days instead
  // would mean a month's absence and a month's proration disagreed about what one day is worth, which is
  // precisely the inconsistency that ruling exists to prevent.
  //
  // It takes no configured rate. A per-day amount an administrator maintained would drift from salary the
  // moment anybody got a raise, and the drift would be invisible.
  UnpaidAbsenceDeduction = 6
}

public sealed class PayElementCode : ValueObject
{
  public const int MaximumLength = 64;

  private PayElementCode(string value)
  {
    Value = value;
    NormalizedValue = value.ToUpperInvariant();
  }

  public string Value { get; }

  public string NormalizedValue { get; }

  public static Result<PayElementCode> Create(string? value)
  {
    if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength || value.Any(char.IsControl))
    {
      return Result.Failure<PayElementCode>(PayElementErrors.InvalidCode);
    }

    return Result.Success(new PayElementCode(value.Trim()));
  }

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return NormalizedValue;
  }
}

public sealed class PayElementName : ValueObject
{
  public const int MaximumLength = 256;

  private PayElementName(string value) => Value = value;

  public string Value { get; }

  public static Result<PayElementName> Create(string? value)
  {
    if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength)
    {
      return Result.Failure<PayElementName>(PayElementErrors.InvalidName);
    }

    return Result.Success(new PayElementName(value.Trim()));
  }

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}

// COMPANY-OWNED (OD-PAY-0005), unlike GL's tenant-wide chart of accounts.
//
// The contrast is deliberate and worth stating because the two look similar. `OD-GL-0003` made the CHART
// tenant-level: one chart shared by every company. `OD-PAY-0005` made compensation and its elements
// COMPANY-owned, because an employee belongs to a company and what a company pays is that company's
// business. So this type carries `ICompanyOwnedEntity` and every save runs the company authorization the
// write boundary applies — which `Account` deliberately does not.
public sealed class PayElement : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  // Cites `OvertimeTierKey.MaximumLength`, which is where the tier vocabulary now lives (T-131). It was
  // duplicated here and in `AttendanceRecord` with a comment on each saying the two must agree — and a
  // fact in two places goes stale in one of them (`DEC-L-080`). Payroll still cannot reference Attendance's
  // domain; the shared kernel is what both may reference.
  public const int OvertimeTierMaximumLength = OvertimeTierKey.MaximumLength;

  private string normalizedCode = string.Empty;
  private string normalizedName = string.Empty;

  private PayElement(
    Guid id,
    Guid companyId,
    PayElementCode code,
    PayElementName name,
    PayElementKind kind,
    PayElementBehaviour behaviour,
    decimal defaultRateOrAmount,
    int calculationOrder)
    : base(id)
  {
    CompanyId = companyId;
    Code = code;
    normalizedCode = code.NormalizedValue;
    Name = name;
    normalizedName = name.Value.ToUpperInvariant();
    Kind = kind;
    Behaviour = behaviour;
    DefaultRateOrAmount = defaultRateOrAmount;
    CalculationOrder = calculationOrder;
    IsActive = true;
  }

  // EF materialization only.
  private PayElement(Guid id)
    : base(id)
  {
    Code = null!;
    Name = null!;
  }

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  // ---- THE CODE IS IMMUTABLE FROM CREATION, following `Account`'s precedent rather than re-deriving it.
  //
  // `REQ-GL-0006` settled the same question for accounts on the stricter reading, and the reasoning carries:
  // a code is a business identifier that pay history was calculated against, so re-coding an element
  // silently re-labels what people were paid. Renaming is a correction; re-coding is not.
  //
  // ---- ⚠ ENFORCED BY THE COMMAND'S SHAPE, NOT BY A CHECK — AND `Account` DOES IT THE OTHER WAY.
  //
  // `UpdatePayElementCommand` has no `Code` parameter, so the rule cannot be broken through the only
  // update path. **`AccountErrors.CodeIsImmutable` IS returned; `PayElementErrors.CodeIsImmutable` was
  // declared, mapped, and returned by nothing** — removed in T-168 as a door with no room behind it.
  //
  // **DO NOT MAKE THE TWO MODULES CONSISTENT.** They enforce one rule two ways, and both are sound:
  // adding a check here would be code that cannot fire, and removing GL's would delete one that can.
  // **The asymmetry is the record; a reader who "fixes" it will not know which side is load-bearing.**
  public PayElementCode Code { get; private set; }

  public PayElementName Name { get; private set; }

  // ---- A SEARCHABLE NORMALIZED SHADOW, WRITTEN UP FRONT RATHER THAN AFTER THE FAILURE.
  //
  // `DEC-POS-0030` records that a value-converted property translates in a PROJECTION but not in a
  // PREDICATE, and that HR shipped a department search that threw for every search term. GL wrote these
  // shadows up front for the same reason. Payroll is the third module to face it, so it is written before
  // the third occurrence rather than after.
  public string NormalizedCode => normalizedCode;

  public string NormalizedName => normalizedName;

  public PayElementKind Kind { get; private set; }

  public PayElementBehaviour Behaviour { get; private set; }

  // The default used when an employee's assignment does not override it. For `FixedAmount` this is money;
  // for the percentage behaviours it is a rate expressed as a percentage (7.5 means 7.5%).
  public decimal DefaultRateOrAmount { get; private set; }

  // `REQ-PAY-0004`. Lower evaluates first. Not unique by constraint: two elements may legitimately share an
  // ordinal when neither depends on the other, and the calculator breaks the tie by code so the result stays
  // deterministic (`TS-PAY-0005`).
  public int CalculationOrder { get; private set; }

  // ---- THE GL MAPPING SLOT (REQ-PAY-0005, OD-PAY-0012).
  //
  // Nullable, because an element can exist before anyone has decided where it posts. What is NOT permitted
  // is APPROVING a run that contains an unmapped element — `OD-PAY-0012` put the check at approval so the
  // failure surfaces before anyone treats the run as final, rather than at posting where the run would be
  // stranded in a state with no exit.
  //
  // It is a bare `Guid`, not a reference: the account lives in GL, and Payroll holds only an identifier
  // (`ADR-012`). There is no navigation property and no database foreign key to GL's tables.
  public Guid? GlAccountId { get; private set; }

  // ---- WHICH OVERTIME TIER THIS ELEMENT PRICES (FP-013, OD-ATT-0008).
  //
  // Meaningful only for `OvertimeHourly`, and null for every other behaviour. Attendance records overtime
  // with a TIER LABEL and no rate; this is the Payroll side of that split, so a company defines one element
  // per tier and gives each its own `DefaultRateOrAmount`.
  //
  // A bare string rather than an enum, because the tier vocabulary is a company's business — the same reason
  // `AttendanceSummaryResult` carries a dictionary rather than fixed fields. Freezing it into an enum here
  // would put a jurisdictional list into the calculation model.
  public string? OvertimeTier { get; private set; }

  public bool IsActive { get; private set; }

  public DateTimeOffset CreatedUtc { get; set; }

  public string? CreatedBy { get; set; }

  public DateTimeOffset ModifiedUtc { get; set; }

  public string? ModifiedBy { get; set; }

  public static Result<PayElement> Create(
    Guid companyId,
    string? code,
    string? name,
    PayElementKind kind,
    PayElementBehaviour behaviour,
    decimal defaultRateOrAmount,
    int calculationOrder)
  {
    if (companyId == Guid.Empty)
    {
      return Result.Failure<PayElement>(PayElementErrors.CompanyRequired);
    }

    var elementCode = PayElementCode.Create(code);
    if (elementCode.IsFailure)
    {
      return Result.Failure<PayElement>(elementCode.Error);
    }

    var elementName = PayElementName.Create(name);
    if (elementName.IsFailure)
    {
      return Result.Failure<PayElement>(elementName.Error);
    }

    // Negative is refused rather than normalized. A negative "amount" on an element whose `Kind` already
    // says whether it earns or deducts is not a smaller number — it is a caller who has misunderstood the
    // model, and silently flipping it would hide that.
    if (defaultRateOrAmount < 0m)
    {
      return Result.Failure<PayElement>(PayElementErrors.NegativeAmount);
    }

    if (calculationOrder < 0)
    {
      return Result.Failure<PayElement>(PayElementErrors.InvalidCalculationOrder);
    }

    return Result.Success(new PayElement(
      Guid.NewGuid(), companyId, elementCode.Value, elementName.Value, kind, behaviour,
      defaultRateOrAmount, calculationOrder));
  }


  // ---- SETTING THE TIER, ON THE `MapToAccount` PRECEDENT RATHER THAN AS A `Create` PARAMETER.
  //
  // `GlAccountId` is not a constructor argument either: an element can exist before anyone has decided where
  // it posts, and the same is true of which tier it prices. Following that shape keeps `Create`'s signature
  // stable, which matters because every existing call site and test would otherwise have to change to say
  // "no tier" — and a parameter that is null almost everywhere is a parameter people stop reading.
  public Result SetOvertimeTier(string? overtimeTier)
  {
    // A tier on any other behaviour is a caller who has misunderstood the model, not a harmless extra: it
    // would sit in the database looking like configuration while the calculator never consults it.
    if (Behaviour != PayElementBehaviour.OvertimeHourly && !string.IsNullOrWhiteSpace(overtimeTier))
    {
      return Result.Failure(PayElementErrors.OvertimeTierNotApplicable);
    }

    if (overtimeTier is not null &&
      (overtimeTier.Length > OvertimeTierMaximumLength || overtimeTier.Any(char.IsControl)))
    {
      return Result.Failure(PayElementErrors.OvertimeTierInvalid);
    }

    OvertimeTier = OvertimeTierKey.Normalize(overtimeTier);
    return Result.Success();
  }
  public Result Update(string? name, decimal defaultRateOrAmount, int calculationOrder)
  {
    var elementName = PayElementName.Create(name);
    if (elementName.IsFailure)
    {
      return Result.Failure(elementName.Error);
    }

    if (defaultRateOrAmount < 0m)
    {
      return Result.Failure(PayElementErrors.NegativeAmount);
    }

    if (calculationOrder < 0)
    {
      return Result.Failure(PayElementErrors.InvalidCalculationOrder);
    }

    Name = elementName.Value;
    normalizedName = elementName.Value.Value.ToUpperInvariant();
    DefaultRateOrAmount = defaultRateOrAmount;
    CalculationOrder = calculationOrder;
    return Result.Success();
  }

  // `Kind` and `Behaviour` are absent from `Update` on purpose. Changing either would redefine what past
  // runs computed while leaving their stored lines unchanged, so the record and its explanation would
  // disagree. An element whose behaviour was wrong is deactivated and replaced, which leaves history intact.
  public Result MapToAccount(Guid glAccountId)
  {
    if (glAccountId == Guid.Empty)
    {
      return Result.Failure(PayElementErrors.AccountRequired);
    }

    GlAccountId = glAccountId;
    return Result.Success();
  }

  // Idempotent, following `Account.Deactivate`: deactivating an inactive element is the state the caller
  // asked for, not an error.
  public void Deactivate() => IsActive = false;

  public void Activate() => IsActive = true;
}
