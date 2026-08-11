using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformSupportPrincipalLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "platform",
                table: "PlatformSupportPrincipals",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Active",
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<string>(
                name: "StatusChangedBy",
                schema: "platform",
                table: "PlatformSupportPrincipals",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StatusChangedUtc",
                schema: "platform",
                table: "PlatformSupportPrincipals",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlatformSupportPrincipals_Status",
                schema: "platform",
                table: "PlatformSupportPrincipals",
                sql: "[Status] IN (N'Active', N'Disabled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlatformSupportPrincipals_Status",
                schema: "platform",
                table: "PlatformSupportPrincipals");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "platform",
                table: "PlatformSupportPrincipals");

            migrationBuilder.DropColumn(
                name: "StatusChangedBy",
                schema: "platform",
                table: "PlatformSupportPrincipals");

            migrationBuilder.DropColumn(
                name: "StatusChangedUtc",
                schema: "platform",
                table: "PlatformSupportPrincipals");
        }
    }
}
