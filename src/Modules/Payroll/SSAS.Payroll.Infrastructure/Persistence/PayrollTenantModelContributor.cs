using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Infrastructure.Persistence;

// PAYROLL'S CONTRIBUTION TO THE TENANT ERP MODEL (ADR-012, ADR-017).
//
// Tenant business data lives in ONE context and ONE migration stream, which Platform owns and may not extend
// with Payroll's types. This maps Payroll's entities into that model without either side referencing the
// other, and it is registered explicitly by the Host — never discovered.
//
// DETERMINISTIC, as the contract requires: the same mapping every time, with no dependence on tenant,
// request or ambient state. The contributor set participates in the EF model cache key, and a contributor
// that varied its output would make that key a lie.
//
// ================================================================================================
// SIX TYPES, ALL EXPLICIT, AND THE COST OF FORGETTING ONE IS SILENT.
// ================================================================================================
//
// There is no assembly scan. An entity absent from this method is absent from the tenant model, absent from
// the migration stream, and — because `TenantCutoverCopyPlan` derives its manifest from the model — absent
// from Shared-to-Dedicated cutover. **That last one fails SILENTLY.**
//
// FP-011 shipped `FiscalPeriod` and `JournalDraftLine` without `ITenantOwnedEntity` and both would have been
// missing from cutover with nothing to show for it. All six types below carry the interface, including the
// owned children: being an owned child is a DOMAIN fact, being copied is a REFLECTION fact, and only the
// interface expresses the second.
public sealed class PayrollTenantModelContributor : ITenantModelContributor
{
  public void Configure(ModelBuilder modelBuilder)
  {
    ArgumentNullException.ThrowIfNull(modelBuilder);

    // Listed principals-first so the dependency direction is visible. ORDER DOES NOT MATTER and is not
    // relied on: EF resolves relationships after every configuration is applied, and `TenantCutoverCopyPlan`
    // derives the copy order from the finished foreign-key graph rather than from this method.
    modelBuilder.ApplyConfiguration(new PayElementConfiguration());
    modelBuilder.ApplyConfiguration(new EmployeeCompensationConfiguration());
    modelBuilder.ApplyConfiguration(new PayElementAssignmentConfiguration());
    modelBuilder.ApplyConfiguration(new OneOffPaymentConfiguration());
    modelBuilder.ApplyConfiguration(new PayrollPeriodConfiguration());
    modelBuilder.ApplyConfiguration(new PayrollRunConfiguration());
    modelBuilder.ApplyConfiguration(new PayrollRunDraftLineConfiguration());
    modelBuilder.ApplyConfiguration(new PayrollRunLineConfiguration());

    // ---- THE FOREIGN KEYS TO THE PLATFORM-OWNED PRINCIPAL.
    //
    // Declared by PRINCIPAL TYPE NAME rather than CLR type, because Payroll cannot reference
    // `SSAS.Platform.Domain` — which is the boundary that makes those tables opaque to it. The constraints
    // are ordinary: Company lives in the TENANT catalog (`ADR-014` revision 1.1 Correction A), so these are
    // intra-catalog and legal. **Nothing here crosses the platform/tenant database boundary**
    // (`DEC-PAY-0008`), and an architecture guard asserts that.
    //
    // RESTRICT rather than Cascade: a company is archived, never deleted, and a cascade here would silently
    // erase a company's entire pay history along with it — including the append-only lines that exist
    // precisely so that cannot happen.
    //
    // WHICH TABLES GET A COMPANY KEY: the four company-owned roots. The line and assignment tables get none
    // — they carry a `TenantId` for cutover and are anchored by their foreign key to the header they belong
    // to, so a second constraint to Company would add nothing. Same treatment GL gave its line tables.
    foreach (var companyOwned in new[] { typeof(PayElement), typeof(EmployeeCompensation), typeof(PayrollPeriod), typeof(PayrollRun) })
    {
      modelBuilder.Entity(companyOwned)
        .HasOne("SSAS.Platform.Domain.Companies.Company", navigationName: null)
        .WithMany()
        .HasForeignKey("CompanyId")
        .OnDelete(DeleteBehavior.Restrict);
    }

    // ---- THE INTRA-PAYROLL KEY FROM ASSIGNMENTS TO ELEMENTS.
    //
    // RESTRICT, because an element is deactivated and never deleted — and because a cascade from an element
    // to the assignments referencing it would be a route to silently changing what someone is paid.
    modelBuilder.Entity<PayElementAssignment>()
      .HasOne<PayElement>()
      .WithMany()
      .HasForeignKey(assignment => assignment.PayElementId)
      .OnDelete(DeleteBehavior.Restrict);

    // ---- AND FROM RUN LINES TO ELEMENTS.
    //
    // RESTRICT on both line tables. For approved lines this is doubly load-bearing: `IAppendOnlyEntity`
    // already refuses a delete at the write boundary, and this makes the DATABASE agree rather than leaving
    // the two to disagree quietly. A cascade here would be a route to deleting pay history by deleting an
    // element, which is the thing the append-only marker exists to prevent.
    modelBuilder.Entity<PayrollRunDraftLine>()
      .HasOne<PayElement>()
      .WithMany()
      .HasForeignKey(line => line.PayElementId)
      .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<PayrollRunLine>()
      .HasOne<PayElement>()
      .WithMany()
      .HasForeignKey(line => line.PayElementId)
      .OnDelete(DeleteBehavior.Restrict);

    // ---- AND FROM RUNS TO PERIODS.
    modelBuilder.Entity<PayrollRun>()
      .HasOne<PayrollPeriod>()
      .WithMany()
      .HasForeignKey(run => run.PayrollPeriodId)
      .OnDelete(DeleteBehavior.Restrict);

    // ---- WHAT IS DELIBERATELY ABSENT.
    //
    // No foreign key from `PayrollEmployeeCompensation.EmployeeId` to HR's `Employee`, and none from
    // `PayrollRuns.JournalEntryId` or `PayrollPeriods.FiscalPeriodId` to GL's tables. Those are module
    // boundaries (`ADR-012`); a database constraint across one would couple the modules' migration streams
    // and make the boundary a fiction exactly where nobody looks for it.
  }
}
