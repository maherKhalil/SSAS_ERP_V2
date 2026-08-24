using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.GL.Domain.Calendar;
using SSAS.GL.Domain.Journals;

namespace SSAS.GL.Infrastructure.Persistence;

// GL'S CONTRIBUTION TO THE TENANT ERP MODEL (ADR-012, ADR-017).
//
// Tenant business data lives in ONE context and ONE migration stream, which Platform owns and may not extend
// with GL's types. This is how GL maps its own entities into that model without either side referencing the
// other, and it is registered explicitly by the Host — not discovered.
//
// IT IS DETERMINISTIC, AS THE CONTRACT REQUIRES: the same mapping every time, with no dependence on tenant,
// request or ambient state. The contributor set participates in the EF model cache key, and a contributor
// that varied its output would make that key a lie.
//
// ================================================================================================
// EVERY CONFIGURATION IS APPLIED EXPLICITLY, AND THE COST OF FORGETTING ONE IS SILENT.
// ================================================================================================
//
// There is no assembly scan and no convention that picks these up. An entity absent from this method is
// absent from the tenant model, absent from the migration stream, and — because `TenantCutoverCopyPlan`
// derives its manifest from the model — absent from Shared to Dedicated cutover. That last one fails
// SILENTLY, which is why the contributor is explicit rather than discovered.
//
// GL adds SEVEN tenant-owned types at once, more than any package since the platform itself. All seven are
// listed here and all seven implement `ITenantOwnedEntity` — including the owned children, which is not
// automatic: being owned is a domain fact, being copied is a reflection fact, and only the interface
// expresses the second.
public sealed class GlTenantModelContributor : ITenantModelContributor
{
  public void Configure(ModelBuilder modelBuilder)
  {
    ArgumentNullException.ThrowIfNull(modelBuilder);

    // Listed principals-first so the dependency direction is visible to a reader. ORDER DOES NOT MATTER and
    // is not relied on: EF resolves relationships after every configuration is applied, and
    // `TenantCutoverCopyPlan` derives the COPY order from the finished foreign-key graph rather than from
    // this method.
    modelBuilder.ApplyConfiguration(new AccountConfiguration());
    modelBuilder.ApplyConfiguration(new FiscalYearConfiguration());
    modelBuilder.ApplyConfiguration(new FiscalPeriodConfiguration());
    modelBuilder.ApplyConfiguration(new JournalDraftConfiguration());
    modelBuilder.ApplyConfiguration(new JournalDraftLineConfiguration());
    modelBuilder.ApplyConfiguration(new JournalEntryConfiguration());
    modelBuilder.ApplyConfiguration(new JournalLineConfiguration());

    // ---- THE FOREIGN KEYS TO PLATFORM-OWNED PRINCIPALS.
    //
    // Declared by PRINCIPAL TYPE NAME rather than by CLR type, because GL cannot reference
    // `SSAS.Platform.Domain` — which is exactly the boundary that makes those tables opaque to it. The
    // constraints themselves are ordinary: Company lives in the TENANT catalog (`ADR-014` revision 1.1
    // Correction A moved it there), so these are intra-catalog and legal. **Nothing here crosses the
    // platform/tenant database boundary** (`DEC-GL-0006`), and an architecture guard asserts that.
    //
    // RESTRICT rather than Cascade: a company is archived, never deleted, and a cascade here would silently
    // erase a company's entire ledger along with it.
    //
    // ---- WHICH TABLES GET A COMPANY KEY, AND WHICH DELIBERATELY DO NOT.
    //
    // The three COMPANY-OWNED roots get one: `GlFiscalYears`, `GlJournalDrafts`, `GlJournalEntries`.
    //
    // `GlAccounts` gets NONE, and that is `OD-GL-0003`: the chart is TENANT-level, so an account has no
    // company to point at. This absence is the ruling made visible in the schema.
    //
    // The line tables get none either. They carry a `TenantId` for cutover and are anchored by their foreign
    // key to the header they belong to; a second constraint to Company would add nothing, and this matches
    // how HR treats its assignment tables.
    modelBuilder.Entity(typeof(FiscalYear))
      .HasOne("SSAS.Platform.Domain.Companies.Company", navigationName: null)
      .WithMany()
      .HasForeignKey(nameof(FiscalYear.CompanyId))
      .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity(typeof(JournalDraft))
      .HasOne("SSAS.Platform.Domain.Companies.Company", navigationName: null)
      .WithMany()
      .HasForeignKey(nameof(JournalDraft.CompanyId))
      .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity(typeof(JournalEntry))
      .HasOne("SSAS.Platform.Domain.Companies.Company", navigationName: null)
      .WithMany()
      .HasForeignKey(nameof(JournalEntry.CompanyId))
      .OnDelete(DeleteBehavior.Restrict);

    // ---- THE INTRA-GL KEYS FROM LINES TO ACCOUNTS.
    //
    // GL's own types, so the typed API is used. RESTRICT because an account is deactivated and never
    // deleted (`BR-GL-0004` is about receiving transactions, not about disappearing) — and because a cascade
    // from an account to its posted lines would be a route to deleting ledger history, which is the thing
    // `IAppendOnlyEntity` exists to prevent.
    modelBuilder.Entity<JournalLine>()
      .HasOne<Domain.Accounts.Account>()
      .WithMany()
      .HasForeignKey(line => line.AccountId)
      .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<JournalDraftLine>()
      .HasOne<Domain.Accounts.Account>()
      .WithMany()
      .HasForeignKey(line => line.AccountId)
      .OnDelete(DeleteBehavior.Restrict);

    // ---- THE REVERSAL LINK (OD-GL-0006).
    //
    // A self-referencing key on the journal table. RESTRICT, so the original of a reversal cannot be
    // removed — which the append-only boundary already refuses, and this makes the database agree rather
    // than leaving the two to disagree quietly.
    modelBuilder.Entity<JournalEntry>()
      .HasOne<JournalEntry>()
      .WithMany()
      .HasForeignKey(entry => entry.ReversesJournalEntryId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
