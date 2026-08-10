namespace SSAS.Platform.Application.PlatformSupport;

public sealed record RevokePlatformPermissionCommand(long PlatformSupportPrincipalId, string PermissionName);
