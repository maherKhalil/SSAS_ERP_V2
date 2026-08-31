using Microsoft.Data.SqlClient;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using Microsoft.EntityFrameworkCore;
using SSAS.Attendance.Application.Permissions;
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

  // ================================================================================================
  // THE COMPANY PREDICATE AND THE SENSITIVITY REDACTION, AGAINST THE REAL READ SERVICE (item 233).
  // ================================================================================================
  //
  // ---- WHY THIS DID NOT EXIST, AND WHY THE REASON DIFFERS FROM PAYROLL'S AND GL'S.
  //
  // `AttendanceReadService` was constructed by no test in any suite. Unlike Payroll and GL, the cause was
  // NOT a host that skips `AddAttendanceInfrastructure` -- this host calls it. The host then registers
  // `AddSingleton<IAttendanceReadService>(Reads)`, an explicit stub, and last registration wins.
  //
  // One symptom, two causes. A single remedy aimed at composition would have left this module untouched
  // while looking complete.
  //
  // ---- ⚠ THE REDACTION IS BELOW THE SEAM EVERY FAST SUITE RUNS AT, AND THAT IS NOT AN ACCIDENT.
  //
  // `maySeeSensitive` is resolved ONCE before the projection so redaction cannot depend on evaluation
  // order, and the redaction happens IN THE SQL PROJECTION so the value never crosses the wire or reaches
  // a query log. Both decisions are right, and both are exactly what put the behaviour where only a real
  // database can observe it. The verification cost moved with the care.
  //
  // ---- FOUR CLAUSES, NAMED, PLUS THE COMPANY PREDICATE.
  [Fact]
  public async Task A_leave_read_is_company_scoped_and_redacts_only_the_sensitive_type()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();

    var employee = Guid.NewGuid();
    var a = await fixture.SeedLeaveAsync(fixture.CompanyA, "AAA", employee);
    await fixture.SeedLeaveAsync(fixture.CompanyB, "BBB", employee, monthOffset: 6);

    await using var context = fixture.CreateContext();

    // ---- THE COMPANY PREDICATE. Company B's id is passed with a scope authorized for A, which isolates
    // the SCOPE predicate from the PARAMETER.
    var privileged = AttendanceFixture.Reads(context, fixture.Resolver(true, fixture.CompanyA));

    var own = await privileged.GetLeaveRequestsAsync(fixture.CompanyA, employee);
    Assert.True(own.IsSuccess, own.IsFailure ? own.Error.Code : null);
    Assert.Equal(2, own.Value.Count);

    var others = await privileged.GetLeaveRequestsAsync(fixture.CompanyB, employee);
    Assert.True(others.IsSuccess, others.IsFailure ? others.Error.Code : null);
    Assert.Empty(others.Value);

    // ---- CLAUSE 1: a caller WITH `ViewSensitive` sees WHICH TYPE.
    var seen = own.Value.Single(view => view.LeaveTypeId == a.SensitiveTypeId);
    Assert.Equal("AAA-SICK", seen.LeaveTypeCode);
    Assert.False(seen.IsTypeRedacted);

    // ---- CLAUSE 2: a caller WITHOUT it still gets THE ROW, redacted.
    //
    // This is the half that matters. A service that dropped the row entirely would satisfy every
    // "cannot see the type" assertion while destroying the fact that the person is away at all.
    var ordinaryCaller = AttendanceFixture.Reads(context, fixture.Resolver(false, fixture.CompanyA));

    var restricted = await ordinaryCaller.GetLeaveRequestsAsync(fixture.CompanyA, employee);
    Assert.True(restricted.IsSuccess, restricted.IsFailure ? restricted.Error.Code : null);
    Assert.Equal(2, restricted.Value.Count);

    var redacted = restricted.Value.Single(view => view.LeaveTypeId == a.SensitiveTypeId);
    Assert.Null(redacted.LeaveTypeCode);
    Assert.Null(redacted.LeaveTypeName);
    Assert.True(redacted.IsTypeRedacted);

    // ---- CLAUSE 3: the ORDINARY type stays visible IN THE SAME RESPONSE.
    //
    // Without this, a service that redacted EVERY row for an unprivileged caller passes clauses 1 and 2
    // both. Sensitivity is a property of the TYPE, not of the REQUEST, and only both kinds present at
    // once can tell a discriminating rule from a blanket one.
    var stillVisible = restricted.Value.Single(view => view.LeaveTypeId == a.OrdinaryTypeId);
    Assert.Equal("AAA-ANN", stillVisible.LeaveTypeCode);
    Assert.False(stillVisible.IsTypeRedacted);

    // ---- CLAUSE 4: THE SELF-SERVICE EXEMPTION, WHICH IS A RULING AND NOT AN OVERSIGHT.
    //
    // `GetLeaveRequestsForEmployeeAsync` passes `maySeeSensitive: true` deliberately: the party the
    // redaction protects is the SUBJECT, and on this route the subject IS the caller. `ViewSensitive` is
    // an administrative grant no plain employee holds, so applying the rule here would hide a person's
    // own sick leave from themselves as a nameless gap in their own list.
    //
    // It is the clause a well-meaning change breaks, because "redact unless the caller is an
    // administrator" sounds like the safer rule. The caller below holds NO sensitive permission.
    var scope = await fixture.Resolver(false, fixture.CompanyA)
      .ResolveCompanyOnlyAsync(AttendancePermissionNames.ViewLeave);
    Assert.True(scope.IsSuccess, scope.IsFailure ? scope.Error.Code : null);

    var mine = await ordinaryCaller.GetLeaveRequestsForEmployeeAsync(scope.Value, employee);
    Assert.True(mine.IsSuccess, mine.IsFailure ? mine.Error.Code : null);

    var ownSensitive = mine.Value.Single(view => view.LeaveTypeId == a.SensitiveTypeId);
    Assert.Equal("AAA-SICK", ownSensitive.LeaveTypeCode);
    Assert.False(ownSensitive.IsTypeRedacted);
  }

}
