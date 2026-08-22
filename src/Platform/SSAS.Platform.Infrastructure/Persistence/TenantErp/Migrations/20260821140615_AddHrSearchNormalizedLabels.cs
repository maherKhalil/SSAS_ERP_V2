using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    // THE SEARCH COLUMNS (DEC-POS-0030), AND THE FP-007 DEFECT THEY CLOSE.
    //
    // `Positions.NormalizedTitle`, `JobGrades.NormalizedName`, `SalaryGrades.NormalizedName` and
    // `Departments.NormalizedName`. Upper-invariant, trimmed, binary-collated, maintained by the domain
    // beside the normalized code — and searched instead of the value-converted display columns, which EF
    // Core cannot translate inside a predicate.
    //
    // The department column is a FIX rather than a feature: `DepartmentReadService.SearchAsync` filtered on
    // `Name.Value.Contains(text)` and therefore threw on every search carrying a `searchText`, from FP-007
    // until now, with no test covering it.
    //
    // ================================================================================================
    // ADDED NULLABLE, BACKFILLED, THEN MADE REQUIRED — NOT ADDED WITH A DEFAULT.
    // ================================================================================================
    //
    // The scaffolded form was `AddColumn(nullable: false, defaultValue: "")`, which succeeds on a populated
    // table and leaves every existing row with an EMPTY search column. Those rows would then be invisible to
    // every name search forever, and nothing would fail — the worst shape a data migration can take, because
    // it looks like it worked.
    //
    // Three steps instead: add nullable, fill from the display column, then tighten to NOT NULL. The final
    // schema also carries no leftover default constraint, since the domain always supplies the value.
    //
    // ---- THE BACKFILL USES SQL SERVER'S `UPPER`, AND THE DOMAIN USES `ToUpperInvariant`.
    //
    // They agree for every character these columns realistically hold, and they are allowed to differ for
    // exotic ones: the backfilled value is only ever a starting point. Any subsequent write re-normalizes
    // through the domain, so a row that is edited converges on the domain's answer. Choosing SQL here is
    // what makes the migration a single set-based statement per table rather than a load-transform-save
    // pass over every tenant's rows.
    /// <inheritdoc />
    public partial class AddHrSearchNormalizedLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddSearchColumn(migrationBuilder, "SalaryGrades", "NormalizedName", "Name", 128);
            AddSearchColumn(migrationBuilder, "JobGrades", "NormalizedName", "Name", 128);
            AddSearchColumn(migrationBuilder, "Positions", "NormalizedTitle", "Title", 128);
            AddSearchColumn(migrationBuilder, "Departments", "NormalizedName", "Name", 200);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NormalizedName",
                schema: "tenant",
                table: "SalaryGrades");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                schema: "tenant",
                table: "JobGrades");

            migrationBuilder.DropColumn(
                name: "NormalizedTitle",
                schema: "tenant",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                schema: "tenant",
                table: "Departments");
        }

        // One table's three steps, written once. Four tables differing only in names and width is exactly
        // the shape where a hand-copied fourth block loses a step.
        private static void AddSearchColumn(
            MigrationBuilder migrationBuilder,
            string table,
            string searchColumn,
            string sourceColumn,
            int maximumLength)
        {
            migrationBuilder.AddColumn<string>(
                name: searchColumn,
                schema: "tenant",
                table: table,
                type: $"nvarchar({maximumLength})",
                maxLength: maximumLength,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.Sql(
                $"UPDATE [tenant].[{table}] " +
                $"SET [{searchColumn}] = UPPER(LTRIM(RTRIM([{sourceColumn}])));");

            migrationBuilder.AlterColumn<string>(
                name: searchColumn,
                schema: "tenant",
                table: table,
                type: $"nvarchar({maximumLength})",
                maxLength: maximumLength,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: $"nvarchar({maximumLength})",
                oldMaxLength: maximumLength,
                oldNullable: true,
                oldCollation: "Latin1_General_100_BIN2");
        }
    }
}
