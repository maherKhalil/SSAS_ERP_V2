using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Fp014SubscriptionBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IssuedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionInvoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPaymentAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPaymentAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantSubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionInvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionInvoiceLines_SubscriptionInvoices_SubscriptionInvoiceId",
                        column: x => x.SubscriptionInvoiceId,
                        principalTable: "SubscriptionInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionInvoiceLines_SubscriptionInvoiceId",
                table: "SubscriptionInvoiceLines",
                column: "SubscriptionInvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionInvoiceLines");

            migrationBuilder.DropTable(
                name: "SubscriptionPaymentAttempts");

            migrationBuilder.DropTable(
                name: "SubscriptionInvoices");
        }
    }
}
