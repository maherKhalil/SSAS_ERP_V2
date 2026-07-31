using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantUsers;

public sealed record TenantUserDto(
  long TenantUserId,
  long IdentityId,
  string Email,
  string DisplayName,
  TenantUserStatus Status,
  IReadOnlyCollection<long> ActiveRoleIds,
  byte[] RowVersion);
