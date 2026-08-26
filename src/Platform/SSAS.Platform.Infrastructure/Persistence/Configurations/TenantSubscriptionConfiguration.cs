using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

// THE APPEND-ONLY SUBSCRIPTION HISTORY (FP-014, `OD-SUB-0008`).
//
// ---- THREE COLUMNS ARE ABSENT AND ALL THREE ABSENCES ARE THE SAME ABSENCE.
//
// No `EffectiveToUtc`, no `RowVersion`, no `ModifiedUtc`/`ModifiedBy`. The row is never updated: closing an
// interval would mean UPDATING the previous row, which is the history mutation append-only exists to
// prevent, and a record that is never updated has no concurrency state to protect. The interval is derived
// by ordering. `EmployeePositionAssignment` established all of this and this table follows it rather than
// inventing a second history shape.
//
// **`PlatformDbContext.PreventAppendOnlyMutation` refuses `Modified` and `Deleted` for this type**, on both
// innermost save overloads, so the absence of those columns is backed by a mechanism rather than by hope.
public sealed class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
  public void Configure(EntityTypeBuilder<TenantSubscription> builder)
  {
    builder.ToTable("TenantSubscriptions", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint(
        "CK_TenantSubscriptions_TermKind", "[TermKind] IN (N'Fixed', N'Perpetual')");

      // ---- THE PERPETUAL MARKER MADE EXPLICIT (`OD-SUB-0009`).
      //
      // `TermEndUtc` is null IF AND ONLY IF the term is perpetual. Without this, a null end could mean
      // "never expires" or "nobody has set it yet" — and since expiry is the one commercial event that
      // refuses login for a whole tenant, the two readings are not interchangeable.
      table.HasCheckConstraint(
        "CK_TenantSubscriptions_Term",
        "([TermKind] = N'Perpetual' AND [TermEndUtc] IS NULL) OR " +
        "([TermKind] = N'Fixed' AND [TermEndUtc] IS NOT NULL AND [TermEndUtc] > [TermStartUtc])");
    });

    builder.HasKey(subscription => subscription.Id);
    builder.Property(subscription => subscription.Id)
      .HasColumnName("TenantSubscriptionId")
      .ValueGeneratedNever();
    builder.Ignore(subscription => subscription.TenantSubscriptionId);

    builder.Property(subscription => subscription.TenantId).IsRequired();
    builder.Property(subscription => subscription.SubscriptionPlanId).IsRequired();
    builder.Property(subscription => subscription.EffectiveFromUtc).IsRequired();
    builder.Property(subscription => subscription.BillingCurrencyCode)
      .HasColumnType($"nchar({TenantSubscription.CurrencyCodeLength})")
      .IsRequired();
    builder.Property(subscription => subscription.CreatedUtc).IsRequired();
    builder.Property(subscription => subscription.ChangedBy)
      .HasMaxLength(TenantSubscription.ActorMaximumLength)
      .IsRequired();
    builder.Property(subscription => subscription.ChangeReasonCode)
      .HasMaxLength(TenantSubscription.ReasonCodeMaximumLength);
    builder.Property(subscription => subscription.ChangeReasonText)
      .HasMaxLength(TenantSubscription.ReasonTextMaximumLength);

    // The term is a value object over three columns; `Rehydrate` validates on the way back, so a row written
    // by any path that bypassed the domain cannot materialise into an object the model believes is valid.
    builder.OwnsOne(subscription => subscription.Term, term =>
    {
      term.Property(item => item.Kind).HasColumnName("TermKind").HasConversion<string>()
        .HasMaxLength(32).IsRequired();
      term.Property(item => item.StartUtc).HasColumnName("TermStartUtc").IsRequired();
      term.Property(item => item.EndUtc).HasColumnName("TermEndUtc");
    });
    builder.Navigation(subscription => subscription.Term).IsRequired();

    // ---- AN INTRA-DATABASE FOREIGN KEY, WHICH `DEC-SUB-0009` PERMITS.
    //
    // That decision bars **cross-database** keys, not this one: `Tenant` and this row are both in the
    // Platform database. `Restrict` rather than cascade — a tenant's commercial history is not a thing to
    // delete alongside it, and `Tenant` is archived rather than removed in any case.
    builder.HasOne<Tenant>()
      .WithMany()
      .HasForeignKey(subscription => subscription.TenantId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<SubscriptionPlan>()
      .WithMany()
      .HasForeignKey(subscription => subscription.SubscriptionPlanId)
      .OnDelete(DeleteBehavior.Restrict);

    // ---- ONE INDEX DOING TWO JOBS, AND IT IS ONE INDEX ON PURPOSE.
    //
    // **The uniqueness backstop for "exactly one in force".** The monotonic-append rule cannot be a table
    // `CHECK` — "strictly greater than the current maximum for this tenant" is not expressible over one row.
    // The domain refuses the ordinary mistake and the write takes a lock on the tenant row; this unique key
    // is what makes two racing appends at the same instant **impossible** rather than merely rare.
    //
    // **And the read path.** Every entitlement read is "the greatest `EffectiveFromUtc <= T` for this
    // tenant", so descending on that column makes it a seek to the first row rather than a scan and a sort.
    //
    // These were first written as two `HasIndex` calls over the same columns. EF Core collapses those into
    // ONE index — the migration proved it — so declaring two was a configuration that read as two and built
    // as one. Stated as a single index instead, because a comment describing an index that does not exist is
    // worse than no comment.
    builder.HasIndex(subscription => new { subscription.TenantId, subscription.EffectiveFromUtc })
      .IsUnique()
      .IsDescending(false, true)
      .HasDatabaseName("UX_TenantSubscriptions_Tenant_EffectiveFromDesc");
  }
}
