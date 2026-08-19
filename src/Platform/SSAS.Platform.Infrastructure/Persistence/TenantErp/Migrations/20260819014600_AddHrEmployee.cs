using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    /// <inheritdoc />
    public partial class AddHrEmployee : Migration
    {
        private static readonly string[] AssignmentReportingColumns = ["TenantId", "CompanyId", "DestinationBranchId", "EffectiveFromUtc"];
        private static readonly string[] AssignmentHistoryColumns = ["TenantId", "EmployeeId", "EffectiveFromUtc", "EmployeeBranchAssignmentId"];
        private static readonly string[] EmployeeSearchColumns = ["TenantId", "CompanyId", "BranchId", "Status"];
        private static readonly string[] EmployeeNumberColumns = ["TenantId", "CompanyId", "NormalizedEmployeeNumber"];
        private static readonly string[] NationalIdColumns = ["TenantId", "CompanyId", "NormalizedNationalId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "tenant",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NormalizedEmployeeNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    NationalId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    NormalizedNationalId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmploymentDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TerminationDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    StatusChangeReasonCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    StatusChangedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StatusChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.EmployeeId);
                    table.CheckConstraint("CK_Employees_EmployeeNumber_NotBlank", "LEN(LTRIM(RTRIM([EmployeeNumber]))) > 0");
                    table.CheckConstraint("CK_Employees_FullName_NotBlank", "LEN(LTRIM(RTRIM([FullName]))) > 0");
                    table.CheckConstraint("CK_Employees_Status", "[Status] IN (N'Active', N'Inactive', N'Terminated')");
                    table.CheckConstraint("CK_Employees_StatusChangeReasonCode", "[StatusChangeReasonCode] IN (N'Created', N'Administrative', N'Operational', N'Compliance', N'Resignation', N'Dismissal', N'EndOfContract')");
                    table.CheckConstraint("CK_Employees_TerminationDateMatchesStatus", "([Status] = N'Terminated' AND [TerminationDate] IS NOT NULL) OR ([Status] <> N'Terminated' AND [TerminationDate] IS NULL)");
                    table.CheckConstraint("CK_Employees_TerminationNotBeforeEmployment", "[TerminationDate] IS NULL OR [TerminationDate] >= [EmploymentDate]");
                    table.ForeignKey(
                        name: "FK_Employees_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "tenant",
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeBranchAssignments",
                schema: "tenant",
                columns: table => new
                {
                    EmployeeBranchAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceBranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinationBranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TransferredBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ReasonText = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeBranchAssignments", x => x.EmployeeBranchAssignmentId);
                    table.CheckConstraint("CK_EmployeeBranchAssignments_InitialAssignmentHasNoSource", "([SourceBranchId] IS NULL AND [ReasonCode] = N'InitialAssignment') OR ([SourceBranchId] IS NOT NULL AND [ReasonCode] <> N'InitialAssignment')");
                    table.CheckConstraint("CK_EmployeeBranchAssignments_ReasonCode", "[ReasonCode] IN (N'InitialAssignment', N'Reorganisation', N'OperationalNeed', N'EmployeeRequest', N'BranchClosure', N'Correction')");
                    table.CheckConstraint("CK_EmployeeBranchAssignments_SourceDiffersFromDestination", "[SourceBranchId] IS NULL OR [SourceBranchId] <> [DestinationBranchId]");
                    table.ForeignKey(
                        name: "FK_EmployeeBranchAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "tenant",
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBranchAssignments_EmployeeId",
                schema: "tenant",
                table: "EmployeeBranchAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBranchAssignments_TenantId_CompanyId_DestinationBranchId_EffectiveFromUtc",
                schema: "tenant",
                table: "EmployeeBranchAssignments",
                columns: AssignmentReportingColumns);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBranchAssignments_TenantId_EmployeeId_EffectiveFromUtc_Id",
                schema: "tenant",
                table: "EmployeeBranchAssignments",
                columns: AssignmentHistoryColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_BranchId",
                schema: "tenant",
                table: "Employees",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId",
                schema: "tenant",
                table: "Employees",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_CompanyId_BranchId_Status",
                schema: "tenant",
                table: "Employees",
                columns: EmployeeSearchColumns);

            migrationBuilder.CreateIndex(
                name: "UX_Employees_TenantId_CompanyId_NormalizedEmployeeNumber",
                schema: "tenant",
                table: "Employees",
                columns: EmployeeNumberColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Employees_TenantId_CompanyId_NormalizedNationalId",
                schema: "tenant",
                table: "Employees",
                columns: NationalIdColumns,
                unique: true,
                filter: "[NormalizedNationalId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeBranchAssignments",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "tenant");
        }
    }
}
