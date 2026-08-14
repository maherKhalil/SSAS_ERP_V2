namespace SSAS.Platform.Domain.Enums;

// Outcome and progress of the last migration attempt against a physical tenant database (ADR-018).
//
// Separate from schema compatibility because they answer different questions: compatibility is a property
// of the database right now, execution is a property of what the orchestrator last did to it. A database
// can be UpToDate with a Failed last attempt (a later run succeeded), or PendingMigrations while Migrating.
public enum TenantDatabaseMigrationExecutionStatus
{
  Idle = 1,

  // A run currently owns this database. ADR-018 gating DENIES traffic while migrating: schema is changing
  // underneath any request that would be served.
  Migrating = 2,

  Succeeded = 3,

  Failed = 4,

  // Pending migrations detected on a database the platform is not permitted to migrate (CustomerDba, or
  // PlatformAfterApproval without approval). ADR-018 requires this be reported as its own category and NOT
  // as a failure — a release is not broken because a customer has not yet acted.
  BlockedPendingCustomer = 5
}
