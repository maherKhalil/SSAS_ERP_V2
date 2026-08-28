using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.TenantUsers;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

// T-092's write path. Both reads are seeks on the two unique indexes `ADR-030` Decision 3 is enforced by —
// `UX_UserEmployeeLink_TenantId_TenantUserId` and `UX_UserEmployeeLink_TenantId_EmployeeId` — which is why
// `data-model.md` specifies no separate covering index for either direction.
//
// TRACKED, not `AsNoTracking`: what these load is about to be removed or refused, and a detached instance
// cannot be handed to `Remove`. The two READ services deliberately do the opposite.
public sealed class UserEmployeeLinkRepository(PlatformDbContext dbContext) : IUserEmployeeLinkRepository
{
  public Task<UserEmployeeLink?> GetByTenantUserAsync(
    Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
    dbContext.UserEmployeeLinks.SingleOrDefaultAsync(
      link => link.TenantId == tenantId && link.TenantUserId == tenantUserId, cancellationToken);

  public Task<UserEmployeeLink?> GetByEmployeeAsync(
    Guid tenantId, Guid employeeId, CancellationToken cancellationToken = default) =>
    dbContext.UserEmployeeLinks.SingleOrDefaultAsync(
      link => link.TenantId == tenantId && link.EmployeeId == employeeId, cancellationToken);

  public async Task AddAsync(UserEmployeeLink link, CancellationToken cancellationToken = default) =>
    await dbContext.UserEmployeeLinks.AddAsync(link, cancellationToken);

  public void Remove(UserEmployeeLink link) => dbContext.UserEmployeeLinks.Remove(link);
}
