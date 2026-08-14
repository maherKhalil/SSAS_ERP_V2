namespace SSAS.Platform.Domain.Enums;

// Backup encryption policy (ADR-022 §9). Encryption KEYS AND CERTIFICATES ARE NEVER STORED IN THE PLATFORM
// DATABASE under any value here — this records which mechanism protects the artifact, never the material
// that would unlock it.
public enum TenantDatabaseBackupEncryptionMode
{
  // V1 relies on approved storage-level encryption at rest rather than SQL Server native certificate-based
  // backup encryption.
  StorageManaged = 1,

  // Native provider backup encryption. A declared EXTENSION POINT, not supported in V1: it requires managed
  // key material this architecture deliberately does not yet hold. Rejected by domain validation until the
  // key-management decision exists.
  ProviderNative = 2
}
