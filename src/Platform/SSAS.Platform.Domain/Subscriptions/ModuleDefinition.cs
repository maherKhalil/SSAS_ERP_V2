using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.Subscriptions;

// THE ENABLEMENT UNIT, AND ONLY THIS UNIT (FP-014, `OD-SUB-0005`).
//
// Four rows today -- HR, Finance/GL, Payroll, Attendance -- the four `IPermissionCatalogContributor`
// registrations in the Host, against seventeen mounted route groups. Seven of those groups are HR's alone
// and they share one key.
//
// ---- A SURROGATE KEY, WHICH IS A DELIBERATE DISAGREEMENT WITH `data-model.md`.
//
// `data-model.md` specifies `ModuleKey nvarchar(64)` as the primary key and argues for it: the key appears
// in a plan's grant list, a tenant's grant list, the enablement cache and a 403 response, so a surrogate
// means every one of those either joins or carries the key anyway.
//
// It also names its own fallback -- "if the build disagrees, a surrogate with a unique natural key is the
// right disagreement" -- and `domain-model.md` specifies `AggregateRoot<Guid>` for this same type. The two
// documents disagree with each other; this follows `domain-model.md`, the sanctioned fallback, and every
// other aggregate in this database. **The natural key keeps a unique ordinal index**, so nothing that
// depended on `ModuleKey` being unique has lost anything.
public sealed class ModuleDefinition : AggregateRoot<Guid>
{
  private ModuleDefinition(
    Guid moduleDefinitionId,
    ModuleKey moduleKey,
    string displayName,
    bool isGateable,
    string actor,
    DateTimeOffset occurredUtc) : base(moduleDefinitionId)
  {
    ModuleKey = moduleKey;
    DisplayName = displayName;
    IsGateable = isGateable;
    CreatedUtc = occurredUtc.ToUniversalTime();
    CreatedBy = actor;
  }

  private ModuleDefinition() : base(Guid.Empty) => ModuleKey = null!;

  public ModuleKey ModuleKey { get; private set; }

  public string DisplayName { get; private set; } = string.Empty;

  // False for the Host and the whole Platform surface (`REQ-SUB-0013`). A platform route that could be
  // gated is a route a lapsed tenant could be locked out of -- taking with it the surface that would let it
  // be re-enabled.
  public bool IsGateable { get; private set; }

  public DateTimeOffset CreatedUtc { get; private set; }

  public string CreatedBy { get; private set; } = string.Empty;

  public DateTimeOffset? ModifiedUtc { get; private set; }

  public string? ModifiedBy { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public static Result<ModuleDefinition> Create(
    ModuleKey moduleKey, string? displayName, bool isGateable, string actor, DateTimeOffset occurredUtc)
  {
    if (moduleKey is null)
    {
      return Result.Failure<ModuleDefinition>(SubscriptionErrors.InvalidModuleKey);
    }

    var trimmedName = displayName?.Trim();
    if (string.IsNullOrWhiteSpace(trimmedName) || trimmedName.Length > 200)
    {
      return Result.Failure<ModuleDefinition>(SubscriptionErrors.InvalidPlanName);
    }

    return string.IsNullOrWhiteSpace(actor)
      ? Result.Failure<ModuleDefinition>(SubscriptionErrors.InvalidActor)
      : Result.Success(new ModuleDefinition(
        Guid.NewGuid(), moduleKey, trimmedName, isGateable, actor, occurredUtc));
  }
}
