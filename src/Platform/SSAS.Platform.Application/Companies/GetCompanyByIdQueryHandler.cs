using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.Companies;

public sealed class GetCompanyByIdQueryHandler(
  ICompanyReadService readService,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public async Task<Result<CompanyDto>> HandleAsync(GetCompanyByIdQuery query, CancellationToken cancellationToken = default)
  {
    var context = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (context.IsFailure)
    {
      return Result.Failure<CompanyDto>(context.Error);
    }

    var company = await readService.GetByIdAsync(query.CompanyId, cancellationToken);
    return company is null
      ? Result.Failure<CompanyDto>(CompanyErrors.NotFound)
      : Result.Success(company);
  }
}
