using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollRunReversedUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollRuns_TenantId_CompanyId_PayrollPeriodId",
                schema: "tenant",
                table: "PayrollRuns");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversedUtc",
                schema: "tenant",
                table: "PayrollRuns",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TenantId_CompanyId_PayrollPeriodId",
                schema: "tenant",
                table: "PayrollRuns",
                columns: new[] { "TenantId", "CompanyId", "PayrollPeriodId" },
                unique: true,
                filter: "[ReversedUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollRuns_TenantId_CompanyId_PayrollPeriodId",
                schema: "tenant",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "ReversedUtc",
                schema: "tenant",
                table: "PayrollRuns");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TenantId_CompanyId_PayrollPeriodId",
                schema: "tenant",
                table: "PayrollRuns",
                columns: new[] { "TenantId", "CompanyId", "PayrollPeriodId" },
                unique: true);
        }
    }
}
