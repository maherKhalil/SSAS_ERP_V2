using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantDatabaseRestoreVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantDatabaseRestoreVerificationRuns",
                schema: "platform",
                columns: table => new
                {
                    TenantDatabaseRestoreVerificationRunId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantDatabaseId = table.Column<long>(type: "bigint", nullable: false),
                    SourceBackupRunId = table.Column<long>(type: "bigint", nullable: false),
                    Depth = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RestoreServerKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CleanupState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    VerificationDatabaseName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, collation: "Latin1_General_100_BIN2"),
                    StartedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantDatabaseRestoreVerificationRuns", x => x.TenantDatabaseRestoreVerificationRunId);
                    table.CheckConstraint("CK_TenantDatabaseRestoreVerificationRuns_CleanupRequiresDatabaseName", "[CleanupState] = N'NotRequired' OR [VerificationDatabaseName] IS NOT NULL");
                    table.CheckConstraint("CK_TenantDatabaseRestoreVerificationRuns_CleanupState", "[CleanupState] IN (N'NotRequired', N'Pending', N'Succeeded', N'Failed')");
                    table.CheckConstraint("CK_TenantDatabaseRestoreVerificationRuns_CompletedNotBeforeStarted", "[CompletedUtc] IS NULL OR [CompletedUtc] >= [StartedUtc]");
                    table.CheckConstraint("CK_TenantDatabaseRestoreVerificationRuns_Depth", "[Depth] IN (N'Full', N'FullWithDifferential', N'FullWithDifferentialAndLog')");
                    table.CheckConstraint("CK_TenantDatabaseRestoreVerificationRuns_RestoringHasDatabaseName", "[Status] = N'Admitted' OR [VerificationDatabaseName] IS NOT NULL");
                    table.CheckConstraint("CK_TenantDatabaseRestoreVerificationRuns_Status", "[Status] IN (N'Admitted', N'Restoring', N'Succeeded', N'Failed', N'InfrastructureUnavailable')");
                    table.ForeignKey(
                        name: "FK_TenantDatabaseRestoreVerificationRuns_TenantDatabaseBackupRuns_SourceBackupRunId",
                        column: x => x.SourceBackupRunId,
                        principalSchema: "platform",
                        principalTable: "TenantDatabaseBackupRuns",
                        principalColumn: "TenantDatabaseBackupRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantDatabaseRestoreVerificationRuns_TenantDatabases_TenantDatabaseId",
                        column: x => x.TenantDatabaseId,
                        principalSchema: "platform",
                        principalTable: "TenantDatabases",
                        principalColumn: "TenantDatabaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantDatabaseRestoreVerificationRuns_SourceBackupRunId",
                schema: "platform",
                table: "TenantDatabaseRestoreVerificationRuns",
                column: "SourceBackupRunId");

            migrationBuilder.CreateIndex(
                name: "UX_TenantDatabaseRestoreVerificationRuns_ActiveTenantDatabase",
                schema: "platform",
                table: "TenantDatabaseRestoreVerificationRuns",
                column: "TenantDatabaseId",
                unique: true,
                filter: "[Status] IN (N'Admitted', N'Restoring')");

            migrationBuilder.CreateIndex(
                name: "UX_TenantDatabaseRestoreVerificationRuns_DatabaseName",
                schema: "platform",
                table: "TenantDatabaseRestoreVerificationRuns",
                column: "VerificationDatabaseName",
                unique: true,
                filter: "[VerificationDatabaseName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantDatabaseRestoreVerificationRuns",
                schema: "platform");
        }
    }
}
