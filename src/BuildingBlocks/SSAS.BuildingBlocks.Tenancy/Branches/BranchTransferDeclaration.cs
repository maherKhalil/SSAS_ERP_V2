using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Tenancy.Branches;

// HOW THE SOURCE BRANCH OF A SANCTIONED TRANSFER IS REACHED (FP-006C2, ADR-024 decisions 6 and 12).
public enum BranchTransferMode
{
  // THE ORDINARY CASE. The source is the caller's trusted execution branch, and the existing branch write
  // authorizer proves it is still theirs at save time. Nothing here widens that.
  CurrentBranch = 0,

  // THE NARROW RECOVERY (ADR-024 decision 12). Branch authorization always intersects with ACTIVE branches,
  // so an entity left in a deactivated branch is unreachable by every principal — including a tenant
  // administrator, whose scope is all *active* branches. This mode is the one-directional way out.
  //
  // It is an explicit exception to ADR-023 decision 5 and is deliberately NOT implemented by teaching
  // ITenantBranchAccessResolver to return inactive branches, which would widen every other caller.
  InactiveSourceRecovery = 1
}

// EXACTLY ONE AUTHORIZED BRANCH TRANSITION (FP-006C2, ADR-024 decision 3).
//
// It names ONE ENTITY, ONE SOURCE and ONE DESTINATION, and the branch write boundary permits a BranchId
// modification only for an entry matching all three. It is not a permission, not a mode, and not a flag:
// there is deliberately no representation here of "branch changes are allowed", because a switch that can
// be turned on for convenience is the boundary's absence rather than its exception (ADR-024 decision 11).
//
// ---- IDENTITY IS THE TRACKED INSTANCE, NOT A KEY.
//
// Matching is by reference against the entity the change tracker holds. That is the strictest available
// match and it sidesteps the whole key problem: branch-owned entities may key on Guid, long or a composite,
// and comparing keys would mean either a per-key-type code path or an unsafe string conversion — both of
// which can be made to collide. Two different entities cannot share a reference, so a declaration for one
// can never authorize another (ADR-024 decision 3; FP-006 AC-EMP-0043).
//
// It is immutable: a declaration that could be edited after the authorization that produced it would prove
// nothing about what was authorized.
public sealed class BranchTransferDeclaration
{
  private BranchTransferDeclaration(
    object entity,
    Type entityType,
    Guid sourceBranchId,
    Guid destinationBranchId,
    BranchTransferMode mode)
  {
    Entity = entity;
    EntityType = entityType;
    SourceBranchId = sourceBranchId;
    DestinationBranchId = destinationBranchId;
    Mode = mode;
  }

  // The tracked instance this transfer authorizes, and nothing else.
  public object Entity { get; }

  // Recorded alongside the reference so a declaration can be described in a log or an audit record without
  // dereferencing the entity, and so a type mismatch is visible rather than implied.
  public Type EntityType { get; }

  public Guid SourceBranchId { get; }

  public Guid DestinationBranchId { get; }

  public BranchTransferMode Mode { get; }

  public static Result<BranchTransferDeclaration> Create<TEntity>(
    TEntity entity,
    Guid sourceBranchId,
    Guid destinationBranchId,
    BranchTransferMode mode)
    where TEntity : class, IBranchOwnedEntity
  {
    if (entity is null || sourceBranchId == Guid.Empty || destinationBranchId == Guid.Empty)
    {
      return Result.Failure<BranchTransferDeclaration>(BranchTransferErrors.TransferInvalid);
    }

    // A transfer to the branch the entity is already in is not a transfer. Permitting it would let a
    // declaration exist that authorizes no observable change, which is a confusing thing to find in an
    // audit record and a pointless one to re-validate.
    if (sourceBranchId == destinationBranchId)
    {
      return Result.Failure<BranchTransferDeclaration>(BranchTransferErrors.TransferInvalid);
    }

    return Result.Success(new BranchTransferDeclaration(
      entity, typeof(TEntity), sourceBranchId, destinationBranchId, mode));
  }

  // Does this declaration authorize exactly this change? Every clause must hold:
  //
  //   * the SAME tracked instance — not another entity, and not another entity of the same type;
  //   * the branch it is LEAVING is the declared source, read from the ORIGINAL value rather than the
  //     current one, because the current value has already been overwritten by the caller;
  //   * the branch it is ENTERING is the declared destination.
  //
  // Anything else falls through to the ordinary rules, which refuse it.
  public bool Authorizes(object entity, Guid originalBranchId, Guid currentBranchId) =>
    ReferenceEquals(entity, Entity) &&
    originalBranchId == SourceBranchId &&
    currentBranchId == DestinationBranchId;
}
