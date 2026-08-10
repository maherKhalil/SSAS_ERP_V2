using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Companies;

public sealed class UpdateCompanyProfileCommandHandler(
  ICompanyRepository companyRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(UpdateCompanyProfileCommand command, CancellationToken cancellationToken = default)
  {
    var context = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (context.IsFailure)
    {
      return Result.Failure(context.Error);
    }

    var name = CompanyName.Create(command.CompanyName);
    if (name.IsFailure)
    {
      return Result.Failure(name.Error);
    }

    var company = await companyRepository.GetByIdAsync(command.CompanyId, cancellationToken);
    if (company is null)
    {
      return Result.Failure(CompanyErrors.NotFound);
    }

    if (!ApplicationExecutionContext.MatchesExpectedVersion(company.RowVersion, command.ExpectedRowVersion))
    {
      return Result.Failure(IdentityAccessErrors.ConcurrencyConflict);
    }

    var update = company.UpdateProfile(name.Value, context.Value.Actor, Guid.NewGuid(), clock.UtcNow);
    return update.IsFailure ? update : await PersistenceResult.SaveAsync(unitOfWork, cancellationToken);
  }
}
