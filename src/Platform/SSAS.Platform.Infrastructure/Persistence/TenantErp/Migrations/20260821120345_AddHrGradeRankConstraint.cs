using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Migrations
{
    // RANK ORDER IS POSITIVE, AND THE DATABASE NOW SAYS SO (BRULE-POS-0007, ruled 2026-08-21).
    //
    // FP-008 Phase 1 enforced this in the domain alone and reported the gap rather than closing it: the
    // package's constraint list for the two grade tables named no rank check, and adding an unlisted
    // constraint would have been filling a gap the specification did not leave. The architect ruled the
    // constraint in, so it arrives here as its own additive migration rather than by editing the shipped
    // one — `ADR-018` forbids rewriting a migration that has run anywhere.
    //
    // ADDITIVE AND REVERSIBLE, and it needs no backfill: `JobGrade.Create` and `SalaryGrade.Create` have
    // refused a non-positive rank since Phase 1, so no row written through the application can violate it.
    // A tenant whose database was populated by direct SQL could, in which case this migration fails loudly
    // on that tenant rather than silently accepting data the model says is impossible — which is the
    // behaviour `DEC-POS-0026` chose for the same class of question.
    /// <inheritdoc />
    public partial class AddHrGradeRankConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_SalaryGrades_RankOrder_Positive",
                schema: "tenant",
                table: "SalaryGrades",
                sql: "[RankOrder] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JobGrades_RankOrder_Positive",
                schema: "tenant",
                table: "JobGrades",
                sql: "[RankOrder] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SalaryGrades_RankOrder_Positive",
                schema: "tenant",
                table: "SalaryGrades");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JobGrades_RankOrder_Positive",
                schema: "tenant",
                table: "JobGrades");
        }
    }
}
