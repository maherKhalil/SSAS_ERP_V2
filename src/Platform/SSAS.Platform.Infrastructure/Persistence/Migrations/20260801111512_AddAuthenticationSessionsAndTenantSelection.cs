using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Generated migration metadata uses inline column arrays.

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationSessionsAndTenantSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthenticationSessions",
                schema: "platform",
                columns: table => new
                {
                    AuthenticationSessionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdentityId = table.Column<long>(type: "bigint", nullable: false),
                    TenantUserId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_AuthenticationSessions", x => x.AuthenticationSessionId);
                    table.UniqueConstraint("AK_AuthenticationSessions_SessionFamilyClient", x => new { x.AuthenticationSessionId, x.TokenFamilyId, x.ClientId });
                    table.CheckConstraint("CK_AuthenticationSessions_Expiry", "[IdleExpiresUtc] > [CreatedUtc] AND [AbsoluteExpiresUtc] > [CreatedUtc] AND [IdleExpiresUtc] <= [AbsoluteExpiresUtc]");
                    table.CheckConstraint("CK_AuthenticationSessions_LifecycleMetadata", "([Status] = N'Active' AND [RevokedUtc] IS NULL AND [RevocationReason] IS NULL AND [CompromisedUtc] IS NULL AND [CompromisedByRefreshTokenRecordId] IS NULL) OR ([Status] = N'Revoked' AND [RevokedUtc] IS NOT NULL AND [RevocationReason] IS NOT NULL AND [CompromisedUtc] IS NULL AND [CompromisedByRefreshTokenRecordId] IS NULL) OR ([Status] = N'Compromised' AND [RevokedUtc] IS NULL AND [RevocationReason] IS NULL AND [CompromisedUtc] IS NOT NULL AND [CompromisedByRefreshTokenRecordId] IS NOT NULL)");
                    table.CheckConstraint("CK_AuthenticationSessions_RevocationReason", "[RevocationReason] IS NULL OR [RevocationReason] IN (N'SessionLimitExceeded', N'PasswordReset', N'SecurityStateChanged', N'IdentityIneligible', N'MembershipIneligible', N'TenantIneligible', N'Administrative')");
                    table.CheckConstraint("CK_AuthenticationSessions_Status", "[Status] IN (N'Active', N'Revoked', N'Compromised')");
                    table.ForeignKey(
                        name: "FK_AuthenticationSessions_Identities_IdentityId",
                        column: x => x.IdentityId,
                        principalSchema: "platform",
                        principalTable: "Identities",
                        principalColumn: "IdentityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuthenticationSessions_TenantUsers_TenantId_TenantUserId",
                        columns: x => new { x.TenantId, x.TenantUserId },
                        principalSchema: "platform",
                        principalTable: "TenantUsers",
                        principalColumns: new[] { "TenantId", "TenantUserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuthenticationSessions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantSelectionTransactions",
                schema: "platform",
                columns: table => new
                {
                    TenantSelectionTransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdentityId = table.Column<long>(type: "bigint", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    SecurityVersionAtAuthentication = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SecretHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSelectionTransactions", x => x.TenantSelectionTransactionId);
                    table.CheckConstraint("CK_TenantSelectionTransactions_Expiry", "[ExpiresUtc] > [CreatedUtc]");
                    table.CheckConstraint("CK_TenantSelectionTransactions_Lifecycle", "NOT ([ConsumedUtc] IS NOT NULL AND [RevokedUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TenantSelectionTransactions_Identities_IdentityId",
                        column: x => x.IdentityId,
                        principalSchema: "platform",
                        principalTable: "Identities",
                        principalColumn: "IdentityId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokenRecords",
                schema: "platform",
                columns: table => new
                {
                    RefreshTokenRecordId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthenticationSessionId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_RefreshTokenRecords", x => x.RefreshTokenRecordId);
                    table.UniqueConstraint("AK_RefreshTokenRecords_SessionFamilyClientRecord", x => new { x.AuthenticationSessionId, x.TokenFamilyId, x.ClientId, x.RefreshTokenRecordId });
                    table.CheckConstraint("CK_RefreshTokenRecords_Expiry", "[ExpiresUtc] > [CreatedUtc]");
                    table.CheckConstraint("CK_RefreshTokenRecords_Lifecycle", "NOT ([ConsumedUtc] IS NOT NULL AND [RevokedUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RefreshTokenRecords_AuthenticationSessions_AuthenticationSessionId_TokenFamilyId_ClientId",
                        columns: x => new { x.AuthenticationSessionId, x.TokenFamilyId, x.ClientId },
                        principalSchema: "platform",
                        principalTable: "AuthenticationSessions",
                        principalColumns: new[] { "AuthenticationSessionId", "TokenFamilyId", "ClientId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefreshTokenRecords_RefreshTokenRecords_AuthenticationSessionId_TokenFamilyId_ClientId_ReplacedByRefreshTokenRecordId",
                        columns: x => new { x.AuthenticationSessionId, x.TokenFamilyId, x.ClientId, x.ReplacedByRefreshTokenRecordId },
                        principalSchema: "platform",
                        principalTable: "RefreshTokenRecords",
                        principalColumns: new[] { "AuthenticationSessionId", "TokenFamilyId", "ClientId", "RefreshTokenRecordId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSessions_Identity_ActiveExpiry",
                schema: "platform",
                table: "AuthenticationSessions",
                columns: new[] { "IdentityId", "Status", "IdleExpiresUtc", "AbsoluteExpiresUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSessions_Identity_Created",
                schema: "platform",
                table: "AuthenticationSessions",
                columns: new[] { "IdentityId", "CreatedUtc", "AuthenticationSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSessions_TenantMembershipIdentity",
                schema: "platform",
                table: "AuthenticationSessions",
                columns: new[] { "TenantId", "TenantUserId", "IdentityId" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenRecords_AuthenticationSessionId_TokenFamilyId_ClientId_ReplacedByRefreshTokenRecordId",
                schema: "platform",
                table: "RefreshTokenRecords",
                columns: new[] { "AuthenticationSessionId", "TokenFamilyId", "ClientId", "ReplacedByRefreshTokenRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenRecords_Session_Created",
                schema: "platform",
                table: "RefreshTokenRecords",
                columns: new[] { "AuthenticationSessionId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_RefreshTokenRecords_PublicId",
                schema: "platform",
                table: "RefreshTokenRecords",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_RefreshTokenRecords_Replacement",
                schema: "platform",
                table: "RefreshTokenRecords",
                column: "ReplacedByRefreshTokenRecordId",
                unique: true,
                filter: "[ReplacedByRefreshTokenRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSelectionTransactions_IdentityClientLifecycle",
                schema: "platform",
                table: "TenantSelectionTransactions",
                columns: new[] { "IdentityId", "ClientId", "ConsumedUtc", "RevokedUtc", "ExpiresUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSelectionTransactions_Unresolved",
                schema: "platform",
                table: "TenantSelectionTransactions",
                columns: new[] { "ConsumedUtc", "RevokedUtc", "ExpiresUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_TenantSelectionTransactions_PublicId",
                schema: "platform",
                table: "TenantSelectionTransactions",
                column: "PublicId",
                unique: true);

            // Authentication evidence is append-only. PlatformDbContext rejects
            // tracked deletes; these triggers are the database backstop for direct SQL.
            migrationBuilder.Sql(
                """
                CREATE TRIGGER [platform].[TR_AuthenticationSessions_PreventDelete]
                ON [platform].[AuthenticationSessions]
                INSTEAD OF DELETE
                AS
                BEGIN
                  SET NOCOUNT ON;
                  THROW 51000, 'Authentication session rows cannot be physically deleted.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [platform].[TR_RefreshTokenRecords_PreventDelete]
                ON [platform].[RefreshTokenRecords]
                INSTEAD OF DELETE
                AS
                BEGIN
                  SET NOCOUNT ON;
                  THROW 51000, 'Refresh token record rows cannot be physically deleted.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER [platform].[TR_TenantSelectionTransactions_PreventDelete]
                ON [platform].[TenantSelectionTransactions]
                INSTEAD OF DELETE
                AS
                BEGIN
                  SET NOCOUNT ON;
                  THROW 51000, 'Tenant selection transaction rows cannot be physically deleted.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokenRecords",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "TenantSelectionTransactions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AuthenticationSessions",
                schema: "platform");
        }
    }
}
