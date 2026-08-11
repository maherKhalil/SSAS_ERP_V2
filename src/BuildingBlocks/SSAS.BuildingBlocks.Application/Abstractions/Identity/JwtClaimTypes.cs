namespace SSAS.BuildingBlocks.Application.Abstractions.Identity;

public static class JwtClaimTypes
{
  public const string Subject = "sub";

  public const string Name = "name";

  public const string Email = "email";

  public const string TenantId = "tenant_id";

  public const string IdentityId = "identity_id";

  public const string TenantUserId = "tenant_user_id";

  public const string CompanyId = "company_id";

  public const string Role = "role";

  public const string Permission = "permission";

  public const string SessionId = "session_id";

  public const string ClientId = "client_id";

  public const string SecurityVersion = "security_version";

  // Platform-plane token profile discriminator (ADR-015 / DEC-TEN-0022). Present and equal to
  // "platform" on platform tokens; absent (or "tenant") on tenant tokens. Server-issued, never
  // caller-editable, never tenant-derived.
  public const string SecurityPlane = "security_plane";

  public const string JwtId = "jti";
}
