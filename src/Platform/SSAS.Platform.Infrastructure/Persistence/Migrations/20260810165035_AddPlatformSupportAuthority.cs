using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformSupportAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformSupportPrincipals",
                schema: "platform",
                columns: table => new
                {
                    PlatformSupportPrincipalId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdentityId = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSupportPrincipals", x => x.PlatformSupportPrincipalId);
                    table.ForeignKey(
                        name: "FK_PlatformSupportPrincipals_Identities_IdentityId",
                        column: x => x.IdentityId,
                        principalSchema: "platform",
                        principalTable: "Identities",
                        principalColumn: "IdentityId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformPermissionAssignments",
                schema: "platform",
                columns: table => new
                {
                    PlatformPermissionAssignmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlatformSupportPrincipalId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    AssignedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AssignedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RemovedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RemovedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformPermissionAssignments", x => x.PlatformPermissionAssignmentId);
                    table.CheckConstraint("CK_PlatformPermissionAssignments_PermissionName_NotBlank", "LEN(LTRIM(RTRIM([PermissionName]))) > 0");
                    table.ForeignKey(
                        name: "FK_PlatformPermissionAssignments_PlatformSupportPrincipals_PlatformSupportPrincipalId",
                        column: x => x.PlatformSupportPrincipalId,
                        principalSchema: "platform",
                        principalTable: "PlatformSupportPrincipals",
                        principalColumn: "PlatformSupportPrincipalId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_PlatformPermissionAssignments_Principal_Permission",
                schema: "platform",
                table: "PlatformPermissionAssignments",
                columns: new[] { "PlatformSupportPrincipalId", "PermissionName" },
                unique: true,
                filter: "[RemovedUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PlatformSupportPrincipals_IdentityId",
                schema: "platform",
                table: "PlatformSupportPrincipals",
                column: "IdentityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformPermissionAssignments",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "PlatformSupportPrincipals",
                schema: "platform");
        }
    }
}
