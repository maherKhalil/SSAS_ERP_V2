namespace SSAS.Platform.Domain.Enums;

// Platform-support principal lifecycle (ADR-016 / DEC-TEN-0020). A Disabled principal's platform
// authority is unusable while its assignments are retained; the only transitions are Active <-> Disabled.
public enum PlatformSupportPrincipalStatus
{
  Active = 1,
  Disabled = 2
}
