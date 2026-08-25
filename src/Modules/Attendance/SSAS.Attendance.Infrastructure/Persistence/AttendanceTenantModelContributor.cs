using Microsoft.EntityFrameworkCore;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Leave;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;
using SSAS.BuildingBlocks.Infrastructure.Persistence;

namespace SSAS.Attendance.Infrastructure.Persistence;

// ATTENDANCE'S CONTRIBUTION TO THE TENANT ERP MODEL (ADR-012, ADR-017).
//
// Tenant business data lives in ONE context and ONE migration stream, which Platform owns and may not extend
// with Attendance's types. This maps them into that model without either side referencing the other, and it
// is registered explicitly by the Host — never discovered.
//
// DETERMINISTIC, as the contract requires: the same mapping every time, with no dependence on tenant,
// request or ambient state. The contributor set participates in the EF model cache key, and a contributor
// that varied its output would make that key a lie.
//
// ================================================================================================
// SEVEN TYPES, ALL EXPLICIT, AND THE COST OF FORGETTING ONE IS SILENT.
// ================================================================================================
//
// There is no assembly scan. An entity absent from this method is absent from the tenant model, absent from
// the migration stream, and — because `TenantCutoverCopyPlan` derives its manifest from the model — absent
// from Shared-to-Dedicated cutover. **That last one fails SILENTLY**: no error, no warning, no failing test
// until a tenant migrates and its data does not arrive.
//
// FP-011 shipped `FiscalPeriod` and `JournalDraftLine` without `ITenantOwnedEntity` and both would have been
// missing from cutover with nothing to show for it. All seven types below carry the interface, including the
// owned child: being an owned child is a DOMAIN fact, being copied is a REFLECTION fact, and only the
// interface expresses the second.
public sealed class AttendanceTenantModelContributor : ITenantModelContributor
{
  public void Configure(ModelBuilder modelBuilder)
  {
    ArgumentNullException.ThrowIfNull(modelBuilder);

    // Listed principals-first so the dependency direction is visible. ORDER DOES NOT MATTER and is not
    // relied on: EF resolves relationships after every configuration is applied, and `TenantCutoverCopyPlan`
    // derives the copy order from the finished foreign-key graph rather than from this method.
    modelBuilder.ApplyConfiguration(new WorkingCalendarConfiguration());
    modelBuilder.ApplyConfiguration(new CalendarHolidayConfiguration());
    modelBuilder.ApplyConfiguration(new AttendancePeriodConfiguration());
    modelBuilder.ApplyConfiguration(new AttendanceRecordConfiguration());
    modelBuilder.ApplyConfiguration(new LeaveTypeConfiguration());
    modelBuilder.ApplyConfiguration(new LeaveBalanceConfiguration());
    modelBuilder.ApplyConfiguration(new LeaveRequestConfiguration());

    // ---- THE FOREIGN KEYS TO THE PLATFORM-OWNED PRINCIPAL.
    //
    // Declared by PRINCIPAL TYPE NAME rather than CLR type, because Attendance cannot reference
    // `SSAS.Platform.Domain` — which is the boundary that makes those tables opaque to it. The constraints
    // are ordinary: Company lives in the TENANT catalog (`ADR-014` revision 1.1 Correction A), so these are
    // intra-catalog and legal. **Nothing here crosses the platform/tenant database boundary**
    // (`ADR-022`, `DEC-ATT-0006`), and an architecture guard asserts that.
    //
    // RESTRICT rather than Cascade: a company is archived, never deleted, and a cascade here would silently
    // erase a company's entire attendance history — including the append-only records that exist precisely
    // so that cannot happen.
    foreach (var companyOwned in new[]
      {
        typeof(WorkingCalendar), typeof(AttendancePeriod), typeof(AttendanceRecord),
        typeof(LeaveType), typeof(LeaveBalance), typeof(LeaveRequest)
      })
    {
      modelBuilder.Entity(companyOwned)
        .HasOne("SSAS.Platform.Domain.Companies.Company", navigationName: null)
        .WithMany()
        .HasForeignKey("CompanyId")
        .OnDelete(DeleteBehavior.Restrict);
    }

    // `CalendarHoliday` gets no company key: it carries a `TenantId` for cutover and is anchored by its
    // foreign key to the calendar it belongs to, so a second constraint to Company would add nothing. The
    // same treatment GL and Payroll gave their line tables.

    // ---- THE BRANCH KEY (OD-ATT-0011).
    //
    // `AttendanceRecord` is the only branch-owned type in this module, and it is the FIRST branch-owned
    // entity outside HR. Declared by principal type name for the same boundary reason as Company; Branch
    // also lives in the tenant catalog.
    //
    // RESTRICT: a branch is deactivated, never deleted, and a cascade would erase the attendance history of
    // everyone who ever worked there.
    modelBuilder.Entity<AttendanceRecord>()
      .HasOne("SSAS.Platform.Domain.Branches.Branch", navigationName: null)
      .WithMany()
      .HasForeignKey("BranchId")
      .OnDelete(DeleteBehavior.Restrict);

    // ---- THE INTRA-ATTENDANCE KEYS.
    //
    // RESTRICT throughout. For records this is doubly load-bearing: `IAppendOnlyEntity` already refuses a
    // delete at the write boundary, and this makes the DATABASE agree rather than leaving the two to
    // disagree quietly. A cascade from a period to its records would be a route to deleting attendance
    // history by deleting a period, which is the thing the append-only marker exists to prevent.
    modelBuilder.Entity<AttendanceRecord>()
      .HasOne<AttendancePeriod>()
      .WithMany()
      .HasForeignKey(record => record.AttendancePeriodId)
      .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<LeaveRequest>()
      .HasOne<LeaveType>()
      .WithMany()
      .HasForeignKey(request => request.LeaveTypeId)
      .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<LeaveBalance>()
      .HasOne<LeaveType>()
      .WithMany()
      .HasForeignKey(balance => balance.LeaveTypeId)
      .OnDelete(DeleteBehavior.Restrict);

    // ---- WHAT IS DELIBERATELY ABSENT.
    //
    // **No foreign key from any `EmployeeId` to HR's `Employee`**, even though it is available — HR and
    // Attendance share the Tenant DB, and `ADR-022` bars only the cross-DATABASE case.
    //
    // The availability is exactly why the absence has to be deliberate. `DEC-ATT-0003` says employee facts
    // come through `IEmployeeRoster`, and a database constraint would couple the two modules' migration
    // streams — making the boundary a fiction precisely where nobody looks for it. The employment-window
    // check runs at write time through the contract instead.
    //
    // **And no self-referencing key from an adjustment to the record it corrects.** `AdjustedRecordId` is a
    // plain `Guid?`. A self-referencing RESTRICT would add nothing an append-only table can violate — the
    // target can never be deleted — and EF's cascade analysis on a self-reference is a cost paid for no
    // guarantee.
  }
}
