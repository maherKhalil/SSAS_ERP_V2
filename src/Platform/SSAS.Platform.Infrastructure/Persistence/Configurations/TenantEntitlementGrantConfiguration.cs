using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

// ADDITIVE ENTITLEMENT GRANTS, APPEND-ONLY (FP-014, `OD-SUB-0011`).
//
// ---- THERE IS NO `CHECK` ENFORCING "ADDITIVE", AND ITS ABSENCE IS DELIBERATE.
//
// It cannot be written: whether a `LimitValue` raises anything depends on the plan carried by the
// subscription record in force, which is two joins away and varies with time. The rule is enforced at write
// time by the domain (`TenantEntitlementGrant.RaiseLimit` refuses a value at or below the plan's cap) and
// made structurally impossible to violate at read time by `max(plan, grants)` in `TenantEntitlement`.
//
// Stated here so nobody looks for the missing constraint and concludes it was forgotten.
public sealed class TenantEntitlementGrantConfiguration : IEntityTypeConfiguration<TenantEntitlementGrant>
{
  public void Configure(EntityTypeBuilder<TenantEntitlementGrant> builder)
  {
    builder.ToTable("TenantEntitlementGrants", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint(
        "CK_TenantEntitlementGrants_GrantKind", "[GrantKind] IN (N'ModuleGrant', N'LimitRaise')");

      // ---- EXACTLY ONE OF THE TWO SHAPES IS POPULATED.
      //
      // A row that is neither is a row the resolution function cannot interpret — it would be silently
      // skipped by both branches and the grant would appear to have been recorded while doing nothing.
      table.HasCheckConstraint(
        "CK_TenantEntitlementGrants_Shape",
        "([GrantKind] = N'ModuleGrant' AND [ModuleKey] IS NOT NULL AND [LimitKey] IS NULL AND " +
        "[LimitValue] IS NULL) OR ([GrantKind] = N'LimitRaise' AND [ModuleKey] IS NULL AND " +
        "[LimitKey] IS NOT NULL AND [LimitValue] IS NOT NULL)");

      table.HasCheckConstraint(
        "CK_TenantEntitlementGrants_LimitValue", "[LimitValue] IS NULL OR [LimitValue] >= 0");

      // An expiry at or before the effective instant is a grant that was never in force, which is a defect
      // rather than a fact about the tenant.
      table.HasCheckConstraint(
        "CK_TenantEntitlementGrants_Expiry",
        "[ExpiresUtc] IS NULL OR [ExpiresUtc] > [EffectiveFromUtc]");
    });

    builder.HasKey(grant => grant.Id);
    builder.Property(grant => grant.Id)
      .HasColumnName("TenantEntitlementGrantId")
      .ValueGeneratedNever();
    builder.Ignore(grant => grant.TenantEntitlementGrantId);

    builder.Property(grant => grant.TenantId).IsRequired();
    builder.Property(grant => grant.GrantKind).HasConversion<string>().HasMaxLength(32).IsRequired();
    builder.Property(grant => grant.ModuleKey)
      .HasConversion(key => key!.Value, value => ModuleKey.Create(value).Value)
      .HasMaxLength(ModuleKey.MaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation);
    builder.Property(grant => grant.LimitKey)
      .HasMaxLength(PlanLimit.KeyMaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation);
    builder.Property(grant => grant.LimitValue);
    builder.Property(grant => grant.EffectiveFromUtc).IsRequired();
    builder.Property(grant => grant.ExpiresUtc);
    builder.Property(grant => grant.CreatedUtc).IsRequired();
    builder.Property(grant => grant.GrantedBy)
      .HasMaxLength(TenantEntitlementGrant.ActorMaximumLength)
      .IsRequired();
    builder.Property(grant => grant.ReasonCode)
      .HasMaxLength(TenantEntitlementGrant.ReasonCodeMaximumLength);
    builder.Property(grant => grant.ReasonText)
      .HasMaxLength(TenantEntitlementGrant.ReasonTextMaximumLength);

    builder.HasOne<Tenant>()
      .WithMany()
      .HasForeignKey(grant => grant.TenantId)
      .OnDelete(DeleteBehavior.Restrict);

    // Grants are read on the same path and in the same query window as subscriptions.
    builder.HasIndex(grant => new { grant.TenantId, grant.EffectiveFromUtc })
      .HasDatabaseName("IX_TenantEntitlementGrants_Tenant_EffectiveFrom");
  }
}
