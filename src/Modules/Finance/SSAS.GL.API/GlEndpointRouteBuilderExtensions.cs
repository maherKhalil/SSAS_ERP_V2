using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.GL.Application.Accounts;
using SSAS.GL.Application.Calendar;
using SSAS.GL.Application.Journals;
using SSAS.GL.Application.Permissions;
using SSAS.GL.Application.Reads;

namespace SSAS.GL.API;

// GL'S HTTP SURFACE (api-contracts.md, DEC-GL-0003, DEC-GL-0004).
//
// ---- STATE CHANGES ARE `POST` TO A SUB-RESOURCE, NEVER `PATCH` ON THE PARENT.
//
// `/deactivation`, `/closure`, `/reopening`, `/posting`, `/reversals`, `/discard` — each is an event with
// its own permission and its own refusals, and for the reversal it is literally the creation of a new
// resource. It also keeps `PATCH` off the journal entirely, where it would suggest a mutation the write
// boundary refuses.
//
// ---- THERE IS NO `DELETE` VERB ANYWHERE, AND ONE ROUTE IS ALLOWED TO DESTROY SOMETHING.
//
// `POST /journal-drafts/{id}/discard` removes a draft, which is the only deletion in this module. It is not
// an exception to `BR-GL-0002`: a draft was never part of the ledger. The named action rather than the verb
// keeps the surface honest about which resources can be destroyed — nothing responds to `DELETE`, so no
// client can assume anything does.
public static class GlEndpointRouteBuilderExtensions
{
  private const string RoutePrefix = "/api/gl";

  private const string ResourceKey = "gl.errors.request_rejected";

  public static IEndpointRouteBuilder MapGlEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    // The gate's dependency, asserted HERE so a host that mounts these routes without it fails at
    // startup rather than answering 500 per request (T-034).
    endpoints.RequireModuleEnablementServices(GlModuleEnablement.Key);

    var group = endpoints.MapGroup(RoutePrefix)
      .WithTags("General Ledger")
      // ---- THE MODULE ENABLEMENT GATE, ON THE GROUP (FP-014, `OD-SUB-0003`).
      //
      // On the GROUP rather than each route, for the same reason the filters below are: a route
      // added later cannot forget it. Entitlement does not differ per operation, so it belongs one
      // level up from `RequirePermission`.
      .RequireModule(GlModuleEnablement.Key)
      .AddEndpointFilter<GlCompanyContextEndpointFilter>()
      .AddEndpointFilter(async (context, next) =>
      {
        ApiResponseSecurity.Apply(context.HttpContext);
        return await next(context);
      });

    // ---- CHART OF ACCOUNTS. Tenant-level (`OD-GL-0003`).
    group.MapPost("/accounts", CreateAccountAsync)
      .RequirePermission(GlPermissionNames.CreateAccounts).WithName("GlAccountsCreate");
    group.MapGet("/accounts", SearchAccountsAsync)
      .RequirePermission(GlPermissionNames.ViewAccounts).WithName("GlAccountsSearch");
    group.MapGet("/accounts/{accountId:guid}", GetAccountAsync)
      .RequirePermission(GlPermissionNames.ViewAccounts).WithName("GlAccountsGet");
    group.MapPut("/accounts/{accountId:guid}", RenameAccountAsync)
      .RequirePermission(GlPermissionNames.UpdateAccounts).WithName("GlAccountsRename");
    group.MapPost("/accounts/{accountId:guid}/deactivation", DeactivateAccountAsync)
      .RequirePermission(GlPermissionNames.DeactivateAccounts).WithName("GlAccountsDeactivate");
    group.MapPost("/accounts/{accountId:guid}/activation", ActivateAccountAsync)
      .RequirePermission(GlPermissionNames.DeactivateAccounts).WithName("GlAccountsActivate");
    group.MapGet("/accounts/{accountId:guid}/balance", GetAccountBalanceAsync)
      .RequirePermission(GlPermissionNames.ViewReports).WithName("GlAccountsBalance");

    // ---- FISCAL CALENDAR. Company-level (`OD-GL-0004`), so these are company-scoped writes.
    group.MapPost("/fiscal-years", DefineFiscalYearAsync)
      .RequirePermission(GlPermissionNames.ManagePeriods).WithName("GlFiscalYearsDefine");
    group.MapGet("/fiscal-periods", GetFiscalPeriodsAsync)
      .RequirePermission(GlPermissionNames.ViewPeriods).WithName("GlFiscalPeriodsList");
    group.MapPost("/fiscal-periods/{fiscalPeriodId:guid}/closure", CloseFiscalPeriodAsync)
      .RequirePermission(GlPermissionNames.ClosePeriods).WithName("GlFiscalPeriodsClose");
    group.MapPost("/fiscal-periods/{fiscalPeriodId:guid}/reopening", ReopenFiscalPeriodAsync)
      .RequirePermission(GlPermissionNames.ClosePeriods).WithName("GlFiscalPeriodsReopen");

    // ---- DRAFTS. The mutable half (`OD-GL-0007`).
    group.MapPost("/journal-drafts", CreateJournalDraftAsync)
      .RequirePermission(GlPermissionNames.ManageDrafts).WithName("GlJournalDraftsCreate");
    group.MapPut("/journal-drafts/{journalDraftId:guid}", UpdateJournalDraftAsync)
      .RequirePermission(GlPermissionNames.ManageDrafts).WithName("GlJournalDraftsUpdate");
    group.MapPost("/journal-drafts/{journalDraftId:guid}/discard", DiscardJournalDraftAsync)
      .RequirePermission(GlPermissionNames.ManageDrafts).WithName("GlJournalDraftsDiscard");

    // Posting is on the DRAFT, because posting is the one-way promotion of that draft into a ledger entry.
    // Placing it under `/journals` would suggest a journal exists before it does.
    group.MapPost("/journal-drafts/{journalDraftId:guid}/posting", PostJournalDraftAsync)
      .RequirePermission(GlPermissionNames.PostJournals).WithName("GlJournalDraftsPost");

    // ================================================================================================
    // READING A DRAFT (T-098). THE HALF THAT WAS MISSING FOR THE WHOLE OF FP-011.
    // ================================================================================================
    //
    // Create, update, discard and post all shipped. **Nothing could read a draft** — the create route above
    // returns a `Location` header and an id for a resource no route could fetch, so a preparer could not
    // see what they were editing and a poster could not see what they were about to post.
    //
    // ---- `GL.Drafts.View`, AND NOTHING IMPLIES IT.
    //
    // The preparer holds `GL.Drafts.Manage`; the reviewer holds `GL.Journals.Post`. **Neither grants this
    // one.** An implied permission makes the explicit one optional and its absence unenforceable —
    // `AC-SS-0005`, and the third time this codebase has refused the shape (T-088, T-089).
    //
    // **Naming a draft by id is not authority to read it**, which is exactly the payslip question T-088
    // answered no. And two grants for two jobs is not friction: the point of a separate `View` is that
    // someone can hold it WITHOUT `Manage` — which is the separation of duties the permission was declared
    // for, in its own words, *"a user who may prepare work for someone else to post"*.
    group.MapGet("/journal-drafts", SearchJournalDraftsAsync)
      .RequirePermission(GlPermissionNames.ViewDrafts).WithName("GlJournalDraftsSearch");
    group.MapGet("/journal-drafts/{journalDraftId:guid}", GetJournalDraftAsync)
      .RequirePermission(GlPermissionNames.ViewDrafts).WithName("GlJournalDraftsGetById");

    // ---- POSTED JOURNALS. Read-only, plus the one route that creates a correction.
    group.MapGet("/journals", SearchJournalsAsync)
      .RequirePermission(GlPermissionNames.ViewJournals).WithName("GlJournalsSearch");
    group.MapGet("/journals/{journalEntryId:guid}", GetJournalAsync)
      .RequirePermission(GlPermissionNames.ViewJournals).WithName("GlJournalsGet");
    group.MapPost("/journals/{journalEntryId:guid}/reversals", ReverseJournalAsync)
      .RequirePermission(GlPermissionNames.ReverseJournals).WithName("GlJournalsReverse");

    // ---- REPORTING.
    group.MapGet("/reports/trial-balance", GetTrialBalanceAsync)
      .RequirePermission(GlPermissionNames.ViewReports).WithName("GlReportsTrialBalance");

    return endpoints;
  }

  // ================================================================================================
  // ACCOUNTS
  // ================================================================================================

  private static async Task<IResult> CreateAccountAsync(
    HttpContext context, CreateAccountCommandHandler handler, CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreateAccountRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["code"] = [JsonValueKind.String],
        ["name"] = [JsonValueKind.String]
      },
      cancellationToken,
      requiredFields: ["code", "name"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var created = await handler.HandleAsync(
      new CreateAccountCommand(request.Code, request.Name), cancellationToken);

    return created.IsFailure
      ? Problem(context, GlApiErrorMapper.Map(created.Error))
      : Results.Created($"{RoutePrefix}/accounts/{created.Value}", new { accountId = created.Value });
  }

  private static async Task<IResult> SearchAccountsAsync(
    HttpContext context, IGlScopeResolver resolver, IGlReadService reads, CancellationToken cancellationToken)
  {
    // The filter allowlist is the contract. An unrecognized query parameter is REFUSED rather than ignored,
    // so a client that misspells `isActive` learns immediately instead of silently receiving every account.
    if (!TryReadFilters(context, ["searchText", "isActive"], out var filters))
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    bool? isActive = null;
    if (filters.TryGetValue("isActive", out var rawActive))
    {
      if (!bool.TryParse(rawActive, out var parsed))
      {
        return Problem(context, ApiErrors.RequestInvalid);
      }

      isActive = parsed;
    }

    var scope = await resolver.ResolveAsync(GlPermissionNames.ViewAccounts, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, GlApiErrorMapper.Map(scope.Error));
    }

    filters.TryGetValue("searchText", out var searchText);
    var accounts = await reads.SearchAccountsAsync(scope.Value, searchText, isActive, cancellationToken);

    return Results.Ok(accounts
      .Select(account => new AccountResponse(account.AccountId, account.Code, account.Name, account.IsActive))
      .ToArray());
  }

  private static async Task<IResult> GetAccountAsync(
    HttpContext context, Guid accountId, IGlScopeResolver resolver, IGlReadService reads,
    CancellationToken cancellationToken)
  {
    var scope = await resolver.ResolveAsync(GlPermissionNames.ViewAccounts, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, GlApiErrorMapper.Map(scope.Error));
    }

    var account = await reads.GetAccountAsync(scope.Value, accountId, cancellationToken);

    return account is null
      ? Problem(context, GlApiErrorMapper.NotFound)
      : Results.Ok(new AccountResponse(account.AccountId, account.Code, account.Name, account.IsActive));
  }

  private static async Task<IResult> RenameAccountAsync(
    HttpContext context, Guid accountId, RenameAccountCommandHandler handler, CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<UpdateAccountRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["name"] = [JsonValueKind.String],
        ["rowVersion"] = [JsonValueKind.String, JsonValueKind.Null]
      },
      cancellationToken,
      requiredFields: ["name"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    var renamed = await handler.HandleAsync(
      new RenameAccountCommand(accountId, request.Name, rowVersion), cancellationToken);

    return renamed.IsFailure
      ? Problem(context, GlApiErrorMapper.Map(renamed.Error))
      : Results.NoContent();
  }

  private static Task<IResult> DeactivateAccountAsync(
    HttpContext context, Guid accountId, SetAccountActivationCommandHandler handler,
    CancellationToken cancellationToken) =>
    SetAccountActivationAsync(context, accountId, isActive: false, handler, cancellationToken);

  private static Task<IResult> ActivateAccountAsync(
    HttpContext context, Guid accountId, SetAccountActivationCommandHandler handler,
    CancellationToken cancellationToken) =>
    SetAccountActivationAsync(context, accountId, isActive: true, handler, cancellationToken);

  private static async Task<IResult> SetAccountActivationAsync(
    HttpContext context, Guid accountId, bool isActive, SetAccountActivationCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<AccountActivationRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["rowVersion"] = [JsonValueKind.String, JsonValueKind.Null]
      },
      cancellationToken,
      requiredFields: []);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    var changed = await handler.HandleAsync(
      new SetAccountActivationCommand(accountId, isActive, rowVersion), cancellationToken);

    return changed.IsFailure
      ? Problem(context, GlApiErrorMapper.Map(changed.Error))
      : Results.NoContent();
  }

  private static async Task<IResult> GetAccountBalanceAsync(
    HttpContext context, Guid accountId, IGlScopeResolver resolver, IGlReadService reads,
    ICurrentTenant currentTenant, ICurrentCompany currentCompany, ITenantCompanyCurrencyLookup currencies,
    CancellationToken cancellationToken)
  {
    if (!TryReadFilters(context, ["fromUtc", "toUtc"], out var filters))
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!TryReadInstant(filters, "fromUtc", out var fromUtc) ||
      !TryReadInstant(filters, "toUtc", out var toUtc))
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var scope = await resolver.ResolveAsync(GlPermissionNames.ViewReports, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, GlApiErrorMapper.Map(scope.Error));
    }

    var balance = await reads.GetAccountBalanceAsync(
      scope.Value, accountId, fromUtc, toUtc, cancellationToken);

    if (balance is null)
    {
      return Problem(context, GlApiErrorMapper.NotFound);
    }

    var currency = await ResolveCurrencyAsync(currentTenant, currentCompany, currencies, cancellationToken);

    return Results.Ok(new AccountBalanceResponse(
      balance.AccountId, balance.Code, balance.Name, currency,
      balance.TotalDebits, balance.TotalCredits, balance.Balance));
  }

  // ================================================================================================
  // FISCAL CALENDAR
  // ================================================================================================

  private static async Task<IResult> DefineFiscalYearAsync(
    HttpContext context, DefineFiscalYearCommandHandler handler, ICurrentCompany currentCompany,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<DefineFiscalYearRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["code"] = [JsonValueKind.String],
        ["startUtc"] = [JsonValueKind.String],
        ["endUtc"] = [JsonValueKind.String],
        ["periods"] = [JsonValueKind.Array]
      },
      cancellationToken,
      requiredFields: ["code", "startUtc", "endUtc", "periods"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (currentCompany.CompanyId is not { } companyId)
    {
      return Problem(context, ApiErrors.Forbidden);
    }

    var defined = await handler.HandleAsync(
      new DefineFiscalYearCommand(
        companyId,
        request.Code,
        request.StartUtc,
        request.EndUtc,
        [.. request.Periods.Select(period =>
          new FiscalPeriodDefinition(period.Name, period.StartUtc, period.EndUtc))]),
      cancellationToken);

    return defined.IsFailure
      ? Problem(context, GlApiErrorMapper.Map(defined.Error))
      : Results.Created($"{RoutePrefix}/fiscal-years/{defined.Value}", new { fiscalYearId = defined.Value });
  }

  private static async Task<IResult> GetFiscalPeriodsAsync(
    HttpContext context, IGlScopeResolver resolver, IGlReadService reads, ICurrentCompany currentCompany,
    CancellationToken cancellationToken)
  {
    var scope = await resolver.ResolveAsync(GlPermissionNames.ViewPeriods, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, GlApiErrorMapper.Map(scope.Error));
    }

    var periods = await reads.GetFiscalPeriodsAsync(scope.Value, currentCompany.CompanyId, cancellationToken);

    return Results.Ok(periods
      .Select(period => new FiscalPeriodResponse(
        period.FiscalPeriodId, period.FiscalYearId, period.FiscalYearCode,
        period.Name, period.StartUtc, period.EndUtc, period.IsOpen))
      .ToArray());
  }

  private static Task<IResult> CloseFiscalPeriodAsync(
    HttpContext context, Guid fiscalPeriodId, SetFiscalPeriodStateCommandHandler handler,
    CancellationToken cancellationToken) =>
    SetFiscalPeriodStateAsync(context, fiscalPeriodId, isOpen: false, handler, cancellationToken);

  private static Task<IResult> ReopenFiscalPeriodAsync(
    HttpContext context, Guid fiscalPeriodId, SetFiscalPeriodStateCommandHandler handler,
    CancellationToken cancellationToken) =>
    SetFiscalPeriodStateAsync(context, fiscalPeriodId, isOpen: true, handler, cancellationToken);

  private static async Task<IResult> SetFiscalPeriodStateAsync(
    HttpContext context, Guid fiscalPeriodId, bool isOpen, SetFiscalPeriodStateCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<FiscalPeriodStateRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["rowVersion"] = [JsonValueKind.String, JsonValueKind.Null]
      },
      cancellationToken,
      requiredFields: []);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    var changed = await handler.HandleAsync(
      new SetFiscalPeriodStateCommand(fiscalPeriodId, isOpen, rowVersion), cancellationToken);

    return changed.IsFailure
      ? Problem(context, GlApiErrorMapper.Map(changed.Error))
      : Results.NoContent();
  }

  // ================================================================================================
  // DRAFTS AND POSTING
  // ================================================================================================

  private static async Task<IResult> CreateJournalDraftAsync(
    HttpContext context, CreateJournalDraftCommandHandler handler, ICurrentCompany currentCompany,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreateJournalDraftRequest>(
      context,
      DraftFieldContract,
      cancellationToken,
      requiredFields: ["entryDateUtc", "description", "lines"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (currentCompany.CompanyId is not { } companyId)
    {
      return Problem(context, ApiErrors.Forbidden);
    }

    var created = await handler.HandleAsync(
      new CreateJournalDraftCommand(
        companyId, request.EntryDateUtc, request.Description, request.Reference,
        [.. request.Lines.Select(ToLineInput)]),
      cancellationToken);

    return created.IsFailure
      ? Problem(context, GlApiErrorMapper.Map(created.Error))
      : Results.Created($"{RoutePrefix}/journal-drafts/{created.Value}", new { journalDraftId = created.Value });
  }

  private static async Task<IResult> UpdateJournalDraftAsync(
    HttpContext context, Guid journalDraftId, UpdateJournalDraftCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<UpdateJournalDraftRequest>(
      context,
      DraftFieldContractWithRowVersion,
      cancellationToken,
      requiredFields: ["entryDateUtc", "description", "lines"]);

    if (request is null)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
    {
      return Problem(context, ApiErrors.RowVersionInvalid);
    }

    var updated = await handler.HandleAsync(
      new UpdateJournalDraftCommand(
        journalDraftId, request.EntryDateUtc, request.Description, request.Reference,
        [.. request.Lines.Select(ToLineInput)], rowVersion),
      cancellationToken);

    return updated.IsFailure
      ? Problem(context, GlApiErrorMapper.Map(updated.Error))
      : Results.NoContent();
  }

  private static async Task<IResult> DiscardJournalDraftAsync(
    HttpContext context, Guid journalDraftId, DiscardJournalDraftCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var discarded = await handler.HandleAsync(
      new DiscardJournalDraftCommand(journalDraftId), cancellationToken);

    return discarded.IsFailure
      ? Problem(context, GlApiErrorMapper.Map(discarded.Error))
      : Results.NoContent();
  }

  // Posting takes NO body: everything it needs is on the draft it names. A body here would let a caller
  // change what is posted at the moment of posting, which is the thing the draft/entry split exists to make
  // impossible.
  private static async Task<IResult> PostJournalDraftAsync(
    HttpContext context, Guid journalDraftId, PostJournalDraftCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var posted = await handler.HandleAsync(
      new PostJournalDraftCommand(journalDraftId), cancellationToken);

    return posted.IsFailure
      ? Problem(context, GlApiErrorMapper.Map(posted.Error))
      : Results.Created($"{RoutePrefix}/journals/{posted.Value}", new { journalEntryId = posted.Value });
  }

  // ================================================================================================
  // POSTED JOURNALS
  // ================================================================================================

  private static async Task<IResult> SearchJournalDraftsAsync(
    HttpContext context, IGlScopeResolver resolver, IGlReadService reads, ICurrentTenant currentTenant,
    ICurrentCompany currentCompany, ITenantCompanyCurrencyLookup currencies, CancellationToken cancellationToken)
  {
    if (!TryReadFilters(context, ["fromUtc", "toUtc", "reference"], out var filters))
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!TryReadInstant(filters, "fromUtc", out var fromUtc) ||
      !TryReadInstant(filters, "toUtc", out var toUtc))
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var scope = await resolver.ResolveAsync(GlPermissionNames.ViewDrafts, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, GlApiErrorMapper.Map(scope.Error));
    }

    filters.TryGetValue("reference", out var reference);
    var drafts = await reads.SearchJournalDraftsAsync(
      scope.Value, currentCompany.CompanyId, fromUtc, toUtc, reference, cancellationToken);

    var currency = await ResolveCurrencyAsync(currentTenant, currentCompany, currencies, cancellationToken);

    return Results.Ok(drafts
      .Select(draft => new JournalDraftSummaryResponse(
        draft.JournalDraftId, draft.CompanyId, draft.EntryDateUtc,
        draft.Description, draft.Reference, currency, draft.TotalDebits))
      .ToArray());
  }

  private static async Task<IResult> GetJournalDraftAsync(
    HttpContext context, Guid journalDraftId, IGlScopeResolver resolver, IGlReadService reads,
    ICurrentTenant currentTenant, ICurrentCompany currentCompany, ITenantCompanyCurrencyLookup currencies,
    CancellationToken cancellationToken)
  {
    var scope = await resolver.ResolveAsync(GlPermissionNames.ViewDrafts, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, GlApiErrorMapper.Map(scope.Error));
    }

    var draft = await reads.GetJournalDraftAsync(scope.Value, journalDraftId, cancellationToken);
    if (draft is null)
    {
      return Problem(context, GlApiErrorMapper.NotFound);
    }

    var currency = await ResolveCurrencyAsync(currentTenant, currentCompany, currencies, cancellationToken);

    return Results.Ok(new JournalDraftResponse(
      draft.JournalDraftId, draft.CompanyId, draft.EntryDateUtc, draft.Description, draft.Reference, currency,
      [.. draft.Lines.Select(line => new JournalLineResponse(
        line.LineNumber, line.AccountId, line.AccountCode, line.AccountName,
        line.Debit, line.Credit, line.Description))]));
  }

  private static async Task<IResult> SearchJournalsAsync(
    HttpContext context, IGlScopeResolver resolver, IGlReadService reads, ICurrentTenant currentTenant, ICurrentCompany currentCompany,
    ITenantCompanyCurrencyLookup currencies, CancellationToken cancellationToken)
  {
    if (!TryReadFilters(context, ["fromUtc", "toUtc", "reference"], out var filters))
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (!TryReadInstant(filters, "fromUtc", out var fromUtc) ||
      !TryReadInstant(filters, "toUtc", out var toUtc))
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    var scope = await resolver.ResolveAsync(GlPermissionNames.ViewJournals, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, GlApiErrorMapper.Map(scope.Error));
    }

    filters.TryGetValue("reference", out var reference);
    var journals = await reads.SearchJournalsAsync(
      scope.Value, currentCompany.CompanyId, fromUtc, toUtc, reference, cancellationToken);

    var currency = await ResolveCurrencyAsync(currentTenant, currentCompany, currencies, cancellationToken);

    return Results.Ok(journals
      .Select(journal => new JournalSummaryResponse(
        journal.JournalEntryId, journal.CompanyId, journal.JournalNumber, journal.EntryDateUtc,
        journal.Description, journal.Reference, currency, journal.TotalDebits,
        journal.ReversesJournalEntryId, journal.IsReversed))
      .ToArray());
  }

  private static async Task<IResult> GetJournalAsync(
    HttpContext context, Guid journalEntryId, IGlScopeResolver resolver, IGlReadService reads,
    ICurrentTenant currentTenant, ICurrentCompany currentCompany, ITenantCompanyCurrencyLookup currencies,
    CancellationToken cancellationToken)
  {
    var scope = await resolver.ResolveAsync(GlPermissionNames.ViewJournals, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, GlApiErrorMapper.Map(scope.Error));
    }

    var journal = await reads.GetJournalAsync(scope.Value, journalEntryId, cancellationToken);
    if (journal is null)
    {
      return Problem(context, GlApiErrorMapper.NotFound);
    }

    var currency = await ResolveCurrencyAsync(currentTenant, currentCompany, currencies, cancellationToken);

    return Results.Ok(new JournalResponse(
      journal.JournalEntryId, journal.CompanyId, journal.JournalNumber, journal.EntryDateUtc,
      journal.Description, journal.Reference, currency, journal.ReversesJournalEntryId, journal.IsReversed,
      [.. journal.Lines.Select(line => new JournalLineResponse(
        line.LineNumber, line.AccountId, line.AccountCode, line.AccountName,
        line.Debit, line.Credit, line.Description))]));
  }

  private static async Task<IResult> ReverseJournalAsync(
    HttpContext context, Guid journalEntryId, ReverseJournalCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var request = await StrictRequestReader.ReadStrictJsonAsync<ReverseJournalRequest>(
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
      new ReverseJournalCommand(journalEntryId, request.ReversalDateUtc, request.Description),
      cancellationToken);

    return reversed.IsFailure
      ? Problem(context, GlApiErrorMapper.Map(reversed.Error))
      : Results.Created($"{RoutePrefix}/journals/{reversed.Value}", new { journalEntryId = reversed.Value });
  }

  // ================================================================================================
  // REPORTING
  // ================================================================================================

  private static async Task<IResult> GetTrialBalanceAsync(
    HttpContext context, IGlScopeResolver resolver, IGlReadService reads, ICurrentTenant currentTenant, ICurrentCompany currentCompany,
    ITenantCompanyCurrencyLookup currencies, CancellationToken cancellationToken)
  {
    if (!TryReadFilters(context, ["fromUtc", "toUtc"], out var filters))
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    // The window is REQUIRED here, unlike the balance enquiry. A trial balance without one would compute
    // over all time, which is a materially different and far more expensive report than anyone asking for
    // "the trial balance" means.
    if (!TryReadInstant(filters, "fromUtc", out var fromUtc) ||
      !TryReadInstant(filters, "toUtc", out var toUtc) ||
      fromUtc is not { } from || toUtc is not { } to)
    {
      return Problem(context, ApiErrors.RequestInvalid);
    }

    if (currentCompany.CompanyId is not { } companyId)
    {
      return Problem(context, ApiErrors.Forbidden);
    }

    var scope = await resolver.ResolveAsync(GlPermissionNames.ViewReports, cancellationToken);
    if (scope.IsFailure)
    {
      return Problem(context, GlApiErrorMapper.Map(scope.Error));
    }

    var trialBalance = await reads.GetTrialBalanceAsync(scope.Value, companyId, from, to, cancellationToken);
    var currency = await ResolveCurrencyAsync(currentTenant, currentCompany, currencies, cancellationToken);

    return Results.Ok(new TrialBalanceResponse(
      companyId, from, to, currency,
      trialBalance.TotalDebits, trialBalance.TotalCredits, trialBalance.Balances,
      [.. trialBalance.Rows.Select(row => new TrialBalanceRowResponse(
        row.AccountId, row.Code, row.Name, row.TotalDebits, row.TotalCredits))]));
  }

  // ================================================================================================
  // SHARED
  // ================================================================================================

  private static readonly Dictionary<string, JsonValueKind[]> DraftFieldContract = new()
  {
    ["entryDateUtc"] = [JsonValueKind.String],
    ["description"] = [JsonValueKind.String],
    ["reference"] = [JsonValueKind.String, JsonValueKind.Null],
    ["lines"] = [JsonValueKind.Array]
  };

  private static readonly Dictionary<string, JsonValueKind[]> DraftFieldContractWithRowVersion = new()
  {
    ["entryDateUtc"] = [JsonValueKind.String],
    ["description"] = [JsonValueKind.String],
    ["reference"] = [JsonValueKind.String, JsonValueKind.Null],
    ["lines"] = [JsonValueKind.Array],
    ["rowVersion"] = [JsonValueKind.String, JsonValueKind.Null]
  };

  private static JournalLineInput ToLineInput(JournalLineRequest line) =>
    new(line.AccountId, line.Debit, line.Credit, line.Description);

  // ---- THE CURRENCY IS PROJECTED, NEVER STORED AND NEVER ACCEPTED (ADR-027 decision 2).
  //
  // Read from the owning company at response time. A representation that showed an amount without a
  // currency would be unreadable, and one that accepted a currency on write would create a second source of
  // truth for a fact the Company already owns.
  private static async Task<string> ResolveCurrencyAsync(
    ICurrentTenant currentTenant,
    ICurrentCompany currentCompany,
    ITenantCompanyCurrencyLookup currencies,
    CancellationToken cancellationToken)
  {
    if (currentTenant.TenantId is not { } tenantId || currentCompany.CompanyId is not { } companyId)
    {
      return string.Empty;
    }

    return await currencies.FindBaseCurrencyCodeAsync(tenantId, companyId, cancellationToken) ?? string.Empty;
  }

  // An unrecognized query parameter is REFUSED, not ignored — the allowlist convention FP-009 established.
  // A client that misspells a filter learns immediately rather than receiving a silently unfiltered result.
  private static bool TryReadFilters(
    HttpContext context, string[] allowed, out Dictionary<string, string> filters)
  {
    filters = new Dictionary<string, string>(StringComparer.Ordinal);

    foreach (var pair in context.Request.Query)
    {
      if (!allowed.Contains(pair.Key, StringComparer.Ordinal))
      {
        return false;
      }

      var value = pair.Value.ToString();
      if (!string.IsNullOrWhiteSpace(value))
      {
        filters[pair.Key] = value;
      }
    }

    return true;
  }

  private static bool TryReadInstant(
    Dictionary<string, string> filters, string key, out DateTimeOffset? instant)
  {
    instant = null;

    if (!filters.TryGetValue(key, out var raw))
    {
      return true;
    }

    if (!DateTimeOffset.TryParse(
      raw, System.Globalization.CultureInfo.InvariantCulture,
      System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
    {
      return false;
    }

    instant = parsed.ToUniversalTime();
    return true;
  }

  // A malformed row version is a 400 with its own code rather than a generic request error: the caller sent
  // something that was not the token they were given, and telling them which field is wrong is the whole
  // value of a distinct code.
  private static bool TryDecodeRowVersion(string? encoded, out byte[]? rowVersion)
  {
    rowVersion = null;

    if (string.IsNullOrWhiteSpace(encoded))
    {
      return true;
    }

    try
    {
      rowVersion = Convert.FromBase64String(encoded);
      return true;
    }
    catch (FormatException)
    {
      return false;
    }
  }

  private static IResult Problem(HttpContext context, ApiError error) =>
    ApiProblems.Problem(context, error, ResourceKey);
}

// GL'S COMPANY-CONTEXT FILTER.
//
// A copy of HR's in shape, and it has to be: `ADR-012` forbids GL referencing `SSAS.HR.API`, and the filter
// is bound to its module's error mapper and resource key. The SHARED part — `ICompanyContextEstablisher` —
// already lives in BuildingBlocks, which is where the logic that could meaningfully be shared went. What
// remains here is the module-specific wiring, and duplicating fifteen lines of it is cheaper than promoting
// a type that would have to know about both modules' error mappers.
public sealed class GlCompanyContextEndpointFilter(ICompanyContextEstablisher establisher) : IEndpointFilter
{
  private const string ResourceKey = "gl.errors.request_rejected";

  public async ValueTask<object?> InvokeAsync(
    EndpointFilterInvocationContext context, EndpointFilterDelegate next)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(next);

    var established = await establisher.EstablishAsync(context.HttpContext.RequestAborted);

    return established.IsFailure
      ? ApiProblems.Problem(context.HttpContext, GlApiErrorMapper.Map(established.Error), ResourceKey)
      : await next(context);
  }
}
