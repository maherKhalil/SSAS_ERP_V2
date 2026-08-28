using Microsoft.Data.SqlClient;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using Microsoft.EntityFrameworkCore;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;
using SSAS.Attendance.Infrastructure.Persistence;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// ================================================================================================
// ATTENDANCE AGAINST REAL SQL (FP-013). THIS IS WHERE THE GUARANTEES LIVE.
// ================================================================================================
//
// **`IAppendOnlyEntity` is enforced by `TenantDbContext.PreventAppendOnlyMutation`, not by
// `AttendanceRecord`.** A unit test of the aggregate would happily mutate a record and never learn that the
// write boundary refuses — which is precisely why `OD-ATT-0012`'s adjustments-never-edits ruling is only
// really proved here.
//
// Column types are asserted from `sys.columns` rather than from the EF model. Asserting from the model tests
// the model's opinion of the database; FP-009 established that the catalog views are the only version that
// catches a hand-written migration.
public sealed class AttendanceSchemaSqlServerTests
{
  [Fact]
  [Trait("Decision", "DEC-ATT-0005")]
  public async Task Every_attendance_string_column_is_nvarchar()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();

    // `Constraints.md` requires Arabic and English, and a leave type's name is exactly the field a user
    // writes in their own language.
    var nonUnicode = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.columns AS c
      JOIN sys.tables AS t ON t.object_id = c.object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
      WHERE s.name = 'tenant' AND t.name LIKE 'Attendance%'
        AND ty.name IN ('varchar', 'char', 'text');
      """);

    Assert.Equal(0, nonUnicode);
  }

  // ================================================================================================
  // NO MONEY COLUMN EXISTS, ASSERTED POSITIVELY (DEC-ATT-0004, AC-ATT-0010).
  // ================================================================================================
  //
  // Money in this product is `decimal(19,4)` (`ADR-027` d1). **No column in this module uses it**, and that
  // is the module boundary made checkable: Attendance records HOW MUCH, Payroll decides what it is worth.
  //
  // A rule a test can check is a rule; a rule only a reviewer can check is a hope.
  [Fact]
  [Trait("Decision", "DEC-ATT-0004")]
  public async Task No_attendance_column_uses_the_money_type_and_every_quantity_is_decimal_9_2()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();

    var money = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.columns AS c
      JOIN sys.tables AS t ON t.object_id = c.object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
      WHERE s.name = 'tenant' AND t.name LIKE 'Attendance%'
        AND ty.name = 'decimal' AND c.precision = 19 AND c.scale = 4;
      """);

    Assert.Equal(0, money);

    // And every decimal that IS there is the quantity shape.
    var wrongQuantity = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.columns AS c
      JOIN sys.tables AS t ON t.object_id = c.object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
      WHERE s.name = 'tenant' AND t.name LIKE 'Attendance%'
        AND ty.name = 'decimal' AND (c.precision <> 9 OR c.scale <> 2);
      """);

    Assert.Equal(0, wrongQuantity);
  }

  // ---- CALENDAR DAYS ARE `date`, NOT `datetimeoffset`.
  //
  // The one place this module departs from the `DateTimeOffset` convention, deliberately: storing a holiday
  // or an attendance date as an instant invites an offset conversion to move it across midnight into the
  // previous day, and every downstream day count would still look plausible.
  [Fact]
  public async Task Calendar_day_columns_are_stored_as_date()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();

    var wrong = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.columns AS c
      JOIN sys.tables AS t ON t.object_id = c.object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
      WHERE s.name = 'tenant' AND t.name LIKE 'Attendance%'
        AND c.name IN ('HolidayDate', 'AttendanceDate', 'StartDate', 'EndDate')
        AND ty.name <> 'date';
      """);

    Assert.Equal(0, wrong);
  }

  [Fact]
  [Trait("Decision", "DEC-ATT-0006")]
  public async Task No_attendance_foreign_key_crosses_to_a_platform_database()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();

    // `ADR-022` bars the cross-DATABASE case. Company and Branch live in the TENANT catalog, so those keys
    // are intra-catalog and legal; anything else would have to be a synonym or a linked server, and neither
    // can appear in `sys.foreign_keys` for this catalog. Asserting the keys resolve WITHIN this database is
    // therefore the checkable form of the rule.
    var unresolved = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.foreign_keys AS fk
      JOIN sys.tables AS t ON t.object_id = fk.parent_object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      LEFT JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
      WHERE s.name = 'tenant' AND t.name LIKE 'Attendance%' AND rt.object_id IS NULL;
      """);

    Assert.Equal(0, unresolved);
  }

  // ================================================================================================
  // THE UNIQUE INDEX THAT MUST NOT EXIST (OD-ATT-0012, AC-ATT-0014).
  // ================================================================================================
  //
  // **A second row for the same employee-date IS an adjustment.** The analysis package flagged this as the
  // sharpest coupling in the data model: a unique index chosen from the happy path would silently foreclose
  // the entire correction model, and the failure would appear as a mysterious constraint violation on a
  // legitimate business act.
  //
  // Asserted here because it is an ABSENCE, and absences do not fail on their own.
  [Fact]
  [Trait("Decision", "OD-ATT-0012")]
  public async Task Attendance_records_carry_no_unique_index_on_employee_and_date()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();

    var offending = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.indexes AS i
      JOIN sys.tables AS t ON t.object_id = i.object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      WHERE s.name = 'tenant' AND t.name = 'AttendanceRecords' AND i.is_unique = 1
        AND EXISTS (
          SELECT 1 FROM sys.index_columns AS ic
          JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
          WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = 'AttendanceDate');
      """);

    Assert.Equal(0, offending);
  }

  // ================================================================================================
  // THE APPEND-ONLY REFUSAL, PROVED — AND BOTH STATES, BECAUSE TESTING ONE PROVES HALF OF IT.
  // ================================================================================================
  //
  // `PreventAppendOnlyMutation` refuses `Modified` **or** `Deleted` UNCONDITIONALLY. This is the guarantee
  // the whole `OD-ATT-0012` ruling rests on, and the guarantee that makes REOPENING a period safe: a
  // reopened period permits appending and never editing, by anyone, whatever permission they hold.
  [Fact]
  [Trait("Decision", "DEC-ATT-0009")]
  public async Task An_attendance_record_cannot_be_modified()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();
    var recordId = await fixture.SeedRecordAsync();

    await using var context = fixture.CreateContext();
    var record = await context.Set<AttendanceRecord>().FirstAsync(row => row.Id == recordId);

    // Nothing on the type has a public setter except the branch stamp, so the mutation goes through the one
    // door the interface leaves open — which is exactly the door the write boundary watches.
    record.BranchId = Guid.NewGuid();

    await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
  }

  [Fact]
  [Trait("Decision", "DEC-ATT-0009")]
  public async Task An_attendance_record_cannot_be_deleted()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();
    var recordId = await fixture.SeedRecordAsync();

    await using var context = fixture.CreateContext();
    var record = await context.Set<AttendanceRecord>().FirstAsync(row => row.Id == recordId);

    context.Set<AttendanceRecord>().Remove(record);

    await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
  }

  // ---- AND AN ADJUSTMENT FOR THE SAME EMPLOYEE-DATE INSERTS CLEANLY.
  //
  // The positive half of the unique-index assertion above: not merely that the index is absent, but that the
  // business act it would have blocked actually works.
  [Fact]
  [Trait("Requirement", "REQ-ATT-0019")]
  public async Task A_second_row_for_the_same_employee_and_date_is_accepted_because_it_is_an_adjustment()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();
    var originalId = await fixture.SeedRecordAsync();

    await using var context = fixture.CreateContext();

    var adjustment = AttendanceRecord.Adjust(
      fixture.CompanyA, fixture.PeriodId, fixture.Employee, AttendanceFixture.RecordDate, originalId,
      workedDelta: -2m, overtimeDelta: 0m, overtimeTier: null,
      paidAbsenceDelta: 0m, unpaidAbsenceDelta: 2m, note: "Two hours were unpaid leave").Value;

    adjustment.BranchId = fixture.BranchId;
    context.Set<AttendanceRecord>().Add(adjustment);

    await context.SaveChangesAsync();

    var rows = await context.Set<AttendanceRecord>()
      .Where(row => row.EmployeeId == fixture.Employee && row.AttendanceDate == AttendanceFixture.RecordDate)
      .ToListAsync();

    Assert.Equal(2, rows.Count);

    // And the truth for the employee-date is their SUM — the arithmetic `IAttendanceSummary` performs.
    Assert.Equal(6m, rows.Sum(row => row.WorkedQuantity));
    Assert.Equal(2m, rows.Sum(row => row.UnpaidAbsenceQuantity));
  }
}
