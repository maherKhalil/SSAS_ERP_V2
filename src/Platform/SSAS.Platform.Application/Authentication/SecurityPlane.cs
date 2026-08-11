namespace SSAS.Platform.Application.Authentication;

// Approved values for the JwtClaimTypes.SecurityPlane claim (ADR-015 / DEC-TEN-0022). These are the
// only two planes; the value is server-selected by the token profile/type, never caller-supplied.
// A tenant token omits the claim (absence ⇒ tenant); a platform token carries exactly Platform.
public static class SecurityPlane
{
  public const string Tenant = "tenant";

  public const string Platform = "platform";
}
