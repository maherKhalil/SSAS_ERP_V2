using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
  /// <inheritdoc />
  public partial class AddAuthenticationCredentialLifecycle : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "AuthenticationAccounts",
          schema: "platform",
          columns: table => new
          {
            AuthenticationAccountId = table.Column<long>(type: "bigint", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            IdentityId = table.Column<long>(type: "bigint", nullable: false),
            LoginEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
            NormalizedLoginEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false, collation: "Latin1_General_100_BIN2"),
            EmailVerifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
            Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
            FailedAttemptCount = table.Column<int>(type: "int", nullable: false),
            LockoutEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
            SecurityVersion = table.Column<long>(type: "bigint", nullable: false),
            PasswordChangedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
            RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
            CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
            ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
            PasswordHash = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_AuthenticationAccounts", x => x.AuthenticationAccountId);
            table.CheckConstraint("CK_AuthenticationAccounts_PasswordHashStatus", "([Status] = N'PendingSetup' AND [PasswordHash] IS NULL AND [EmailVerifiedUtc] IS NULL AND [PasswordChangedUtc] IS NULL) OR ([Status] IN (N'Active', N'Disabled') AND [PasswordHash] IS NOT NULL AND [EmailVerifiedUtc] IS NOT NULL AND [PasswordChangedUtc] IS NOT NULL)");
            table.ForeignKey(
                      name: "FK_AuthenticationAccounts_Identities_IdentityId",
                      column: x => x.IdentityId,
                      principalSchema: "platform",
                      principalTable: "Identities",
                      principalColumn: "IdentityId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "AccountActionTokens",
          schema: "platform",
          columns: table => new
          {
            AccountActionTokenId = table.Column<long>(type: "bigint", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            Purpose = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
            IdentityId = table.Column<long>(type: "bigint", nullable: false),
            AuthenticationAccountId = table.Column<long>(type: "bigint", nullable: false),
            TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
            TenantUserId = table.Column<long>(type: "bigint", nullable: true),
            IssuedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            ConsumedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
            RevokedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
            RevokedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
            RevocationReason = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
            RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
            CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
            ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
            SecretHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_AccountActionTokens", x => x.AccountActionTokenId);
            table.CheckConstraint("CK_AccountActionTokens_Expiry", "[ExpiresUtc] > [IssuedUtc]");
            table.CheckConstraint("CK_AccountActionTokens_OwnershipBinding", "([Purpose] = N'Invitation' AND [TenantId] IS NOT NULL AND [TenantUserId] IS NOT NULL) OR ([Purpose] = N'PasswordReset' AND [TenantId] IS NULL AND [TenantUserId] IS NULL)");
            table.ForeignKey(
                      name: "FK_AccountActionTokens_AuthenticationAccounts_AuthenticationAccountId",
                      column: x => x.AuthenticationAccountId,
                      principalSchema: "platform",
                      principalTable: "AuthenticationAccounts",
                      principalColumn: "AuthenticationAccountId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_AccountActionTokens_Identities_IdentityId",
                      column: x => x.IdentityId,
                      principalSchema: "platform",
                      principalTable: "Identities",
                      principalColumn: "IdentityId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_AccountActionTokens_TenantUsers_TenantId_TenantUserId",
                      columns: x => new { x.TenantId, x.TenantUserId },
                      principalSchema: "platform",
                      principalTable: "TenantUsers",
                      principalColumns: new[] { "TenantId", "TenantUserId" },
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateIndex(
          name: "IX_AccountActionTokens_AuthenticationAccountId",
          schema: "platform",
          table: "AccountActionTokens",
          column: "AuthenticationAccountId");

      migrationBuilder.CreateIndex(
          name: "IX_AccountActionTokens_IdentityId",
          schema: "platform",
          table: "AccountActionTokens",
          column: "IdentityId");

      migrationBuilder.CreateIndex(
          name: "IX_AccountActionTokens_PublicId",
          schema: "platform",
          table: "AccountActionTokens",
          column: "PublicId",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_AccountActionTokens_Purpose_AuthenticationAccountId",
          schema: "platform",
          table: "AccountActionTokens",
          columns: new[] { "Purpose", "AuthenticationAccountId" },
          unique: true,
          filter: "[ConsumedUtc] IS NULL AND [RevokedUtc] IS NULL AND [TenantUserId] IS NULL");

      migrationBuilder.CreateIndex(
          name: "IX_AccountActionTokens_Purpose_TenantId_TenantUserId",
          schema: "platform",
          table: "AccountActionTokens",
          columns: new[] { "Purpose", "TenantId", "TenantUserId" },
          unique: true,
          filter: "[ConsumedUtc] IS NULL AND [RevokedUtc] IS NULL AND [TenantUserId] IS NOT NULL");

      migrationBuilder.CreateIndex(
          name: "IX_AccountActionTokens_SecretHash",
          schema: "platform",
          table: "AccountActionTokens",
          column: "SecretHash",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_AccountActionTokens_TenantId_TenantUserId",
          schema: "platform",
          table: "AccountActionTokens",
          columns: new[] { "TenantId", "TenantUserId" });

      migrationBuilder.CreateIndex(
          name: "IX_AuthenticationAccounts_IdentityId",
          schema: "platform",
          table: "AuthenticationAccounts",
          column: "IdentityId",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_AuthenticationAccounts_NormalizedLoginEmail",
          schema: "platform",
          table: "AuthenticationAccounts",
          column: "NormalizedLoginEmail",
          unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "AccountActionTokens",
          schema: "platform");

      migrationBuilder.DropTable(
          name: "AuthenticationAccounts",
          schema: "platform");
    }
  }
}
