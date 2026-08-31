using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Authorization;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Companies;

namespace SSAS.Attendance.Application.Reads;

// ================================================================================================
// THE FIRST THREE-DIMENSIONAL READ SCOPE IN THE PRODUCT (OD-ATT-0011).
// ================================================================================================
//
// `AuthorizedCompanyScope` (HR), `GlReadScope` and `PayrollReadScope` are all tenant + company.
// **This one adds BRANCH**, and it is the first scope type where branch carries authorization meaning
// rather than describing a row.
//
// The private constructor and the `internal` factory are the same shape as its three predecessors, and for
// the same reason: **holding one is proof that ATTENDANCE's permission check and ATTENDANCE's company and
// branch resolution all ran against live state.** A scope handed in as a parameter would be forgeable by
// whoever called, and the property the type exists to guarantee is *checked live, just now*.
//
// The type is NOT shared across modules. `PayrollReadScope` recorded why when the company SET was promoted
// into BuildingBlocks: the value moved, the wrapper did not, because a shared scope type would let any
// module that could build one hand it to any other module's read service.
//
// ---- THE BRANCH SET IS SEPARATE FROM THE COMPANY SET, AND BOTH MUST BE NON-EMPTY.
//
// An empty branch set REFUSES rather than returning an empty page, exactly as the company set does. An empty
// page says "there is nothing here", a claim about the DATA; a refusal says "you cannot see", a claim about
// the CALLER. Only the second is true, and only the second stays true when someone later grants a branch.
//
// ---- AND THE HALF THAT IS NOT HERE.
//
// **`IAttendanceSummary` does not take one of these.** `OD-ATT-0011`'s split makes the Payroll summary
// contract branch-blind and company-complete, so it resolves its own company authority and applies NO
// branch predicate at all. That asymmetry is the ruling, the hole in it is ruled INTENDED, and an
// architecture guard asserts the contract's query carries no branch filter.
public sealed class AttendanceReadScope
{
  private AttendanceReadScope(Guid tenantId, AuthorizedCompanySet companies, IReadOnlyList<Guid> branchIds)
  {
    TenantId = tenantId;
    Companies = companies;
    BranchIds = branchIds;
  }

  // Carried so a query STATES the invariant it depends on rather than inheriting it from the global filter.
  public Guid TenantId { get; }

  // The promoted set (`ADR-027` d4). The wrapper carries the proof; this is only the data.
  public AuthorizedCompanySet Companies { get; }

  public IReadOnlyList<Guid> CompanyIds => Companies.CompanyIds;

  // ---- ACTIVE BRANCHES ONLY, AND THAT IS THE RESOLVER'S GUARANTEE RATHER THAN THIS TYPE'S.
  //
  // `ITenantBranchAccessResolver` always intersects with active branches: an assignment row naming a
  // deactivated branch survives deliberately, so reactivating restores prior access, and the resolver's
  // filter is what stops the retained row granting entry meanwhile.
  //
  // This type does not re-derive that. Re-deriving it would be a second implementation of a rule that lives
  // in one place, and the two would eventually disagree.
  public IReadOnlyList<Guid> BranchIds { get; }

  // `internal`, single caller. The empty checks live here rather than in the resolver so they hold for every
  // future caller of the factory, not merely the one that exists today.
  internal static AttendanceReadScope? Create(
    Guid tenantId, IReadOnlyList<Guid> companyIds, IReadOnlyList<Guid> branchIds)
  {
    ArgumentNullException.ThrowIfNull(companyIds);
    ArgumentNullException.ThrowIfNull(branchIds);

    if (tenantId == Guid.Empty || branchIds.Count == 0)
    {
      return null;
    }

    var companies = AuthorizedCompanySet.Create(companyIds);
    return companies is null ? null : new AttendanceReadScope(tenantId, companies, [.. branchIds]);
  }
}

public interface IAttendanceScopeResolver
{
  // ---- THE THREE-DIMENSIONAL RESOLVE, FOR RECORD READS.
  //
  // The functional permission is a PARAMETER rather than a constant because this module has several read
  // surfaces with materially different authority — a calendar is structural configuration, a record is
  // personal data, and a leave type can disclose a medical fact. A resolver that hard-coded one would either
  // check the wrong permission or tempt a caller to bypass it for the others.
  Task<Result<AttendanceReadScope>> ResolveAsync(
    string permissionName, CancellationToken cancellationToken = default);

  // ---- THE TWO-DIMENSIONAL RESOLVE, FOR SURFACES BRANCH DOES NOT SCOPE.
  //
  // Calendars, leave types, leave requests and balances are company-owned and asserted NOT branch-owned.
  // Applying a branch predicate to them would filter on a dimension they do not carry, which in EF terms
  // means filtering on a column that does not exist and in review terms means nobody could explain it.
  //
  // A SEPARATE METHOD rather than a nullable flag: a flag would make "did this read apply branch scope"
  // a question about an argument at every call site, and the answer would be wrong exactly once.
  Task<Result<AttendanceReadScope>> ResolveCompanyOnlyAsync(
    string permissionName, CancellationToken cancellationToken = default);

  // Writes name exactly ONE company and must prove the caller may reach that one. A separate method rather
  // than reusing the read path, because a write filtered by a set is answering a different question from the
  // one it needs answered.
  Task<Result> AuthorizeAsync(
    string permissionName, Guid companyId, CancellationToken cancellationToken = default);

  Result RequirePermission(string permissionName);

  bool HasPermission(string permissionName);
}

// THE ONLY PLACE AN `AttendanceReadScope` COMES FROM.
//
// Every check reads LIVE state. `ITenantCompanyAccessResolver` and `ITenantBranchAccessResolver` each answer
// "what may this user reach RIGHT NOW", never a value the caller supplied and never one cached from an
// earlier request. A grant revoked a moment ago must refuse the read in flight (`AC-ATT-0029`).
//
// The axes are independent and none widens another: the functional permission says which OPERATION is
// permitted, the company set which company's data is reachable, the branch set which location's. A tenant
// administrator without `Attendance.Records.View` reads no attendance at all.
public sealed class AttendanceScopeResolver(
  ITenantCompanyAccessResolver companyAccess,
  ITenantBranchAccessResolver branchAccess,
  ICurrentTenant currentTenant,
  ICurrentTenantUser currentTenantUser,
  ICurrentUser currentUser) : IAttendanceScopeResolver
{
  public bool HasPermission(string permissionName)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
    return currentUser.Permissions.Contains(permissionName, StringComparer.Ordinal);
  }

  public Result RequirePermission(string permissionName) =>
    HasPermission(permissionName)
      ? Result.Success()
      : Result.Failure(AttendanceScopeErrors.WritePermissionDenied);

  public async Task<Result> AuthorizeAsync(
    string permissionName, Guid companyId, CancellationToken cancellationToken = default)
  {
    // Permission first, so an unauthorized caller never causes a company lookup: the cheap refusal happens
    // before the expensive one.
    var permitted = RequirePermission(permissionName);
    if (permitted.IsFailure)
    {
      return permitted;
    }

    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure(AttendanceScopeErrors.InvalidActor);
    }

    var companies = await companyAccess.GetPermittedCompaniesAsync(tenantId, tenantUserId, cancellationToken);
    if (companies.IsFailure)
    {
      return Result.Failure(AttendanceScopeErrors.CompanyScopeDenied);
    }

    return companies.Value.Any(company => company.CompanyId == companyId)
      ? Result.Success()
      : Result.Failure(AttendanceScopeErrors.CompanyScopeDenied);
  }

  public Task<Result<AttendanceReadScope>> ResolveAsync(
    string permissionName, CancellationToken cancellationToken = default) =>
    ResolveCoreAsync(permissionName, includeBranches: true, cancellationToken);

  public Task<Result<AttendanceReadScope>> ResolveCompanyOnlyAsync(
    string permissionName, CancellationToken cancellationToken = default) =>
    ResolveCoreAsync(permissionName, includeBranches: false, cancellationToken);

  private async Task<Result<AttendanceReadScope>> ResolveCoreAsync(
    string permissionName, bool includeBranches, CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);

    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure<AttendanceReadScope>(AttendanceScopeErrors.InvalidActor);
    }

    if (!currentUser.Permissions.Contains(permissionName, StringComparer.Ordinal))
    {
      return Result.Failure<AttendanceReadScope>(AttendanceScopeErrors.ReadPermissionDenied);
    }

    var permitted = await companyAccess.GetPermittedCompaniesAsync(tenantId, tenantUserId, cancellationToken);
    if (permitted.IsFailure)
    {
      return Result.Failure<AttendanceReadScope>(AttendanceScopeErrors.CompanyScopeDenied);
    }

    var companyIds = permitted.Value.Select(company => company.CompanyId).ToArray();

    // ---- THE COMPANY-ONLY PATH STILL PRODUCES A NON-EMPTY BRANCH LIST, AND THIS NEEDS SAYING.
    //
    // The factory refuses an empty branch set, so a company-only scope carries a single sentinel entry that
    // NO QUERY READS. The alternative — making `BranchIds` nullable — would put a null check at every use
    // site and make "does this scope carry branch authority" a runtime question.
    //
    // Callers on the company-only path do not touch `BranchIds` at all, and the architecture guard asserting
    // which queries apply a branch predicate is what keeps that true.
    if (!includeBranches)
    {
      var companyOnly = AttendanceReadScope.Create(tenantId, companyIds, [Guid.Empty]);
      return companyOnly is null
        ? Result.Failure<AttendanceReadScope>(AttendanceScopeErrors.CompanyScopeDenied)
        : Result.Success(companyOnly);
    }

    var branches = await branchAccess.GetPermittedBranchesAsync(tenantId, tenantUserId, cancellationToken);
    if (branches.IsFailure)
    {
      return Result.Failure<AttendanceReadScope>(AttendanceScopeErrors.BranchScopeDenied);
    }

    var branchIds = branches.Value.Select(branch => branch.BranchId).ToArray();

    // Fail closed. `ITenantBranchAccessResolver` documents an empty answer as legitimate for a tenant
    // administrator whose tenant has no branches yet, and instructs callers to fail closed rather than fall
    // back to "all" — this does exactly that, and the refusal is distinguishable from the company one so an
    // operator can tell which grant is missing.
    // ---- ⚠ AND THE REFUSAL NAMES THE GRANT CLASS THAT IS ACTUALLY MISSING (item 229).
    //
    // `Create` returns null when EITHER set is empty, so labelling every null `BranchScopeDenied` sent an
    // operator to grant a BRANCH when the missing grant was a COMPANY — and the promise two paragraphs
    // above is precisely that the two refusals are distinguishable. **Constructible: a tenant
    // administrator with branch access and no company assignment.**
    //
    // The empty checks stay in the factory, which is where the GUARANTEE lives; this only decides the
    // LABEL. `AuthorizedCompanySet.Create` returns null for a null-or-empty list and nothing else, so an
    // empty `companyIds` is an exact discriminator rather than a guess at which check fired.
    //
    // ⚠ **NO DISCLOSURE CHANGE.** An operator learns which grant CLASS is missing, never which grant —
    // neither refusal names a company, a branch, a tenant or any topology, exactly as before.
    var scope = AttendanceReadScope.Create(tenantId, companyIds, branchIds);
    return scope is null
      ? Result.Failure<AttendanceReadScope>(companyIds.Length == 0
        ? AttendanceScopeErrors.CompanyScopeDenied
        : AttendanceScopeErrors.BranchScopeDenied)
      : Result.Success(scope);
  }
}

// Scope refusals name NO company, NO branch, NO tenant and NO database topology. A caller who cannot reach a
// company learns only that they cannot, never whether the identifier they guessed exists.
public static class AttendanceScopeErrors
{
  public static readonly Error InvalidActor = new(
    "Attendance.InvalidActor",
    "The request does not carry a resolved tenant user.");

  public static readonly Error ReadPermissionDenied = new(
    "Attendance.ReadPermissionDenied",
    "The caller does not hold the permission required for this read.");

  public static readonly Error WritePermissionDenied = new(
    "Attendance.WritePermissionDenied",
    "The caller does not hold the permission required for this operation.");

  public static readonly Error CompanyScopeDenied = new(
    "Attendance.CompanyScopeDenied",
    "The caller has no authorized company scope for this read.");

  public static readonly Error BranchScopeDenied = new(
    "Attendance.BranchScopeDenied",
    "The caller has no authorized branch scope for this read.");
}
