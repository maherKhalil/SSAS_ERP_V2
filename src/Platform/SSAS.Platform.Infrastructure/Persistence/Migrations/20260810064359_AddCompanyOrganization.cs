using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companies",
                schema: "platform",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NormalizedCompanyCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaseCurrencyCode = table.Column<string>(type: "char(3)", fixedLength: true, maxLength: 3, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    StatusChangeReasonCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    StatusChangedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StatusChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.CompanyId);
                    table.CheckConstraint("CK_Companies_BaseCurrencyCode", "[BaseCurrencyCode] LIKE '[A-Z][A-Z][A-Z]'");
                    table.CheckConstraint("CK_Companies_CompanyCode_NotBlank", "LEN(LTRIM(RTRIM([CompanyCode]))) > 0");
                    table.CheckConstraint("CK_Companies_CompanyName_NotBlank", "LEN(LTRIM(RTRIM([CompanyName]))) > 0");
                    table.CheckConstraint("CK_Companies_Status", "[Status] IN (N'Inactive', N'Active', N'Archived')");
                    table.CheckConstraint("CK_Companies_StatusChangeReasonCode", "[StatusChangeReasonCode] IN (N'Created', N'Administrative', N'Operational', N'Compliance', N'CustomerRequest', N'IssueResolved')");
                    table.ForeignKey(
                        name: "FK_Companies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId_Status_CompanyName_CompanyId",
                schema: "platform",
                table: "Companies",
                columns: new[] { "TenantId", "Status", "CompanyName", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "UX_Companies_TenantId_NormalizedCompanyCode",
                schema: "platform",
                table: "Companies",
                columns: new[] { "TenantId", "NormalizedCompanyCode" },
                unique: true);

            // EF-tracked deletion is rejected by PlatformDbContext. This trigger is the
            // database backstop for direct and batch SQL; dropping the table in Down
            // removes its schema-scoped trigger before a later reapplication.
            migrationBuilder.Sql(
                """
                CREATE TRIGGER [platform].[TR_Companies_PreventDelete]
                ON [platform].[Companies]
                INSTEAD OF DELETE
                AS
                BEGIN
                  SET NOCOUNT ON;
                  THROW 51000, 'Company rows cannot be physically deleted; use the Archive lifecycle transition.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Companies",
                schema: "platform");
        }
    }
}
