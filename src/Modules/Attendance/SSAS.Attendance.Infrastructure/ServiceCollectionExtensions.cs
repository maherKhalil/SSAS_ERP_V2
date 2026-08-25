using Microsoft.Extensions.DependencyInjection;
using SSAS.Attendance.Application.Abstractions;
using SSAS.Attendance.Application.Approval;
using SSAS.Attendance.Application.Calendars;
using SSAS.Attendance.Application.Leave;
using SSAS.Attendance.Application.Periods;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Application.Records;
using SSAS.Attendance.Contracts.Summaries;
using SSAS.Attendance.Infrastructure.Persistence;
using SSAS.Attendance.Infrastructure.Summaries;
using SSAS.BuildingBlocks.Infrastructure.Persistence;

namespace SSAS.Attendance.Infrastructure;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddAttendanceInfrastructure(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    // ---- ATTENDANCE'S CONTRIBUTION TO THE SINGLE TENANT MODEL.
    //
    // Registered EXPLICITLY, never discovered. Without this line Attendance's seven entities are absent from
    // the tenant model, absent from the migration stream, and -- because TenantCutoverCopyPlan derives its
    // manifest from the model -- absent from Shared-to-Dedicated cutover, which fails SILENTLY.
    services.AddSingleton<ITenantModelContributor, AttendanceTenantModelContributor>();

    services.AddScoped<IWorkingCalendarRepository, WorkingCalendarRepository>();
    services.AddScoped<IAttendancePeriodRepository, AttendancePeriodRepository>();
    services.AddScoped<IAttendanceRecordRepository, AttendanceRecordRepository>();
    services.AddScoped<ILeaveTypeRepository, LeaveTypeRepository>();
    services.AddScoped<ILeaveBalanceRepository, LeaveBalanceRepository>();
    services.AddScoped<ILeaveRequestRepository, LeaveRequestRepository>();

    services.AddScoped<IAttendanceScopeResolver, AttendanceScopeResolver>();
    services.AddScoped<IAttendanceReadService, AttendanceReadService>();
    services.AddScoped<ILeaveApprovalRouter, LeaveApprovalRouter>();

    // ---- THE PUBLISHED CONTRACT PAYROLL CONSUMES (OD-ATT-0009, OD-ATT-0010).
    //
    // Registered here rather than in Payroll: the OWNER of the data implements the contract, and Payroll
    // holds only a reference to SSAS.Attendance.Contracts. Same shape as IEmployeeRoster and IJournalPoster.
    services.AddScoped<IAttendanceSummary, AttendanceSummaryService>();

    services.AddScoped<CreateWorkingCalendarCommandHandler>();
    services.AddScoped<UpdateWorkingCalendarCommandHandler>();
    services.AddScoped<AddHolidayCommandHandler>();
    services.AddScoped<RemoveHolidayCommandHandler>();

    services.AddScoped<CreateAttendancePeriodCommandHandler>();
    services.AddScoped<CloseAttendancePeriodCommandHandler>();
    services.AddScoped<ReopenAttendancePeriodCommandHandler>();

    services.AddScoped<RecordAttendanceCommandHandler>();
    services.AddScoped<AdjustAttendanceCommandHandler>();

    services.AddScoped<CreateLeaveTypeCommandHandler>();
    services.AddScoped<UpdateLeaveTypeCommandHandler>();
    services.AddScoped<SetLeaveTypeActivationCommandHandler>();
    services.AddScoped<SetLeaveEntitlementCommandHandler>();
    services.AddScoped<SubmitLeaveRequestCommandHandler>();
    services.AddScoped<ApproveLeaveRequestCommandHandler>();
    services.AddScoped<RejectLeaveRequestCommandHandler>();
    services.AddScoped<CancelLeaveRequestCommandHandler>();

    return services;
  }
}
