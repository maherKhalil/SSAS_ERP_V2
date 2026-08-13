using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.TenantStorage;

// Maps a tenant to the physical database that currently serves it (ADR-017). Endpoint facts belong to
// TenantDatabase; this type owns only the mapping and its history.
//
// HISTORY IS IN-TABLE: an assignment is ACTIVE while EndedUtc IS NULL, and superseded assignments are
// retained rather than deleted — the same active/removed idiom the repository already uses for
// RolePermissionAssignment and PlatformPermissionAssignment. A filtered unique index over TenantId where
// EndedUtc IS NULL makes "at most one active assignment per tenant" a database invariant, not an
// application convention.
//
// Platform operational metadata, so deliberately NOT ITenantOwnedEntity: routing and bootstrap must read
// assignments without an ambient tenant filter. Tenant isolation comes from the resolver looking up a
// TRUSTED TenantId, never from treating these rows as tenant business entities.
//
// FUTURE FLIP HAZARD (TS-1D, not implemented here): changing the active assignment must END the current
// row, SaveChanges, INSERT the replacement, SaveChanges — two flushes inside ONE explicit transaction.
// SQL Server has no deferrable constraints, and EF Core does not guarantee it emits the UPDATE that
// vacates the active slot before the INSERT that fills it, so a single flush can transiently violate the
// filtered unique index.
public sealed class TenantDatabaseAssignment : AggregateRoot<long>, IAuditableEntity
{
  public const int ReasonMaximumLength = 256;

  public const int ActorMaximumLength = 256;

  // The routing version of a tenant's FIRST assignment. Subsequent assignments increment monotonically
  // per tenant inside the flip transaction (TS-1D).
  public const long InitialRoutingVersion = 1;

  private TenantDatabaseAssignment(
    long id,
    Guid tenantId,
    long tenantDatabaseId,
    long routingVersion,
    string? reason,
    string actor,
    DateTimeOffset occurredUtc)
    : base(id)
  {
    TenantId = tenantId;
    TenantDatabaseId = tenantDatabaseId;
    RoutingVersion = routingVersion;
    Reason = reason;
    AssignedUtc = occurredUtc.ToUniversalTime();
    CreatedUtc = AssignedUtc;
    CreatedBy = actor;
    ModifiedUtc = AssignedUtc;
    ModifiedBy = actor;
  }

  private TenantDatabaseAssignment()
    : base(0)
  {
  }

  public Guid TenantId { get; private set; }

  public long TenantDatabaseId { get; private set; }

  // Monotonic per tenant. This is the correctness mechanism future resolver caching depends on: a cached
  // route is valid only for the RoutingVersion it was resolved under. It is not informational metadata.
  public long RoutingVersion { get; private set; }

  public DateTimeOffset AssignedUtc { get; private set; }

  // NULL means ACTIVE. This is the column the filtered unique index and every "current assignment" query
  // predicate on.
  public DateTimeOffset? EndedUtc { get; private set; }

  public string? Reason { get; private set; }

  public bool IsActive => EndedUtc is null;

  public byte[] RowVersion { get; private set; } = [];

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  // The only creation path in this slice: a tenant's first assignment, at InitialRoutingVersion.
  public static Result<TenantDatabaseAssignment> CreateInitial(
    Guid tenantId,
    long tenantDatabaseId,
    string? reason,
    string actor,
    DateTimeOffset occurredUtc) =>
    Create(tenantId, tenantDatabaseId, InitialRoutingVersion, reason, actor, occurredUtc);

  public static Result<TenantDatabaseAssignment> Create(
    Guid tenantId,
    long tenantDatabaseId,
    long routingVersion,
    string? reason,
    string actor,
    DateTimeOffset occurredUtc)
  {
    if (tenantId == Guid.Empty)
    {
      return Result.Failure<TenantDatabaseAssignment>(TenantStorageErrors.TenantRequired);
    }

    if (tenantDatabaseId <= 0)
    {
      return Result.Failure<TenantDatabaseAssignment>(TenantStorageErrors.TenantDatabaseRequired);
    }

    if (routingVersion < InitialRoutingVersion)
    {
      return Result.Failure<TenantDatabaseAssignment>(TenantStorageErrors.RoutingVersionInvalid);
    }

    if (reason is not null && reason.Length > ReasonMaximumLength)
    {
      reason = reason[..ReasonMaximumLength];
    }

    return Result.Success(new TenantDatabaseAssignment(
      0, tenantId, tenantDatabaseId, routingVersion, reason, actor, occurredUtc));
  }

  // Ends an active assignment, retaining it as history. The replacement row is inserted by the flip
  // service (TS-1D) in a separate flush within the same transaction — see the type comment.
  public Result End(string actor, DateTimeOffset occurredUtc)
  {
    if (EndedUtc is not null)
    {
      return Result.Failure(TenantStorageErrors.AssignmentAlreadyEnded);
    }

    var endedUtc = occurredUtc.ToUniversalTime();
    if (endedUtc < AssignedUtc)
    {
      return Result.Failure(TenantStorageErrors.AssignmentEndBeforeStart);
    }

    EndedUtc = endedUtc;
    ModifiedUtc = endedUtc;
    ModifiedBy = actor;
    return Result.Success();
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
