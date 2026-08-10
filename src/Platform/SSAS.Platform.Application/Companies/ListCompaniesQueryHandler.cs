using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;

namespace SSAS.Platform.Application.Companies;

public sealed class ListCompaniesQueryHandler(
  ICompanyReadService readService,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public const int MaximumPageSize = 200;

  public async Task<Result<PagedResult<CompanyDto>>> HandleAsync(
    ListCompaniesQuery query,
    CancellationToken cancellationToken = default)
  {
    var context = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (context.IsFailure)
    {
      return Result.Failure<PagedResult<CompanyDto>>(context.Error);
    }

    if (query.PageNumber < 1 || query.PageSize is < 1 or > MaximumPageSize ||
      (query.Status.HasValue && !Enum.IsDefined(query.Status.Value)))
    {
      return Result.Failure<PagedResult<CompanyDto>>(
        new Error("Company.ListFilterInvalid", "Company status and paging values must be valid and bounded."));
    }

    return Result.Success(await readService.ListAsync(
      query.Status,
      query.PageNumber,
      query.PageSize,
      cancellationToken));
  }
}
