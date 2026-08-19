using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees;

// UPDATE AN EMPLOYEE PROFILE (REQ-HR-0002).
//
// THE COMMAND CARRIES ONLY WHAT MAY CHANGE. There is no TenantId, CompanyId, BranchId, EmployeeNumber or
// Status on it — omission at the contract level, so an ordinary update cannot express a relocation, a
// company move or a lifecycle change even by accident. The shared write boundaries refuse those anyway,
// which is the second of the two protections.
public sealed record UpdateEmployeeProfileCommand(
  Guid EmployeeId,
  string FullName,
  string? NationalId,
  byte[] ExpectedRowVersion);

public sealed class UpdateEmployeeProfileCommandHandler(
  IEmployeeRepository employees,
  ITenantUnitOfWork unitOfWork,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    UpdateEmployeeProfileCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(EmployeeErrors.InvalidActor);
    }

    var fullName = EmployeeFullName.Create(command.FullName);
    if (fullName.IsFailure)
    {
      return Result.Failure(fullName.Error);
    }

    NationalId? nationalId = null;
    if (!string.IsNullOrWhiteSpace(command.NationalId))
    {
      var parsed = NationalId.Create(command.NationalId);
      if (parsed.IsFailure)
      {
        return Result.Failure(parsed.Error);
      }

      nationalId = parsed.Value;
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

    // Uniqueness of a CHANGED national identifier is enforced by the filtered per-company index; it is not
    // pre-checked here because the value may legitimately be unchanged, and the index is authoritative.
    var updated = employee.UpdateProfile(fullName.Value, nationalId, Guid.NewGuid(), clock.UtcNow);
    if (updated.IsFailure)
    {
      return updated;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }

  private static bool MatchesExpectedVersion(byte[] current, byte[]? expected) =>
    expected is { Length: > 0 } && current.AsSpan().SequenceEqual(expected);
}
