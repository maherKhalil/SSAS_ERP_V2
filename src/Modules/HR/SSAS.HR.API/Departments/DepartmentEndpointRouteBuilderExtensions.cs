using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.API.Employees;
using SSAS.HR.Application.Departments;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Departments;

namespace SSAS.HR.API.Departments;

// ==================================================================================================
// THE DEPARTMENT HTTP SURFACE (FP-007 Phase 4, api-contracts).
// ==================================================================================================
//
// ---- WHAT THIS LAYER DOES, AND THE SHORT LIST IT DOES NOT.
//
// It parses transport, dispatches, and maps the answer. It decides nothing: no company scope, no
// permission evaluation beyond declaring which one a route needs, no query composition, and no choice of
// which command to run. Phases 1 to 3 settled all of that, and re-deciding any of it here would be a
// second opinion that could disagree with the write boundary.
//
// The clearest evidence is what is absent: no DbContext, no repository, no DepartmentScopeResolver
// reference. The handlers resolve their own scope per request, exactly as the employee routes leave
// EmployeeScopeResolver to the employee handlers.
//
// ---- ONE ROUTE PER OPERATION. NO PAYLOAD DISCRIMINATORS.
//
// `move` and `move-to-root` are separate routes because Phase 2 shipped them as separate commands with
// different validation — a parent change walks the ancestry, a root move has no destination to check.
// Folding them into one route with a nullable parent would put the choice of command in transport and
// make the most destructive reading of the field the quiet one.
//
// ---- STATE CHANGES ARE NAMED POST ROUTES, NEVER VERBS ON A RESOURCE.
//
// The employee surface has no MapDelete at all: activate, deactivate, terminate and transfer are all named
// POSTs. Manager removal follows it as `manager/remove` rather than `DELETE /manager`, because ending an
// association is not deleting a resource — the employee is untouched, and only the association ends.
public static class DepartmentEndpointRouteBuilderExtensions
{
  private const string RoutePrefix = "/api/hr/departments";

  private const string EmployeeRoutePrefix = "/api/hr/employees";

  // HR's own i18n key for this surface: the shared transport projection requires each surface to name its
  // own rather than inherit another module's.
  private const string ResourceKey = "hr.departments.errors.request_rejected";

  public static IEndpointRouteBuilder MapHrDepartmentEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    var group = endpoints.MapGroup(RoutePrefix)
      .WithTags("HR Departments")
      // ONE FILTER, EVERY ROUTE. On the group rather than each endpoint so a route added later cannot
      // forget it. Every department operation is company-owned, so establishing is never optional.
      .AddEndpointFilter<CompanyContextEndpointFilter>()
      .AddEndpointFilter(async (context, next) =>
      {
        ApiResponseSecurity.Apply(context.HttpContext);
        return await next(context);
      });

    group.MapPost("", CreateAsync)
      .RequirePermission(HrPermissionNames.CreateDepartments)
      .WithName("HrDepartmentsCreate");

    group.MapGet("", SearchAsync)
      .RequirePermission(HrPermissionNames.ViewDepartments)
      .WithName("HrDepartmentsSearch");

    group.MapGet("/{departmentId:guid}", GetByIdAsync)
      .RequirePermission(HrPermissionNames.ViewDepartments)
      .WithName("HrDepartmentsGetById");

    group.MapGet("/{departmentId:guid}/children", GetChildrenAsync)
      .RequirePermission(HrPermissionNames.ViewDepartments)
      .WithName("HrDepartmentsChildren");

    group.MapPut("/{departmentId:guid}", UpdateAsync)
      .RequirePermission(HrPermissionNames.UpdateDepartments)
      .WithName("HrDepartmentsUpdate");

    // ---- HIERARCHY. Two operations, two routes, both Update authority.
    group.MapPost("/{departmentId:guid}/move", MoveAsync)
      .RequirePermission(HrPermissionNames.UpdateDepartments)
      .WithName("HrDepartmentsMove");

    group.MapPost("/{departmentId:guid}/move-to-root", MoveToRootAsync)
      .RequirePermission(HrPermissionNames.UpdateDepartments)
      .WithName("HrDepartmentsMoveToRoot");

    // ---- MANAGER. Assignment is a replacement, so one route covers assign and reassign.
    group.MapPost("/{departmentId:guid}/manager", AssignManagerAsync)
      .RequirePermission(HrPermissionNames.UpdateDepartments)
      .WithName("HrDepartmentsAssignManager");

    group.MapPost("/{departmentId:guid}/manager/remove", RemoveManagerAsync)
      .RequirePermission(HrPermissionNames.UpdateDepartments)
      .WithName("HrDepartmentsRemoveManager");

    // ---- LIFECYCLE. BOTH DIRECTIONS CARRY THE *Deactivate* PERMISSION.
    //
    // That permission governs whether a department may receive employees, and both directions change that
    // answer: deactivating closes it to new members, activating reopens it. Granting reactivation under
    // ordinary Update authority would let a caller who may only rename a department undo a closure someone
    // with the sensitive permission deliberately made. Same reasoning as HrPermissionNames'
    // DeactivateDepartments rationale — the permission names the capability, not the direction.
    group.MapPost("/{departmentId:guid}/activate", ActivateAsync)
      .RequirePermission(HrPermissionNames.DeactivateDepartments)
      .WithName("HrDepartmentsActivate");

    group.MapPost("/{departmentId:guid}/deactivate", DeactivateAsync)
      .RequirePermission(HrPermissionNames.DeactivateDepartments)
      .WithName("HrDepartmentsDeactivate");

    return endpoints;
  }

  // ---- THE EMPLOYEE-GROUP ROUTE, MAPPED HERE BECAUSE IT IS A DEPARTMENT OPERATION.
  //
  // It lives on the employee prefix because it changes an EMPLOYEE, and it carries HR.Employees.Update
  // rather than a transfer permission: DepartmentId is a classification, not a security partition
  // (ADR-024), so nothing moves across an authorization boundary. Registered separately so the department
  // group's company filter and this route's employee-group conventions do not have to be reconciled.
  public static IEndpointRouteBuilder MapHrEmployeeDepartmentEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    var group = endpoints.MapGroup(EmployeeRoutePrefix)
      .WithTags("HR Employees")
      .AddEndpointFilter<CompanyContextEndpointFilter>()
      .AddEndpointFilter(async (context, next) =>
      {
        ApiResponseSecurity.Apply(context.HttpContext);
        return await next(context);
      });

    group.MapPost("/{employeeId:guid}/change-department", ChangeEmployeeDepartmentAsync)
      .RequirePermission(HrPermissionNames.UpdateEmployees)
      .WithName("HrEmployeesChangeDepartment");

    return endpoints;
  }

  // ================================================================================================
  // WRITES
  // ================================================================================================

  private static async Task<IResult> CreateAsync(
    HttpContext context,
    CreateDepartmentCommandHandler handler,
    GetDepartmentQueryHandler reader,
    ICurrentCompany currentCompany,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreateDepartmentRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["code"] = [JsonValueKind.String],
        ["name"] = [JsonValueKind.String],
        ["parentDepartmentId"] = [JsonValueKind.String, JsonValueKind.Null]
      },
      cancellationToken,
      requiredFields: ["code", "name"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    // The company comes from the ESTABLISHED context, never from the body. The filter above already proved
    // it exists, is active and is reachable by this caller.
    if (currentCompany.CompanyId is not { } companyId)
    {
      return Problem(context, ApiErrors.Forbidden);
    }

    var created = await handler.HandleAsync(
      new CreateDepartmentCommand(companyId, request.Code!, request.Name!, request.ParentDepartmentId),
      cancellationToken);

    if (created.IsFailure)
    {
      return Problem(context, DepartmentApiErrorMapper.Map(created.Error));
    }

    // Read back through the SCOPED read path, so the response is built from what the caller is actually
    // permitted to see rather than from what the command happened to write.
    var read = await reader.HandleAsync(new GetDepartmentQuery(created.Value), cancellationToken);

    return read.IsFailure
      ? Problem(context, ApiErrors.WriteFailure)
      : Results.Created($"{RoutePrefix}/{created.Value}", ToResponse(read.Value));
  }

  private static async Task<IResult> UpdateAsync(
    HttpContext context,
    Guid departmentId,
    UpdateDepartmentCommandHandler handler,
    GetDepartmentQueryHandler reader,
    CancellationToken cancellationToken)
  {
    // parentDepartmentId and status are absent from the declared set, so an update cannot express a
    // hierarchy move or a lifecycle change.
    var request = await StrictRequestReader.ReadStrictJsonAsync<UpdateDepartmentRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["code"] = [JsonValueKind.String],
        ["name"] = [JsonValueKind.String],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["code", "name", "expectedRowVersion"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    var result = await handler.HandleAsync(
      new UpdateDepartmentCommand(departmentId, request.Code!, request.Name!, rowVersion),
      cancellationToken);

    return await ReadBackAsync(context, reader, departmentId, result, cancellationToken);
  }

  private static async Task<IResult> MoveAsync(
    HttpContext context,
    Guid departmentId,
    ChangeDepartmentParentCommandHandler handler,
    GetDepartmentQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<MoveDepartmentRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["parentDepartmentId"] = [JsonValueKind.String],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["parentDepartmentId", "expectedRowVersion"]);

    // parentDepartmentId is REQUIRED here and null is not an accepted kind: "no parent" is the
    // move-to-root route, not a null on this one.
    if (request?.ParentDepartmentId is not { } parentDepartmentId)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    var result = await handler.HandleAsync(
      new ChangeDepartmentParentCommand(departmentId, parentDepartmentId, rowVersion), cancellationToken);

    return await ReadBackAsync(context, reader, departmentId, result, cancellationToken);
  }

  private static async Task<IResult> MoveToRootAsync(
    HttpContext context,
    Guid departmentId,
    MoveDepartmentToRootCommandHandler handler,
    GetDepartmentQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var parsed = await ReadRowVersionAsync(context, cancellationToken);
    if (parsed.Failure is { } failure)
    {
      return failure;
    }

    var result = await handler.HandleAsync(
      new MoveDepartmentToRootCommand(departmentId, parsed.RowVersion), cancellationToken);

    return await ReadBackAsync(context, reader, departmentId, result, cancellationToken);
  }

  private static async Task<IResult> AssignManagerAsync(
    HttpContext context,
    Guid departmentId,
    AssignDepartmentManagerCommandHandler handler,
    GetDepartmentQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<AssignDepartmentManagerRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["employeeId"] = [JsonValueKind.String],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["employeeId", "expectedRowVersion"]);

    if (request?.EmployeeId is not { } employeeId)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    var result = await handler.HandleAsync(
      new AssignDepartmentManagerCommand(departmentId, employeeId, rowVersion), cancellationToken);

    // ---- THE PRE-TRANSLATION. See DepartmentApiErrorMapper.TranslateManagerConflict.
    //
    // On this route the only unique constraint is PK_DepartmentManagers, so a violation means another
    // caller seated a manager first — the same answer a stale rowversion deserves, and the caller must not
    // be able to tell which check fired.
    if (result.IsFailure)
    {
      return Problem(
        context, DepartmentApiErrorMapper.Map(DepartmentApiErrorMapper.TranslateManagerConflict(result.Error)));
    }

    return await ReadBackAsync(context, reader, departmentId, result, cancellationToken);
  }

  private static async Task<IResult> RemoveManagerAsync(
    HttpContext context,
    Guid departmentId,
    ClearDepartmentManagerCommandHandler handler,
    GetDepartmentQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var parsed = await ReadRowVersionAsync(context, cancellationToken);
    if (parsed.Failure is { } failure)
    {
      return failure;
    }

    var result = await handler.HandleAsync(
      new ClearDepartmentManagerCommand(departmentId, parsed.RowVersion), cancellationToken);

    return await ReadBackAsync(context, reader, departmentId, result, cancellationToken);
  }

  private static async Task<IResult> ActivateAsync(
    HttpContext context,
    Guid departmentId,
    ReactivateDepartmentCommandHandler handler,
    GetDepartmentQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var parsed = await ReadRowVersionAsync(context, cancellationToken);
    if (parsed.Failure is { } failure)
    {
      return failure;
    }

    var result = await handler.HandleAsync(
      new ReactivateDepartmentCommand(departmentId, parsed.RowVersion), cancellationToken);

    return await ReadBackAsync(context, reader, departmentId, result, cancellationToken);
  }

  private static async Task<IResult> DeactivateAsync(
    HttpContext context,
    Guid departmentId,
    DeactivateDepartmentCommandHandler handler,
    GetDepartmentQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var parsed = await ReadRowVersionAsync(context, cancellationToken);
    if (parsed.Failure is { } failure)
    {
      return failure;
    }

    var result = await handler.HandleAsync(
      new DeactivateDepartmentCommand(departmentId, parsed.RowVersion), cancellationToken);

    return await ReadBackAsync(context, reader, departmentId, result, cancellationToken);
  }

  private static async Task<IResult> ChangeEmployeeDepartmentAsync(
    HttpContext context,
    Guid employeeId,
    SSAS.HR.Application.Employees.ChangeEmployeeDepartmentCommandHandler handler,
    SSAS.HR.Application.Employees.Reads.GetEmployeeQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<ChangeEmployeeDepartmentRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["departmentId"] = [JsonValueKind.String],
        ["reasonCode"] = [JsonValueKind.String, JsonValueKind.Null],
        ["reasonText"] = [JsonValueKind.String, JsonValueKind.Null],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["departmentId", "expectedRowVersion"]);

    if (request?.DepartmentId is not { } departmentId)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    var result = await handler.HandleAsync(
      new SSAS.HR.Application.Employees.ChangeEmployeeDepartmentCommand(
        employeeId, departmentId, rowVersion, request.ReasonCode, request.ReasonText),
      cancellationToken);

    // The EMPLOYEE mapper, because this route answers about an employee. Its Employee.Department* arms are
    // the ones that describe an unusable destination here.
    if (result.IsFailure)
    {
      return Problem(context, EmployeeApiErrorMapper.Map(result.Error));
    }

    var read = await reader.HandleAsync(
      new SSAS.HR.Application.Employees.Reads.GetEmployeeQuery(employeeId), cancellationToken);

    return read.IsFailure
      ? Problem(context, ApiErrors.WriteFailure)
      : Results.Ok(EmployeeResponse.From(read.Value, RowVersionCodec.Encode(read.Value.RowVersion)));
  }

  // ================================================================================================
  // READS
  // ================================================================================================

  private static async Task<IResult> GetByIdAsync(
    HttpContext context,
    Guid departmentId,
    GetDepartmentQueryHandler handler,
    CancellationToken cancellationToken)
  {
    var result = await handler.HandleAsync(new GetDepartmentQuery(departmentId), cancellationToken);

    // Unknown, another tenant's, another company's and out-of-scope departments are all
    // department.not_found — the handler already collapsed them, and this simply does not undo it.
    return result.IsFailure
      ? Problem(context, DepartmentApiErrorMapper.Map(result.Error))
      : Results.Ok(ToResponse(result.Value));
  }

  private static async Task<IResult> GetChildrenAsync(
    HttpContext context,
    Guid departmentId,
    GetDepartmentChildrenQueryHandler handler,
    CancellationToken cancellationToken)
  {
    var result = await handler.HandleAsync(
      new GetDepartmentChildrenQuery(departmentId), cancellationToken);

    return result.IsFailure
      ? Problem(context, DepartmentApiErrorMapper.Map(result.Error))
      : Results.Ok(result.Value.Select(DepartmentChildResponse.From).ToArray());
  }

  private static async Task<IResult> SearchAsync(
    HttpContext context,
    SearchDepartmentsQueryHandler handler,
    CancellationToken cancellationToken)
  {
    if (!TrySearchQuery(context.Request.Query, out var query))
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var result = await handler.HandleAsync(query, cancellationToken);
    if (result.IsFailure)
    {
      return Problem(context, DepartmentApiErrorMapper.Map(result.Error));
    }

    var page = result.Value;

    return Results.Ok(new DepartmentPageResponse(
      page.Items
        .Select(item => DepartmentSummaryResponse.From(item, RowVersionCodec.Encode(item.RowVersion)))
        .ToArray(),
      page.PageNumber,
      page.PageSize,
      page.TotalCount,
      page.TotalPages));
  }

  // ================================================================================================
  // SHARED
  // ================================================================================================

  // EVERY WRITE READS BACK THROUGH THE SCOPED PATH. The response describes what the caller may see, not
  // what the command wrote — the two can differ, and the scoped answer is the honest one.
  private static async Task<IResult> ReadBackAsync(
    HttpContext context,
    GetDepartmentQueryHandler reader,
    Guid departmentId,
    Result result,
    CancellationToken cancellationToken)
  {
    if (result.IsFailure)
    {
      return Problem(context, DepartmentApiErrorMapper.Map(result.Error));
    }

    var read = await reader.HandleAsync(new GetDepartmentQuery(departmentId), cancellationToken);

    return read.IsFailure
      ? Problem(context, ApiErrors.WriteFailure)
      : Results.Ok(ToResponse(read.Value));
  }

  private static async Task<(IResult? Failure, byte[] RowVersion)> ReadRowVersionAsync(
    HttpContext context, CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<DepartmentRowVersionRequest>(
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
  // the search handler applies when it REFUSES an out-of-range page rather than clamping it.
  private static bool TrySearchQuery(IQueryCollection queryString, out SearchDepartmentsQuery query)
  {
    query = new SearchDepartmentsQuery();

    var page = DepartmentSearchCriteria.DefaultPageNumber;
    var pageSize = DepartmentSearchCriteria.DefaultPageSize;

    if (queryString.TryGetValue("page", out var pageValues) &&
      !int.TryParse(pageValues.ToString(), out page))
    {
      return false;
    }

    if (queryString.TryGetValue("pageSize", out var sizeValues) &&
      !int.TryParse(sizeValues.ToString(), out pageSize))
    {
      return false;
    }

    DepartmentStatus? status = null;
    if (queryString.TryGetValue("status", out var statusValues) &&
      !string.IsNullOrWhiteSpace(statusValues.ToString()))
    {
      if (!Enum.TryParse<DepartmentStatus>(statusValues.ToString(), ignoreCase: true, out var parsed))
      {
        return false;
      }

      status = parsed;
    }

    Guid? parentDepartmentId = null;
    if (queryString.TryGetValue("parentDepartmentId", out var parentValues) &&
      !string.IsNullOrWhiteSpace(parentValues.ToString()))
    {
      if (!Guid.TryParse(parentValues.ToString(), out var parsedParent))
      {
        return false;
      }

      parentDepartmentId = parsedParent;
    }

    // ---- THE COMPANY SCOPE MODE IS NOT A QUERY PARAMETER.
    //
    // It stays at CurrentCompany, the established context. Letting a caller widen their own scope from the
    // query string would be the one thing this whole surface exists to prevent — and the resolver would
    // refuse it anyway, so accepting it would only produce a confusing denial.
    var searchText = queryString.TryGetValue("search", out var searchValues)
      ? searchValues.ToString()
      : null;

    query = new SearchDepartmentsQuery(
      DepartmentCompanyScopeMode.CurrentCompany,
      string.IsNullOrWhiteSpace(searchText) ? null : searchText,
      status,
      parentDepartmentId,
      page,
      pageSize);

    return true;
  }

  private static DepartmentResponse ToResponse(DepartmentDetail detail) =>
    DepartmentResponse.From(detail, RowVersionCodec.Encode(detail.RowVersion));

  private static IResult Problem(HttpContext context, ApiError error) =>
    ApiProblems.Problem(context, error, ResourceKey);
}
