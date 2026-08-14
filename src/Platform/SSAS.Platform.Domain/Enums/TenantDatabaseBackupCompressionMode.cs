namespace SSAS.Platform.Domain.Enums;

// Compression policy for managed backups (ADR-022 §9).
public enum TenantDatabaseBackupCompressionMode
{
  // The default. Compress where the deployed SQL Server edition supports it; take an approved UNCOMPRESSED
  // backup where it does not. An unavailable capability is not a policy violation, and classifying it as
  // one would report a perfectly protected database unprotected.
  PreferredWhereSupported = 1,

  // Compression is genuinely required by this policy. An edition that cannot compress is then a real policy
  // failure rather than a capability gap.
  Required = 2,

  Disabled = 3
}
