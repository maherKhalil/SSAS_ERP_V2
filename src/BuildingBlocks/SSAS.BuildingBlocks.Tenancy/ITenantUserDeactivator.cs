using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Tenancy;

// ==================================================================================================
// CLOSING THE ACCOUNT WHEN THE EMPLOYMENT ENDS (T-091, REQ-SS-0007) — THE SECOND OF TWO GUARDS.
// ==================================================================================================
//
// ---- THE FIRST GUARD IS T-090's, AND THIS ONE CANNOT REPLACE IT.
//
// `IUserEmployeeResolver` refuses to resolve a terminated employee, so **self-service closes at once, per
// request, against live state.** This closes AUTHENTICATION, which is what `REQ-SS-0007` literally asks
// for — and it **cannot** close an access token that has already been issued, because permissions travel
// in the token's claims rather than being resolved per request (`CurrentUser.Permissions`).
//
// **That residual window is bounded at fifteen minutes by `JwtOptionsValidator` and it is not zero.**
// T-090's guard is the one that closes it. Neither guard is redundant and neither is on the LINK.
//
// ---- IT IS CALLED SYNCHRONOUSLY, AND THAT WAS THE WHOLE ARGUMENT.
//
// The alternative was an `EmployeeTerminated` domain-event consumer. The road exists and already carries
// Platform's localization cache — but **it has no outbox**: dispatch runs after the commit, is not
// persisted and is not retried, so a failing consumer leaves a terminated employee with a live account and
// an operator who reasonably believes nothing happened.
//
// **Called from the handler, the failure lands in the handler**, and termination refuses rather than
// half-happens. That is the entire reason for this shape.
//
// ---- WHY IT LIVES HERE.
//
// Same seam and same reasoning as `IUserEmployeeResolver` and `IEmploymentStandingDirectory`: HR cannot
// reference Platform, Platform cannot reference HR, and `BuildingBlocks.Tenancy` is the edge that already
// exists between them. This one points module -> Platform, like `IUserEmployeeResolver` and unlike
// `IEmploymentStandingDirectory`.
public interface ITenantUserDeactivator
{
  // ---- TAKES THE EMPLOYEE, NOT THE TENANT USER, AND THAT IS DELIBERATE.
  //
  // The caller is a module handler that has just terminated an employee. **It does not know whether that
  // employee has an account at all**, and making it find out would put the `UserEmployeeLink` lookup — a
  // Platform table — inside a module. Platform owns the link, so Platform resolves it.
  //
  // ---- AN EMPLOYEE WITH NO ACCOUNT IS A SUCCESS, NOT A FAILURE.
  //
  // Most employees have no tenant user, and **today every employee does**, because nothing in production
  // writes a `UserEmployeeLink` yet. A termination must not fail because the person never had a login.
  //
  // Already-deactivated is also a success: the operation is idempotent, so a retry after a partial failure
  // does not refuse on the half it already completed.
  //
  // ---- A FAILURE IS A REAL FAILURE, AND THE CALLER MUST NOT SWALLOW IT.
  //
  // Returned as a `Result` rather than thrown so the handler can refuse its own operation with the error
  // the caller sees. A `bool` would let a caller ignore it with no diagnostic left behind.
  Task<Result> DeactivateForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
}
