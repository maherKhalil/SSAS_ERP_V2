using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// A flat projection of one recorded backup operation.
//
// The operation is carried as the PROVIDER-SCOPED pair it is stored as, never mapped onto a universal
// Full/Differential/Log enum (ADR-022 §10) — flattening it here would reintroduce, in the Application layer,
// exactly the coupling the domain model avoids.
public sealed record TenantDatabaseBackupRunRecord(
  long TenantDatabaseBackupRunId,
  long TenantDatabaseId,
  string OperationProviderKey,
  string OperationCode,
  TenantDatabaseBackupRunStatus Status,
  DateTimeOffset StartedUtc,
  DateTimeOffset? CompletedUtc,
  string? DestinationKey,
  string? ArtifactReference,
  string? ProviderBackupIdentity,
  long? SizeBytes,
  TenantDatabaseBackupVerificationState VerificationState,
  DateTimeOffset? LastVerifiedUtc,
  string? ErrorSummary);
