using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
  /// <inheritdoc />
  public partial class AddTenantLifecycle : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "Tenants",
          schema: "platform",
          columns: table => new
          {
            TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            TenantCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
            NormalizedTenantCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
            TenantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
            Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
            CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
            ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
            ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
            StatusChangedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            StatusChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
            StatusChangeReasonCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
            RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Tenants", x => x.TenantId);
            table.CheckConstraint("CK_Tenants_Status", "[Status] IN (N'Provisioning', N'Active', N'Suspended', N'Archived')");
            table.CheckConstraint("CK_Tenants_StatusChangeReasonCode", "[StatusChangeReasonCode] IN (N'Created', N'ProvisioningCompleted', N'Administrative', N'Security', N'Compliance', N'Operational', N'CustomerClosure', N'IssueResolved')");
            table.CheckConstraint("CK_Tenants_StatusMetadata", "[StatusChangedUtc] >= [CreatedUtc]");
            table.CheckConstraint("CK_Tenants_TenantCode_NotBlank", "LEN(LTRIM(RTRIM([TenantCode]))) > 0");
            table.CheckConstraint("CK_Tenants_TenantName_NotBlank", "LEN(LTRIM(RTRIM([TenantName]))) > 0");
          });

      migrationBuilder.CreateIndex(
          name: "IX_Tenants_Status_TenantName_TenantId",
          schema: "platform",
          table: "Tenants",
          columns: new[] { "Status", "TenantName", "TenantId" });

      migrationBuilder.CreateIndex(
          name: "UX_Tenants_NormalizedTenantCode",
          schema: "platform",
          table: "Tenants",
          column: "NormalizedTenantCode",
          unique: true);

      // EF-tracked deletion is rejected by PlatformDbContext. This trigger is the
      // database backstop for direct and batch SQL; dropping the table in Down
      // removes its schema-scoped trigger before a later reapplication.
      migrationBuilder.Sql(
          """
          CREATE TRIGGER [platform].[TR_Tenants_PreventDelete]
          ON [platform].[Tenants]
          INSTEAD OF DELETE
          AS
          BEGIN
            SET NOCOUNT ON;
            THROW 51000, 'Tenant rows cannot be physically deleted; use the Archive lifecycle transition.', 1;
          END;
          """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "Tenants",
          schema: "platform");
    }
  }
}
