using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

// EF migration APIs require inline column-name arrays; the established convention for this stream
// (see AddTenantCompanyOrganization) is to disable the rule file-wide rather than rewrite generated code.
#pragma warning disable CA1861

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeImportExportRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeExportRuns",
                schema: "tenant",
                columns: table => new
                {
                    ExportRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    ColumnSet = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    ScopeCompanyIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScopeBranchIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExecutedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeExportRuns", x => x.ExportRunId);
                    table.CheckConstraint("CK_EmployeeExportRuns_ColumnSet_Present", "LEN([ColumnSet]) > 0");
                    table.CheckConstraint("CK_EmployeeExportRuns_RowCount_NonNegative", "[RowCount] >= 0");
                    table.ForeignKey(
                        name: "FK_EmployeeExportRuns_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeImportRuns",
                schema: "tenant",
                columns: table => new
                {
                    ImportRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NormalizedImportKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ByteCount = table.Column<int>(type: "int", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    AcceptedCount = table.Column<int>(type: "int", nullable: false),
                    RejectedCount = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ExecutedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExecutedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeImportRuns", x => x.ImportRunId);
                    table.CheckConstraint("CK_EmployeeImportRuns_AllOrNothing", "([Outcome] = N'Refused' AND [AcceptedCount] = 0) OR ([Outcome] <> N'Refused' AND [AcceptedCount] = [RowCount])");
                    table.CheckConstraint("CK_EmployeeImportRuns_Counts_NonNegative", "[ByteCount] >= 0 AND [RowCount] >= 0 AND [AcceptedCount] >= 0 AND [RejectedCount] >= 0");
                    table.CheckConstraint("CK_EmployeeImportRuns_Outcome", "[Outcome] IN (N'Validated', N'Applied', N'Refused')");
                    table.CheckConstraint("CK_EmployeeImportRuns_RejectedWithinRowCount", "[RejectedCount] <= [RowCount]");
                    table.ForeignKey(
                        name: "FK_EmployeeImportRuns_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeExportRuns_CompanyId",
                schema: "tenant",
                table: "EmployeeExportRuns",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeExportRuns_TenantId_CompanyId_ExecutedUtc",
                schema: "tenant",
                table: "EmployeeExportRuns",
                columns: new[] { "TenantId", "CompanyId", "ExecutedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeImportRuns_TenantId_CompanyId_ExecutedUtc",
                schema: "tenant",
                table: "EmployeeImportRuns",
                columns: new[] { "TenantId", "CompanyId", "ExecutedUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_EmployeeImportRuns_Company_Key",
                schema: "tenant",
                table: "EmployeeImportRuns",
                columns: new[] { "CompanyId", "NormalizedImportKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeExportRuns",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "EmployeeImportRuns",
                schema: "tenant");
        }
    }
}
