using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Domain.Departments;
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

    // ---- FP-007 PHASE 1. Applied EXPLICITLY, like everything else here.
    //
    // There is no assembly scan and no convention that would pick these up: an entity absent from this
    // method is absent from the tenant model, absent from the migration stream, and — because
    // TenantCutoverCopyPlan derives its manifest from the model — absent from Shared→Dedicated cutover.
    // That last one fails silently, which is why the contributor is explicit rather than discovered.
    modelBuilder.ApplyConfiguration(new DepartmentConfiguration());
    modelBuilder.ApplyConfiguration(new DepartmentManagerConfiguration());
    modelBuilder.ApplyConfiguration(new EmployeeDepartmentAssignmentConfiguration());

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

    // Department belongs to a Company and to nothing else in Platform. It has NO branch foreign key,
    // because it has no branch: a department spans the branches of its company (ADR-026 decision 1).
    //
    // The intra-HR relationships — Department's self-referencing parent, DepartmentManager's two, and the
    // department history's three — are declared with the typed API in their own configurations, because
    // those principals are HR's own types. Only Company has to be named as a string.
    modelBuilder.Entity(typeof(Department))
      .HasOne("SSAS.Platform.Domain.Companies.Company", navigationName: null)
      .WithMany()
      .HasForeignKey(nameof(Department.CompanyId))
      .OnDelete(DeleteBehavior.Restrict);
  }
}
