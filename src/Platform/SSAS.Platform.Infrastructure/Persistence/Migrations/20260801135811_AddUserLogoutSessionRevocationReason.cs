using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
  /// <inheritdoc />
  public partial class AddUserLogoutSessionRevocationReason : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropCheckConstraint(
          name: "CK_AuthenticationSessions_RevocationReason",
          schema: "platform",
          table: "AuthenticationSessions");

      migrationBuilder.AddCheckConstraint(
          name: "CK_AuthenticationSessions_RevocationReason",
          schema: "platform",
          table: "AuthenticationSessions",
          sql: "[RevocationReason] IS NULL OR [RevocationReason] IN (N'SessionLimitExceeded', N'PasswordReset', N'SecurityStateChanged', N'IdentityIneligible', N'MembershipIneligible', N'TenantIneligible', N'Administrative', N'UserLogout')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropCheckConstraint(
          name: "CK_AuthenticationSessions_RevocationReason",
          schema: "platform",
          table: "AuthenticationSessions");

      migrationBuilder.AddCheckConstraint(
          name: "CK_AuthenticationSessions_RevocationReason",
          schema: "platform",
          table: "AuthenticationSessions",
          sql: "[RevocationReason] IS NULL OR [RevocationReason] IN (N'SessionLimitExceeded', N'PasswordReset', N'SecurityStateChanged', N'IdentityIneligible', N'MembershipIneligible', N'TenantIneligible', N'Administrative')");
    }
  }
}
