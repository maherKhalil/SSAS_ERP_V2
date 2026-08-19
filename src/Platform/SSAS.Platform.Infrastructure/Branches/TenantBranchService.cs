using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Platform.Infrastructure.Branches;

// THE BRANCH LIFECYCLE, IMPLEMENTED ACROSS TWO PLANES (Branch foundation B1a).
//
// Branches live in the tenant database; the user assignments that make a deactivation safe or unsafe live
// in the platform one. Every operation here is therefore tenant-database work guarded by platform-database
// facts, never a join between them.
//
// THE "EXACTLY ONE ACTIVE MAIN" RULE IS STRONGER THAN THE SCHEMA'S. The filtered unique index enforces
// AT MOST one; that a tenant which has finished onboarding always has AT LEAST one is enforced here,
// because "the set of active branches is non-empty implies one of them is main" is a statement about a set,
// which no row constraint can make.
internal sealed class TenantBranchService(
  PlatformDbContext platform,
  ITenantDbContextFactory tenantContextFactory,
  ITenantAdministratorAuthority administratorAuthority,
  ICurrentTenant currentTenant,
  ICurrentAuthenticationSession currentSession) : ITenantBranchService
{
  // Long enough to outlast an administrator's validation round trip, short enough that a stuck peer
  // surfaces as a retryable refusal rather than a hung request.
  private static readonly TimeSpan TopologyLockTimeout = TimeSpan.FromSeconds(15);

  public async Task<Result<BranchDto>> CreateAsync(
    CreateBranchRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var authorized = await AuthorizeAsync(cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<BranchDto>(authorized.Error);
    }

    var code = BranchCode.Create(request.Code);
    var name = BranchName.Create(request.Name);
    if (code.IsFailure || name.IsFailure)
    {
      return Result.Failure<BranchDto>(code.IsFailure ? code.Error : name.Error);
    }

    var tenantId = authorized.Value.TenantId;
    var context = await tenantContextFactory.CreateAsync(tenantId, cancellationToken);
    if (context.IsFailure)
    {
      return Result.Failure<BranchDto>(context.Error);
    }

    await using var tenant = context.Value;

    var activeCount = await tenant.Branches.CountAsync(branch => branch.IsActive, cancellationToken);

    // ---- THE FIRST ACTIVE BRANCH IS ALWAYS MAIN, whatever the caller asked for. A tenant emerging from
    // onboarding with no main branch would satisfy the schema and still be unusable, and asking the very
    // first administrator to know that rule is how it gets missed.
    var isMain = activeCount == 0 || request.IsMainBranch;

    // A SECOND MAIN IS REFUSED HERE RATHER THAN SWITCHED. Promotion is a two-row transition and belongs to
    // Update, which performs it atomically; letting Create silently demote an existing main would change a
    // branch the caller never named.
    if (isMain && activeCount > 0 &&
      await tenant.Branches.AnyAsync(branch => branch.IsActive && branch.IsMainBranch, cancellationToken))
    {
      return Result.Failure<BranchDto>(BranchErrors.MainBranchAlreadyExists);
    }

    var created = Branch.Create(tenantId, code.Value, name.Value, isMain, authorized.Value.Actor);
    if (created.IsFailure)
    {
      return Result.Failure<BranchDto>(created.Error);
    }

    tenant.Branches.Add(created.Value);

    try
    {
      await tenant.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception) when (UniqueViolation(exception) is { } index)
    {
      // The database is the final authority on both uniqueness rules, and it names which one it refused —
      // so a concurrent create loses on the exact rule it broke rather than on a guessed one.
      return Result.Failure<BranchDto>(index.Contains("MainBranch", StringComparison.Ordinal)
        ? BranchErrors.MainBranchAlreadyExists
        : BranchErrors.CodeAlreadyExists);
    }

    return Result.Success(ToDto(created.Value));
  }

  public async Task<Result<BranchDto>> GetAsync(Guid branchId, CancellationToken cancellationToken = default)
  {
    var authorized = await AuthorizeAsync(cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<BranchDto>(authorized.Error);
    }

    var context = await tenantContextFactory.CreateAsync(authorized.Value.TenantId, cancellationToken);
    if (context.IsFailure)
    {
      return Result.Failure<BranchDto>(context.Error);
    }

    await using var tenant = context.Value;

    // The global tenant filter already scopes this; a branch from another tenant is simply not found, which
    // is also the answer that reveals least.
    var branch = await tenant.Branches.AsNoTracking()
      .SingleOrDefaultAsync(candidate => candidate.Id == branchId, cancellationToken);

    return branch is null
      ? Result.Failure<BranchDto>(BranchErrors.NotFound)
      : Result.Success(ToDto(branch));
  }

  public async Task<Result<IReadOnlyList<BranchDto>>> ListAsync(
    bool includeInactive = false,
    CancellationToken cancellationToken = default)
  {
    var authorized = await AuthorizeAsync(cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<IReadOnlyList<BranchDto>>(authorized.Error);
    }

    var context = await tenantContextFactory.CreateAsync(authorized.Value.TenantId, cancellationToken);
    if (context.IsFailure)
    {
      return Result.Failure<IReadOnlyList<BranchDto>>(context.Error);
    }

    await using var tenant = context.Value;

    // MAIN FIRST, THEN BY NAME — deterministic, and it puts the branch most callers want at the top of the
    // list rather than wherever the collation happens to place it.
    var branches = await tenant.Branches.AsNoTracking()
      .Where(branch => includeInactive || branch.IsActive)
      .OrderByDescending(branch => branch.IsMainBranch)
      .ThenBy(branch => branch.BranchName)
      .ToListAsync(cancellationToken);

    return Result.Success<IReadOnlyList<BranchDto>>(branches.Select(ToDto).ToArray());
  }

  public async Task<Result<BranchDto>> UpdateAsync(
    UpdateBranchRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var authorized = await AuthorizeAsync(cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<BranchDto>(authorized.Error);
    }

    var code = BranchCode.Create(request.Code);
    var name = BranchName.Create(request.Name);
    if (code.IsFailure || name.IsFailure)
    {
      return Result.Failure<BranchDto>(code.IsFailure ? code.Error : name.Error);
    }

    var tenantId = authorized.Value.TenantId;

    // PROMOTION TOUCHES TWO ROWS, so it is serialised with deactivation: both change which branch is main,
    // and interleaving them can leave a tenant with none.
    await using var lease = await AcquireTopologyAsync(tenantId, cancellationToken);
    if (lease is null)
    {
      return Result.Failure<BranchDto>(BranchErrors.TopologyBusy);
    }

    var context = await tenantContextFactory.CreateAsync(tenantId, cancellationToken);
    if (context.IsFailure)
    {
      return Result.Failure<BranchDto>(context.Error);
    }

    await using var tenant = context.Value;
    await using var transaction = await tenant.Database.BeginTransactionAsync(cancellationToken);

    var branch = await tenant.Branches
      .SingleOrDefaultAsync(candidate => candidate.Id == request.BranchId, cancellationToken);
    if (branch is null)
    {
      return Result.Failure<BranchDto>(BranchErrors.NotFound);
    }

    if (!branch.RowVersion.AsSpan().SequenceEqual(request.RowVersion.AsSpan()))
    {
      return Result.Failure<BranchDto>(BranchErrors.ConcurrencyConflict);
    }

    var renamed = branch.Rename(code.Value, name.Value);
    if (renamed.IsFailure)
    {
      return Result.Failure<BranchDto>(renamed.Error);
    }

    if (request.IsMainBranch && !branch.IsMainBranch)
    {
      // ---- THE SWITCH, IN ONE TRANSACTION AND IN THIS ORDER. The old main is demoted and FLUSHED before
      // the new one is promoted: the filtered unique index admits one active main and SQL Server has no
      // deferrable constraints, so promoting first would be refused by the very rule being maintained.
      // One transaction is also what stops an observer from ever seeing a tenant with no main branch.
      var currentMain = await tenant.Branches
        .SingleOrDefaultAsync(candidate => candidate.IsActive && candidate.IsMainBranch, cancellationToken);
      if (currentMain is not null)
      {
        currentMain.ClearMainBranch();
        await tenant.SaveChangesAsync(cancellationToken);
      }

      var promoted = branch.MarkAsMainBranch();
      if (promoted.IsFailure)
      {
        return Result.Failure<BranchDto>(promoted.Error);
      }
    }
    else if (!request.IsMainBranch && branch.IsMainBranch)
    {
      // Clearing main without naming a successor would leave an onboarded tenant with active branches and
      // no main — the "at least one" half of the invariant, which no index can state.
      return Result.Failure<BranchDto>(BranchErrors.ReplacementMainBranchRequired);
    }

    try
    {
      await tenant.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
      return Result.Failure<BranchDto>(BranchErrors.ConcurrencyConflict);
    }
    catch (DbUpdateException exception) when (UniqueViolation(exception) is { } index)
    {
      return Result.Failure<BranchDto>(index.Contains("MainBranch", StringComparison.Ordinal)
        ? BranchErrors.MainBranchAlreadyExists
        : BranchErrors.CodeAlreadyExists);
    }

    return Result.Success(ToDto(branch));
  }

  public async Task<Result> DeactivateAsync(
    DeactivateBranchRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var authorized = await AuthorizeAsync(cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    var tenantId = authorized.Value.TenantId;

    // ---- SERIALISED AGAINST EVERY OTHER TOPOLOGY CHANGE. See BranchTopologyLock for the three
    // interleavings this closes and the one obligation it places on the assignment workflow.
    await using var lease = await AcquireTopologyAsync(tenantId, cancellationToken);
    if (lease is null)
    {
      return Result.Failure(BranchErrors.TopologyBusy);
    }

    var context = await tenantContextFactory.CreateAsync(tenantId, cancellationToken);
    if (context.IsFailure)
    {
      return context;
    }

    await using var tenant = context.Value;

    var branch = await tenant.Branches
      .SingleOrDefaultAsync(candidate => candidate.Id == request.BranchId, cancellationToken);
    if (branch is null)
    {
      return Result.Failure(BranchErrors.NotFound);
    }

    if (!branch.IsActive)
    {
      return Result.Failure(BranchErrors.AlreadyInactive);
    }

    if (!branch.RowVersion.AsSpan().SequenceEqual(request.RowVersion.AsSpan()))
    {
      return Result.Failure(BranchErrors.ConcurrencyConflict);
    }

    var remainingActive = await tenant.Branches
      .Where(candidate => candidate.IsActive && candidate.Id != request.BranchId)
      .Select(candidate => candidate.Id)
      .ToListAsync(cancellationToken);

    // ---- ONBOARDING IS NOT REVERSIBLE. Zero active branches is a provisioning state a tenant passes
    // through once; returning to it would make the tenant unusable with no onboarding path back.
    if (remainingActive.Count == 0)
    {
      return Result.Failure(BranchErrors.CannotDeactivateOnlyActiveBranch);
    }

    Branch? replacement = null;
    if (branch.IsMainBranch)
    {
      if (request.ReplacementMainBranchId is not { } replacementId || replacementId == request.BranchId)
      {
        return Result.Failure(BranchErrors.ReplacementMainBranchRequired);
      }

      replacement = await tenant.Branches
        .SingleOrDefaultAsync(candidate => candidate.Id == replacementId && candidate.IsActive, cancellationToken);
      if (replacement is null)
      {
        return Result.Failure(BranchErrors.ReplacementMainBranchRequired);
      }
    }

    var stranded = await WouldStrandUsersAsync(tenantId, request.BranchId, remainingActive, cancellationToken);
    if (stranded.IsFailure)
    {
      return stranded;
    }

    await using var transaction = await tenant.Database.BeginTransactionAsync(cancellationToken);

    try
    {
      // DEACTIVATE FIRST, THEN PROMOTE. Deactivating removes the outgoing branch from the filtered unique
      // index's scope, so the replacement can be promoted without the two ever both matching the filter.
      var deactivated = branch.Deactivate();
      if (deactivated.IsFailure)
      {
        return deactivated;
      }

      branch.ClearMainBranch();
      await tenant.SaveChangesAsync(cancellationToken);

      if (replacement is not null)
      {
        var promoted = replacement.MarkAsMainBranch();
        if (promoted.IsFailure)
        {
          return promoted;
        }

        await tenant.SaveChangesAsync(cancellationToken);
      }

      await transaction.CommitAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
      return Result.Failure(BranchErrors.ConcurrencyConflict);
    }

    return Result.Success();
  }

  public async Task<Result<TenantBranchOnboardingState>> GetOnboardingStateAsync(
    CancellationToken cancellationToken = default)
  {
    if (currentTenant.TenantId is not { } tenantId || tenantId == Guid.Empty)
    {
      return Result.Failure<TenantBranchOnboardingState>(BranchErrors.ContextRequired);
    }

    // DELIBERATELY NOT ADMINISTRATOR-GATED. Every authenticated user of the tenant needs to know whether
    // the tenant is still awaiting its first branch; refusing to say so would leave a normal user with an
    // unexplained empty branch list.
    var context = await tenantContextFactory.CreateAsync(tenantId, cancellationToken);
    if (context.IsFailure)
    {
      return Result.Failure<TenantBranchOnboardingState>(context.Error);
    }

    await using var tenant = context.Value;
    var activeCount = await tenant.Branches.CountAsync(branch => branch.IsActive, cancellationToken);
    return Result.Success(new TenantBranchOnboardingState(activeCount == 0, activeCount));
  }

  // ---- WOULD ANY ACTIVE NORMAL USER BE LEFT WITH NOTHING?
  //
  // Only users assigned to THIS branch can be affected, so the platform read is bounded by that branch's
  // assignment list rather than by the tenant's user count. For each, the question is whether any of their
  // OTHER assignments names a branch that is still active after this one goes.
  //
  // TENANT ADMINISTRATORS ARE EXEMPT because their scope is implicit: they hold no assignment rows and
  // reach every active branch, so a deactivation cannot strand them while any branch remains.
  private async Task<Result> WouldStrandUsersAsync(
    Guid tenantId,
    Guid branchId,
    IReadOnlyList<Guid> remainingActiveBranchIds,
    CancellationToken cancellationToken)
  {
    var affectedUserIds = await platform.UserBranchAccess
      .AsNoTracking()
      .Where(access => access.TenantId == tenantId && access.BranchId == branchId)
      .Select(access => access.TenantUserId)
      .Distinct()
      .ToListAsync(cancellationToken);

    if (affectedUserIds.Count == 0)
    {
      return Result.Success();
    }

    // One round trip for the whole affected set: their other assignments that survive the deactivation.
    var survivingByUser = await platform.UserBranchAccess
      .AsNoTracking()
      .Where(access => access.TenantId == tenantId &&
        affectedUserIds.Contains(access.TenantUserId) &&
        access.BranchId != branchId &&
        remainingActiveBranchIds.Contains(access.BranchId))
      .Select(access => access.TenantUserId)
      .Distinct()
      .ToListAsync(cancellationToken);

    var wouldStrand = affectedUserIds.Where(userId => !survivingByUser.Contains(userId)).ToArray();
    foreach (var userId in wouldStrand)
    {
      if (!await administratorAuthority.IsTenantAdministratorAsync(tenantId, userId, cancellationToken))
      {
        // NO AUTOMATIC REASSIGNMENT. Choosing a replacement branch for someone is a business decision, and
        // making it silently during a deactivation is how people end up working in the wrong place.
        return Result.Failure(BranchErrors.DeactivationWouldStrandUsers);
      }
    }

    return Result.Success();
  }

  private async Task<Result<(Guid TenantId, string Actor)>> AuthorizeAsync(CancellationToken cancellationToken)
  {
    if (currentTenant.TenantId is not { } tenantId || tenantId == Guid.Empty ||
      currentSession.Value is not { } session || session.TenantId != tenantId)
    {
      return Result.Failure<(Guid, string)>(BranchErrors.TenantAdministratorRequired);
    }

    // ASKED OF THE DATABASE, NOT OF THE TOKEN. Authority can be revoked inside a session's lifetime, and a
    // branch administration command is exactly where a revoked administrator must stop being one.
    return await administratorAuthority.IsTenantAdministratorAsync(tenantId, session.TenantUserId, cancellationToken)
      ? Result.Success((tenantId, session.TenantUserId.ToString(System.Globalization.CultureInfo.InvariantCulture)))
      : Result.Failure<(Guid, string)>(BranchErrors.TenantAdministratorRequired);
  }

  private async Task<SqlConnection?> AcquireTopologyAsync(Guid tenantId, CancellationToken cancellationToken)
  {
    var connection = new SqlConnection(platform.Database.GetConnectionString());
    await connection.OpenAsync(cancellationToken);

    if (await BranchTopologyLock.TryAcquireForSessionAsync(
      connection, tenantId, TopologyLockTimeout, cancellationToken))
    {
      return connection;
    }

    await connection.DisposeAsync();
    return null;
  }

  private static string? UniqueViolation(DbUpdateException exception) =>
    exception.InnerException is SqlException { Number: 2601 or 2627 } sql ? sql.Message : null;

  private static BranchDto ToDto(Branch branch) => new(
    branch.Id, branch.BranchCode.Value, branch.BranchName.Value,
    branch.IsMainBranch, branch.IsActive, branch.RowVersion);
}
