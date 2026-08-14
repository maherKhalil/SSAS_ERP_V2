using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Relinquishes platform ownership of Company (ADR-017). Company now belongs to the tenant ERP
    /// database and is owned by <c>TenantDbContext</c>.
    /// </summary>
    /// <remarks>
    /// EF scaffolded this as <c>DropTable</c>. That is deliberately NOT what it does. A drop would destroy
    /// the only copy of the data if it ran before the tenant migration had copied it, and the two streams
    /// are independent — nothing guarantees their relative order. The table is RENAMED and retained
    /// instead, which:
    /// <list type="bullet">
    /// <item>removes platform ownership immediately, so no code can reach the old table;</item>
    /// <item>preserves every row in any deployment ordering, since the tenant migration copies from
    /// whichever name exists;</item>
    /// <item>leaves a verifiable before/after copy for reconciliation.</item>
    /// </list>
    /// Physically dropping <c>platform.Companies_MigratedToTenant</c> is a separate, explicitly authorized
    /// step for a later slice, once the move has been verified in each environment. Retiring it is cheap;
    /// recovering from an unverified drop is not.
    /// </remarks>
    public partial class MoveCompanyToTenantDatabase : Migration
    {
        private const string PlatformTable = "[platform].[Companies]";
        private const string RetiredTable = "[platform].[Companies_MigratedToTenant]";
        private const string RetiredName = "Companies_MigratedToTenant";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                IF OBJECT_ID(N'{PlatformTable}', N'U') IS NOT NULL
                    AND OBJECT_ID(N'{RetiredTable}', N'U') IS NULL
                BEGIN
                    EXEC sp_rename N'{PlatformTable}', N'{RetiredName}';
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                IF OBJECT_ID(N'{RetiredTable}', N'U') IS NOT NULL
                    AND OBJECT_ID(N'{PlatformTable}', N'U') IS NULL
                BEGIN
                    EXEC sp_rename N'{RetiredTable}', N'Companies';
                END
                """);
        }
    }
}
