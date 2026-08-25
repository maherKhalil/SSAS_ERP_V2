using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

// EF migration APIs require inline column-name arrays; the established convention for this stream
// (see AddTenantCompanyOrganization, AddGlFoundation, AddPayrollFoundation) is to disable the rule
// file-wide rather than rewrite generated code.
#pragma warning disable CA1861

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OvertimeTier",
                schema: "tenant",
                table: "PayrollElements",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AttendanceLeaveTypes",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Behaviour = table.Column<int>(type: "int", nullable: false),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceLeaveTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceLeaveTypes_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendancePeriods",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ClosedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendancePeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendancePeriods_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceWorkingCalendars",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Weekend = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceWorkingCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceWorkingCalendars_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceLeaveBalances",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    EntitlementQuantity = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    ConsumedQuantity = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceLeaveBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceLeaveBalances_AttendanceLeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalSchema: "tenant",
                        principalTable: "AttendanceLeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceLeaveBalances_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceLeaveRequests",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WorkingDaysConsumed = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DecidedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DecidedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApproverEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceLeaveRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceLeaveRequests_AttendanceLeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalSchema: "tenant",
                        principalTable: "AttendanceLeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceLeaveRequests_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttendancePeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AdjustedRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkedQuantity = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    OvertimeQuantity = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    OvertimeTier = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PaidAbsenceQuantity = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    UnpaidAbsenceQuantity = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_AttendancePeriods_AttendancePeriodId",
                        column: x => x.AttendancePeriodId,
                        principalSchema: "tenant",
                        principalTable: "AttendancePeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "tenant",
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceCalendarHolidays",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkingCalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HolidayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceCalendarHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceCalendarHolidays_AttendanceWorkingCalendars_WorkingCalendarId",
                        column: x => x.WorkingCalendarId,
                        principalSchema: "tenant",
                        principalTable: "AttendanceWorkingCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCalendarHolidays_WorkingCalendarId_HolidayDate",
                schema: "tenant",
                table: "AttendanceCalendarHolidays",
                columns: new[] { "WorkingCalendarId", "HolidayDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLeaveBalances_CompanyId",
                schema: "tenant",
                table: "AttendanceLeaveBalances",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLeaveBalances_LeaveTypeId",
                schema: "tenant",
                table: "AttendanceLeaveBalances",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLeaveBalances_TenantId_EmployeeId_LeaveTypeId_PeriodYear",
                schema: "tenant",
                table: "AttendanceLeaveBalances",
                columns: new[] { "TenantId", "EmployeeId", "LeaveTypeId", "PeriodYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLeaveRequests_CompanyId",
                schema: "tenant",
                table: "AttendanceLeaveRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLeaveRequests_LeaveTypeId",
                schema: "tenant",
                table: "AttendanceLeaveRequests",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLeaveRequests_TenantId_EmployeeId_StartDate_EndDate",
                schema: "tenant",
                table: "AttendanceLeaveRequests",
                columns: new[] { "TenantId", "EmployeeId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLeaveTypes_CompanyId",
                schema: "tenant",
                table: "AttendanceLeaveTypes",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLeaveTypes_TenantId_CompanyId_NormalizedCode",
                schema: "tenant",
                table: "AttendanceLeaveTypes",
                columns: new[] { "TenantId", "CompanyId", "NormalizedCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePeriods_CompanyId",
                schema: "tenant",
                table: "AttendancePeriods",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePeriods_TenantId_CompanyId_StartDate_EndDate",
                schema: "tenant",
                table: "AttendancePeriods",
                columns: new[] { "TenantId", "CompanyId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_AttendancePeriodId",
                schema: "tenant",
                table: "AttendanceRecords",
                column: "AttendancePeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_BranchId",
                schema: "tenant",
                table: "AttendanceRecords",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_CompanyId",
                schema: "tenant",
                table: "AttendanceRecords",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_TenantId_AttendancePeriodId_EmployeeId",
                schema: "tenant",
                table: "AttendanceRecords",
                columns: new[] { "TenantId", "AttendancePeriodId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_TenantId_BranchId_AttendanceDate",
                schema: "tenant",
                table: "AttendanceRecords",
                columns: new[] { "TenantId", "BranchId", "AttendanceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceWorkingCalendars_CompanyId",
                schema: "tenant",
                table: "AttendanceWorkingCalendars",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceWorkingCalendars_TenantId_CompanyId_NormalizedName",
                schema: "tenant",
                table: "AttendanceWorkingCalendars",
                columns: new[] { "TenantId", "CompanyId", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceCalendarHolidays",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AttendanceLeaveBalances",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AttendanceLeaveRequests",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AttendanceRecords",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AttendanceWorkingCalendars",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AttendanceLeaveTypes",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AttendancePeriods",
                schema: "tenant");

            migrationBuilder.DropColumn(
                name: "OvertimeTier",
                schema: "tenant",
                table: "PayrollElements");
        }
    }
}
