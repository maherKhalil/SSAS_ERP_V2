using System.Net;
using SSAS.API.Tests.Gl;
using SSAS.API.Tests.Positions;
using SSAS.BuildingBlocks.Domain;
using SSAS.GL.Application.Permissions;
using SSAS.HR.Application.Permissions;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// EVERY MODULE ROUTE GROUP REFUSES A REQUEST WHOSE COMPANY CONTEXT CANNOT BE ESTABLISHED (T-139).
// ==================================================================================================
//
// ⚠⚠⚠ THE FILE NAME OVER-CLAIMS AND THE CLASS NAMES DO NOT. ONE OF THE TWO TESTS BELOW BINDS ITS FILTER
// ATTACHMENT; THE OTHER PROVES ONLY THAT THE REFUSAL HAPPENS. **`GlCompanyContextFilterAttachmentTests`
// reddens when GL's attachment is deleted. `PositionCompanyContextRefusalTests` DOES NOT** — measured, not
// assumed, and explained under "ONLY THE GL ROW BINDS ITS ATTACHMENT" below. *Read the class name, not the
// file name.*
//
// ---- ⚠⚠⚠ WHY: FOUR OF THE SEVEN ATTACHMENTS COULD BE DELETED AND NOTHING NOTICED.
//
// Each module route group carries one line — `.AddEndpointFilter<…CompanyContextEndpointFilter>()` — and the
// filter calls `ICompanyContextEstablisher.EstablishAsync`, returning a problem response when it fails.
// **Removing that one line is silent: the group simply stops establishing company context, and the request
// proceeds.**
//
// Measured by plant before this file existed:
//
//     all SEVEN attachments removed          6 red, EVERY ONE in EmployeeEndpointTests / DepartmentEndpointTests
//     GL's attachment alone                  GREEN — API 1006, Platform 1159, Architecture 725
//     Attendance + Payroll + HR-POSITIONS    GREEN — 1006/1006
//
// ***SO HR's EMPLOYEES AND DEPARTMENTS GROUPS WERE COVERED AND THE OTHER FOUR WERE NOT — INCLUDING HR's OWN
// POSITIONS GROUP.*** ⚠ **Same module, same filter type, four attachment sites, two of them asserted.** A
// reader who found `A3_Create_without_company_scope_is_refused` would reasonably conclude HR was covered.
//
// ⚠⚠ AND THE ONE PLACE A FILTER WAS NAMED IN `tests/` DID NOT HELP. `EmployeeHostCompositionTests:82`
// asserts `GetRequiredService<CompanyContextEndpointFilter>()` is non-null — **that the filter is
// RESOLVABLE, never that it is ATTACHED.** *Removing every attachment in the tree leaves it green.*
//
// ---- ⚠⚠ WHY THIS IS BEHAVIOURAL AND NOT A LIST OF ATTACHMENT SITES.
//
// A guard asserting "these seven call sites exist" would be **silent on an eighth route group added without
// the filter** — the frozen-list hazard in its dangerous direction, which is not acceptable for company
// isolation. *These tests drive the establisher to failure through each group's real pipeline and assert the
// REFUSAL, which no registration list could do.*
//
// ---- ⚠ THE LIMIT, STATED.
//
// **This proves the refusal HAPPENS. It does NOT prove the refusal cannot be bypassed by some other route**
// — a group added later gets no protection from this file until a row is added for it, and that is the
// residual a behavioural test cannot close on its own. *`GatedRouteInventoryTests` holds the route
// population; this holds the refusal.*
//
// ---- ⚠⚠⚠ AND ONLY THE GL ROW BINDS ITS ATTACHMENT. THE POSITIONS ROW DOES NOT, AND THAT IS MEASURED.
//
// Each row was planted by removing its own group's attachment:
//
//     GL attachment removed          `Failed: 1` — the GL row below, ALONE
//     POSITIONS attachment removed   ***1008/1008 GREEN — the Positions row DID NOT REDDEN***
//
// ***A PLANT'S GREEN IS NOT AN ABSENCE: IT MEANS SOMETHING ELSE ALSO ENFORCES THIS.*** The enforcement set
// for the positions family has **TWO members, and the mechanisms are different**: the group filter, and the
// handlers' own reading of `ICurrentCompany` — whose `CompanyId` is null exactly when establishment failed.
// **Either one alone produces the same 403 and the same wire code, so a black-box refusal test cannot tell
// them apart.**
//
// ⚠ So the Positions row is an honest REFUSAL test and NOT an attachment guard: *deleting
// `.AddEndpointFilter<CompanyContextEndpointFilter>()` from the positions group leaves it green.* **Its name
// says "is refused", which is what it proves — it does not say "the filter is attached", which it does
// not.** *Closing the attachment for that group needs a different instrument than this one.*
//
// ---- ⚠ FOUR CLASSES RATHER THAN ONE, BECAUSE EACH MODULE'S HOST IS ITS OWN FIXTURE.
//
// Every module's endpoint tests take that module's host as an `IClassFixture` inside that module's
// `[Collection]`, and the collection is what serialises access to a shared host. **One class taking four
// fixtures would sit in none of the four collections and could run beside a test mutating the same stub.**
// *So the arrangement is mirrored per module, which is also what makes each test read like the `A3` it is
// modelled on.*
internal static class CompanyContextRefusal
{
  // Each module's host stubs `ICompanyContextEstablisher`, so a failing establishment is arranged the way
  // `A3_Create_without_company_scope_is_refused` arranges it — the IDENTICAL error, so a divergence in a
  // module's error mapping shows up as a different wire code rather than as a passing test.
  internal static readonly Error Denied = new("Company.InvalidSelection", "denied");

  internal const string ExpectedCode = "company.scope_denied";
}

[Collection(GlApiEndpointGroup.Name)]
public sealed class GlCompanyContextFilterAttachmentTests(GlApiTestHost host) : IClassFixture<GlApiTestHost>
{
  [Fact]
  public async Task A_gl_route_is_refused_when_company_context_cannot_be_established()
  {
    host.ResetToAuthorizedState();
    host.CompanyContext.Error = CompanyContextRefusal.Denied;

    var response = await host.Client.SendAsync(GlApiTestHost.Request(
      HttpMethod.Get, "/api/gl/accounts", host.TokenWith(GlPermissionNames.ViewAccounts)));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal(CompanyContextRefusal.ExpectedCode, await GlApiTestHost.ProblemCodeAsync(response));
  }
}

// ==================================================================================================
// ⚠⚠⚠ PAYROLL AND ATTENDANCE ARE DELIBERATELY ABSENT FROM THIS FILE, AND THE REASON IS A PRODUCT FINDING.
// ==================================================================================================
//
// The two rows were WRITTEN, identical in shape to the two above, and they FAILED — not because the refusal
// is missing, but because of what it returns:
//
//     Expected: Forbidden          Actual: InternalServerError
//
// **The filter IS attached and the request IS refused in both — the handler never runs.** *This is not a
// company-isolation hole.* ⚠ **What differs is the RESPONSE: a caller denied a company gets `500 Internal
// Server Error` instead of `403 company.scope_denied`.**
//
// The cause is in the mappers, and it is exact:
//
//     GlApiErrorMapper.cs:148           "Company.InvalidSelection" => CompanyScopeDenied        403
//     PayrollApiErrorMapper.cs:279-291  knows "Company.ContextRequired" and "Company.ScopeDenied"
//                                       — NOT "Company.InvalidSelection"     → `_ => WriteFailure`  500
//     AttendanceApiErrorMapper.cs:198   knows "Attendance.CompanyScopeDenied" only, NO `Company.*` code
//                                       at all                               → `_ => WriteFailure`  500
//
// ⚠ **`Company.InvalidSelection` IS PRODUCT-EMITTED, not an invented test value**: `TenantCompanyAccessResolver`
// returns it at four sites, it is what `A3_Create_without_company_scope_is_refused` arranges, and GL maps it
// explicitly. *A user selecting a company they are not assigned to produces it.*
//
// ---- ⚠⚠⚠ THE WRITTEN RULE, AND ITS SCOPE — WHICH IS NARROWER THAN IT LOOKS.
//
// `docs/17-features/FP-006-hr-employee/api-contracts.md`, status *Approved for Implementation*, **frontmatter
// `module: HR`** — both quotes read from the file:
//
//   `:41`   *"A missing header returns `400 request.invalid`. A company that fails any validation step
//           returns `403 company.scope_denied`, identical for nonexistent, wrong-tenant, inactive, and
//           unauthorized identifiers, so existence is never disclosed."*
//   `:258`  `| Company unauthorized, inactive, wrong tenant, or unknown | 403 | company.scope_denied |`
//
// ⚠⚠ **The clause's stated purpose is non-disclosure THROUGH UNIFORMITY — *identical* for every failure
// reason — and a 500 is not uniform with a 403.**
//
// ⚠⚠⚠ **BUT THE DOCUMENT IS HR's.** Searched: the only files specifying `company.scope_denied` are
// **FP-006 (HR employee), FP-007 (HR department) and FP-009 (HR import/export) — all HR.** ***NO PAYROLL OR
// ATTENDANCE CONTRACT SPECIFYING THIS CODE EXISTS IN `docs/`.*** So the accurate statement is **not** that
// those two modules violate a ratified contract binding on them; it is that **they diverge from a rule
// written down for HR and independently followed by GL, with no contract of their own found either way.**
// *That is a question for whoever owns the API surface, and it is a weaker claim than "contradicts a closed
// ruling" — recorded at the weaker strength deliberately.*
//
// ⚠ A THIRD PASSAGE WAS NEARLY CITED HERE AND IS NOT, because reading it disqualified it: `:267` says
// internal refusals *"map to `403 branch.scope_denied` or `403 company.scope_denied`"* — **but its subject
// is "internal persistence refusals from the branch and company WRITE boundaries", and this is the
// establishment path on reads.** *Right words, wrong referent.*
//
// ***THE TESTS ARE NOT ADDED HERE ASSERTING 500, AND NOT ADDED ASSERTING 403 EITHER.*** There are two ways
// to reconcile this — the mappers change, or the contract does — **and a test asserting either side would be
// a ruling.** *A header citing both sides is the artefact. The rows go in when the owner rules, not when the
// mappers change.*

// ⚠ HR's POSITIONS GROUP IS A SEPARATE ATTACHMENT FROM EMPLOYEES' AND DEPARTMENTS', AND WAS THE ONLY
// UNCOVERED ONE INSIDE AN OTHERWISE COVERED MODULE. It is the row most likely to be thought redundant later
// — "HR is already covered" — so the reason it is not is written here rather than left to be re-derived.
[Collection(PositionApiEndpointGroup.Name)]
// ⚠ NAMED `…RefusalTests`, NOT `…FilterAttachmentTests`, AND THE DIFFERENCE IS THE MEASUREMENT ABOVE: the
// positions attachment can be deleted with this green. **The class name is what appears in a test run, so it
// is the name that has to be honest.**
public sealed class PositionCompanyContextRefusalTests(PositionApiTestHost host)
  : IClassFixture<PositionApiTestHost>
{
  [Fact]
  public async Task An_hr_positions_route_is_refused_when_company_context_cannot_be_established()
  {
    host.Reset();
    host.CompanyContext.Error = CompanyContextRefusal.Denied;

    var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Get, "/api/hr/positions", host.TokenWith(HrPermissionNames.ViewPositions)));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal(CompanyContextRefusal.ExpectedCode, await PositionApiTestHost.ProblemCodeAsync(response));
  }
}
