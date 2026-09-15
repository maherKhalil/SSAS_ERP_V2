using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.Attendance.Application.Calendars;
using SSAS.Attendance.Application.Leave;
using SSAS.Attendance.Application.Periods;
using SSAS.Attendance.Application.Permissions;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Application.Records;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.API;

// ================================================================================================
// ATTENDANCE'S HTTP SURFACE (api-contracts.md).
// ================================================================================================
//
// ---- STATE CHANGES ARE `POST` TO A SUB-RESOURCE, NEVER A STATUS FIELD ON A `PUT`.
//
// `/close`, `/reopen`, `/approve`, `/reject`, `/cancel`, `/activate`, `/deactivate` — each an event with its
// own permission and its own refusals. A `PUT {status: "closed"}` would let the act that freezes Payroll's
// inputs arrive through the same door as an ordinary edit.
//
// ---- RECORDS HAVE A `POST` AND NO `PUT`, AND NOTHING RESPONDS TO `DELETE`.
//
// `OD-ATT-0012` ruled adjustments-never-edits, and `AttendanceRecord` is `IAppendOnlyEntity` — so the absent
// verbs are the ruling made visible in the surface rather than a rule someone has to remember. A correction
// is a `POST` to `/records/{id}/adjustments`.
//
// `TS-ATT-0024` asserts `PUT` and `DELETE` on `/records` return 405, so a future route adding one has to
// delete the test.
//
// ---- HOLIDAY REMOVAL IS A `POST`, FOLLOWING HR'S `manager/remove`.
//
// Taking a date off a maintained list is a named administrative act, and the codebase already spells those
// that way.
public static class AttendanceEndpointRouteBuilderExtensions
{
  private const string RoutePrefix = "/api/attendance";

  private const string ResourceKey = "attendance.errors.request_rejected";

  public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    // The gate's dependency, asserted HERE so a host that mounts these routes without it fails at
    // startup rather than answering 500 per request (T-034).
    endpoints.RequireModuleEnablementServices(AttendanceModuleEnablement.Key);

    var group = endpoints.MapGroup(RoutePrefix)
      .WithTags("Attendance")
      // ---- THE MODULE ENABLEMENT GATE, ON THE GROUP (FP-014, `OD-SUB-0003`).
      //
      // On the GROUP rather than each route, for the same reason the filters below are: a route
      // added later cannot forget it. Entitlement does not differ per operation, so it belongs one
      // level up from `RequirePermission`.
      .RequireModule(AttendanceModuleEnablement.Key)
      .AddEndpointFilter<AttendanceCompanyContextEndpointFilter>()
      .AddEndpointFilter(async (context, next) =>
      {
        ApiResponseSecurity.Apply(context.HttpContext);
        return await next(context);
      });

    // ---- WORKING CALENDAR. Structural configuration, not personal data.
    group.MapPost("/calendars", CreateCalendarAsync)
      .RequirePermission(AttendancePermissionNames.ManageCalendars).WithName("AttendanceCalendarsCreate");
    group.MapGet("/calendars", GetCalendarsAsync)
      .RequirePermission(AttendancePermissionNames.ViewCalendars).WithName("AttendanceCalendarsList");
    group.MapPut("/calendars/{workingCalendarId}", UpdateCalendarAsync)
      .RequirePermission(AttendancePermissionNames.ManageCalendars).WithName("AttendanceCalendarsUpdate");
    group.MapPost("/calendars/{workingCalendarId}/holidays", AddHolidayAsync)
      .RequirePermission(AttendancePermissionNames.ManageCalendars).WithName("AttendanceHolidaysAdd");
    group.MapPost("/calendars/{workingCalendarId}/holidays/remove", RemoveHolidayAsync)
      .RequirePermission(AttendancePermissionNames.ManageCalendars).WithName("AttendanceHolidaysRemove");

    // The query `REQ-ATT-0003` requires, exposed because clients need the SAME answer the domain uses. A
    // client computing working days itself would drift the first time a holiday moved, and the drift would
    // surface as a leave request consuming a different number of days than the preview promised.
    group.MapGet("/calendars/working-days", GetWorkingDaysAsync)
      .RequirePermission(AttendancePermissionNames.ViewCalendars).WithName("AttendanceWorkingDays");

    // ---- PERIODS. Close and reopen are the sensitive acts and share their own grant.
    group.MapPost("/periods", CreatePeriodAsync)
      .RequirePermission(AttendancePermissionNames.ManagePeriods).WithName("AttendancePeriodsCreate");
    group.MapGet("/periods", GetPeriodsAsync)
      .RequirePermission(AttendancePermissionNames.ViewPeriods).WithName("AttendancePeriodsList");
    group.MapPost("/periods/{attendancePeriodId}/close", ClosePeriodAsync)
      .RequirePermission(AttendancePermissionNames.ClosePeriods).WithName("AttendancePeriodsClose");
    group.MapPost("/periods/{attendancePeriodId}/reopen", ReopenPeriodAsync)
      .RequirePermission(AttendancePermissionNames.ClosePeriods).WithName("AttendancePeriodsReopen");

    // ---- RECORDS. Branch-scoped reads (OD-ATT-0011); no PUT, no DELETE.
    group.MapPost("/records", RecordAttendanceAsync)
      .RequirePermission(AttendancePermissionNames.ManageRecords).WithName("AttendanceRecordsCreate");
    group.MapGet("/records", GetRecordsAsync)
      .RequirePermission(AttendancePermissionNames.ViewRecords).WithName("AttendanceRecordsList");
    group.MapPost("/records/{attendanceRecordId}/adjustments", AdjustAttendanceAsync)
      .RequirePermission(AttendancePermissionNames.ManageRecords).WithName("AttendanceRecordsAdjust");

    // ---- LEAVE TYPES.
    group.MapPost("/leave-types", CreateLeaveTypeAsync)
      .RequirePermission(AttendancePermissionNames.ManageLeaveTypes).WithName("AttendanceLeaveTypesCreate");
    group.MapGet("/leave-types", GetLeaveTypesAsync)
      .RequirePermission(AttendancePermissionNames.ViewLeaveTypes).WithName("AttendanceLeaveTypesList");
    group.MapPut("/leave-types/{leaveTypeId}", UpdateLeaveTypeAsync)
      .RequirePermission(AttendancePermissionNames.ManageLeaveTypes).WithName("AttendanceLeaveTypesUpdate");
    group.MapPost("/leave-types/{leaveTypeId}/activate", ActivateLeaveTypeAsync)
      .RequirePermission(AttendancePermissionNames.ManageLeaveTypes).WithName("AttendanceLeaveTypesActivate");
    group.MapPost("/leave-types/{leaveTypeId}/deactivate", DeactivateLeaveTypeAsync)
      .RequirePermission(AttendancePermissionNames.ManageLeaveTypes).WithName("AttendanceLeaveTypesDeactivate");

    // ---- LEAVE REQUESTS. Approve and reject carry the separate sensitive grant.
    group.MapPost("/leave-requests", SubmitLeaveRequestAsync)
      .RequirePermission(AttendancePermissionNames.ManageLeave).WithName("AttendanceLeaveRequestsSubmit");
    group.MapGet("/leave-requests", GetLeaveRequestsAsync)
      .RequirePermission(AttendancePermissionNames.ViewLeave).WithName("AttendanceLeaveRequestsList");
    group.MapPost("/leave-requests/{leaveRequestId}/approve", ApproveLeaveRequestAsync)
      .RequirePermission(AttendancePermissionNames.ApproveLeave).WithName("AttendanceLeaveRequestsApprove");
    group.MapPost("/leave-requests/{leaveRequestId}/reject", RejectLeaveRequestAsync)
      .RequirePermission(AttendancePermissionNames.ApproveLeave).WithName("AttendanceLeaveRequestsReject");
    group.MapPost("/leave-requests/{leaveRequestId}/cancel", CancelLeaveRequestAsync)
      .RequirePermission(AttendancePermissionNames.ManageLeave).WithName("AttendanceLeaveRequestsCancel");

    // ---- BALANCES. Entitlement is settable; consumed is not (AC-ATT-0040).
    group.MapPut("/leave-balances", SetLeaveEntitlementAsync)
      .RequirePermission(AttendancePermissionNames.ManageLeave).WithName("AttendanceLeaveBalancesSet");
    group.MapGet("/leave-balances", GetLeaveBalancesAsync)
      .RequirePermission(AttendancePermissionNames.ViewLeave).WithName("AttendanceLeaveBalancesList");

    // ================================================================================================
    // SELF-SERVICE (FP-015, `REQ-SS-0004`, T-089). TWO ROUTES, BECAUSE THE MODULE HAS TWO PLANES.
    // ================================================================================================
    //
    // **NO EMPLOYEE ANYWHERE IN EITHER CONTRACT** — not on the path, not in query, header or body. The
    // subject is resolved from the caller's own identity (`AC-SS-0007`), asserted against the CONTRACT.
    //
    // ---- TWO PERMISSIONS, NOT ONE, AND THE SPLIT IS THE ADMINISTRATIVE ONE.
    //
    // Records and leave are separately permissioned administratively (`Attendance.Records.View` versus
    // `Attendance.Leave.View`) because a timesheet and a leave history disclose different things. **A single
    // `Attendance.ViewOwn` would be a WIDENING wearing the costume of a simplification:** granting sight of
    // one's own attendance would silently grant sight of one's own leave, which the administrative plane
    // treats as a separate decision. `TS-SS-0013` asserts the two do not substitute for each other.
    //
    // Both sit in the same group as everything above, so `RequireModule` and the `BR-PLT-0008` gate come
    // free — `REQ-SS-0008` costs nothing to satisfy and cannot be forgotten.
    group.MapGet("/me/records", GetOwnRecordsAsync)
      .RequirePermission(AttendancePermissionNames.ViewOwnRecords).WithName("AttendanceOwnRecordsList");
    group.MapGet("/me/leave-requests", GetOwnLeaveRequestsAsync)
      .RequirePermission(AttendancePermissionNames.ViewOwnLeave).WithName("AttendanceOwnLeaveRequestsList");

    return endpoints;
  }

  private static IResult Problem(HttpContext context, ApiError error) =>
    ApiProblems.Problem(context, error, ResourceKey);

  private static IResult Problem(HttpContext context, Error error) =>
    ApiProblems.Problem(context, AttendanceApiErrorMapper.Map(error), ResourceKey);

  // ---- CALENDARS.

  private static async Task<IResult> CreateCalendarAsync(
    HttpContext context, CreateWorkingCalendarCommandHandler handler, CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreateWorkingCalendarRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["companyId"] = [JsonValueKind.String],
        ["name"] = [JsonValueKind.String],
        ["weekendDays"] = [JsonValueKind.Array, JsonValueKind.Null],
        ["isDefault"] = [JsonValueKind.True, JsonValueKind.False]
      },
      cancellationToken,
      requiredFields: ["companyId", "name"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var created = await handler.HandleAsync(
      new CreateWorkingCalendarCommand(
        request.CompanyId, request.Name, ToDays(request.WeekendDays), request.IsDefault),
      cancellationToken);

    return created.IsFailure
      ? Problem(context, created.Error)
      : Results.Created($"{RoutePrefix}/calendars/{created.Value}", new { workingCalendarId = created.Value });
  }

  private static async Task<IResult> UpdateCalendarAsync(
    HttpContext context, Guid workingCalendarId, UpdateWorkingCalendarCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<UpdateWorkingCalendarRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["name"] = [JsonValueKind.String],
        ["weekendDays"] = [JsonValueKind.Array, JsonValueKind.Null]
      },
      cancellationToken,
      requiredFields: ["name"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var updated = await handler.HandleAsync(
      new UpdateWorkingCalendarCommand(workingCalendarId, request.Name, ToDays(request.WeekendDays)),
      cancellationToken);

    return updated.IsFailure ? Problem(context, updated.Error) : Results.NoContent();
  }

  private static async Task<IResult> AddHolidayAsync(
    HttpContext context, Guid workingCalendarId, AddHolidayCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<AddHolidayRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["holidayDate"] = [JsonValueKind.String],
        ["name"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["holidayDate", "name"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var added = await handler.HandleAsync(
      new AddHolidayCommand(workingCalendarId, request.HolidayDate, request.Name), cancellationToken);

    return added.IsFailure ? Problem(context, added.Error) : Results.NoContent();
  }

  private static async Task<IResult> RemoveHolidayAsync(
    HttpContext context, Guid workingCalendarId, RemoveHolidayCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<RemoveHolidayRequest>(
      context,
      new Dictionary<string, JsonValueKind[]> { ["holidayDate"] = [JsonValueKind.String] },
      cancellationToken,
      requiredFields: ["holidayDate"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var removed = await handler.HandleAsync(
      new RemoveHolidayCommand(workingCalendarId, request.HolidayDate), cancellationToken);

    return removed.IsFailure ? Problem(context, removed.Error) : Results.NoContent();
  }

  private static async Task<IResult> GetCalendarsAsync(
    HttpContext context, Guid? companyId, IAttendanceReadService reads, CancellationToken cancellationToken)
  {
    var calendars = await reads.GetCalendarsAsync(companyId, cancellationToken);
    return calendars.IsFailure ? Problem(context, calendars.Error) : Results.Ok(calendars.Value);
  }

  private static async Task<IResult> GetWorkingDaysAsync(
    HttpContext context, Guid companyId, DateOnly fromDate, DateOnly toDate,
    IAttendanceReadService reads, CancellationToken cancellationToken)
  {
    var days = await reads.GetWorkingDaysAsync(companyId, fromDate, toDate, cancellationToken);
    return days.IsFailure ? Problem(context, days.Error) : Results.Ok(new { workingDays = days.Value });
  }

  // ---- PERIODS.

  private static async Task<IResult> CreatePeriodAsync(
    HttpContext context, CreateAttendancePeriodCommandHandler handler, CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreateAttendancePeriodRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["companyId"] = [JsonValueKind.String],
        ["name"] = [JsonValueKind.String],
        ["startDate"] = [JsonValueKind.String],
        ["endDate"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["companyId", "name", "startDate", "endDate"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var created = await handler.HandleAsync(
      new CreateAttendancePeriodCommand(request.CompanyId, request.Name, request.StartDate, request.EndDate),
      cancellationToken);

    return created.IsFailure
      ? Problem(context, created.Error)
      : Results.Created($"{RoutePrefix}/periods/{created.Value}", new { attendancePeriodId = created.Value });
  }

  private static async Task<IResult> GetPeriodsAsync(
    HttpContext context, Guid? companyId, IAttendanceReadService reads, CancellationToken cancellationToken)
  {
    var periods = await reads.GetPeriodsAsync(companyId, cancellationToken);
    return periods.IsFailure ? Problem(context, periods.Error) : Results.Ok(periods.Value);
  }

  // NO BODY. Everything close needs is on the period it names, and a body would let a caller change what is
  // being closed at the moment of closing.
  private static async Task<IResult> ClosePeriodAsync(
    HttpContext context, Guid attendancePeriodId, CloseAttendancePeriodCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var closed = await handler.HandleAsync(
      new CloseAttendancePeriodCommand(attendancePeriodId), cancellationToken);

    return closed.IsFailure ? Problem(context, closed.Error) : Results.NoContent();
  }

  private static async Task<IResult> ReopenPeriodAsync(
    HttpContext context, Guid attendancePeriodId, ReopenAttendancePeriodCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var reopened = await handler.HandleAsync(
      new ReopenAttendancePeriodCommand(attendancePeriodId), cancellationToken);

    return reopened.IsFailure ? Problem(context, reopened.Error) : Results.NoContent();
  }

  // ---- RECORDS.

  private static async Task<IResult> RecordAttendanceAsync(
    HttpContext context, RecordAttendanceCommandHandler handler, CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<RecordAttendanceRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["companyId"] = [JsonValueKind.String],
        ["employeeId"] = [JsonValueKind.String],
        ["attendanceDate"] = [JsonValueKind.String],
        ["workedQuantity"] = [JsonValueKind.Number],
        ["overtimeQuantity"] = [JsonValueKind.Number],
        ["overtimeTier"] = [JsonValueKind.String, JsonValueKind.Null],
        ["paidAbsenceQuantity"] = [JsonValueKind.Number],
        ["unpaidAbsenceQuantity"] = [JsonValueKind.Number],
        ["note"] = [JsonValueKind.String, JsonValueKind.Null]
      },
      cancellationToken,
      requiredFields: ["companyId", "employeeId", "attendanceDate", "workedQuantity"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var created = await handler.HandleAsync(
      new RecordAttendanceCommand(
        request.CompanyId, request.EmployeeId, request.AttendanceDate,
        request.WorkedQuantity, request.OvertimeQuantity, request.OvertimeTier,
        request.PaidAbsenceQuantity, request.UnpaidAbsenceQuantity, request.Note),
      cancellationToken);

    return created.IsFailure
      ? Problem(context, created.Error)
      : Results.Created($"{RoutePrefix}/records/{created.Value}", new { attendanceRecordId = created.Value });
  }

  private static async Task<IResult> AdjustAttendanceAsync(
    HttpContext context, Guid attendanceRecordId, AdjustAttendanceCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<AdjustAttendanceRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["workedDelta"] = [JsonValueKind.Number],
        ["overtimeDelta"] = [JsonValueKind.Number],
        ["overtimeTier"] = [JsonValueKind.String, JsonValueKind.Null],
        ["paidAbsenceDelta"] = [JsonValueKind.Number],
        ["unpaidAbsenceDelta"] = [JsonValueKind.Number],
        ["note"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["note"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var created = await handler.HandleAsync(
      new AdjustAttendanceCommand(
        attendanceRecordId, request.WorkedDelta, request.OvertimeDelta, request.OvertimeTier,
        request.PaidAbsenceDelta, request.UnpaidAbsenceDelta, request.Note),
      cancellationToken);

    return created.IsFailure
      ? Problem(context, created.Error)
      : Results.Created($"{RoutePrefix}/records/{created.Value}", new { attendanceRecordId = created.Value });
  }

  private static async Task<IResult> GetRecordsAsync(
    HttpContext context, Guid? companyId, Guid? employeeId, DateOnly? fromDate, DateOnly? toDate,
    IAttendanceReadService reads, CancellationToken cancellationToken)
  {
    var records = await reads.GetRecordsAsync(companyId, employeeId, fromDate, toDate, cancellationToken);
    return records.IsFailure ? Problem(context, records.Error) : Results.Ok(records.Value);
  }

  // ---- SELF-SERVICE (FP-015, T-089).
  //
  // `fromDate` and `toDate` ARE bound from the request and that is correct: they narrow a set the caller is
  // already authorized to see, exactly as they do on the administrative route. **What must never be bound is
  // the SUBJECT**, and `TS-SS-0003` draws the line there rather than at "no bound parameters at all".
  //
  // The scope comes from `ResolveForOwnRecordsAsync` — company AND branch, derived from the caller's own
  // employee placement rather than from any administrative grant. See the resolver for why.
  private static async Task<IResult> GetOwnRecordsAsync(
    HttpContext context, DateOnly? fromDate, DateOnly? toDate,
    IAttendanceSelfServiceScopeResolver resolver, IAttendanceReadService reads,
    CancellationToken cancellationToken)
  {
    var own = await resolver.ResolveForOwnRecordsAsync(
      AttendancePermissionNames.ViewOwnRecords, cancellationToken);

    // An unlinked caller lands here as `Attendance.NoLinkedEmployee` and the mapper answers
    // `404 attendance.no_linked_employee` — an ordinary refusal naming the condition, nothing thrown and
    // nothing logged.
    if (own.IsFailure)
    {
      return Problem(context, own.Error);
    }

    var records = await reads.GetRecordsForEmployeeAsync(
      own.Value.Scope, own.Value.EmployeeId, fromDate, toDate, cancellationToken);

    return records.IsFailure ? Problem(context, records.Error) : Results.Ok(records.Value);
  }

  // Company-only scope, matching the administrative leave reads: leave is not branch-owned, so a branch
  // predicate here would filter on a column the type does not carry.
  private static async Task<IResult> GetOwnLeaveRequestsAsync(
    HttpContext context, IAttendanceSelfServiceScopeResolver resolver, IAttendanceReadService reads,
    CancellationToken cancellationToken)
  {
    var own = await resolver.ResolveForOwnLeaveAsync(
      AttendancePermissionNames.ViewOwnLeave, cancellationToken);
    if (own.IsFailure)
    {
      return Problem(context, own.Error);
    }

    var requests = await reads.GetLeaveRequestsForEmployeeAsync(
      own.Value.Scope, own.Value.EmployeeId, cancellationToken);

    return requests.IsFailure ? Problem(context, requests.Error) : Results.Ok(requests.Value);
  }

  // ---- LEAVE TYPES.

  private static async Task<IResult> CreateLeaveTypeAsync(
    HttpContext context, CreateLeaveTypeCommandHandler handler, CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreateLeaveTypeRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["companyId"] = [JsonValueKind.String],
        ["code"] = [JsonValueKind.String],
        ["name"] = [JsonValueKind.String],
        ["behaviour"] = [JsonValueKind.String],
        ["isSensitive"] = [JsonValueKind.True, JsonValueKind.False]
      },
      cancellationToken,
      requiredFields: ["companyId", "code", "name", "behaviour"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var created = await handler.HandleAsync(
      new CreateLeaveTypeCommand(request.CompanyId, request.Code, request.Name, request.Behaviour, request.IsSensitive),
      cancellationToken);

    return created.IsFailure
      ? Problem(context, created.Error)
      : Results.Created($"{RoutePrefix}/leave-types/{created.Value}", new { leaveTypeId = created.Value });
  }

  private static async Task<IResult> UpdateLeaveTypeAsync(
    HttpContext context, Guid leaveTypeId, UpdateLeaveTypeCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<UpdateLeaveTypeRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["name"] = [JsonValueKind.String],
        ["isSensitive"] = [JsonValueKind.True, JsonValueKind.False]
      },
      cancellationToken,
      requiredFields: ["name"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var updated = await handler.HandleAsync(
      new UpdateLeaveTypeCommand(leaveTypeId, request.Name, request.IsSensitive), cancellationToken);

    return updated.IsFailure ? Problem(context, updated.Error) : Results.NoContent();
  }

  private static Task<IResult> ActivateLeaveTypeAsync(
    HttpContext context, Guid leaveTypeId, SetLeaveTypeActivationCommandHandler handler,
    CancellationToken cancellationToken) =>
    SetLeaveTypeActivationAsync(context, leaveTypeId, isActive: true, handler, cancellationToken);

  private static Task<IResult> DeactivateLeaveTypeAsync(
    HttpContext context, Guid leaveTypeId, SetLeaveTypeActivationCommandHandler handler,
    CancellationToken cancellationToken) =>
    SetLeaveTypeActivationAsync(context, leaveTypeId, isActive: false, handler, cancellationToken);

  private static async Task<IResult> SetLeaveTypeActivationAsync(
    HttpContext context, Guid leaveTypeId, bool isActive, SetLeaveTypeActivationCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var changed = await handler.HandleAsync(
      new SetLeaveTypeActivationCommand(leaveTypeId, isActive), cancellationToken);

    return changed.IsFailure ? Problem(context, changed.Error) : Results.NoContent();
  }

  private static async Task<IResult> GetLeaveTypesAsync(
    HttpContext context, Guid? companyId, IAttendanceReadService reads, CancellationToken cancellationToken)
  {
    var types = await reads.GetLeaveTypesAsync(companyId, cancellationToken);
    return types.IsFailure ? Problem(context, types.Error) : Results.Ok(types.Value);
  }

  // ---- LEAVE REQUESTS.

  private static async Task<IResult> SubmitLeaveRequestAsync(
    HttpContext context, SubmitLeaveRequestCommandHandler handler, CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<SubmitLeaveRequestRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["companyId"] = [JsonValueKind.String],
        ["employeeId"] = [JsonValueKind.String],
        ["leaveTypeId"] = [JsonValueKind.String],
        ["startDate"] = [JsonValueKind.String],
        ["endDate"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["companyId", "employeeId", "leaveTypeId", "startDate", "endDate"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var created = await handler.HandleAsync(
      new SubmitLeaveRequestCommand(
        request.CompanyId, request.EmployeeId, request.LeaveTypeId, request.StartDate, request.EndDate),
      cancellationToken);

    return created.IsFailure
      ? Problem(context, created.Error)
      : Results.Created($"{RoutePrefix}/leave-requests/{created.Value}", new { leaveRequestId = created.Value });
  }

  private static async Task<IResult> ApproveLeaveRequestAsync(
    HttpContext context, Guid leaveRequestId, ApproveLeaveRequestCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var note = await ReadDecisionNoteAsync(context, cancellationToken);
    if (note.IsFailure)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var approved = await handler.HandleAsync(
      new DecideLeaveRequestCommand(leaveRequestId, note.Value), cancellationToken);

    return approved.IsFailure ? Problem(context, approved.Error) : Results.NoContent();
  }

  private static async Task<IResult> RejectLeaveRequestAsync(
    HttpContext context, Guid leaveRequestId, RejectLeaveRequestCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var note = await ReadDecisionNoteAsync(context, cancellationToken);
    if (note.IsFailure)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var rejected = await handler.HandleAsync(
      new DecideLeaveRequestCommand(leaveRequestId, note.Value), cancellationToken);

    return rejected.IsFailure ? Problem(context, rejected.Error) : Results.NoContent();
  }

  // A decision note and nothing else. The body cannot carry dates, a type or an employee: an approver must
  // not be able to alter the request at the moment of approving it.
  private static async Task<Result<string?>> ReadDecisionNoteAsync(
    HttpContext context, CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<DecideLeaveRequestRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["decisionNote"] = [JsonValueKind.String, JsonValueKind.Null]
      },
      cancellationToken);

    return request is null
      ? Result.Failure<string?>(new Error("Attendance.RequestInvalid", "The request body could not be read."))
      : Result.Success(request.DecisionNote);
  }

  private static async Task<IResult> CancelLeaveRequestAsync(
    HttpContext context, Guid leaveRequestId, CancelLeaveRequestCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var cancelled = await handler.HandleAsync(
      new CancelLeaveRequestCommand(leaveRequestId), cancellationToken);

    return cancelled.IsFailure ? Problem(context, cancelled.Error) : Results.NoContent();
  }

  private static async Task<IResult> GetLeaveRequestsAsync(
    HttpContext context, Guid? companyId, Guid? employeeId, IAttendanceReadService reads,
    CancellationToken cancellationToken)
  {
    var requests = await reads.GetLeaveRequestsAsync(companyId, employeeId, cancellationToken);
    return requests.IsFailure ? Problem(context, requests.Error) : Results.Ok(requests.Value);
  }

  // ---- BALANCES.

  private static async Task<IResult> SetLeaveEntitlementAsync(
    HttpContext context, SetLeaveEntitlementCommandHandler handler, CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<SetLeaveEntitlementRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["companyId"] = [JsonValueKind.String],
        ["employeeId"] = [JsonValueKind.String],
        ["leaveTypeId"] = [JsonValueKind.String],
        ["periodYear"] = [JsonValueKind.Number],
        ["entitlementQuantity"] = [JsonValueKind.Number]
      },
      cancellationToken,
      requiredFields: ["companyId", "employeeId", "leaveTypeId", "periodYear", "entitlementQuantity"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var set = await handler.HandleAsync(
      new SetLeaveEntitlementCommand(
        request.CompanyId, request.EmployeeId, request.LeaveTypeId,
        request.PeriodYear, request.EntitlementQuantity),
      cancellationToken);

    return set.IsFailure ? Problem(context, set.Error) : Results.Ok(new { leaveBalanceId = set.Value });
  }

  private static async Task<IResult> GetLeaveBalancesAsync(
    HttpContext context, Guid? companyId, Guid? employeeId, int? periodYear,
    IAttendanceReadService reads, CancellationToken cancellationToken)
  {
    var balances = await reads.GetLeaveBalancesAsync(companyId, employeeId, periodYear, cancellationToken);
    return balances.IsFailure ? Problem(context, balances.Error) : Results.Ok(balances.Value);
  }

  // Day ordinals to `DayOfWeek`. Out-of-range values are DROPPED rather than refused here: the domain's
  // `WeekendPattern.Create` is the authority on what a valid pattern is, and a transport-layer validation
  // would be a second opinion that could disagree with it.
  private static DayOfWeek[]? ToDays(IReadOnlyList<int>? ordinals) =>
    ordinals?.Where(ordinal => ordinal is >= 0 and <= 6).Select(ordinal => (DayOfWeek)ordinal).ToArray();
}

public sealed class AttendanceCompanyContextEndpointFilter(ICompanyContextEstablisher establisher) : IEndpointFilter
{
  private const string ResourceKey = "attendance.errors.request_rejected";

  public async ValueTask<object?> InvokeAsync(
    EndpointFilterInvocationContext context, EndpointFilterDelegate next)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(next);

    var established = await establisher.EstablishAsync(context.HttpContext.RequestAborted);

    return established.IsFailure
      ? ApiProblems.Problem(context.HttpContext, AttendanceApiErrorMapper.Map(established.Error), ResourceKey)
      : await next(context);
  }
}
