using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Tenants;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

public sealed class TenantReadService(PlatformDbContext dbContext) : ITenantReadService
{
  public async Task<TenantDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
  {
    var tenant = await dbContext.Tenants.AsNoTracking()
      .SingleOrDefaultAsync(item => item.Id == tenantId, cancellationToken);
    return tenant is null ? null : Map(tenant);
  }

  public async Task<PagedResult<TenantDto>> ListAsync(
    TenantStatus? status,
    int pageNumber,
    int pageSize,
    CancellationToken cancellationToken = default)
  {
    var query = dbContext.Tenants.AsNoTracking();
    if (status.HasValue)
    {
      query = query.Where(tenant => tenant.Status == status.Value);
    }

    var totalCount = await query.CountAsync(cancellationToken);
    var tenants = await query
      .OrderBy(tenant => tenant.TenantName)
      .ThenBy(tenant => tenant.Id)
      .Skip((pageNumber - 1) * pageSize)
      .Take(pageSize)
      .ToArrayAsync(cancellationToken);
    return new PagedResult<TenantDto>(tenants.Select(Map).ToArray(), pageNumber, pageSize, totalCount);
  }

  private static TenantDto Map(Tenant tenant) => new(
    tenant.TenantId,
    tenant.TenantCode.Value,
    tenant.TenantName.Value,
    tenant.Status,
    tenant.CreatedUtc,
    tenant.CreatedBy,
    tenant.ModifiedUtc,
    tenant.ModifiedBy,
    tenant.StatusChangedUtc,
    tenant.StatusChangedBy,
    tenant.StatusChangeReasonCode,
    tenant.RowVersion.ToArray());
}
