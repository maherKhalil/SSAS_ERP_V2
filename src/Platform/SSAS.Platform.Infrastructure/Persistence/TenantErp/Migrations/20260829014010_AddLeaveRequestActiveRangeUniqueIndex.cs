using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveRequestActiveRangeUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_AttendanceLeaveRequests_Employee_Range_Active",
                schema: "tenant",
                table: "AttendanceLeaveRequests",
                columns: new[] { "TenantId", "EmployeeId", "StartDate", "EndDate" },
                unique: true,
                filter: "[Status] IN ('Submitted', 'Approved')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AttendanceLeaveRequests_Employee_Range_Active",
                schema: "tenant",
                table: "AttendanceLeaveRequests");
        }
    }
}
