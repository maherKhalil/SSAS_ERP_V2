using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantDatabaseBackupRecoveryFoundation : Migration
    {
        // Hoisted out of the CreateIndex calls below to satisfy CA1861, which the repository treats as an
        // error in Release.
        private static readonly string[] RunsByStarted = ["TenantDatabaseId", "StartedUtc"];

        private static readonly string[] RunsByStatusAndStarted = ["TenantDatabaseId", "Status", "StartedUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastRecoveryReadinessCheckUtc",
                schema: "platform",
                table: "TenantDatabases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastRestoreVerificationUtc",
                schema: "platform",
                table: "TenantDatabases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSuccessfulDifferentialBackupUtc",
                schema: "platform",
                table: "TenantDatabases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSuccessfulFullBackupUtc",
                schema: "platform",
                table: "TenantDatabases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSuccessfulLogBackupUtc",
                schema: "platform",
                table: "TenantDatabases",
                type: "datetimeoffset",
                nullable: true);

            // BACKFILL IS 'Unknown', NOT the scaffolded empty string and emphatically not 'Protected'.
            //
            // Two reasons. The empty string would violate CK_TenantDatabases_RecoveryReadinessStatus the
            // moment it is added below, so every existing row would block this migration. More importantly,
            // 'Unknown' is the TRUTH about every database that exists today: TS-Backup has proven nothing
            // about their recoverability, and the lifecycle gates that read this column must fail closed on
            // an unverified assumption rather than inherit a comfortable one (ADR-022 §6, §8).
            migrationBuilder.AddColumn<string>(
                name: "RecoveryReadinessStatus",
                schema: "platform",
                table: "TenantDatabases",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown",
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.CreateTable(
                name: "TenantDatabaseBackupPolicies",
                schema: "platform",
                columns: table => new
                {
                    TenantDatabaseBackupPolicyId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantDatabaseId = table.Column<long>(type: "bigint", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    ManagementMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    DestinationKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, collation: "Latin1_General_100_BIN2"),
                    FullBackupIntervalMinutes = table.Column<int>(type: "int", nullable: true),
                    DifferentialBackupIntervalMinutes = table.Column<int>(type: "int", nullable: true),
                    TransactionLogBackupIntervalMinutes = table.Column<int>(type: "int", nullable: true),
                    RetentionExpectationDays = table.Column<int>(type: "int", nullable: false),
                    RestoreVerificationIntervalDays = table.Column<int>(type: "int", nullable: true),
                    MaximumBackupAgeMinutes = table.Column<int>(type: "int", nullable: true),
                    CompressionMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    EncryptionMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantDatabaseBackupPolicies", x => x.TenantDatabaseBackupPolicyId);
                    table.CheckConstraint("CK_TenantDatabaseBackupPolicies_CompressionMode", "[CompressionMode] IN (N'PreferredWhereSupported', N'Required', N'Disabled')");
                    table.CheckConstraint("CK_TenantDatabaseBackupPolicies_DestinationWhenPlatformExecutes", "[Enabled] = CAST(0 AS bit) OR [ManagementMode] = N'CustomerDba' OR ([DestinationKey] IS NOT NULL AND LEN(LTRIM(RTRIM([DestinationKey]))) > 0)");
                    table.CheckConstraint("CK_TenantDatabaseBackupPolicies_EncryptionMode", "[EncryptionMode] IN (N'StorageManaged')");
                    table.CheckConstraint("CK_TenantDatabaseBackupPolicies_FullBaselineRequired", "[FullBackupIntervalMinutes] IS NOT NULL OR ([DifferentialBackupIntervalMinutes] IS NULL AND [TransactionLogBackupIntervalMinutes] IS NULL)");
                    table.CheckConstraint("CK_TenantDatabaseBackupPolicies_IntervalsPositive", "([FullBackupIntervalMinutes] IS NULL OR [FullBackupIntervalMinutes] > 0) AND ([DifferentialBackupIntervalMinutes] IS NULL OR [DifferentialBackupIntervalMinutes] > 0) AND ([TransactionLogBackupIntervalMinutes] IS NULL OR [TransactionLogBackupIntervalMinutes] > 0) AND ([MaximumBackupAgeMinutes] IS NULL OR [MaximumBackupAgeMinutes] > 0)");
                    table.CheckConstraint("CK_TenantDatabaseBackupPolicies_ManagementMode", "[ManagementMode] IN (N'AutomaticByPlatform', N'PlatformAfterApproval', N'CustomerDba')");
                    table.CheckConstraint("CK_TenantDatabaseBackupPolicies_RetentionPositive", "[RetentionExpectationDays] > 0 AND ([RestoreVerificationIntervalDays] IS NULL OR [RestoreVerificationIntervalDays] > 0)");
                    table.ForeignKey(
                        name: "FK_TenantDatabaseBackupPolicies_TenantDatabases_TenantDatabaseId",
                        column: x => x.TenantDatabaseId,
                        principalSchema: "platform",
                        principalTable: "TenantDatabases",
                        principalColumn: "TenantDatabaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantDatabaseBackupRuns",
                schema: "platform",
                columns: table => new
                {
                    TenantDatabaseBackupRunId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantDatabaseId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    StartedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DestinationKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, collation: "Latin1_General_100_BIN2"),
                    ArtifactReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ProviderBackupIdentity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    FirstLsn = table.Column<decimal>(type: "decimal(25,0)", nullable: true),
                    LastLsn = table.Column<decimal>(type: "decimal(25,0)", nullable: true),
                    DatabaseBackupLsn = table.Column<decimal>(type: "decimal(25,0)", nullable: true),
                    BackupSetGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerificationState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    LastVerifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OperationCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    OperationProviderKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantDatabaseBackupRuns", x => x.TenantDatabaseBackupRunId);
                    table.CheckConstraint("CK_TenantDatabaseBackupRuns_CompletedNotBeforeStarted", "[CompletedUtc] IS NULL OR [CompletedUtc] >= [StartedUtc]");
                    table.CheckConstraint("CK_TenantDatabaseBackupRuns_OperationCode_NotBlank", "LEN(LTRIM(RTRIM([OperationCode]))) > 0");
                    table.CheckConstraint("CK_TenantDatabaseBackupRuns_ProviderKey", "[OperationProviderKey] IN (N'SqlServer')");
                    table.CheckConstraint("CK_TenantDatabaseBackupRuns_SizeNotNegative", "[SizeBytes] IS NULL OR [SizeBytes] >= 0");
                    table.CheckConstraint("CK_TenantDatabaseBackupRuns_Status", "[Status] IN (N'Pending', N'Running', N'Succeeded', N'Failed', N'SkippedOwnershipHeld', N'SkippedInFlightOperation', N'BlockedByPolicy', N'VerificationFailed')");
                    table.CheckConstraint("CK_TenantDatabaseBackupRuns_SucceededHasEvidence", "[Status] <> N'Succeeded' OR ([ProviderBackupIdentity] IS NOT NULL AND [CompletedUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_TenantDatabaseBackupRuns_VerificationState", "[VerificationState] IN (N'NotVerified', N'ReadabilityVerified', N'RestoreVerified', N'VerificationFailed')");
                    table.ForeignKey(
                        name: "FK_TenantDatabaseBackupRuns_TenantDatabases_TenantDatabaseId",
                        column: x => x.TenantDatabaseId,
                        principalSchema: "platform",
                        principalTable: "TenantDatabases",
                        principalColumn: "TenantDatabaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantDatabases_ProtectedRequiresFullBackup",
                schema: "platform",
                table: "TenantDatabases",
                sql: "[RecoveryReadinessStatus] <> N'Protected' OR [LastSuccessfulFullBackupUtc] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantDatabases_RecoveryCheckedWhenKnown",
                schema: "platform",
                table: "TenantDatabases",
                sql: "[RecoveryReadinessStatus] = N'Unknown' OR [LastRecoveryReadinessCheckUtc] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TenantDatabases_RecoveryReadinessStatus",
                schema: "platform",
                table: "TenantDatabases",
                sql: "[RecoveryReadinessStatus] IN (N'Unknown', N'Protected', N'Degraded', N'Unprotected', N'RecoveryModelInvalid', N'VerificationOverdue')");

            migrationBuilder.CreateIndex(
                name: "UX_TenantDatabaseBackupPolicies_TenantDatabase",
                schema: "platform",
                table: "TenantDatabaseBackupPolicies",
                column: "TenantDatabaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantDatabaseBackupRuns_TenantDatabase_Started",
                schema: "platform",
                table: "TenantDatabaseBackupRuns",
                columns: RunsByStarted);

            migrationBuilder.CreateIndex(
                name: "IX_TenantDatabaseBackupRuns_TenantDatabase_Status_Started",
                schema: "platform",
                table: "TenantDatabaseBackupRuns",
                columns: RunsByStatusAndStarted);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantDatabaseBackupPolicies",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "TenantDatabaseBackupRuns",
                schema: "platform");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantDatabases_ProtectedRequiresFullBackup",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantDatabases_RecoveryCheckedWhenKnown",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TenantDatabases_RecoveryReadinessStatus",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "LastRecoveryReadinessCheckUtc",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "LastRestoreVerificationUtc",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulDifferentialBackupUtc",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulFullBackupUtc",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulLogBackupUtc",
                schema: "platform",
                table: "TenantDatabases");

            migrationBuilder.DropColumn(
                name: "RecoveryReadinessStatus",
                schema: "platform",
                table: "TenantDatabases");
        }
    }
}
