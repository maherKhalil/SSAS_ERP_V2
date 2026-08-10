namespace SSAS.Platform.Application.PlatformSupport;

public sealed record GrantPlatformPermissionCommand(long PlatformSupportPrincipalId, string PermissionName);
