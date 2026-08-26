using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    // ==================================================================================================
    // THREE OWNERSHIP FOREIGN KEYS MOVE FROM `Restrict` TO `Cascade`. HAND-WRITTEN, AND THAT IS THE
    // POINT (T-047).
    // ==================================================================================================
    //
    // ---- EF GENERATED THIS FILE EMPTY, AND THAT IS EXACTLY WHY IT HAD TO BE WRITTEN.
    //
    // `PersistenceDbContext` no longer forces `Restrict` onto ownership foreign keys, so the model now
    // agrees with the migrations snapshot — which has always implied `Cascade` for an owned relationship,
    // because **the snapshot format serialises no delete behaviour for one**. Scaffolding after the change
    // therefore produces an empty `Up`, which is the whole outcome T-047 was after.
    //
    // **But the DATABASE still has `Restrict`.** `AddSubscriptionCommercialPlane` created these three keys
    // that way, and migrations are diffed snapshot-against-model rather than database-against-model — so
    // **no future scaffold will ever notice.** Left alone, the loud divergence (six spurious operations in
    // every migration) would have been traded for a silent one: a schema that quietly disagrees with the
    // model, with nothing to report it.
    //
    // So this `Up` is written by hand. It is the only reconciliation that will ever be offered.
    //
    // ---- WHAT CHANGES IN BEHAVIOUR, WITHOUT OVERSTATING THE SAFETY.
    //
    // Deleting a `SubscriptionPlan` row now removes its module grants, limits and prices with it, where
    // before the database refused. **Through EF nothing changes at all** — owned rows are deleted with
    // their owner by definition, and EF ordered the deletes itself under `Restrict`. What changes is the
    // behaviour of a **raw** `DELETE` that bypasses the write boundary.
    //
    // **The window is narrow and it is not empty.** A plan any tenant is on is still protected by
    // `FK_TenantSubscriptions_SubscriptionPlans_SubscriptionPlanId`, which is a REFERENCE key and keeps
    // `Restrict`. `SubscriptionPlan`'s lifecycle has no removal — it is retired, never deleted, because
    // historical subscription records point at it. So the exposure is a raw delete of a plan nobody is on.
    //
    // **This migration does not claim that cannot happen.** These aggregates are archived rather than
    // deleted and the behaviour has very likely never fired, but *"has never fired"* is not *"cannot
    // fire"*. `SubscriptionPlanOwnershipCascadeSqlServerTests` asserts what the database now does, so the
    // trade is recorded as an executable fact rather than as a paragraph.
    //
    // ---- DATED, PER `DEC-L-039`.
    //
    // Written 2026-08-26. The claim above about what the database contained — `Restrict` on these three
    // keys and nothing else altered since — was true on that date and is the reason `Down` can restore it
    // exactly.
    public partial class RelaxOwnershipDeleteBehaviour : Migration
    {
        private const string Schema = "platform";

        private static readonly (string Table, string Name)[] OwnershipForeignKeys =
        [
            ("SubscriptionPlanLimits", "FK_SubscriptionPlanLimits_SubscriptionPlans_SubscriptionPlanId"),
            ("SubscriptionPlanModules", "FK_SubscriptionPlanModules_SubscriptionPlans_SubscriptionPlanId"),
            ("SubscriptionPlanPrices", "FK_SubscriptionPlanPrices_SubscriptionPlans_SubscriptionPlanId"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Rewrite(migrationBuilder, ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Rewrite(migrationBuilder, ReferentialAction.Restrict);
        }

        // One shape for both directions: a delete-behaviour change is a drop and a re-add either way, and
        // writing it twice is how the two drift apart.
        private static void Rewrite(MigrationBuilder migrationBuilder, ReferentialAction onDelete)
        {
            foreach (var (table, name) in OwnershipForeignKeys)
            {
                migrationBuilder.DropForeignKey(name: name, schema: Schema, table: table);
            }

            foreach (var (table, name) in OwnershipForeignKeys)
            {
                migrationBuilder.AddForeignKey(
                    name: name,
                    schema: Schema,
                    table: table,
                    column: "SubscriptionPlanId",
                    principalSchema: Schema,
                    principalTable: "SubscriptionPlans",
                    principalColumn: "SubscriptionPlanId",
                    onDelete: onDelete);
            }
        }
    }
}
