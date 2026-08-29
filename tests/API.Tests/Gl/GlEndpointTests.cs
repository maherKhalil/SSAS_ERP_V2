using System.Net;
using SSAS.GL.Application.Permissions;
using SSAS.GL.Application.Reads;
using SSAS.GL.Domain.Accounts;
using SSAS.GL.Domain.Journals;

namespace SSAS.API.Tests.Gl;

// GL'S TRANSPORT BEHAVIOUR.
//
// What these prove: routing, authentication, permission enforcement, strict reading, status and error
// codes, and that a read cannot be reached without a scope.
//
// What they deliberately do NOT prove: anything the write boundary or the database enforces. The
// append-only guarantee, the posting transaction and the concurrency token are asserted in
// `Integration.Tests` against real SQL, because this harness's unit of work is a stub and would report
// success for code that could never work.
[Collection(GlApiEndpointGroup.Name)]
public sealed class GlEndpointTests : IClassFixture<GlApiTestHost>
{
  private readonly GlApiTestHost host;

  public GlEndpointTests(GlApiTestHost host)
  {
    this.host = host;
    host.ResetToAuthorizedState();
  }

  // ================================================================================================
  // AUTHENTICATION AND AUTHORIZATION
  // ================================================================================================

  [Theory]
  [InlineData("GET", "/api/gl/accounts")]
  [InlineData("POST", "/api/gl/accounts")]
  [InlineData("GET", "/api/gl/journals")]
  [InlineData("GET", "/api/gl/fiscal-periods")]
  [InlineData("GET", "/api/gl/reports/trial-balance")]
  public async Task An_unauthenticated_request_is_refused(string method, string path)
  {
    var response = await host.Client.SendAsync(
      GlApiTestHost.Request(new HttpMethod(method), path, token: null));

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task A_caller_without_the_permission_is_refused_even_holding_every_other_one()
  {
    // Holding twelve of thirteen permissions must not open the thirteenth door. This is the check that
    // catches a route wired to the wrong constant — a mistake no unit test sees, because the handler is
    // correct and only the route is wrong.
    var everythingElse = new[]
    {
      GlPermissionNames.ViewAccounts, GlPermissionNames.CreateAccounts, GlPermissionNames.UpdateAccounts,
      GlPermissionNames.DeactivateAccounts, GlPermissionNames.ViewPeriods, GlPermissionNames.ManagePeriods,
      GlPermissionNames.ClosePeriods, GlPermissionNames.ViewDrafts, GlPermissionNames.ManageDrafts,
      GlPermissionNames.ViewJournals, GlPermissionNames.ReverseJournals, GlPermissionNames.ViewReports
    };

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post,
      $"/api/gl/journal-drafts/{GlApiTestHost.DraftId}/posting",
      host.TokenWith(everythingElse)));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  [Trait("Decision", "AC-GL-0017")]
  public async Task A_tenant_administrator_holding_no_gl_permission_reads_nothing()
  {
    // `ADR-025` decision 8: Platform.Tenant.Administer widens SCOPE and grants no FUNCTIONAL authority.
    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Get, "/api/gl/journals", host.TokenWith("Platform.Tenant.Administer")));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ================================================================================================
  // THE SCOPE IS REACHED, AND IT CARRIES WHAT THE CALLER IS AUTHORIZED FOR
  // ================================================================================================

  [Fact]
  [Trait("Decision", "DEC-GL-0004")]
  public async Task A_read_passes_the_resolved_scope_and_not_the_requested_company()
  {
    host.CompanyAccess.Permitted = [GlApiTestHost.CompanyA];

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Get, "/api/gl/journals", host.TokenWith(GlPermissionNames.ViewJournals)));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    // The scope the read service actually received carries the AUTHORIZED set. A route that passed the
    // caller's requested company instead would let a caller reach a company by naming it, and the response
    // would look identical in every other respect.
    var scope = Assert.Single(host.Reads.ObservedScopes);
    Assert.Equal([GlApiTestHost.CompanyA], scope.CompanyIds);
    Assert.Equal(GlApiTestHost.TenantId, scope.TenantId);
  }

  [Fact]
  [Trait("Decision", "AC-GL-0014")]
  public async Task A_caller_with_no_authorized_company_is_refused_rather_than_served_an_empty_page()
  {
    // An empty page claims something about the DATA; a refusal claims something about the CALLER. Only the
    // second is true, and only the second stays true when someone later grants them a company.
    host.CompanyAccess.Permitted = [];

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Get, "/api/gl/journals", host.TokenWith(GlPermissionNames.ViewJournals)));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal("company.scope_denied", await GlApiTestHost.ProblemCodeAsync(response));
    Assert.Empty(host.Reads.ObservedScopes);
  }

  // ================================================================================================
  // STRICT READING
  // ================================================================================================

  [Fact]
  [Trait("Decision", "TS-GL-0026")]
  public async Task An_unknown_property_is_refused_rather_than_ignored()
  {
    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, "/api/gl/accounts", host.TokenWith(GlPermissionNames.CreateAccounts),
      """{"code":"4100","name":"Receivables","surprise":true}"""));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await GlApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  [Trait("Decision", "TS-GL-0027")]
  public async Task A_request_supplying_a_currency_is_refused_as_an_unknown_property()
  {
    // `OD-GL-0002` ruled single currency and `ADR-027` decision 2 projects it on READ. The contract has no
    // currency field, so this is the strict reader doing its ordinary job rather than a special case.
    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, "/api/gl/journal-drafts", host.TokenWith(GlPermissionNames.ManageDrafts),
      $$"""
      {"entryDateUtc":"2026-05-31T00:00:00Z","description":"D","reference":null,
       "currencyCode":"SAR","lines":[]}
      """));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await GlApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  [Trait("Decision", "AC-GL-0002")]
  public async Task A_request_naming_a_fiscal_period_is_refused_because_the_contract_has_no_such_field()
  {
    // A caller who could name the period could post into one the entry date does not belong to, which would
    // make `BR-GL-0003` unenforceable by inspection. The field does not exist, so the idea is unexpressible.
    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, "/api/gl/journal-drafts", host.TokenWith(GlPermissionNames.ManageDrafts),
      $$"""
      {"entryDateUtc":"2026-05-31T00:00:00Z","description":"D","reference":null,
       "fiscalPeriodId":"{{GlApiTestHost.PeriodId}}","lines":[]}
      """));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task A_missing_required_field_is_refused()
  {
    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, "/api/gl/accounts", host.TokenWith(GlPermissionNames.CreateAccounts),
      """{"code":"4100"}"""));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  [Trait("Decision", "TS-GL-0029")]
  public async Task An_unrecognized_query_filter_is_refused_rather_than_ignored()
  {
    // A client that misspells a filter must learn immediately. Ignoring it returns a silently unfiltered
    // result, which for a ledger search means showing more than was asked for.
    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Get, "/api/gl/accounts?isActve=true", host.TokenWith(GlPermissionNames.ViewAccounts)));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task A_malformed_row_version_has_its_own_code()
  {
    host.Accounts.Accounts[GlApiTestHost.AccountId] = Account.Create("4100", "Receivables").Value;

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Put, $"/api/gl/accounts/{GlApiTestHost.AccountId}",
      host.TokenWith(GlPermissionNames.UpdateAccounts),
      """{"name":"Renamed","rowVersion":"not-base64!!"}"""));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("platform.rowversion_invalid", await GlApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // ERROR MAPPING
  // ================================================================================================

  [Fact]
  [Trait("Decision", "BR-GL-0001")]
  public async Task An_unbalanced_journal_is_422_and_not_400()
  {
    // Well-formed, and refused by a RULE ABOUT THE CONTENT — which is what 422 means. A client
    // distinguishing "malformed JSON" from "your journal does not balance" needs the two to differ, because
    // only the second can be shown to a user and corrected.
    var draft = JournalDraft.Create(DateTimeOffset.UtcNow, "Unbalanced", null).Value;
    draft.CompanyId = GlApiTestHost.CompanyA;
    draft.ReplaceLines(
      [(Guid.NewGuid(), 100m, 0m, null), (Guid.NewGuid(), 0m, 99m, null)]);
    host.Drafts.Drafts[draft.Id] = draft;

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, $"/api/gl/journal-drafts/{draft.Id}/posting",
      host.TokenWith(GlPermissionNames.PostJournals)));

    Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    Assert.Equal("gl.journal_unbalanced", await GlApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task A_missing_account_is_404()
  {
    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Get, $"/api/gl/accounts/{Guid.NewGuid()}",
      host.TokenWith(GlPermissionNames.ViewAccounts)));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("gl.not_found", await GlApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  [Trait("Decision", "api-contracts.md")]
  public async Task An_account_outside_the_callers_scope_is_reported_as_absent_and_not_as_forbidden()
  {
    // Deliberately indistinguishable from "no such account". Reporting 403 would let a caller enumerate the
    // chart one probe at a time — ask for an identifier, read the status, learn whether it exists.
    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Get, $"/api/gl/accounts/{GlApiTestHost.AccountId}",
      host.TokenWith(GlPermissionNames.ViewAccounts)));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task A_duplicate_account_code_is_409()
  {
    host.Accounts.CodeTaken = true;

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, "/api/gl/accounts", host.TokenWith(GlPermissionNames.CreateAccounts),
      """{"code":"4100","name":"Receivables"}"""));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("gl.conflict", await GlApiTestHost.ProblemCodeAsync(response));
  }

  // ---- THE JOURNAL-NUMBER RACE ANSWERS 409, NOT 500 (T-165).
  //
  // `NextJournalNumberAsync` is a read-then-write and `UX_GlJournalEntries_Tenant_Company_Year_Number`
  // decides the race at commit. **The loser used to answer 500**: the unit of work returns the generic
  // `Persistence.UniqueConstraint`, `GlApiErrorMapper` has no arm for it, and the default is
  // `WriteFailure` — while `Gl.JournalNumberConflict`, mapped to 409, was returned by nothing.
  //
  // ⚠ **This asserts the STATUS AND THE CODE, and the code is the load-bearing half.** A 409 alone would
  // also be produced by an inactive account or an already-reversed journal; only `gl.conflict` arriving
  // from `JournalErrors.NumberConflict` says the translation happened.
  // ---- THE OTHER TWO GL UNIQUENESS RACES (T-177), AND THEY ARE A DIFFERENT SHAPE FROM THE JOURNAL NUMBER.
  //
  // A lost journal-number race is satisfied by retrying — the retry allocates a new number. **These two are
  // not**: the race and the pre-check produce the same condition, so retrying the identical request fails
  // again and the caller must change the code. Same 409, different instruction.
  [Fact]
  [Trait("Decision", "DEC-DEP-0027")]
  public async Task A_duplicate_account_code_race_is_409_rather_than_500()
  {
    host.UnitOfWork.Failure = new SSAS.BuildingBlocks.Domain.Error(
      "Persistence.UniqueConstraint", "Unique index violated.");

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, "/api/gl/accounts", host.TokenWith(GlPermissionNames.CreateAccounts),
      """{"code":"4100","name":"Receivables"}"""));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("gl.conflict", await GlApiTestHost.ProblemCodeAsync(response));
  }

  // ⚠ The fiscal-year translation names the CODE race only. The OVERLAP race has no index behind it
  // (`DEC-L-084`) and is unchanged by this — `GlFiscalYearOverlapChainSqlServerTests` is what covers the
  // guard that remains its only enforcement.
  // ---- LOSING THE CALENDAR LOCK IS A 409 AND IT IS THE ONE THAT IS WORTH RETRYING (T-184).
  //
  // `Gl.FiscalCalendarBusy` is transient: the caller is not wrong and nothing about the request needs
  // changing. **That is the opposite of the two other 409s on this route** — a duplicate code and an
  // overlapping range both mean the input must change, and repeating the request cannot help.
  //
  // Same status, three different instructions, which is why the CODE is asserted and not just the status.
  [Fact]
  [Trait("Decision", "DEC-L-084")]
  public async Task A_busy_fiscal_calendar_is_409_and_names_a_retryable_condition()
  {
    host.CalendarLock.Failure = SSAS.GL.Domain.Calendar.CalendarErrors.CalendarDefinitionBusy;

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, "/api/gl/fiscal-years", host.TokenWith(GlPermissionNames.ManagePeriods),
      """{"code":"FY2026","startUtc":"2026-01-01T00:00:00Z","endUtc":"2027-01-01T00:00:00Z","periods":[{"name":"P1","startUtc":"2026-01-01T00:00:00Z","endUtc":"2027-01-01T00:00:00Z"}]}"""));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("gl.conflict", await GlApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  [Trait("Decision", "DEC-DEP-0027")]
  public async Task A_duplicate_fiscal_year_code_race_is_409_rather_than_500()
  {
    host.UnitOfWork.Failure = new SSAS.BuildingBlocks.Domain.Error(
      "Persistence.UniqueConstraint", "Unique index violated.");

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, "/api/gl/fiscal-years", host.TokenWith(GlPermissionNames.ManagePeriods),
      """{"code":"FY2026","startUtc":"2026-01-01T00:00:00Z","endUtc":"2027-01-01T00:00:00Z","periods":[{"name":"P1","startUtc":"2026-01-01T00:00:00Z","endUtc":"2027-01-01T00:00:00Z"}]}"""));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("gl.conflict", await GlApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  [Trait("Decision", "DEC-DEP-0027")]
  public async Task A_duplicate_journal_number_is_409_rather_than_500()
  {
    var debit = Account.Create("5300", "Rent").Value;
    host.Accounts.Accounts[debit.Id] = debit;

    var credit = Account.Create("1100", "Bank").Value;
    host.Accounts.Accounts[credit.Id] = credit;

    var draft = JournalDraft.Create(DateTimeOffset.UtcNow, "Racing", null).Value;
    draft.CompanyId = GlApiTestHost.CompanyA;
    draft.ReplaceLines([(debit.Id, 100m, 0m, null), (credit.Id, 0m, 100m, null)]);
    host.Drafts.Drafts[draft.Id] = draft;

    var year = SSAS.GL.Domain.Calendar.FiscalYear.Create(
      "FY2026",
      new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
      new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
      [("FY", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero))]).Value;
    year.CompanyId = GlApiTestHost.CompanyA;
    host.Calendar.Years[year.Id] = year;

    // What SQL Server 2601/2627 becomes by the time it reaches this handler.
    host.UnitOfWork.Failure = new SSAS.BuildingBlocks.Domain.Error("Persistence.UniqueConstraint", "Unique index violated.");

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, $"/api/gl/journal-drafts/{draft.Id}/posting",
      host.TokenWith(GlPermissionNames.PostJournals)));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("gl.conflict", await GlApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  [Trait("Decision", "BR-GL-0004")]
  public async Task Posting_to_an_inactive_account_is_409_and_the_error_names_the_account()
  {
    var inactive = Account.Create("5200", "Office Supplies").Value;
    inactive.Deactivate();
    host.Accounts.Accounts[inactive.Id] = inactive;

    var other = Account.Create("1000", "Cash").Value;
    host.Accounts.Accounts[other.Id] = other;

    var draft = JournalDraft.Create(DateTimeOffset.UtcNow, "Posting", null).Value;
    draft.CompanyId = GlApiTestHost.CompanyA;
    draft.ReplaceLines([(inactive.Id, 100m, 0m, null), (other.Id, 0m, 100m, null)]);
    host.Drafts.Drafts[draft.Id] = draft;

    var year = SSAS.GL.Domain.Calendar.FiscalYear.Create(
      "FY2026",
      new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
      new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
      [("FY", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero))]).Value;
    year.CompanyId = GlApiTestHost.CompanyA;
    host.Calendar.Years[year.Id] = year;

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, $"/api/gl/journal-drafts/{draft.Id}/posting",
      host.TokenWith(GlPermissionNames.PostJournals)));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("gl.account_inactive", await GlApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // RESPONSES
  // ================================================================================================

  [Fact]
  [Trait("Decision", "ADR-027")]
  public async Task A_journal_response_projects_the_company_currency_it_never_stored()
  {
    host.Reads.Journal = new JournalDetail(
      GlApiTestHost.JournalId, GlApiTestHost.CompanyA, "1",
      new DateTimeOffset(2026, 5, 31, 0, 0, 0, TimeSpan.Zero),
      "Opening", "REF", null, false,
      [new JournalLineDetail(1, GlApiTestHost.AccountId, "4100", "Receivables", 100m, 0m, null)]);

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Get, $"/api/gl/journals/{GlApiTestHost.JournalId}",
      host.TokenWith(GlPermissionNames.ViewJournals)));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = await GlApiTestHost.DocumentAsync(response);

    // Present on the way OUT and refused on the way IN — the two halves of ADR-027 decision 2.
    Assert.True(document.RootElement.TryGetProperty("currencyCode", out _));
  }

  [Fact]
  [Trait("Decision", "AC-GL-0016")]
  public async Task A_trial_balance_reports_whether_it_balances()
  {
    host.Reads.TrialBalance = new TrialBalance(
    [
      new TrialBalanceRow(Guid.NewGuid(), "1000", "Cash", 500m, 0m),
      new TrialBalanceRow(Guid.NewGuid(), "4100", "Receivables", 0m, 500m)
    ]);

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Get,
      "/api/gl/reports/trial-balance?fromUtc=2026-01-01T00:00:00Z&toUtc=2027-01-01T00:00:00Z",
      host.TokenWith(GlPermissionNames.ViewReports)));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = await GlApiTestHost.DocumentAsync(response);
    var root = document.RootElement;

    Assert.True(root.GetProperty("balances").GetBoolean());
    Assert.Equal(500m, root.GetProperty("totalDebits").GetDecimal());
    Assert.Equal(500m, root.GetProperty("totalCredits").GetDecimal());
  }

  [Fact]
  public async Task A_trial_balance_without_a_window_is_refused()
  {
    // Required here, unlike the balance enquiry: a trial balance over all time is a materially different
    // and far more expensive report than anyone asking for "the trial balance" means.
    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Get, "/api/gl/reports/trial-balance", host.TokenWith(GlPermissionNames.ViewReports)));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }
}
