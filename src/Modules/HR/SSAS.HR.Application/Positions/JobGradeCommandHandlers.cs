using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Application.Positions.Reads;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Application.Positions;

// THE JOB GRADE LADDER (FR-POS-0206, FR-POS-0207, FR-POS-0208).
//
// ================================================================================================
// WHY THIS IS NOT ONE GENERIC `GradeCommandHandlers<TGrade>`
// ================================================================================================
//
// The two ladders have the same SHAPE and are not the same THING. `OD-POS-002` made them separate
// aggregates with separate permissions, separate uniqueness scopes, separate error constants and — from
// `DEC-POS-0018` — a deliberate sensitivity difference: a salary grade carries money and a job grade does
// not. A generic handler parameterized over both would erase every one of those differences into type
// arguments, and the first thing to go would be the compiler's ability to stop a salary-grade permission
// being used on a job-grade write.
//
// The duplication is real and it is the cheaper of the two costs.
public sealed record CreateJobGradeCommand(
  Guid CompanyId,
  string Code,
  string Name,
  int RankOrder,
  Guid? SalaryGradeId);

public sealed class CreateJobGradeCommandHandler(
  IJobGradeRepository jobGrades,
  ISalaryGradeRepository salaryGrades,
  IPositionScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<Guid>> HandleAsync(
    CreateJobGradeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (currentTenant.TenantId is not { } tenantId || string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure<Guid>(PositionErrors.InvalidActor);
    }

    var authorized = await scope.AuthorizeAsync(
      HrPermissionNames.CreateJobGrades, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var code = JobGradeCode.Create(command.Code);
    if (code.IsFailure)
    {
      return Result.Failure<Guid>(code.Error);
    }

    var name = JobGradeName.Create(command.Name);
    if (name.IsFailure)
    {
      return Result.Failure<Guid>(name.Error);
    }

    // ---- THE SALARY GRADE, IF ONE WAS NAMED (BRULE-POS-0010, BRULE-POS-0011).
    //
    // The reference runs Job Grade -> Salary Grade and never the reverse, which is what keeps the
    // foreign-key graph a tree and the cutover copy order decidable (`DEC-POS-0002`, `NFR-POS-0305`).
    var reference = await PositionGradeReference.ValidateSalaryGradeAsync(
      salaryGrades, command.SalaryGradeId, tenantId, command.CompanyId, cancellationToken);
    if (reference.IsFailure)
    {
      return Result.Failure<Guid>(reference.Error);
    }

    // ---- TWO UNIQUENESS QUESTIONS, ASKED SEPARATELY (BRULE-POS-0004, BRULE-POS-0007).
    //
    // Two unique indexes exist and they are NOT interchangeable, which `api-contracts.md` records as a case
    // the Department precedent does not cover. Asking both means the caller is told which one they
    // collided with rather than being handed whichever index the database happened to check first.
    if (await jobGrades.CodeExistsAsync(command.CompanyId, code.Value.NormalizedValue, cancellationToken))
    {
      return Result.Failure<Guid>(PositionErrors.JobGradeCodeConflict);
    }

    if (await jobGrades.RankOrderExistsAsync(command.CompanyId, command.RankOrder, cancellationToken))
    {
      return Result.Failure<Guid>(PositionErrors.JobGradeRankConflict);
    }

    var occurredUtc = clock.UtcNow;
    var jobGrade = JobGrade.Create(
      code.Value, name.Value, command.RankOrder, command.SalaryGradeId,
      currentUser.UserId!, Guid.NewGuid(), occurredUtc);
    if (jobGrade.IsFailure)
    {
      return Result.Failure<Guid>(jobGrade.Error);
    }

    jobGrade.Value.StampCreated(tenantId, command.CompanyId, Guid.NewGuid(), occurredUtc);

    await jobGrades.AddAsync(jobGrade.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure
      ? Result.Failure<Guid>(saved.Error)
      : Result.Success(jobGrade.Value.Id);
  }
}

// UPDATE A JOB GRADE (FR-POS-0207). Code, Name, RankOrder and the salary grade reference.
public sealed record UpdateJobGradeCommand(
  Guid JobGradeId,
  string Code,
  string Name,
  int RankOrder,
  Guid? SalaryGradeId,
  byte[] RowVersion);

public sealed class UpdateJobGradeCommandHandler(
  IJobGradeRepository jobGrades,
  ISalaryGradeRepository salaryGrades,
  IPositionScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    UpdateJobGradeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var loaded = await JobGradeWriteContext.LoadAsync(
      jobGrades, scope, currentTenant, command.JobGradeId,
      HrPermissionNames.UpdateJobGrades, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      return Result.Failure(loaded.Error);
    }

    var jobGrade = loaded.Value;

    var code = JobGradeCode.Create(command.Code);
    if (code.IsFailure)
    {
      return Result.Failure(code.Error);
    }

    var name = JobGradeName.Create(command.Name);
    if (name.IsFailure)
    {
      return Result.Failure(name.Error);
    }

    var reference = await PositionGradeReference.ValidateSalaryGradeAsync(
      salaryGrades, command.SalaryGradeId, jobGrade.TenantId, jobGrade.CompanyId, cancellationToken);
    if (reference.IsFailure)
    {
      return Result.Failure(reference.Error);
    }

    if (await jobGrades.CodeExistsForAnotherAsync(
      jobGrade.CompanyId, code.Value.NormalizedValue, jobGrade.Id, cancellationToken))
    {
      return Result.Failure(PositionErrors.JobGradeCodeConflict);
    }

    if (await jobGrades.RankOrderExistsForAnotherAsync(
      jobGrade.CompanyId, command.RankOrder, jobGrade.Id, cancellationToken))
    {
      return Result.Failure(PositionErrors.JobGradeRankConflict);
    }

    var updated = jobGrade.UpdateDescription(
      code.Value, name.Value, command.RankOrder, command.SalaryGradeId, Guid.NewGuid(), clock.UtcNow);
    if (updated.IsFailure)
    {
      return updated;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }
}

public sealed record DeactivateJobGradeCommand(Guid JobGradeId, byte[] RowVersion);

public sealed record ReactivateJobGradeCommand(Guid JobGradeId, byte[] RowVersion);

// DEACTIVATE A JOB GRADE (FR-POS-0208, BRULE-POS-0015, DEC-POS-0013).
//
// ================================================================================================
// IT REFUSES WHILE ACTIVE DEPENDENTS REMAIN, AND IT DOES NOT CASCADE.
// ================================================================================================
//
// A cascade would deactivate an arbitrary amount of structure from one click, and it would destroy the
// information needed to reverse it: reactivating could not tell which positions were already inactive
// beforehand. Refusing until the dependents are handled is more work for the operator and is the only
// version that is actually reversible — the same reasoning `DeactivateDepartmentCommandHandler` states for
// active children.
//
// Inactive positions do not block it — they are already in the state that makes the reference harmless.
//
// ---- THE CHECK AND THE WRITE ARE ONE TRANSACTION, AND THAT IS THE WHOLE GUARANTEE.
//
// `HasActivePositionsForJobGradeAsync` reads through the SAME context the save then commits through, so the
// count and the state change are one unit. A check performed against a separate connection could be true
// when it was asked and false when the write landed, which is precisely the race the rule exists to lose.
//
// It is NOT proof against a position being created against this grade in a concurrent transaction — the
// database has no constraint that would refuse it, and `DEC-POS-0013` did not ask for one. What that
// interleave produces is an Active position pointing at an Inactive grade, which the reference re-validation
// on the next position update refuses; the window is recorded rather than hidden.
public sealed class DeactivateJobGradeCommandHandler(
  IJobGradeRepository jobGrades,
  IPositionRepository positions,
  IPositionScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    DeactivateJobGradeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(PositionErrors.InvalidActor);
    }

    var loaded = await JobGradeWriteContext.LoadAsync(
      jobGrades, scope, currentTenant, command.JobGradeId,
      HrPermissionNames.DeactivateJobGrades, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      return Result.Failure(loaded.Error);
    }

    var jobGrade = loaded.Value;

    if (await positions.HasActivePositionsForJobGradeAsync(jobGrade.Id, cancellationToken))
    {
      return Result.Failure(PositionErrors.GradeHasActiveDependents);
    }

    var deactivated = jobGrade.Deactivate(currentUser.UserId!, Guid.NewGuid(), clock.UtcNow);
    if (deactivated.IsFailure)
    {
      return deactivated;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }
}

// REACTIVATE A JOB GRADE (FR-POS-0208).
//
// No dependent check: dependents are what deactivation refuses, and reactivation only ever makes more
// references legal. The salary grade this job grade points at is not re-validated, for the same reason
// `ReactivatePositionCommandHandler` does not re-validate its grade — the refusal would be unactionable to
// a caller holding only the Deactivate permission.
public sealed class ReactivateJobGradeCommandHandler(
  IJobGradeRepository jobGrades,
  IPositionScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    ReactivateJobGradeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(PositionErrors.InvalidActor);
    }

    var loaded = await JobGradeWriteContext.LoadAsync(
      jobGrades, scope, currentTenant, command.JobGradeId,
      HrPermissionNames.DeactivateJobGrades, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      return Result.Failure(loaded.Error);
    }

    var reactivated = loaded.Value.Reactivate(currentUser.UserId!, Guid.NewGuid(), clock.UtcNow);
    if (reactivated.IsFailure)
    {
      return reactivated;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }
}

// The same four questions in the same order as `PositionWriteContext`, over the job grade aggregate. See
// that type for why the order is what it is.
internal static class JobGradeWriteContext
{
  public static async Task<Result<JobGrade>> LoadAsync(
    IJobGradeRepository jobGrades,
    IPositionScopeResolver scope,
    ICurrentTenant currentTenant,
    Guid jobGradeId,
    string permission,
    byte[]? expectedRowVersion,
    CancellationToken cancellationToken)
  {
    if (currentTenant.TenantId is not { } tenantId)
    {
      return Result.Failure<JobGrade>(PositionErrors.InvalidActor);
    }

    var permitted = scope.RequirePermission(permission);
    if (permitted.IsFailure)
    {
      return Result.Failure<JobGrade>(permitted.Error);
    }

    var jobGrade = await jobGrades.GetByIdAsync(jobGradeId, cancellationToken);
    if (jobGrade is null || jobGrade.TenantId != tenantId)
    {
      return Result.Failure<JobGrade>(PositionErrors.JobGradeNotFound);
    }

    var authorized = await scope.AuthorizeAsync(permission, jobGrade.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<JobGrade>(PositionErrors.JobGradeNotFound);
    }

    if (expectedRowVersion is not null &&
      !jobGrade.RowVersion.AsSpan().SequenceEqual(expectedRowVersion))
    {
      return Result.Failure<JobGrade>(PositionErrors.ConcurrencyConflict);
    }

    return Result.Success(jobGrade);
  }
}
