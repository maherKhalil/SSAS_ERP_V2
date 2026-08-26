using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

// THE PLAN CATALOG (FP-014). PLATFORM-GLOBAL, SO THE UNIQUENESS KEY CARRIES NO `TenantId`.
//
// That single omission is the visible difference between this table and every tenant-owned table in the
// product, and it is `ADR-017` § Lookup classification's "tenants cannot create global rows" expressed as a
// constraint rather than as a comment.
public sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
  public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
  {
    builder.ToTable("SubscriptionPlans", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint(
        "CK_SubscriptionPlans_Status",
        "[Status] IN (N'Draft', N'Active', N'Retired')");
      table.HasCheckConstraint(
        "CK_SubscriptionPlans_PlanCode_NotBlank", "LEN(LTRIM(RTRIM([PlanCode]))) > 0");
      table.HasCheckConstraint(
        "CK_SubscriptionPlans_PlanName_NotBlank", "LEN(LTRIM(RTRIM([PlanName]))) > 0");
    });

    builder.HasKey(plan => plan.Id);
    builder.Property(plan => plan.Id).HasColumnName("SubscriptionPlanId").ValueGeneratedNever();
    builder.Ignore(plan => plan.SubscriptionPlanId);

    builder.Property(plan => plan.PlanCode)
      .HasConversion(code => code.Value, value => PlanCode.Create(value).Value)
      .HasMaxLength(PlanCode.MaximumLength)
      .IsRequired();
    builder.Property(plan => plan.NormalizedPlanCode)
      .HasField("normalizedPlanCode")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(PlanCode.MaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(plan => plan.PlanName)
      .HasConversion(name => name.Value, value => PlanName.Create(value).Value)
      .HasMaxLength(PlanName.MaximumLength)
      .IsRequired();

    builder.Property(plan => plan.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
    builder.Property(plan => plan.CreatedUtc).IsRequired();
    builder.Property(plan => plan.CreatedBy).HasMaxLength(256).IsRequired();
    builder.Property(plan => plan.ModifiedBy).HasMaxLength(256);
    builder.Property(plan => plan.RowVersion).IsRowVersion();

    // No `TenantId` in this index, and that is the point.
    builder.HasIndex(plan => plan.NormalizedPlanCode)
      .IsUnique()
      .HasDatabaseName("UX_SubscriptionPlans_NormalizedPlanCode");

    // ---- THE CHILD COLLECTIONS ARE OWNED, NOT SEPARATE AGGREGATES.
    //
    // A plan's modules, limits and prices have no identity or lifetime apart from the plan: nothing cites a
    // `PlanLimit` and nothing loads one on its own. Owned types give them their own tables with cascading
    // lifetime and no surrogate key, which is exactly what `data-model.md` specifies — the composite pair
    // IS the fact.
    builder.OwnsMany(plan => plan.ModuleGrants, grant =>
    {
      grant.ToTable("SubscriptionPlanModules", PlatformPersistenceConstants.Schema);
      grant.WithOwner().HasForeignKey(nameof(PlanModuleGrant.SubscriptionPlanId));
      grant.Property(item => item.SubscriptionPlanId).HasColumnName("SubscriptionPlanId");
      grant.Property(item => item.ModuleKey)
        .HasColumnName("ModuleKey")
        .HasConversion(key => key.Value, value => ModuleKey.Create(value).Value)
        .HasMaxLength(ModuleKey.MaximumLength)
        .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
        .IsRequired();
      grant.HasKey(nameof(PlanModuleGrant.SubscriptionPlanId), nameof(PlanModuleGrant.ModuleKey));
    });

    builder.OwnsMany(plan => plan.Limits, limit =>
    {
      limit.ToTable("SubscriptionPlanLimits", PlatformPersistenceConstants.Schema, table =>
        table.HasCheckConstraint("CK_SubscriptionPlanLimits_LimitValue", "[LimitValue] >= 0"));
      limit.WithOwner().HasForeignKey(nameof(PlanLimit.SubscriptionPlanId));
      limit.Property(item => item.SubscriptionPlanId).HasColumnName("SubscriptionPlanId");
      limit.Property(item => item.LimitKey)
        .HasMaxLength(PlanLimit.KeyMaximumLength)
        .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
        .IsRequired();
      // `bigint`, because a limit may one day count storage bytes or API calls and widening a key column
      // later is the expensive kind of change.
      limit.Property(item => item.LimitValue).IsRequired();
      limit.HasKey(nameof(PlanLimit.SubscriptionPlanId), nameof(PlanLimit.LimitKey));
    });

    builder.OwnsMany(plan => plan.Prices, price =>
    {
      price.ToTable("SubscriptionPlanPrices", PlatformPersistenceConstants.Schema, table =>
      {
        table.HasCheckConstraint("CK_SubscriptionPlanPrices_Amount", "[Amount] >= 0");
        table.HasCheckConstraint(
          "CK_SubscriptionPlanPrices_BillingPeriod", "[BillingPeriod] IN (N'Monthly', N'Annual')");
      });
      price.WithOwner().HasForeignKey(nameof(PlanPrice.SubscriptionPlanId));
      price.Property(item => item.SubscriptionPlanId).HasColumnName("SubscriptionPlanId");
      price.Property(item => item.CurrencyCode)
        .HasColumnType($"nchar({PlanPrice.CurrencyCodeLength})")
        .IsRequired();
      price.Property(item => item.BillingPeriod).HasConversion<string>().HasMaxLength(32).IsRequired();
      // `ADR-027`'s money representation, INHERITED. Not a decision of this package (`DEC-SUB-0008`).
      price.Property(item => item.Amount).HasColumnType("decimal(19,4)").IsRequired();
      price.HasKey(
        nameof(PlanPrice.SubscriptionPlanId),
        nameof(PlanPrice.CurrencyCode),
        nameof(PlanPrice.BillingPeriod));
    });
  }
}
