using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
  // THE ROUTING-VERSION MONOTONICITY GUARD (ADR-020, TS-Storage Phase E4).
  //
  // WHY A TRIGGER, HAVING DELIBERATELY AVOIDED ONE THROUGH E1-E3. RoutingVersion is the correctness
  // mechanism the whole resolver cache rests on: a cached route is valid exactly while the authoritative
  // version still matches it. A routing change that failed to advance the version would make every stale
  // cached route in the estate valid again -- the precise failure E2 exists to prevent.
  //
  // The invariant is "a tenant's new assignment must carry a version greater than every assignment that
  // tenant has ever held". That is a statement ACROSS ROWS, and SQL Server's declarative constraints cannot
  // express it: CHECK is row-scoped, UNIQUE cannot express ordering, and a foreign key relates rows without
  // comparing them. A trigger is the only durable mechanism -- and durability is the requirement, because
  // the application service is not the only thing that can write this table.
  //
  // IT IS A GUARD, NEVER A GENERATOR. It rejects; it never assigns or increments RoutingVersion itself. A
  // trigger that supplied the next version would hide a caller that forgot to advance it, and would make
  // the flip's behaviour depend on something invisible at the call site.
  //
  // SET-BASED AND MULTI-ROW SAFE. Each check is one EXISTS over inserted/deleted: nothing iterates, nothing
  // assumes a single row, and nothing depends on the order rows are processed in.
  //
  // NO DELETE EVENT. Assignments are retained as history and never deleted, so there is no routing-
  // significant delete to guard; declaring the event anyway would claim protection that was never designed.
  //
  // The model records this table as triggered (see TenantDatabaseAssignmentConfiguration). That is metadata
  // rather than DDL, and it is required: SQL Server refuses `OUTPUT` without `INTO` against a table with an
  // enabled trigger, which is exactly how EF reads back this table's generated identity.
  /// <inheritdoc />
  public partial class AddRoutingVersionGuard : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      ArgumentNullException.ThrowIfNull(migrationBuilder);

      migrationBuilder.Sql("""
        CREATE TRIGGER [platform].[TR_TenantDatabaseAssignments_EnforceRoutingVersion]
        ON [platform].[TenantDatabaseAssignments]
        AFTER INSERT, UPDATE
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
        END
        """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      ArgumentNullException.ThrowIfNull(migrationBuilder);

      migrationBuilder.Sql(
        "DROP TRIGGER IF EXISTS [platform].[TR_TenantDatabaseAssignments_EnforceRoutingVersion];");
    }
  }
}
