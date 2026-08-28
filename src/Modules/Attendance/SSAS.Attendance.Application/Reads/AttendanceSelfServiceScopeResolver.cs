using SSAS.Attendance.Domain.Leave;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.HR.Contracts.Employment;

namespace SSAS.Attendance.Application.Reads;

// A self-service read's subject and the scope derived from it. One object because they are resolved
// together and must not be able to disagree.
public sealed record OwnEmployeeAttendanceScope(AttendanceReadScope Scope, Guid EmployeeId);

// ==================================================================================================
// FP-015's ATTENDANCE SELF-SERVICE SCOPE (T-089) — TWO METHODS, MIRRORING THE MODULE'S OWN SPLIT.
// ==================================================================================================
//
// ---- WHY IT IS SEPARATE FROM `IAttendanceScopeResolver`.
//
// Measured in T-088 on the Payroll side: every command handler takes the module's scope resolver, so
// adding self-service dependencies to it makes them construction-time dependencies of every write.
// Twenty-five API tests said so. **Two questions, two objects.**
//
// ---- AND WHY TWO METHODS RATHER THAN ONE.
//
// **Attendance's read scope has three dimensions and its two surfaces use different ones:** records are
// branch-scoped (`OD-ATT-0011`, `AttendanceReadService.cs:105`) and leave is company-only (`:179`, `:233`).
// The module already draws that line with `ResolveAsync` and `ResolveCompanyOnlyAsync`, and this mirrors it
// rather than inventing a third shape.
//
// **The placement hands over the branch; mirroring the split is what makes a records scope impossible to
// construct without one.** One method returning "a scope" would let the records path receive a company-only
// scope and filter on a branch nobody set — a silently unbranched read that looks like a working feature.
//
// ---- WHAT REPLACES THE COMPANY-ACCESS LOOKUP, AND WHY.
//
// `ResolveCoreAsync` answers *which companies may this user ADMINISTER*, from their access grants. **A plain
// employee holding a self permission and no `UserCompanyAccess` row would be refused `company.scope_denied`**
// — making the feature true only for employees an administrator had separately privileged. The self read
// asks *which employee am I*, and the answer comes from `UserEmployeeLink` and the employee's own placement.
public interface IAttendanceSelfServiceScopeResolver
{
  // Company AND branch. For records.
  Task<Result<OwnEmployeeAttendanceScope>> ResolveForOwnRecordsAsync(
    string permissionName, CancellationToken cancellationToken = default);

  // Company only. For leave, matching `ResolveCompanyOnlyAsync`.
  Task<Result<OwnEmployeeAttendanceScope>> ResolveForOwnLeaveAsync(
    string permissionName, CancellationToken cancellationToken = default);
}

public sealed class AttendanceSelfServiceScopeResolver(
  ICurrentTenant currentTenant,
  ICurrentTenantUser currentTenantUser,
  ICurrentUser currentUser,
  IUserEmployeeResolver userEmployees,
  IEmployeePlacementDirectory employeePlacements) : IAttendanceSelfServiceScopeResolver
{
  public Task<Result<OwnEmployeeAttendanceScope>> ResolveForOwnRecordsAsync(
    string permissionName, CancellationToken cancellationToken = default) =>
    ResolveCoreAsync(permissionName, includeBranch: true, cancellationToken);

  public Task<Result<OwnEmployeeAttendanceScope>> ResolveForOwnLeaveAsync(
    string permissionName, CancellationToken cancellationToken = default) =>
    ResolveCoreAsync(permissionName, includeBranch: false, cancellationToken);

  private async Task<Result<OwnEmployeeAttendanceScope>> ResolveCoreAsync(
    string permissionName, bool includeBranch, CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);

    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure<OwnEmployeeAttendanceScope>(AttendanceScopeErrors.InvalidActor);
    }

    if (!currentUser.Permissions.Contains(permissionName, StringComparer.Ordinal))
    {
      return Result.Failure<OwnEmployeeAttendanceScope>(AttendanceScopeErrors.ReadPermissionDenied);
    }

    var employeeId = await userEmployees.ResolveEmployeeIdAsync(tenantUserId, cancellationToken);
    if (employeeId is not { } employee)
    {
      return Result.Failure<OwnEmployeeAttendanceScope>(LeaveErrors.NoLinkedEmployee);
    }

    // A link outliving the employee it names is reachable — `ADR-030` Decision 4 forbids the cross-database
    // foreign key that would prevent it — and collapses into the same refusal deliberately: the caller did
    // nothing wrong, cannot act on the difference, and distinguishing them would disclose that a link points
    // at a record that does not exist. **The cost is that a dangling link is invisible from the wire**, which
    // belongs to whoever owns the link's lifecycle rather than to a read.
    var placement = await employeePlacements.GetPlacementAsync(employee, cancellationToken);
    if (placement is not { } placed)
    {
      return Result.Failure<OwnEmployeeAttendanceScope>(LeaveErrors.NoLinkedEmployee);
    }

    // The company-only path carries the same sentinel branch the module's own resolver uses, and for the
    // same stated reason: the factory refuses an empty branch set, and no company-only query reads it.
    var branches = includeBranch ? new[] { placed.BranchId } : [Guid.Empty];
    var scope = AttendanceReadScope.Create(tenantId, [placed.CompanyId], branches);

    return scope is null
      ? Result.Failure<OwnEmployeeAttendanceScope>(AttendanceScopeErrors.CompanyScopeDenied)
      : Result.Success(new OwnEmployeeAttendanceScope(scope, employee));
  }
}
