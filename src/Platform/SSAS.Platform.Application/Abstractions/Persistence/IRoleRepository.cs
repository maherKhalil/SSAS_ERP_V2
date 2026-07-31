using SSAS.Platform.Domain.Roles;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface IRoleRepository
{
  Task<Role?> GetByIdAsync(long roleId, CancellationToken cancellationToken = default);

  Task<IReadOnlyCollection<Role>> GetByIdsAsync(IReadOnlyCollection<long> roleIds, CancellationToken cancellationToken = default);

  Task<bool> NameExistsAsync(string normalizedRoleName, long? excludingRoleId = null, CancellationToken cancellationToken = default);

  Task AddAsync(Role role, CancellationToken cancellationToken = default);
}
