using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.Domain.Leave;

// ================================================================================================
// THE LEAVE TYPE CATALOG (REQ-ATT-0010; OD-ATT-0005) — THE PayElement PRECEDENT, APPLIED EXACTLY.
// ================================================================================================
//
// `OD-PAY-0006` settled this shape for pay elements and `OD-ATT-0005` reused it without amendment:
// **a tenant DEFINES the types, the product IMPLEMENTS what each one does.** So a company may create
// "Compassionate Leave" and bind it to a behaviour, but it cannot author a rule.
//
// The alternative — a fixed enum of leave types in the domain — would freeze a list that is
// **jurisdictional**. Annual, sick, unpaid, maternity, bereavement, study, pilgrimage and compassionate all
// vary by country and by employer, and a product that shipped one of them as code would be wrong everywhere
// it was not written for.
//
// COMPANY-OWNED and NOT branch-owned: a catalog is company policy, following `Department`'s asserted
// classification. `DEC-ATT-0014` requires the negative to be asserted, and an architecture test does.

// ---- THE CLOSED BEHAVIOUR SET, AND WHY IT STOPS HERE.
//
// `DEC-PAY-0002`'s discipline applies without exception: **a behaviour whose input does not exist must not
// be declared.**
//
// So there is no `Accruing`. `OD-ATT-0006` ruled balances ADMINISTERED and deferred accrual rules, which
// means no accrual engine exists to drive such a member. Declaring it would be `PayElementBehaviour`'s
// `OvertimeMultiple` mistake in a fresh costume — an enum value the calculator cannot honour, sitting in
// the model looking implemented.
//
// Adding it later is additive: a new member and its implementation, with no existing type changing shape.
public enum LeaveBehaviour
{
  // Consumes balance; the employee is paid as normal. Annual and sick leave in most schemes.
  PaidFromBalance = 0,

  // ---- THE ONE PAYROLL ACTUALLY SEES.
  //
  // Consumes balance and produces UNPAID days, which reach Payroll through `IAttendanceSummary` as
  // `UnpaidAbsenceQuantity` and drive the deduction behaviour `DEC-PAY-0002` could not build.
  Unpaid = 1,

  // Paid, and does NOT consume a balance — statutory entitlements a company grants without metering, such
  // as bereavement in many jurisdictions. Separate from `PaidFromBalance` because a balance of zero must
  // not refuse it.
  PaidWithoutBalance = 2
}

public sealed class LeaveTypeCode : ValueObject
{
  public const int MaximumLength = 32;

  private LeaveTypeCode(string value)
  {
    Value = value;
    NormalizedValue = value.ToUpperInvariant();
  }

  public string Value { get; }

  public string NormalizedValue { get; }

  public static Result<LeaveTypeCode> Create(string? value)
  {
    if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength || value.Any(char.IsControl))
    {
      return Result.Failure<LeaveTypeCode>(LeaveErrors.InvalidLeaveTypeCode);
    }

    return Result.Success(new LeaveTypeCode(value.Trim()));
  }

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return NormalizedValue;
  }
}

public sealed class LeaveTypeName : ValueObject
{
  public const int MaximumLength = 200;

  private LeaveTypeName(string value) => Value = value;

  public string Value { get; }

  public static Result<LeaveTypeName> Create(string? value)
  {
    if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength || value.Any(char.IsControl))
    {
      return Result.Failure<LeaveTypeName>(LeaveErrors.InvalidLeaveTypeName);
    }

    return Result.Success(new LeaveTypeName(value.Trim()));
  }

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}

public sealed class LeaveType
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  private string normalizedCode = string.Empty;
  private string normalizedName = string.Empty;

  private LeaveType(Guid id, Guid companyId, LeaveTypeCode code, LeaveTypeName name, LeaveBehaviour behaviour, bool isSensitive)
    : base(id)
  {
    CompanyId = companyId;
    Code = code;
    normalizedCode = code.NormalizedValue;
    Name = name;
    normalizedName = name.Value.ToUpperInvariant();
    Behaviour = behaviour;
    IsSensitive = isSensitive;
    IsActive = true;
  }

  // EF materialization only.
  private LeaveType(Guid id)
    : base(id)
  {
    Code = null!;
    Name = null!;
  }

  public Guid LeaveTypeId => Id;

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  // ---- IMMUTABLE FROM CREATION, following `Account` and `PayElement` rather than re-deriving it.
  //
  // A code is a business identifier that settled leave balances were decided against, so re-coding a type
  // silently re-labels leave people have already taken. Renaming is a correction; re-coding is not — which
  // is why the update request has no field for it.
  public LeaveTypeCode Code { get; private set; }

  public LeaveTypeName Name { get; private set; }

  public string NormalizedCode => normalizedCode;

  public string NormalizedName => normalizedName;

  public LeaveBehaviour Behaviour { get; private set; }

  // ---- THE SENSITIVITY FLAG (REQ-ATT-0025, OD-ATT-0013(3)).
  //
  // Seeing "absent 3 days" and seeing "sick leave, 3 days" are different disclosures — the second is health
  // information about an identified person. `Attendance.Leave.ViewSensitive` gates the TYPE; the occurrence
  // stays readable under `Attendance.Leave.View`, because a scheduler needs to know someone is away.
  //
  // A flag per type rather than a hardcoded list, for the same reason the catalog is configurable at all:
  // which types are sensitive is a company's judgement, not the product's.
  public bool IsSensitive { get; private set; }

  // Deactivated, never deleted (`BR-ATT-0009`). Requests referencing a deactivated type stay intact — the
  // `PayElement` and `Account` precedent, and the only treatment that keeps historical leave readable.
  public bool IsActive { get; private set; }

  public DateTimeOffset CreatedUtc { get; set; }

  public string? CreatedBy { get; set; }

  public DateTimeOffset ModifiedUtc { get; set; }

  public string? ModifiedBy { get; set; }

  public byte[] RowVersion { get; private set; } = [];

  public bool ConsumesBalance => Behaviour is LeaveBehaviour.PaidFromBalance or LeaveBehaviour.Unpaid;

  public static Result<LeaveType> Create(
    Guid companyId, string? code, string? name, LeaveBehaviour behaviour, bool isSensitive)
  {
    if (companyId == Guid.Empty)
    {
      return Result.Failure<LeaveType>(LeaveErrors.CompanyRequired);
    }

    if (!Enum.IsDefined(behaviour))
    {
      return Result.Failure<LeaveType>(LeaveErrors.InvalidLeaveBehaviour);
    }

    var typeCode = LeaveTypeCode.Create(code);
    if (typeCode.IsFailure)
    {
      return Result.Failure<LeaveType>(typeCode.Error);
    }

    var typeName = LeaveTypeName.Create(name);
    if (typeName.IsFailure)
    {
      return Result.Failure<LeaveType>(typeName.Error);
    }

    return Result.Success(new LeaveType(Guid.NewGuid(), companyId, typeCode.Value, typeName.Value, behaviour, isSensitive));
  }

  // No `behaviour` parameter, on the same reasoning that omits `code`: changing what a type DOES would
  // redefine what past requests consumed while leaving their stored rows untouched.
  public Result Update(string? name, bool isSensitive)
  {
    var typeName = LeaveTypeName.Create(name);
    if (typeName.IsFailure)
    {
      return Result.Failure(typeName.Error);
    }

    Name = typeName.Value;
    normalizedName = typeName.Value.Value.ToUpperInvariant();
    IsSensitive = isSensitive;
    return Result.Success();
  }

  public Result SetActivation(bool isActive)
  {
    if (IsActive == isActive)
    {
      return Result.Failure(isActive ? LeaveErrors.LeaveTypeAlreadyActive : LeaveErrors.LeaveTypeAlreadyInactive);
    }

    IsActive = isActive;
    return Result.Success();
  }
}
