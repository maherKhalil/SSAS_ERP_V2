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
// ---- THE DEPARTMENT *IS* ON THE COMMAND, UNLIKE THE THREE OWNERSHIP DIMENSIONS.
//
// It is not ownership and there is no trusted context to read it from: which department a new hire joins is
// a business choice the caller makes, so it is an input that gets VALIDATED rather than a value that gets
// stamped. It is mandatory from FP-007 Phase 3 onward — there is no nullable grace period and no automatic
// fallback to the migration UNASSIGNED department, which exists for legacy remediation alone.
//
// ---- AND SO IS THE POSITION, FROM FP-008 PHASE 3 (BR-HR-0006, OD-POS-001).
//
// Required from day one, with no nullable grace period and no fallback: `OD-POS-001` established that no
// production employees existed and the migration asserted it, so there is no cohort for a default to serve.
// It is validated on exactly the same terms as the department and for the same reasons.
public sealed record CreateEmployeeCommand(
  string EmployeeNumber,
  string FullName,
  DateTimeOffset EmploymentDate,
  string? NationalId,
  Guid DepartmentId,
  Guid PositionId);

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

    // ---- THE DEPARTMENT, RESOLVED INSIDE THE TRUSTED COMPANY AND NOWHERE ELSE.
    //
    // The company comes from the execution context above, never from the command, so this cannot be pointed
    // at another company's department by a caller. Absent and belonging-to-another-company are the same
    // answer deliberately: distinguishing them would make employee creation a probe for the existence of
    // departments the caller has no company scope for.
    //
    // Checked BEFORE the uniqueness probes below, so a request naming an unusable department does not first
    // reveal whether an employee number is taken.
    var department = await ResolveDepartmentAsync(companyId, command.DepartmentId, cancellationToken);
    if (department.IsFailure)
    {
      return Result.Failure<Guid>(department.Error);
    }

    // ---- THE POSITION, ON THE SAME TERMS AND IN THE SAME TRANSACTION (BRULE-POS-0016, DEC-POS-0021).
    //
    // Same company as the employee, `Active` at the moment of assignment, and a position in another company
    // reported ABSENT rather than refused. Read through the caller's context, so the status this validates
    // is the status the write commits against rather than one observed earlier.
    var position = await ValidatePositionAsync(
      employees, companyId, command.PositionId, cancellationToken);
    if (position.IsFailure)
    {
      return Result.Failure<Guid>(position.Error);
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
    // The initial DEPARTMENT assignment rides the same call and the same guarantee: three rows — the
    // employee, its first branch record and its first department record — commit together or none does.
    // FP-008 Phase 3 makes it FOUR rows: the employee, its first branch record, its first department record
    // and its first position record, all in one save.
    var stamped = employee.Value.StampInitialAssignment(
      tenantId, companyId, branch.Value, command.DepartmentId, command.PositionId,
      currentUser.UserId!, Guid.NewGuid(), occurredUtc);
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

  // ---- THE RULES OF §5 AND §8, IN ONE PLACE SO BOTH OPERATIONS ANSWER IDENTICALLY.
  //
  // Creation and department change ask exactly the same question of a destination department, and a
  // divergence between them would be a security-relevant inconsistency rather than a cosmetic one. It is
  // internal so the change handler shares it rather than restating it.
  //
  // NOTHING HERE ASKS ABOUT BRANCHES. A department spans the branches of its company (ADR-026 decision 1),
  // so requiring one to match the employee's branch would invent a rule the approved model does not have.
  internal static async Task<Result> ValidateDepartmentAsync(
    IEmployeeRepository employees,
    Guid companyId,
    Guid departmentId,
    CancellationToken cancellationToken)
  {
    if (departmentId == Guid.Empty)
    {
      return Result.Failure(EmployeeErrors.DepartmentRequired);
    }

    var department = await employees.FindAssignableDepartmentAsync(
      companyId, departmentId, cancellationToken);

    if (department is null)
    {
      return Result.Failure(EmployeeErrors.DepartmentNotFound);
    }

    // An INACTIVE department keeps the employees it already has — FP-007 Phase 3 §16 — but accepts no new
    // ones. Deactivating a department must not silently keep absorbing hires.
    return department.IsActive
      ? Result.Success()
      : Result.Failure(EmployeeErrors.DepartmentInactive);
  }

  // ---- THE SAME RULES FOR THE POSITION, IN ONE PLACE SO BOTH OPERATIONS ANSWER IDENTICALLY.
  //
  // Creation and `ChangePosition` ask exactly the same question of a destination position, and a divergence
  // between them would be a security-relevant inconsistency rather than a cosmetic one. Internal so the
  // change handler shares it rather than restating it — the shape `ValidateDepartmentAsync` established.
  //
  // NOTHING HERE ASKS ABOUT BRANCHES. A position is company-wide and may be held by employees in any branch
  // of its company (`BRULE-POS-0003`), so requiring one to match the employee's branch would invent a rule
  // the approved model does not have.
  internal static async Task<Result> ValidatePositionAsync(
    IEmployeeRepository employees,
    Guid companyId,
    Guid positionId,
    CancellationToken cancellationToken)
  {
    if (positionId == Guid.Empty)
    {
      return Result.Failure(EmployeeErrors.PositionRequired);
    }

    var position = await employees.FindAssignablePositionAsync(
      companyId, positionId, cancellationToken);

    if (position is null)
    {
      return Result.Failure(EmployeeErrors.PositionNotFound);
    }

    // An INACTIVE position keeps the employees who already hold it — `OD-POS-005`, the assignment reading —
    // and accepts no new ones. Retiring a job must not silently keep absorbing hires (`BRULE-POS-0013`).
    return position.IsActive
      ? Result.Success()
      : Result.Failure(EmployeeErrors.PositionInactive);
  }

  private Task<Result> ResolveDepartmentAsync(
    Guid companyId, Guid departmentId, CancellationToken cancellationToken) =>
    ValidateDepartmentAsync(employees, companyId, departmentId, cancellationToken);
}
