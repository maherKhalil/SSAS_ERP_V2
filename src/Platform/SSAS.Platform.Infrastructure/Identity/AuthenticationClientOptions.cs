namespace SSAS.Platform.Infrastructure.Identity;

public sealed class AuthenticationClientOptions
{
  public const string SectionName = "Authentication:Clients";

  public string[] AllowedClientIds { get; set; } = ["ssas-erp-web"];
}
