using System.Net;
using SSAS.GL.Domain.Accounts;
using SSAS.GL.Application.Permissions;

namespace SSAS.API.Tests.Gl;

// =================================================================================================
// GL'S CORRECTION PATHS, OVER HTTP (T-205). THE SEVEN ROUTES NOTHING WAS CALLING.
// =================================================================================================
//
// ---- ⚠ CORRECTION PATHS FIRST, AND THE ORDERING IS A PRIOR RATHER THAN A PREFERENCE.
//
// Both live 500s found on 2026-08-29 were on correction paths: reversing an ALREADY-reversed payroll run,
// and a leave submission that lost a race. **Neither is a first attempt.** A developer proves the happy
// path by using it once; nobody does the thing twice, so the second-time paths are the ones no request has
// ever taken. Reversal, period reopening and account reactivation are exactly that shape in the ledger.
//
// Two for two is not proof. It is a prior worth ordering by.
//
// ---- WHAT THESE PROVE. THE PERMISSION PAIRINGS ARE THE SHARP PART.
//
// `GlRoutePermissionTests` asserts every route requires SOME permission by enumerating the route table. It
// issues no request, so it cannot tell `GL.Periods.Close` from `GL.Periods.Manage` — and those are
// different authorities: defining next year's calendar is not the same as sealing last month's ledger.
//
// **The one worth reading twice is the account balance**, which is gated on `GL.Reports.View` rather than
// `GL.Accounts.View`. A balance is a report about money, not a property of the account record, and a caller
// who may list the chart of accounts is not thereby entitled to what is in them.
public sealed class GlCorrectionPathEndpointTests(GlApiTestHost host) : IClassFixture<GlApiTestHost>
{
  [Fact]
  public async Task Reversing_a_journal_needs_the_reverse_permission_not_merely_draft_management()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, $"/api/gl/journals/{Guid.NewGuid()}/reversals",
      host.TokenWith(GlPermissionNames.ManageDrafts),
      """{"reversalDateUtc":"2026-06-15T00:00:00Z","description":"Correction"}"""));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Reversing_an_unknown_journal_is_a_not_found_rather_than_a_server_error()
  {
    host.ResetToAuthorizedState();
    host.Journals.Entries.Clear();

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, $"/api/gl/journals/{Guid.NewGuid()}/reversals",
      host.TokenWith(GlPermissionNames.ReverseJournals),
      """{"reversalDateUtc":"2026-06-15T00:00:00Z","description":"Correction"}"""));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("gl.not_found", await GlApiTestHost.ProblemCodeAsync(response));
  }

  // ⚠ CLOSING AND REOPENING ARE THE SAME AUTHORITY AND IT IS NOT THE ONE THAT DEFINES THE CALENDAR.
  // `GL.Periods.Manage` defines fiscal years. `GL.Periods.Close` seals and unseals them. An accountant who
  // may lay out next year must not be able to reopen a month that has been reported on.
  [Fact]
  public async Task Closing_a_period_needs_the_close_permission_not_merely_period_management()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, $"/api/gl/fiscal-periods/{Guid.NewGuid()}/closure",
      host.TokenWith(GlPermissionNames.ManagePeriods), "{}"));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Reopening_a_period_is_the_same_authority_as_closing_one()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, $"/api/gl/fiscal-periods/{Guid.NewGuid()}/reopening",
      host.TokenWith(GlPermissionNames.ManagePeriods), "{}"));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Reopening_an_unknown_period_is_a_not_found()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, $"/api/gl/fiscal-periods/{Guid.NewGuid()}/reopening",
      host.TokenWith(GlPermissionNames.ClosePeriods), "{}"));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("gl.not_found", await GlApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // ⚠ THE CORRECTION PATH WAS TESTED AND THE ACT IT CORRECTS WAS NOT (T-240).
  // ================================================================================================
  //
  // `POST /accounts/{}/deactivation` was one of five live routes addressed by no test, while
  // `/accounts/{}/activation` directly below has been covered since this class was written. **The undo was
  // guarded and the do was not** — which is the inversion you would least expect to find and the easiest to
  // produce, because the correction path is the one that feels risky.
  //
  // **BOTH ROUTES REACH THE SAME HANDLER.** `DeactivateAccountAsync` and `ActivateAccountAsync` are one-line
  // forwards to `SetAccountActivationAsync`, differing only by `isActive`. So everything the activation test
  // covers — reading, permission, concurrency token, save — was already covered for this route too, and
  // **the single uncovered thing was the one bit that differs.** That is what this asserts: the account
  // comes back INACTIVE rather than active.
  //
  // A status-only test would have proved nothing here at all: a `/deactivation` wired to `isActive: true`
  // returns exactly the same 204.
  [Fact]
  public async Task Deactivating_an_account_leaves_it_inactive_rather_than_active()
  {
    host.ResetToAuthorizedState();

    var account = Account.Create("4900", "Suspense").Value;
    Assert.True(account.IsActive, "the fixture must start active or this proves nothing");
    host.Accounts.Accounts[account.Id] = account;

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, $"/api/gl/accounts/{account.Id}/deactivation",
      host.TokenWith(GlPermissionNames.DeactivateAccounts), "{}"));

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.False(account.IsActive,
      "the deactivation route returned success while leaving the account active, so it is wired to the " +
      "activation branch of the shared handler.");
  }

  // The permission pairing, asserted for the act as it already is for the correction.
  [Fact]
  public async Task Deactivating_an_account_needs_the_deactivate_permission_not_the_update_one()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, $"/api/gl/accounts/{Guid.NewGuid()}/deactivation",
      host.TokenWith(GlPermissionNames.UpdateAccounts), "{}"));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // Reactivation is the correction path for a deactivation, and it carries the DEACTIVATE authority rather
  // than the update one — the same permission undoes what it did, which is the pairing least likely to be
  // wrong and most costly if it is.
  [Fact]
  public async Task Reactivating_an_account_needs_the_deactivate_permission_not_the_update_one()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, $"/api/gl/accounts/{Guid.NewGuid()}/activation",
      host.TokenWith(GlPermissionNames.UpdateAccounts), "{}"));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Discarding_an_unknown_draft_is_a_not_found()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Post, $"/api/gl/journal-drafts/{Guid.NewGuid()}/discard",
      host.TokenWith(GlPermissionNames.ManageDrafts), "{}"));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("gl.not_found", await GlApiTestHost.ProblemCodeAsync(response));
  }

  // ---- ⚠ A BALANCE IS A REPORT, NOT A PROPERTY OF THE ACCOUNT RECORD — AND IT IS GUARDED TWICE.
  //
  // Gated on `GL.Reports.View` rather than `GL.Accounts.View`: a caller who may list the chart of accounts
  // is not thereby entitled to the money in them.
  //
  // **The route attribute is not what decides it.** `GetAccountBalanceAsync` independently calls
  // `resolver.ResolveAsync(GlPermissionNames.ViewReports)`, so the permission is enforced in TWO places and
  // regrading the route alone changes nothing. I found that by planting the route and watching this test
  // stay green — **a non-reddening plant that meant the guard was deeper than the plant, not that the test
  // was weak.** Both diagnoses fit a green plant and only one was true here.
  [Fact]
  public async Task An_account_balance_needs_the_reports_permission_not_the_accounts_one()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Get, $"/api/gl/accounts/{Guid.NewGuid()}/balance",
      host.TokenWith(GlPermissionNames.ViewAccounts)));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // The control for the test above: the SAME request with the reports permission gets past authorization
  // and answers about the account instead. Without it, a balance route that refused everyone would satisfy
  // the refusal assertion perfectly.
  [Fact]
  public async Task The_reports_permission_reaches_the_balance_and_answers_about_the_account()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Get, $"/api/gl/accounts/{Guid.NewGuid()}/balance",
      host.TokenWith(GlPermissionNames.ViewReports)));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("gl.not_found", await GlApiTestHost.ProblemCodeAsync(response));
  }
}
