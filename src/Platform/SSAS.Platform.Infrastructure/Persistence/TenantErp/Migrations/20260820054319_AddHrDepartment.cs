using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    /// <inheritdoc />
    public partial class AddHrDepartment : Migration
    {
        private static readonly string[] ManagerEmployeeColumns = ["TenantId", "CompanyId", "EmployeeId"];
        private static readonly string[] DepartmentHierarchyColumns = ["TenantId", "CompanyId", "ParentDepartmentId"];
        private static readonly string[] DepartmentCodeColumns = ["TenantId", "CompanyId", "NormalizedCode"];
        private static readonly string[] DepartmentHistoryColumns = ["TenantId", "CompanyId", "EmployeeId", "EffectiveFromUtc", "EmployeeDepartmentAssignmentId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                schema: "tenant",
                columns: table => new
                {
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NormalizedCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParentDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_Departments", x => x.DepartmentId);
                    table.CheckConstraint("CK_Departments_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_Departments_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_Departments_ParentIsNotSelf", "[ParentDepartmentId] IS NULL OR [ParentDepartmentId] <> [DepartmentId]");
                    table.CheckConstraint("CK_Departments_Status", "[Status] IN (N'Active', N'Inactive')");
                    table.ForeignKey(
                        name: "FK_Departments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Departments_Departments_ParentDepartmentId",
                        column: x => x.ParentDepartmentId,
                        principalSchema: "tenant",
                        principalTable: "Departments",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DepartmentManagers",
                schema: "tenant",
                columns: table => new
                {
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AssignedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentManagers", x => x.DepartmentId);
                    table.ForeignKey(
                        name: "FK_DepartmentManagers_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "tenant",
                        principalTable: "Departments",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentManagers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "tenant",
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDepartmentAssignments",
                schema: "tenant",
                columns: table => new
                {
                    EmployeeDepartmentAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinationDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, collation: "Latin1_General_100_BIN2"),
                    ReasonText = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDepartmentAssignments", x => x.EmployeeDepartmentAssignmentId);
                    table.CheckConstraint("CK_EmployeeDepartmentAssignments_SourceDiffersFromDestination", "[SourceDepartmentId] IS NULL OR [SourceDepartmentId] <> [DestinationDepartmentId]");
                    table.ForeignKey(
                        name: "FK_EmployeeDepartmentAssignments_Departments_DestinationDepartmentId",
                        column: x => x.DestinationDepartmentId,
                        principalSchema: "tenant",
                        principalTable: "Departments",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDepartmentAssignments_Departments_SourceDepartmentId",
                        column: x => x.SourceDepartmentId,
                        principalSchema: "tenant",
                        principalTable: "Departments",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDepartmentAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "tenant",
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentManagers_EmployeeId",
                schema: "tenant",
                table: "DepartmentManagers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentManagers_TenantId_CompanyId_EmployeeId",
                schema: "tenant",
                table: "DepartmentManagers",
                columns: ManagerEmployeeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_CompanyId",
                schema: "tenant",
                table: "Departments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_ParentDepartmentId",
                schema: "tenant",
                table: "Departments",
                column: "ParentDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_TenantId_CompanyId_ParentDepartmentId",
                schema: "tenant",
                table: "Departments",
                columns: DepartmentHierarchyColumns);

            migrationBuilder.CreateIndex(
                name: "UX_Departments_TenantId_CompanyId_NormalizedCode",
                schema: "tenant",
                table: "Departments",
                columns: DepartmentCodeColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDepartmentAssignments_DestinationDepartmentId",
                schema: "tenant",
                table: "EmployeeDepartmentAssignments",
                column: "DestinationDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDepartmentAssignments_EmployeeId",
                schema: "tenant",
                table: "EmployeeDepartmentAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDepartmentAssignments_SourceDepartmentId",
                schema: "tenant",
                table: "EmployeeDepartmentAssignments",
                column: "SourceDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDepartmentAssignments_TenantId_CompanyId_EmployeeId_EffectiveFromUtc_Id",
                schema: "tenant",
                table: "EmployeeDepartmentAssignments",
                columns: DepartmentHistoryColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartmentManagers",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "EmployeeDepartmentAssignments",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "Departments",
                schema: "tenant");
        }
    }
}
