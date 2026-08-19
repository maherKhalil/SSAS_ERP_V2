using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.Companies;

public sealed class ArchiveCompanyCommandHandler(
  ICompanyRepository companyRepository,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(ArchiveCompanyCommand command, CancellationToken cancellationToken = default)
  {
    var context = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (context.IsFailure)
    {
      return Result.Failure(context.Error);
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

    var transition = company.Archive(command.ReasonCode, context.Value.Actor, Guid.NewGuid(), clock.UtcNow);
    return transition.IsFailure ? transition : await PersistenceResult.SaveAsync(unitOfWork, cancellationToken);
  }
}
