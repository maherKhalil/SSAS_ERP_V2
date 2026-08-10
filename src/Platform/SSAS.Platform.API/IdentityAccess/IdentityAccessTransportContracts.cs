namespace SSAS.Platform.API.IdentityAccess;

// Safe HTTP projections for the Platform Identity/Access admin surface. Transport contracts
// live in the API layer only and never carry a writable owning-tenant field; the owning tenant
// is always the trusted current-tenant context.
public sealed record RoleSummaryResponse(
  long RoleId,
  string Name,
  string? Description,
  string RoleType,
  string Status,
  IReadOnlyCollection<string> ActivePermissions,
  string RowVersion);

public sealed record RolePageResponse(
  IReadOnlyCollection<RoleSummaryResponse> Items,
  int PageNumber,
  int PageSize,
  int TotalCount,
  int TotalPages);
