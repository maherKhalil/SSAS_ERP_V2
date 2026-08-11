namespace SSAS.Platform.Application.PlatformSupport;

public sealed record DisablePlatformSupportPrincipalCommand(long PlatformSupportPrincipalId, byte[] ExpectedRowVersion);
