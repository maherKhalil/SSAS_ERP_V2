using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees;

// ACTIVATE AND DEACTIVATE (REQ-HR-0002, BRULE-EMP-0002).
//
// TWO COMMANDS, NOT ONE WITH A TARGET STATUS. A caller naming the status it wants would be asserting the
// transition rather than requesting an operation, and the permitted transitions are not symmetric —
// `activate` requires `Inactive`, `deactivate` requires `Active`. Keeping them apart means an unpermitted
// transition is refused by the domain rather than expressible in the contract.
//
// NEITHER IS A LIFECYCLE INVENTION. Employee.Activate and Employee.Deactivate were settled in the domain
// model; these are the thin application shells the approved routes need, mirroring the terminate handler
// exactly — load, check the expected version, ask the aggregate, save.
public sealed record DeactivateEmployeeCommand(
  Guid EmployeeId,
  EmployeeStatusChangeReason ReasonCode,
  byte[] ExpectedRowVersion);

public sealed record ActivateEmployeeCommand(
  Guid EmployeeId,
  EmployeeStatusChangeReason ReasonCode,
  byte[] ExpectedRowVersion);

public sealed class DeactivateEmployeeCommandHandler(
  IEmployeeRepository employees,
  ITenantUnitOfWork unitOfWork,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    DeactivateEmployeeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    return await EmployeeStatusTransition.ExecuteAsync(
      employees,
      unitOfWork,
      currentUser,
      command.EmployeeId,
      command.ExpectedRowVersion,
      (employee, actor) => employee.Deactivate(command.ReasonCode, actor, Guid.NewGuid(), clock.UtcNow),
      cancellationToken);
  }
}

public sealed class ActivateEmployeeCommandHandler(
  IEmployeeRepository employees,
  ITenantUnitOfWork unitOfWork,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    ActivateEmployeeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    return await EmployeeStatusTransition.ExecuteAsync(
      employees,
      unitOfWork,
      currentUser,
      command.EmployeeId,
      command.ExpectedRowVersion,
      (employee, actor) => employee.Activate(command.ReasonCode, actor, Guid.NewGuid(), clock.UtcNow),
      cancellationToken);
  }
}

// The shape both transitions share. Extracted because two handlers differing only in which aggregate method
// they call should not differ anywhere else — an actor check or a version check that drifted between them
// would be a security difference nobody chose.
internal static class EmployeeStatusTransition
{
  internal static async Task<Result> ExecuteAsync(
    IEmployeeRepository employees,
    ITenantUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    Guid employeeId,
    byte[] expectedRowVersion,
    Func<Employee, string, Result> transition,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(EmployeeErrors.InvalidActor);
    }

    // Scoped by the repository's trusted tenant and by the company and branch write boundaries at save.
    var employee = await employees.GetByIdAsync(employeeId, cancellationToken);
    if (employee is null)
    {
      return Result.Failure(EmployeeErrors.NotFound);
    }

    // CHECKED BEFORE THE TRANSITION, so a stale caller is told their version is stale rather than that their
    // transition was invalid — two different problems with two different fixes.
    if (!MatchesExpectedVersion(employee.RowVersion, expectedRowVersion))
    {
      return Result.Failure(EmployeeErrors.ConcurrencyConflict);
    }

    var applied = transition(employee, currentUser.UserId!);
    if (applied.IsFailure)
    {
      return applied;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }

  private static bool MatchesExpectedVersion(byte[] current, byte[]? expected) =>
    expected is { Length: > 0 } && current.AsSpan().SequenceEqual(expected);
}
