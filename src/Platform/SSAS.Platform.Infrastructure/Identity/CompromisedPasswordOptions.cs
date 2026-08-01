namespace SSAS.Platform.Infrastructure.Identity;

public sealed class CompromisedPasswordOptions
{
  public const string SectionName = "Authentication:CompromisedPasswords";

  public bool Enabled { get; set; } = true;

  public string? DatasetPath { get; set; }

  public string? DatasetVersion { get; set; }

  public string? LicenseName { get; set; }

  public string? LicenseUrl { get; set; }
}
