using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Infrastructure.Persistence;

// HR'S CONTRIBUTION TO THE TENANT ERP MODEL (FP-006C3, ADR-012, ADR-017).
//
// Tenant business data lives in ONE context and ONE migration stream, which Platform owns and may not extend
// with HR's types. This is how HR maps its own entities into that model without either side referencing the
// other, and it is registered explicitly by the Host — not discovered.
//
// IT IS DETERMINISTIC, AS THE CONTRACT REQUIRES. The same mapping every time, with no dependence on tenant,
// request or ambient state: the contributor set participates in the EF model cache key, and a contributor
// that varied its output would make that key a lie.
public sealed class HrTenantModelContributor : ITenantModelContributor
{
  public void Configure(ModelBuilder modelBuilder)
  {
    ArgumentNullException.ThrowIfNull(modelBuilder);

    modelBuilder.ApplyConfiguration(new EmployeeConfiguration());
    modelBuilder.ApplyConfiguration(new EmployeeBranchAssignmentConfiguration());

    // ---- THE FOREIGN KEYS TO PLATFORM-OWNED PRINCIPALS.
    //
    // Declared by PRINCIPAL TYPE NAME rather than by CLR type, because HR cannot reference
    // SSAS.Platform.Domain — which is exactly the boundary that makes these tables opaque to it. The
    // constraints themselves are ordinary: Company and Branch both live in the TENANT catalog (ADR-014
    // revision 1.1 Correction A moved Company there), so these are intra-catalog and legal. Nothing here
    // crosses the platform/tenant database boundary.
    //
    // RESTRICT rather than Cascade: a company is archived and a branch deactivated, never deleted, and a
    // cascade here would silently erase employment records along with them.
    //
    // Resolved against the model Platform has ALREADY configured — contributors run after Platform's own
    // configurations are applied, so both principal types are present by the time this executes.
    modelBuilder.Entity(typeof(Employee))
      .HasOne("SSAS.Platform.Domain.Companies.Company", navigationName: null)
      .WithMany()
      .HasForeignKey(nameof(Employee.CompanyId))
      .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity(typeof(Employee))
      .HasOne("SSAS.Platform.Domain.Branches.Branch", navigationName: null)
      .WithMany()
      .HasForeignKey(nameof(Employee.BranchId))
      .OnDelete(DeleteBehavior.Restrict);
  }
}
