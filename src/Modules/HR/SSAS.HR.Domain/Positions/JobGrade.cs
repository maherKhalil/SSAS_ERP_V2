using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Events;

namespace SSAS.HR.Domain.Positions;

// THE JOB EVALUATION LADDER (REQ-HR-0201, DEC-POS-0005).
//
// `OD-POS-002` ruled THREE aggregates, one per requirement line: a job grade classifies the WORK — its
// level and scope — and a salary grade classifies the PAY. Organizations that run job evaluation separately
// from pay benchmarking keep them apart, and this is the model that lets them.
//
// ================================================================================================
// THE REFERENCE RUNS ONE WAY: JobGrade -> SalaryGrade, AND NEVER BACK.
// ================================================================================================
//
// A `SalaryGrade -> JobGrade` foreign key would reintroduce the cycle `DEC-POS-0002` exists to prevent, in a
// place nobody would think to look for it — the two grades are peers in every other respect, so the
// asymmetry reads as arbitrary until the copy-order consequence is named. `AC-POS-0017` and `TS-POS-0019`
// assert the absence against the composed model.
//
// Tenant + Company owned and not branch owned, for the reason recorded on `Position`.
public sealed class JobGrade
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  public const int ActorMaximumLength = 256;

  private string normalizedCode = string.Empty;

  private JobGrade(
    Guid jobGradeId,
    JobGradeCode code,
    JobGradeName name,
    int rankOrder,
    Guid? salaryGradeId,
    string actor,
    DateTimeOffset occurredUtc) : base(jobGradeId)
  {
    Code = code;
    normalizedCode = code.NormalizedValue;
    Name = name;
    RankOrder = rankOrder;
    SalaryGradeId = salaryGradeId;
    Status = JobGradeStatus.Active;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor;
  }

  private JobGrade()
    : base(Guid.Empty)
  {
    Code = null!;
    Name = null!;
    StatusChangedBy = string.Empty;
  }

  public Guid JobGradeId => Id;

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public JobGradeCode Code { get; private set; }

  public string NormalizedCode => normalizedCode;

  public JobGradeName Name { get; private set; }

  // ---- THE LADDER'S ORDER IS DATA, NOT A READING OF THE CODE (DEC-POS-0006).
  //
  // Deriving order from a code string is derived state that can drift — the trade this codebase refused in
  // `DEC-DEP-0005` and again in `DEC-DEP-0029` — and it is wrong on its own terms: "G10" sorts BEFORE "G9"
  // under every collation the product uses. `TS-POS-0016` makes that trap executable.
  //
  // SPARSE VALUES ARE THE INTENT (10, 20, 30), so a grade can be inserted between two others without
  // renumbering a live ladder. Nothing enforces sparseness — consecutive values are legal and merely
  // inconvenient — because a rule requiring gaps would be a rule no requirement asks for.
  //
  // ---- NO CURRENT REQUIREMENT COMPARES TWO GRADES.
  //
  // Nothing promotes, nothing ranks, nothing asks "is this grade higher". The field is carried anyway
  // because adding order later to a POPULATED ladder means inventing it retroactively for rows whose
  // intended sequence nobody wrote down — the same not-free deferral `DEC-DEP-0016` described, and the
  // reason that one was reversed.
  public int RankOrder { get; private set; }

  // NULL MEANS UNPRICED — this grade has not been mapped to a pay band yet.
  public Guid? SalaryGradeId { get; private set; }

  public JobGradeStatus Status { get; private set; }

  public DateTimeOffset StatusChangedUtc { get; private set; }

  public string StatusChangedBy { get; private set; }

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public static Result<JobGrade> Create(
    JobGradeCode code,
    JobGradeName name,
    int rankOrder,
    Guid? salaryGradeId,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (code is null)
    {
      return Result.Failure<JobGrade>(PositionErrors.InvalidJobGradeCode);
    }

    if (name is null)
    {
      return Result.Failure<JobGrade>(PositionErrors.InvalidJobGradeName);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure<JobGrade>(PositionErrors.InvalidActor);
    }

    // POSITIVE, per `BRULE-POS-0007`. Zero and negatives are refused in the domain and nowhere else: the
    // package's constraint list for this table does not include a rank check, and adding an unlisted
    // constraint would be filling a gap the specification did not leave.
    if (rankOrder <= 0)
    {
      return Result.Failure<JobGrade>(PositionErrors.InvalidRankOrder);
    }

    if (salaryGradeId == Guid.Empty)
    {
      return Result.Failure<JobGrade>(PositionErrors.InvalidGradeReference);
    }

    return Result.Success(new JobGrade(
      Guid.NewGuid(), code, name, rankOrder, salaryGradeId, actor.Trim(), occurredUtc));
  }

  public Result StampCreated(Guid tenantId, Guid companyId, Guid eventId, DateTimeOffset occurredUtc)
  {
    RaiseDomainEvent(new JobGradeCreated(
      eventId, occurredUtc, Id, tenantId, companyId, RankOrder, SalaryGradeId, JobGradeStatus.Active));

    return Result.Success();
  }

  public Result UpdateDescription(
    JobGradeCode code,
    JobGradeName name,
    int rankOrder,
    Guid? salaryGradeId,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (code is null)
    {
      return Result.Failure(PositionErrors.InvalidJobGradeCode);
    }

    if (name is null)
    {
      return Result.Failure(PositionErrors.InvalidJobGradeName);
    }

    if (rankOrder <= 0)
    {
      return Result.Failure(PositionErrors.InvalidRankOrder);
    }

    if (salaryGradeId == Guid.Empty)
    {
      return Result.Failure(PositionErrors.InvalidGradeReference);
    }

    var previousSalaryGradeId = SalaryGradeId;

    Code = code;
    normalizedCode = code.NormalizedValue;
    Name = name;
    RankOrder = rankOrder;
    SalaryGradeId = salaryGradeId;

    RaiseDomainEvent(new JobGradeUpdated(
      eventId, occurredUtc, Id, TenantId, CompanyId, rankOrder, previousSalaryGradeId, salaryGradeId));

    return Result.Success();
  }

  // ---- LIFECYCLE.
  //
  // THE ASYMMETRY WITH `Position` IS DELIBERATE AND IS WORTH READING (DEC-POS-0013). A position with
  // incumbents MAY be deactivated; a grade with active dependents MAY NOT. An employee's assignment is a
  // fact about a person that survives the position's retirement; a grade reference is a structural pointer,
  // and deactivating its target would leave an `Active` position aimed at an `Inactive` grade — an
  // incoherent tree, refused for the same reason `DEC-DEP-0006` step 3 refuses an active child under an
  // inactive parent.
  //
  // The dependent check is a repository lookup and belongs to Phase 2's orchestration; this method does not
  // perform it, and `PositionErrors.GradeHasActiveDependents` is the error that will carry the refusal.
  // Stated here so the absence reads as sequencing rather than as an oversight.
  public Result Deactivate(string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != JobGradeStatus.Active)
    {
      return Result.Failure(PositionErrors.InvalidTransition);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure(PositionErrors.InvalidActor);
    }

    Status = JobGradeStatus.Inactive;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor.Trim();

    RaiseDomainEvent(new JobGradeDeactivated(
      eventId, occurredUtc, Id, TenantId, CompanyId, JobGradeStatus.Active, JobGradeStatus.Inactive));

    return Result.Success();
  }

  public Result Reactivate(string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != JobGradeStatus.Inactive)
    {
      return Result.Failure(PositionErrors.InvalidTransition);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure(PositionErrors.InvalidActor);
    }

    Status = JobGradeStatus.Active;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor.Trim();

    RaiseDomainEvent(new JobGradeReactivated(
      eventId, occurredUtc, Id, TenantId, CompanyId, JobGradeStatus.Inactive, JobGradeStatus.Active));

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
