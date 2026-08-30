using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Host.API.Authentication;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// THE TENANT ACCESS TOKEN'S CLAIM SET, PINNED AS A SET (FP-002 `DEC-AUTH-0049`, item 163).
// ==================================================================================================
//
// `DEC-AUTH-0049`: tokens "contain exactly one occurrence of `iss`, `aud`, `sub`, `jti`, `iat`, `nbf`,
// `exp`, `identity_id`, `tenant_id`, `tenant_user_id`, `session_id`, `client_id`, and `security_version`.
// They contain zero or more `role` and `permission` claims." And they "exclude login and tenant email,
// user display name, TenantName, TenantStatus, CompanyId, subscription or billing information".
//
// ---- ⚠ WHY A SET EQUALITY AND NOT MORE `DoesNotContain` LINES.
//
// `JwtInfrastructureTests` already pins much of this token — subject, tenant_id, sorted roles and
// permissions, and the absence of `email`, `name` and `security_plane`. **What no test asserted is the
// SET.** A denylist only refuses the exclusions someone thought of: `company_id` is named in a BINDING
// prohibition (`ADR-025` decision 4, quoted at `ICompanySelection`) and appeared in no denylist here.
//
// **A set equality subsumes every denylist and, unlike one, reddens when a claim is ADDED** — which is the
// direction the prohibition actually needs guarding, since the risk is a future claim rather than a
// missing one.
//
// ---- WHAT THIS DOES NOT COVER, STATED SO IT IS NOT READ AS MORE.
//
// This pins what the ISSUER emits. It does not assert what the VALIDATOR would accept: the tenant profile
// in `StrictAccessTokenValidator` carries no forbidden-claim list, where the platform profile does
// (`PlatformForbiddenClaims`, which includes `company_id`). That asymmetry is reported in item 163's
// result file, not asserted here, because asserting today's behaviour would pin the weaker side as correct.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class TenantAccessTokenClaimSetTests(HostWebApplicationFactory factory)
{
  // Exactly the set `DEC-AUTH-0049` names. `iss`, `aud`, `nbf` and `exp` are written by the token
  // descriptor rather than the issuer's claim list, and the decision counts them, so they belong here.
  private static readonly string[] Specified =
  [
    "iss", "aud", "sub", "jti", "iat", "nbf", "exp",
    JwtClaimTypes.IdentityId, JwtClaimTypes.TenantId, JwtClaimTypes.TenantUserId,
    JwtClaimTypes.SessionId, JwtClaimTypes.ClientId, JwtClaimTypes.SecurityVersion,
    JwtClaimTypes.Role, JwtClaimTypes.Permission
  ];

  [Fact]
  public void The_tenant_token_carries_exactly_the_specified_claim_types_and_no_others()
  {
    var token = Issue();

    var actual = token.Claims.Select(claim => claim.Type).Distinct(StringComparer.Ordinal)
      .OrderBy(type => type, StringComparer.Ordinal).ToArray();

    Assert.Equal(
      Specified.OrderBy(type => type, StringComparer.Ordinal).ToArray(),
      actual);
  }

  // ---- ⚠ `company_id` BY NAME, BECAUSE THE PROHIBITION IS BY NAME.
  // `ICompanySelection`: "WHAT MUST NEVER IMPLEMENT THIS: a JWT `company_id` claim or
  // `ICurrentUser.CompanyId`… `ADR-025` decision 4 makes that prohibition binding rather than advisory."
  // A claim would be a client-presentable assertion of scope surviving revocation until token expiry.
  [Theory]
  [InlineData(JwtClaimTypes.CompanyId)]
  [InlineData(JwtClaimTypes.Email)]
  [InlineData(JwtClaimTypes.Name)]
  [InlineData(JwtClaimTypes.SecurityPlane)]
  public void The_tenant_token_carries_no_excluded_claim(string excluded)
  {
    Assert.DoesNotContain(Issue().Claims, claim => StringComparer.Ordinal.Equals(claim.Type, excluded));
  }

  // ---- EVERY SPECIFIED SINGLETON APPEARS EXACTLY ONCE, WHICH IS WHAT "exactly one occurrence" MEANS.
  [Theory]
  [InlineData("sub")]
  [InlineData("jti")]
  [InlineData("iat")]
  [InlineData(JwtClaimTypes.IdentityId)]
  [InlineData(JwtClaimTypes.TenantId)]
  [InlineData(JwtClaimTypes.TenantUserId)]
  [InlineData(JwtClaimTypes.SessionId)]
  [InlineData(JwtClaimTypes.ClientId)]
  [InlineData(JwtClaimTypes.SecurityVersion)]
  public void Each_specified_singleton_claim_occurs_exactly_once(string type)
  {
    Assert.Single(Issue().Claims, claim => StringComparer.Ordinal.Equals(claim.Type, type));
  }

  // ---- `DEC-AUTH-0049`: "Role and permission values are exact, distinct, and ordinally sorted."
  // Issued below with duplicates and in reverse order, so a pass-through would fail this.
  [Fact]
  public void Role_and_permission_values_are_deduplicated_and_ordinally_sorted()
  {
    var token = Issue();

    Assert.Equal(
      ["a-role", "z-role"],
      token.Claims.Where(claim => claim.Type == JwtClaimTypes.Role).Select(claim => claim.Value));
    Assert.Equal(
      ["a.permission", "z.permission"],
      token.Claims.Where(claim => claim.Type == JwtClaimTypes.Permission).Select(claim => claim.Value));
  }

  private JwtSecurityToken Issue()
  {
    using var scope = factory.Services.CreateScope();
    var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;

    var issued = issuer.Issue(
      new AccessTokenClaims(
        "immutable-subject", 11, Guid.Parse("72064151-a6a5-414b-bced-43083bc88b3c"), 22, 33, client, 4,
        ["z-role", "a-role", "z-role"],
        ["z.permission", "a.permission", "z.permission"]),
      DateTimeOffset.UtcNow);

    Assert.True(issued.IsSuccess);

    return new JwtSecurityTokenHandler().ReadJwtToken(issued.Value.AccessToken.RevealOnce().Value);
  }
}
