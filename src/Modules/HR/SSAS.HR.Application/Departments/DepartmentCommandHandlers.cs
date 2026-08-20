using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Application.Departments;

// CREATE A DEPARTMENT (REQ-HR-0100).
//
// THE COMMAND CARRIES NO TENANT AND NO STATUS. Both come from the trusted execution context or from the
// aggregate, and a caller-supplied one would be confirmed rather than trusted anyway. Leaving them off the
// command means the question never reaches the boundary.
//
// It DOES carry CompanyId, unlike `CreateEmployeeCommand`. That is not a relaxation: the value is proven
// against live company access before anything is written, and carrying it explicitly lets one caller create
// departments in any company they are authorized for without switching the ambient selection. The
// persistence boundary still stamps ownership from its own resolution.
public sealed record CreateDepartmentCommand(
  Guid CompanyId,
  string Code,
  string Name,
  Guid? ParentDepartmentId);

public sealed class CreateDepartmentCommandHandler(
  IDepartmentRepository departments,
  IDepartmentScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<Guid>> HandleAsync(
    CreateDepartmentCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (currentTenant.TenantId is not { } tenantId || string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure<Guid>(DepartmentErrors.InvalidActor);
    }

    // FUNCTIONAL PERMISSION AND COMPANY SCOPE, both, and independently (ADR-025 decision 8).
    var authorized = await scope.AuthorizeAsync(
      HrPermissionNames.CreateDepartments, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var code = DepartmentCode.Create(command.Code);
    if (code.IsFailure)
    {
      return Result.Failure<Guid>(code.Error);
    }

    var name = DepartmentName.Create(command.Name);
    if (name.IsFailure)
    {
      return Result.Failure<Guid>(name.Error);
    }

    // ---- THE PARENT, IF ONE WAS NAMED.
    //
    // A newly created department has no descendants, so it cannot participate in a cycle and no ancestry
    // walk is needed. Only the three checks the parent itself must satisfy apply.
    if (command.ParentDepartmentId is { } parentId)
    {
      var parent = await departments.GetByIdAsync(parentId, cancellationToken);
      if (parent is null)
      {
        return Result.Failure<Guid>(DepartmentErrors.ParentNotFound);
      }

      if (parent.TenantId != tenantId || parent.CompanyId != command.CompanyId)
      {
        return Result.Failure<Guid>(DepartmentErrors.ParentInDifferentCompany);
      }

      if (parent.Status != DepartmentStatus.Active)
      {
        return Result.Failure<Guid>(DepartmentErrors.ParentInactive);
      }
    }

    // ---- UNIQUENESS IS TESTED HERE AND ENFORCED BY THE DATABASE.
    //
    // The per-company unique index is authoritative under concurrent creation; this check turns the common
    // case into a named conflict instead of a raw persistence failure. It is an optimisation of the error
    // message, not the rule.
    if (await departments.CodeExistsAsync(
      command.CompanyId, code.Value.NormalizedValue, cancellationToken))
    {
      return Result.Failure<Guid>(DepartmentErrors.CodeConflict);
    }

    var occurredUtc = clock.UtcNow;
    var department = Department.Create(
      code.Value, name.Value, command.ParentDepartmentId, currentUser.UserId!, Guid.NewGuid(), occurredUtc);
    if (department.IsFailure)
    {
      return Result.Failure<Guid>(department.Error);
    }

    department.Value.StampCreated(tenantId, command.CompanyId, Guid.NewGuid(), occurredUtc);

    await departments.AddAsync(department.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure
      ? Result.Failure<Guid>(saved.Error)
      : Result.Success(department.Value.Id);
  }
}

// ---- THE ORDINARY EDIT, AND NOTHING ELSE (REQ-HR-0100).
//
// Code and Name only. Parent, Status and Manager are absent from this command BY CONSTRUCTION rather than
// by validation: there is no field for them, so a caller cannot express the change and a reviewer does not
// have to notice that they did. Each has its own explicit operation.
public sealed record UpdateDepartmentCommand(
  Guid DepartmentId,
  string Code,
  string Name,
  byte[] RowVersion);

public sealed class UpdateDepartmentCommandHandler(
  IDepartmentRepository departments,
  IDepartmentScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    UpdateDepartmentCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var loaded = await DepartmentWriteContext.LoadAsync(
      departments, scope, currentTenant, command.DepartmentId,
      HrPermissionNames.UpdateDepartments, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      return Result.Failure(loaded.Error);
    }

    var department = loaded.Value;

    var code = DepartmentCode.Create(command.Code);
    if (code.IsFailure)
    {
      return Result.Failure(code.Error);
    }

    var name = DepartmentName.Create(command.Name);
    if (name.IsFailure)
    {
      return Result.Failure(name.Error);
    }

    // Excluding this department, so keeping its own code is not a conflict with itself.
    if (await departments.CodeExistsForAnotherAsync(
      department.CompanyId, code.Value.NormalizedValue, department.Id, cancellationToken))
    {
      return Result.Failure(DepartmentErrors.CodeConflict);
    }

    var updated = department.UpdateDescription(
      code.Value, name.Value, Guid.NewGuid(), clock.UtcNow);
    if (updated.IsFailure)
    {
      return updated;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }
}

// ================================================================================================
// THE SHARED WRITE PRELUDE
// ================================================================================================
//
// Every department write asks the same four questions in the same order, and getting the ORDER wrong is how
// an authorization surface leaks: checking the row version before the permission would tell an
// unauthorized caller whether their token was current, and checking company scope before the functional
// permission would tell them the department exists.
//
// Extracted so the order is written once and every handler inherits it, rather than being retyped six times
// and drifting on the fifth.
internal static class DepartmentWriteContext
{
  public static async Task<Result<Department>> LoadAsync(
    IDepartmentRepository departments,
    IDepartmentScopeResolver scope,
    ICurrentTenant currentTenant,
    Guid departmentId,
    string permission,
    byte[]? expectedRowVersion,
    CancellationToken cancellationToken)
  {
    if (currentTenant.TenantId is not { } tenantId)
    {
      return Result.Failure<Department>(DepartmentErrors.InvalidActor);
    }

    // 1. THE FUNCTIONAL PERMISSION, before anything is loaded. A caller without it learns nothing at all.
    var permitted = scope.RequirePermission(permission);
    if (permitted.IsFailure)
    {
      return Result.Failure<Department>(permitted.Error);
    }

    // 2. The department. NotFound is also the answer for another tenant's row, because the tenant filter
    //    makes it unreachable rather than forbidden.
    var department = await departments.GetByIdAsync(departmentId, cancellationToken);
    if (department is null || department.TenantId != tenantId)
    {
      return Result.Failure<Department>(DepartmentErrors.NotFound);
    }

    // 3. COMPANY SCOPE, re-asked live against the department's OWN company — not against a company the
    //    caller named, which they never get to choose here.
    var authorized = await scope.AuthorizeAsync(permission, department.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      // The same NotFound a nonexistent department gives. A distinct refusal would confirm that a
      // department exists in a company the caller may not see (BR-PLT-0002).
      return Result.Failure<Department>(DepartmentErrors.NotFound);
    }

    // 4. THE CONCURRENCY TOKEN, last. Compared here so a stale caller is refused before the aggregate is
    //    mutated, and compared again by the database on save — this is the friendly error, not the rule.
    if (expectedRowVersion is not null &&
      !department.RowVersion.AsSpan().SequenceEqual(expectedRowVersion))
    {
      return Result.Failure<Department>(DepartmentErrors.ConcurrencyConflict);
    }

    return Result.Success(department);
  }
}
