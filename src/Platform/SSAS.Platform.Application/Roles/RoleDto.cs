using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Roles;

public sealed record RoleDto(
  long RoleId,
  string Name,
  string? Description,
  RoleType RoleType,
  RoleStatus Status,
  IReadOnlyCollection<string> ActivePermissions,
  byte[] RowVersion);
