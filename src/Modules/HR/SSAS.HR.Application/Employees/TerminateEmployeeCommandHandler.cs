using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees;

// TERMINATE AN EMPLOYEE (REQ-HR-0003, BR-HR-0003, BR-HR-0004).
//
// TERMINATION IS NOT DELETION. The record, its identifiers and its whole branch history are retained, so
// reporting over periods before termination stays correct and the employee number stays reserved within the
// company.
public sealed record TerminateEmployeeCommand(
  Guid EmployeeId,
  DateTimeOffset TerminationDate,
  EmployeeStatusChangeReason ReasonCode,
  byte[] ExpectedRowVersion);

//
// ==================================================================================================
// AND IT CLOSES THE ACCOUNT (T-091, REQ-SS-0007) — THE SECOND OF TWO GUARDS.
// ==================================================================================================
//
// T-090 put the FIRST guard at `IUserEmployeeResolver`: a terminated employee stops resolving, so
// self-service closes per request against live state. **That guard is the only one that can close an access
// token already issued**, because permissions travel in the token's claims — bounded at fifteen minutes by
// `JwtOptionsValidator`, and not zero. This one closes AUTHENTICATION, which is what `REQ-SS-0007` asks for
// literally, and it cannot substitute for T-090's. **Neither guard is on the LINK** (`REQ-SS-0006`).
//
// ---- SYNCHRONOUS, NOT AN EVENT, AND THE REASON IS THE FAILURE MODE.
//
// The domain-event road exists and already carries Platform's localization cache. **It has no outbox:**
// dispatch runs after the commit, is not persisted and is not retried. A failing consumer would leave a
// terminated employee with a live account and an operator who reasonably believes nothing happened.
// Called here, the failure lands here.
//
// ==================================================================================================
// THE ORDERING, AND THE TRADE IT ACCEPTS (RULED 2026-08-28).
// ==================================================================================================
//
// Two databases, and `ADR-017` means no transaction spans them. Both naive orders leave a half-state, and
// **they are not comparable, because TERMINATION IS TERMINAL.** Every operation on `Employee` refuses once
// `Terminated`; `Deactivate` requires `Active`; `Activate` requires `Inactive`; `ApplyTransition` has no
// path out. **Nothing in the product can un-terminate an employee.**
//
// So *commit termination, then deactivate* fails OPEN into a state nothing can undo — it recreates
// `AC-SS-0012`'s exposure inside the change that closes it. That order is refused on that ground alone.
//
// **What ships instead holds the tenant transaction OPEN across the Platform write:**
//
//   BeginTransaction -> Terminate -> SaveChanges (uncommitted) -> deactivate the account -> Commit
//
// The COMMON failures — Platform unreachable, a concurrency conflict, a stale row version — all happen
// before the commit, so the transaction rolls back and **nothing happened at all.** The operator retries
// the whole command.
//
// ---- THE COST, WHICH IS REAL AND IS WHY THIS PARAGRAPH EXISTS.
//
// **An open tenant transaction now spans a cross-database call**, so a slow or hanging Platform write holds
// a lock on this `Employee` row for its duration. Bounded by the command timeout; one row; not a hot row;
// and the row belongs to the employee being terminated, so nobody else is waiting on it in practice.
//
// **A future reader who finds a remote call inside a transaction and no explanation will assume it is a
// mistake.** It is a ruling, and the alternative was an unrepairable half-state.
//
// ---- THE ONE HALF-STATE THAT REMAINS, AND WHY IT IS NOT COMPENSATED AUTOMATICALLY.
//
// If the account closes and the tenant commit then fails, the employee is unchanged and the account is not.
// **A best-effort reactivate that itself failed would put us here with more code; one that silently
// succeeded would hide that a write failed.** So it is REPORTED — `EmployeeErrors.TerminationIncomplete`
// names the state and the repair — and `Platform.Users.Reactivate` got transport in this same task so the
// repair exists.
public sealed class TerminateEmployeeCommandHandler(
  IEmployeeRepository employees,
  ITenantUnitOfWork unitOfWork,
  ITenantUserDeactivator tenantUsers,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    TerminateEmployeeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(EmployeeErrors.InvalidActor);
    }

    var employee = await employees.GetByIdAsync(command.EmployeeId, cancellationToken);
    if (employee is null)
    {
      return Result.Failure(EmployeeErrors.NotFound);
    }

    if (!MatchesExpectedVersion(employee.RowVersion, command.ExpectedRowVersion))
    {
      return Result.Failure(EmployeeErrors.ConcurrencyConflict);
    }

    // Opened BEFORE the domain change so the whole operation is one rollback unit. Disposing without a
    // commit rolls back, which is what every early return below relies on.
    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    var terminated = employee.Terminate(
      command.TerminationDate, command.ReasonCode, currentUser.UserId!, Guid.NewGuid(), clock.UtcNow);
    if (terminated.IsFailure)
    {
      return terminated;
    }

    // Written, NOT committed. `EfUnitOfWork` dispatches domain events only when no transaction is open, so
    // `EmployeeTerminated` is not announced here — and on the rollback path it is never announced at all.
    // An event reporting a termination that did not happen would be worse than either half-state.
    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      return Result.Failure(saved.Error);
    }

    // The Platform write, which commits on its own. A failure here leaves the termination uncommitted, so
    // the rollback on dispose returns the system to exactly where it started.
    var deactivated = await tenantUsers.DeactivateForEmployeeAsync(employee.Id, cancellationToken);
    if (deactivated.IsFailure)
    {
      return deactivated;
    }

    // ---- THE ONLY POINT AT WHICH A HALF-STATE BECOMES REACHABLE.
    //
    // The account is already closed. If this throws, the termination rolls back and the two sides disagree.
    //
    // **Caught broadly on purpose**, cancellation included: a cancelled commit leaves exactly the same
    // half-state as a failed one, and letting it propagate would surface as a generic failure that says
    // nothing about what needs repairing. The narrow catch would be the tidier code and the worse answer.
    try
    {
      await transaction.CommitAsync(cancellationToken);
    }
    catch (Exception)
    {
      return Result.Failure(EmployeeErrors.TerminationIncomplete);
    }

    return Result.Success();
  }

  private static bool MatchesExpectedVersion(byte[] current, byte[]? expected) =>
    expected is { Length: > 0 } && current.AsSpan().SequenceEqual(expected);
}
