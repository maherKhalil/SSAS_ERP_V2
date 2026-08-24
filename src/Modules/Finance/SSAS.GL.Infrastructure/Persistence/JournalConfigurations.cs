using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.GL.Domain.Journals;

namespace SSAS.GL.Infrastructure.Persistence;

// THE EDITABLE HALF (OD-GL-0007, option 3).
//
// Ordinary mutable tables with `RowVersion`. Everything interesting about this module's integrity is on the
// posted side; a draft is scratch space, and treating it as anything more would have cost `BR-GL-0002` its
// structural enforcement.
public sealed class JournalDraftConfiguration : IEntityTypeConfiguration<JournalDraft>
{
  public void Configure(EntityTypeBuilder<JournalDraft> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("GlJournalDrafts", GlPersistenceConstants.TenantSchema);

    builder.HasKey(draft => draft.Id);

    builder.Property(draft => draft.TenantId).IsRequired();
    builder.Property(draft => draft.CompanyId).IsRequired();
    builder.Property(draft => draft.EntryDateUtc).IsRequired();

    builder.Property(draft => draft.Description)
      .HasMaxLength(JournalDraft.MaximumDescriptionLength)
      .IsRequired();

    builder.Property(draft => draft.Reference)
      .HasMaxLength(JournalDraft.MaximumReferenceLength);

    builder.Property(draft => draft.CreatedUtc).IsRequired();
    builder.Property(draft => draft.CreatedBy).HasMaxLength(GlPersistenceConstants.ActorMaximumLength);
    builder.Property(draft => draft.ModifiedUtc).IsRequired();
    builder.Property(draft => draft.ModifiedBy).HasMaxLength(GlPersistenceConstants.ActorMaximumLength);

    builder.Property(draft => draft.RowVersion).IsRowVersion().IsConcurrencyToken();

    builder.Ignore(draft => draft.DomainEvents);

    builder.HasMany(draft => draft.Lines)
      .WithOne()
      .HasForeignKey(line => line.JournalDraftId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.Navigation(draft => draft.Lines)
      .HasField("lines")
      .UsePropertyAccessMode(PropertyAccessMode.Field);

    builder.HasIndex(draft => new { draft.TenantId, draft.CompanyId })
      .HasDatabaseName("IX_GlJournalDrafts_Tenant_Company");
  }
}

public sealed class JournalDraftLineConfiguration : IEntityTypeConfiguration<JournalDraftLine>
{
  public void Configure(EntityTypeBuilder<JournalDraftLine> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("GlJournalDraftLines", GlPersistenceConstants.TenantSchema, table =>
    {
      table.HasCheckConstraint(
        "CK_GlJournalDraftLines_SingleSided",
        "([Debit] > 0 AND [Credit] = 0) OR ([Credit] > 0 AND [Debit] = 0)");
    });

    builder.HasKey(line => line.Id);

    builder.Property(line => line.TenantId).IsRequired();
    builder.Property(line => line.JournalDraftId).IsRequired();
    builder.Property(line => line.LineNumber).IsRequired();
    builder.Property(line => line.AccountId).IsRequired();

    builder.Property(line => line.Debit)
      .HasPrecision(GlPersistenceConstants.MoneyPrecision, GlPersistenceConstants.MoneyScale)
      .IsRequired();
    builder.Property(line => line.Credit)
      .HasPrecision(GlPersistenceConstants.MoneyPrecision, GlPersistenceConstants.MoneyScale)
      .IsRequired();

    builder.Property(line => line.Description).HasMaxLength(JournalDraft.MaximumDescriptionLength);

    builder.HasIndex(line => new { line.JournalDraftId, line.LineNumber })
      .IsUnique()
      .HasDatabaseName("UX_GlJournalDraftLines_Draft_LineNumber");
  }
}

// ================================================================================================
// THE POSTED JOURNAL — APPEND-ONLY (BR-GL-0002, DEC-GL-0002, DEC-GL-0007).
// ================================================================================================
//
// TWO ABSENCES HERE ARE DELIBERATE AND BOTH ARE LOAD-BEARING.
//
// **No `RowVersion`.** `DEC-GL-0007` puts optimistic concurrency on mutable aggregates only. There is no
// concurrent update to detect here because the write boundary refuses updates to this type entirely; a
// version column would advertise a mutation that cannot happen and invite someone to write the update path
// it implies.
//
// **No cascade from the header to the lines.** Cascade would be a supported way to DELETE posted lines, and
// deleting a posted line is precisely what `IAppendOnlyEntity` exists to refuse. `Restrict` means the
// database agrees with the write boundary instead of quietly offering a route around it.
public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
  public void Configure(EntityTypeBuilder<JournalEntry> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("GlJournalEntries", GlPersistenceConstants.TenantSchema);

    builder.HasKey(entry => entry.Id);

    builder.Property(entry => entry.TenantId).IsRequired();
    builder.Property(entry => entry.CompanyId).IsRequired();
    builder.Property(entry => entry.FiscalYearId).IsRequired();
    builder.Property(entry => entry.FiscalPeriodId).IsRequired();

    builder.Property(entry => entry.JournalNumber)
      .HasMaxLength(64)
      .UseCollation(GlPersistenceConstants.OrdinalCollation)
      .IsRequired();

    builder.Property(entry => entry.EntryDateUtc).IsRequired();

    builder.Property(entry => entry.Description)
      .HasMaxLength(JournalDraft.MaximumDescriptionLength)
      .IsRequired();

    builder.Property(entry => entry.Reference)
      .HasMaxLength(JournalDraft.MaximumReferenceLength);

    builder.Property(entry => entry.ReversesJournalEntryId);

    builder.Property(entry => entry.CreatedUtc).IsRequired();
    builder.Property(entry => entry.CreatedBy).HasMaxLength(GlPersistenceConstants.ActorMaximumLength);
    builder.Property(entry => entry.ModifiedUtc).IsRequired();
    builder.Property(entry => entry.ModifiedBy).HasMaxLength(GlPersistenceConstants.ActorMaximumLength);

    builder.Ignore(entry => entry.DomainEvents);

    builder.HasMany(entry => entry.Lines)
      .WithOne()
      .HasForeignKey(line => line.JournalEntryId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.Navigation(entry => entry.Lines)
      .HasField("lines")
      .UsePropertyAccessMode(PropertyAccessMode.Field);

    // ---- BR-GL-0005 AS SCOPED BY OD-GL-0004.
    //
    // Unique within (Tenant, Company, FiscalYear). The tenant column leads because every GL query is
    // tenant-filtered first, so the index is useful for reads as well as for the constraint.
    //
    // UNIQUE, NOT GAPLESS — `AC-GL-0013` asserts uniqueness only. A gapless sequence is a materially harder
    // obligation that forbids a failed attempt consuming a number, and nobody has agreed to it.
    builder.HasIndex(entry => new { entry.TenantId, entry.CompanyId, entry.FiscalYearId, entry.JournalNumber })
      .IsUnique()
      .HasDatabaseName("UX_GlJournalEntries_Tenant_Company_Year_Number");

    // ---- ONE REVERSAL PER ORIGINAL, ENFORCED BY THE DATABASE.
    //
    // `JournalErrors.AlreadyReversed` refuses the second reversal in the aggregate, but that check reads
    // state and two concurrent requests can both read "not yet reversed". A FILTERED unique index makes the
    // race unwinnable: the second writer loses at commit. Filtered so the many NULLs on original journals do
    // not collide with each other.
    builder.HasIndex(entry => new { entry.TenantId, entry.ReversesJournalEntryId })
      .IsUnique()
      .HasFilter("[ReversesJournalEntryId] IS NOT NULL")
      .HasDatabaseName("UX_GlJournalEntries_OneReversalPerOriginal");

    builder.HasIndex(entry => new { entry.TenantId, entry.CompanyId, entry.EntryDateUtc })
      .HasDatabaseName("IX_GlJournalEntries_Tenant_Company_EntryDate");
  }
}

public sealed class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
  public void Configure(EntityTypeBuilder<JournalLine> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("GlJournalLines", GlPersistenceConstants.TenantSchema, table =>
    {
      table.HasCheckConstraint(
        "CK_GlJournalLines_SingleSided",
        "([Debit] > 0 AND [Credit] = 0) OR ([Credit] > 0 AND [Debit] = 0)");
    });

    builder.HasKey(line => line.Id);

    builder.Property(line => line.TenantId).IsRequired();
    builder.Property(line => line.JournalEntryId).IsRequired();
    builder.Property(line => line.LineNumber).IsRequired();
    builder.Property(line => line.AccountId).IsRequired();

    builder.Property(line => line.Debit)
      .HasPrecision(GlPersistenceConstants.MoneyPrecision, GlPersistenceConstants.MoneyScale)
      .IsRequired();
    builder.Property(line => line.Credit)
      .HasPrecision(GlPersistenceConstants.MoneyPrecision, GlPersistenceConstants.MoneyScale)
      .IsRequired();

    builder.Property(line => line.Description).HasMaxLength(JournalDraft.MaximumDescriptionLength);

    builder.HasIndex(line => new { line.JournalEntryId, line.LineNumber })
      .IsUnique()
      .HasDatabaseName("UX_GlJournalLines_Entry_LineNumber");

    // The balance enquiry's access path (`REQ-GL-0013`): movements for one account within a tenant.
    builder.HasIndex(line => new { line.TenantId, line.AccountId })
      .HasDatabaseName("IX_GlJournalLines_Tenant_Account");
  }
}
