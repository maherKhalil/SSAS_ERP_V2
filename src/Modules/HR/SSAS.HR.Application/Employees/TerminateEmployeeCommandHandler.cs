using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
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

public sealed class TerminateEmployeeCommandHandler(
  IEmployeeRepository employees,
  ITenantUnitOfWork unitOfWork,
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

    var terminated = employee.Terminate(
      command.TerminationDate, command.ReasonCode, currentUser.UserId!, Guid.NewGuid(), clock.UtcNow);
    if (terminated.IsFailure)
    {
      return terminated;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }

  private static bool MatchesExpectedVersion(byte[] current, byte[]? expected) =>
    expected is { Length: > 0 } && current.AsSpan().SequenceEqual(expected);
}
