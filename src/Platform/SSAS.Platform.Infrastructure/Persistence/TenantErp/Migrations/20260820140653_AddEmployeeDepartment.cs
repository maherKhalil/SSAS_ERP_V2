using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeDepartment : Migration
    {
        private static readonly string[] EmployeeDepartmentColumns = ["TenantId", "CompanyId", "DepartmentId"];

        // ============================================================================================
        // THE LEGACY BACKFILL (FP-007 Phase 3 §17-§21, OD-DEP-001).
        // ============================================================================================
        //
        // Every employee that existed before FP-007 has no department, and the final schema has no room for
        // one without. So this migration gives each AFFECTED COMPANY exactly one department to put them in,
        // moves them into it, and records that department tracking began — then makes the column NOT NULL.
        //
        // ---- WHY THE COLUMN IS TEMPORARILY NULLABLE.
        //
        // The scaffolded version added it NOT NULL with a Guid.Empty default, which would have pointed every
        // legacy employee at a department that does not exist and then failed the foreign key — or worse,
        // succeeded before the key was added and left the rows permanently dangling. The nullable window
        // exists for the length of this script and closes inside it: step 9 asserts nothing is left null
        // before step 10 forbids it.
        //
        // ---- WHY A COLLISION IS A HARD FAILURE (§18, OD-DEP-001 as approved).
        //
        // If a company already has a department whose NormalizedCode is UNASSIGNED, this migration STOPS.
        // It does not reuse that department, rename it, delete it, modify it, add a suffix, or pick another
        // code. Each of those would silently attach real employees to a department a customer created for
        // their own purposes, and none of them can be undone by a later migration that cannot know what the
        // customer meant. An operator renaming one department is a smaller cost than a tenant discovering
        // their "Unassigned" cost centre quietly acquired 400 people.
        //
        // THROW inside the migration transaction is what makes that safe: EF runs a migration's statements
        // in one transaction, so the create-and-backfill work already done for earlier companies rolls back
        // with it and the database is left exactly as it was.
        private const string BackfillSql = """
            SET NOCOUNT ON;
            SET XACT_ABORT ON;

            -- ---- STEP 3. THE COLLISION CHECK, BEFORE ANYTHING IS WRITTEN FOR ANY COMPANY.
            --
            -- Deliberately a SEPARATE pass over every affected company rather than a per-company check
            -- inside the loop. A migration that created UNASSIGNED for companies A and B and only then
            -- discovered a collision in company C would be relying on the rollback to undo two companies'
            -- worth of writes. Checking everything first means the common failure never writes at all.
            DECLARE @collision nvarchar(max) =
            (
                SELECT STRING_AGG(CONVERT(nvarchar(36), conflicted.CompanyId), N', ')
                FROM
                (
                    SELECT DISTINCT department.CompanyId
                    FROM [tenant].[Departments] AS department
                    WHERE department.NormalizedCode = N'UNASSIGNED'
                      AND EXISTS
                      (
                          SELECT 1
                          FROM [tenant].[Employees] AS employee
                          WHERE employee.CompanyId = department.CompanyId
                            AND employee.TenantId = department.TenantId
                            AND employee.DepartmentId IS NULL
                      )
                ) AS conflicted
            );

            -- ---- STEP 4. FAIL LOUDLY, AND SAY WHAT TO DO ABOUT IT.
            --
            -- The message names the companies and the one remedy, because an operator reading a failed
            -- deployment log needs to act rather than investigate. State 16 is an ordinary error a
            -- migration runner surfaces; severity 16 aborts the batch under XACT_ABORT.
            IF @collision IS NOT NULL
            BEGIN
                DECLARE @message nvarchar(max) = N'FP-007 department migration STOPPED. ' +
                    N'These companies already have a department with NormalizedCode = ''UNASSIGNED'' and ' +
                    N'also have employees awaiting backfill: ' + @collision + N'. ' +
                    N'The migration will not reuse, rename, modify or delete an existing department, and ' +
                    N'will not choose another code. Rename the existing UNASSIGNED department in each ' +
                    N'company listed above, then run this migration again. No changes have been applied.';

                THROW 50007, @message, 1;
            END;

            -- ---- STEP 5. ONE MIGRATION DEPARTMENT PER AFFECTED COMPANY, AND ONLY AFFECTED ONES.
            --
            -- A company with no legacy employees gets nothing: creating an empty UNASSIGNED department in
            -- every company would put a permanent artefact of a one-time migration into tenants that never
            -- needed it. The EXISTS clause is what keeps this proportionate to the actual problem.
            --
            -- NEWID() rather than a deterministic identifier: these rows are ordinary departments from the
            -- moment they exist, and a predictable primary key would buy nothing.
            DECLARE @created TABLE (TenantId uniqueidentifier, CompanyId uniqueidentifier, DepartmentId uniqueidentifier);

            INSERT INTO [tenant].[Departments]
                ([DepartmentId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Name],
                 [ParentDepartmentId], [Status], [StatusChangedUtc], [StatusChangedBy],
                 [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
            OUTPUT inserted.[TenantId], inserted.[CompanyId], inserted.[DepartmentId] INTO @created
            SELECT
                NEWID(),
                affected.TenantId,
                affected.CompanyId,
                N'UNASSIGNED',
                N'UNASSIGNED',
                N'Unassigned',
                NULL,
                N'Active',
                SYSDATETIMEOFFSET(),
                N'fp-007-department-migration',
                SYSDATETIMEOFFSET(),
                N'fp-007-department-migration',
                SYSDATETIMEOFFSET(),
                N'fp-007-department-migration'
            FROM
            (
                -- ---- STEP 2. THE AFFECTED SET: companies that actually have employees without a
                -- department. Grouped rather than DISTINCT-per-row so exactly one department is created
                -- per company however many employees it has.
                SELECT employee.TenantId, employee.CompanyId
                FROM [tenant].[Employees] AS employee
                WHERE employee.DepartmentId IS NULL
                GROUP BY employee.TenantId, employee.CompanyId
            ) AS affected;

            -- ---- STEP 6. THE BACKFILL. Joined on BOTH dimensions, so an employee is never moved into
            -- another tenant's or another company's department even if identifiers were to collide.
            UPDATE employee
            SET employee.[DepartmentId] = created.DepartmentId
            FROM [tenant].[Employees] AS employee
            INNER JOIN @created AS created
                ON created.TenantId = employee.TenantId
               AND created.CompanyId = employee.CompanyId
            WHERE employee.[DepartmentId] IS NULL;

            -- ---- STEP 7. ONE INITIAL HISTORY ROW EACH, AND NOT ONE FABRICATED FACT (§21).
            --
            -- SourceDepartmentId is NULL because there was no previous department — not because the
            -- previous one is unknown. That is the same shape the application writes for a new hire, and it
            -- says exactly what is true: department tracking begins here.
            --
            -- EffectiveFromUtc is the MIGRATION INSTANT, not the employment date. Backdating it to the hire
            -- would assert the employee had been in this department since then, which is a historical claim
            -- this migration has no basis for and cannot be walked back once written.
            --
            -- One timestamp for the whole batch rather than SYSDATETIMEOFFSET() per row: every one of these
            -- rows records the same event, and a spread of instants would imply a sequence that did not
            -- happen.
            DECLARE @migratedUtc datetimeoffset = SYSDATETIMEOFFSET();

            INSERT INTO [tenant].[EmployeeDepartmentAssignments]
                ([EmployeeDepartmentAssignmentId], [TenantId], [CompanyId], [EmployeeId],
                 [SourceDepartmentId], [DestinationDepartmentId], [EffectiveFromUtc], [ChangedBy],
                 [ReasonCode], [ReasonText], [CreatedUtc], [CreatedBy])
            SELECT
                NEWID(),
                employee.[TenantId],
                employee.[CompanyId],
                employee.[EmployeeId],
                NULL,
                created.DepartmentId,
                @migratedUtc,
                N'fp-007-department-migration',
                NULL,
                NULL,
                @migratedUtc,
                N'fp-007-department-migration'
            FROM [tenant].[Employees] AS employee
            INNER JOIN @created AS created
                ON created.TenantId = employee.TenantId
               AND created.CompanyId = employee.CompanyId
            WHERE employee.[DepartmentId] = created.DepartmentId
              AND NOT EXISTS
              (
                  -- Idempotent against a partially applied earlier attempt: an employee that somehow
                  -- already has history is not given a second initial record.
                  SELECT 1
                  FROM [tenant].[EmployeeDepartmentAssignments] AS existing
                  WHERE existing.[EmployeeId] = employee.[EmployeeId]
              );

            -- ---- STEP 8. VERIFY BEFORE TIGHTENING.
            --
            -- The ALTER COLUMN in step 9 would fail on its own if anything were still null, but it would
            -- fail with a generic message about the column. This fails with one that says what actually
            -- went wrong, and it fails BEFORE the schema change is attempted.
            IF EXISTS (SELECT 1 FROM [tenant].[Employees] WHERE [DepartmentId] IS NULL)
            BEGIN
                THROW 50008,
                    N'FP-007 department migration STOPPED. Employees remain without a department after the backfill. No changes have been applied.',
                    1;
            END;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- STEP 1. THE COLUMN ARRIVES NULLABLE, with no default value.
            //
            // A default would quietly give every existing row a value and make the backfill look
            // unnecessary while pointing at nothing.
            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                schema: "tenant",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: true);

            // ---- STEPS 2 THROUGH 8.
            migrationBuilder.Sql(BackfillSql);

            // ---- STEP 9. THE COLUMN BECOMES REQUIRED, and stays that way. There is no runtime nullable
            // grace period: from here on an employee without a department cannot be stored.
            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                schema: "tenant",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            // ---- STEPS 10 AND 11. The foreign key and the indexes, added AFTER the backfill so the key is
            // never checked against rows that have not been filled in yet.
            //
            // IX_Employees_DepartmentId is EF's index for the foreign key, matching the ones that already
            // exist for CompanyId and BranchId. The composite below is the one the department-filtered
            // search uses, with the scope columns leading.
            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId",
                schema: "tenant",
                table: "Employees",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_CompanyId_DepartmentId",
                schema: "tenant",
                table: "Employees",
                columns: EmployeeDepartmentColumns);

            // RESTRICT: a department is deactivated, never deleted, and a cascade would erase employment
            // records along with an org-structure change.
            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Departments_DepartmentId",
                schema: "tenant",
                table: "Employees",
                column: "DepartmentId",
                principalSchema: "tenant",
                principalTable: "Departments",
                principalColumn: "DepartmentId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Departments_DepartmentId",
                schema: "tenant",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_DepartmentId",
                schema: "tenant",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_TenantId_CompanyId_DepartmentId",
                schema: "tenant",
                table: "Employees");

            // ---- THE DEPARTMENT HISTORY THIS MIGRATION WROTE IS REMOVED WITH THE COLUMN.
            //
            // Identified by the migration actor, which is the only thing that distinguishes migration-written
            // history from history the application wrote. Leaving these rows behind would strand records
            // pointing at departments whose employees no longer reference them, and a later re-run would add
            // a second initial record for the same employee.
            //
            // The departments themselves are deliberately NOT removed. By the time anyone runs Down they may
            // have been renamed, given children, or had employees deliberately assigned to them — a
            // migration cannot tell, so it does not guess.
            migrationBuilder.Sql("""
                DELETE FROM [tenant].[EmployeeDepartmentAssignments]
                WHERE [ChangedBy] = N'fp-007-department-migration';
                """);

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                schema: "tenant",
                table: "Employees");
        }
    }
}
