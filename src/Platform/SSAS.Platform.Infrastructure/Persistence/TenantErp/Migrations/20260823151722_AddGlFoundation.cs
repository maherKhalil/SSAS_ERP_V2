using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

// EF migration APIs require inline column-name arrays; the established convention for this stream
// (see AddTenantCompanyOrganization) is to disable the rule file-wide rather than rewrite generated code.
#pragma warning disable CA1861

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    /// <inheritdoc />
    public partial class AddGlFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlAccounts",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_BIN2"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlFiscalYears",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    StartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlFiscalYears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlFiscalYears_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GlJournalDrafts",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlJournalDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlJournalDrafts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GlJournalEntries",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    EntryDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReversesJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlJournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlJournalEntries_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GlJournalEntries_GlJournalEntries_ReversesJournalEntryId",
                        column: x => x.ReversesJournalEntryId,
                        principalSchema: "tenant",
                        principalTable: "GlJournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GlFiscalPeriods",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlFiscalPeriods", x => x.Id);
                    table.CheckConstraint("CK_GlFiscalPeriods_Range", "[EndUtc] > [StartUtc]");
                    table.CheckConstraint("CK_GlFiscalPeriods_Status", "[Status] IN (N'Open', N'Closed')");
                    table.ForeignKey(
                        name: "FK_GlFiscalPeriods_GlFiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalSchema: "tenant",
                        principalTable: "GlFiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GlJournalDraftLines",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalDraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlJournalDraftLines", x => x.Id);
                    table.CheckConstraint("CK_GlJournalDraftLines_SingleSided", "([Debit] > 0 AND [Credit] = 0) OR ([Credit] > 0 AND [Debit] = 0)");
                    table.ForeignKey(
                        name: "FK_GlJournalDraftLines_GlAccounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "tenant",
                        principalTable: "GlAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GlJournalDraftLines_GlJournalDrafts_JournalDraftId",
                        column: x => x.JournalDraftId,
                        principalSchema: "tenant",
                        principalTable: "GlJournalDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GlJournalLines",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlJournalLines", x => x.Id);
                    table.CheckConstraint("CK_GlJournalLines_SingleSided", "([Debit] > 0 AND [Credit] = 0) OR ([Credit] > 0 AND [Debit] = 0)");
                    table.ForeignKey(
                        name: "FK_GlJournalLines_GlAccounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "tenant",
                        principalTable: "GlAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GlJournalLines_GlJournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "tenant",
                        principalTable: "GlJournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_GlAccounts_Tenant_NormalizedCode",
                schema: "tenant",
                table: "GlAccounts",
                columns: new[] { "TenantId", "NormalizedCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlFiscalPeriods_Year_Start",
                schema: "tenant",
                table: "GlFiscalPeriods",
                columns: new[] { "FiscalYearId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GlFiscalYears_CompanyId",
                schema: "tenant",
                table: "GlFiscalYears",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "UX_GlFiscalYears_Tenant_Company_Code",
                schema: "tenant",
                table: "GlFiscalYears",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlJournalDraftLines_AccountId",
                schema: "tenant",
                table: "GlJournalDraftLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "UX_GlJournalDraftLines_Draft_LineNumber",
                schema: "tenant",
                table: "GlJournalDraftLines",
                columns: new[] { "JournalDraftId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlJournalDrafts_CompanyId",
                schema: "tenant",
                table: "GlJournalDrafts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_GlJournalDrafts_Tenant_Company",
                schema: "tenant",
                table: "GlJournalDrafts",
                columns: new[] { "TenantId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_GlJournalEntries_CompanyId",
                schema: "tenant",
                table: "GlJournalEntries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_GlJournalEntries_ReversesJournalEntryId",
                schema: "tenant",
                table: "GlJournalEntries",
                column: "ReversesJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_GlJournalEntries_Tenant_Company_EntryDate",
                schema: "tenant",
                table: "GlJournalEntries",
                columns: new[] { "TenantId", "CompanyId", "EntryDateUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_GlJournalEntries_OneReversalPerOriginal",
                schema: "tenant",
                table: "GlJournalEntries",
                columns: new[] { "TenantId", "ReversesJournalEntryId" },
                unique: true,
                filter: "[ReversesJournalEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_GlJournalEntries_Tenant_Company_Year_Number",
                schema: "tenant",
                table: "GlJournalEntries",
                columns: new[] { "TenantId", "CompanyId", "FiscalYearId", "JournalNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlJournalLines_AccountId",
                schema: "tenant",
                table: "GlJournalLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_GlJournalLines_Tenant_Account",
                schema: "tenant",
                table: "GlJournalLines",
                columns: new[] { "TenantId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "UX_GlJournalLines_Entry_LineNumber",
                schema: "tenant",
                table: "GlJournalLines",
                columns: new[] { "JournalEntryId", "LineNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlFiscalPeriods",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "GlJournalDraftLines",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "GlJournalLines",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "GlFiscalYears",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "GlJournalDrafts",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "GlAccounts",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "GlJournalEntries",
                schema: "tenant");
        }
    }
}
