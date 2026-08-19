using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees;

// TRANSFER AN EMPLOYEE BETWEEN BRANCHES (REQ-HR-0004, ADR-024).
//
// THE COMMAND CARRIES A DESTINATION, NOT A SOURCE. The source is the employee's current branch, read from
// the record; accepting one from the caller would let a request assert where a record used to be.
public sealed record TransferEmployeeCommand(
  Guid EmployeeId,
  Guid DestinationBranchId,
  EmployeeBranchTransferReason ReasonCode,
  string? ReasonText,
  byte[] ExpectedRowVersion,
  // ADR-024 decision 12. Opting in is deliberate: a recovery is a different operation with different
  // preconditions, and it must be requested rather than fallen into when an ordinary transfer fails.
  bool InactiveSourceRecovery = false);

// ---- THE ORCHESTRATION ADR-024 DECISION 3 REQUIRES.
//
// The handler performs DUAL AUTHORIZATION and only then opens the sanctioned channel. It does not
// re-implement either half: the destination goes through ITenantBranchAccessResolver, and the source is the
// trusted execution context that the branch write boundary re-proves at save time.
//
// EVERYTHING IS RE-ASKED AT SAVE. Opening the channel here proves the authorization existed now; the
// boundary re-validates the whole declaration when the transaction commits, so access revoked in between
// refuses the write. That is why this handler deliberately does no caching of its own.
public sealed class TransferEmployeeCommandHandler(
  IEmployeeRepository employees,
  ITenantBranchAccessResolver branchAccess,
  IBranchTransferScope transferScope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentTenantUser currentTenantUser,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    TransferEmployeeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId ||
      string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(EmployeeErrors.InvalidActor);
    }

    // ---- 1. LOAD. Scoped by the repository to the trusted tenant and the caller's authorized company and
    // branch, so an employee outside that scope is simply not found — never a distinguishable refusal.
    var employee = await employees.GetByIdAsync(command.EmployeeId, cancellationToken);
    if (employee is null)
    {
      return Result.Failure(EmployeeErrors.NotFound);
    }

    // ---- 2. DOMAIN PRECONDITIONS, before anything is authorized. A terminated employee is refused here
    // rather than after a round trip to the access resolver.
    if (employee.Status == EmployeeStatus.Terminated)
    {
      return Result.Failure(EmployeeErrors.TransferAfterTermination);
    }

    if (command.DestinationBranchId == employee.BranchId)
    {
      return Result.Failure(EmployeeErrors.TransferDestinationUnchanged);
    }

    // ---- 3. OPTIMISTIC CONCURRENCY, checked before the authorization round trips so a stale request fails
    // fast — and enforced again by the rowversion token at commit, which is the authoritative check.
    if (!MatchesExpectedVersion(employee.RowVersion, command.ExpectedRowVersion))
    {
      return Result.Failure(EmployeeErrors.ConcurrencyConflict);
    }

    // ---- 4. DESTINATION AUTHORIZATION, THROUGH THE RESOLVER AND NOWHERE ELSE.
    //
    // It intersects with ACTIVE branches, so a deactivated destination is refused, and its generic error is
    // returned unchanged so a destination identifier cannot be probed for existence through this path.
    var destination = await branchAccess.AuthorizeBranchAsync(
      tenantId, tenantUserId, command.DestinationBranchId, cancellationToken);
    if (destination.IsFailure)
    {
      return destination;
    }

    // ---- 5. DECLARE THE ONE TRANSITION.
    //
    // The source is the employee's CURRENT branch, read from the record. The write boundary independently
    // requires that to equal the trusted execution branch for an ordinary transfer, which is what joins the
    // caller's proven scope to the record's actual location.
    var declaration = BranchTransferDeclaration.Create(
      employee,
      employee.BranchId,
      command.DestinationBranchId,
      command.InactiveSourceRecovery
        ? BranchTransferMode.InactiveSourceRecovery
        : BranchTransferMode.CurrentBranch);
    if (declaration.IsFailure)
    {
      return Result.Failure(declaration.Error);
    }

    var opened = transferScope.Begin(declaration.Value);
    if (opened.IsFailure)
    {
      return Result.Failure(opened.Error);
    }

    // The declaration lives exactly as long as this operation, on the success and the failure path alike.
    using var transfer = opened.Value;

    // ---- 6. THE DOMAIN MOVES THE EMPLOYEE AND RECORDS THAT IT MOVED, in one step. The appended history is
    // produced by the aggregate rather than assembled here, so a branch change without a matching record is
    // not expressible.
    var occurredUtc = clock.UtcNow;
    var moved = employee.Transfer(
      command.DestinationBranchId, command.ReasonCode, command.ReasonText,
      currentUser.UserId!, Guid.NewGuid(), occurredUtc);
    if (moved.IsFailure)
    {
      return Result.Failure(moved.Error);
    }

    await employees.AppendBranchAssignmentAsync(moved.Value, cancellationToken);

    // ---- 7. ONE SAVE. The BranchId change and the appended record commit together or not at all, and the
    // boundary re-validates the whole declaration against live state as they do.
    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }

  private static bool MatchesExpectedVersion(byte[] current, byte[]? expected) =>
    expected is { Length: > 0 } && current.AsSpan().SequenceEqual(expected);
}
