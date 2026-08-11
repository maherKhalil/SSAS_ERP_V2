namespace SSAS.Platform.Application.PlatformSupport;

public sealed record ReenablePlatformSupportPrincipalCommand(long PlatformSupportPrincipalId, byte[] ExpectedRowVersion);
