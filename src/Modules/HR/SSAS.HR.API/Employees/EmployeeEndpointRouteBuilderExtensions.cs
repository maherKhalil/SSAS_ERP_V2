using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.API.Employees;

// ==================================================================================================
// THE EMPLOYEE HTTP SURFACE (FP-006 api-contracts, FP-006C5).
// ==================================================================================================
//
// ---- WHAT THIS LAYER DOES, AND THE SHORT LIST IT DOES NOT.
//
// It parses transport, dispatches, and maps the answer. It does NOT decide anything: no company scope, no
// branch scope, no permission evaluation beyond declaring which one the route needs, and no query
// composition. Every one of those was settled in C1 to C4 and re-deciding any of it here would be a second
// opinion that could disagree with the write boundary.
//
// The clearest evidence is what is absent: no DbContext, no repository, no resolver, no IBranchTransferScope.
// The transfer route hands the destination to the command and lets the C2 sanctioned channel do its work.
//
// ---- COMPANY IS AN AMBIENT DIMENSION, SO IT IS A HEADER.
//
// `X-Company-Id` is INTENT. It is established once per request by the filter below, against live state, and
// is never written onto an entity from the request. Routes stay /api/hr/employees/... rather than
// /api/hr/companies/{companyId}/employees/... precisely because company is execution context like tenant and
// branch, not a resource in the path.
//
// BRANCH IS NEVER TRANSMITTED AT ALL. It comes from the durable session (ADR-023 decision 8). The single
// branch identifier a caller may send is the transfer DESTINATION, which is a business argument authorized
// server-side.
public static class EmployeeEndpointRouteBuilderExtensions
{
  private const string RoutePrefix = "/api/hr/employees";

  // HR's own i18n key: the shared transport projection requires each surface to name its own rather than
  // inherit another module's.
  private const string ResourceKey = "hr.employees.errors.request_rejected";

  public static IEndpointRouteBuilder MapHrEmployeeEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    var group = endpoints.MapGroup(RoutePrefix)
      .WithTags("HR Employees")
      // ---- ONE FILTER, EVERY ROUTE.
      //
      // Applied to the GROUP rather than to each endpoint so a route added later cannot forget it. Every
      // Employee operation is company-owned, so there is no route for which establishing is optional.
      .AddEndpointFilter<CompanyContextEndpointFilter>()
      // The contract's response security headers, applied to every route by the group rather than by each
      // handler remembering to — the one place a new route cannot omit them from.
      .AddEndpointFilter(async (context, next) =>
      {
        ApiResponseSecurity.Apply(context.HttpContext);
        return await next(context);
      });

    group.MapPost("", CreateAsync)
      .RequirePermission(HrPermissionNames.CreateEmployees)
      .WithName("HrEmployeesCreate");

    group.MapGet("", SearchAsync)
      .RequirePermission(HrPermissionNames.ViewEmployees)
      .WithName("HrEmployeesSearch");

    group.MapGet("/{employeeId:guid}", GetByIdAsync)
      .RequirePermission(HrPermissionNames.ViewEmployees)
      .WithName("HrEmployeesGetById");

    group.MapPut("/{employeeId:guid}", UpdateAsync)
      .RequirePermission(HrPermissionNames.UpdateEmployees)
      .WithName("HrEmployeesUpdate");

    // ---- LIFECYCLE ROUTES ARE SEPARATE OPERATIONS, NOT A STATUS FIELD.
    //
    // Activate and deactivate carry Update authority; termination carries its own, because it is terminal
    // and a user permitted to correct a profile is not thereby permitted to end employment.
    group.MapPost("/{employeeId:guid}/activate", ActivateAsync)
      .RequirePermission(HrPermissionNames.UpdateEmployees)
      .WithName("HrEmployeesActivate");

    group.MapPost("/{employeeId:guid}/deactivate", DeactivateAsync)
      .RequirePermission(HrPermissionNames.UpdateEmployees)
      .WithName("HrEmployeesDeactivate");

    group.MapPost("/{employeeId:guid}/terminate", TerminateAsync)
      .RequirePermission(HrPermissionNames.TerminateEmployees)
      .WithName("HrEmployeesTerminate");

    // Transfer moves a record across a security partition and is the only operation permitted to change
    // BranchId, so it holds a permission of its own (BRULE-EMP-0015).
    group.MapPost("/{employeeId:guid}/transfer", TransferAsync)
      .RequirePermission(HrPermissionNames.TransferEmployees)
      .WithName("HrEmployeesTransfer");

    group.MapGet("/{employeeId:guid}/branch-history", GetBranchHistoryAsync)
      .RequirePermission(HrPermissionNames.ViewEmployees)
      .WithName("HrEmployeesBranchHistory");

    return endpoints;
  }

  private static async Task<IResult> CreateAsync(
    HttpContext context,
    CreateEmployeeCommandHandler handler,
    GetEmployeeQueryHandler reader,
    CancellationToken cancellationToken)
  {
    // The declared field set IS the contract: tenantId, companyId, branchId and status are not listed, so a
    // request carrying one is rejected rather than quietly stripped.
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreateEmployeeRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["employeeNumber"] = [JsonValueKind.String],
        ["fullName"] = [JsonValueKind.String],
        ["employmentDate"] = [JsonValueKind.String],
        ["nationalId"] = [JsonValueKind.String, JsonValueKind.Null],
        ["departmentId"] = [JsonValueKind.String],
        ["positionId"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields:
        ["employeeNumber", "fullName", "employmentDate", "departmentId", "positionId"]);

    if (request is null ||
      request.EmploymentDate is not { } employmentDate ||
      request.DepartmentId is not { } departmentId ||
      request.PositionId is not { } positionId)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var created = await handler.HandleAsync(
      new CreateEmployeeCommand(
        request.EmployeeNumber!, request.FullName!, employmentDate, request.NationalId,
        departmentId, positionId),
      cancellationToken);

    if (created.IsFailure)
    {
      return Problem(context, EmployeeApiErrorMapper.Map(created.Error));
    }

    // Read back through the SCOPED read path, so the response is built from what the caller is actually
    // permitted to see rather than from what the command happened to write.
    var read = await reader.HandleAsync(new GetEmployeeQuery(created.Value), cancellationToken);

    return read.IsFailure
      ? Problem(context, ApiErrors.WriteFailure)
      : Results.Created($"{RoutePrefix}/{created.Value}", ToResponse(read.Value));
  }

  private static async Task<IResult> GetByIdAsync(
    HttpContext context,
    Guid employeeId,
    GetEmployeeQueryHandler handler,
    CancellationToken cancellationToken)
  {
    var result = await handler.HandleAsync(new GetEmployeeQuery(employeeId), cancellationToken);

    // Unknown, another tenant's, another company's and an unauthorized branch's employee are all
    // employee.not_found — the handler already collapsed them, and this simply does not undo it.
    return result.IsFailure
      ? Problem(context, EmployeeApiErrorMapper.Map(result.Error))
      : Results.Ok(ToResponse(result.Value));
  }

  private static async Task<IResult> SearchAsync(
    HttpContext context,
    SearchEmployeesQueryHandler handler,
    CancellationToken cancellationToken)
  {
    if (!TrySearchQuery(context.Request.Query, out var query))
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var result = await handler.HandleAsync(query, cancellationToken);
    if (result.IsFailure)
    {
      return Problem(context, EmployeeApiErrorMapper.Map(result.Error));
    }

    var page = result.Value;

    return Results.Ok(new EmployeePageResponse(
      page.Items.Select(EmployeeSummaryResponse.From).ToArray(),
      page.PageNumber,
      page.PageSize,
      page.TotalCount,
      page.TotalPages));
  }

  private static async Task<IResult> UpdateAsync(
    HttpContext context,
    Guid employeeId,
    UpdateEmployeeProfileCommandHandler handler,
    GetEmployeeQueryHandler reader,
    CancellationToken cancellationToken)
  {
    // employeeNumber, status, and the three ownership fields are absent from the declared set, so an update
    // cannot express a rename of the identifier, a lifecycle change, or a transfer.
    var request = await StrictRequestReader.ReadStrictJsonAsync<UpdateEmployeeProfileRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["fullName"] = [JsonValueKind.String],
        ["nationalId"] = [JsonValueKind.String, JsonValueKind.Null],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["fullName", "expectedRowVersion"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    var result = await handler.HandleAsync(
      new UpdateEmployeeProfileCommand(employeeId, request.FullName!, request.NationalId, rowVersion),
      cancellationToken);

    return await ReadBackAsync(context, reader, employeeId, result, cancellationToken);
  }

  private static async Task<IResult> ActivateAsync(
    HttpContext context,
    Guid employeeId,
    ActivateEmployeeCommandHandler handler,
    GetEmployeeQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var parsed = await ReadLifecycleAsync(context, cancellationToken);
    if (parsed.Failure is { } failure)
    {
      return failure;
    }

    var result = await handler.HandleAsync(
      new ActivateEmployeeCommand(employeeId, parsed.Reason, parsed.RowVersion), cancellationToken);

    return await ReadBackAsync(context, reader, employeeId, result, cancellationToken);
  }

  private static async Task<IResult> DeactivateAsync(
    HttpContext context,
    Guid employeeId,
    DeactivateEmployeeCommandHandler handler,
    GetEmployeeQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var parsed = await ReadLifecycleAsync(context, cancellationToken);
    if (parsed.Failure is { } failure)
    {
      return failure;
    }

    var result = await handler.HandleAsync(
      new DeactivateEmployeeCommand(employeeId, parsed.Reason, parsed.RowVersion), cancellationToken);

    return await ReadBackAsync(context, reader, employeeId, result, cancellationToken);
  }

  private static async Task<IResult> TerminateAsync(
    HttpContext context,
    Guid employeeId,
    TerminateEmployeeCommandHandler handler,
    GetEmployeeQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<TerminateEmployeeRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["terminationDate"] = [JsonValueKind.String],
        ["reasonCode"] = [JsonValueKind.String],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken);

    if (request is null ||
      request.TerminationDate is not { } terminationDate ||
      !TryParseStatusReason(request.ReasonCode, out var reason))
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    var result = await handler.HandleAsync(
      new TerminateEmployeeCommand(employeeId, terminationDate, reason, rowVersion), cancellationToken);

    return await ReadBackAsync(context, reader, employeeId, result, cancellationToken);
  }

  private static async Task<IResult> TransferAsync(
    HttpContext context,
    Guid employeeId,
    TransferEmployeeCommandHandler handler,
    GetEmployeeQueryHandler reader,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<TransferEmployeeRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["destinationBranchId"] = [JsonValueKind.String],
        ["reasonCode"] = [JsonValueKind.String],
        ["reasonText"] = [JsonValueKind.String, JsonValueKind.Null],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["destinationBranchId", "reasonCode", "expectedRowVersion"]);

    if (request is null ||
      request.DestinationBranchId is not { } destinationBranchId ||
      destinationBranchId == Guid.Empty ||
      !TryParseTransferReason(request.ReasonCode, out var reason) ||
      request.ReasonText is { Length: > 512 })
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    // ---- THE DESTINATION IS PASSED ON, NOT ACTED ON.
    //
    // No branch resolver call here, and no transfer scope opened here. The command owns the dual
    // authorization and the C2 sanctioned channel; this route's only job is to carry the argument.
    // InactiveSourceRecovery is deliberately not exposed: it is an administrative recovery, not part of the
    // ordinary transfer contract, and the command defaults it off.
    var result = await handler.HandleAsync(
      new TransferEmployeeCommand(employeeId, destinationBranchId, reason, request.ReasonText, rowVersion),
      cancellationToken);

    return await ReadBackAsync(context, reader, employeeId, result, cancellationToken);
  }

  private static async Task<IResult> GetBranchHistoryAsync(
    HttpContext context,
    Guid employeeId,
    GetEmployeeBranchHistoryQueryHandler handler,
    CancellationToken cancellationToken)
  {
    // Goes through the C4 handler, which proves the EMPLOYEE is in scope before any assignment row is read.
    // The assignment table is never queried from here.
    var result = await handler.HandleAsync(new GetEmployeeBranchHistoryQuery(employeeId), cancellationToken);

    return result.IsFailure
      ? Problem(context, EmployeeApiErrorMapper.Map(result.Error))
      : Results.Ok(result.Value.Select(EmployeeBranchHistoryResponse.From).ToArray());
  }

  // ---- THE SHARED SHAPE OF EVERY MUTATION'S RESPONSE.
  //
  // A successful write is answered by reading the record back through the SCOPED read path, so the caller
  // receives the post-write state including the new rowversion, projected from what they may see.
  private static async Task<IResult> ReadBackAsync(
    HttpContext context,
    GetEmployeeQueryHandler reader,
    Guid employeeId,
    Result result,
    CancellationToken cancellationToken)
  {
    if (result.IsFailure)
    {
      return Problem(context, EmployeeApiErrorMapper.Map(result.Error));
    }

    var read = await reader.HandleAsync(new GetEmployeeQuery(employeeId), cancellationToken);

    return read.IsFailure
      ? Problem(context, ApiErrors.WriteFailure)
      : Results.Ok(ToResponse(read.Value));
  }

  private static async Task<(IResult? Failure, EmployeeStatusChangeReason Reason, byte[] RowVersion)>
    ReadLifecycleAsync(HttpContext context, CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<EmployeeLifecycleRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["reasonCode"] = [JsonValueKind.String],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken);

    if (request is null || !TryParseStatusReason(request.ReasonCode, out var reason))
    {
      return (Problem(context, ApiErrors.RequestInvalid), default, []);
    }

    return !RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion)
      ? (Problem(context, ApiErrors.RowVersionInvalid), default, [])
      : (null, reason, rowVersion);
  }

  // Transport enforces the bounded reason SET; the domain remains authoritative for whether the transition
  // is permitted from the employee's current status. `Created` is excluded because it is the reason an
  // employee comes into existence, never a reason a caller may supply.
  private static bool TryParseStatusReason(string? value, out EmployeeStatusChangeReason reason)
  {
    reason = default;

    if (value is null || !StrictRequestReader.IsOneOf(
      value, ["Administrative", "Operational", "Compliance", "Resignation", "Dismissal", "EndOfContract"]))
    {
      return false;
    }

    reason = Enum.Parse<EmployeeStatusChangeReason>(value);
    return true;
  }

  // `InitialAssignment` is excluded for the same reason: it records the first assignment, which is written
  // by creation and can never be a caller-chosen transfer reason.
  private static bool TryParseTransferReason(string? value, out EmployeeBranchTransferReason reason)
  {
    reason = default;

    if (value is null || !StrictRequestReader.IsOneOf(
      value, ["Reorganisation", "OperationalNeed", "EmployeeRequest", "BranchClosure", "Correction"]))
    {
      return false;
    }

    reason = Enum.Parse<EmployeeBranchTransferReason>(value);
    return true;
  }

  private static bool TrySearchQuery(IQueryCollection values, out SearchEmployeesQuery query)
  {
    query = default!;

    // ---- THE ACCEPTED PARAMETER SET IS CLOSED.
    //
    // Exactly the approved contract, and no more. A filter that is not listed here — a name search, say — is
    // rejected rather than ignored, so a caller cannot believe they narrowed a result set that ran wide.
    if (!StrictRequestReader.HasOnly(
        values, ["pageNumber", "pageSize", "status", "branchScope", "branchIds", "companyScope", "employeeNumber"]) ||
      !StrictRequestReader.TryInt(values, "pageNumber", 1, out var pageNumber) ||
      !StrictRequestReader.TryInt(values, "pageSize", 50, out var pageSize) ||
      !StrictRequestReader.TryOptional(values, "status", out var statusText) ||
      !StrictRequestReader.IsOneOf(statusText, ["Active", "Inactive", "Terminated"]) ||
      !StrictRequestReader.TryOptional(values, "branchScope", out var branchScopeText) ||
      !StrictRequestReader.IsOneOf(
        branchScopeText, ["CurrentBranch", "SelectedAuthorizedBranches", "AllAuthorizedBranches"]) ||
      !StrictRequestReader.TryOptional(values, "companyScope", out var companyScopeText) ||
      !StrictRequestReader.IsOneOf(companyScopeText, ["CurrentCompany", "AllAuthorizedCompanies"]) ||
      !StrictRequestReader.TryOptional(values, "employeeNumber", out var employeeNumber))
    {
      return false;
    }

    var branchScope = branchScopeText is null
      ? EmployeeBranchScopeMode.CurrentBranch
      : Enum.Parse<EmployeeBranchScopeMode>(branchScopeText);

    var companyScope = companyScopeText is null
      ? EmployeeCompanyScopeMode.CurrentCompany
      : Enum.Parse<EmployeeCompanyScopeMode>(companyScopeText);

    // branchIds is comma-separated and REQUIRED for the selection mode, rejected for the others — a stray
    // list would otherwise let a caller believe they had narrowed a read that ignored it.
    Guid[]? branchIds = null;
    if (values.TryGetValue("branchIds", out var rawBranchIds))
    {
      if (branchScope != EmployeeBranchScopeMode.SelectedAuthorizedBranches || rawBranchIds.Count != 1)
      {
        return false;
      }

      var parsed = new List<Guid>();
      foreach (var candidate in rawBranchIds[0]!.Split(',', StringSplitOptions.TrimEntries))
      {
        if (!Guid.TryParse(candidate, out var branchId) || branchId == Guid.Empty)
        {
          return false;
        }

        parsed.Add(branchId);
      }

      branchIds = [.. parsed];
    }
    else if (branchScope == EmployeeBranchScopeMode.SelectedAuthorizedBranches)
    {
      return false;
    }

    // Omitted status means Active AND Inactive, applied in SQL by the read service. Null is passed through
    // rather than expanded here so the default cannot be lost in transport.
    IReadOnlyCollection<EmployeeStatus>? statuses = statusText is null
      ? null
      : [Enum.Parse<EmployeeStatus>(statusText)];

    query = new SearchEmployeesQuery(
      new EmployeeScopeRequest(companyScope, branchScope, branchIds),
      pageNumber,
      pageSize,
      employeeNumber,
      statuses);

    return true;
  }

  private static EmployeeResponse ToResponse(EmployeeDetail detail) =>
    EmployeeResponse.From(detail, RowVersionCodec.Encode(detail.RowVersion));

  private static IResult Problem(HttpContext context, ApiError error) =>
    ApiProblems.Problem(context, error, ResourceKey);
}

// ==================================================================================================
// ESTABLISH THE COMPANY BEFORE ANY EMPLOYEE WORK, OR REFUSE (ADR-025 decision 2).
// ==================================================================================================
//
// The header alone is intent. This runs the five-step live validation once per request — exists, belongs to
// the trusted tenant, is Active, and the caller is currently authorized — and refuses the request when it
// does not pass.
//
// ---- IT IS NOT THE AUTHORIZATION.
//
// Company access is revocable inside a request, so the write boundary re-asks regardless. This exists so a
// company-owned request fails EARLY and for a precise reason, not so a later check can be skipped.
//
// ---- SYNTAX AND SCOPE ARE DIFFERENT ANSWERS.
//
// A header that is missing or is not a well-formed identifier is a MALFORMED REQUEST: the caller can see
// their own header, so telling them it is malformed reveals nothing. A well-formed identifier that fails any
// validation step gets the one generic company denial, because distinguishing nonexistent from
// wrong-tenant from inactive from unauthorized would map the tenant's organisation for them.
public sealed class CompanyContextEndpointFilter(ICompanyContextEstablisher establisher) : IEndpointFilter
{
  private const string ResourceKey = "hr.employees.errors.request_rejected";

  public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(next);

    // The resolver already draws the distinction, and the mapper preserves it: a missing or malformed header
    // becomes 400, every validation outcome becomes one generic 403. This does not re-derive either.
    var established = await establisher.EstablishAsync(context.HttpContext.RequestAborted);

    return established.IsFailure
      ? ApiProblems.Problem(context.HttpContext, EmployeeApiErrorMapper.Map(established.Error), ResourceKey)
      : await next(context);
  }
}
