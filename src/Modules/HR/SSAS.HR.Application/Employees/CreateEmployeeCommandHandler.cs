using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees;

// CREATE AN EMPLOYEE (REQ-HR-0001).
//
// THE COMMAND CARRIES NO OWNERSHIP. There is no TenantId, no CompanyId and no BranchId: all three come from
// the trusted execution context, and a caller-supplied one would be confirmed rather than trusted anyway.
// Leaving them off the command means the question never reaches the boundary.
public sealed record CreateEmployeeCommand(
  string EmployeeNumber,
  string FullName,
  DateTimeOffset EmploymentDate,
  string? NationalId);

public sealed class CreateEmployeeCommandHandler(
  IEmployeeRepository employees,
  ITenantUnitOfWork unitOfWork,
  ICurrentBranchResolver currentBranch,
  ICurrentTenant currentTenant,
  ICurrentCompany currentCompany,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<Guid>> HandleAsync(
    CreateEmployeeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    // The COMPANY must be established before a company-owned record can be created.
    if (currentTenant.TenantId is not { } tenantId ||
      currentCompany.CompanyId is not { } companyId ||
      string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure<Guid>(EmployeeErrors.InvalidActor);
    }

    // ---- THE BRANCH IS READ FROM THE SAME SOURCE THE BOUNDARY STAMPS FROM.
    //
    // The Employee itself would be stamped without this, but the INITIAL ASSIGNMENT record has to name the
    // branch too, and it is not branch-owned so nothing stamps it. Reading the one authoritative answer here
    // is what keeps the history and the record it describes from disagreeing — and the boundary still
    // CONFIRMS the Employee's branch against its own resolution, so this value is checked, not trusted.
    var branch = await currentBranch.ResolveCurrentBranchAsync(cancellationToken);
    if (branch.IsFailure)
    {
      return Result.Failure<Guid>(branch.Error);
    }

    var employeeNumber = EmployeeNumber.Create(command.EmployeeNumber);
    if (employeeNumber.IsFailure)
    {
      return Result.Failure<Guid>(employeeNumber.Error);
    }

    var fullName = EmployeeFullName.Create(command.FullName);
    if (fullName.IsFailure)
    {
      return Result.Failure<Guid>(fullName.Error);
    }

    NationalId? nationalId = null;
    if (!string.IsNullOrWhiteSpace(command.NationalId))
    {
      var parsed = NationalId.Create(command.NationalId);
      if (parsed.IsFailure)
      {
        return Result.Failure<Guid>(parsed.Error);
      }

      nationalId = parsed.Value;
    }

    // ---- UNIQUENESS IS TESTED HERE AND ENFORCED BY THE DATABASE.
    //
    // The per-company unique indexes are authoritative under concurrent creation; these checks turn the
    // common case into a named conflict instead of a raw persistence failure. They are an optimisation of
    // the error message, not the rule.
    if (await employees.EmployeeNumberExistsAsync(
      companyId, employeeNumber.Value.NormalizedValue, cancellationToken))
    {
      return Result.Failure<Guid>(EmployeeErrors.NumberConflict);
    }

    if (nationalId is not null && await employees.NationalIdExistsAsync(
      companyId, nationalId.NormalizedValue, cancellationToken))
    {
      return Result.Failure<Guid>(EmployeeErrors.NationalIdConflict);
    }

    var occurredUtc = clock.UtcNow;
    var employee = Employee.Create(
      employeeNumber.Value, fullName.Value, nationalId, command.EmploymentDate,
      currentUser.UserId!, Guid.NewGuid(), occurredUtc);
    if (employee.IsFailure)
    {
      return Result.Failure<Guid>(employee.Error);
    }

    // ---- THE INITIAL BRANCH ASSIGNMENT IS PRODUCED BY THE AGGREGATE, NOT ASSEMBLED HERE.
    //
    // An Employee with no branch history is a defect, so the aggregate is what creates the record and this
    // handler cannot forget to. Both land in the SAME unit of work below: they commit together or neither
    // does (AC-EMP-0005).
    var stamped = employee.Value.StampInitialAssignment(
      tenantId, companyId, branch.Value, currentUser.UserId!, Guid.NewGuid(), occurredUtc);
    if (stamped.IsFailure)
    {
      return Result.Failure<Guid>(stamped.Error);
    }

    await employees.AddAsync(employee.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure
      ? Result.Failure<Guid>(saved.Error)
      : Result.Success(employee.Value.Id);
  }
}
