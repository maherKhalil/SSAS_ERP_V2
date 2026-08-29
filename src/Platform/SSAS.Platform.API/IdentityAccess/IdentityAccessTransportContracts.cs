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

// The permission catalogue as a flat list (T-203). Not paged: it is a constant of the deployment, a few
// dozen entries, and paging it would invent a contract the caller must then handle for no benefit.
//
// `Scope` is projected as a STRING rather than the enum's numeric value, so adding a scope cannot silently
// change what an existing scope means on the wire.
public sealed record PermissionCatalogItemResponse(
  string Name,
  string Scope,
  string? Description);

public sealed record PermissionCatalogResponse(
  IReadOnlyCollection<PermissionCatalogItemResponse> Items);
