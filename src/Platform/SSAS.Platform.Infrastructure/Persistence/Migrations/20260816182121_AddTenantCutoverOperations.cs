using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantCutoverOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantCutoverOperations",
                schema: "platform",
                columns: table => new
                {
                    TenantCutoverOperationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceTenantDatabaseId = table.Column<long>(type: "bigint", nullable: false),
                    TargetTenantDatabaseId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    StartedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FreezeRequestedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FrozenUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FreezeReleasedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RoutingFlippedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RoutingVersion = table.Column<long>(type: "bigint", nullable: true),
                    PostCutoverWriteObservedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureSummary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantCutoverOperations", x => x.TenantCutoverOperationId);
                    table.CheckConstraint("CK_TenantCutoverOperations_DistinctEndpoints", "[SourceTenantDatabaseId] <> [TargetTenantDatabaseId]");
                    table.CheckConstraint("CK_TenantCutoverOperations_FlippedHasFlipTimestamp", "[Status] NOT IN (N'RoutingFlipped', N'Completed') OR [RoutingFlippedUtc] IS NOT NULL");
                    table.CheckConstraint("CK_TenantCutoverOperations_FlipRecordsVersion", "[RoutingFlippedUtc] IS NULL OR [RoutingVersion] IS NOT NULL");
                    table.CheckConstraint("CK_TenantCutoverOperations_FrozenHasTimestamp", "[Status] <> N'Frozen' OR [FrozenUtc] IS NOT NULL");
                    table.CheckConstraint("CK_TenantCutoverOperations_PostCutoverWriteFollowsFlip", "[PostCutoverWriteObservedUtc] IS NULL OR ([RoutingFlippedUtc] IS NOT NULL AND [PostCutoverWriteObservedUtc] >= [RoutingFlippedUtc])");
                    table.CheckConstraint("CK_TenantCutoverOperations_Status", "[Status] IN (N'Preparing', N'Frozen', N'RoutingFlipped', N'Completed', N'Abandoned')");
                    table.ForeignKey(
                        name: "FK_TenantCutoverOperations_TenantDatabases_SourceTenantDatabaseId",
                        column: x => x.SourceTenantDatabaseId,
                        principalSchema: "platform",
                        principalTable: "TenantDatabases",
                        principalColumn: "TenantDatabaseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantCutoverOperations_TenantDatabases_TargetTenantDatabaseId",
                        column: x => x.TargetTenantDatabaseId,
                        principalSchema: "platform",
                        principalTable: "TenantDatabases",
                        principalColumn: "TenantDatabaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantCutoverOperations_SourceTenantDatabaseId",
                schema: "platform",
                table: "TenantCutoverOperations",
                column: "SourceTenantDatabaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantCutoverOperations_TargetTenantDatabaseId",
                schema: "platform",
                table: "TenantCutoverOperations",
                column: "TargetTenantDatabaseId");

            migrationBuilder.CreateIndex(
                name: "UX_TenantCutoverOperations_ActiveTenant",
                schema: "platform",
                table: "TenantCutoverOperations",
                column: "TenantId",
                unique: true,
                filter: "[Status] IN (N'Preparing', N'Frozen', N'RoutingFlipped')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantCutoverOperations",
                schema: "platform");
        }
    }
}
