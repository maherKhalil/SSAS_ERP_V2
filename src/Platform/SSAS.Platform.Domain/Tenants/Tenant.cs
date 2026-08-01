using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Events;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.Tenants;

public sealed class Tenant : AggregateRoot<Guid>
{
  public const int ActorMaximumLength = 256;

  private string normalizedTenantCode = string.Empty;

  private Tenant(
    Guid tenantId,
    TenantCode tenantCode,
    TenantName tenantName,
    string actor,
    DateTimeOffset occurredUtc) : base(tenantId)
  {
    TenantCode = tenantCode;
    normalizedTenantCode = tenantCode.NormalizedValue;
    TenantName = tenantName;
    Status = TenantStatus.Provisioning;
    CreatedUtc = occurredUtc.ToUniversalTime();
    CreatedBy = actor;
    StatusChangedUtc = CreatedUtc;
    StatusChangedBy = actor;
    StatusChangeReasonCode = TenantStatusChangeReason.Created;
  }

  private Tenant()
    : base(Guid.Empty)
  {
    TenantCode = null!;
    TenantName = null!;
  }

  public Guid TenantId => Id;

  public TenantCode TenantCode { get; private set; }

  public string NormalizedTenantCode => normalizedTenantCode;

  public TenantName TenantName { get; private set; }

  public TenantStatus Status { get; private set; }

  public DateTimeOffset CreatedUtc { get; private set; }

  public string CreatedBy { get; private set; } = string.Empty;

  public DateTimeOffset? ModifiedUtc { get; private set; }

  public string? ModifiedBy { get; private set; }

  public DateTimeOffset StatusChangedUtc { get; private set; }

  public string StatusChangedBy { get; private set; } = string.Empty;

  public TenantStatusChangeReason StatusChangeReasonCode { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public bool IsAuthenticationEligible => Status == TenantStatus.Active;

  public static Result<Tenant> Create(
    TenantCode tenantCode,
    TenantName tenantName,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (tenantCode is null)
    {
      return Result.Failure<Tenant>(TenantLifecycleErrors.InvalidTenantCode);
    }

    if (tenantName is null)
    {
      return Result.Failure<Tenant>(TenantLifecycleErrors.InvalidTenantName);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure<Tenant>(TenantLifecycleErrors.InvalidActor);
    }

    var tenantId = Guid.NewGuid();
    var tenant = new Tenant(tenantId, tenantCode, tenantName, actor, occurredUtc);
    tenant.RaiseDomainEvent(new TenantCreated(
      eventId,
      occurredUtc,
      tenantId,
      TenantStatus.Provisioning,
      TenantStatusChangeReason.Created));
    return Result.Success(tenant);
  }

  public Result Activate(string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != TenantStatus.Provisioning)
    {
      return Result.Failure(TenantLifecycleErrors.InvalidTransition);
    }

    return Transition(
      TenantStatus.Active,
      TenantStatusChangeReason.ProvisioningCompleted,
      actor,
      eventId,
      occurredUtc,
      static (id, time, tenantId, previous, next, reason) =>
        new TenantActivated(id, time, tenantId, previous, next, reason));
  }

  public Result Suspend(
    TenantStatusChangeReason reason,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (Status != TenantStatus.Active)
    {
      return Result.Failure(TenantLifecycleErrors.InvalidTransition);
    }

    if (reason == TenantStatusChangeReason.ProvisioningCompleted)
    {
      return Result.Failure(TenantLifecycleErrors.InvalidTransitionReason);
    }

    return Transition(
      TenantStatus.Suspended,
      reason,
      actor,
      eventId,
      occurredUtc,
      static (id, time, tenantId, previous, next, transitionReason) =>
        new TenantSuspended(id, time, tenantId, previous, next, transitionReason));
  }

  public Result Reactivate(
    TenantStatusChangeReason reason,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (Status != TenantStatus.Suspended)
    {
      return Result.Failure(TenantLifecycleErrors.InvalidTransition);
    }

    if (reason is not (TenantStatusChangeReason.IssueResolved or
      TenantStatusChangeReason.Administrative or
      TenantStatusChangeReason.Security or
      TenantStatusChangeReason.Compliance or
      TenantStatusChangeReason.Operational))
    {
      return Result.Failure(TenantLifecycleErrors.InvalidTransitionReason);
    }

    return Transition(
      TenantStatus.Active,
      reason,
      actor,
      eventId,
      occurredUtc,
      static (id, time, tenantId, previous, next, transitionReason) =>
        new TenantReactivated(id, time, tenantId, previous, next, transitionReason));
  }

  public Result Archive(
    TenantStatusChangeReason reason,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (Status is not (TenantStatus.Provisioning or TenantStatus.Active or TenantStatus.Suspended))
    {
      return Result.Failure(TenantLifecycleErrors.InvalidTransition);
    }

    return Transition(
      TenantStatus.Archived,
      reason,
      actor,
      eventId,
      occurredUtc,
      static (id, time, tenantId, previous, next, transitionReason) =>
        new TenantArchived(id, time, tenantId, previous, next, transitionReason));
  }

  private Result Transition(
    TenantStatus nextStatus,
    TenantStatusChangeReason reason,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc,
    Func<Guid, DateTimeOffset, Guid, TenantStatus, TenantStatus, TenantStatusChangeReason, DomainEvent> createEvent)
  {
    if (!Enum.IsDefined(reason) || reason == TenantStatusChangeReason.Created)
    {
      return Result.Failure(TenantLifecycleErrors.InvalidTransitionReason);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure(TenantLifecycleErrors.InvalidActor);
    }

    var utc = occurredUtc.ToUniversalTime();
    var previousStatus = Status;
    Status = nextStatus;
    StatusChangedUtc = utc;
    StatusChangedBy = actor;
    StatusChangeReasonCode = reason;
    ModifiedUtc = utc;
    ModifiedBy = actor;
    RaiseDomainEvent(createEvent(eventId, utc, TenantId, previousStatus, nextStatus, reason));
    return Result.Success();
  }

  private static bool IsValidActor(string? actor) =>
    !string.IsNullOrWhiteSpace(actor) && actor.Length <= ActorMaximumLength;
}
