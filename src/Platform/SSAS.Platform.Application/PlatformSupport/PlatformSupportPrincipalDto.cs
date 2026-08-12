using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.PlatformSupport;

// Read projection of a platform-support principal for authority administration (DEC-TEN-0025, Phase 4C).
// Global platform-plane authority: no TenantId/TenantUserId/CompanyId and no identity/account secrets.
// RowVersion is exposed as the repository-conventional byte[] concurrency token (mirrors TenantDto) so a
// future Disable/Re-enable HTTP caller can supply ExpectedRowVersion; transport encoding is a Phase-4D concern.
public sealed record PlatformSupportPrincipalDto(
  long PlatformSupportPrincipalId,
  long IdentityId,
  PlatformSupportPrincipalStatus Status,
  DateTimeOffset CreatedUtc,
  string? CreatedBy,
  DateTimeOffset ModifiedUtc,
  string? ModifiedBy,
  DateTimeOffset? StatusChangedUtc,
  string? StatusChangedBy,
  byte[] RowVersion);
