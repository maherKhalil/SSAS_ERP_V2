using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.API.Companies;
using SSAS.Platform.API.IdentityAccess;
using SSAS.Platform.API.Localization;
using SSAS.Platform.API.PlatformSupport;

namespace SSAS.Platform.Tests.IdentityAccess;

// ==================================================================================================
// THE TWELVE ANSWERS T-093 RULED, PINNED AS STATUS CODES.
// ==================================================================================================
//
// ---- WHY THIS EXISTS ALONGSIDE THE ARCHITECTURE REGISTER.
//
// `ModuleErrorMappingArchitectureTests` asserts that a code HAS an arm. It deliberately does not assert
// WHICH status the arm produces — *"a status code is a contract decision owned by the surface, and a test
// asserting one would be choosing it."*
//
// **So the register would stay green if `Common.NotFound` were mapped to 400.** The defect T-091 shipped
// was a wrong ANSWER, not a missing arm, and the register alone cannot see the difference. This file
// pins the answers a person ruled.
//
// ---- AND THE FLIP BROKE NOTHING, WHICH IS THE REASON THESE ARE WORTH WRITING.
//
// Turning two sites' defaults from 400 to 500 left all 759 API tests green. **Nothing was asserting the
// old behaviour — and nothing was covering the new behaviour either.** A wrong answer that no test can
// distinguish from a right one is exactly how this survived being shipped.
public sealed class PlatformErrorMappingTests
{
  // ---- THE DEFECT ITSELF. `POST /api/platform/tenant-users/{id}/deactivation` answered
  // ---- `400 request.invalid` for a tenant user that does not exist.
  [Theory]
  [InlineData("Common.NotFound", 404)]
  [InlineData("TenantUser.InvalidTransition", 409)]
  [InlineData("Persistence.UniqueConstraint", 409)]
  [InlineData("Persistence.ConcurrencyConflict", 409)]
  [InlineData("Authorization.Unauthorized", 403)]
  [InlineData("Tenant.Unauthorized", 403)]
  [InlineData("Persistence.WriteFailure", 500)]
  public void The_identity_access_site_answers_as_ruled(string code, int status) =>
    Assert.Equal(status, IdentityAccessApiErrorMapper.Map(new Error(code, "x")).StatusCode);

  // ---- T-092's LINK REFUSALS, AND THE TWO COLLISION DIRECTIONS SHARE A WIRE CODE ON PURPOSE.
  //
  // The two `Error` values stay separate so the DESCRIPTION names the repair; the STATUS and code are shared
  // because a client branching on which side collided would be making a decision only a human can act on.
  //
  // `EmploymentEnded` is 409 rather than 400: the request is well formed and the STATE refuses it.
  // `UserEmployeeLink.NotFound` is 404 and deliberately not a silent success, so a typo in the tenant user
  // id cannot look like a completed correction.
  [Theory]
  [InlineData("UserEmployeeLink.TenantUserAlreadyLinked", 409)]
  [InlineData("UserEmployeeLink.EmployeeAlreadyLinked", 409)]
  [InlineData("UserEmployeeLink.EmploymentEnded", 409)]
  [InlineData("UserEmployeeLink.NotFound", 404)]
  [InlineData("UserEmployeeLink.Invalid", 400)]
  public void The_link_refusals_answer_as_ruled(string code, int status) =>
    Assert.Equal(status, IdentityAccessApiErrorMapper.Map(new Error(code, "x")).StatusCode);

  // ---- AND THE INVERSION SURVIVES THE MAPPER.
  //
  // The handler distinguishes an unknown employee from a terminated one. **A mapper that folded both into
  // one status would undo that at the last step**, and the administrator would be back to guessing whether
  // they mistyped an id or named a former employee.
  [Fact]
  public void An_unknown_employee_and_a_terminated_one_reach_the_wire_as_different_statuses() =>
    Assert.NotEqual(
      IdentityAccessApiErrorMapper.Map(new Error("Common.NotFound", "x")).StatusCode,
      IdentityAccessApiErrorMapper.Map(new Error("UserEmployeeLink.EmploymentEnded", "x")).StatusCode);

  // ---- THE DEFAULT, ASSERTED AS A DEFAULT.
  //
  // A code no arm names must answer 500, not 400: an unmapped code means the table is out of date, and a
  // 400 blames the caller for the gap and hides it. The invented code is what makes this a test of the
  // FALLBACK rather than of some arm that happens to exist.
  [Fact]
  public void An_unmapped_code_is_a_server_error_at_the_identity_access_site() =>
    Assert.Equal(500, IdentityAccessApiErrorMapper.Map(new Error("Nothing.MapsThis", "x")).StatusCode);

  // ---- THE FINDING: ONE REFUSAL, MISSING AT EVERY SITE.
  //
  // `Tenant.Unauthorized` comes from `ApplicationExecutionContext.GetTenantActor`, which every
  // tenant-plane handler funnels through. It answered 500 at two sites and 400 at the other two.
  //
  // Asserted at all four TOGETHER rather than site by site, because the property that matters is that they
  // AGREE — a caller must not learn which surface refused them from the status code.
  [Fact]
  public void Every_platform_site_answers_a_tenant_authorization_refusal_with_403()
  {
    var unauthorized = new Error("Tenant.Unauthorized", "x");

    Assert.Equal(403, IdentityAccessApiErrorMapper.Map(unauthorized).StatusCode);
    Assert.Equal(403, CompanyApiErrorMapper.Map(unauthorized).StatusCode);
    Assert.Equal(403, PlatformSupportAuthorityApiErrorMapper.Map(unauthorized).StatusCode);

    Assert.True(LocalizationApiErrorMapper.TryMap(unauthorized.Code, out var localization));
    Assert.Equal(403, localization.StatusCode);
  }

  // ---- THE EXPLICIT ARMS THAT MATCH THEIR DEFAULT.
  //
  // T-080's precedent: an arm agreeing with the default is a decision, its absence is an accident, and the
  // two are indistinguishable from the wire. **This test is what stops the next reader deleting them as
  // redundant** — it fails on the arm's absence at Localization, whose default is not even in the same
  // file, and passes either way at the other two. So it is written where it can fail.
  [Fact]
  public void A_write_failure_is_a_server_error_at_every_platform_site()
  {
    var writeFailure = new Error("Persistence.WriteFailure", "x");

    Assert.Equal(500, IdentityAccessApiErrorMapper.Map(writeFailure).StatusCode);
    Assert.Equal(500, CompanyApiErrorMapper.Map(writeFailure).StatusCode);
    Assert.Equal(500, PlatformSupportAuthorityApiErrorMapper.Map(writeFailure).StatusCode);

    // Localization's arm is NOT redundant with its default: reaching the default there requires the call
    // site, so an absent arm would be a silent behaviour change no other assertion here would catch.
    Assert.True(LocalizationApiErrorMapper.TryMap(writeFailure.Code, out var localization));
    Assert.Equal(500, localization.StatusCode);
  }

  // ================================================================================================
  // THE NINE RULED IN T-093b — THE CODES THE REGISTER FOUND AND THE STATIC WALK DID NOT.
  // ================================================================================================
  //
  // Five of them are `CompanyAccessErrors`, raised by the company-context establisher. **An injected
  // service is exactly what the static reachability walk cannot see**, so these were found by the register
  // and ruled afterwards — the sequence the floor warning predicts.
  //
  // `Company.InvalidSelection` is 403 and NOT 404 on purpose: `TenantCompanyAccessResolver` collapses "no
  // such company", "another tenant's company", "not active" and "not assigned" into it deliberately, so a
  // caller cannot probe for companies it may not see. **A 404 would undo that collapse from the wire**,
  // which is why the status is asserted rather than left to read as an oversight.
  [Theory]
  [InlineData("Company.ContextRequired", 403)]
  [InlineData("Company.InvalidSelection", 403)]
  [InlineData("Company.SelectionRequired", 400)]
  [InlineData("Company.InvalidSelectionFormat", 400)]
  [InlineData("Company.AssignmentInvalid", 400)]
  public void The_company_site_answers_as_ruled(string code, int status) =>
    Assert.Equal(status, CompanyApiErrorMapper.Map(new Error(code, "x")).StatusCode);

  // Two authority refusals: statements about WHO is asking, not about what they sent. Under the site's
  // default they answered 500, telling an operator a working system had failed.
  [Theory]
  [InlineData("PlatformSupport.AccountIneligible", 403)]
  [InlineData("PlatformSupport.NoUsablePlatformAuthority", 403)]
  public void The_platform_support_site_answers_as_ruled(string code, int status) =>
    Assert.Equal(status, PlatformSupportAuthorityApiErrorMapper.Map(new Error(code, "x")).StatusCode);

  // ---- LOCALIZATION'S OTHER TWO RULED CODES.
  [Theory]
  [InlineData("Authorization.Unauthorized", 403)]
  [InlineData("Persistence.UniqueConstraint", 409)]
  [InlineData("localization.actor_invalid", 403)]
  [InlineData("localization.group_invalid", 400)]
  public void The_localization_site_answers_as_ruled(string code, int status)
  {
    Assert.True(LocalizationApiErrorMapper.TryMap(code, out var mapped));
    Assert.Equal(status, mapped.StatusCode);
  }

  // ---- AND ITS FALLBACK, WHICH LIVES AT THE CALL SITE AND SO IS ASSERTED THROUGH ITS SHAPE.
  //
  // `TryMap` returning false is what the route file turns into `WriteFailure`. Asserting the `false` here
  // pins the contract this site's default depends on: if `TryMap` ever started returning a value for an
  // unknown code, the call site's fallback would become unreachable and nothing else would notice.
  [Fact]
  public void An_unmapped_code_is_refused_by_the_localization_mapper_rather_than_guessed() =>
    Assert.False(LocalizationApiErrorMapper.TryMap("Nothing.MapsThis", out _));
}
