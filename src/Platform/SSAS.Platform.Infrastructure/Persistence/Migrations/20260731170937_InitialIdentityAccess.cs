using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
  /// <inheritdoc />
  public partial class InitialIdentityAccess : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.EnsureSchema(
          name: "platform");

      migrationBuilder.CreateTable(
          name: "Identities",
          schema: "platform",
          columns: table => new
          {
            IdentityId = table.Column<long>(type: "bigint", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            Subject = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_BIN2"),
            RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
            CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
            ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Identities", x => x.IdentityId);
          });

      migrationBuilder.CreateTable(
          name: "Roles",
          schema: "platform",
          columns: table => new
          {
            RoleId = table.Column<long>(type: "bigint", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
            NormalizedRoleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
            Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
            RoleType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
            Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
            RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
            CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
            ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Roles", x => x.RoleId);
            table.UniqueConstraint("AK_Roles_TenantId_RoleId", x => new { x.TenantId, x.RoleId });
          });

      migrationBuilder.CreateTable(
          name: "TenantUsers",
          schema: "platform",
          columns: table => new
          {
            TenantUserId = table.Column<long>(type: "bigint", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            IdentityId = table.Column<long>(type: "bigint", nullable: false),
            TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
            NormalizedEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
            DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
            Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
            RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
            CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
            ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_TenantUsers", x => x.TenantUserId);
            table.UniqueConstraint("AK_TenantUsers_TenantId_TenantUserId", x => new { x.TenantId, x.TenantUserId });
            table.ForeignKey(
                      name: "FK_TenantUsers_Identities_IdentityId",
                      column: x => x.IdentityId,
                      principalSchema: "platform",
                      principalTable: "Identities",
                      principalColumn: "IdentityId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "RolePermissionAssignments",
          schema: "platform",
          columns: table => new
          {
            AssignmentId = table.Column<long>(type: "bigint", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            RoleId = table.Column<long>(type: "bigint", nullable: false),
            PermissionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
            AssignedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            AssignedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
            RemovedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
            RemovedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_RolePermissionAssignments", x => x.AssignmentId);
            table.ForeignKey(
                      name: "FK_RolePermissionAssignments_Roles_TenantId_RoleId",
                      columns: x => new { x.TenantId, x.RoleId },
                      principalSchema: "platform",
                      principalTable: "Roles",
                      principalColumns: new[] { "TenantId", "RoleId" },
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "TenantUserRoleAssignments",
          schema: "platform",
          columns: table => new
          {
            AssignmentId = table.Column<long>(type: "bigint", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            TenantUserId = table.Column<long>(type: "bigint", nullable: false),
            RoleId = table.Column<long>(type: "bigint", nullable: false),
            AssignedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            AssignedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
            RemovedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
            RemovedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_TenantUserRoleAssignments", x => x.AssignmentId);
            table.ForeignKey(
                      name: "FK_TenantUserRoleAssignments_Roles_TenantId_RoleId",
                      columns: x => new { x.TenantId, x.RoleId },
                      principalSchema: "platform",
                      principalTable: "Roles",
                      principalColumns: new[] { "TenantId", "RoleId" },
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_TenantUserRoleAssignments_TenantUsers_TenantId_TenantUserId",
                      columns: x => new { x.TenantId, x.TenantUserId },
                      principalSchema: "platform",
                      principalTable: "TenantUsers",
                      principalColumns: new[] { "TenantId", "TenantUserId" },
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateIndex(
          name: "IX_Identities_Subject",
          schema: "platform",
          table: "Identities",
          column: "Subject",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_RolePermissionAssignments_TenantId_RoleId_PermissionName",
          schema: "platform",
          table: "RolePermissionAssignments",
          columns: new[] { "TenantId", "RoleId", "PermissionName" },
          unique: true,
          filter: "[RemovedUtc] IS NULL");

      migrationBuilder.CreateIndex(
          name: "IX_Roles_TenantId_NormalizedRoleName",
          schema: "platform",
          table: "Roles",
          columns: new[] { "TenantId", "NormalizedRoleName" },
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_TenantUserRoleAssignments_TenantId_RoleId",
          schema: "platform",
          table: "TenantUserRoleAssignments",
          columns: new[] { "TenantId", "RoleId" });

      migrationBuilder.CreateIndex(
          name: "IX_TenantUserRoleAssignments_TenantId_TenantUserId_RoleId",
          schema: "platform",
          table: "TenantUserRoleAssignments",
          columns: new[] { "TenantId", "TenantUserId", "RoleId" },
          unique: true,
          filter: "[RemovedUtc] IS NULL");

      migrationBuilder.CreateIndex(
          name: "IX_TenantUsers_IdentityId",
          schema: "platform",
          table: "TenantUsers",
          column: "IdentityId");

      migrationBuilder.CreateIndex(
          name: "IX_TenantUsers_TenantId_IdentityId",
          schema: "platform",
          table: "TenantUsers",
          columns: new[] { "TenantId", "IdentityId" },
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_TenantUsers_TenantId_NormalizedEmail",
          schema: "platform",
          table: "TenantUsers",
          columns: new[] { "TenantId", "NormalizedEmail" },
          unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "RolePermissionAssignments",
          schema: "platform");

      migrationBuilder.DropTable(
          name: "TenantUserRoleAssignments",
          schema: "platform");

      migrationBuilder.DropTable(
          name: "Roles",
          schema: "platform");

      migrationBuilder.DropTable(
          name: "TenantUsers",
          schema: "platform");

      migrationBuilder.DropTable(
          name: "Identities",
          schema: "platform");
    }
  }
}
#pragma warning restore CA1861
