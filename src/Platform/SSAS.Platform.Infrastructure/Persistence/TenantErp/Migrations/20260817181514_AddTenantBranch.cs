using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBranch : Migration
    {
        private static readonly string[] TenantIdActiveNameColumns = ["TenantId", "IsActive", "BranchName"];
        private static readonly string[] TenantIdNormalizedCodeColumns = ["TenantId", "NormalizedBranchCode"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Branches",
                schema: "tenant",
                columns: table => new
                {
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NormalizedBranchCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    BranchName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsMainBranch = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.BranchId);
                    table.CheckConstraint("CK_Branches_BranchCode_NotBlank", "LEN(LTRIM(RTRIM([BranchCode]))) > 0");
                    table.CheckConstraint("CK_Branches_BranchName_NotBlank", "LEN(LTRIM(RTRIM([BranchName]))) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TenantId_IsActive_BranchName",
                schema: "tenant",
                table: "Branches",
                columns: TenantIdActiveNameColumns);

            migrationBuilder.CreateIndex(
                name: "UX_Branches_TenantId_MainBranch",
                schema: "tenant",
                table: "Branches",
                column: "TenantId",
                unique: true,
                filter: "[IsMainBranch] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_Branches_TenantId_NormalizedBranchCode",
                schema: "tenant",
                table: "Branches",
                columns: TenantIdNormalizedCodeColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Branches",
                schema: "tenant");
        }
    }
}
