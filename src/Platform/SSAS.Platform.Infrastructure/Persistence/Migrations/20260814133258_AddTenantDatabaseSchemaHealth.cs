using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the ADR-018 schema-health and migration-orchestration state to the physical
    /// <c>platform.TenantDatabases</c> row.
    /// </summary>
    /// <remarks>
    /// The four status columns are backfilled with their pre-verification values — <c>Unknown</c>,
    /// <c>Unknown</c>, <c>Idle</c> and <c>AutomaticByPlatform</c> — rather than EF's scaffolded empty
    /// string, which would have violated the new CHECK constraints on every existing row.
    /// <para>
    /// Backfilling health as <c>Unknown</c> is deliberate and is the fail-closed choice: ADR-018's gating
    /// table denies ERP traffic on <c>Unknown</c>, so no database is presumed servable until something has
    /// actually verified it. Assuming <c>UpToDate</c> for existing rows would have been the convenient
    /// backfill and exactly the wrong one.
    /// </para>
    /// <para>
    /// State lives on the physical database row, never duplicated per assignment: a shared database has
    /// one schema and one migration state however many tenants it hosts.
    /// </para>
    /// </remarks>
    public partial class AddTenantDatabaseSchemaHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppliedMigration",
                schema: "platform",
                table: "TenantDatabases",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConnectivityStatus",
                schema: "platform",
                table: "TenantDatabases",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown",
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastConnectivityCheckUtc",
                schema: "platform",
                table: "TenantDatabases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastMigrationAttemptUtc",
                schema: "platform",
                table: "TenantDatabases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastMigrationError",
                schema: "platform",
                table: "TenantDatabases",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastMigrationFailureUtc",
                schema: "platform",
                table: "TenantDatabases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastMigrationSuccessUtc",
                schema: "platform",
                table: "TenantDatabases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSchemaCheckUtc",
                schema: "platform",
                table: "TenantDatabases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MigrationExecutionStatus",
                schema: "platform",
                table: "TenantDatabases",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Idle",
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<string>(
                name: "MigrationManagementMode",
                schema: "platform",
                table: "TenantDatabases",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "AutomaticByPlatform",
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<string>(
                name: "SchemaCompatibilityStatus",
                schema: "platform",
                table: "TenantDatabases",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown",
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<string>(
                name: "TargetMigration",
                schema: "platform",
                table: "TenantDatabases",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantDatabases_ConnectivityCheckedWhenKnown",
                schema: "platform",
                table: "TenantDatabases",
                sql: "[ConnectivityStatus] = N'Unknown' OR [LastConnectivityCheckUtc] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantDatabases_ConnectivityStatus",
                schema: "platform",
                table: "TenantDatabases",
                sql: "[ConnectivityStatus] IN (N'Unknown', N'Healthy', N'Unreachable', N'AuthenticationFailed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantDatabases_MigrationExecutionStatus",
                schema: "platform",
                table: "TenantDatabases",
                sql: "[MigrationExecutionStatus] IN (N'Idle', N'Migrating', N'Succeeded', N'Failed', N'BlockedPendingCustomer')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantDatabases_MigrationManagementMode",
                schema: "platform",
                table: "TenantDatabases",
                sql: "[MigrationManagementMode] IN (N'AutomaticByPlatform', N'PlatformAfterApproval', N'CustomerDba')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantDatabases_SchemaCheckedWhenKnown",
                schema: "platform",
                table: "TenantDatabases",
                sql: "[SchemaCompatibilityStatus] = N'Unknown' OR [LastSchemaCheckUtc] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantDatabases_SchemaCompatibilityStatus",
                schema: "platform",
                table: "TenantDatabases",
                sql: "[SchemaCompatibilityStatus] IN (N'Unknown', N'UpToDate', N'PendingMigrations', N'AheadOfApplication', N'MigrationHistoryMismatch')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantDatabases_ConnectivityCheckedWhenKnown",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantDatabases_ConnectivityStatus",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantDatabases_MigrationExecutionStatus",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantDatabases_MigrationManagementMode",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantDatabases_SchemaCheckedWhenKnown",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantDatabases_SchemaCompatibilityStatus",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "AppliedMigration",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "ConnectivityStatus",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "LastConnectivityCheckUtc",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "LastMigrationAttemptUtc",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "LastMigrationError",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "LastMigrationFailureUtc",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "LastMigrationSuccessUtc",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "LastSchemaCheckUtc",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "MigrationExecutionStatus",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "MigrationManagementMode",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "SchemaCompatibilityStatus",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "TargetMigration",
                schema: "platform",
                table: "TenantDatabases");
        }
    }
}
