using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF migration APIs require inline column-name arrays.

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalizationCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocalizationCatalogStates",
                schema: "platform",
                columns: table => new
                {
                    LocalizationCatalogStateId = table.Column<byte>(type: "tinyint", nullable: false),
                    CatalogSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    HighestActivatedCatalogVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalizationCatalogStates", x => x.LocalizationCatalogStateId);
                    table.CheckConstraint("CK_LocalizationCatalogStates_Singleton", "[LocalizationCatalogStateId] = 1");
                    table.CheckConstraint("CK_LocalizationCatalogStates_Versions", "[CatalogSchemaVersion] > 0 AND [HighestActivatedCatalogVersion] > 0");
                });

            migrationBuilder.CreateTable(
                name: "TenantLocalizationOverrides",
                schema: "platform",
                columns: table => new
                {
                    TenantLocalizationOverrideId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Culture = table.Column<string>(type: "varchar(2)", nullable: false, collation: "Latin1_General_100_BIN2"),
                    TextFormat = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CurrentPlainTextValue = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CurrentMultilineTextValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CurrentVersionNumber = table.Column<long>(type: "bigint", nullable: false),
                    CatalogVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResourceVersion = table.Column<int>(type: "int", nullable: false),
                    PlaceholderFingerprint = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CompatibilityFingerprint = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantLocalizationOverrides", x => x.TenantLocalizationOverrideId);
                    table.UniqueConstraint("AK_TenantLocalizationOverrides_TenantId_TenantLocalizationOverrideId_ResourceKey_Culture", x => new { x.TenantId, x.TenantLocalizationOverrideId, x.ResourceKey, x.Culture });
                    table.CheckConstraint("CK_TenantLocalizationOverrides_Culture", "[Culture] IN ('en', 'ar')");
                    table.CheckConstraint("CK_TenantLocalizationOverrides_Format", "[TextFormat] IN ('PlainText', 'MultilineText')");
                    table.CheckConstraint("CK_TenantLocalizationOverrides_Value", "([IsActive] = 0 AND [CurrentPlainTextValue] IS NULL AND [CurrentMultilineTextValue] IS NULL) OR ([IsActive] = 1 AND (([TextFormat] = 'PlainText' AND [CurrentPlainTextValue] IS NOT NULL AND [CurrentMultilineTextValue] IS NULL) OR ([TextFormat] = 'MultilineText' AND [CurrentPlainTextValue] IS NULL AND [CurrentMultilineTextValue] IS NOT NULL)))");
                    table.CheckConstraint("CK_TenantLocalizationOverrides_Versions", "[CurrentVersionNumber] > 0 AND [CatalogVersion] > 0 AND [ResourceVersion] > 0");
                    table.ForeignKey(
                        name: "FK_TenantLocalizationOverrides_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantLocalizationSettings",
                schema: "platform",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantDefaultCulture = table.Column<string>(type: "varchar(2)", nullable: false, collation: "Latin1_General_100_BIN2"),
                    TenantLocalizationVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantLocalizationSettings", x => x.TenantId);
                    table.CheckConstraint("CK_TenantLocalizationSettings_Culture", "[TenantDefaultCulture] IN ('en', 'ar')");
                    table.CheckConstraint("CK_TenantLocalizationSettings_Version", "[TenantLocalizationVersion] > 0");
                    table.ForeignKey(
                        name: "FK_TenantLocalizationSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantLocalizationOverrideVersions",
                schema: "platform",
                columns: table => new
                {
                    TenantLocalizationOverrideVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantLocalizationOverrideId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Culture = table.Column<string>(type: "varchar(2)", nullable: false, collation: "Latin1_General_100_BIN2"),
                    VersionNumber = table.Column<long>(type: "bigint", nullable: false),
                    TextFormat = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false, collation: "Latin1_General_100_BIN2"),
                    PlainTextValue = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    MultilineTextValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ChangeType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    PriorLogicalVersionNumber = table.Column<long>(type: "bigint", nullable: true),
                    UndoTargetVersionNumber = table.Column<long>(type: "bigint", nullable: true),
                    CatalogVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResourceVersion = table.Column<int>(type: "int", nullable: false),
                    PlaceholderFingerprint = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CompatibilityFingerprint = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantLocalizationOverrideVersions", x => x.TenantLocalizationOverrideVersionId);
                    table.CheckConstraint("CK_TenantLocalizationOverrideVersions_ChangeType", "[ChangeType] IN ('Created', 'Updated', 'Undone', 'RestoredDefault')");
                    table.CheckConstraint("CK_TenantLocalizationOverrideVersions_Culture", "[Culture] IN ('en', 'ar')");
                    table.CheckConstraint("CK_TenantLocalizationOverrideVersions_Format", "[TextFormat] IN ('PlainText', 'MultilineText')");
                    table.CheckConstraint("CK_TenantLocalizationOverrideVersions_Lineage", "([ChangeType] = 'Created' AND [PriorLogicalVersionNumber] IS NULL AND [UndoTargetVersionNumber] IS NULL) OR ([ChangeType] IN ('Updated', 'RestoredDefault') AND [PriorLogicalVersionNumber] IS NOT NULL AND [UndoTargetVersionNumber] IS NULL) OR ([ChangeType] = 'Undone' AND [UndoTargetVersionNumber] IS NOT NULL)");
                    table.CheckConstraint("CK_TenantLocalizationOverrideVersions_Value", "([IsActive] = 0 AND [ChangeType] IN ('Undone', 'RestoredDefault') AND [PlainTextValue] IS NULL AND [MultilineTextValue] IS NULL) OR ([IsActive] = 1 AND [ChangeType] IN ('Created', 'Updated', 'Undone') AND (([TextFormat] = 'PlainText' AND [PlainTextValue] IS NOT NULL AND [MultilineTextValue] IS NULL) OR ([TextFormat] = 'MultilineText' AND [PlainTextValue] IS NULL AND [MultilineTextValue] IS NOT NULL)))");
                    table.CheckConstraint("CK_TenantLocalizationOverrideVersions_Versions", "[VersionNumber] > 0 AND [CatalogVersion] > 0 AND [ResourceVersion] > 0");
                    table.ForeignKey(
                        name: "FK_TenantLocalizationOverrideVersions_TenantLocalizationOverrides_TenantId_TenantLocalizationOverrideId_ResourceKey_Culture",
                        columns: x => new { x.TenantId, x.TenantLocalizationOverrideId, x.ResourceKey, x.Culture },
                        principalSchema: "platform",
                        principalTable: "TenantLocalizationOverrides",
                        principalColumns: new[] { "TenantId", "TenantLocalizationOverrideId", "ResourceKey", "Culture" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantLocalizationOverrides_Tenant_Culture_Resource",
                schema: "platform",
                table: "TenantLocalizationOverrides",
                columns: new[] { "TenantId", "Culture", "ResourceKey" });

            migrationBuilder.CreateIndex(
                name: "UX_TenantLocalizationOverrides_Tenant_Resource_Culture",
                schema: "platform",
                table: "TenantLocalizationOverrides",
                columns: new[] { "TenantId", "ResourceKey", "Culture" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantLocalizationOverrideVersions_Tenant_Resource_Culture_Version",
                schema: "platform",
                table: "TenantLocalizationOverrideVersions",
                columns: new[] { "TenantId", "ResourceKey", "Culture", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantLocalizationOverrideVersions_TenantId_TenantLocalizationOverrideId_ResourceKey_Culture",
                schema: "platform",
                table: "TenantLocalizationOverrideVersions",
                columns: new[] { "TenantId", "TenantLocalizationOverrideId", "ResourceKey", "Culture" });

            migrationBuilder.CreateIndex(
                name: "UX_TenantLocalizationOverrideVersions_Override_Version",
                schema: "platform",
                table: "TenantLocalizationOverrideVersions",
                columns: new[] { "TenantLocalizationOverrideId", "VersionNumber" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO [platform].[LocalizationCatalogStates]
                    ([LocalizationCatalogStateId], [CatalogSchemaVersion], [HighestActivatedCatalogVersion],
                     [CreatedUtc], [ModifiedUtc], [CreatedBy], [ModifiedBy])
                VALUES
                    (1, 1, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), N'localization-migration', N'localization-migration');
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO [platform].[TenantLocalizationSettings]
                    ([TenantId], [TenantDefaultCulture], [TenantLocalizationVersion],
                     [CreatedUtc], [ModifiedUtc], [CreatedBy], [ModifiedBy])
                SELECT
                    [TenantId], 'en', CAST(1 AS bigint),
                    SYSUTCDATETIME(), SYSUTCDATETIME(), N'localization-migration', N'localization-migration'
                FROM [platform].[Tenants];
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [platform].[TR_LocalizationCatalogStates_PreventDelete]
                ON [platform].[LocalizationCatalogStates]
                INSTEAD OF DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'Localization catalog state cannot be deleted.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [platform].[TR_TenantLocalizationSettings_PreventDelete]
                ON [platform].[TenantLocalizationSettings]
                INSTEAD OF DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'Tenant localization settings cannot be deleted.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [platform].[TR_TenantLocalizationOverrides_PreventDelete]
                ON [platform].[TenantLocalizationOverrides]
                INSTEAD OF DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'Tenant localization overrides cannot be deleted.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [platform].[TR_TenantLocalizationOverrideVersions_Immutable]
                ON [platform].[TenantLocalizationOverrideVersions]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'Tenant localization override versions are immutable.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [platform].[TR_TenantLocalizationOverrideVersions_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [platform].[TR_TenantLocalizationOverrides_PreventDelete];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [platform].[TR_TenantLocalizationSettings_PreventDelete];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [platform].[TR_LocalizationCatalogStates_PreventDelete];");

            migrationBuilder.DropTable(
                name: "LocalizationCatalogStates",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "TenantLocalizationOverrideVersions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "TenantLocalizationSettings",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "TenantLocalizationOverrides",
                schema: "platform");
        }
    }
}
#pragma warning restore CA1861
