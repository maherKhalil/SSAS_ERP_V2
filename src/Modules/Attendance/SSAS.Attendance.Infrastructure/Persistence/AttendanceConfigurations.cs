using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Leave;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;

namespace SSAS.Attendance.Infrastructure.Persistence;

// ATTENDANCE'S EF MAPPINGS.
//
// Every persisted application string is `nvarchar` (`ADR-018`) — the default for `string` in this stack, and
// an integration test verifies it **against the created database** rather than against the model, because
// the model is what we believe and the database is what shipped.
//
// Dates that mean a CALENDAR DAY are `DateOnly`, mapping to SQL `date`. See `CalendarHoliday` for why: an
// instant invites an offset conversion to move a holiday across midnight into the previous day, and the
// numbers would still look plausible afterwards.

public sealed class WorkingCalendarConfiguration : IEntityTypeConfiguration<WorkingCalendar>
{
  public void Configure(EntityTypeBuilder<WorkingCalendar> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("AttendanceWorkingCalendars", AttendancePersistenceConstants.TenantSchema);
    builder.HasKey(calendar => calendar.Id);

    builder.Property(calendar => calendar.TenantId).IsRequired();
    builder.Property(calendar => calendar.CompanyId).IsRequired();

    // Display value, casing preserved. Value-converted, so projectable but NOT usable in a predicate —
    // which is what the normalized shadow is for (`DEC-POS-0030`).
    builder.Property(calendar => calendar.Name)
      .HasConversion(name => name.Value, value => WorkingCalendarName.Create(value).Value)
      .HasMaxLength(WorkingCalendarName.MaximumLength)
      .IsRequired();

    builder.Property(calendar => calendar.NormalizedName)
      .HasField("normalizedName")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(WorkingCalendarName.MaximumLength)
      .UseCollation(AttendancePersistenceConstants.OrdinalCollation)
      .IsRequired();

    // ---- THE WEEKEND PATTERN, VALUE-CONVERTED TO A SHORT ORDINAL STRING.
    //
    // See `WeekendPattern` for why this is a column and not a child table: a set from a closed seven-value
    // domain, never joined on, never range-queried, and read only after the calendar is loaded.
    builder.Property(calendar => calendar.Weekend)
      .HasConversion(
        weekend => weekend.PersistedValue,
        value => WeekendPattern.FromPersisted(value).Value)
      .HasMaxLength(AttendancePersistenceConstants.WeekendPatternMaximumLength)
      .IsRequired();

    builder.Property(calendar => calendar.IsDefault).IsRequired();

    builder.Property(calendar => calendar.CreatedUtc).IsRequired();
    builder.Property(calendar => calendar.ModifiedUtc).IsRequired();
    builder.Property(calendar => calendar.CreatedBy).HasMaxLength(AttendancePersistenceConstants.ActorMaximumLength);
    builder.Property(calendar => calendar.ModifiedBy).HasMaxLength(AttendancePersistenceConstants.ActorMaximumLength);
    builder.Property(calendar => calendar.RowVersion).IsRowVersion();

    builder.HasIndex(calendar => new { calendar.TenantId, calendar.CompanyId, calendar.NormalizedName })
      .IsUnique();

    builder.Metadata
      .FindNavigation(nameof(WorkingCalendar.Holidays))!
      .SetPropertyAccessMode(PropertyAccessMode.Field);

    builder.HasMany(calendar => calendar.Holidays)
      .WithOne()
      .HasForeignKey(holiday => holiday.WorkingCalendarId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}

public sealed class CalendarHolidayConfiguration : IEntityTypeConfiguration<CalendarHoliday>
{
  public void Configure(EntityTypeBuilder<CalendarHoliday> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("AttendanceCalendarHolidays", AttendancePersistenceConstants.TenantSchema);
    builder.HasKey(holiday => holiday.Id);

    builder.Property(holiday => holiday.TenantId).IsRequired();
    builder.Property(holiday => holiday.WorkingCalendarId).IsRequired();

    // `date`, not `datetimeoffset`. A public holiday is a calendar day, not an instant on a timeline.
    builder.Property(holiday => holiday.HolidayDate).HasColumnType("date").IsRequired();

    builder.Property(holiday => holiday.Name)
      .HasMaxLength(CalendarHoliday.NameMaximumLength)
      .IsRequired();

    // `AC-ATT-0004`. The domain refuses a duplicate too; the index is what makes a race lose rather than
    // duplicate.
    builder.HasIndex(holiday => new { holiday.WorkingCalendarId, holiday.HolidayDate }).IsUnique();
  }
}

public sealed class AttendancePeriodConfiguration : IEntityTypeConfiguration<AttendancePeriod>
{
  public void Configure(EntityTypeBuilder<AttendancePeriod> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("AttendancePeriods", AttendancePersistenceConstants.TenantSchema);
    builder.HasKey(period => period.Id);

    builder.Property(period => period.TenantId).IsRequired();
    builder.Property(period => period.CompanyId).IsRequired();

    builder.Property(period => period.Name)
      .HasConversion(name => name.Value, value => AttendancePeriodName.Create(value).Value)
      .HasMaxLength(AttendancePeriodName.MaximumLength)
      .IsRequired();

    builder.Property(period => period.NormalizedName)
      .HasField("normalizedName")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(AttendancePeriodName.MaximumLength)
      .UseCollation(AttendancePersistenceConstants.OrdinalCollation)
      .IsRequired();

    builder.Property(period => period.StartDate).HasColumnType("date").IsRequired();
    builder.Property(period => period.EndDate).HasColumnType("date").IsRequired();

    // ---- STATUS AS A STRING, AND THIS IS A SCAR RATHER THAN A PREFERENCE.
    //
    // FP-012's integration fixture seeded a company with an INTEGER status and `SYSUTCDATETIME()`, both
    // copied verbatim from GL's fixture and both wrong. Status enums in this codebase persist as strings,
    // and a fixture that guesses the storage shape fails during SETUP — which reads as an environment
    // problem rather than as the fixture bug it is.
    //
    // Note this differs from `PayElementKind`, which persists as an int because it is a closed calculation
    // vocabulary. A LIFECYCLE status is read by humans in the database; a calculation enum is not.
    builder.Property(period => period.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .IsRequired();

    builder.Property(period => period.ClosedUtc);
    builder.Property(period => period.ClosedBy).HasMaxLength(AttendancePersistenceConstants.ActorMaximumLength);

    builder.Property(period => period.CreatedUtc).IsRequired();
    builder.Property(period => period.ModifiedUtc).IsRequired();
    builder.Property(period => period.CreatedBy).HasMaxLength(AttendancePersistenceConstants.ActorMaximumLength);
    builder.Property(period => period.ModifiedBy).HasMaxLength(AttendancePersistenceConstants.ActorMaximumLength);
    builder.Property(period => period.RowVersion).IsRowVersion();

    // Not unique — a period is identified by its range, and the overlap check in the handler is what keeps
    // the ranges disjoint. An index on the range supports both that check and `GetCoveringAsync`.
    builder.HasIndex(period => new { period.TenantId, period.CompanyId, period.StartDate, period.EndDate });
  }
}

public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
  public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("AttendanceRecords", AttendancePersistenceConstants.TenantSchema);
    builder.HasKey(record => record.Id);

    builder.Property(record => record.TenantId).IsRequired();
    builder.Property(record => record.CompanyId).IsRequired();

    // The branch half of `OD-ATT-0011`'s split. Stamped by the write boundary from the execution context;
    // no caller supplies it.
    builder.Property(record => record.BranchId).IsRequired();

    builder.Property(record => record.AttendancePeriodId).IsRequired();
    builder.Property(record => record.EmployeeId).IsRequired();
    builder.Property(record => record.AttendanceDate).HasColumnType("date").IsRequired();

    builder.Property(record => record.Kind)
      .HasConversion<string>()
      .HasMaxLength(32)
      .IsRequired();

    builder.Property(record => record.AdjustedRecordId);

    // ---- QUANTITIES, AND NOT ONE OF THEM IS THE MONEY TYPE (DEC-ATT-0004).
    foreach (var quantity in new[]
      {
        nameof(AttendanceRecord.WorkedQuantity),
        nameof(AttendanceRecord.OvertimeQuantity),
        nameof(AttendanceRecord.PaidAbsenceQuantity),
        nameof(AttendanceRecord.UnpaidAbsenceQuantity)
      })
    {
      builder.Property(quantity)
        .HasPrecision(AttendancePersistenceConstants.QuantityPrecision, AttendancePersistenceConstants.QuantityScale)
        .IsRequired();
    }

    builder.Property(record => record.OvertimeTier)
      .HasMaxLength(AttendanceRecord.OvertimeTierMaximumLength);

    builder.Property(record => record.Note).HasMaxLength(AttendanceRecord.NoteMaximumLength);

    builder.Property(record => record.CreatedUtc).IsRequired();
    builder.Property(record => record.ModifiedUtc).IsRequired();
    builder.Property(record => record.CreatedBy).HasMaxLength(AttendancePersistenceConstants.ActorMaximumLength);
    builder.Property(record => record.ModifiedBy).HasMaxLength(AttendancePersistenceConstants.ActorMaximumLength);

    // ================================================================================================
    // NO RowVersion, AND NO UNIQUE INDEX ON (TenantId, EmployeeId, AttendanceDate).
    // ================================================================================================
    //
    // **No `RowVersion`** because `AttendanceRecord` is `IAppendOnlyEntity`: there is no update path to
    // concurrency-check, and the column would imply one exists.
    //
    // **No unique index on employee-and-date** because a second row for the same employee-date **IS** an
    // adjustment (`OD-ATT-0012`). The analysis package flagged this as the sharpest coupling in the data
    // model, and it is worth restating at the site: an index chosen from the happy path would silently
    // forecloses the entire correction model, and the failure would appear as a mysterious constraint
    // violation on a legitimate business act.
    //
    // The index below is the one the summary contract reads, and it is deliberately NOT unique.
    builder.HasIndex(record => new { record.TenantId, record.AttendancePeriodId, record.EmployeeId });

    // Supports the record-list read, which is branch-scoped per `OD-ATT-0011`.
    builder.HasIndex(record => new { record.TenantId, record.BranchId, record.AttendanceDate });
  }
}

public sealed class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
  public void Configure(EntityTypeBuilder<LeaveType> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("AttendanceLeaveTypes", AttendancePersistenceConstants.TenantSchema);
    builder.HasKey(leaveType => leaveType.Id);

    builder.Property(leaveType => leaveType.TenantId).IsRequired();
    builder.Property(leaveType => leaveType.CompanyId).IsRequired();

    builder.Property(leaveType => leaveType.Code)
      .HasConversion(code => code.Value, value => LeaveTypeCode.Create(value).Value)
      .HasMaxLength(LeaveTypeCode.MaximumLength)
      .IsRequired();

    builder.Property(leaveType => leaveType.NormalizedCode)
      .HasField("normalizedCode")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(LeaveTypeCode.MaximumLength)
      .UseCollation(AttendancePersistenceConstants.OrdinalCollation)
      .IsRequired();

    builder.Property(leaveType => leaveType.Name)
      .HasConversion(name => name.Value, value => LeaveTypeName.Create(value).Value)
      .HasMaxLength(LeaveTypeName.MaximumLength)
      .IsRequired();

    // The search column. No index: a leading-wildcard LIKE cannot seek, so an index would be write cost
    // buying nothing — the same reasoning HR, GL and Payroll applied to theirs.
    builder.Property(leaveType => leaveType.NormalizedName)
      .HasField("normalizedName")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(LeaveTypeName.MaximumLength)
      .UseCollation(AttendancePersistenceConstants.OrdinalCollation)
      .IsRequired();

    // An int, unlike the lifecycle statuses above. `LeaveBehaviour` is a closed CALCULATION vocabulary that
    // only code reads — the `PayElementBehaviour` precedent — and a string column would invite a value
    // nobody implemented.
    builder.Property(leaveType => leaveType.Behaviour).HasConversion<int>().IsRequired();

    builder.Property(leaveType => leaveType.IsSensitive).IsRequired();
    builder.Property(leaveType => leaveType.IsActive).IsRequired();

    builder.Property(leaveType => leaveType.CreatedUtc).IsRequired();
    builder.Property(leaveType => leaveType.ModifiedUtc).IsRequired();
    builder.Property(leaveType => leaveType.CreatedBy).HasMaxLength(AttendancePersistenceConstants.ActorMaximumLength);
    builder.Property(leaveType => leaveType.ModifiedBy).HasMaxLength(AttendancePersistenceConstants.ActorMaximumLength);
    builder.Property(leaveType => leaveType.RowVersion).IsRowVersion();

    builder.HasIndex(leaveType => new { leaveType.TenantId, leaveType.CompanyId, leaveType.NormalizedCode })
      .IsUnique();
  }
}

public sealed class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
  public void Configure(EntityTypeBuilder<LeaveBalance> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("AttendanceLeaveBalances", AttendancePersistenceConstants.TenantSchema);
    builder.HasKey(balance => balance.Id);

    builder.Property(balance => balance.TenantId).IsRequired();
    builder.Property(balance => balance.CompanyId).IsRequired();
    builder.Property(balance => balance.EmployeeId).IsRequired();
    builder.Property(balance => balance.LeaveTypeId).IsRequired();
    builder.Property(balance => balance.PeriodYear).IsRequired();

    builder.Property(balance => balance.EntitlementQuantity)
      .HasPrecision(AttendancePersistenceConstants.QuantityPrecision, AttendancePersistenceConstants.QuantityScale)
      .IsRequired();

    builder.Property(balance => balance.ConsumedQuantity)
      .HasPrecision(AttendancePersistenceConstants.QuantityPrecision, AttendancePersistenceConstants.QuantityScale)
      .IsRequired();

    builder.Property(balance => balance.CreatedUtc).IsRequired();
    builder.Property(balance => balance.ModifiedUtc).IsRequired();
    builder.Property(balance => balance.CreatedBy).HasMaxLength(AttendancePersistenceConstants.ActorMaximumLength);
    builder.Property(balance => balance.ModifiedBy).HasMaxLength(AttendancePersistenceConstants.ActorMaximumLength);
    builder.Property(balance => balance.RowVersion).IsRowVersion();

    // One balance per employee, type and year. Unique HERE — unlike attendance records — because a second
    // balance row for the same three is a duplicate rather than a correction: an entitlement is amended in
    // place (it is not append-only), so there is nothing a second row could legitimately mean.
    builder.HasIndex(balance => new
      { balance.TenantId, balance.EmployeeId, balance.LeaveTypeId, balance.PeriodYear })
      .IsUnique();
  }
}

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
  public void Configure(EntityTypeBuilder<LeaveRequest> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("AttendanceLeaveRequests", AttendancePersistenceConstants.TenantSchema);
    builder.HasKey(request => request.Id);

    builder.Property(request => request.TenantId).IsRequired();
    builder.Property(request => request.CompanyId).IsRequired();
    builder.Property(request => request.EmployeeId).IsRequired();
    builder.Property(request => request.LeaveTypeId).IsRequired();

    builder.Property(request => request.StartDate).HasColumnType("date").IsRequired();
    builder.Property(request => request.EndDate).HasColumnType("date").IsRequired();

    // ---- THE FROZEN FIGURE (BR-ATT-0003, AC-ATT-0019).
    //
    // Computed from the calendar at submission and STORED, because the calendar is maintainable. A holiday
    // added next year must not change how many days a request taken last year consumed — and therefore a
    // balance already settled, and therefore what somebody was paid.
    builder.Property(request => request.WorkingDaysConsumed)
      .HasPrecision(AttendancePersistenceConstants.QuantityPrecision, AttendancePersistenceConstants.QuantityScale)
      .IsRequired();

    builder.Property(request => request.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .IsRequired();

    builder.Property(request => request.DecidedBy).HasMaxLength(LeaveRequest.ActorMaximumLength);
    builder.Property(request => request.DecidedUtc);
    builder.Property(request => request.DecisionNote).HasMaxLength(LeaveRequest.DecisionNoteMaximumLength);

    // Nullable, and the null is meaningful: a root-fallback decision has no approver EMPLOYEE because the
    // holder is authenticated as a user and no identity-to-employee mapping exists (`OD-ATT-0013`).
    builder.Property(request => request.ApproverEmployeeId);

    builder.Property(request => request.CreatedUtc).IsRequired();
    builder.Property(request => request.ModifiedUtc).IsRequired();
    builder.Property(request => request.CreatedBy).HasMaxLength(AttendancePersistenceConstants.ActorMaximumLength);
    builder.Property(request => request.ModifiedBy).HasMaxLength(AttendancePersistenceConstants.ActorMaximumLength);
    builder.Property(request => request.RowVersion).IsRowVersion();

    builder.HasIndex(request => new { request.TenantId, request.EmployeeId, request.StartDate, request.EndDate });
  }
}
