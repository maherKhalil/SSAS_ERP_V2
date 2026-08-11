using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAuthenticationSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_PlatformSupportPrincipals_PrincipalIdentity",
                schema: "platform",
                table: "PlatformSupportPrincipals",
                columns: new[] { "PlatformSupportPrincipalId", "IdentityId" });

            migrationBuilder.CreateTable(
                name: "PlatformAuthenticationSessions",
                schema: "platform",
                columns: table => new
                {
                    PlatformAuthenticationSessionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdentityId = table.Column<long>(type: "bigint", nullable: false),
                    PlatformSupportPrincipalId = table.Column<long>(type: "bigint", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    TokenFamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastRefreshedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IdleExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AbsoluteExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SecurityVersionAtCreation = table.Column<long>(type: "bigint", nullable: false),
                    RevokedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, collation: "Latin1_General_100_BIN2"),
                    CompromisedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompromisedByRefreshTokenRecordId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAuthenticationSessions", x => x.PlatformAuthenticationSessionId);
                    table.UniqueConstraint("AK_PlatformAuthenticationSessions_SessionFamilyClient", x => new { x.PlatformAuthenticationSessionId, x.TokenFamilyId, x.ClientId });
                    table.CheckConstraint("CK_PlatformAuthenticationSessions_Expiry", "[IdleExpiresUtc] > [CreatedUtc] AND [AbsoluteExpiresUtc] > [CreatedUtc] AND [IdleExpiresUtc] <= [AbsoluteExpiresUtc]");
                    table.CheckConstraint("CK_PlatformAuthenticationSessions_LifecycleMetadata", "([Status] = N'Active' AND [RevokedUtc] IS NULL AND [RevocationReason] IS NULL AND [CompromisedUtc] IS NULL AND [CompromisedByRefreshTokenRecordId] IS NULL) OR ([Status] = N'Revoked' AND [RevokedUtc] IS NOT NULL AND [RevocationReason] IS NOT NULL AND [CompromisedUtc] IS NULL AND [CompromisedByRefreshTokenRecordId] IS NULL) OR ([Status] = N'Compromised' AND [RevokedUtc] IS NULL AND [RevocationReason] IS NULL AND [CompromisedUtc] IS NOT NULL AND [CompromisedByRefreshTokenRecordId] IS NOT NULL)");
                    table.CheckConstraint("CK_PlatformAuthenticationSessions_RevocationReason", "[RevocationReason] IS NULL OR [RevocationReason] IN (N'SessionLimitExceeded', N'SecurityStateChanged', N'IdentityIneligible', N'Administrative', N'UserLogout', N'PlatformPrincipalIneligible')");
                    table.CheckConstraint("CK_PlatformAuthenticationSessions_SecurityVersion", "[SecurityVersionAtCreation] > 0");
                    table.CheckConstraint("CK_PlatformAuthenticationSessions_Status", "[Status] IN (N'Active', N'Revoked', N'Compromised')");
                    table.ForeignKey(
                        name: "FK_PlatformAuthenticationSessions_Identities_IdentityId",
                        column: x => x.IdentityId,
                        principalSchema: "platform",
                        principalTable: "Identities",
                        principalColumn: "IdentityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlatformAuthenticationSessions_PlatformSupportPrincipals_PlatformSupportPrincipalId_IdentityId",
                        columns: x => new { x.PlatformSupportPrincipalId, x.IdentityId },
                        principalSchema: "platform",
                        principalTable: "PlatformSupportPrincipals",
                        principalColumns: new[] { "PlatformSupportPrincipalId", "IdentityId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformRefreshTokenRecords",
                schema: "platform",
                columns: table => new
                {
                    PlatformRefreshTokenRecordId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlatformAuthenticationSessionId = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenFamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReplacedByRefreshTokenRecordId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SecretHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformRefreshTokenRecords", x => x.PlatformRefreshTokenRecordId);
                    table.UniqueConstraint("AK_PlatformRefreshTokenRecords_PlatformAuthenticationSessionId_TokenFamilyId_ClientId_PlatformRefreshTokenRecordId", x => new { x.PlatformAuthenticationSessionId, x.TokenFamilyId, x.ClientId, x.PlatformRefreshTokenRecordId });
                    table.CheckConstraint("CK_PlatformRefreshTokenRecords_Expiry", "[ExpiresUtc] > [CreatedUtc]");
                    table.CheckConstraint("CK_PlatformRefreshTokenRecords_Lifecycle", "NOT ([ConsumedUtc] IS NOT NULL AND [RevokedUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PlatformRefreshTokenRecords_PlatformAuthenticationSessions_PlatformAuthenticationSessionId_TokenFamilyId_ClientId",
                        columns: x => new { x.PlatformAuthenticationSessionId, x.TokenFamilyId, x.ClientId },
                        principalSchema: "platform",
                        principalTable: "PlatformAuthenticationSessions",
                        principalColumns: new[] { "PlatformAuthenticationSessionId", "TokenFamilyId", "ClientId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlatformRefreshTokenRecords_PlatformRefreshTokenRecords_PlatformAuthenticationSessionId_TokenFamilyId_ClientId_ReplacedByRef~",
                        columns: x => new { x.PlatformAuthenticationSessionId, x.TokenFamilyId, x.ClientId, x.ReplacedByRefreshTokenRecordId },
                        principalSchema: "platform",
                        principalTable: "PlatformRefreshTokenRecords",
                        principalColumns: new[] { "PlatformAuthenticationSessionId", "TokenFamilyId", "ClientId", "PlatformRefreshTokenRecordId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAuthenticationSessions_Identity_ActiveExpiry",
                schema: "platform",
                table: "PlatformAuthenticationSessions",
                columns: new[] { "IdentityId", "Status", "IdleExpiresUtc", "AbsoluteExpiresUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAuthenticationSessions_PlatformSupportPrincipalId_IdentityId",
                schema: "platform",
                table: "PlatformAuthenticationSessions",
                columns: new[] { "PlatformSupportPrincipalId", "IdentityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAuthenticationSessions_Principal_Status",
                schema: "platform",
                table: "PlatformAuthenticationSessions",
                columns: new[] { "PlatformSupportPrincipalId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformRefreshTokenRecords_PlatformAuthenticationSessionId_TokenFamilyId_ClientId_ReplacedByRefreshTokenRecordId",
                schema: "platform",
                table: "PlatformRefreshTokenRecords",
                columns: new[] { "PlatformAuthenticationSessionId", "TokenFamilyId", "ClientId", "ReplacedByRefreshTokenRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformRefreshTokenRecords_Session_Created",
                schema: "platform",
                table: "PlatformRefreshTokenRecords",
                columns: new[] { "PlatformAuthenticationSessionId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PlatformRefreshTokenRecords_PublicId",
                schema: "platform",
                table: "PlatformRefreshTokenRecords",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PlatformRefreshTokenRecords_Replacement",
                schema: "platform",
                table: "PlatformRefreshTokenRecords",
                column: "ReplacedByRefreshTokenRecordId",
                unique: true,
                filter: "[ReplacedByRefreshTokenRecordId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformRefreshTokenRecords",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "PlatformAuthenticationSessions",
                schema: "platform");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PlatformSupportPrincipals_PrincipalIdentity",
                schema: "platform",
                table: "PlatformSupportPrincipals");
        }
    }
}
