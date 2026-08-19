using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBranchAccess : Migration
    {
        private static readonly string[] TenantUserPrincipalColumns = ["TenantId", "TenantUserId"];
        private static readonly string[] TenantIdBranchIdColumns = ["TenantId", "BranchId"];
        private static readonly string[] TenantUserBranchColumns = ["TenantId", "TenantUserId", "BranchId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserBranchAccess",
                schema: "platform",
                columns: table => new
                {
                    UserBranchAccessId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantUserId = table.Column<long>(type: "bigint", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBranchAccess", x => x.UserBranchAccessId);
                    table.ForeignKey(
                        name: "FK_UserBranchAccess_TenantUsers_TenantId_TenantUserId",
                        columns: x => new { x.TenantId, x.TenantUserId },
                        principalSchema: "platform",
                        principalTable: "TenantUsers",
                        principalColumns: TenantUserPrincipalColumns,
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserBranchAccess_TenantId_BranchId",
                schema: "platform",
                table: "UserBranchAccess",
                columns: TenantIdBranchIdColumns);

            migrationBuilder.CreateIndex(
                name: "UX_UserBranchAccess_TenantId_TenantUserId_BranchId",
                schema: "platform",
                table: "UserBranchAccess",
                columns: TenantUserBranchColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserBranchAccess",
                schema: "platform");
        }
    }
}
