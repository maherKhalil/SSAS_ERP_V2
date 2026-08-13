using System.Text.Json.Serialization;
using SSAS.Platform.Application.PlatformSupport;

namespace SSAS.Platform.API.PlatformSupport;

// Phase-4D platform authority-administration transport contracts (ADR-016 §5, DEC-TEN-0020/0021/0025/0026).
// Transport-owned: no EF entities, no secrets, no tenant fields. Requests carry only the administrative TARGET
// and business input — never the caller's identity, plane or permissions, which come from the validated token.
// Every request member declares an explicit [JsonPropertyName] that exactly matches the strict-reader allow-map
// keys, so the shared reader's case-sensitive deserialization binds them.

// Register targets an EXISTING identity; registration never creates an Identity or AuthenticationAccount.
public sealed record RegisterPlatformSupportPrincipalRequest(
  [property: JsonPropertyName("identityId")] long? IdentityId);

// Grant/Revoke carry only the permission name; scope is resolved from the code-owned catalog, never supplied.
public sealed record PlatformPermissionRequest(
  [property: JsonPropertyName("permissionName")] string? PermissionName);

// Lifecycle transitions carry the optimistic concurrency token only; the caller never supplies a status.
public sealed record PlatformSupportPrincipalLifecycleRequest(
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

public sealed record RegisterPlatformSupportPrincipalResponse(long PlatformSupportPrincipalId);

public sealed record PlatformSupportPrincipalResponse(
  long PlatformSupportPrincipalId,
  long IdentityId,
  string Status,
  DateTimeOffset CreatedUtc,
  string? CreatedBy,
  DateTimeOffset ModifiedUtc,
  string? ModifiedBy,
  DateTimeOffset? StatusChangedUtc,
  string? StatusChangedBy,
  string RowVersion)
{
  public static PlatformSupportPrincipalResponse From(PlatformSupportPrincipalDto dto, string rowVersion)
  {
    ArgumentNullException.ThrowIfNull(dto);
    return new PlatformSupportPrincipalResponse(
      dto.PlatformSupportPrincipalId,
      dto.IdentityId,
      dto.Status.ToString(),
      dto.CreatedUtc,
      dto.CreatedBy,
      dto.ModifiedUtc,
      dto.ModifiedBy,
      dto.StatusChangedUtc,
      dto.StatusChangedBy,
      rowVersion);
  }
}

public sealed record PlatformSupportPrincipalPageResponse(
  IReadOnlyList<PlatformSupportPrincipalResponse> Items,
  int PageNumber,
  int PageSize,
  int TotalCount,
  int TotalPages);

// Assignment HISTORY: active and revoked records alike, with audit metadata (DEC-TEN-0025).
public sealed record PlatformPermissionAssignmentResponse(
  long PlatformPermissionAssignmentId,
  long PlatformSupportPrincipalId,
  string PermissionName,
  DateTimeOffset AssignedUtc,
  string AssignedBy,
  DateTimeOffset? RemovedUtc,
  string? RemovedBy,
  bool IsActive)
{
  public static PlatformPermissionAssignmentResponse From(PlatformPermissionAssignmentDto dto)
  {
    ArgumentNullException.ThrowIfNull(dto);
    return new PlatformPermissionAssignmentResponse(
      dto.PlatformPermissionAssignmentId,
      dto.PlatformSupportPrincipalId,
      dto.PermissionName,
      dto.AssignedUtc,
      dto.AssignedBy,
      dto.RemovedUtc,
      dto.RemovedBy,
      dto.IsActive);
  }
}

// Current effective authority projection: active, current-catalog PlatformSupport permission names only.
public sealed record PlatformSupportActivePermissionsResponse(IReadOnlyList<string> PermissionNames);
