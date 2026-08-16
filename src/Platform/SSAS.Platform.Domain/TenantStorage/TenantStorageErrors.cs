using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.TenantStorage;

// Tenant-storage registry errors (ADR-017). Platform operational metadata, kept separate from both
// tenant-plane IdentityAccessErrors and platform-authority PlatformSupportErrors.
public static class TenantStorageErrors
{
  public static readonly Error ServerKeyRequired =
    new("TenantStorage.ServerKeyRequired", "A server key is required to register a tenant database.");

  public static readonly Error DatabaseNameRequired =
    new("TenantStorage.DatabaseNameRequired", "A database name is required to register a tenant database.");

  // CustomerManaged storage is dedicated to the authorized customer by definition; placing another
  // customer's tenant rows inside a customer-owned database is never an acceptable configuration.
  public static readonly Error CustomerManagedMustBeDedicated =
    new("TenantStorage.CustomerManagedMustBeDedicated", "A customer-managed tenant database must be dedicated.");

  public static readonly Error TenantRequired =
    new("TenantStorage.TenantRequired", "A valid tenant is required to assign a tenant database.");

  public static readonly Error TenantDatabaseRequired =
    new("TenantStorage.TenantDatabaseRequired", "A valid tenant database is required to create an assignment.");

  public static readonly Error RoutingVersionInvalid =
    new("TenantStorage.RoutingVersionInvalid", "A routing version must be greater than zero.");

  public static readonly Error AssignmentAlreadyEnded =
    new("TenantStorage.AssignmentAlreadyEnded", "The tenant database assignment has already been ended.");

  public static readonly Error AssignmentEndBeforeStart =
    new("TenantStorage.AssignmentEndBeforeStart", "A tenant database assignment cannot end before it was assigned.");

  // ---- Routing (TS-1C). Every one of these is a FAIL-CLOSED outcome: routing is refused rather than
  // satisfied from any other database (ADR-017 "No automatic fallback"). Messages never carry a connection
  // string, credential or raw SQL text.

  public static readonly Error TenantContextMissing =
    new("TenantStorage.TenantContextMissing", "A trusted tenant context is required to resolve tenant storage routing.");

  public static readonly Error ActiveAssignmentMissing =
    new("TenantStorage.ActiveAssignmentMissing", "The tenant has no active tenant database assignment.");

  // NOTE: there is deliberately no `AmbiguousActiveAssignment` or `TenantDatabaseMissing` error.
  // Both described conditions are structurally impossible — UX_TenantDatabaseAssignments_ActiveTenant
  // permits one active assignment per tenant, and the FK to TenantDatabase is Restrict — and neither was
  // reachable. Ambiguity surfaces instead as a hard failure from SingleOrDefaultAsync in the registry read,
  // which is the louder outcome; a declared error suggested a controlled Result path that did not exist.

  public static readonly Error TenantDatabaseNotReady =
    new("TenantStorage.TenantDatabaseNotReady", "The assigned tenant database is not ready to serve traffic.");

  public static readonly Error UnsupportedHostingMode =
    new("TenantStorage.UnsupportedHostingMode", "The assigned tenant database uses a hosting mode that is not supported yet.");

  public static readonly Error ServerKeyNotConfigured =
    new("TenantStorage.ServerKeyNotConfigured", "The tenant database server key is not present in trusted configuration.");

  // ---- Schema health and migration orchestration (ADR-018).

  public static readonly Error ConnectivityResultRequired =
    new("TenantStorage.ConnectivityResultRequired", "A completed connectivity check must record a definite result.");

  public static readonly Error MigrationIdentifierInvalid =
    new("TenantStorage.MigrationIdentifierInvalid", "The migration identifier is not valid.");

  public static readonly Error MigrationNotPermittedByManagementMode =
    new("TenantStorage.MigrationNotPermittedByManagementMode", "The tenant database's migration management mode does not permit the platform to apply migrations.");

  public static readonly Error MigrationAlreadyRunning =
    new("TenantStorage.MigrationAlreadyRunning", "A migration is already running for this tenant database.");

  public static readonly Error MigrationNotRunning =
    new("TenantStorage.MigrationNotRunning", "No migration is running for this tenant database.");

  public static readonly Error MigrationOwnershipNotAcquired =
    new("TenantStorage.MigrationOwnershipNotAcquired", "Migration ownership for this tenant database could not be acquired.");

  public static readonly Error MigrationOwnershipLost =
    new("TenantStorage.MigrationOwnershipLost", "Migration ownership for this tenant database was lost during the run.");

  public static readonly Error MigrationVerificationFailed =
    new("TenantStorage.MigrationVerificationFailed", "The tenant database's migration history did not reach the expected state after migrating.");

  // ---- Traffic gating (ADR-018). These deny ERP traffic; none of them falls back to another database.

  public static readonly Error TenantDatabaseUnavailable =
    new("TenantStorage.TenantDatabaseUnavailable", "The tenant database cannot currently be reached.");

  public static readonly Error DatabaseUpgradeRequired =
    new("TenantStorage.DatabaseUpgradeRequired", "The tenant database schema is not up to date for this application version.");

  public static readonly Error DatabaseUpgrading =
    new("TenantStorage.DatabaseUpgrading", "The tenant database is currently being migrated.");

  public static readonly Error SchemaHealthUnknown =
    new("TenantStorage.SchemaHealthUnknown", "The tenant database schema compatibility has not been verified.");

  public static readonly Error SchemaHealthStale =
    new("TenantStorage.SchemaHealthStale", "The tenant database schema compatibility check is too old to be trusted.");

  public static readonly Error DatabaseNameInvalid =
    new("TenantStorage.DatabaseNameInvalid", "The tenant database name is not valid for connection construction.");

  // ---- Backup and recovery (ADR-022). Messages are operator-facing and carry no destination, credential
  // or provider command text, for the same reason the routing errors above carry none.

  public static readonly Error BackupDestinationKeyRequired =
    new("TenantStorage.BackupDestinationKeyRequired", "A trusted backup destination key is required for a platform-executed backup policy.");

  public static readonly Error BackupDestinationKeyInvalid =
    new("TenantStorage.BackupDestinationKeyInvalid", "The backup destination key is not valid.");

  public static readonly Error BackupScheduleInvalid =
    new("TenantStorage.BackupScheduleInvalid", "A backup schedule interval must be a positive number of minutes within supported bounds.");

  // Transaction-log and differential backups restore only onto a full baseline (ADR-022 §9), so scheduling
  // them without one configures a chain with no base.
  public static readonly Error BackupFullBaselineRequired =
    new("TenantStorage.BackupFullBaselineRequired", "A backup policy requires a full baseline schedule; log or differential backups alone are not a backup strategy.");

  public static readonly Error BackupRetentionInvalid =
    new("TenantStorage.BackupRetentionInvalid", "The backup retention expectation must be a positive number of days within supported bounds.");

  public static readonly Error BackupVerificationIntervalInvalid =
    new("TenantStorage.BackupVerificationIntervalInvalid", "The restore verification interval must be a positive number of days within supported bounds.");

  public static readonly Error BackupEncryptionModeNotSupported =
    new("TenantStorage.BackupEncryptionModeNotSupported", "Provider-native backup encryption is not supported in this version.");

  public static readonly Error BackupOperationInvalid =
    new("TenantStorage.BackupOperationInvalid", "A backup operation requires a provider key and an operation code.");

  // A run is successful on reconciled provider evidence, never on a command having returned.
  public static readonly Error BackupProviderEvidenceRequired =
    new("TenantStorage.BackupProviderEvidenceRequired", "A successful backup run requires provider backup evidence.");

  public static readonly Error BackupArtifactReferenceInvalid =
    new("TenantStorage.BackupArtifactReferenceInvalid", "The backup artifact reference is not valid.");

  public static readonly Error BackupRunSizeInvalid =
    new("TenantStorage.BackupRunSizeInvalid", "A backup run size cannot be negative.");

  public static readonly Error BackupRunSkipStatusInvalid =
    new("TenantStorage.BackupRunSkipStatusInvalid", "The requested status is not a controlled backup skip outcome.");

  public static readonly Error BackupVerificationResultRequired =
    new("TenantStorage.BackupVerificationResultRequired", "A verification result is required to record backup verification.");

  public static readonly Error RecoveryReadinessResultRequired =
    new("TenantStorage.RecoveryReadinessResultRequired", "A recovery readiness evaluation must record a concluded status.");

  // ---- Backup execution (ADR-022, TS-Backup Phase B). These are operator-facing and never carry a
  // resolved destination, credential or provider command text.

  // No BACKUP identity is configured for this server. Deliberately distinct from ServerKeyNotConfigured:
  // a server may be routable without the platform being permitted or equipped to back it up, and falling
  // back to the runtime credential would be the exact reuse ADR-022 §11 forbids.
  public static readonly Error BackupServerNotConfigured =
    new("TenantStorage.BackupServerNotConfigured", "No backup authority is configured for the tenant database's server.");

  public static readonly Error BackupDestinationNotConfigured =
    new("TenantStorage.BackupDestinationNotConfigured", "The backup destination key is not present in trusted configuration.");

  public static readonly Error BackupCompressionNotSupported =
    new("TenantStorage.BackupCompressionNotSupported", "The policy requires backup compression, which this SQL Server edition does not support.");

  public static readonly Error BackupNotPermittedByManagementMode =
    new("TenantStorage.BackupNotPermittedByManagementMode", "The backup management mode does not permit the platform to execute this backup.");

  public static readonly Error BackupPolicyNotConfigured =
    new("TenantStorage.BackupPolicyNotConfigured", "No backup policy is configured for the tenant database.");

  public static readonly Error BackupPolicyDisabled =
    new("TenantStorage.BackupPolicyDisabled", "The tenant database's backup policy is disabled.");

  public static readonly Error BackupDatabaseStateUnsupported =
    new("TenantStorage.BackupDatabaseStateUnsupported", "The tenant database is not in a state that supports backup.");

  // SIMPLE recovery cannot produce a log backup at all. Reported rather than corrected: changing a recovery
  // model automatically would start log growth on a database that is by definition misconfigured.
  public static readonly Error BackupRecoveryModelUnsupported =
    new("TenantStorage.BackupRecoveryModelUnsupported", "The tenant database's recovery model does not support a transaction log backup.");

  public static readonly Error BackupBaselineMissing =
    new("TenantStorage.BackupBaselineMissing", "No full backup baseline exists for this database, so the requested operation has no base.");

  // The command returned but reconciled provider evidence was absent or did not match. NEVER a success.
  public static readonly Error BackupEvidenceMissing =
    new("TenantStorage.BackupEvidenceMissing", "The backup command completed but no matching provider evidence could be reconciled.");

  public static readonly Error BackupOwnershipLost =
    new("TenantStorage.BackupOwnershipLost", "Backup ownership of the tenant database was lost before the operation could be verified.");

  // Evidence WAS found and correlated, but fails a quality requirement — missing checksums, or copy-only
  // where the managed chain requires a base-resetting backup. Distinct from BackupEvidenceMissing so an
  // operator can tell "nothing was recorded" from "what was recorded is not acceptable".
  public static readonly Error BackupEvidenceRejected =
    new("TenantStorage.BackupEvidenceRejected", "The reconciled backup evidence did not satisfy the managed chain's requirements.");

  // The backup identity cannot see server-wide request state, so whether another backup is already running
  // against this database CANNOT BE DETERMINED. Deliberately distinct from a detected in-flight operation:
  // sys.dm_exec_requests silently narrows to the caller's own session rather than failing, so treating an
  // empty result as "nothing running" would be an assumption, not an observation (ADR-022 §14).
  public static readonly Error BackupInFlightVisibilityUnavailable =
    new("TenantStorage.BackupInFlightVisibilityUnavailable", "The backup identity lacks the server permission required to determine whether a backup is already in flight.");

  // Another platform worker already satisfied the scheduling decision this run was created for. Distinct
  // from ownership contention and from an in-flight operation: nothing is running now, and nothing failed —
  // the work was simply already done (ADR-022 §13, §14).
  // The execution was cancelled client-side before evidence could be reconciled. Recorded as a terminal
  // failure rather than left Running: Phase B established that cancelling the client does not reliably stop
  // a server-side BACKUP, so the honest statement is that this run cannot be shown to have succeeded.
  public static readonly Error BackupExecutionCancelled =
    new("TenantStorage.BackupExecutionCancelled", "The backup execution was cancelled before provider evidence could be reconciled.");

  public static readonly Error BackupSupersededByRecentRun =
    new("TenantStorage.BackupSupersededByRecentRun", "A platform-managed backup of this operation completed after the scheduling decision was made, so this run is redundant.");

  // ---- Restore verification (ADR-022 §17, TS-Backup Phase D). Operator-facing, and never carrying a
  // server name, path, credential or restored data.

  public static readonly Error RestoreVerificationBaselineRequired =
    new("TenantStorage.RestoreVerificationBaselineRequired", "A restore verification requires a successful full backup baseline to restore.");

  public static readonly Error RestoreVerificationServerKeyInvalid =
    new("TenantStorage.RestoreVerificationServerKeyInvalid", "The restore verification server key is not valid.");

  public static readonly Error RestoreVerificationDatabaseNameInvalid =
    new("TenantStorage.RestoreVerificationDatabaseNameInvalid", "The generated verification database name is not valid.");

  public static readonly Error RestoreVerificationNotAdmitted =
    new("TenantStorage.RestoreVerificationNotAdmitted", "The restore verification is not in an admitted state.");

  public static readonly Error RestoreVerificationNotRunning =
    new("TenantStorage.RestoreVerificationNotRunning", "The restore verification is not running.");

  public static readonly Error RestoreVerificationAlreadyCompleted =
    new("TenantStorage.RestoreVerificationAlreadyCompleted", "The restore verification has already reached a terminal result.");

  public static readonly Error RestoreVerificationCleanupStateInvalid =
    new("TenantStorage.RestoreVerificationCleanupStateInvalid", "The requested cleanup state is not a terminal cleanup outcome.");

  // Another application instance already holds the effective verification for this database's due state.
  // NOT a failure: the admission invariant worked exactly as intended (ADR-022 §17, compliance rule 43).
  public static readonly Error RestoreVerificationAlreadyAdmitted =
    new("TenantStorage.RestoreVerificationAlreadyAdmitted", "An effective restore verification is already admitted for this tenant database.");

  public static readonly Error RestoreVerificationNotDue =
    new("TenantStorage.RestoreVerificationNotDue", "The tenant database does not currently require a restore verification.");

  // The due state this decision was made against has since been satisfied by another successful verification.
  // NOT the same as ownership contention: nothing is running now, and nothing failed — the work was already
  // done (ADR-022 §17, compliance rule 43). Distinguished from AlreadyAdmitted because the two call for
  // different operator responses.
  public static readonly Error RestoreVerificationAlreadySatisfied =
    new("TenantStorage.RestoreVerificationAlreadySatisfied", "A successful restore verification completed after this decision was made, so this verification is redundant.");

  public static readonly Error RestoreVerificationReconciliationStale =
    new("TenantStorage.RestoreVerificationReconciliationStale", "The restore verification changed before reconciliation could apply its conservative terminal transition.");

  // The verification target is absent, unresolvable, or not trusted for this environment. FAILS CLOSED —
  // there is no fallback to the source database's server (ADR-022 §17, compliance rule 44).
  public static readonly Error RestoreVerificationServerNotConfigured =
    new("TenantStorage.RestoreVerificationServerNotConfigured", "No trusted restore verification server is configured for the requested key.");

  // The configured verification target is the server hosting the authoritative tenant database, and the
  // deployment has not declared itself non-production. Refused (ADR-022 §17, compliance rule 32).
  public static readonly Error RestoreVerificationTargetNotIsolated =
    new("TenantStorage.RestoreVerificationTargetNotIsolated", "The restore verification target must not be the server hosting the authoritative tenant database.");

  public static readonly Error RestoreVerificationNotConfigured =
    new("TenantStorage.RestoreVerificationNotConfigured", "Restore verification is not configured for this deployment.");

  // A generated verification name collided with a registered authoritative database. Refused rather than
  // resolved: nothing that could be a production database may be a restore target (ADR-022 §17).
  public static readonly Error RestoreVerificationTargetNameNotSafe =
    new("TenantStorage.RestoreVerificationTargetNameNotSafe", "The verification database name does not satisfy the platform's reserved verification vocabulary or collides with a registered tenant database.");

  // ---- Chain selection and restore execution (ADR-022 §17, TS-Backup Phase D5/D6).

  public static readonly Error RestoreChainBaselineMissing =
    new("TenantStorage.RestoreChainBaselineMissing", "No successful platform-managed full backup is available to restore.");

  // The platform's own artifacts cannot form the required restorable sequence — a segment is missing,
  // superseded by an externally taken backup, or expired. Reported rather than silently downgraded to a
  // shallower restore, so readiness degrades honestly (ADR-022 §17, compliance rule 37).
  public static readonly Error RestoreChainBroken =
    new("TenantStorage.RestoreChainBroken", "The platform-managed backup artifacts do not form a complete restorable sequence.");

  // A verification database already exists under the generated name. FAILS SAFE — never overwritten, never
  // dropped and retried (ADR-022 §17, compliance rule 38).
  public static readonly Error RestoreVerificationTargetAlreadyExists =
    new("TenantStorage.RestoreVerificationTargetAlreadyExists", "A database already exists under the generated verification name.");

  public static readonly Error RestoreVerificationArtifactUnavailable =
    new("TenantStorage.RestoreVerificationArtifactUnavailable", "A selected backup artifact could not be read from its trusted destination.");

  public static readonly Error RestoreVerificationRestoreFailed =
    new("TenantStorage.RestoreVerificationRestoreFailed", "The selected restore sequence did not complete.");

  // The restore completed but the database did not come online, so nothing was demonstrated.
  public static readonly Error RestoreVerificationDatabaseNotOnline =
    new("TenantStorage.RestoreVerificationDatabaseNotOnline", "The restored verification database did not come online.");

  // Something the admission decision depended on changed before execution began. Fails closed rather than
  // restoring against drifted state.
  public static readonly Error RestoreVerificationTargetDrifted =
    new("TenantStorage.RestoreVerificationTargetDrifted", "The verification target or its admitted operation changed before the restore began.");

  // ---- Post-restore probes (ADR-022 §17, TS-Backup Phase D7).

  // The restored database came online but carries no readable tenant migration history. Whatever restored,
  // it is not a recoverable tenant database.
  public static readonly Error RestoreVerificationMigrationHistoryUnreadable =
    new("TenantStorage.RestoreVerificationMigrationHistoryUnreadable", "The restored verification database has no readable tenant migration history.");

  // The restored schema is at a position the deployed application does not recognise, so its lineage
  // diverged from the tenant migration catalog.
  public static readonly Error RestoreVerificationSchemaPositionUnexpected =
    new("TenantStorage.RestoreVerificationSchemaPositionUnexpected", "The restored verification database is not at a schema position this application recognises.");

  // The restore reached a shallower recovery path than the active policy requires, so the deeper capability
  // the policy claims was never exercised (ADR-022 §17, v1.2).
  public static readonly Error RestoreVerificationDepthNotAchieved =
    new("TenantStorage.RestoreVerificationDepthNotAchieved", "The restore did not reach the recovery depth the active policy requires.");

  // Eligibility changed between admission and execution — hosting mode, policy, authority or the admitted
  // operation itself. Fails closed rather than restoring against drifted state.
  // The selected baseline predates checkpoint-LSN capture, so differential applicability cannot be decided.
  // DISTINCT FROM A CHAIN BREAK: the platform has not established that SQL Server's chain is broken, only
  // that it cannot tell from its own records (TS-Backup Phase D7).
  public static readonly Error RestoreChainMetadataUnavailable =
    new("TenantStorage.RestoreChainMetadataUnavailable", "The selected full backup lacks the checkpoint metadata required to establish differential applicability.");

  public static readonly Error RestoreVerificationNotEligible =
    new("TenantStorage.RestoreVerificationNotEligible", "The tenant database is no longer eligible for platform restore verification.");

  // ---- Recovery-gated Dedicated activation (ADR-017 cutover ordering, ADR-022 §18, TS-Storage Phase E).
  //
  // ONE ERROR PER REFUSAL, because each one sends an operator somewhere different. Collapsing them into a
  // single "not ready" would leave the most common case — a verification that ran against a full backup that
  // has since been superseded — indistinguishable from a database with no backups at all.

  public static readonly Error RecoveryActivationEvidenceUnavailable =
    new("TenantStorage.RecoveryActivationEvidenceUnavailable", "No recovery activation evidence exists for the requested tenant database.");

  public static readonly Error RecoveryActivationReadinessUnknown =
    new("TenantStorage.RecoveryActivationReadinessUnknown", "Recovery readiness has not been established for this tenant database, so activation cannot be authorised.");

  public static readonly Error RecoveryActivationUnprotected =
    new("TenantStorage.RecoveryActivationUnprotected", "The tenant database has no usable recovery position, so activation is refused.");

  public static readonly Error RecoveryActivationDegraded =
    new("TenantStorage.RecoveryActivationDegraded", "The tenant database's recovery evidence is degraded, so activation is refused.");

  public static readonly Error RecoveryActivationRecoveryModelInvalid =
    new("TenantStorage.RecoveryActivationRecoveryModelInvalid", "The tenant database's recovery model cannot support the protection its policy requires, so activation is refused.");

  public static readonly Error RecoveryActivationVerificationOverdue =
    new("TenantStorage.RecoveryActivationVerificationOverdue", "The tenant database's restore verification is overdue under the active policy, so activation is refused.");

  public static readonly Error RecoveryActivationBaselineUnavailable =
    new("TenantStorage.RecoveryActivationBaselineUnavailable", "No current full backup baseline could be identified for the tenant database, so activation is refused.");

  // PROTECTED IS NOT SUFFICIENT. Where a policy sets no verification interval, `Protected` is reached
  // without any restore ever having been performed (ADR-022 §6) — and moving live traffic onto a database
  // whose recoverability has never been demonstrated is exactly what this gate exists to prevent.
  public static readonly Error RecoveryActivationRestoreVerificationRequired =
    new("TenantStorage.RecoveryActivationRestoreVerificationRequired", "No successful restore verification exists for the tenant database, so activation is refused.");

  public static readonly Error RecoveryActivationRestoreVerificationSuperseded =
    new("TenantStorage.RecoveryActivationRestoreVerificationSuperseded", "The successful restore verification exercised a superseded baseline that is no longer the current recovery path, so activation is refused.");

  public static readonly Error RecoveryActivationRestoreVerificationDepthInsufficient =
    new("TenantStorage.RecoveryActivationRestoreVerificationDepthInsufficient", "The successful restore verification did not exercise the recovery depth the active policy requires, so activation is refused.");

  public static readonly Error RecoveryActivationRestoreVerificationStale =
    new("TenantStorage.RecoveryActivationRestoreVerificationStale", "The successful restore verification has aged past the interval the active policy requires, so activation is refused.");

  // ---- Shared → Dedicated cutover operation and tenant write freeze (ADR-020, TS-Storage Phase E1).

  public static readonly Error ActorRequired =
    new("TenantStorage.ActorRequired", "An actor is required to record a tenant storage operation.");

  public static readonly Error CutoverTargetNotEligible =
    new("TenantStorage.CutoverTargetNotEligible", "The cutover target must be a distinct platform-managed dedicated tenant database.");

  public static readonly Error CutoverSourceNotEligible =
    new("TenantStorage.CutoverSourceNotEligible", "The cutover source must be the platform-managed database the tenant is currently assigned to.");

  public static readonly Error CutoverOperationNotFound =
    new("TenantStorage.CutoverOperationNotFound", "No cutover operation exists for the requested identifier.");

  public static readonly Error CutoverOperationNotPreparing =
    new("TenantStorage.CutoverOperationNotPreparing", "The cutover operation is not in a state that permits this freeze transition.");

  public static readonly Error CutoverAlreadyActive =
    new("TenantStorage.CutoverAlreadyActive", "An active cutover operation already exists for this tenant.");

  public static readonly Error CutoverAlreadyFlipped =
    new("TenantStorage.CutoverAlreadyFlipped", "Routing has already moved to the cutover target, so the source freeze cannot be released.");

  // THE DRAIN BUDGET EXPIRED. Deliberately distinct from a frozen refusal: nothing was frozen, and the
  // operation is terminal rather than retryable in place.
  public static readonly Error CutoverFreezeTimedOut =
    new("TenantStorage.CutoverFreezeTimedOut", "Existing tenant writes did not drain within the configured cutover freeze timeout.");

  // What an application write sees while a cutover holds the tenant. A CONTROLLED, VISIBLE maintenance
  // outcome rather than a generic error (ADR-020 freeze failure safety).
  public static readonly Error TenantWritesFrozen =
    new("TenantStorage.TenantWritesFrozen", "Writes for this tenant are temporarily frozen by an in-progress storage cutover.");

  // ---- Version-aware routing (ADR-020 "Resolver cache", TS-Storage Phase E2).

  // THE AUTHORITATIVE ROUTING VERSION COULD NOT BE ESTABLISHED, so no cached route can be shown to still be
  // current. Deliberately NOT ActiveAssignmentMissing (which states the tenant has no assignment — a fact
  // this outcome specifically failed to determine) and NOT TenantContextMissing (which is about the caller,
  // not the registry). Conflating them would send an operator to look at the tenant's configuration when the
  // Platform database is what is unreachable.
  //
  // It exists so that "cannot check" cannot be reported as "unchanged": serving a remembered route here is
  // how a tenant's writes land in the pre-cutover database.
  public static readonly Error RoutingVersionUnavailable =
    new("TenantStorage.RoutingVersionUnavailable", "The authoritative tenant routing version could not be read, so routing is refused rather than served from cache.");
}
