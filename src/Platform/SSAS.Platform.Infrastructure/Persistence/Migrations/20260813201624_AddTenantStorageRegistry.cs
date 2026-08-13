using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantStorageRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantDatabases",
                schema: "platform",
                columns: table => new
                {
                    TenantDatabaseId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HostingMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    StorageMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ServerKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    DatabaseName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ProvisioningStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantDatabases", x => x.TenantDatabaseId);
                    table.CheckConstraint("CK_TenantDatabases_CustomerManagedIsDedicated", "NOT ([HostingMode] = N'CustomerManaged' AND [StorageMode] = N'Shared')");
                    table.CheckConstraint("CK_TenantDatabases_DatabaseName_NotBlank", "LEN(LTRIM(RTRIM([DatabaseName]))) > 0");
                    table.CheckConstraint("CK_TenantDatabases_HostingMode", "[HostingMode] IN (N'PlatformManaged', N'CustomerManaged')");
                    table.CheckConstraint("CK_TenantDatabases_ProvisioningStatus", "[ProvisioningStatus] IN (N'Registered', N'Provisioning', N'Onboarding', N'Ready', N'Disabled')");
                    table.CheckConstraint("CK_TenantDatabases_ServerKey_NotBlank", "LEN(LTRIM(RTRIM([ServerKey]))) > 0");
                    table.CheckConstraint("CK_TenantDatabases_StorageMode", "[StorageMode] IN (N'Shared', N'Dedicated')");
                });

            migrationBuilder.CreateTable(
                name: "TenantDatabaseAssignments",
                schema: "platform",
                columns: table => new
                {
                    TenantDatabaseAssignmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantDatabaseId = table.Column<long>(type: "bigint", nullable: false),
                    RoutingVersion = table.Column<long>(type: "bigint", nullable: false),
                    AssignedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantDatabaseAssignments", x => x.TenantDatabaseAssignmentId);
                    table.CheckConstraint("CK_TenantDatabaseAssignments_EndedUtc", "[EndedUtc] IS NULL OR [EndedUtc] >= [AssignedUtc]");
                    table.CheckConstraint("CK_TenantDatabaseAssignments_RoutingVersion", "[RoutingVersion] > 0");
                    table.ForeignKey(
                        name: "FK_TenantDatabaseAssignments_TenantDatabases_TenantDatabaseId",
                        column: x => x.TenantDatabaseId,
                        principalSchema: "platform",
                        principalTable: "TenantDatabases",
                        principalColumn: "TenantDatabaseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantDatabaseAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantDatabaseAssignments_TenantDatabaseId",
                schema: "platform",
                table: "TenantDatabaseAssignments",
                column: "TenantDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantDatabaseAssignments_TenantId_RoutingVersion",
                schema: "platform",
                table: "TenantDatabaseAssignments",
                columns: new[] { "TenantId", "RoutingVersion" });

            migrationBuilder.CreateIndex(
                name: "UX_TenantDatabaseAssignments_ActiveTenant",
                schema: "platform",
                table: "TenantDatabaseAssignments",
                column: "TenantId",
                unique: true,
                filter: "[EndedUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_TenantDatabases_ServerKey_DatabaseName",
                schema: "platform",
                table: "TenantDatabases",
                columns: new[] { "ServerKey", "DatabaseName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantDatabaseAssignments",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "TenantDatabases",
                schema: "platform");
        }
    }
}
