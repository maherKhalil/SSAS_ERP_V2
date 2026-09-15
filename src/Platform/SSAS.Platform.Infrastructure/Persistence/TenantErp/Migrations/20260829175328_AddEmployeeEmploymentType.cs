using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeEmploymentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- ⚠ EVERY EXISTING EMPLOYEE BECOMES FullTime, AND THAT IS A STATED ASSUMPTION.
            //
            // `defaultValue: 0` is `EmploymentType.FullTime`, so this migration decides the engagement of
            // every row already in a customer database. **The enum was built for exactly that**: its own
            // comment records that `FullTime` is `default` so employees written before the property existed
            // keep the arrangement they already had, following the same construction used when `SalaryType`
            // arrived.
            //
            // **The default is also what makes this migration safe on a populated table.** `nullable: false`
            // with no default fails against any table that already has rows — a production-only failure
            // that no test here could see, because every catalogue in the suite is created empty and
            // migrated before a row exists.
            //
            // ⚠ IT TOUCHES AN OPEN OWNER DECISION. Whether a non-full-time employee will be hired is not
            // yet answered; this assigns an engagement to existing rows rather than waiting for it. That is
            // the only defensible default — the alternative is a null nobody can act on — but it is a
            // decision made here, not one deferred.
            migrationBuilder.AddColumn<int>(
                name: "EmploymentType",
                schema: "tenant",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmploymentType",
                schema: "tenant",
                table: "Employees");
        }
    }
}
