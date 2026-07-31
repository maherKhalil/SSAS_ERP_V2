using SSAS.Platform.Domain.TenantUsers;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface ITenantUserRepository
{
  Task<TenantUser?> GetByIdAsync(long tenantUserId, CancellationToken cancellationToken = default);

  Task<bool> EmailExistsAsync(string normalizedEmail, long? excludingTenantUserId = null, CancellationToken cancellationToken = default);

  Task<bool> MembershipExistsAsync(long identityId, CancellationToken cancellationToken = default);

  Task<bool> HasActiveAssignmentToRoleAsync(long roleId, CancellationToken cancellationToken = default);

  Task AddAsync(TenantUser tenantUser, CancellationToken cancellationToken = default);
}
