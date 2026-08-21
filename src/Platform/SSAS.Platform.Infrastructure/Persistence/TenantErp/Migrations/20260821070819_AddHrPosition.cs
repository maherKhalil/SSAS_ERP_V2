using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    /// <inheritdoc />
    public partial class AddHrPosition : Migration
    {
        // Hoisted so the analyzer does not flag a constant array argument at every call site (CA1861),
        // matching the convention 20260820054319_AddHrDepartment established. Named by PURPOSE rather than
        // by content, so a reader sees why the columns are in that order: the scope columns lead, because
        // every scoped read filters on tenant then company.
        private static readonly string[] ScopedCodeColumns = ["TenantId", "CompanyId", "NormalizedCode"];
        private static readonly string[] ScopedRankColumns = ["TenantId", "CompanyId", "RankOrder"];
        private static readonly string[] ScopedStatusColumns = ["TenantId", "CompanyId", "Status"];
        private static readonly string[] PositionGradeColumns = ["TenantId", "CompanyId", "JobGradeId"];

        // The history index serves ordered playback AND point-in-time attribution; the identifier is the
        // deterministic tie-break, matching the branch and department history indexes exactly.
        private static readonly string[] PositionHistoryColumns =
            ["TenantId", "CompanyId", "EmployeeId", "EffectiveFromUtc", "EmployeePositionAssignmentId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalaryGrades",
                schema: "tenant",
                columns: table => new
                {
                    SalaryGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NormalizedCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RankOrder = table.Column<int>(type: "int", nullable: false),
                    MinimumAmount = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    MidpointAmount = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    MaximumAmount = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
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
                    table.PrimaryKey("PK_SalaryGrades", x => x.SalaryGradeId);
                    table.CheckConstraint("CK_SalaryGrades_Amounts_NonNegative", "[MinimumAmount] IS NULL OR ([MinimumAmount] >= 0 AND [MidpointAmount] >= 0 AND [MaximumAmount] >= 0)");
                    table.CheckConstraint("CK_SalaryGrades_Amounts_Ordered", "[MinimumAmount] IS NULL OR ([MinimumAmount] <= [MidpointAmount] AND [MidpointAmount] <= [MaximumAmount])");
                    table.CheckConstraint("CK_SalaryGrades_Band_Atomic", "([MinimumAmount] IS NULL AND [MidpointAmount] IS NULL AND [MaximumAmount] IS NULL) OR ([MinimumAmount] IS NOT NULL AND [MidpointAmount] IS NOT NULL AND [MaximumAmount] IS NOT NULL)");
                    table.CheckConstraint("CK_SalaryGrades_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_SalaryGrades_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_SalaryGrades_Status", "[Status] IN (N'Active', N'Inactive')");
                    table.ForeignKey(
                        name: "FK_SalaryGrades_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobGrades",
                schema: "tenant",
                columns: table => new
                {
                    JobGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NormalizedCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RankOrder = table.Column<int>(type: "int", nullable: false),
                    SalaryGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
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
                    table.PrimaryKey("PK_JobGrades", x => x.JobGradeId);
                    table.CheckConstraint("CK_JobGrades_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_JobGrades_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_JobGrades_Status", "[Status] IN (N'Active', N'Inactive')");
                    table.ForeignKey(
                        name: "FK_JobGrades_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobGrades_SalaryGrades_SalaryGradeId",
                        column: x => x.SalaryGradeId,
                        principalSchema: "tenant",
                        principalTable: "SalaryGrades",
                        principalColumn: "SalaryGradeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                schema: "tenant",
                columns: table => new
                {
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NormalizedCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Title = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    JobGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
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
                    table.PrimaryKey("PK_Positions", x => x.PositionId);
                    table.CheckConstraint("CK_Positions_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_Positions_Status", "[Status] IN (N'Active', N'Inactive')");
                    table.CheckConstraint("CK_Positions_Title_NotBlank", "LEN(LTRIM(RTRIM([Title]))) > 0");
                    table.ForeignKey(
                        name: "FK_Positions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Positions_JobGrades_JobGradeId",
                        column: x => x.JobGradeId,
                        principalSchema: "tenant",
                        principalTable: "JobGrades",
                        principalColumn: "JobGradeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeePositionAssignments",
                schema: "tenant",
                columns: table => new
                {
                    EmployeePositionAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcePositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinationPositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, collation: "Latin1_General_100_BIN2"),
                    ReasonText = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePositionAssignments", x => x.EmployeePositionAssignmentId);
                    table.CheckConstraint("CK_EmployeePositionAssignments_SourceDiffersFromDestination", "[SourcePositionId] IS NULL OR [SourcePositionId] <> [DestinationPositionId]");
                    table.ForeignKey(
                        name: "FK_EmployeePositionAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "tenant",
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePositionAssignments_Positions_DestinationPositionId",
                        column: x => x.DestinationPositionId,
                        principalSchema: "tenant",
                        principalTable: "Positions",
                        principalColumn: "PositionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePositionAssignments_Positions_SourcePositionId",
                        column: x => x.SourcePositionId,
                        principalSchema: "tenant",
                        principalTable: "Positions",
                        principalColumn: "PositionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionAssignments_DestinationPositionId",
                schema: "tenant",
                table: "EmployeePositionAssignments",
                column: "DestinationPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionAssignments_EmployeeId",
                schema: "tenant",
                table: "EmployeePositionAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionAssignments_SourcePositionId",
                schema: "tenant",
                table: "EmployeePositionAssignments",
                column: "SourcePositionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionAssignments_TenantId_CompanyId_EmployeeId_EffectiveFromUtc_Id",
                schema: "tenant",
                table: "EmployeePositionAssignments",
                columns: PositionHistoryColumns);

            migrationBuilder.CreateIndex(
                name: "IX_JobGrades_CompanyId",
                schema: "tenant",
                table: "JobGrades",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_JobGrades_SalaryGradeId",
                schema: "tenant",
                table: "JobGrades",
                column: "SalaryGradeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobGrades_TenantId_CompanyId_Status",
                schema: "tenant",
                table: "JobGrades",
                columns: ScopedStatusColumns);

            migrationBuilder.CreateIndex(
                name: "UX_JobGrades_TenantId_CompanyId_NormalizedCode",
                schema: "tenant",
                table: "JobGrades",
                columns: ScopedCodeColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_JobGrades_TenantId_CompanyId_RankOrder",
                schema: "tenant",
                table: "JobGrades",
                columns: ScopedRankColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_CompanyId",
                schema: "tenant",
                table: "Positions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_JobGradeId",
                schema: "tenant",
                table: "Positions",
                column: "JobGradeId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_TenantId_CompanyId_JobGradeId",
                schema: "tenant",
                table: "Positions",
                columns: PositionGradeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_TenantId_CompanyId_Status",
                schema: "tenant",
                table: "Positions",
                columns: ScopedStatusColumns);

            migrationBuilder.CreateIndex(
                name: "UX_Positions_TenantId_CompanyId_NormalizedCode",
                schema: "tenant",
                table: "Positions",
                columns: ScopedCodeColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryGrades_CompanyId",
                schema: "tenant",
                table: "SalaryGrades",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryGrades_TenantId_CompanyId_Status",
                schema: "tenant",
                table: "SalaryGrades",
                columns: ScopedStatusColumns);

            migrationBuilder.CreateIndex(
                name: "UX_SalaryGrades_TenantId_CompanyId_NormalizedCode",
                schema: "tenant",
                table: "SalaryGrades",
                columns: ScopedCodeColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SalaryGrades_TenantId_CompanyId_RankOrder",
                schema: "tenant",
                table: "SalaryGrades",
                columns: ScopedRankColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePositionAssignments",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "Positions",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "JobGrades",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "SalaryGrades",
                schema: "tenant");
        }
    }
}
