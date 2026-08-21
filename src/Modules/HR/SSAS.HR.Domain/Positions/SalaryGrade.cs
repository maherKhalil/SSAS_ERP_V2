using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Events;

namespace SSAS.HR.Domain.Positions;

// THE PAY BANDING LADDER — AND THE PRODUCT'S FIRST ENTITY THAT CARRIES MONEY (REQ-HR-0202, ADR-027).
//
// ================================================================================================
// THE BANDS ARE INFORMATIONAL. THEY CONSTRAIN NOTHING (OD-POS-004, DEC-POS-0023).
// ================================================================================================
//
// FP-008 stores no employee compensation, so there is nothing in the product for a range to validate. The
// "validation" reading of `OD-POS-004` was recorded as UNAVAILABLE rather than rejected: range enforcement
// transfers to Payroll as a named obligation, and claiming it here would mark a constraint as enforced when
// no write can violate it — the failure `ADR-026` decision 10 names.
//
// ================================================================================================
// THERE IS NO CURRENCY COLUMN (DEC-POS-0015, ADR-027 decision 2).
// ================================================================================================
//
// Amounts are denominated in the owning Company's `BaseCurrencyCode`. Three facts make that sufficient
// rather than sloppy: `DEC-CMP-0009` makes a company's base currency required at creation and IMMUTABLE, so
// every row under one company has exactly one unambiguous currency; a per-row copy would be a second source
// of truth for a fact the Company already owns; and `SSAS.HR.Domain` cannot reference
// `SSAS.Platform.Domain.ValueObjects.BaseCurrencyCode` at all under `ADR-012` — the compiler enforces it,
// the same way it stopped `DepartmentApiErrorMapper` reaching for Platform's error type in FP-007 Phase 4.
//
// The currency is PROJECTED ON READ and rejected on write. `ADR-027` decision 3 names the conditions under
// which this stops being sufficient, so the reason for a missing column does not decay into folklore.
//
// Tenant + Company owned and not branch owned, for the reason recorded on `Position`.
public sealed class SalaryGrade
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  public const int ActorMaximumLength = 256;

  private string normalizedCode = string.Empty;

  // The search form of the label, maintained beside the code's (`DEC-POS-0030`). Set in exactly the two
  // places `normalizedCode` is set, so the two can never disagree about which write produced them.
  private string normalizedName = string.Empty;

  private SalaryGrade(
    Guid salaryGradeId,
    SalaryGradeCode code,
    SalaryGradeName name,
    int rankOrder,
    SalaryBand? band,
    string actor,
    DateTimeOffset occurredUtc) : base(salaryGradeId)
  {
    Code = code;
    normalizedCode = code.NormalizedValue;
    normalizedName = name.NormalizedValue;
    Name = name;
    RankOrder = rankOrder;
    Band = band;
    Status = SalaryGradeStatus.Active;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor;
  }

  private SalaryGrade()
    : base(Guid.Empty)
  {
    Code = null!;
    Name = null!;
    StatusChangedBy = string.Empty;
  }

  public Guid SalaryGradeId => Id;

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public SalaryGradeCode Code { get; private set; }

  public string NormalizedCode => normalizedCode;

  public string NormalizedName => normalizedName;

  public SalaryGradeName Name { get; private set; }

  // Authoritative ladder order. See `JobGrade.RankOrder` for the full reasoning; it is the same field on the
  // same terms, on the other ladder.
  public int RankOrder { get; private set; }

  // ---- THE BAND IS ONE VALUE, PRESENT OR ABSENT (DEC-POS-0027).
  //
  // NULL means UNPRICED — the grade exists on the ladder but has not been benchmarked. It does NOT mean
  // "zero", and it is not three independent nullable amounts: `SalaryBand` refuses any partial combination,
  // the mapping stores it as an optional owned type so the three columns are null together or populated
  // together, and `CK_SalaryGrades_Band_Atomic` states the same rule to SQL Server for writes that bypass
  // the application.
  //
  // The nullability's ORIGINAL justification — that `OD-POS-001`'s seeded grade would otherwise have to
  // invent money — died with that ruling, which seeds nothing. What remains is define-before-price, and
  // `DEC-POS-0016` records it as an open question rather than pretending the stronger reason survives.
  public SalaryBand? Band { get; private set; }

  public SalaryGradeStatus Status { get; private set; }

  public DateTimeOffset StatusChangedUtc { get; private set; }

  public string StatusChangedBy { get; private set; }

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  // The band arrives already constructed, so an invalid one cannot reach this method: `SalaryBand.Create`
  // is the only way to build one and it refuses partial, negative and out-of-order combinations. A `null`
  // here is the legal unpriced case, not a validation failure that slipped through.
  public static Result<SalaryGrade> Create(
    SalaryGradeCode code,
    SalaryGradeName name,
    int rankOrder,
    SalaryBand? band,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (code is null)
    {
      return Result.Failure<SalaryGrade>(PositionErrors.InvalidSalaryGradeCode);
    }

    if (name is null)
    {
      return Result.Failure<SalaryGrade>(PositionErrors.InvalidSalaryGradeName);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure<SalaryGrade>(PositionErrors.InvalidActor);
    }

    if (rankOrder <= 0)
    {
      return Result.Failure<SalaryGrade>(PositionErrors.InvalidRankOrder);
    }

    return Result.Success(new SalaryGrade(
      Guid.NewGuid(), code, name, rankOrder, band, actor.Trim(), occurredUtc));
  }

  public Result StampCreated(Guid tenantId, Guid companyId, Guid eventId, DateTimeOffset occurredUtc)
  {
    RaiseDomainEvent(new SalaryGradeCreated(
      eventId, occurredUtc, Id, tenantId, companyId, RankOrder, Band is not null,
      SalaryGradeStatus.Active));

    return Result.Success();
  }

  public Result UpdateDescription(
    SalaryGradeCode code,
    SalaryGradeName name,
    int rankOrder,
    SalaryBand? band,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (code is null)
    {
      return Result.Failure(PositionErrors.InvalidSalaryGradeCode);
    }

    if (name is null)
    {
      return Result.Failure(PositionErrors.InvalidSalaryGradeName);
    }

    if (rankOrder <= 0)
    {
      return Result.Failure(PositionErrors.InvalidRankOrder);
    }

    Code = code;
    normalizedCode = code.NormalizedValue;
    normalizedName = name.NormalizedValue;
    Name = name;
    RankOrder = rankOrder;

    // A band may be REMOVED as well as set. Un-pricing a grade is a legal correction — the alternative
    // would be that a mistaken band can never be withdrawn, only overwritten with a different guess.
    Band = band;

    RaiseDomainEvent(new SalaryGradeUpdated(
      eventId, occurredUtc, Id, TenantId, CompanyId, rankOrder, band is not null));

    return Result.Success();
  }

  // ---- LIFECYCLE. The dependent check is Phase 2's; see `JobGrade.Deactivate` for the full reasoning.
  public Result Deactivate(string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != SalaryGradeStatus.Active)
    {
      return Result.Failure(PositionErrors.InvalidTransition);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure(PositionErrors.InvalidActor);
    }

    Status = SalaryGradeStatus.Inactive;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor.Trim();

    RaiseDomainEvent(new SalaryGradeDeactivated(
      eventId, occurredUtc, Id, TenantId, CompanyId,
      SalaryGradeStatus.Active, SalaryGradeStatus.Inactive));

    return Result.Success();
  }

  public Result Reactivate(string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != SalaryGradeStatus.Inactive)
    {
      return Result.Failure(PositionErrors.InvalidTransition);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure(PositionErrors.InvalidActor);
    }

    Status = SalaryGradeStatus.Active;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor.Trim();

    RaiseDomainEvent(new SalaryGradeReactivated(
      eventId, occurredUtc, Id, TenantId, CompanyId,
      SalaryGradeStatus.Inactive, SalaryGradeStatus.Active));

    return Result.Success();
  }

  private static bool IsValidActor(string actor) =>
    !string.IsNullOrWhiteSpace(actor) && actor.Trim().Length <= ActorMaximumLength;

  DateTimeOffset IAuditableEntity.CreatedUtc
  {
    get => CreatedUtc;
    set => CreatedUtc = value;
  }

  string? IAuditableEntity.CreatedBy
  {
    get => CreatedBy;
    set => CreatedBy = value;
  }

  DateTimeOffset IAuditableEntity.ModifiedUtc
  {
    get => ModifiedUtc;
    set => ModifiedUtc = value;
  }

  string? IAuditableEntity.ModifiedBy
  {
    get => ModifiedBy;
    set => ModifiedBy = value;
  }
}
