using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.Payroll.Application.Compensation;
using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Application.Elements;
using SSAS.Payroll.Application.Permissions;
using SSAS.Payroll.Application.Reads;
using SSAS.Payroll.Application.Runs;

namespace SSAS.Payroll.API;

// PAYROLL'S HTTP SURFACE (api-contracts.md).
//
// ---- STATE CHANGES ARE `POST` TO A SUB-RESOURCE, NEVER A STATUS FIELD ON A `PUT`.
//
// `/calculation`, `/approval`, `/posting`, `/reversals`, `/deactivation`, `/activation` — each an event with
// its own permission and its own refusals. A `PUT {status: "approved"}` would let the most sensitive act in
// the module arrive through the same door as an ordinary edit, and `BR-PLT-0103` names that act sensitive.
//
// ---- NOTHING RESPONDS TO `DELETE`, AND NOTHING IN THIS MODULE DESTROYS ANYTHING.
//
// GL has one destructive route (discarding a draft, which was never part of the ledger). Payroll has none
// at all: compensation is dated history, elements deactivate, and run lines are append-only once approved.
// A test asserts the verb's absence, so a future route adding it must delete the test.
//
// ---- COMPENSATION HAS A `POST` AND NO `PUT`.
//
// `OD-PAY-0003` ruled dated history, so a change is a NEW record. The absent verb is the ruling made visible
// in the surface rather than a rule someone has to remember.
public static class PayrollEndpointRouteBuilderExtensions
{
  private const string RoutePrefix = "/api/payroll";

  private const string ResourceKey = "payroll.errors.request_rejected";

  public static IEndpointRouteBuilder MapPayrollEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    // The gate's dependency, asserted HERE so a host that mounts these routes without it fails at
    // startup rather than answering 500 per request (T-034).
    endpoints.RequireModuleEnablementServices(PayrollModuleEnablement.Key);

    var group = endpoints.MapGroup(RoutePrefix)
      .WithTags("Payroll")
      // ---- THE MODULE ENABLEMENT GATE, ON THE GROUP (FP-014, `OD-SUB-0003`).
      //
      // On the GROUP rather than each route, for the same reason the filters below are: a route
      // added later cannot forget it. Entitlement does not differ per operation, so it belongs one
      // level up from `RequirePermission`.
      .RequireModule(PayrollModuleEnablement.Key)
      .AddEndpointFilter<PayrollCompanyContextEndpointFilter>()
      .AddEndpointFilter(async (context, next) =>
      {
        ApiResponseSecurity.Apply(context.HttpContext);
        return await next(context);
      });

    // ---- COMPENSATION. The personal-data surface (BR-PAY-0010, OD-PAY-0016).
    group.MapPost("/employees/{employeeId}/compensation", RecordCompensationAsync)
      .RequirePermission(PayrollPermissionNames.ManageCompensation).WithName("PayrollCompensationRecord");

    // ---- ONE-OFF PAY INSTRUCTIONS (T-110). POST ONLY, like compensation and for a different reason.
    //
    // Deciding someone is paid an amount is the same authority whether it recurs or happens once, so this
    // takes `ManageCompensation` rather than a permission of its own — a second one would let the two be
    // granted apart, which nobody has ruled.
    group.MapPost("/employees/{employeeId}/one-off-payments", RecordOneOffPaymentAsync)
      .RequirePermission(PayrollPermissionNames.ManageCompensation).WithName("PayrollOneOffPaymentRecord");
    group.MapGet("/employees/{employeeId}/compensation", GetCompensationHistoryAsync)
      .RequirePermission(PayrollPermissionNames.ViewCompensation).WithName("PayrollCompensationHistory");
    group.MapGet("/employees/{employeeId}/compensation/current", GetCompensationCurrentAsync)
      .RequirePermission(PayrollPermissionNames.ViewCompensation).WithName("PayrollCompensationCurrent");

    // ---- PAY ELEMENTS. Structural, and deliberately a weaker permission than compensation.
    group.MapPost("/elements", CreateElementAsync)
      .RequirePermission(PayrollPermissionNames.ManageElements).WithName("PayrollElementsCreate");
    group.MapGet("/elements", SearchElementsAsync)
      .RequirePermission(PayrollPermissionNames.ViewElements).WithName("PayrollElementsSearch");
    group.MapGet("/elements/{payElementId}", GetElementAsync)
      .RequirePermission(PayrollPermissionNames.ViewElements).WithName("PayrollElementsGet");
    group.MapPut("/elements/{payElementId}", UpdateElementAsync)
      .RequirePermission(PayrollPermissionNames.ManageElements).WithName("PayrollElementsUpdate");
    group.MapPost("/elements/{payElementId}/deactivation", DeactivateElementAsync)
      .RequirePermission(PayrollPermissionNames.ManageElements).WithName("PayrollElementsDeactivate");
    group.MapPost("/elements/{payElementId}/activation", ActivateElementAsync)
      .RequirePermission(PayrollPermissionNames.ManageElements).WithName("PayrollElementsActivate");

    // ---- PERIODS. Generated from the fiscal calendar, never authored beside it (OD-PAY-0002).
    group.MapPost("/periods", GeneratePeriodAsync)
      .RequirePermission(PayrollPermissionNames.ManageRuns).WithName("PayrollPeriodsGenerate");
    group.MapGet("/periods", GetPeriodsAsync)
      .RequirePermission(PayrollPermissionNames.ViewRuns).WithName("PayrollPeriodsList");

    // ---- RUNS. Each transition its own named action and its own grant.
    group.MapPost("/runs", CreateRunAsync)
      .RequirePermission(PayrollPermissionNames.ManageRuns).WithName("PayrollRunsCreate");
    group.MapGet("/runs", GetRunsAsync)
      .RequirePermission(PayrollPermissionNames.ViewRuns).WithName("PayrollRunsList");
    group.MapGet("/runs/{payrollRunId}", GetRunAsync)
      .RequirePermission(PayrollPermissionNames.ViewRuns).WithName("PayrollRunsGet");
    group.MapPost("/runs/{payrollRunId}/calculation", CalculateRunAsync)
      .RequirePermission(PayrollPermissionNames.ManageRuns).WithName("PayrollRunsCalculate");

    // THE SENSITIVE ACT (`BR-PLT-0103`, `OD-PAY-0009`). Its own permission, so preparing work and
    // authorizing it can be different people — the `GL.Drafts.Manage` / `GL.Journals.Post` precedent.
    group.MapPost("/runs/{payrollRunId}/approval", ApproveRunAsync)
      .RequirePermission(PayrollPermissionNames.ApproveRuns).WithName("PayrollRunsApprove");
    group.MapPost("/runs/{payrollRunId}/posting", PostRunAsync)
      .RequirePermission(PayrollPermissionNames.PostRuns).WithName("PayrollRunsPost");
    group.MapPost("/runs/{payrollRunId}/reversals", ReverseRunAsync)
      .RequirePermission(PayrollPermissionNames.PostRuns).WithName("PayrollRunsReverse");

    // ---- PAYSLIPS. A projection over approved lines only (OD-PAY-0015).
    group.MapGet("/runs/{payrollRunId}/payslips/{employeeId}", GetPayslipAsync)
      .RequirePermission(PayrollPermissionNames.ViewPayslips).WithName("PayrollPayslipsGet");
    // ---- SELF-SERVICE (FP-015, `REQ-SS-0004`, T-088). NO EMPLOYEE ANYWHERE IN THE CONTRACT.
    //
    // The route names no employee on its path, and the handler takes none from query, header or body: the
    // subject is resolved from the caller's own identity. **That is `AC-SS-0007`, and it is asserted against
    // the contract rather than the handler by `PayrollSelfServiceContractTests`.**
    //
    // It sits in the same group as everything above, so `RequireModule` and the `BR-PLT-0008` gate come
    // free — `REQ-SS-0008` costs nothing to satisfy and cannot be forgotten.
    group.MapGet("/me/payslips", GetOwnPayslipsAsync)
      .RequirePermission(PayrollPermissionNames.ViewOwnPayslips).WithName("PayrollOwnPayslipsList");

    group.MapGet("/employees/{employeeId}/payslips", GetPayslipsAsync)
      .RequirePermission(PayrollPermissionNames.ViewPayslips).WithName("PayrollPayslipsForEmployee");

    return endpoints;
  }

  private static IResult Problem(HttpContext context, ApiError error) =>
    ApiProblems.Problem(context, error, ResourceKey);

  // ---- COMPENSATION.

  private static async Task<IResult> RecordOneOffPaymentAsync(
    HttpContext context, Guid employeeId, RecordOneOffPaymentCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<RecordOneOffPaymentRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["companyId"] = [JsonValueKind.String],
        ["payrollPeriodId"] = [JsonValueKind.String],
        ["payElementId"] = [JsonValueKind.String],
        ["amount"] = [JsonValueKind.Number],
        ["reason"] = [JsonValueKind.String, JsonValueKind.Null]
      },
      cancellationToken,
      requiredFields: ["companyId", "payrollPeriodId", "payElementId", "amount"]);
    if (request is null)
    {
      return Results.Empty;
    }

    var created = await handler.HandleAsync(
      new RecordOneOffPaymentCommand(
        request.CompanyId, employeeId, request.PayrollPeriodId, request.PayElementId,
        request.Amount, request.Reason),
      cancellationToken);

    return created.IsFailure
      ? Problem(context, PayrollApiErrorMapper.Map(created.Error))
      : Results.Created(
        $"{RoutePrefix}/employees/{employeeId}/one-off-payments",
        new { oneOffPaymentId = created.Value });
  }

  private static async Task<IResult> RecordCompensationAsync(
    HttpContext context, Guid employeeId, RecordCompensationCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<RecordCompensationRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["companyId"] = [JsonValueKind.String],
        ["effectiveFromUtc"] = [JsonValueKind.String],
        ["baseAmount"] = [JsonValueKind.Number],

        // ---- ADDED T-110, AND IT IS A T-107 DEFECT RATHER THAN A T-110 FEATURE.
        //
        // T-107 added `salaryType` to `RecordCompensationRequest` and NOT to this dictionary.
        // `StrictRequestReader` rejects any member it does not declare (`:39`), so **a client sending a
        // salary type got a 400 and a client omitting it got Monthly** — hourly and daily were unreachable
        // through the API from the moment they shipped. The domain, the calculator, the persistence and the
        // migration were all correct; the door was never opened.
        // `Null` added T-112. T-111's sweep found this was the only optional member in the product that
        // refused an explicit null, and the rule the other sites follow is that an optional member accepts
        // one — `StrictRequestReader:45`, `requiredFields ?? fields.Keys`.
        ["salaryType"] = [JsonValueKind.String, JsonValueKind.Number, JsonValueKind.Null],

        ["assignments"] = [JsonValueKind.Array, JsonValueKind.Null],
        ["wasOutsideGradeBand"] = [JsonValueKind.True, JsonValueKind.False],
        ["gradeBandObservation"] = [JsonValueKind.String, JsonValueKind.Null]
      },
      cancellationToken,
      requiredFields: ["companyId", "effectiveFromUtc", "baseAmount"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var assignments = (request.Assignments ?? [])
      .Select(a => (a.PayElementId, a.RateOrAmount))
      .ToList();

    var created = await handler.HandleAsync(
      new RecordCompensationCommand(
        request.CompanyId, employeeId, request.EffectiveFromUtc, request.BaseAmount,
        request.SalaryType ?? SalaryType.Monthly,
        assignments, request.WasOutsideGradeBand, request.GradeBandObservation),
      cancellationToken);

    return created.IsFailure
      ? Problem(context, PayrollApiErrorMapper.Map(created.Error))
      : Results.Created(
        $"{RoutePrefix}/employees/{employeeId}/compensation",
        new { employeeCompensationId = created.Value });
  }

  private static async Task<IResult> GetCompensationHistoryAsync(
    HttpContext context, Guid employeeId, IPayrollScopeResolver resolver, IPayrollReadService reads,
    CancellationToken cancellationToken)
  {
    var scope = await resolver.ResolveAsync(PayrollPermissionNames.ViewCompensation, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, PayrollApiErrorMapper.Map(scope.Error));
    }

    var history = await reads.GetCompensationHistoryAsync(scope.Value, employeeId, cancellationToken);
    return Results.Ok(history);
  }

  private static async Task<IResult> GetCompensationCurrentAsync(
    HttpContext context, Guid employeeId, IPayrollScopeResolver resolver, IPayrollReadService reads,
    CancellationToken cancellationToken)
  {
    var scope = await resolver.ResolveAsync(PayrollPermissionNames.ViewCompensation, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, PayrollApiErrorMapper.Map(scope.Error));
    }

    var current = await reads.GetCompensationInForceAsync(
      scope.Value, employeeId, DateTimeOffset.UtcNow, cancellationToken);

    return current is null ? Problem(context, PayrollApiErrorMapper.NotFound) : Results.Ok(current);
  }

  // ---- ELEMENTS.

  private static async Task<IResult> CreateElementAsync(
    HttpContext context, ICurrentCompany company, CreatePayElementCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreatePayElementRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["code"] = [JsonValueKind.String],
        ["name"] = [JsonValueKind.String],
        ["kind"] = [JsonValueKind.String, JsonValueKind.Number],
        ["behaviour"] = [JsonValueKind.String, JsonValueKind.Number],
        ["defaultRateOrAmount"] = [JsonValueKind.Number],
        ["calculationOrder"] = [JsonValueKind.Number],
        ["glAccountId"] = [JsonValueKind.String, JsonValueKind.Null]
      },
      cancellationToken,
      requiredFields: ["code", "name", "kind", "behaviour"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    // The company comes from the ESTABLISHED CONTEXT, not the body. A caller who could name it could
    // authorize against one company and write into another.
    if (company.CompanyId is not { } companyId)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var created = await handler.HandleAsync(
      new CreatePayElementCommand(
        companyId, request.Code, request.Name, request.Kind, request.Behaviour,
        request.DefaultRateOrAmount, request.CalculationOrder, request.GlAccountId),
      cancellationToken);

    return created.IsFailure
      ? Problem(context, PayrollApiErrorMapper.Map(created.Error))
      : Results.Created($"{RoutePrefix}/elements/{created.Value}", new { payElementId = created.Value });
  }

  private static async Task<IResult> SearchElementsAsync(
    HttpContext context, ICurrentCompany company, IPayrollScopeResolver resolver, IPayrollReadService reads,
    CancellationToken cancellationToken)
  {
    var scope = await resolver.ResolveAsync(PayrollPermissionNames.ViewElements, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, PayrollApiErrorMapper.Map(scope.Error));
    }

    if (company.CompanyId is not { } companyId)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var search = context.Request.Query["search"].ToString();
    var elements = await reads.GetElementsAsync(scope.Value, companyId, search, cancellationToken);
    return Results.Ok(elements);
  }

  private static async Task<IResult> GetElementAsync(
    HttpContext context, Guid payElementId, IPayrollScopeResolver resolver, IPayrollReadService reads,
    CancellationToken cancellationToken)
  {
    var scope = await resolver.ResolveAsync(PayrollPermissionNames.ViewElements, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, PayrollApiErrorMapper.Map(scope.Error));
    }

    var element = await reads.GetElementAsync(scope.Value, payElementId, cancellationToken);
    return element is null ? Problem(context, PayrollApiErrorMapper.NotFound) : Results.Ok(element);
  }

  private static async Task<IResult> UpdateElementAsync(
    HttpContext context, Guid payElementId, UpdatePayElementCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<UpdatePayElementRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["name"] = [JsonValueKind.String],
        ["defaultRateOrAmount"] = [JsonValueKind.Number],
        ["calculationOrder"] = [JsonValueKind.Number],
        ["glAccountId"] = [JsonValueKind.String, JsonValueKind.Null],
        ["rowVersion"] = [JsonValueKind.String, JsonValueKind.Null]
      },
      cancellationToken,
      requiredFields: ["name"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var updated = await handler.HandleAsync(
      new UpdatePayElementCommand(
        payElementId, request.Name, request.DefaultRateOrAmount, request.CalculationOrder, request.GlAccountId),
      cancellationToken);

    return updated.IsFailure
      ? Problem(context, PayrollApiErrorMapper.Map(updated.Error))
      : Results.NoContent();
  }

  private static Task<IResult> DeactivateElementAsync(
    HttpContext context, Guid payElementId, SetPayElementActivationCommandHandler handler,
    CancellationToken cancellationToken) =>
    SetElementActivationAsync(context, payElementId, handler, isActive: false, cancellationToken);

  private static Task<IResult> ActivateElementAsync(
    HttpContext context, Guid payElementId, SetPayElementActivationCommandHandler handler,
    CancellationToken cancellationToken) =>
    SetElementActivationAsync(context, payElementId, handler, isActive: true, cancellationToken);

  private static async Task<IResult> SetElementActivationAsync(
    HttpContext context, Guid payElementId, SetPayElementActivationCommandHandler handler,
    bool isActive, CancellationToken cancellationToken)
  {
    var changed = await handler.HandleAsync(
      new SetPayElementActivationCommand(payElementId, isActive), cancellationToken);

    return changed.IsFailure
      ? Problem(context, PayrollApiErrorMapper.Map(changed.Error))
      : Results.NoContent();
  }

  // ---- PERIODS.

  private static async Task<IResult> GeneratePeriodAsync(
    HttpContext context, GeneratePayrollPeriodCommandHandler handler, CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<GeneratePayrollPeriodRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["companyId"] = [JsonValueKind.String],
        ["name"] = [JsonValueKind.String, JsonValueKind.Null],
        ["anyDateInPeriodUtc"] = [JsonValueKind.String],
        ["payDateUtc"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["companyId", "anyDateInPeriodUtc", "payDateUtc"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var created = await handler.HandleAsync(
      new GeneratePayrollPeriodCommand(
        request.CompanyId, request.Name, request.AnyDateInPeriodUtc, request.PayDateUtc),
      cancellationToken);

    return created.IsFailure
      ? Problem(context, PayrollApiErrorMapper.Map(created.Error))
      : Results.Created($"{RoutePrefix}/periods/{created.Value}", new { payrollPeriodId = created.Value });
  }

  private static async Task<IResult> GetPeriodsAsync(
    HttpContext context, ICurrentCompany company, IPayrollScopeResolver resolver, IPayrollReadService reads,
    CancellationToken cancellationToken)
  {
    var scope = await resolver.ResolveAsync(PayrollPermissionNames.ViewRuns, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, PayrollApiErrorMapper.Map(scope.Error));
    }

    if (company.CompanyId is not { } companyId)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    return Results.Ok(await reads.GetPeriodsAsync(scope.Value, companyId, cancellationToken));
  }

  // ---- RUNS.

  private static async Task<IResult> CreateRunAsync(
    HttpContext context, CreatePayrollRunCommandHandler handler, CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreatePayrollRunRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["companyId"] = [JsonValueKind.String],
        ["payrollPeriodId"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["companyId", "payrollPeriodId"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var created = await handler.HandleAsync(
      new CreatePayrollRunCommand(request.CompanyId, request.PayrollPeriodId), cancellationToken);

    return created.IsFailure
      ? Problem(context, PayrollApiErrorMapper.Map(created.Error))
      : Results.Created($"{RoutePrefix}/runs/{created.Value}", new { payrollRunId = created.Value });
  }

  private static async Task<IResult> GetRunsAsync(
    HttpContext context, ICurrentCompany company, IPayrollScopeResolver resolver, IPayrollReadService reads,
    CancellationToken cancellationToken)
  {
    var scope = await resolver.ResolveAsync(PayrollPermissionNames.ViewRuns, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, PayrollApiErrorMapper.Map(scope.Error));
    }

    if (company.CompanyId is not { } companyId)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    return Results.Ok(await reads.GetRunsAsync(scope.Value, companyId, cancellationToken));
  }

  private static async Task<IResult> GetRunAsync(
    HttpContext context, Guid payrollRunId, IPayrollScopeResolver resolver, IPayrollReadService reads,
    CancellationToken cancellationToken)
  {
    var scope = await resolver.ResolveAsync(PayrollPermissionNames.ViewRuns, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, PayrollApiErrorMapper.Map(scope.Error));
    }

    var run = await reads.GetRunAsync(scope.Value, payrollRunId, cancellationToken);
    return run is null ? Problem(context, PayrollApiErrorMapper.NotFound) : Results.Ok(run);
  }

  // Calculation, approval and posting take NO BODY. Everything each needs is on the run it names, and a body
  // would let a caller change what is being approved at the moment of approval.
  private static async Task<IResult> CalculateRunAsync(
    HttpContext context, Guid payrollRunId, CalculatePayrollRunCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var calculated = await handler.HandleAsync(
      new CalculatePayrollRunCommand(payrollRunId), cancellationToken);

    return calculated.IsFailure
      ? Problem(context, PayrollApiErrorMapper.Map(calculated.Error))
      : Results.NoContent();
  }

  private static async Task<IResult> ApproveRunAsync(
    HttpContext context, Guid payrollRunId, ApprovePayrollRunCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var approved = await handler.HandleAsync(
      new ApprovePayrollRunCommand(payrollRunId), cancellationToken);

    return approved.IsFailure
      ? Problem(context, PayrollApiErrorMapper.Map(approved.Error))
      : Results.NoContent();
  }

  private static async Task<IResult> PostRunAsync(
    HttpContext context, Guid payrollRunId, PostPayrollRunCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var posted = await handler.HandleAsync(new PostPayrollRunCommand(payrollRunId), cancellationToken);

    return posted.IsFailure
      ? Problem(context, PayrollApiErrorMapper.Map(posted.Error))
      : Results.NoContent();
  }

  private static async Task<IResult> ReverseRunAsync(
    HttpContext context, Guid payrollRunId, ReversePayrollRunCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<ReversePayrollRunRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["reversalDateUtc"] = [JsonValueKind.String],
        ["description"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["reversalDateUtc", "description"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var reversed = await handler.HandleAsync(
      new ReversePayrollRunCommand(payrollRunId, request.ReversalDateUtc, request.Description),
      cancellationToken);

    return reversed.IsFailure
      ? Problem(context, PayrollApiErrorMapper.Map(reversed.Error))
      : Results.Created($"/api/gl/journals/{reversed.Value}", new { journalEntryId = reversed.Value });
  }

  // ---- PAYSLIPS.

  private static async Task<IResult> GetPayslipAsync(
    HttpContext context, Guid payrollRunId, Guid employeeId,
    IPayrollScopeResolver resolver, IPayrollReadService reads, CancellationToken cancellationToken)
  {
    var scope = await resolver.ResolveAsync(PayrollPermissionNames.ViewPayslips, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, PayrollApiErrorMapper.Map(scope.Error));
    }

    var payslip = await reads.GetPayslipAsync(scope.Value, payrollRunId, employeeId, cancellationToken);
    return payslip is null ? Problem(context, PayrollApiErrorMapper.NotFound) : Results.Ok(payslip);
  }

  // ---- THE SELF READ. IT REUSES THE ADMINISTRATIVE READ AND DIFFERS ONLY IN WHERE THE EMPLOYEE COMES FROM.
  //
  // `GetPayslipsForEmployeeAsync` is the same method the administrative route calls. The employee is a
  // method ARGUMENT to it, never a member of any contract, which is what lets a self route reuse the read
  // without carrying an identifier a caller could change.
  //
  // The scope comes from `ResolveForOwnEmployeeAsync`, derived from the resolved employee's company rather
  // than the caller's administrative grants — see that method for why.
  private static async Task<IResult> GetOwnPayslipsAsync(
    HttpContext context, IPayrollSelfServiceScopeResolver resolver, IPayrollReadService reads,
    CancellationToken cancellationToken)
  {
    var own = await resolver.ResolveForOwnEmployeeAsync(
      PayrollPermissionNames.ViewOwnPayslips, cancellationToken);

    // An unlinked caller lands here as `Payroll.NoLinkedEmployee` and the mapper answers
    // `404 payroll.no_linked_employee` — an ordinary refusal naming the condition, with nothing thrown and
    // nothing logged (`AC-SS-0008`, `AC-SS-0009`).
    if (own.IsFailure)
    {
      return Problem(context, PayrollApiErrorMapper.Map(own.Error));
    }

    return Results.Ok(await reads.GetPayslipsForEmployeeAsync(
      own.Value.Scope, own.Value.EmployeeId, cancellationToken));
  }

  private static async Task<IResult> GetPayslipsAsync(
    HttpContext context, Guid employeeId, IPayrollScopeResolver resolver, IPayrollReadService reads,
    CancellationToken cancellationToken)
  {
    var scope = await resolver.ResolveAsync(PayrollPermissionNames.ViewPayslips, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, PayrollApiErrorMapper.Map(scope.Error));
    }

    return Results.Ok(await reads.GetPayslipsForEmployeeAsync(scope.Value, employeeId, cancellationToken));
  }
}

// Fifteen lines bound to Payroll's own error mapper and resource key. The genuinely shared part,
// `ICompanyContextEstablisher`, already lives in BuildingBlocks — this is the module-specific half, and GL
// wrote its own for exactly the same reason rather than promoting a filter neither module would agree on.
public sealed class PayrollCompanyContextEndpointFilter(ICompanyContextEstablisher establisher) : IEndpointFilter
{
  private const string ResourceKey = "payroll.errors.request_rejected";

  public async ValueTask<object?> InvokeAsync(
    EndpointFilterInvocationContext context, EndpointFilterDelegate next)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(next);

    var established = await establisher.EstablishAsync(context.HttpContext.RequestAborted);

    return established.IsFailure
      ? ApiProblems.Problem(context.HttpContext, PayrollApiErrorMapper.Map(established.Error), ResourceKey)
      : await next(context);
  }
}
