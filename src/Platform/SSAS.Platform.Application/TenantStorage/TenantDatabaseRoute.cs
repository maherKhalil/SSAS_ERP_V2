using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// Trusted routing metadata for one tenant (ADR-017). Every field originates from Platform database state
// derived from a validated tenant identity — never from caller input.
//
// SECRET CONTAINMENT: this type deliberately carries NO connection string, password, username, endpoint or
// credential reference. It is the Application-layer boundary object, so anything on it may reach logs,
// diagnostics and future callers. Credential material stays inside Infrastructure, where the connection
// factory resolves ServerKey against trusted configuration.
//
// RoutingVersion is correctness metadata, not diagnostics: when caching is eventually introduced, a cached
// route is valid only for the RoutingVersion it was resolved under (ADR-020).
public sealed record TenantDatabaseRoute(
  Guid TenantId,
  long TenantDatabaseId,
  string ServerKey,
  string DatabaseName,
  TenantDatabaseHostingMode HostingMode,
  TenantDatabaseStorageMode StorageMode,
  long RoutingVersion);
