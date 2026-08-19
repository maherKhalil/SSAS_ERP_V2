using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Companies;

public sealed class CreateCompanyCommandHandler(
  ICompanyRepository companyRepository,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<Guid>> HandleAsync(CreateCompanyCommand command, CancellationToken cancellationToken = default)
  {
    var context = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (context.IsFailure)
    {
      return Result.Failure<Guid>(context.Error);
    }

    var code = CompanyCode.Create(command.CompanyCode);
    if (code.IsFailure)
    {
      return Result.Failure<Guid>(code.Error);
    }

    var name = CompanyName.Create(command.CompanyName);
    if (name.IsFailure)
    {
      return Result.Failure<Guid>(name.Error);
    }

    var currency = BaseCurrencyCode.Create(command.BaseCurrencyCode);
    if (currency.IsFailure)
    {
      return Result.Failure<Guid>(currency.Error);
    }

    if (await companyRepository.NormalizedCodeExistsAsync(code.Value.NormalizedValue, cancellationToken))
    {
      return Result.Failure<Guid>(CompanyErrors.CodeConflict);
    }

    var (tenantId, actor) = context.Value;
    var company = Company.Create(tenantId, code.Value, name.Value, currency.Value, actor, Guid.NewGuid(), clock.UtcNow);
    if (company.IsFailure)
    {
      return Result.Failure<Guid>(company.Error);
    }

    await companyRepository.AddAsync(company.Value, cancellationToken);
    var saveResult = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saveResult.IsFailure)
    {
      return saveResult.Error == IdentityAccessErrors.UniqueConstraintViolation
        ? Result.Failure<Guid>(CompanyErrors.CodeConflict)
        : Result.Failure<Guid>(saveResult.Error);
    }

    return Result.Success(company.Value.CompanyId);
  }
}
