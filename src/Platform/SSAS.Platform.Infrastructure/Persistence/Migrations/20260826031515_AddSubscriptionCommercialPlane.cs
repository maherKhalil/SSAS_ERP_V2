using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    // ==================================================================================================
    // THE COMMERCIAL PLANE'S TABLES. NO BACKFILL, NO DEFAULT PLAN, AND THAT IS A RELEASE DECISION.
    // ==================================================================================================
    //
    // ---- WHAT THIS MIGRATION DELIBERATELY DOES NOT DO.
    //
    // It creates seven empty tables and writes NOT ONE ROW. No default plan, no subscription assigned to
    // any existing tenant, no module definition seeded. `CON-0001` and `OD-SUB-0004` are explicit that
    // entitlement is RECORDED, never assumed: inventing a row to keep an estate working would be inventing
    // a commercial agreement nobody signed.
    //
    // ---- WHAT THAT MEANS AT CUTOVER, STATED SO THE SEQUENCING IS REVIEWABLE RATHER THAN DISCOVERED.
    //
    // **Today this changes nothing.** The enablement seam (T-032) resolves through
    // `TransitionalGrantsEveryModuleEntitlement`, which grants every module to every tenant and does not
    // read these tables at all. Applying this migration is invisible to every caller.
    //
    // **On the day the resolver is switched to read them, a tenant with no subscription row reaches no
    // gated module.** With four gateable modules that is HR, Finance/GL, Payroll and Attendance -- in
    // practice the whole product for that tenant, since the platform plane stays reachable by
    // `REQ-SUB-0013` and nothing else does. That is correct per `CON-0001`, and it is also how an entire
    // existing estate is locked out in one deploy.
    //
    // **So the switch is a separate act with a separate decision.** The ordering it requires: apply this
    // migration, populate plans and per-tenant subscriptions, verify coverage, and only then change the
    // registration. Doing the last step first is the failure this comment exists to prevent.
    //
    // ---- THE VERIFICATION AT THE FOOT OF `Up` IS NOT DECORATION.
    //
    // It asserts this migration created no plan, no subscription and no grant, in the `DEC-POS-0026`
    // shape: fail loudly rather than assume. Trivially true today, and here so a later edit adding a
    // convenience backfill fails at deployment with a named diagnosis instead of silently granting an
    // estate something nobody agreed to.
    public partial class AddSubscriptionCommercialPlane : Migration
    {
        // Hoisted from the scaffolded inline arrays: CA1861 refuses a constant array allocated at each
        // call site, and this is the shape `AddUserCompanyAccess` already uses.
        private static readonly string[] TenantEffectiveFromColumns = ["TenantId", "EffectiveFromUtc"];
        private static readonly bool[] TenantEffectiveFromDescending = [false, true];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModuleDefinitions",
                schema: "platform",
                columns: table => new
                {
                    ModuleDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsGateable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleDefinitions", x => x.ModuleDefinitionId);
                    table.CheckConstraint("CK_ModuleDefinitions_ModuleKey_NotBlank", "LEN(LTRIM(RTRIM([ModuleKey]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                schema: "platform",
                columns: table => new
                {
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NormalizedPlanCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    PlanName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.SubscriptionPlanId);
                    table.CheckConstraint("CK_SubscriptionPlans_PlanCode_NotBlank", "LEN(LTRIM(RTRIM([PlanCode]))) > 0");
                    table.CheckConstraint("CK_SubscriptionPlans_PlanName_NotBlank", "LEN(LTRIM(RTRIM([PlanName]))) > 0");
                    table.CheckConstraint("CK_SubscriptionPlans_Status", "[Status] IN (N'Draft', N'Active', N'Retired')");
                });

            migrationBuilder.CreateTable(
                name: "TenantEntitlementGrants",
                schema: "platform",
                columns: table => new
                {
                    TenantEntitlementGrantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrantKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ModuleKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    LimitKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    LimitValue = table.Column<long>(type: "bigint", nullable: true),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GrantedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ReasonText = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantEntitlementGrants", x => x.TenantEntitlementGrantId);
                    table.CheckConstraint("CK_TenantEntitlementGrants_Expiry", "[ExpiresUtc] IS NULL OR [ExpiresUtc] > [EffectiveFromUtc]");
                    table.CheckConstraint("CK_TenantEntitlementGrants_GrantKind", "[GrantKind] IN (N'ModuleGrant', N'LimitRaise')");
                    table.CheckConstraint("CK_TenantEntitlementGrants_LimitValue", "[LimitValue] IS NULL OR [LimitValue] >= 0");
                    table.CheckConstraint("CK_TenantEntitlementGrants_Shape", "([GrantKind] = N'ModuleGrant' AND [ModuleKey] IS NOT NULL AND [LimitKey] IS NULL AND [LimitValue] IS NULL) OR ([GrantKind] = N'LimitRaise' AND [ModuleKey] IS NULL AND [LimitKey] IS NOT NULL AND [LimitValue] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TenantEntitlementGrants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlanLimits",
                schema: "platform",
                columns: table => new
                {
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LimitKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    LimitValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlanLimits", x => new { x.SubscriptionPlanId, x.LimitKey });
                    table.CheckConstraint("CK_SubscriptionPlanLimits_LimitValue", "[LimitValue] >= 0");
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanLimits_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalSchema: "platform",
                        principalTable: "SubscriptionPlans",
                        principalColumn: "SubscriptionPlanId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlanModules",
                schema: "platform",
                columns: table => new
                {
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlanModules", x => new { x.SubscriptionPlanId, x.ModuleKey });
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanModules_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalSchema: "platform",
                        principalTable: "SubscriptionPlans",
                        principalColumn: "SubscriptionPlanId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlanPrices",
                schema: "platform",
                columns: table => new
                {
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", nullable: false),
                    BillingPeriod = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlanPrices", x => new { x.SubscriptionPlanId, x.CurrencyCode, x.BillingPeriod });
                    table.CheckConstraint("CK_SubscriptionPlanPrices_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_SubscriptionPlanPrices_BillingPeriod", "[BillingPeriod] IN (N'Monthly', N'Annual')");
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanPrices_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalSchema: "platform",
                        principalTable: "SubscriptionPlans",
                        principalColumn: "SubscriptionPlanId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantSubscriptions",
                schema: "platform",
                columns: table => new
                {
                    TenantSubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TermKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TermStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TermEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    BillingCurrencyCode = table.Column<string>(type: "nchar(3)", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ChangeReasonCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ChangeReasonText = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSubscriptions", x => x.TenantSubscriptionId);
                    table.CheckConstraint("CK_TenantSubscriptions_Term", "([TermKind] = N'Perpetual' AND [TermEndUtc] IS NULL) OR ([TermKind] = N'Fixed' AND [TermEndUtc] IS NOT NULL AND [TermEndUtc] > [TermStartUtc])");
                    table.CheckConstraint("CK_TenantSubscriptions_TermKind", "[TermKind] IN (N'Fixed', N'Perpetual')");
                    table.ForeignKey(
                        name: "FK_TenantSubscriptions_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalSchema: "platform",
                        principalTable: "SubscriptionPlans",
                        principalColumn: "SubscriptionPlanId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantSubscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ModuleDefinitions_ModuleKey",
                schema: "platform",
                table: "ModuleDefinitions",
                column: "ModuleKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SubscriptionPlans_NormalizedPlanCode",
                schema: "platform",
                table: "SubscriptionPlans",
                column: "NormalizedPlanCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantEntitlementGrants_Tenant_EffectiveFrom",
                schema: "platform",
                table: "TenantEntitlementGrants",
                columns: TenantEffectiveFromColumns);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_SubscriptionPlanId",
                schema: "platform",
                table: "TenantSubscriptions",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "UX_TenantSubscriptions_Tenant_EffectiveFromDesc",
                schema: "platform",
                table: "TenantSubscriptions",
                columns: TenantEffectiveFromColumns,
                unique: true,
                descending: TenantEffectiveFromDescending);

            // ---- NO BACKFILL, NO DEFAULT PLAN -- ASSERTED, NOT ASSUMED (`DEC-POS-0026` shape).
            migrationBuilder.Sql(
                """
                DECLARE @plans int = (SELECT COUNT(*) FROM [platform].[SubscriptionPlans]);
                DECLARE @subscriptions int = (SELECT COUNT(*) FROM [platform].[TenantSubscriptions]);
                DECLARE @grants int = (SELECT COUNT(*) FROM [platform].[TenantEntitlementGrants]);

                IF (@plans <> 0 OR @subscriptions <> 0 OR @grants <> 0)
                BEGIN
                    DECLARE @message nvarchar(2048) = CONCAT(
                        N'FP-014 T-035: this migration must create the commercial plane EMPTY. Database ',
                        DB_NAME(), N' has ', @plans, N' plan(s), ', @subscriptions,
                        N' subscription(s) and ', @grants, N' grant(s) after it ran. CON-0001 and',
                        N' OD-SUB-0004 forbid a default plan and any backfill: entitlement is recorded,',
                        N' never assumed. Remedy: remove the seeding from this migration and assign',
                        N' subscriptions as a separate, reviewable act BEFORE the entitlement resolver',
                        N' is switched to read these tables.');
                    THROW 51014, @message, 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModuleDefinitions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "SubscriptionPlanLimits",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "SubscriptionPlanModules",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "SubscriptionPlanPrices",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "TenantEntitlementGrants",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "TenantSubscriptions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans",
                schema: "platform");
        }
    }
}
