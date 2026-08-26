using Microsoft.EntityFrameworkCore.Migrations;
using SSAS.Platform.Infrastructure.Persistence.Seeding;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    // ==================================================================================================
    // THE CUTOVER SEED. THIS ONE *DOES* WRITE ROWS, AND ITS PREDECESSOR EXISTS TO CATCH THAT (T-041).
    // ==================================================================================================
    //
    // ---- READ THIS BESIDE `AddSubscriptionCommercialPlane`, WHICH REFUSES TO WRITE ANYTHING.
    //
    // That migration creates seven empty tables and ends by **counting plans, subscriptions and grants and
    // throwing if any exist**. This one inserts all three. The two are not in conflict, and the difference
    // is the point:
    //
    //   * that `THROW` is a check on **one migration's own effect** -- evaluated at the foot of its own
    //     `Up`, inside its own transaction, before any later migration runs. It is not a standing
    //     constraint on the tables and cannot see what a successor does;
    //   * it fires if a convenience backfill is ever added **to that file**, which is precisely what it was
    //     built to catch: an estate silently granted a commercial agreement nobody signed.
    //
    // **So the ordering is the design.** The plane is created empty and *proved* empty; filling it is a
    // separate, dated, reviewable act with its own decision behind it. Collapsing the two into one
    // migration would have destroyed exactly the evidence that made the fill reviewable.
    //
    // ---- WHAT IS BEING WRITTEN, AND THE DECISION THAT PUT IT HERE.
    //
    // `DEC-L-034`: a tenant without a subscription gets an **all-module plan with a 14-day term**. T-040
    // made the entitlement resolver real, so a tenant holding no subscription record now reaches no gated
    // module -- correct under `CON-0001`, and also how an entire existing estate is locked out. **This
    // migration is what stops that being a lockout**, and the two must ship together: T-040 deployed
    // without T-041 locks every tenant out of every module.
    //
    // `OD-SUB-0014` ruled a trial **is a plan with a short term, not a state and not a flag**, so nothing
    // here introduces a concept. There is no `IsTrial` column to add because there is no `IsTrial`.
    //
    // ---- THE SQL IS NOT IN THIS FILE, WHICH IS DELIBERATE.
    //
    // `TrialSubscriptionSeed.Sql` is built once from `TrialSubscription`, the single definition that
    // tenant creation also reads. `DEC-L-034` requires **one rule for existing and new tenants**; sharing
    // the definition is how that stays true instead of being asserted. It also makes the seed testable:
    // a migration runs once, so "re-running does not double-issue" can only be proved by a test that
    // executes **the same statement** twice, which is what `Integration.Tests` does.
    //
    // ---- `Down` DELIBERATELY DOES NOTHING.
    //
    // A data seed is not a schema operation, and the rows it wrote are **append-only history**
    // (`OD-SUB-0008`). A `Down` that deleted them would be rewriting a subscription history -- the exact
    // mutation `PreventAppendOnlyMutation` refuses at the write boundary -- and it could not tell a trial
    // record a tenant still holds from one they have since moved off. Reverting past
    // `AddSubscriptionCommercialPlane` drops the tables and takes these rows with them, which is the only
    // coherent reversal there is.
    public partial class AddTrialSubscriptionSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(TrialSubscriptionSeed.Sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty -- see the note above. Reversal is by dropping the tables, not by
            // deleting append-only rows.
        }
    }
}
