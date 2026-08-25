using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

// EF migration APIs require inline column-name arrays; the established convention for this stream
// (see AddTenantCompanyOrganization, AddGlFoundation) is to disable the rule file-wide rather than rewrite
// generated code.
#pragma warning disable CA1861

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollElements",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Behaviour = table.Column<int>(type: "int", nullable: false),
                    DefaultRateOrAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CalculationOrder = table.Column<int>(type: "int", nullable: false),
                    GlAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollElements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollElements_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollEmployeeCompensation",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    WasOutsideGradeBand = table.Column<bool>(type: "bit", nullable: false),
                    GradeBandObservation = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollEmployeeCompensation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollEmployeeCompensation_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollPeriods",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PayDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollPeriods_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollElementAssignments",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeCompensationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayElementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateOrAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollElementAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollElementAssignments_PayrollElements_PayElementId",
                        column: x => x.PayElementId,
                        principalSchema: "tenant",
                        principalTable: "PayrollElements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollElementAssignments_PayrollEmployeeCompensation_EmployeeCompensationId",
                        column: x => x.EmployeeCompensationId,
                        principalSchema: "tenant",
                        principalTable: "PayrollEmployeeCompensation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRuns",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CalculatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CalculatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ApprovedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PostedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PostedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRuns_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "tenant",
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollRuns_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalSchema: "tenant",
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRunDraftLines",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayElementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    GlAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRunDraftLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRunDraftLines_PayrollElements_PayElementId",
                        column: x => x.PayElementId,
                        principalSchema: "tenant",
                        principalTable: "PayrollElements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollRunDraftLines_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalSchema: "tenant",
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRunLines",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayElementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    GlAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRunLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRunLines_PayrollElements_PayElementId",
                        column: x => x.PayElementId,
                        principalSchema: "tenant",
                        principalTable: "PayrollElements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollRunLines_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalSchema: "tenant",
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollElementAssignments_EmployeeCompensationId_PayElementId",
                schema: "tenant",
                table: "PayrollElementAssignments",
                columns: new[] { "EmployeeCompensationId", "PayElementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollElementAssignments_PayElementId",
                schema: "tenant",
                table: "PayrollElementAssignments",
                column: "PayElementId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollElements_CompanyId",
                schema: "tenant",
                table: "PayrollElements",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollElements_TenantId_CompanyId_NormalizedCode",
                schema: "tenant",
                table: "PayrollElements",
                columns: new[] { "TenantId", "CompanyId", "NormalizedCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeCompensation_CompanyId",
                schema: "tenant",
                table: "PayrollEmployeeCompensation",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployeeCompensation_TenantId_CompanyId_EmployeeId_EffectiveFromUtc",
                schema: "tenant",
                table: "PayrollEmployeeCompensation",
                columns: new[] { "TenantId", "CompanyId", "EmployeeId", "EffectiveFromUtc" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_CompanyId",
                schema: "tenant",
                table: "PayrollPeriods",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_TenantId_CompanyId_FiscalPeriodId",
                schema: "tenant",
                table: "PayrollPeriods",
                columns: new[] { "TenantId", "CompanyId", "FiscalPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRunDraftLines_PayElementId",
                schema: "tenant",
                table: "PayrollRunDraftLines",
                column: "PayElementId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRunDraftLines_PayrollRunId_EmployeeId_Sequence",
                schema: "tenant",
                table: "PayrollRunDraftLines",
                columns: new[] { "PayrollRunId", "EmployeeId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRunLines_PayElementId",
                schema: "tenant",
                table: "PayrollRunLines",
                column: "PayElementId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRunLines_PayrollRunId_EmployeeId_Sequence",
                schema: "tenant",
                table: "PayrollRunLines",
                columns: new[] { "PayrollRunId", "EmployeeId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_CompanyId",
                schema: "tenant",
                table: "PayrollRuns",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_PayrollPeriodId",
                schema: "tenant",
                table: "PayrollRuns",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TenantId_CompanyId_PayrollPeriodId",
                schema: "tenant",
                table: "PayrollRuns",
                columns: new[] { "TenantId", "CompanyId", "PayrollPeriodId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollElementAssignments",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "PayrollRunDraftLines",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "PayrollRunLines",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "PayrollEmployeeCompensation",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "PayrollElements",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "PayrollRuns",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "PayrollPeriods",
                schema: "tenant");
        }
    }
}
