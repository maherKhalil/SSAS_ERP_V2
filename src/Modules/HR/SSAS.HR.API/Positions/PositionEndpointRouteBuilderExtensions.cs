using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.HR.API.Employees;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Application.Positions;
using SSAS.HR.Application.Positions.Reads;
using SSAS.HR.Domain.Positions;
using SSAS.BuildingBlocks.Api.Authorization;

namespace SSAS.HR.API.Positions;

// ==================================================================================================
// THE POSITION HTTP SURFACE (FP-008 Phase 4, api-contracts).
// ==================================================================================================
//
// ---- WHAT THIS LAYER DOES, AND THE SHORT LIST IT DOES NOT.
//
// It parses transport, dispatches, and maps the answer. It decides nothing: no company scope, no permission
// evaluation beyond declaring which one a route needs, no query composition, and no choice of which command
// to run. Phases 1 to 3 settled all of that, and re-deciding any of it here would be a second opinion that
// could disagree with the write boundary.
//
// The clearest evidence is what is absent: no DbContext, no repository, no `PositionScopeResolver`
// reference. The handlers resolve their own scope per request.
//
// ---- THREE GROUPS, ONE SHAPE, AND NO GENERIC ROUTE FACTORY.
//
// The three aggregates take the same six routes, and it would be easy to generate them from a shared
// helper parameterized over permission names and handler types. They are written out instead, for the same
// reason the application layer refused a generic `GradeCommandHandlers<T>`: the families differ in ways a
// type parameter erases — a salary grade's `View` is a separate permission because pay bands are sensitive,
// and a factory would make that difference an argument rather than a fact. Twenty routes written plainly
// are auditable; twenty generated ones are a call you have to expand in your head.
//
// ---- STATE CHANGES ARE NAMED POST ROUTES, NEVER VERBS ON A RESOURCE.
//
// There is no `MapDelete` anywhere: `BRULE-POS-0012` prohibits deletion, so the verb would name an
// operation that does not exist. Activate and deactivate are named POSTs, and BOTH carry the entity's
// **Deactivate** permission — `DEC-DEP-0025`, carried over. That permission governs whether the record can
// receive employees, and both directions change that answer: granting reactivation under ordinary `Update`
// would let a caller who may only retitle undo a closure someone with the sensitive permission deliberately
// made. The permission names the capability, not the direction.
public static class PositionEndpointRouteBuilderExtensions
{
  private const string PositionRoutePrefix = "/api/hr/positions";

  private const string JobGradeRoutePrefix = "/api/hr/job-grades";

  private const string SalaryGradeRoutePrefix = "/api/hr/salary-grades";

  private const string EmployeeRoutePrefix = "/api/hr/employees";

  // HR's own i18n key for this surface: the shared transport projection requires each surface to name its
  // own rather than inherit another module's.
  private const string ResourceKey = "hr.positions.errors.request_rejected";

  public static IEndpointRouteBuilder MapHrPositionEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    // The gate's dependency, asserted HERE so a host that mounts these routes without it fails at
    // startup rather than answering 500 per request (T-034).
    endpoints.RequireModuleEnablementServices(HrModuleEnablement.Key);

    var group = Group(endpoints, PositionRoutePrefix, "HR Positions");

    group.MapPost("", CreatePositionAsync)
      .RequirePermission(HrPermissionNames.CreatePositions)
      .WithName("HrPositionsCreate");

    group.MapGet("", SearchPositionsAsync)
      .RequirePermission(HrPermissionNames.ViewPositions)
      .WithName("HrPositionsSearch");

    group.MapGet("/{positionId}", GetPositionAsync)
      .RequirePermission(HrPermissionNames.ViewPositions)
      .WithName("HrPositionsGetById");

    group.MapPut("/{positionId}", UpdatePositionAsync)
      .RequirePermission(HrPermissionNames.UpdatePositions)
      .WithName("HrPositionsUpdate");

    group.MapPost("/{positionId}/activate", ActivatePositionAsync)
      .RequirePermission(HrPermissionNames.DeactivatePositions)
      .WithName("HrPositionsActivate");

    group.MapPost("/{positionId}/deactivate", DeactivatePositionAsync)
      .RequirePermission(HrPermissionNames.DeactivatePositions)
      .WithName("HrPositionsDeactivate");

    return endpoints;
  }

  public static IEndpointRouteBuilder MapHrJobGradeEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    // The gate's dependency, asserted HERE so a host that mounts these routes without it fails at
    // startup rather than answering 500 per request (T-034).
    endpoints.RequireModuleEnablementServices(HrModuleEnablement.Key);

    var group = Group(endpoints, JobGradeRoutePrefix, "HR Job Grades");

    group.MapPost("", CreateJobGradeAsync)
      .RequirePermission(HrPermissionNames.CreateJobGrades)
      .WithName("HrJobGradesCreate");

    group.MapGet("", SearchJobGradesAsync)
      .RequirePermission(HrPermissionNames.ViewJobGrades)
      .WithName("HrJobGradesSearch");

    group.MapGet("/{jobGradeId}", GetJobGradeAsync)
      .RequirePermission(HrPermissionNames.ViewJobGrades)
      .WithName("HrJobGradesGetById");

    group.MapPut("/{jobGradeId}", UpdateJobGradeAsync)
      .RequirePermission(HrPermissionNames.UpdateJobGrades)
      .WithName("HrJobGradesUpdate");

    group.MapPost("/{jobGradeId}/activate", ActivateJobGradeAsync)
      .RequirePermission(HrPermissionNames.DeactivateJobGrades)
      .WithName("HrJobGradesActivate");

    group.MapPost("/{jobGradeId}/deactivate", DeactivateJobGradeAsync)
      .RequirePermission(HrPermissionNames.DeactivateJobGrades)
      .WithName("HrJobGradesDeactivate");

    return endpoints;
  }

  // ---- THE SENSITIVE FAMILY. Every route here carries an `HR.SalaryGrades.*` permission, and `View` in
  // particular is the one `DEC-POS-0018` separated from `HR.Positions.View` because pay bands are more
  // sensitive than job titles. A caller holding every position and job grade permission reaches none of it.
  public static IEndpointRouteBuilder MapHrSalaryGradeEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    // The gate's dependency, asserted HERE so a host that mounts these routes without it fails at
    // startup rather than answering 500 per request (T-034).
    endpoints.RequireModuleEnablementServices(HrModuleEnablement.Key);

    var group = Group(endpoints, SalaryGradeRoutePrefix, "HR Salary Grades");

    group.MapPost("", CreateSalaryGradeAsync)
      .RequirePermission(HrPermissionNames.CreateSalaryGrades)
      .WithName("HrSalaryGradesCreate");

    group.MapGet("", SearchSalaryGradesAsync)
      .RequirePermission(HrPermissionNames.ViewSalaryGrades)
      .WithName("HrSalaryGradesSearch");

    group.MapGet("/{salaryGradeId}", GetSalaryGradeAsync)
      .RequirePermission(HrPermissionNames.ViewSalaryGrades)
      .WithName("HrSalaryGradesGetById");

    group.MapPut("/{salaryGradeId}", UpdateSalaryGradeAsync)
      .RequirePermission(HrPermissionNames.UpdateSalaryGrades)
      .WithName("HrSalaryGradesUpdate");

    group.MapPost("/{salaryGradeId}/activate", ActivateSalaryGradeAsync)
      .RequirePermission(HrPermissionNames.DeactivateSalaryGrades)
      .WithName("HrSalaryGradesActivate");

    group.MapPost("/{salaryGradeId}/deactivate", DeactivateSalaryGradeAsync)
      .RequirePermission(HrPermissionNames.DeactivateSalaryGrades)
      .WithName("HrSalaryGradesDeactivate");

    return endpoints;
  }

  // ---- THE TWO EMPLOYEE-GROUP ROUTES, MAPPED HERE BECAUSE THEY ARE POSITION OPERATIONS.
  //
  // Both live on the employee prefix because both are about an EMPLOYEE, and both carry employee
  // permissions: `DEC-POS-0019` ruled the change under `HR.Employees.Update`, and the history read is a
  // read of the employee's own record under `HR.Employees.View`. Neither is a position permission — giving
  // the change to `HR.Positions.Update` would let someone who may edit the job catalog reassign people.
  public static IEndpointRouteBuilder MapHrEmployeePositionEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    // The gate's dependency, asserted HERE so a host that mounts these routes without it fails at
    // startup rather than answering 500 per request (T-034).
    endpoints.RequireModuleEnablementServices(HrModuleEnablement.Key);

    var group = Group(endpoints, EmployeeRoutePrefix, "HR Employees");

    group.MapPost("/{employeeId}/change-position", ChangeEmployeePositionAsync)
      .RequirePermission(HrPermissionNames.UpdateEmployees)
      .WithName("HrEmployeesChangePosition");

    group.MapGet("/{employeeId}/position-history", GetEmployeePositionHistoryAsync)
      .RequirePermission(HrPermissionNames.ViewEmployees)
      .WithName("HrEmployeesPositionHistory");

    return endpoints;
  }

  // ================================================================================================
  // POSITIONS
  // ================================================================================================

  private static async Task<IResult> CreatePositionAsync(
    HttpContext context,
    CreatePositionCommandHandler handler,
    GetPositionQueryHandler reader,
    PositionCompositionServices composition,
    ICurrentCompany currentCompany,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreatePositionRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["code"] = [JsonValueKind.String],
        ["title"] = [JsonValueKind.String],
        ["jobGradeId"] = [JsonValueKind.String, JsonValueKind.Null]
      },
      cancellationToken,
      requiredFields: ["code", "title"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    // The company comes from the ESTABLISHED context, never from the body. The group filter already proved
    // it exists, is active and is reachable by this caller.
    if (currentCompany.CompanyId is not { } companyId)
    {
      return Problem(context, ApiErrors.Forbidden);
    }

    var created = await handler.HandleAsync(
      new CreatePositionCommand(companyId, request.Code!, request.Title!, request.JobGradeId),
      cancellationToken);

    if (created.IsFailure)
    {
      return Problem(context, PositionApiErrorMapper.MapPosition(created.Error));
    }

    // Read back through the SCOPED read path, so the response is built from what the caller is actually
    // permitted to see rather than from what the command happened to write.
    var read = await reader.HandleAsync(new GetPositionQuery(created.Value), cancellationToken);
    if (read.IsFailure)
    {
      return Problem(context, ApiErrors.WriteFailure);
    }

    var response = await ComposePositionAsync(read.Value, composition, cancellationToken);

    return Results.Created($"{PositionRoutePrefix}/{created.Value}", response);
  }

  private static async Task<IResult> UpdatePositionAsync(
    HttpContext context,
    Guid positionId,
    UpdatePositionCommandHandler handler,
    GetPositionQueryHandler reader,
    PositionCompositionServices composition,
    CancellationToken cancellationToken)
  {
    // `status` is absent from the declared set, so an update cannot express a lifecycle change.
    var request = await StrictRequestReader.ReadStrictJsonAsync<UpdatePositionRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["code"] = [JsonValueKind.String],
        ["title"] = [JsonValueKind.String],
        ["jobGradeId"] = [JsonValueKind.String, JsonValueKind.Null],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["code", "title", "expectedRowVersion"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    var result = await handler.HandleAsync(
      new UpdatePositionCommand(positionId, request.Code!, request.Title!, request.JobGradeId, rowVersion),
      cancellationToken);

    return await ReadPositionBackAsync(context, reader, composition, positionId, result, cancellationToken);
  }

  private static async Task<IResult> ActivatePositionAsync(
    HttpContext context,
    Guid positionId,
    ReactivatePositionCommandHandler handler,
    GetPositionQueryHandler reader,
    PositionCompositionServices composition,
    CancellationToken cancellationToken)
  {
    var parsed = await ReadRowVersionAsync(context, cancellationToken);
    if (parsed.Failure is { } failure)
    {
      return failure;
    }

    var result = await handler.HandleAsync(
      new ReactivatePositionCommand(positionId, parsed.RowVersion), cancellationToken);

    return await ReadPositionBackAsync(context, reader, composition, positionId, result, cancellationToken);
  }

  private static async Task<IResult> DeactivatePositionAsync(
    HttpContext context,
    Guid positionId,
    DeactivatePositionCommandHandler handler,
    GetPositionQueryHandler reader,
    PositionCompositionServices composition,
    CancellationToken cancellationToken)
  {
    var parsed = await ReadRowVersionAsync(context, cancellationToken);
    if (parsed.Failure is { } failure)
    {
      return failure;
    }

    var result = await handler.HandleAsync(
      new DeactivatePositionCommand(positionId, parsed.RowVersion), cancellationToken);

    return await ReadPositionBackAsync(context, reader, composition, positionId, result, cancellationToken);
  }

  private static async Task<IResult> GetPositionAsync(
    HttpContext context,
    Guid positionId,
    GetPositionQueryHandler handler,
    PositionCompositionServices composition,
    CancellationToken cancellationToken)
  {
    var result = await handler.HandleAsync(new GetPositionQuery(positionId), cancellationToken);

    // Unknown, another tenant's, another company's and out-of-scope positions are all
    // `position.not_found` — the handler already collapsed them, and this simply does not undo it.
    return result.IsFailure
      ? Problem(context, PositionApiErrorMapper.MapPosition(result.Error))
      : Results.Ok(await ComposePositionAsync(result.Value, composition, cancellationToken));
  }

  private static async Task<IResult> SearchPositionsAsync(
    HttpContext context,
    SearchPositionsQueryHandler handler,
    CancellationToken cancellationToken)
  {
    if (!TryPositionSearchQuery(context.Request.Query, out var query))
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var result = await handler.HandleAsync(query, cancellationToken);
    if (result.IsFailure)
    {
      return Problem(context, PositionApiErrorMapper.MapPosition(result.Error));
    }

    var page = result.Value;

    return Results.Ok(new PositionPageResponse(
      page.Items
        .Select(item => PositionSummaryResponse.From(item, RowVersionCodec.Encode(item.RowVersion)))
        .ToArray(),
      page.PageNumber,
      page.PageSize,
      page.TotalCount,
      page.TotalPages));
  }

  // ================================================================================================
  // JOB GRADES
  // ================================================================================================

  private static async Task<IResult> CreateJobGradeAsync(
    HttpContext context,
    CreateJobGradeCommandHandler handler,
    GetJobGradeQueryHandler reader,
    ICurrentCompany currentCompany,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreateJobGradeRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["code"] = [JsonValueKind.String],
        ["name"] = [JsonValueKind.String],
        ["rankOrder"] = [JsonValueKind.Number],
        ["salaryGradeId"] = [JsonValueKind.String, JsonValueKind.Null]
      },
      cancellationToken,
      requiredFields: ["code", "name", "rankOrder"]);

    if (request?.RankOrder is not { } rankOrder)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (currentCompany.CompanyId is not { } companyId)
    {
      return Problem(context, ApiErrors.Forbidden);
    }

    var created = await handler.HandleAsync(
      new CreateJobGradeCommand(
        companyId, request.Code!, request.Name!, rankOrder, request.SalaryGradeId),
      cancellationToken);

    if (created.IsFailure)
    {
      return Problem(context, PositionApiErrorMapper.MapJobGrade(created.Error));
    }

    var read = await reader.HandleAsync(new GetJobGradeQuery(created.Value), cancellationToken);

    return read.IsFailure
      ? Problem(context, ApiErrors.WriteFailure)
      : Results.Created(
        $"{JobGradeRoutePrefix}/{created.Value}",
        JobGradeResponse.From(read.Value, RowVersionCodec.Encode(read.Value.RowVersion)));
  }

  private static async Task<IResult> UpdateJobGradeAsync(
    HttpContext context,
    Guid jobGradeId,
    UpdateJobGradeCommandHandler handler,
    GetJobGradeQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<UpdateJobGradeRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["code"] = [JsonValueKind.String],
        ["name"] = [JsonValueKind.String],
        ["rankOrder"] = [JsonValueKind.Number],
        ["salaryGradeId"] = [JsonValueKind.String, JsonValueKind.Null],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["code", "name", "rankOrder", "expectedRowVersion"]);

    if (request?.RankOrder is not { } rankOrder)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    var result = await handler.HandleAsync(
      new UpdateJobGradeCommand(
        jobGradeId, request.Code!, request.Name!, rankOrder, request.SalaryGradeId, rowVersion),
      cancellationToken);

    return await ReadJobGradeBackAsync(context, reader, jobGradeId, result, cancellationToken);
  }

  private static async Task<IResult> ActivateJobGradeAsync(
    HttpContext context,
    Guid jobGradeId,
    ReactivateJobGradeCommandHandler handler,
    GetJobGradeQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var parsed = await ReadRowVersionAsync(context, cancellationToken);
    if (parsed.Failure is { } failure)
    {
      return failure;
    }

    var result = await handler.HandleAsync(
      new ReactivateJobGradeCommand(jobGradeId, parsed.RowVersion), cancellationToken);

    return await ReadJobGradeBackAsync(context, reader, jobGradeId, result, cancellationToken);
  }

  private static async Task<IResult> DeactivateJobGradeAsync(
    HttpContext context,
    Guid jobGradeId,
    DeactivateJobGradeCommandHandler handler,
    GetJobGradeQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var parsed = await ReadRowVersionAsync(context, cancellationToken);
    if (parsed.Failure is { } failure)
    {
      return failure;
    }

    var result = await handler.HandleAsync(
      new DeactivateJobGradeCommand(jobGradeId, parsed.RowVersion), cancellationToken);

    return await ReadJobGradeBackAsync(context, reader, jobGradeId, result, cancellationToken);
  }

  private static async Task<IResult> GetJobGradeAsync(
    HttpContext context,
    Guid jobGradeId,
    GetJobGradeQueryHandler handler,
    CancellationToken cancellationToken)
  {
    var result = await handler.HandleAsync(new GetJobGradeQuery(jobGradeId), cancellationToken);

    return result.IsFailure
      ? Problem(context, PositionApiErrorMapper.MapJobGrade(result.Error))
      : Results.Ok(JobGradeResponse.From(result.Value, RowVersionCodec.Encode(result.Value.RowVersion)));
  }

  private static async Task<IResult> SearchJobGradesAsync(
    HttpContext context,
    SearchJobGradesQueryHandler handler,
    CancellationToken cancellationToken)
  {
    if (!TryGradeSearchQuery<JobGradeStatus>(context.Request.Query, out var page, out var pageSize,
      out var searchText, out var status))
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var result = await handler.HandleAsync(
      new SearchJobGradesQuery(
        PositionCompanyScopeMode.CurrentCompany, searchText, status, page, pageSize),
      cancellationToken);

    if (result.IsFailure)
    {
      return Problem(context, PositionApiErrorMapper.MapJobGrade(result.Error));
    }

    var paged = result.Value;

    return Results.Ok(new JobGradePageResponse(
      paged.Items
        .Select(item => JobGradeSummaryResponse.From(item, RowVersionCodec.Encode(item.RowVersion)))
        .ToArray(),
      paged.PageNumber,
      paged.PageSize,
      paged.TotalCount,
      paged.TotalPages));
  }

  // ================================================================================================
  // SALARY GRADES
  // ================================================================================================

  private static async Task<IResult> CreateSalaryGradeAsync(
    HttpContext context,
    CreateSalaryGradeCommandHandler handler,
    GetSalaryGradeQueryHandler reader,
    PositionCompositionServices composition,
    ICurrentCompany currentCompany,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreateSalaryGradeRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["code"] = [JsonValueKind.String],
        ["name"] = [JsonValueKind.String],
        ["rankOrder"] = [JsonValueKind.Number],
        ["minimumAmount"] = [JsonValueKind.Number, JsonValueKind.Null],
        ["midpointAmount"] = [JsonValueKind.Number, JsonValueKind.Null],
        ["maximumAmount"] = [JsonValueKind.Number, JsonValueKind.Null]
      },
      cancellationToken,
      requiredFields: ["code", "name", "rankOrder"]);

    if (request?.RankOrder is not { } rankOrder)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (currentCompany.CompanyId is not { } companyId)
    {
      return Problem(context, ApiErrors.Forbidden);
    }

    var created = await handler.HandleAsync(
      new CreateSalaryGradeCommand(
        companyId, request.Code!, request.Name!, rankOrder,
        request.MinimumAmount, request.MidpointAmount, request.MaximumAmount),
      cancellationToken);

    if (created.IsFailure)
    {
      return Problem(context, PositionApiErrorMapper.MapSalaryGrade(created.Error));
    }

    var read = await reader.HandleAsync(new GetSalaryGradeQuery(created.Value), cancellationToken);
    if (read.IsFailure)
    {
      return Problem(context, ApiErrors.WriteFailure);
    }

    var currency = await composition.ResolveCurrencyAsync(read.Value.CompanyId, cancellationToken);

    return Results.Created(
      $"{SalaryGradeRoutePrefix}/{created.Value}",
      SalaryGradeResponse.From(read.Value, currency, RowVersionCodec.Encode(read.Value.RowVersion)));
  }

  private static async Task<IResult> UpdateSalaryGradeAsync(
    HttpContext context,
    Guid salaryGradeId,
    UpdateSalaryGradeCommandHandler handler,
    GetSalaryGradeQueryHandler reader,
    PositionCompositionServices composition,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<UpdateSalaryGradeRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["code"] = [JsonValueKind.String],
        ["name"] = [JsonValueKind.String],
        ["rankOrder"] = [JsonValueKind.Number],
        ["minimumAmount"] = [JsonValueKind.Number, JsonValueKind.Null],
        ["midpointAmount"] = [JsonValueKind.Number, JsonValueKind.Null],
        ["maximumAmount"] = [JsonValueKind.Number, JsonValueKind.Null],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["code", "name", "rankOrder", "expectedRowVersion"]);

    if (request?.RankOrder is not { } rankOrder)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    var result = await handler.HandleAsync(
      new UpdateSalaryGradeCommand(
        salaryGradeId, request.Code!, request.Name!, rankOrder,
        request.MinimumAmount, request.MidpointAmount, request.MaximumAmount, rowVersion),
      cancellationToken);

    return await ReadSalaryGradeBackAsync(
      context, reader, composition, salaryGradeId, result, cancellationToken);
  }

  private static async Task<IResult> ActivateSalaryGradeAsync(
    HttpContext context,
    Guid salaryGradeId,
    ReactivateSalaryGradeCommandHandler handler,
    GetSalaryGradeQueryHandler reader,
    PositionCompositionServices composition,
    CancellationToken cancellationToken)
  {
    var parsed = await ReadRowVersionAsync(context, cancellationToken);
    if (parsed.Failure is { } failure)
    {
      return failure;
    }

    var result = await handler.HandleAsync(
      new ReactivateSalaryGradeCommand(salaryGradeId, parsed.RowVersion), cancellationToken);

    return await ReadSalaryGradeBackAsync(
      context, reader, composition, salaryGradeId, result, cancellationToken);
  }

  private static async Task<IResult> DeactivateSalaryGradeAsync(
    HttpContext context,
    Guid salaryGradeId,
    DeactivateSalaryGradeCommandHandler handler,
    GetSalaryGradeQueryHandler reader,
    PositionCompositionServices composition,
    CancellationToken cancellationToken)
  {
    var parsed = await ReadRowVersionAsync(context, cancellationToken);
    if (parsed.Failure is { } failure)
    {
      return failure;
    }

    var result = await handler.HandleAsync(
      new DeactivateSalaryGradeCommand(salaryGradeId, parsed.RowVersion), cancellationToken);

    return await ReadSalaryGradeBackAsync(
      context, reader, composition, salaryGradeId, result, cancellationToken);
  }

  private static async Task<IResult> GetSalaryGradeAsync(
    HttpContext context,
    Guid salaryGradeId,
    GetSalaryGradeQueryHandler handler,
    PositionCompositionServices composition,
    CancellationToken cancellationToken)
  {
    var result = await handler.HandleAsync(new GetSalaryGradeQuery(salaryGradeId), cancellationToken);
    if (result.IsFailure)
    {
      return Problem(context, PositionApiErrorMapper.MapSalaryGrade(result.Error));
    }

    var currency = await composition.ResolveCurrencyAsync(result.Value.CompanyId, cancellationToken);

    return Results.Ok(
      SalaryGradeResponse.From(result.Value, currency, RowVersionCodec.Encode(result.Value.RowVersion)));
  }

  private static async Task<IResult> SearchSalaryGradesAsync(
    HttpContext context,
    SearchSalaryGradesQueryHandler handler,
    PositionCompositionServices composition,
    CancellationToken cancellationToken)
  {
    if (!TryGradeSearchQuery<SalaryGradeStatus>(context.Request.Query, out var page, out var pageSize,
      out var searchText, out var status))
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var result = await handler.HandleAsync(
      new SearchSalaryGradesQuery(
        PositionCompanyScopeMode.CurrentCompany, searchText, status, page, pageSize),
      cancellationToken);

    if (result.IsFailure)
    {
      return Problem(context, PositionApiErrorMapper.MapSalaryGrade(result.Error));
    }

    var paged = result.Value;

    // ---- ONE CURRENCY LOOKUP PER COMPANY IN THE PAGE, NOT ONE PER ROW.
    //
    // A page is company-scoped in the common case, so this is normally a single lookup; a multi-company
    // search resolves each distinct company once. Doing it per row would turn a paged read into N cross-
    // catalog round trips for a value that is immutable per company (`DEC-CMP-0009`).
    var currencies = new Dictionary<Guid, string?>();
    foreach (var companyId in paged.Items.Select(item => item.CompanyId).Distinct())
    {
      currencies[companyId] = await composition.ResolveCurrencyAsync(companyId, cancellationToken);
    }

    return Results.Ok(new SalaryGradePageResponse(
      paged.Items
        .Select(item => SalaryGradeSummaryResponse.From(
          item, currencies[item.CompanyId], RowVersionCodec.Encode(item.RowVersion)))
        .ToArray(),
      paged.PageNumber,
      paged.PageSize,
      paged.TotalCount,
      paged.TotalPages));
  }

  // ================================================================================================
  // EMPLOYEE GROUP
  // ================================================================================================

  private static async Task<IResult> ChangeEmployeePositionAsync(
    HttpContext context,
    Guid employeeId,
    SSAS.HR.Application.Employees.ChangeEmployeePositionCommandHandler handler,
    GetEmployeeQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<ChangeEmployeePositionRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["positionId"] = [JsonValueKind.String],
        ["reasonCode"] = [JsonValueKind.String, JsonValueKind.Null],
        ["reasonText"] = [JsonValueKind.String, JsonValueKind.Null],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["positionId", "expectedRowVersion"]);

    if (request?.PositionId is not { } positionId)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    var result = await handler.HandleAsync(
      new SSAS.HR.Application.Employees.ChangeEmployeePositionCommand(
        employeeId, positionId, rowVersion, request.ReasonCode, request.ReasonText),
      cancellationToken);

    // The EMPLOYEE mapper, because this route answers about an employee. Its `Employee.Position*` arms are
    // the ones that describe an unusable destination here — and they are distinct from the position
    // family's own, which describe the job catalog.
    if (result.IsFailure)
    {
      return Problem(context, Employees.EmployeeApiErrorMapper.Map(result.Error));
    }

    var read = await reader.HandleAsync(new GetEmployeeQuery(employeeId), cancellationToken);

    return read.IsFailure
      ? Problem(context, ApiErrors.WriteFailure)
      : Results.Ok(Employees.EmployeeResponse.From(
        read.Value, RowVersionCodec.Encode(read.Value.RowVersion)));
  }

  private static async Task<IResult> GetEmployeePositionHistoryAsync(
    HttpContext context,
    Guid employeeId,
    GetEmployeePositionHistoryQueryHandler handler,
    CancellationToken cancellationToken)
  {
    var result = await handler.HandleAsync(
      new GetEmployeePositionHistoryQuery(employeeId), cancellationToken);

    // An employee outside the caller's scope is `employee.not_found`, exactly as the employee read is —
    // the handler collapsed it, and mapping through the EMPLOYEE mapper keeps the two answers identical.
    return result.IsFailure
      ? Problem(context, Employees.EmployeeApiErrorMapper.Map(result.Error))
      : Results.Ok(result.Value.Select(EmployeePositionHistoryResponse.From).ToArray());
  }

  // ================================================================================================
  // SHARED
  // ================================================================================================

  private static RouteGroupBuilder Group(
    IEndpointRouteBuilder endpoints, string prefix, string tag) =>
    endpoints.MapGroup(prefix)
      .WithTags(tag)
      // ---- THE MODULE ENABLEMENT GATE, ON THE GROUP (FP-014, `OD-SUB-0003`).
      //
      // On the GROUP rather than each route, for the same reason the filters below are: a route
      // added later cannot forget it. Entitlement does not differ per operation, so it belongs one
      // level up from `RequirePermission`.
      .RequireModule(HrModuleEnablement.Key)
      // ONE FILTER, EVERY ROUTE. On the group rather than each endpoint so a route added later cannot
      // forget it. Every position-family operation is company-owned, so establishing is never optional.
      .AddEndpointFilter<CompanyContextEndpointFilter>()
      .AddEndpointFilter(async (context, next) =>
      {
        ApiResponseSecurity.Apply(context.HttpContext);
        return await next(context);
      });

  // EVERY WRITE READS BACK THROUGH THE SCOPED PATH. The response describes what the caller may see, not
  // what the command wrote — the two can differ, and the scoped answer is the honest one.
  private static async Task<IResult> ReadPositionBackAsync(
    HttpContext context,
    GetPositionQueryHandler reader,
    PositionCompositionServices composition,
    Guid positionId,
    Result result,
    CancellationToken cancellationToken)
  {
    if (result.IsFailure)
    {
      return Problem(context, PositionApiErrorMapper.MapPosition(result.Error));
    }

    var read = await reader.HandleAsync(new GetPositionQuery(positionId), cancellationToken);

    return read.IsFailure
      ? Problem(context, ApiErrors.WriteFailure)
      : Results.Ok(await ComposePositionAsync(read.Value, composition, cancellationToken));
  }

  private static async Task<IResult> ReadJobGradeBackAsync(
    HttpContext context,
    GetJobGradeQueryHandler reader,
    Guid jobGradeId,
    Result result,
    CancellationToken cancellationToken)
  {
    if (result.IsFailure)
    {
      return Problem(context, PositionApiErrorMapper.MapJobGrade(result.Error));
    }

    var read = await reader.HandleAsync(new GetJobGradeQuery(jobGradeId), cancellationToken);

    return read.IsFailure
      ? Problem(context, ApiErrors.WriteFailure)
      : Results.Ok(JobGradeResponse.From(read.Value, RowVersionCodec.Encode(read.Value.RowVersion)));
  }

  private static async Task<IResult> ReadSalaryGradeBackAsync(
    HttpContext context,
    GetSalaryGradeQueryHandler reader,
    PositionCompositionServices composition,
    Guid salaryGradeId,
    Result result,
    CancellationToken cancellationToken)
  {
    if (result.IsFailure)
    {
      return Problem(context, PositionApiErrorMapper.MapSalaryGrade(result.Error));
    }

    var read = await reader.HandleAsync(new GetSalaryGradeQuery(salaryGradeId), cancellationToken);
    if (read.IsFailure)
    {
      return Problem(context, ApiErrors.WriteFailure);
    }

    var currency = await composition.ResolveCurrencyAsync(read.Value.CompanyId, cancellationToken);

    return Results.Ok(
      SalaryGradeResponse.From(read.Value, currency, RowVersionCodec.Encode(read.Value.RowVersion)));
  }

  private static async Task<PositionResponse> ComposePositionAsync(
    PositionDetail detail,
    PositionCompositionServices composition,
    CancellationToken cancellationToken) =>
    PositionResponse.From(
      detail,
      await composition.CountEmployeesAsync(detail.PositionId, cancellationToken),
      RowVersionCodec.Encode(detail.RowVersion));

  private static async Task<(IResult? Failure, byte[] RowVersion)> ReadRowVersionAsync(
    HttpContext context, CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<PositionRowVersionRequest>(
      context,
      new Dictionary<string, JsonValueKind[]> { ["expectedRowVersion"] = [JsonValueKind.String] },
      cancellationToken,
      requiredFields: ["expectedRowVersion"]);

    if (request is null)
    {
      return (Problem(context, ApiErrors.RequestInvalid), []);
    }

    return RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion)
      ? (null, rowVersion)
      : (Problem(context, ApiErrors.RowVersionInvalid), []);
  }

  // ---- QUERY STRING PARSING IS STRICT TOO.
  //
  // An unparseable page size is a malformed request, not a reason to substitute a default — the same rule
  // the search handlers apply when they REFUSE an out-of-range page rather than clamping it.
  private static bool TryPositionSearchQuery(IQueryCollection queryString, out SearchPositionsQuery query)
  {
    query = new SearchPositionsQuery();

    if (!TryPaging(queryString, out var page, out var pageSize))
    {
      return false;
    }

    PositionStatus? status = null;
    if (queryString.TryGetValue("status", out var statusValues) &&
      !string.IsNullOrWhiteSpace(statusValues.ToString()))
    {
      if (!Enum.TryParse<PositionStatus>(statusValues.ToString(), ignoreCase: true, out var parsed))
      {
        return false;
      }

      status = parsed;
    }

    Guid? jobGradeId = null;
    if (queryString.TryGetValue("jobGradeId", out var gradeValues) &&
      !string.IsNullOrWhiteSpace(gradeValues.ToString()))
    {
      if (!Guid.TryParse(gradeValues.ToString(), out var parsedGrade))
      {
        return false;
      }

      jobGradeId = parsedGrade;
    }

    var searchText = queryString.TryGetValue("search", out var searchValues)
      ? searchValues.ToString()
      : null;

    // ---- THE COMPANY SCOPE MODE IS NOT A QUERY PARAMETER.
    //
    // It stays at `CurrentCompany`, the established context. Letting a caller widen their own scope from
    // the query string would be the one thing this whole surface exists to prevent — and the resolver would
    // refuse it anyway, so accepting it would only produce a confusing denial.
    query = new SearchPositionsQuery(
      PositionCompanyScopeMode.CurrentCompany,
      string.IsNullOrWhiteSpace(searchText) ? null : searchText,
      status,
      jobGradeId,
      page,
      pageSize);

    return true;
  }

  // The two grade ladders take the same three filters, so one parser serves both — parameterized over the
  // STATUS enum alone, which is the only thing that differs. This is not the generic-route-factory the
  // header refuses: it composes no route and decides no permission.
  private static bool TryGradeSearchQuery<TStatus>(
    IQueryCollection queryString,
    out int page,
    out int pageSize,
    out string? searchText,
    out TStatus? status)
    where TStatus : struct, Enum
  {
    status = null;
    searchText = null;

    if (!TryPaging(queryString, out page, out pageSize))
    {
      return false;
    }

    if (queryString.TryGetValue("status", out var statusValues) &&
      !string.IsNullOrWhiteSpace(statusValues.ToString()))
    {
      if (!Enum.TryParse<TStatus>(statusValues.ToString(), ignoreCase: true, out var parsed))
      {
        return false;
      }

      status = parsed;
    }

    var text = queryString.TryGetValue("search", out var searchValues)
      ? searchValues.ToString()
      : null;

    searchText = string.IsNullOrWhiteSpace(text) ? null : text;

    return true;
  }

  private static bool TryPaging(IQueryCollection queryString, out int page, out int pageSize)
  {
    page = PositionSearchCriteria.DefaultPageNumber;
    pageSize = PositionSearchCriteria.DefaultPageSize;

    if (queryString.TryGetValue("page", out var pageValues) &&
      !int.TryParse(pageValues.ToString(), out page))
    {
      return false;
    }

    return !queryString.TryGetValue("pageSize", out var sizeValues) ||
      int.TryParse(sizeValues.ToString(), out pageSize);
  }

  private static IResult Problem(HttpContext context, ApiError error) =>
    ApiProblems.Problem(context, error, ResourceKey);
}

// ==================================================================================================
// THE TWO DEFERRED WIRE FIELDS, COMPOSED IN ONE PLACE (DEC-POS-0034, DEC-POS-0035)
// ==================================================================================================
//
// Both fields appear on a POSITION-family representation and neither can be produced by the position read
// side, for two different reasons that happen to have the same shape:
//
//   * `employeeCount` needs an EMPLOYEE read scope, because employees are branch-scoped and positions are
//     not. Counting on the position side would need a second branch authorization model or would leak the
//     size of branches the caller cannot read.
//   * `currencyCode` lives on a Platform-owned Company, which `SSAS.HR.*` cannot reference under `ADR-012`.
//
// Gathering them here keeps both compositions out of the route methods and gives each one place to be read
// and audited. It is a scoped service rather than static helpers because both dependencies are per-request.
public sealed class PositionCompositionServices(
  IEmployeeScopeResolver employeeScopes,
  IEmployeeReadService employees,
  ITenantCompanyCurrencyLookup currencies,
  ICurrentTenant currentTenant)
{
  // ---- NULL MEANS "NOT COMPUTABLE FOR THIS CALLER", AND NOTHING ELSE (DEC-POS-0034).
  //
  // A caller holding `HR.Positions.View` but not `HR.Employees.View` cannot obtain an employee scope, so
  // the resolver refuses and there is no honest number to report. Zero would be a lie — the position may
  // have holders this caller simply cannot count — and omitting the field would make the JSON shape vary
  // per caller. The ruling chose null: the honest meaning at a stable shape.
  //
  // ALL AUTHORIZED BRANCHES, because the question is "how many holders may this caller see at all", not
  // "how many are in the branch they happen to be acting in" — the same reasoning
  // `GetDepartmentQueryHandler` uses when it resolves a manager.
  public async Task<int?> CountEmployeesAsync(Guid positionId, CancellationToken cancellationToken)
  {
    var scope = await employeeScopes.ResolveAsync(
      new EmployeeScopeRequest(
        EmployeeCompanyScopeMode.AllAuthorizedCompanies,
        EmployeeBranchScopeMode.AllAuthorizedBranches),
      cancellationToken);

    return scope.IsFailure
      ? null
      : await employees.CountEmployeesByPositionAsync(scope.Value, positionId, cancellationToken);
  }

  // ---- THE CURRENCY ECHO (DEC-POS-0015, DEC-POS-0035).
  //
  // Read through the module-facing seam, never from a Company the module can see. A null here means the
  // company genuinely is not in this tenant, which for a company the caller could already read is a
  // dangling reference rather than an authorization outcome — so it surfaces as a missing field rather than
  // being translated into the scoped-absence 404 the detail route uses. The two answers stay distinct,
  // exactly as the lookup's own contract requires.
  public async Task<string?> ResolveCurrencyAsync(Guid companyId, CancellationToken cancellationToken) =>
    currentTenant.TenantId is not { } tenantId
      ? null
      : await currencies.FindBaseCurrencyCodeAsync(tenantId, companyId, cancellationToken);
}
