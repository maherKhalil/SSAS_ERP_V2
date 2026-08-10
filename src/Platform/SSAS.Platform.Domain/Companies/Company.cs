using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Events;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.Companies;

public sealed class Company : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity
{
  public const int ActorMaximumLength = 256;

  private string normalizedCompanyCode = string.Empty;

  private Company(
    Guid companyId,
    Guid tenantId,
    CompanyCode companyCode,
    CompanyName companyName,
    BaseCurrencyCode baseCurrencyCode,
    string actor,
    DateTimeOffset occurredUtc) : base(companyId)
  {
    TenantId = tenantId;
    CompanyCode = companyCode;
    normalizedCompanyCode = companyCode.NormalizedValue;
    CompanyName = companyName;
    BaseCurrencyCode = baseCurrencyCode;
    Status = CompanyStatus.Inactive;
    StatusChangeReasonCode = CompanyStatusChangeReason.Created;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor;
  }

  private Company()
    : base(Guid.Empty)
  {
    CompanyCode = null!;
    CompanyName = null!;
    BaseCurrencyCode = null!;
  }

  public Guid CompanyId => Id;

  public Guid TenantId { get; private set; }

  public CompanyCode CompanyCode { get; private set; }

  public string NormalizedCompanyCode => normalizedCompanyCode;

  public CompanyName CompanyName { get; private set; }

  public BaseCurrencyCode BaseCurrencyCode { get; private set; }

  public CompanyStatus Status { get; private set; }

  public CompanyStatusChangeReason StatusChangeReasonCode { get; private set; }

  public DateTimeOffset StatusChangedUtc { get; private set; }

  public string StatusChangedBy { get; private set; } = string.Empty;

  // CreatedUtc/CreatedBy/ModifiedUtc/ModifiedBy are owned by the IAuditableEntity persistence
  // infrastructure and are never stamped by the Domain.
  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public static Result<Company> Create(
    Guid tenantId,
    CompanyCode companyCode,
    CompanyName companyName,
    BaseCurrencyCode baseCurrencyCode,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (companyCode is null)
    {
      return Result.Failure<Company>(CompanyErrors.InvalidCompanyCode);
    }

    if (companyName is null)
    {
      return Result.Failure<Company>(CompanyErrors.InvalidCompanyName);
    }

    if (baseCurrencyCode is null)
    {
      return Result.Failure<Company>(CompanyErrors.InvalidBaseCurrency);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure<Company>(CompanyErrors.InvalidActor);
    }

    var companyId = Guid.NewGuid();
    var company = new Company(companyId, tenantId, companyCode, companyName, baseCurrencyCode, actor, occurredUtc);
    company.RaiseDomainEvent(new CompanyCreated(
      eventId,
      occurredUtc,
      companyId,
      tenantId,
      CompanyStatus.Inactive,
      CompanyStatusChangeReason.Created));
    return Result.Success(company);
  }

  public Result Activate(CompanyStatusChangeReason reason, string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != CompanyStatus.Inactive)
    {
      return Result.Failure(CompanyErrors.InvalidTransition);
    }

    return Transition(
      CompanyStatus.Active,
      reason,
      actor,
      eventId,
      occurredUtc,
      static (id, time, companyId, tenantId, previous, next, transitionReason) =>
        new CompanyActivated(id, time, companyId, tenantId, previous, next, transitionReason));
  }

  public Result Deactivate(CompanyStatusChangeReason reason, string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != CompanyStatus.Active)
    {
      return Result.Failure(CompanyErrors.InvalidTransition);
    }

    return Transition(
      CompanyStatus.Inactive,
      reason,
      actor,
      eventId,
      occurredUtc,
      static (id, time, companyId, tenantId, previous, next, transitionReason) =>
        new CompanyDeactivated(id, time, companyId, tenantId, previous, next, transitionReason));
  }

  public Result Archive(CompanyStatusChangeReason reason, string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status is not (CompanyStatus.Active or CompanyStatus.Inactive))
    {
      return Result.Failure(CompanyErrors.InvalidTransition);
    }

    return Transition(
      CompanyStatus.Archived,
      reason,
      actor,
      eventId,
      occurredUtc,
      static (id, time, companyId, tenantId, previous, next, transitionReason) =>
        new CompanyArchived(id, time, companyId, tenantId, previous, next, transitionReason));
  }

  public Result UpdateProfile(CompanyName companyName, string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (companyName is null)
    {
      return Result.Failure(CompanyErrors.InvalidCompanyName);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure(CompanyErrors.InvalidActor);
    }

    if (Status == CompanyStatus.Archived)
    {
      return Result.Failure(CompanyErrors.InvalidTransition);
    }

    CompanyName = companyName;
    RaiseDomainEvent(new CompanyProfileUpdated(eventId, occurredUtc.ToUniversalTime(), CompanyId, TenantId));
    return Result.Success();
  }

  private Result Transition(
    CompanyStatus nextStatus,
    CompanyStatusChangeReason reason,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc,
    Func<Guid, DateTimeOffset, Guid, Guid, CompanyStatus, CompanyStatus, CompanyStatusChangeReason, DomainEvent> createEvent)
  {
    if (!Enum.IsDefined(reason) || reason == CompanyStatusChangeReason.Created)
    {
      return Result.Failure(CompanyErrors.InvalidTransitionReason);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure(CompanyErrors.InvalidActor);
    }

    var utc = occurredUtc.ToUniversalTime();
    var previousStatus = Status;
    Status = nextStatus;
    StatusChangeReasonCode = reason;
    StatusChangedUtc = utc;
    StatusChangedBy = actor;
    RaiseDomainEvent(createEvent(eventId, utc, CompanyId, TenantId, previousStatus, nextStatus, reason));
    return Result.Success();
  }

  private static bool IsValidActor(string? actor) =>
    !string.IsNullOrWhiteSpace(actor) && actor.Length <= ActorMaximumLength;

  Guid ITenantOwnedEntity.TenantId
  {
    get => TenantId;
    set => TenantId = value;
  }

  DateTimeOffset IAuditableEntity.CreatedUtc
  {
    get => CreatedUtc;
    set => CreatedUtc = value;
  }

  DateTimeOffset IAuditableEntity.ModifiedUtc
  {
    get => ModifiedUtc;
    set => ModifiedUtc = value;
  }

  string? IAuditableEntity.CreatedBy
  {
    get => CreatedBy;
    set => CreatedBy = value;
  }

  string? IAuditableEntity.ModifiedBy
  {
    get => ModifiedBy;
    set => ModifiedBy = value;
  }
}
