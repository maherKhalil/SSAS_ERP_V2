using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCompanyAccess : Migration
    {
        private static readonly string[] TenantUserPrincipalColumns = ["TenantId", "TenantUserId"];
        private static readonly string[] TenantIdCompanyIdColumns = ["TenantId", "CompanyId"];
        private static readonly string[] TenantUserCompanyColumns = ["TenantId", "TenantUserId", "CompanyId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserCompanyAccess",
                schema: "platform",
                columns: table => new
                {
                    UserCompanyAccessId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantUserId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCompanyAccess", x => x.UserCompanyAccessId);
                    table.ForeignKey(
                        name: "FK_UserCompanyAccess_TenantUsers_TenantId_TenantUserId",
                        columns: x => new { x.TenantId, x.TenantUserId },
                        principalSchema: "platform",
                        principalTable: "TenantUsers",
                        principalColumns: TenantUserPrincipalColumns,
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyAccess_TenantId_CompanyId",
                schema: "platform",
                table: "UserCompanyAccess",
                columns: TenantIdCompanyIdColumns);

            migrationBuilder.CreateIndex(
                name: "UX_UserCompanyAccess_TenantId_TenantUserId_CompanyId",
                schema: "platform",
                table: "UserCompanyAccess",
                columns: TenantUserCompanyColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCompanyAccess",
                schema: "platform");
        }
    }
}
