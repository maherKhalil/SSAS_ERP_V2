using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.ImportExport;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Employees;
using SSAS.HR.Domain.ImportExport;
using SSAS.BuildingBlocks.Api.Authorization;

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

    // The gate's dependency, asserted HERE so a host that mounts these routes without it fails at
    // startup rather than answering 500 per request (T-034).
    endpoints.RequireModuleEnablementServices(HrModuleEnablement.Key);

    var group = endpoints.MapGroup(RoutePrefix)
      .WithTags("HR Employees")
      // ---- THE MODULE ENABLEMENT GATE, ON THE GROUP (FP-014, `OD-SUB-0003`).
      //
      // On the GROUP rather than each route, for the same reason the filters below are: a route
      // added later cannot forget it. Entitlement does not differ per operation, so it belongs one
      // level up from `RequirePermission`.
      .RequireModule(HrModuleEnablement.Key)
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

    // ================================================================================================
    // FP-009 PHASE 2. FIVE ROUTES: BULK IN, BULK OUT, AND THE TWO AUDIT LISTINGS.
    // ================================================================================================
    //
    // ---- THE SIZE CEILING IS AT THE ROUTE, NOT AT THE GROUP, AND ONLY ON THE TWO THAT CARRY A FILE.
    //
    // Every other filter here is applied to the group precisely so a route added later cannot forget it.
    // This is the deliberate exception: a TIGHTENING that belongs to two routes. A group-wide 10 MB ceiling
    // would silently RAISE the limit for all forty-four JSON routes in HR — the group's cannot-forget logic
    // protects DEFAULTS, and a per-route narrowing is the opposite shape.
    //
    // ---- `import/validate` IS A `POST` THAT CREATES NO EMPLOYEES.
    //
    // A body on a `GET` is outside what the platform's transports handle predictably, so `POST` here means
    // "a request with a payload", not "this mutates". It writes one run record with outcome `Validated`,
    // which `api-contracts.md` calls the honest exception — audit rather than state.
    group.MapPost("/import/validate", ValidateImportAsync)
      .RequirePermission(HrPermissionNames.ImportEmployees)
      .WithMaxBodySize(EmployeeImportLimits.Default.MaximumBytes)
      .WithName("HrEmployeesImportValidate");

    group.MapPost("/import", ImportAsync)
      .RequirePermission(HrPermissionNames.ImportEmployees)
      .WithMaxBodySize(EmployeeImportLimits.Default.MaximumBytes)
      .WithName("HrEmployeesImport");

    // ---- BOTH HISTORIES CARRY `View`, NEVER `Import` OR `Export`.
    //
    // Reading the record that an extraction happened is an employee read; PERFORMING one is the separately
    // granted capability (`OD-DOC-005`). Gating the history on `Export` would mean the people who audit
    // extractions must also be able to perform them.
    group.MapGet("/import-runs", GetImportRunsAsync)
      .RequirePermission(HrPermissionNames.ViewEmployees)
      .WithName("HrEmployeesImportRuns");

    group.MapGet("/export", ExportAsync)
      .RequirePermission(HrPermissionNames.ExportEmployees)
      .WithName("HrEmployeesExport");

    group.MapGet("/export-runs", GetExportRunsAsync)
      .RequirePermission(HrPermissionNames.ViewEmployees)
      .WithName("HrEmployeesExportRuns");

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

  // ================================================================================================
  // THE FILTER VOCABULARY, IN ONE PLACE, SHARED BY SEARCH AND EXPORT (FP-009, R7)
  // ================================================================================================
  //
  // `api-contracts.md` says the export "accepts **the same query parameters as employee search**, with the
  // same strict allowlist, the same defaults and the same refusals". The way to make that true of the NEXT
  // parameter as well is for there to be exactly one place to add one — so the names and their parsing live
  // here, and the two entry points below differ only in how they treat PAGING.
  //
  // The FP-006/FP-007 audit is why this is written this way. It found `FR-DEP-0111` implemented end-to-end
  // below the transport and unreachable above it — a capability nobody could use. A parameter that reached
  // search and silently not export would be the same defect wearing a different hat, and a copied allowlist
  // is exactly how it would arrive.
  private static readonly string[] EmployeeFilterParameters =
  [
    "status", "branchScope", "branchIds", "companyScope", "employeeNumber",
    // ---- FR-DEP-0111, REACHABLE AT LAST (shipped 2026-08-22).
    //
    // Everything beneath this line already existed: `EmployeeSearchCriteria.DepartmentId`, the SQL conjunct,
    // and the tests proving it narrows rather than widens. Only the allowlist entry was missing, so the
    // filter was implemented and unreachable — a request naming it was rejected as an undeclared parameter.
    // Adding the name was the whole unblock, and the export INHERITS it here rather than restating it.
    "departmentId"
  ];

  private static readonly string[] PagingParameters = ["pageNumber", "pageSize"];

  // What both entry points get out of the shared parse. A record rather than five out-parameters, so adding
  // a filter is one field here instead of a signature change at every call site.
  private sealed record EmployeeFilters(
    EmployeeScopeRequest Scope,
    string? EmployeeNumber,
    IReadOnlyCollection<EmployeeStatus>? Statuses,
    Guid? DepartmentId);

  // ---- THE SHARED PARSE. Every refusal below belongs to search and export alike, by construction.
  private static bool TryEmployeeFilters(
    IQueryCollection values, string[] allowed, out EmployeeFilters filters)
  {
    filters = default!;

    if (!StrictRequestReader.HasOnly(values, allowed) ||
      !StrictRequestReader.TryOptional(values, "status", out var statusText) ||
      !StrictRequestReader.IsOneOf(statusText, ["Active", "Inactive", "Terminated"]) ||
      !StrictRequestReader.TryOptional(values, "branchScope", out var branchScopeText) ||
      !StrictRequestReader.IsOneOf(
        branchScopeText, ["CurrentBranch", "SelectedAuthorizedBranches", "AllAuthorizedBranches"]) ||
      !StrictRequestReader.TryOptional(values, "companyScope", out var companyScopeText) ||
      !StrictRequestReader.IsOneOf(companyScopeText, ["CurrentCompany", "AllAuthorizedCompanies"]) ||
      !StrictRequestReader.TryOptional(values, "employeeNumber", out var employeeNumber) ||
      !StrictRequestReader.TryOptional(values, "departmentId", out var departmentIdText))
    {
      return false;
    }

    // A malformed department identifier is a 400 rather than a filter that quietly matches nothing: the
    // second would answer "no employees in that department" to a caller who never named a department at all.
    Guid? departmentId = null;
    if (!string.IsNullOrWhiteSpace(departmentIdText))
    {
      if (!Guid.TryParse(departmentIdText, out var parsedDepartment))
      {
        return false;
      }

      departmentId = parsedDepartment;
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

    filters = new EmployeeFilters(
      new EmployeeScopeRequest(companyScope, branchScope, branchIds),
      employeeNumber,
      statuses,
      departmentId);

    return true;
  }

  // ---- SEARCH: THE SHARED FILTERS **PLUS** PAGING.
  private static bool TrySearchQuery(IQueryCollection values, out SearchEmployeesQuery query)
  {
    query = default!;

    if (!TryEmployeeFilters(values, [.. EmployeeFilterParameters, .. PagingParameters], out var filters) ||
      !StrictRequestReader.TryInt(values, "pageNumber", 1, out var pageNumber) ||
      !StrictRequestReader.TryInt(values, "pageSize", 50, out var pageSize))
    {
      return false;
    }

    query = new SearchEmployeesQuery(
      filters.Scope, pageNumber, pageSize, filters.EmployeeNumber, filters.Statuses, filters.DepartmentId);

    return true;
  }

  // ---- EXPORT: THE SHARED FILTERS AND **NOT** PAGING (R7).
  //
  // An export is not paged — a file with a page 2 is not a file — and the row CEILING governs its size
  // instead. `pageNumber` and `pageSize` are therefore absent from the allowlist, so naming either is
  // refused as an undeclared parameter, exactly as any other unknown name would be.
  //
  // ACCEPT-AND-IGNORE WAS FORBIDDEN BY `OD-DOC-010`'s OWN LOGIC, taken two days earlier: silently
  // discarding a declared parameter is the behaviour this contract refuses, and a caller who sent
  // `pageSize=50` and received five thousand rows would have been told nothing about what happened to their
  // request.
  private static bool TryExportQuery(IQueryCollection values, out ExportEmployeesQuery query)
  {
    query = default!;

    if (!TryEmployeeFilters(values, EmployeeFilterParameters, out var filters))
    {
      return false;
    }

    query = new ExportEmployeesQuery(
      filters.Scope, filters.EmployeeNumber, filters.Statuses, filters.DepartmentId);

    return true;
  }

  // ================================================================================================
  // IMPORT (FR-DOC-0101, FR-DOC-0102)
  // ================================================================================================
  //
  // Both routes share one body: the ONLY difference is `validateOnly`, and duplicating it to vary a boolean
  // is how two paths that must behave identically stop doing so.
  private static Task<IResult> ValidateImportAsync(
    HttpContext context, ImportEmployeesCommandHandler handler, CancellationToken cancellationToken) =>
    RunImportAsync(context, handler, validateOnly: true, cancellationToken);

  private static Task<IResult> ImportAsync(
    HttpContext context, ImportEmployeesCommandHandler handler, CancellationToken cancellationToken) =>
    RunImportAsync(context, handler, validateOnly: false, cancellationToken);

  private static async Task<IResult> RunImportAsync(
    HttpContext context,
    ImportEmployeesCommandHandler handler,
    bool validateOnly,
    CancellationToken cancellationToken)
  {
    // ---- THE QUERY VOCABULARY IS CLOSED HERE TOO (`DEC-DOC-0014`).
    //
    // `importKey` is the only parameter either import route accepts, and it is REQUIRED. Multipart would
    // have carried it as a form field validated against a declared set; the strict allowlist gives the same
    // property over a different transport, which is why the change of transport cost nothing.
    if (!StrictRequestReader.HasOnly(context.Request.Query, ["importKey"]) ||
      !StrictRequestReader.TryRequired(context.Request.Query, "importKey", out var importKey))
    {
      return ApiProblems.Problem(context, ApiErrors.RequestInvalid, ResourceKey);
    }

    // ---- THE CONTENT-TYPE GATE, AND WHAT A NULL MEANS.
    //
    // `StrictCsvReader` returns null for a body that is not `text/csv`, declares a charset other than UTF-8,
    // or is not valid UTF-8. All three refuse the REQUEST rather than the file's contents, so none reaches
    // the handler and none writes a run record — there was no import to record.
    var content = await StrictCsvReader.ReadStrictCsvAsync(context, cancellationToken);
    if (content is null)
    {
      return ApiProblems.Problem(context, ApiErrors.RequestInvalid, ResourceKey);
    }

    // The size the TRANSPORT measured, not the string's length: a UTF-8 character is one to four bytes, and
    // the cap the operator was promised is a byte cap.
    var byteCount = context.Request.ContentLength is { } declared && declared >= 0
      ? (int)Math.Min(declared, int.MaxValue)
      : System.Text.Encoding.UTF8.GetByteCount(content);

    // A raw body carries no file name (`DEC-DOC-0017`).
    if (!TryFileName(context.Request, out var fileName))
    {
      return ApiProblems.Problem(context, ApiErrors.RequestInvalid, ResourceKey);
    }

    var report = await handler.HandleAsync(
      new ImportEmployeesCommand(content, importKey, fileName, byteCount, validateOnly), cancellationToken);

    // ---- A REFUSED RUN IS A `200`, NOT A `400`, AND THAT IS THE CONTRACT.
    //
    // The per-row report IS the response (`DEC-DOC-0003`) — the operator's working document, which they fix
    // the file against and re-submit. A problem document would discard the very thing they need. `outcome`
    // carries `Refused` and `rejectedCount` says how many rows to look at.
    //
    // What DOES answer with a problem document is the set of failures that produced no report at all: an
    // unusable import key, or an actor the boundary refused.
    return report.IsFailure
      ? ApiProblems.Problem(context, EmployeeApiErrorMapper.Map(report.Error), ResourceKey)
      : Results.Ok(EmployeeImportReportResponse.From(report.Value));
  }

  // ================================================================================================
  // EXPORT (FR-DOC-0201) — THE FIRST FILE RESPONSE IN THE PRODUCT
  // ================================================================================================
  private static async Task<IResult> ExportAsync(
    HttpContext context, ExportEmployeesQueryHandler handler, CancellationToken cancellationToken)
  {
    if (!TryExportQuery(context.Request.Query, out var query))
    {
      return ApiProblems.Problem(context, ApiErrors.RequestInvalid, ResourceKey);
    }

    var exported = await handler.HandleAsync(query, cancellationToken);
    if (exported.IsFailure)
    {
      return ApiProblems.Problem(context, EmployeeApiErrorMapper.Map(exported.Error), ResourceKey);
    }

    // ---- THE BOM IS ADDED HERE AND NOWHERE ELSE (`DEC-DOC-0008`).
    //
    // The application handler returns a string and stays byte-agnostic on purpose; a byte order mark is a
    // TRANSPORT concern. Excel opens UTF-8 without one as mojibake and this file exists to be opened in
    // Excel — and `StrictCsvReader` strips it on the way back in, so the round trip survives it.
    var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
      .Concat(System.Text.Encoding.UTF8.GetBytes(exported.Value.Content))
      .ToArray();

    // ---- THE HEADERS, AND HOW THEY COMPOSE WITH THE GROUP'S SECURITY FILTER.
    //
    // `ApiResponseSecurity` has ALREADY run — it is a group filter applied before the handler — so
    // `nosniff`, `no-store`, `no-cache` and `Referrer-Policy` are on this response and are NOT bypassed by
    // returning bytes.
    //
    // `nosniff` and `text/csv` compose rather than conflict: nosniff forbids the browser from second-
    // guessing a declared type, and this response declares its type honestly. The pairing worth knowing
    // about is `no-store` with `Content-Disposition: attachment`, which some older browsers handled badly
    // for downloads. The platform's headers win: an employee extract must not be cached.
    //
    // The FILE NAME is server-generated from the clock and carries no caller input — reflecting a
    // caller-supplied name into this header is a header-injection surface for no benefit.
    context.Response.Headers.ContentDisposition =
      $"attachment; filename=\"{exported.Value.FileName}\"";

    return Results.Bytes(bytes, "text/csv; charset=utf-8");
  }

  // ================================================================================================
  // THE RUN HISTORIES (FR-DOC-0103, FR-DOC-0202)
  // ================================================================================================
  private static async Task<IResult> GetImportRunsAsync(
    HttpContext context, SearchImportRunsQueryHandler handler, CancellationToken cancellationToken)
  {
    if (!TryRunHistoryQuery(context.Request.Query, out var pageNumber, out var pageSize))
    {
      return ApiProblems.Problem(context, ApiErrors.RequestInvalid, ResourceKey);
    }

    var page = await handler.HandleAsync(
      new SearchImportRunsQuery(pageNumber, pageSize), cancellationToken);

    return page.IsFailure
      ? ApiProblems.Problem(context, EmployeeApiErrorMapper.Map(page.Error), ResourceKey)
      : Results.Ok(EmployeeImportRunPageResponse.From(page.Value));
  }

  private static async Task<IResult> GetExportRunsAsync(
    HttpContext context, SearchExportRunsQueryHandler handler, CancellationToken cancellationToken)
  {
    if (!TryRunHistoryQuery(context.Request.Query, out var pageNumber, out var pageSize))
    {
      return ApiProblems.Problem(context, ApiErrors.RequestInvalid, ResourceKey);
    }

    var page = await handler.HandleAsync(
      new SearchExportRunsQuery(pageNumber, pageSize), cancellationToken);

    return page.IsFailure
      ? ApiProblems.Problem(context, EmployeeApiErrorMapper.Map(page.Error), ResourceKey)
      : Results.Ok(EmployeeExportRunPageResponse.From(page.Value));
  }

  // ---- THE IMPORTED FILE'S NAME (`DEC-DOC-0017`).
  //
  // `DEC-DOC-0014` removed multipart and with it the only place a file name could come from. It is recorded
  // rather than defaulted away because an audit column that always holds a constant is dead weight — and
  // recording it is the SAFE DIRECTION: caller input into a stored field that is never reflected back is not
  // the case `api-contracts.md` forbids, which is echoing caller input into a RESPONSE header.
  //
  // Three constraints, all read off the column's own definition — "recorded for audit; never used to locate
  // anything":
  //
  //   1. PATH COMPONENTS ARE STRIPPED to the leaf, so a value like `..\..\x.csv` stores as `x.csv`. It is a
  //      NAME, not a location, and storing something shaped like a path invites a later reader to treat it
  //      as one.
  //   2. CONTROL CHARACTERS ARE REFUSED rather than stripped. A name containing them is a malformed request,
  //      and silently cleaning it would record a value the caller never sent.
  //   3. LENGTH IS CAPPED to the column's own limit, so a value that passes here cannot fail to persist.
  private static bool TryFileName(HttpRequest request, out string fileName)
  {
    fileName = "import.csv";

    if (!request.Headers.TryGetValue("X-File-Name", out var declared))
    {
      return true;
    }

    var candidate = declared.ToString().Trim();
    if (string.IsNullOrEmpty(candidate))
    {
      return true;
    }

    if (candidate.Any(char.IsControl))
    {
      return false;
    }

    // Both separators, whatever the caller's platform. An all-separator value has an empty leaf, which
    // falls back to the default rather than storing nothing.
    var leaf = candidate[(candidate.LastIndexOfAny(['/', '\\']) + 1)..];
    if (string.IsNullOrWhiteSpace(leaf))
    {
      return true;
    }

    fileName = leaf.Length > EmployeeImportRun.FileNameMaximumLength
      ? leaf[..EmployeeImportRun.FileNameMaximumLength]
      : leaf;

    return true;
  }

  // Paging and nothing else. Both listings are audit reads over an append-only table, and the package
  // defines no filter vocabulary for them — inventing one here would be specifying a contract nobody
  // approved.
  private static bool TryRunHistoryQuery(IQueryCollection values, out int pageNumber, out int pageSize)
  {
    pageNumber = EmployeeRunHistoryCriteria.DefaultPageNumber;
    pageSize = EmployeeRunHistoryCriteria.DefaultPageSize;

    return StrictRequestReader.HasOnly(values, PagingParameters) &&
      StrictRequestReader.TryInt(
        values, "pageNumber", EmployeeRunHistoryCriteria.DefaultPageNumber, out pageNumber) &&
      StrictRequestReader.TryInt(
        values, "pageSize", EmployeeRunHistoryCriteria.DefaultPageSize, out pageSize);
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
