namespace SSAS.Platform.Application.PlatformSupport;

// Read projection of one platform-support permission assignment for authority administration (DEC-TEN-0025,
// Phase 4C). This is HISTORY: both active and revoked/removed assignments are represented, with their grant
// and removal audit metadata. It is deliberately NOT filtered through the current catalog — a historical
// assignment to a since-retired permission remains visible as persisted authority evidence. "Effective
// authority now" is a separate projection (active catalog-valid PlatformSupport permission names).
public sealed record PlatformPermissionAssignmentDto(
  long PlatformPermissionAssignmentId,
  long PlatformSupportPrincipalId,
  string PermissionName,
  DateTimeOffset AssignedUtc,
  string AssignedBy,
  DateTimeOffset? RemovedUtc,
  string? RemovedBy,
  bool IsActive);
