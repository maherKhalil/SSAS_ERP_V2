using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEmployeeLink : Migration
    {
        // Hoisted out of the scaffolded inline arrays (CA1861), the way every other migration in this
        // stream carries its column lists.
        private static readonly string[] TenantUserPrincipalColumns = ["TenantId", "TenantUserId"];

        private static readonly string[] LinkByTenantAndEmployee = ["TenantId", "EmployeeId"];

        private static readonly string[] LinkByTenantAndUser = ["TenantId", "TenantUserId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserEmployeeLink",
                schema: "platform",
                columns: table => new
                {
                    UserEmployeeLinkId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantUserId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEmployeeLink", x => x.UserEmployeeLinkId);
                    table.ForeignKey(
                        name: "FK_UserEmployeeLink_TenantUsers_TenantId_TenantUserId",
                        columns: x => new { x.TenantId, x.TenantUserId },
                        principalSchema: "platform",
                        principalTable: "TenantUsers",
                        principalColumns: TenantUserPrincipalColumns,
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_UserEmployeeLink_TenantId_EmployeeId",
                schema: "platform",
                table: "UserEmployeeLink",
                columns: LinkByTenantAndEmployee,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_UserEmployeeLink_TenantId_TenantUserId",
                schema: "platform",
                table: "UserEmployeeLink",
                columns: LinkByTenantAndUser,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserEmployeeLink",
                schema: "platform");
        }
    }
}
