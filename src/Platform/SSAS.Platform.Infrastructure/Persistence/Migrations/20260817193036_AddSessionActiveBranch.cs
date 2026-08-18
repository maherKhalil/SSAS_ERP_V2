using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionActiveBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActiveBranchId",
                schema: "platform",
                table: "AuthenticationSessions",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveBranchId",
                schema: "platform",
                table: "AuthenticationSessions");
        }
    }
}
