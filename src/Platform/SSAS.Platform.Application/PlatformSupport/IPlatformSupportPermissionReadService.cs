namespace SSAS.Platform.Application.PlatformSupport;

// Narrow authority read for later platform token issuance (Phase 3). Returns the principal's active
// PlatformSupport permission names, re-validated against the code-owned catalog (defense in depth).
// Phase 2 does NOT issue any claim or token; this only exposes the persisted authority for later use.
public interface IPlatformSupportPermissionReadService
{
  Task<IReadOnlyCollection<string>> GetActivePermissionsAsync(
    long platformSupportPrincipalId,
    CancellationToken cancellationToken = default);
}
