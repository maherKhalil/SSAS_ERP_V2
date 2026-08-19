using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.Branches;

// AN OPERATING LOCATION INSIDE A TENANT (Branch foundation B0/B1).
//
// IT LIVES IN THE TENANT DATABASE, with the business data it scopes. Putting it in the platform database
// would make every branch-scoped read a cross-database join, and would move tenant business structure onto
// the plane that must stay available when tenant storage is not.
//
// IT IS TENANT-OWNED BUT NOT BRANCH-OWNED. A branch cannot belong to a branch, so it deliberately does not
// implement IBranchOwnedEntity — the write boundary would then demand an active branch context in order to
// create the very first branch, which is unreachable by construction.
//
// DEACTIVATION, NEVER DELETION. Branch identifiers are referenced from the platform database (user access)
// and will be referenced from years of business documents; removing the row would strand both. IsActive is
// what makes a branch stop being usable while remaining explicable.
public sealed class Branch : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity
{
  public const int ActorMaximumLength = 256;

  private string normalizedBranchCode = string.Empty;

  private Branch(
    Guid branchId,
    Guid tenantId,
    BranchCode branchCode,
    BranchName branchName,
    bool isMainBranch) : base(branchId)
  {
    TenantId = tenantId;
    BranchCode = branchCode;
    normalizedBranchCode = branchCode.NormalizedValue;
    BranchName = branchName;
    IsMainBranch = isMainBranch;
    IsActive = true;
  }

  private Branch()
    : base(Guid.Empty)
  {
    BranchCode = null!;
    BranchName = null!;
  }

  public Guid BranchId => Id;

  public Guid TenantId { get; private set; }

  public BranchCode BranchCode { get; private set; }

  public string NormalizedBranchCode => normalizedBranchCode;

  public BranchName BranchName { get; private set; }

  public bool IsMainBranch { get; private set; }

  public bool IsActive { get; private set; }

  // Owned by the IAuditableEntity persistence infrastructure; never stamped by the Domain.
  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public static Result<Branch> Create(
    Guid tenantId,
    BranchCode branchCode,
    BranchName branchName,
    bool isMainBranch,
    string actor)
  {
    ArgumentNullException.ThrowIfNull(branchCode);
    ArgumentNullException.ThrowIfNull(branchName);

    if (tenantId == Guid.Empty)
    {
      return Result.Failure<Branch>(BranchErrors.NotFound);
    }

    return string.IsNullOrWhiteSpace(actor) || actor.Length > ActorMaximumLength
      ? Result.Failure<Branch>(BranchErrors.InvalidActor)
      : Result.Success(new Branch(Guid.NewGuid(), tenantId, branchCode, branchName, isMainBranch));
  }

  public Result Rename(BranchCode branchCode, BranchName branchName)
  {
    ArgumentNullException.ThrowIfNull(branchCode);
    ArgumentNullException.ThrowIfNull(branchName);

    if (!IsActive)
    {
      return Result.Failure(BranchErrors.Inactive);
    }

    BranchCode = branchCode;
    normalizedBranchCode = branchCode.NormalizedValue;
    BranchName = branchName;
    return Result.Success();
  }

  // PROMOTION IS ONE-SIDED HERE. Making this branch main does not demote the previous one in this method:
  // that is a two-row change the caller must sequence inside one transaction, and the filtered unique index
  // is what refuses the moment both are main. Doing it silently here would hide a second branch mutating.
  public Result MarkAsMainBranch()
  {
    if (!IsActive)
    {
      return Result.Failure(BranchErrors.Inactive);
    }

    IsMainBranch = true;
    return Result.Success();
  }

  public Result ClearMainBranch()
  {
    IsMainBranch = false;
    return Result.Success();
  }

  // Idempotence is deliberately NOT offered: a caller deactivating an already-inactive branch is acting on
  // a stale view of the estate, and the invariant it was asked to protect was evaluated against that view.
  public Result Deactivate()
  {
    if (!IsActive)
    {
      return Result.Failure(BranchErrors.AlreadyInactive);
    }

    IsActive = false;
    return Result.Success();
  }

  public Result Reactivate()
  {
    IsActive = true;
    return Result.Success();
  }

  // The persistence infrastructure needs setters; the Domain keeps them closed. Same convention as Company.
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
