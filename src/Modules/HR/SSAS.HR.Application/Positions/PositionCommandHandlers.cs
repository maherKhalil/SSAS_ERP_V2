using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Application.Positions.Reads;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Application.Positions;

// CREATE A POSITION (FR-POS-0201).
//
// THE COMMAND CARRIES NO TENANT AND NO STATUS. Both come from the trusted execution context or from the
// aggregate, and a caller-supplied one would be confirmed rather than trusted anyway. Leaving them off the
// command means the question never reaches the boundary (`BRULE-POS-0001`).
//
// It DOES carry CompanyId, exactly as `CreateDepartmentCommand` does: the value is proven against live
// company access before anything is written, and carrying it explicitly lets one caller create positions in
// any company they are authorized for without switching the ambient selection.
//
// IT CARRIES NO DEPARTMENT AND NO BRANCH, and there is no field for either. `OD-POS-003` ruled Position
// independent of Department, and `DEC-POS-0001` ruled it not branch-owned — so both absences are structural
// rather than validated.
public sealed record CreatePositionCommand(
  Guid CompanyId,
  string Code,
  string Title,
  Guid? JobGradeId);

public sealed class CreatePositionCommandHandler(
  IPositionRepository positions,
  IJobGradeRepository jobGrades,
  IPositionScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<Guid>> HandleAsync(
    CreatePositionCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (currentTenant.TenantId is not { } tenantId || string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure<Guid>(PositionErrors.InvalidActor);
    }

    // FUNCTIONAL PERMISSION AND COMPANY SCOPE, both, and independently (ADR-025 decision 8).
    var authorized = await scope.AuthorizeAsync(
      HrPermissionNames.CreatePositions, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var code = PositionCode.Create(command.Code);
    if (code.IsFailure)
    {
      return Result.Failure<Guid>(code.Error);
    }

    var title = PositionTitle.Create(command.Title);
    if (title.IsFailure)
    {
      return Result.Failure<Guid>(title.Error);
    }

    // ---- THE GRADE, IF ONE WAS NAMED (BRULE-POS-0009, BRULE-POS-0011).
    var grade = await PositionGradeReference.ValidateJobGradeAsync(
      jobGrades, command.JobGradeId, tenantId, command.CompanyId, cancellationToken);
    if (grade.IsFailure)
    {
      return Result.Failure<Guid>(grade.Error);
    }

    // ---- UNIQUENESS IS TESTED HERE AND ENFORCED BY THE DATABASE (BRULE-POS-0004).
    //
    // The per-company unique index is authoritative under concurrent creation; this check turns the common
    // case into a named conflict instead of a raw persistence failure. It is an optimisation of the error
    // message, not the rule.
    if (await positions.CodeExistsAsync(
      command.CompanyId, code.Value.NormalizedValue, cancellationToken))
    {
      return Result.Failure<Guid>(PositionErrors.PositionCodeConflict);
    }

    var occurredUtc = clock.UtcNow;
    var position = Position.Create(
      code.Value, title.Value, command.JobGradeId, currentUser.UserId!, Guid.NewGuid(), occurredUtc);
    if (position.IsFailure)
    {
      return Result.Failure<Guid>(position.Error);
    }

    position.Value.StampCreated(tenantId, command.CompanyId, Guid.NewGuid(), occurredUtc);

    await positions.AddAsync(position.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure
      ? Result.Failure<Guid>(saved.Error)
      : Result.Success(position.Value.Id);
  }
}

// ---- THE ORDINARY EDIT, PLUS THE RE-GRADE, AND NOTHING ELSE (FR-POS-0204).
//
// Code, Title and the grade reference. Ownership and Status are absent from this command BY CONSTRUCTION
// rather than by validation: there is no field for them, so a caller cannot express the change and a
// reviewer does not have to notice that they did. Status has its own operation.
//
// The re-grade travels with the edit because `DEC-POS-0018` grouped it under `HR.Positions.Update`
// deliberately — see the permission constant for why.
public sealed record UpdatePositionCommand(
  Guid PositionId,
  string Code,
  string Title,
  Guid? JobGradeId,
  byte[] RowVersion);

public sealed class UpdatePositionCommandHandler(
  IPositionRepository positions,
  IJobGradeRepository jobGrades,
  IPositionScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    UpdatePositionCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var loaded = await PositionWriteContext.LoadAsync(
      positions, scope, currentTenant, command.PositionId,
      HrPermissionNames.UpdatePositions, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      return Result.Failure(loaded.Error);
    }

    var position = loaded.Value;

    var code = PositionCode.Create(command.Code);
    if (code.IsFailure)
    {
      return Result.Failure(code.Error);
    }

    var title = PositionTitle.Create(command.Title);
    if (title.IsFailure)
    {
      return Result.Failure(title.Error);
    }

    // The grade is re-validated on every update, including when it is unchanged. A grade that was Active
    // when it was first assigned may have been deactivated since, and an update that silently preserved a
    // now-invalid reference would let the aggregate drift past `BRULE-POS-0009` without any operation
    // having broken it.
    var grade = await PositionGradeReference.ValidateJobGradeAsync(
      jobGrades, command.JobGradeId, position.TenantId, position.CompanyId, cancellationToken);
    if (grade.IsFailure)
    {
      return Result.Failure(grade.Error);
    }

    // Excluding this position, so keeping its own code is not a conflict with itself.
    if (await positions.CodeExistsForAnotherAsync(
      position.CompanyId, code.Value.NormalizedValue, position.Id, cancellationToken))
    {
      return Result.Failure(PositionErrors.PositionCodeConflict);
    }

    var updated = position.UpdateDescription(
      code.Value, title.Value, command.JobGradeId, Guid.NewGuid(), clock.UtcNow);
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
// Every position write asks the same four questions in the same order, and getting the ORDER wrong is how
// an authorization surface leaks: checking the row version before the permission would tell an unauthorized
// caller whether their token was current, and checking company scope before the functional permission would
// tell them the position exists.
//
// Extracted so the order is written once and every handler inherits it, rather than being retyped four
// times and drifting on the third. `JobGradeWriteContext` and `SalaryGradeWriteContext` state the same four
// questions for their own aggregates; see `JobGradeCommandHandlers.cs` and `SalaryGradeCommandHandlers.cs` for why they are not one generic type.
internal static class PositionWriteContext
{
  public static async Task<Result<Position>> LoadAsync(
    IPositionRepository positions,
    IPositionScopeResolver scope,
    ICurrentTenant currentTenant,
    Guid positionId,
    string permission,
    byte[]? expectedRowVersion,
    CancellationToken cancellationToken)
  {
    if (currentTenant.TenantId is not { } tenantId)
    {
      return Result.Failure<Position>(PositionErrors.InvalidActor);
    }

    // 1. THE FUNCTIONAL PERMISSION, before anything is loaded. A caller without it learns nothing at all.
    var permitted = scope.RequirePermission(permission);
    if (permitted.IsFailure)
    {
      return Result.Failure<Position>(permitted.Error);
    }

    // 2. The position. NotFound is also the answer for another tenant's row, because the tenant filter makes
    //    it unreachable rather than forbidden.
    var position = await positions.GetByIdAsync(positionId, cancellationToken);
    if (position is null || position.TenantId != tenantId)
    {
      return Result.Failure<Position>(PositionErrors.PositionNotFound);
    }

    // 3. COMPANY SCOPE, re-asked live against the position's OWN company — not against a company the caller
    //    named, which they never get to choose here.
    var authorized = await scope.AuthorizeAsync(permission, position.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      // The same NotFound a nonexistent position gives. A distinct refusal would confirm that a position
      // exists in a company the caller may not see (BR-PLT-0002).
      return Result.Failure<Position>(PositionErrors.PositionNotFound);
    }

    // 4. THE CONCURRENCY TOKEN, last. Compared here so a stale caller is refused before the aggregate is
    //    mutated, and compared again by the database on save — this is the friendly error, not the rule.
    if (expectedRowVersion is not null &&
      !position.RowVersion.AsSpan().SequenceEqual(expectedRowVersion))
    {
      return Result.Failure<Position>(PositionErrors.ConcurrencyConflict);
    }

    return Result.Success(position);
  }
}

// ================================================================================================
// THE GRADE REFERENCE CHECK, WRITTEN ONCE (BRULE-POS-0009, BRULE-POS-0010, BRULE-POS-0011)
// ================================================================================================
//
// Three questions, in one order, for both referencing directions: does the grade exist, is it in the same
// tenant and company as the record pointing at it, and is it Active at the moment of assignment.
//
// ---- WHY "ACTIVE AT THE MOMENT OF ASSIGNMENT" IS NOT AN ONGOING INVARIANT.
//
// A grade may be deactivated later, and `DEC-POS-0013` refuses that while ACTIVE dependents remain — so the
// only way to end up with an active position aimed at an inactive grade is to deactivate the position
// first. The check here is what makes that refusal complete rather than what enforces it alone.
internal static class PositionGradeReference
{
  public static async Task<Result> ValidateJobGradeAsync(
    IJobGradeRepository jobGrades,
    Guid? jobGradeId,
    Guid tenantId,
    Guid companyId,
    CancellationToken cancellationToken)
  {
    // No grade is a legal answer. A position may be defined before the ladder it will sit on exists — the
    // same "define before you price" reasoning that makes a salary band nullable (`DEC-POS-0027`).
    if (jobGradeId is not { } gradeId)
    {
      return Result.Success();
    }

    var grade = await jobGrades.GetByIdAsync(gradeId, cancellationToken);

    if (grade is null || grade.TenantId != tenantId)
    {
      return Result.Failure(PositionErrors.GradeReferenceNotFound);
    }

    if (grade.CompanyId != companyId)
    {
      return Result.Failure(PositionErrors.GradeInDifferentCompany);
    }

    return grade.Status != JobGradeStatus.Active
      ? Result.Failure(PositionErrors.GradeInactive)
      : Result.Success();
  }

  public static async Task<Result> ValidateSalaryGradeAsync(
    ISalaryGradeRepository salaryGrades,
    Guid? salaryGradeId,
    Guid tenantId,
    Guid companyId,
    CancellationToken cancellationToken)
  {
    if (salaryGradeId is not { } gradeId)
    {
      return Result.Success();
    }

    var grade = await salaryGrades.GetByIdAsync(gradeId, cancellationToken);

    if (grade is null || grade.TenantId != tenantId)
    {
      return Result.Failure(PositionErrors.GradeReferenceNotFound);
    }

    if (grade.CompanyId != companyId)
    {
      return Result.Failure(PositionErrors.GradeInDifferentCompany);
    }

    return grade.Status != SalaryGradeStatus.Active
      ? Result.Failure(PositionErrors.GradeInactive)
      : Result.Success();
  }
}
