using SSAS.Attendance.Application.Permissions;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Domain.Leave;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Contracts.Employment;

namespace SSAS.Attendance.Application.Approval;

// ================================================================================================
// WHO MAY DECIDE A LEAVE REQUEST (OD-ATT-0007) — THE POLICY, IN THE MODULE THAT OWNS IT.
// ================================================================================================
//
// The ruling: **department-manager approval, self-approval barred, parent-chain escalation for unmanaged
// and self-referential cases, permission-holder fallback at the root.**
//
// ---- THE DIVISION OF LABOUR, WHICH IS WHY THIS FILE IS SEPARATE FROM THE CONTRACT.
//
// `IEmployeeApproverDirectory` walks HR's department tree and applies HR's facts: a department with no
// manager contributes nothing, a terminated manager is excluded. Both states are reachable —
// `ManagerNotAssigned` and `ManagerTerminated` are modelled errors in HR today.
//
// **It deliberately does NOT apply the self-approval bar.** That is `BR-ATT-0007`, Attendance's rule, and it
// is applied HERE. If HR filtered the requester out, the rule would live in two modules and could drift,
// with the module that owns it not being the module enforcing it — the same split `IEmployeeRoster` drew
// when it refused to filter by "employed during the period".
//
// ---- WHY THE SELF-APPROVAL BAR IS APPLIED TWICE, AND THAT IS NOT REDUNDANCY.
//
// Here it decides ROUTING: a department manager requesting their own leave is skipped so the chain escalates
// to their parent, which is what makes the ruling's "self-referential" case work at all.
//
// In `LeaveRequest.Approve` it is an INVARIANT: the aggregate refuses regardless of how the approver was
// chosen. `OD-PAY-0009` drew the same line — a permission check answers "may this person approve requests",
// and only the aggregate can answer "may this person approve THIS request".
//
// Removing either one leaves a hole. The router alone could be bypassed by a caller supplying an approver;
// the aggregate alone would refuse instead of escalating, and a manager's own leave would be undecidable.
public sealed record ApprovalRoute(Guid ApproverEmployeeId, Guid DepartmentId, bool UsedRootFallback);

public interface ILeaveApprovalRouter
{
  Task<Result<ApprovalRoute>> ResolveApproverAsync(
    Guid companyId, Guid requesterEmployeeId, CancellationToken cancellationToken = default);
}

public sealed class LeaveApprovalRouter(
  IEmployeeApproverDirectory directory,
  IAttendanceScopeResolver scope) : ILeaveApprovalRouter
{
  public async Task<Result<ApprovalRoute>> ResolveApproverAsync(
    Guid companyId, Guid requesterEmployeeId, CancellationToken cancellationToken = default)
  {
    // The ordinary approval permission is required before the chain is even walked. A caller who cannot
    // approve anything should not cause a department-tree read — the cheap refusal before the expensive one,
    // the same ordering `PayrollScopeResolver.AuthorizeAsync` uses.
    var permitted = scope.RequirePermission(AttendancePermissionNames.ApproveLeave);
    if (permitted.IsFailure)
    {
      return Result.Failure<ApprovalRoute>(permitted.Error);
    }

    var chain = await directory.GetApproverChainAsync(companyId, requesterEmployeeId, cancellationToken);

    // ---- NEAREST ELIGIBLE, WITH THE REQUESTER SKIPPED RATHER THAN REFUSED.
    //
    // The chain arrives nearest-first. Skipping the requester is what implements "parent-chain escalation
    // for self-referential cases": a department manager's own leave routes to THEIR manager, which is the
    // answer anyone would give if asked, and is not derivable from HR's data alone because HR does not know
    // the bar exists.
    foreach (var candidate in chain)
    {
      if (candidate.EmployeeId != requesterEmployeeId)
      {
        return Result.Success(new ApprovalRoute(candidate.EmployeeId, candidate.DepartmentId, UsedRootFallback: false));
      }
    }

    // ================================================================================================
    // THE ROOT FALLBACK (OD-ATT-0007) — REACHED IN THREE DIFFERENT WAYS, ALL OF THEM REAL.
    // ================================================================================================
    //
    //   1. Every department in the chain is unmanaged (`ManagerNotAssigned` is modelled).
    //   2. Every manager in the chain is the requester (a one-department company run by its manager).
    //   3. The chain reached the top of the organisation and nobody above the requester exists.
    //
    // In all three the request would otherwise be **undecidable by anyone**, which is why the ruling
    // provided a fallback rather than leaving the states to fail.
    //
    // `ApproveLeaveAtRoot` is strictly wider authority than `ApproveLeave`: it decides for employees whose
    // management chain does not reach the holder at all. So it is a separate grant, and holding ordinary
    // approval does not confer it.
    if (!scope.HasPermission(AttendancePermissionNames.ApproveLeaveAtRoot))
    {
      return Result.Failure<ApprovalRoute>(LeaveErrors.NoApproverInChain);
    }

    // ---- THE FALLBACK APPROVER HAS NO EMPLOYEE IDENTITY, AND THAT IS NOT A GAP.
    //
    // The holder is authenticated as a USER, and **no identity-to-employee mapping exists** (`OD-ATT-0013`,
    // verified: `Employee` carries no user identifier). So the route carries `Guid.Empty` as the approver
    // employee, and `LeaveRequest.Approve` refuses `Guid.Empty` outright.
    //
    // Which means the fallback path does NOT go through `Approve`'s employee-identified route: the handler
    // uses the root-decision path, which records the acting USER in `DecidedBy` and leaves
    // `ApproverEmployeeId` null. The distinction is visible in the stored request rather than lost.
    //
    // **This is the third consecutive feature to be shaped by that missing mapping.** It is recorded here
    // because the shape of this branch is otherwise inexplicable.
    return Result.Success(new ApprovalRoute(Guid.Empty, Guid.Empty, UsedRootFallback: true));
  }
}
