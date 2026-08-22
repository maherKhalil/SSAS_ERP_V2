using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    // EVERY EMPLOYEE GETS A POSITION, AND THE MIGRATION ASSERTS THE FACT THAT LICENSES THAT
    // (BR-HR-0006, OD-POS-001, DEC-POS-0026).
    //
    // ================================================================================================
    // THE COUNT CHECK RUNS FIRST, BEFORE ANY DDL, EVERY TIME, IN EVERY TENANT DATABASE.
    // ================================================================================================
    //
    // `OD-POS-001` ruled `PositionId` `NOT NULL` from day one with **no backfill**. That ruling is correct
    // ONLY while no Employee rows exist — an operational fact about a moment in time, asserted here about
    // every database this runs against rather than about the one the owner was asked about.
    //
    // A tenant provisioned between the ruling and the upgrade, a restored database, a demo catalog, or a
    // customer-managed database under `ADR-021` could each hold rows the ruling never contemplated. So the
    // fact is CHECKED, not assumed, and it is checked as a separate pass before anything is written — the
    // `DEC-DEP-0009` collision-pass shape exactly, so the common failure never writes at all rather than
    // relying on rollback to undo it.
    //
    // ---- THE FOUR ACCOMMODATIONS ARE STRUCTURALLY ABSENT, NOT MERELY UNUSED.
    //
    // The scaffolded form of this migration was:
    //
    //     AddColumn<Guid>("PositionId", nullable: false, defaultValue: Guid.Empty)
    //
    // which is `DEC-POS-0026`'s first forbidden accommodation written by a tool. On a populated database it
    // SUCCEEDS and stamps every existing employee with a position nobody chose — an all-zeros identifier
    // pointing at no position at all — and no later migration could know what those employees meant. The
    // default is removed: on the empty database the ruling describes, `ADD … NOT NULL` needs none.
    //
    // There is likewise no `DELETE`, no skip, and no degrade-to-nullable anywhere below. None of the four is
    // reachable by editing a flag, because none of them is written.
    //
    // ---- AND THE MESSAGE TELLS THE OPERATOR WHAT TO DO.
    //
    // It names the database, the row count found, and the recorded decision — so whoever reads a failed
    // deployment log reads the REASONING rather than guessing at a constraint violation. The one remedy is
    // stated and it is not "edit this migration": a tenant holding employees predates the ruling's premise,
    // and the backfill strategy `OD-POS-001` declined has to be reconsidered for that tenant by someone
    // entitled to decide it.
    //
    // Proven by `TS-POS-0043`: a database with an Employees row refuses this migration with this message.
    /// <inheritdoc />
    public partial class AddEmployeePosition : Migration
    {
        // Hoisted per the repo convention (CA1861): a constant array argument allocates on every call, and
        // the analyzer is Release-only — which is why the standing Release-build gate exists.
        private static readonly string[] ScopedPositionColumns = ["TenantId", "CompanyId", "PositionId"];

        // ---- THE ASSERTION, AS ITS OWN PASS.
        //
        // `THROW` inside the migration transaction is what makes the failure safe: EF runs a migration's
        // statements in one transaction, so nothing this script did survives the abort. Severity 16 aborts
        // the batch under `XACT_ABORT`, and a migration runner surfaces it as an ordinary error.
        //
        // `DB_NAME()` rather than a parameter: the point is to name the database that actually failed, which
        // only the server running the statement knows. An operator upgrading forty tenants needs to be told
        // WHICH one stopped.
        private const string EmptinessAssertionSql = """
            SET NOCOUNT ON;
            SET XACT_ABORT ON;

            DECLARE @employees bigint = (SELECT COUNT_BIG(*) FROM [tenant].[Employees]);

            IF @employees <> 0
            BEGIN
                DECLARE @message nvarchar(max) =
                    N'FP-008 employee position migration STOPPED in database [' + DB_NAME() + N']. ' +
                    N'It found ' + CONVERT(nvarchar(20), @employees) + N' row(s) in [tenant].[Employees], ' +
                    N'and FP-008 DEC-POS-0009 / OD-POS-001 authorised a NOT NULL PositionId with NO ' +
                    N'BACKFILL only on the operational fact that no employees existed. ' +
                    N'This migration will not supply a default position, will not delete employees, will ' +
                    N'not skip the column and will not degrade it to nullable: each would silently attach ' +
                    N'real people to a position nobody chose, and no later migration could know what was ' +
                    N'meant. ' +
                    N'REMEDY: this tenant predates the ruling''s premise, so the backfill strategy ' +
                    N'OD-POS-001 declined must be reconsidered for it by the architect. Do NOT edit this ' +
                    N'migration to force it through. No changes have been applied.';

                THROW 50009, @message, 1;
            END;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // STEP 1. THE FACT, BEFORE ANY DDL. Nothing below this line runs on a populated database.
            migrationBuilder.Sql(EmptinessAssertionSql);

            // STEP 2. THE COLUMN, REQUIRED, WITH NO DEFAULT.
            //
            // `ADD … NOT NULL` without a default is legal on an empty table and refused on a populated one.
            // That refusal is a second line of defence rather than the plan: step 1 has already stopped with
            // a message an operator can act on, and this would only be reached if the table gained rows
            // between the two statements of one transaction.
            migrationBuilder.AddColumn<Guid>(
                name: "PositionId",
                schema: "tenant",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_PositionId",
                schema: "tenant",
                table: "Employees",
                column: "PositionId");

            // The scoped lookup index. Leading keys match the mandatory predicate order — tenant, then
            // company — so a position-filtered employee search cannot be served by a plan that skipped a
            // scope column (`NFR-POS-0301`), and the holder count uses the same shape.
            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_CompanyId_PositionId",
                schema: "tenant",
                table: "Employees",
                columns: ScopedPositionColumns);

            // ---- THE EDGE THAT ORDERS POSITIONS BEFORE EMPLOYEES IN THE CUTOVER COPY.
            //
            // RESTRICT, matching every other relationship in this module: a position is deactivated, never
            // deleted (`BRULE-POS-0012`), and a cascade here would erase employment records along with an
            // org-structure change.
            //
            // Intra-catalog: both tables live in the tenant database, so this crosses no database boundary
            // (`ADR-017`).
            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Positions_PositionId",
                schema: "tenant",
                table: "Employees",
                column: "PositionId",
                principalSchema: "tenant",
                principalTable: "Positions",
                principalColumn: "PositionId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Positions_PositionId",
                schema: "tenant",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_PositionId",
                schema: "tenant",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_TenantId_CompanyId_PositionId",
                schema: "tenant",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PositionId",
                schema: "tenant",
                table: "Employees");
        }
    }
}
