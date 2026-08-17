using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
  // TWO E5 DATABASE CHANGES, BOTH CLOSING A HOLE FOUND BY TESTING (ADR-020, TS-Storage Phase E5).
  //
  // 1. THE ROUTING GUARD NOW COVERS DELETE (E4 review LOW-2). The E4 trigger enforced monotonicity on
  //    INSERT and immutability on UPDATE, which left one escape: a direct SQL actor could DELETE a tenant's
  //    assignment history and re-insert at RoutingVersion 1. The insert check can only compare against rows
  //    that still exist, so with the history gone the reset looked legal — and a reset version makes every
  //    stale cached route in the estate valid again, which is the failure the guard exists to prevent.
  //    Assignment history is the record of where a tenant's data has lived; the moment anyone wants it gone
  //    is the moment it matters most.
  //
  // 2. THE WRITE-GATE INDEX. Since E5 the tenant write fence must keep refusing a stale source-bound
  //    context after orchestration reaches Completed, so its lookup spans Completed operations —
  //    which UX_TenantCutoverOperations_ActiveTenant deliberately excludes. Without this index that lookup
  //    scans the whole cutover history on EVERY tenant write. It is filtered to exactly the statuses the
  //    fence asks about and ordered so the newest operation is the first row of the seek.
  //
  // The trigger is REPLACED rather than supplemented: a second overlapping routing trigger would make
  // firing order and interaction something a reader has to work out, where one guard states the whole rule.
  /// <inheritdoc />
  public partial class StrengthenRoutingGuardAndWriteGate : Migration
  {
    // TenantId leads so the fence's per-tenant lookup is a seek; the identity follows so the newest
    // operation is the first row read.
    private static readonly string[] WriteGateColumns = ["TenantId", "TenantCutoverOperationId"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      ArgumentNullException.ThrowIfNull(migrationBuilder);

      migrationBuilder.CreateIndex(
          name: "IX_TenantCutoverOperations_WriteGate",
          schema: "platform",
          table: "TenantCutoverOperations",
          columns: WriteGateColumns,
          filter: "[Status] IN (N'Frozen', N'RoutingFlipped', N'Completed')");

      migrationBuilder.Sql(
        "DROP TRIGGER IF EXISTS [platform].[TR_TenantDatabaseAssignments_EnforceRoutingVersion];");

      migrationBuilder.Sql("""
        CREATE TRIGGER [platform].[TR_TenantDatabaseAssignments_EnforceRoutingVersion]
        ON [platform].[TenantDatabaseAssignments]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
          SET NOCOUNT ON;

          -- A NEW assignment must advance past EVERY assignment this tenant has ever held, active or ended.
          -- Comparing only against the currently active row would let a version be reused after one was
          -- ended, and a reused version is indistinguishable from the one a stale cache still holds.
          IF EXISTS (
            SELECT 1
            FROM inserted AS i
            WHERE NOT EXISTS (
                    SELECT 1 FROM deleted AS d
                    WHERE d.[TenantDatabaseAssignmentId] = i.[TenantDatabaseAssignmentId])
              AND EXISTS (
                    SELECT 1
                    FROM [platform].[TenantDatabaseAssignments] AS a
                    WHERE a.[TenantId] = i.[TenantId]
                      AND a.[TenantDatabaseAssignmentId] <> i.[TenantDatabaseAssignmentId]
                      AND a.[RoutingVersion] >= i.[RoutingVersion]))
          BEGIN
            THROW 51020, N'A new tenant database assignment must advance the tenant routing version beyond every previous assignment.', 1;
          END

          -- An EXISTING assignment's routing identity is immutable. Re-pointing a live assignment at another
          -- database, rewriting its version, or reviving an ended one would move a tenant's data with no
          -- version change at all -- invisible to every resolver cache in the estate. Ending an assignment
          -- (EndedUtc NULL -> NOT NULL) remains permitted: that is how the flip vacates the active slot.
          IF EXISTS (
            SELECT 1
            FROM inserted AS i
            JOIN deleted AS d ON d.[TenantDatabaseAssignmentId] = i.[TenantDatabaseAssignmentId]
            WHERE i.[RoutingVersion] <> d.[RoutingVersion]
               OR i.[TenantId] <> d.[TenantId]
               OR i.[TenantDatabaseId] <> d.[TenantDatabaseId]
               OR (d.[EndedUtc] IS NOT NULL AND i.[EndedUtc] IS NULL))
          BEGIN
            THROW 51021, N'A tenant database assignment routing identity is immutable; end it and create a replacement at a higher routing version.', 1;
          END

          -- ROUTING HISTORY MAY NOT BE PHYSICALLY DELETED. A deleted row with no inserted row of the same
          -- identity is a DELETE; one WITH a matching inserted row is an UPDATE, already judged above. That
          -- distinction is what makes this correct for mixed DML — a MERGE combining updates and deletes is
          -- rejected for its deleted rows alone, and the whole statement rolls back with it.
          IF EXISTS (
            SELECT 1
            FROM deleted AS d
            WHERE NOT EXISTS (
                    SELECT 1 FROM inserted AS i
                    WHERE i.[TenantDatabaseAssignmentId] = d.[TenantDatabaseAssignmentId]))
          BEGIN
            THROW 51022, N'Tenant database assignment history cannot be physically deleted; it is the record of where a tenant''s data has lived.', 1;
          END
        END
        """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      ArgumentNullException.ThrowIfNull(migrationBuilder);

      migrationBuilder.DropIndex(
          name: "IX_TenantCutoverOperations_WriteGate",
          schema: "platform",
          table: "TenantCutoverOperations");

      // Restores the E4 guard exactly, so a down-migration weakens DELETE protection and nothing else.
      migrationBuilder.Sql(
        "DROP TRIGGER IF EXISTS [platform].[TR_TenantDatabaseAssignments_EnforceRoutingVersion];");

      migrationBuilder.Sql("""
        CREATE TRIGGER [platform].[TR_TenantDatabaseAssignments_EnforceRoutingVersion]
        ON [platform].[TenantDatabaseAssignments]
        AFTER INSERT, UPDATE
        AS
        BEGIN
          SET NOCOUNT ON;

          IF EXISTS (
            SELECT 1
            FROM inserted AS i
            WHERE NOT EXISTS (
                    SELECT 1 FROM deleted AS d
                    WHERE d.[TenantDatabaseAssignmentId] = i.[TenantDatabaseAssignmentId])
              AND EXISTS (
                    SELECT 1
                    FROM [platform].[TenantDatabaseAssignments] AS a
                    WHERE a.[TenantId] = i.[TenantId]
                      AND a.[TenantDatabaseAssignmentId] <> i.[TenantDatabaseAssignmentId]
                      AND a.[RoutingVersion] >= i.[RoutingVersion]))
          BEGIN
            THROW 51020, N'A new tenant database assignment must advance the tenant routing version beyond every previous assignment.', 1;
          END

          IF EXISTS (
            SELECT 1
            FROM inserted AS i
            JOIN deleted AS d ON d.[TenantDatabaseAssignmentId] = i.[TenantDatabaseAssignmentId]
            WHERE i.[RoutingVersion] <> d.[RoutingVersion]
               OR i.[TenantId] <> d.[TenantId]
               OR i.[TenantDatabaseId] <> d.[TenantDatabaseId]
               OR (d.[EndedUtc] IS NOT NULL AND i.[EndedUtc] IS NULL))
          BEGIN
            THROW 51021, N'A tenant database assignment routing identity is immutable; end it and create a replacement at a higher routing version.', 1;
          END
        END
        """);
    }
  }
}
