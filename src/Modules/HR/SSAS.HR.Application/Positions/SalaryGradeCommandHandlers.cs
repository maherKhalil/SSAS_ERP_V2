using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Application.Positions.Reads;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Application.Positions;

// THE SALARY GRADE LADDER (FR-POS-0206, FR-POS-0207, FR-POS-0208, FR-POS-0209).
//
// ================================================================================================
// THE AMOUNTS TRAVEL WITH THE EDIT, AND THAT IS ONE OPERATION RATHER THAN TWO.
// ================================================================================================
//
// `FR-POS-0209` — maintain a salary grade's amounts — is served by the same `Update` as the code, name and
// rank, under `HR.SalaryGrades.Update`. It is not a separate command because the aggregate has no separate
// method: `SalaryGrade.UpdateDescription` takes the band, and splitting the application operation would
// mean two handlers racing to write the same row through the same rowversion.
//
// The three amounts are carried as three nullable decimals rather than as a constructed `SalaryBand`
// because a command is a transport shape and must be able to express the INVALID combinations a caller can
// send — a half-filled band is a real request that must be refused with `SalaryBandIncomplete`, and it
// cannot be refused by a type that cannot represent it.
public sealed record CreateSalaryGradeCommand(
  Guid CompanyId,
  string Code,
  string Name,
  int RankOrder,
  decimal? MinimumAmount,
  decimal? MidpointAmount,
  decimal? MaximumAmount);

public sealed class CreateSalaryGradeCommandHandler(
  ISalaryGradeRepository salaryGrades,
  IPositionScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<Guid>> HandleAsync(
    CreateSalaryGradeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (currentTenant.TenantId is not { } tenantId || string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure<Guid>(PositionErrors.InvalidActor);
    }

    var authorized = await scope.AuthorizeAsync(
      HrPermissionNames.CreateSalaryGrades, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var code = SalaryGradeCode.Create(command.Code);
    if (code.IsFailure)
    {
      return Result.Failure<Guid>(code.Error);
    }

    var name = SalaryGradeName.Create(command.Name);
    if (name.IsFailure)
    {
      return Result.Failure<Guid>(name.Error);
    }

    // ALL THREE OR NONE (DEC-POS-0027). `SalaryBand.Create` is the only way to build one, and a null result
    // on success is the legal unpriced case rather than a validation failure that slipped through.
    var band = SalaryBand.Create(
      command.MinimumAmount, command.MidpointAmount, command.MaximumAmount);
    if (band.IsFailure)
    {
      return Result.Failure<Guid>(band.Error);
    }

    if (await salaryGrades.CodeExistsAsync(
      command.CompanyId, code.Value.NormalizedValue, cancellationToken))
    {
      return Result.Failure<Guid>(PositionErrors.SalaryGradeCodeConflict);
    }

    if (await salaryGrades.RankOrderExistsAsync(command.CompanyId, command.RankOrder, cancellationToken))
    {
      return Result.Failure<Guid>(PositionErrors.SalaryGradeRankConflict);
    }

    var occurredUtc = clock.UtcNow;
    var salaryGrade = SalaryGrade.Create(
      code.Value, name.Value, command.RankOrder, band.Value,
      currentUser.UserId!, Guid.NewGuid(), occurredUtc);
    if (salaryGrade.IsFailure)
    {
      return Result.Failure<Guid>(salaryGrade.Error);
    }

    salaryGrade.Value.StampCreated(tenantId, command.CompanyId, Guid.NewGuid(), occurredUtc);

    await salaryGrades.AddAsync(salaryGrade.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure
      ? Result.Failure<Guid>(saved.Error)
      : Result.Success(salaryGrade.Value.Id);
  }
}

public sealed record UpdateSalaryGradeCommand(
  Guid SalaryGradeId,
  string Code,
  string Name,
  int RankOrder,
  decimal? MinimumAmount,
  decimal? MidpointAmount,
  decimal? MaximumAmount,
  byte[] RowVersion);

public sealed class UpdateSalaryGradeCommandHandler(
  ISalaryGradeRepository salaryGrades,
  IPositionScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    UpdateSalaryGradeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var loaded = await SalaryGradeWriteContext.LoadAsync(
      salaryGrades, scope, currentTenant, command.SalaryGradeId,
      HrPermissionNames.UpdateSalaryGrades, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      return Result.Failure(loaded.Error);
    }

    var salaryGrade = loaded.Value;

    var code = SalaryGradeCode.Create(command.Code);
    if (code.IsFailure)
    {
      return Result.Failure(code.Error);
    }

    var name = SalaryGradeName.Create(command.Name);
    if (name.IsFailure)
    {
      return Result.Failure(name.Error);
    }

    // A band may be REMOVED as well as set: three nulls un-price the grade. Un-pricing is a legal
    // correction — the alternative would be that a mistaken band can never be withdrawn, only overwritten
    // with a different guess.
    var band = SalaryBand.Create(
      command.MinimumAmount, command.MidpointAmount, command.MaximumAmount);
    if (band.IsFailure)
    {
      return Result.Failure(band.Error);
    }

    if (await salaryGrades.CodeExistsForAnotherAsync(
      salaryGrade.CompanyId, code.Value.NormalizedValue, salaryGrade.Id, cancellationToken))
    {
      return Result.Failure(PositionErrors.SalaryGradeCodeConflict);
    }

    if (await salaryGrades.RankOrderExistsForAnotherAsync(
      salaryGrade.CompanyId, command.RankOrder, salaryGrade.Id, cancellationToken))
    {
      return Result.Failure(PositionErrors.SalaryGradeRankConflict);
    }

    var updated = salaryGrade.UpdateDescription(
      code.Value, name.Value, command.RankOrder, band.Value, Guid.NewGuid(), clock.UtcNow);
    if (updated.IsFailure)
    {
      return updated;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }
}

public sealed record DeactivateSalaryGradeCommand(Guid SalaryGradeId, byte[] RowVersion);

public sealed record ReactivateSalaryGradeCommand(Guid SalaryGradeId, byte[] RowVersion);

// DEACTIVATE A SALARY GRADE (FR-POS-0208, BRULE-POS-0015, DEC-POS-0013).
//
// The dependent set is ACTIVE JOB GRADES, and that is the whole of it: nothing else references a salary
// grade, because the reference runs one way (`BRULE-POS-0010`). A position never points at one directly, so
// there is no second question to ask — which is the shape the one-way reference was chosen to produce.
//
// The check reads through the same context the save commits through; see the job grade handler for what
// that does and does not guarantee.
public sealed class DeactivateSalaryGradeCommandHandler(
  ISalaryGradeRepository salaryGrades,
  IJobGradeRepository jobGrades,
  IPositionScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    DeactivateSalaryGradeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(PositionErrors.InvalidActor);
    }

    var loaded = await SalaryGradeWriteContext.LoadAsync(
      salaryGrades, scope, currentTenant, command.SalaryGradeId,
      HrPermissionNames.DeactivateSalaryGrades, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      return Result.Failure(loaded.Error);
    }

    var salaryGrade = loaded.Value;

    if (await jobGrades.HasActiveJobGradesForSalaryGradeAsync(salaryGrade.Id, cancellationToken))
    {
      return Result.Failure(PositionErrors.GradeHasActiveDependents);
    }

    var deactivated = salaryGrade.Deactivate(currentUser.UserId!, Guid.NewGuid(), clock.UtcNow);
    if (deactivated.IsFailure)
    {
      return deactivated;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure(saved.Error) : Result.Success();
  }
}

public sealed class ReactivateSalaryGradeCommandHandler(
  ISalaryGradeRepository salaryGrades,
  IPositionScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    ReactivateSalaryGradeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(PositionErrors.InvalidActor);
    }

    var loaded = await SalaryGradeWriteContext.LoadAsync(
      salaryGrades, scope, currentTenant, command.SalaryGradeId,
      HrPermissionNames.DeactivateSalaryGrades, command.RowVersion, cancellationToken);
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

// The same four questions in the same order as `PositionWriteContext`, over the salary grade aggregate.
internal static class SalaryGradeWriteContext
{
  public static async Task<Result<SalaryGrade>> LoadAsync(
    ISalaryGradeRepository salaryGrades,
    IPositionScopeResolver scope,
    ICurrentTenant currentTenant,
    Guid salaryGradeId,
    string permission,
    byte[]? expectedRowVersion,
    CancellationToken cancellationToken)
  {
    if (currentTenant.TenantId is not { } tenantId)
    {
      return Result.Failure<SalaryGrade>(PositionErrors.InvalidActor);
    }

    var permitted = scope.RequirePermission(permission);
    if (permitted.IsFailure)
    {
      return Result.Failure<SalaryGrade>(permitted.Error);
    }

    var salaryGrade = await salaryGrades.GetByIdAsync(salaryGradeId, cancellationToken);
    if (salaryGrade is null || salaryGrade.TenantId != tenantId)
    {
      return Result.Failure<SalaryGrade>(PositionErrors.SalaryGradeNotFound);
    }

    var authorized = await scope.AuthorizeAsync(permission, salaryGrade.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<SalaryGrade>(PositionErrors.SalaryGradeNotFound);
    }

    if (expectedRowVersion is not null &&
      !salaryGrade.RowVersion.AsSpan().SequenceEqual(expectedRowVersion))
    {
      return Result.Failure<SalaryGrade>(PositionErrors.ConcurrencyConflict);
    }

    return Result.Success(salaryGrade);
  }
}
