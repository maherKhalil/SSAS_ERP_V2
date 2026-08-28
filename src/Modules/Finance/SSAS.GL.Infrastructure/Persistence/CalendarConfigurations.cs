using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.GL.Domain.Calendar;

namespace SSAS.GL.Infrastructure.Persistence;

// THE FISCAL CALENDAR (REQ-GL-0009..0011, BR-GL-0003, OD-GL-0004).
//
// COMPANY-OWNED, which is what makes closing a period a company-scoped write: `FiscalYear` implements
// `ICompanyOwnedEntity`, so `TenantDbContext.ApplyCompanyRulesAsync` runs `AuthorizeCurrentCompanyAsync`
// before any close reaches SQL. The column is not the mechanism — the interface is — but the column is where
// a reader sees it.
public sealed class FiscalYearConfiguration : IEntityTypeConfiguration<FiscalYear>
{
  public void Configure(EntityTypeBuilder<FiscalYear> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("GlFiscalYears", GlPersistenceConstants.TenantSchema);

    builder.HasKey(year => year.Id);

    

    // The key is assigned in the constructor, so the store generates nothing (see the guard

    // `Every_constructor_keyed_entity_declares_its_key_value_generated_never`).

    builder.Property(year => year.Id).ValueGeneratedNever();

    builder.Property(year => year.TenantId).IsRequired();
    builder.Property(year => year.CompanyId).IsRequired();

    builder.Property(year => year.Code)
      .HasMaxLength(FiscalYear.MaximumCodeLength)
      .UseCollation(GlPersistenceConstants.OrdinalCollation)
      .IsRequired();

    builder.Property(year => year.StartUtc).IsRequired();
    builder.Property(year => year.EndUtc).IsRequired();

    builder.Property(year => year.CreatedUtc).IsRequired();
    builder.Property(year => year.CreatedBy).HasMaxLength(GlPersistenceConstants.ActorMaximumLength);
    builder.Property(year => year.ModifiedUtc).IsRequired();
    builder.Property(year => year.ModifiedBy).HasMaxLength(GlPersistenceConstants.ActorMaximumLength);

    builder.Property(year => year.RowVersion).IsRowVersion().IsConcurrencyToken();

    builder.Ignore(year => year.DomainEvents);

    // ---- THE YEAR OWNS ITS PERIODS, AND THE CASCADE SAYS SO.
    //
    // Cascade rather than Restrict, and it is the one cascade in this module. A period has no independent
    // life: it is created only through the year that validated the whole set for contiguity, and a period
    // orphaned from its year could not answer which calendar it belongs to. Contrast the journal tables
    // below, where cascade would be a way to delete a posted line.
    builder.HasMany(year => year.Periods)
      .WithOne()
      .HasForeignKey(period => period.FiscalYearId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.Navigation(year => year.Periods)
      .HasField("periods")
      .UsePropertyAccessMode(PropertyAccessMode.Field);

    builder.HasIndex(year => new { year.TenantId, year.CompanyId, year.Code })
      .IsUnique()
      .HasDatabaseName("UX_GlFiscalYears_Tenant_Company_Code");

    // ---- AND THERE IS DELIBERATELY NO INDEX ON (StartUtc, EndUtc). THE ABSENCE IS THE DECISION.
    //
    // Years must not overlap, and no index can enforce that — `DEC-L-084`. `DefineFiscalYearCommandHandler`
    // is the only enforcement, and `CalendarCommandHandlers.cs:73` weighs the residual exposure and
    // accepts it. **Adding a range index here would be reasonable for query support and would still
    // constrain nothing** — so it must not be read as closing the gap.
  }
}

public sealed class FiscalPeriodConfiguration : IEntityTypeConfiguration<FiscalPeriod>
{
  public void Configure(EntityTypeBuilder<FiscalPeriod> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("GlFiscalPeriods", GlPersistenceConstants.TenantSchema, table =>
    {
      // ---- THE ONE PART OF THE CONTIGUITY INVARIANT A CONSTRAINT CAN EXPRESS.
      //
      // A period must be non-empty. That a SET of periods partitions its year exactly is not expressible as
      // a row-level CHECK — it needs the sibling rows — so it lives in `FiscalYear.Create`, which is also
      // the only place a period is ever created. Recorded so the absence of a fuller constraint reads as a
      // deliberate boundary rather than an oversight.
      table.HasCheckConstraint("CK_GlFiscalPeriods_Range", "[EndUtc] > [StartUtc]");
      table.HasCheckConstraint("CK_GlFiscalPeriods_Status", "[Status] IN (N'Open', N'Closed')");
    });

    builder.HasKey(period => period.Id);

    

    // The key is assigned in the constructor, so the store generates nothing (see the guard

    // `Every_constructor_keyed_entity_declares_its_key_value_generated_never`).

    builder.Property(period => period.Id).ValueGeneratedNever();

    // Present because FiscalPeriod is ITenantOwnedEntity — which it must be, or the E3 cutover manifest
    // (derived by reflection over that interface) would not carry this table and the periods would silently
    // fail to copy. See the note on the entity.
    builder.Property(period => period.TenantId).IsRequired();
    builder.Property(period => period.FiscalYearId).IsRequired();

    builder.Property(period => period.Name)
      .HasMaxLength(FiscalYear.MaximumCodeLength)
      .IsRequired();

    builder.Property(period => period.StartUtc).IsRequired();
    builder.Property(period => period.EndUtc).IsRequired();

    builder.Property(period => period.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(GlPersistenceConstants.OrdinalCollation)
      .IsRequired();

    builder.Property(period => period.RowVersion).IsRowVersion().IsConcurrencyToken();

    builder.HasIndex(period => new { period.FiscalYearId, period.StartUtc })
      .HasDatabaseName("IX_GlFiscalPeriods_Year_Start");
  }
}
