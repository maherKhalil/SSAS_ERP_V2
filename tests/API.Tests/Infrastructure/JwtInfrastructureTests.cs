using SSAS.Platform.Domain;
using SSAS.BuildingBlocks.Domain;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using SSAS.Host.API.Authentication;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Application.Authentication;

namespace SSAS.API.Tests.Infrastructure;

[Collection(HostIntegrationTestGroup.Name)]
public sealed class JwtInfrastructureTests(HostWebApplicationFactory factory)
{
  [Fact]
  public void Access_token_issuer_emits_rs256_known_kid_and_exact_trusted_bindings()
  {
    using var scope = factory.Services.CreateScope();
    var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
    var keys = scope.ServiceProvider.GetRequiredService<ISigningKeyProvider>();
    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var now = DateTimeOffset.UtcNow;
    var tenantId = Guid.Parse("72064151-a6a5-414b-bced-43083bc88b3c");

    var issued = issuer.Issue(new AccessTokenClaims(
      "immutable-subject", 11, tenantId, 22, 33, client, 4,
      ["z-role", "a-role", "z-role"], ["z.permission", "a.permission", "z.permission"]), now);

    Assert.True(issued.IsSuccess);
    var compact = issued.Value.AccessToken.RevealOnce().Value;
    var token = new JwtSecurityTokenHandler().ReadJwtToken(compact);
    Assert.Equal(SecurityAlgorithms.RsaSha256, token.Header.Alg);
    Assert.Equal(keys.Snapshot.ActiveSigningKey.KeyId, token.Header.Kid);
    Assert.Equal(TimeSpan.FromMinutes(15), token.ValidTo - token.ValidFrom);
    Assert.True(compact.Length <= 8192);
    Assert.True(Guid.TryParseExact(token.Id, "N", out _));
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.Subject && claim.Value == "immutable-subject");
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.TenantId && claim.Value == tenantId.ToString("D"));
    Assert.Equal(["a-role", "z-role"], token.Claims.Where(claim => claim.Type == JwtClaimTypes.Role).Select(claim => claim.Value));
    Assert.Equal(["a.permission", "z.permission"], token.Claims.Where(claim => claim.Type == JwtClaimTypes.Permission).Select(claim => claim.Value));
    Assert.DoesNotContain(token.Claims, claim => claim.Type is JwtClaimTypes.Email or JwtClaimTypes.Name);
    // Tenant regression (DEC-TEN-0022): the tenant profile carries NO security_plane claim (absence ⇒ tenant).
    Assert.DoesNotContain(token.Claims, claim => claim.Type == JwtClaimTypes.SecurityPlane);
  }

  [Fact]
  public void Platform_token_issuer_emits_the_platform_profile_and_no_tenant_claims()
  {
    using var scope = factory.Services.CreateScope();
    var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var now = DateTimeOffset.UtcNow;

    var issued = issuer.Issue(new PlatformAccessTokenClaims(
      "immutable-subject", 11, 33, client, 4,
      ["Platform.Tenants.View", "Platform.Support.Administer", "Platform.Tenants.View"]), now);

    Assert.True(issued.IsSuccess);
    var token = new JwtSecurityTokenHandler().ReadJwtToken(issued.Value.AccessToken.RevealOnce().Value);

    // security_plane=platform exactly once.
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.SecurityPlane && claim.Value == "platform");
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.Subject && claim.Value == "immutable-subject");
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.IdentityId && claim.Value == "11");
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.SessionId && claim.Value == "33");
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.ClientId && claim.Value == AuthenticationClientId.V1Web);
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.SecurityVersion && claim.Value == "4");
    // Permissions deduped + ordinally ordered.
    Assert.Equal(
      ["Platform.Support.Administer", "Platform.Tenants.View"],
      token.Claims.Where(claim => claim.Type == JwtClaimTypes.Permission).Select(claim => claim.Value));

    // Platform profile forbids every tenant-shaped claim.
    Assert.DoesNotContain(token.Claims, claim =>
      claim.Type == JwtClaimTypes.TenantId || claim.Type == JwtClaimTypes.TenantUserId ||
      claim.Type == JwtClaimTypes.Role || claim.Type == JwtClaimTypes.CompanyId);
  }

  [Fact]
  public void Platform_token_issuer_rejects_a_claim_set_with_no_permissions()
  {
    using var scope = factory.Services.CreateScope();
    var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;

    var issued = issuer.Issue(new PlatformAccessTokenClaims("immutable-subject", 11, 33, client, 4, []), DateTimeOffset.UtcNow);

    Assert.True(issued.IsFailure);
  }

  [Fact]
  public void Access_token_issuer_rejects_a_token_larger_than_the_approved_ceiling()
  {
    using var scope = factory.Services.CreateScope();
    var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var permissions = Enumerable.Range(0, 500).Select(index => $"permission.{index:D4}.{new string('x', 32)}").ToArray();

    var issued = issuer.Issue(new AccessTokenClaims(
      "immutable-subject", 11, Guid.NewGuid(), 22, 33, client, 4, [], permissions), DateTimeOffset.UtcNow);

    Assert.True(issued.IsFailure);
    Assert.Equal("Authentication.AccessTokenUnavailable", issued.Error.Code);
  }

  [Fact]
  public async Task Invalid_jwt_is_rejected_by_the_registered_authentication_handler()
  {
    var token = CreateToken("DifferentTestSigningKey-ForInvalidSignature-NotASecret", DateTime.UtcNow.AddMinutes(5));

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Fact]
  public async Task Expired_jwt_is_rejected_by_the_registered_authentication_handler()
  {
    var key = factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var token = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(-1));

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Fact]
  public async Task Unknown_kid_is_rejected_without_trying_the_active_key()
  {
    var key = factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var token = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(5), "unknown-kid");

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Fact]
  public async Task Missing_kid_is_rejected()
  {
    var key = factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var token = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(5), removeKid: true);

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Fact]
  public async Task Unsigned_alg_none_token_is_rejected()
  {
    var now = DateTimeOffset.UtcNow;
    var token = new JwtSecurityToken(
      HostWebApplicationFactory.Issuer,
      HostWebApplicationFactory.Audience,
      CreateRequiredClaims(now),
      now.AddMinutes(-1).UtcDateTime,
      now.AddMinutes(5).UtcDateTime);

    var result = await AuthenticateAsync(new JwtSecurityTokenHandler().WriteToken(token));

    Assert.False(result.Succeeded);
  }

  [Fact]
  public async Task Algorithm_substitution_is_rejected()
  {
    var key = factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var token = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(5), algorithm: SecurityAlgorithms.RsaSha384);

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Theory]
  [InlineData("https://wrong-issuer.test", HostWebApplicationFactory.Audience)]
  [InlineData(HostWebApplicationFactory.Issuer, "wrong-audience")]
  public async Task Wrong_issuer_or_audience_is_rejected(string issuer, string audience)
  {
    var key = factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var token = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(5), issuer: issuer, audience: audience);

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Fact]
  public async Task Not_yet_valid_token_is_rejected()
  {
    var key = factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var token = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(10), notBefore: DateTime.UtcNow.AddMinutes(5));

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Fact]
  public async Task Malformed_identifier_and_duplicate_claims_are_rejected()
  {
    var key = factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var malformed = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => Replace(claims, JwtClaimTypes.IdentityId, "01"));
    var duplicateCritical = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(5),
      extraClaims: [new Claim(JwtClaimTypes.SessionId, "4")]);
    var duplicateRole = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(5),
      extraClaims: [new Claim(JwtClaimTypes.Role, "role"), new Claim(JwtClaimTypes.Role, "role")]);

    Assert.False((await AuthenticateAsync(malformed)).Succeeded);
    Assert.False((await AuthenticateAsync(duplicateCritical)).Succeeded);
    Assert.False((await AuthenticateAsync(duplicateRole)).Succeeded);
  }

  [Fact]
  public async Task Token_from_the_access_token_issuer_passes_strict_bearer_validation()
  {
    var issuer = factory.Services.GetRequiredService<IAccessTokenIssuer>();
    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var issued = issuer.Issue(new AccessTokenClaims(
      "validated-subject", 1, Guid.NewGuid(), 2, 3, client, 1, ["role"], ["permission"]), DateTimeOffset.UtcNow);
    var token = issued.Value.AccessToken.RevealOnce().Value;

    var result = await AuthenticateAsync(token);

    Assert.True(result.Succeeded);
  }

  // ---- Phase 3C-2: security-plane profile validation (ADR-015 / DEC-TEN-0022) ----

  [Fact]
  public async Task Platform_token_from_the_issuer_passes_strict_bearer_validation()
  {
    // Anchors 3C-1 issuer ↔ 3C-2 validator compatibility on a real signed platform token.
    var issuer = factory.Services.GetRequiredService<IAccessTokenIssuer>();
    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var issued = issuer.Issue(
      new PlatformAccessTokenClaims("platform-subject", 1, 3, client, 1, ["Platform.Support.Administer", "Platform.Tenants.View"]),
      DateTimeOffset.UtcNow);

    Assert.True((await AuthenticateAsync(issued.Value.AccessToken.RevealOnce().Value)).Succeeded);
  }

  [Fact]
  public async Task Legacy_tenant_token_without_security_plane_is_accepted()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5));

    Assert.True((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  public async Task Explicit_tenant_security_plane_is_accepted()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      extraClaims: [new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Tenant)]);

    Assert.True((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  public async Task Structural_platform_token_is_accepted()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5), mutateClaims: ToPlatformClaims);

    Assert.True((await AuthenticateAsync(token)).Succeeded);
  }

  [Theory]
  [InlineData(JwtClaimTypes.TenantId, "b1b7c1e2-0000-4000-8000-000000000001")] // platform + tenant_id
  [InlineData(JwtClaimTypes.TenantId, "")]                                     // forbidden even when blank
  [InlineData(JwtClaimTypes.TenantUserId, "2")]                                // platform + tenant_user_id
  [InlineData(JwtClaimTypes.Role, "anything")]                                 // platform + role
  [InlineData(JwtClaimTypes.CompanyId, "b1b7c1e2-0000-4000-8000-000000000002")] // platform + company_id
  public async Task Platform_token_with_a_forbidden_claim_is_rejected(string forbiddenType, string forbiddenValue)
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => ToPlatformClaims(claims).Append(new Claim(forbiddenType, forbiddenValue)).ToList());

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  // ---- THE TENANT PLANE REFUSES `company_id` TOO (ADR-025 decision 4, item 164).
  // The prohibition names this claim by hand and is binding, and until now the guard existed only on the
  // PLATFORM plane -- the plane where the claim was never plausible -- and not on the one it names.
  // This is a NAMED prohibition, not a general out-of-set rejection; the tenant profile still accepts an
  // unlisted extra claim, which is costed rather than built (item 164).
  [Fact]
  public async Task Tenant_token_carrying_company_id_is_rejected()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => claims
        .Append(new Claim(JwtClaimTypes.CompanyId, "b1b7c1e2-0000-4000-8000-000000000003")).ToList());

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  // ---- THE CONTROL: the very same token WITHOUT the claim authenticates, so the refusal above is the
  // ---- claim's doing and not some unrelated defect in how this token was built.
  [Fact]
  public async Task The_same_tenant_token_without_company_id_is_accepted()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5));

    Assert.True((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  public async Task Platform_token_with_no_permission_is_rejected()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => claims
        .Where(claim => claim.Type is not (JwtClaimTypes.TenantId or JwtClaimTypes.TenantUserId))
        .Append(new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform))
        .ToList());

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  public async Task Platform_token_with_a_duplicate_permission_is_rejected()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => ToPlatformClaims(claims).Append(new Claim(JwtClaimTypes.Permission, "Platform.Support.Administer")).ToList());

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  public async Task Platform_token_with_a_blank_permission_is_rejected()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => claims
        .Where(claim => claim.Type is not (JwtClaimTypes.TenantId or JwtClaimTypes.TenantUserId))
        .Append(new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform))
        .Append(new Claim(JwtClaimTypes.Permission, " "))
        .ToList());

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  public async Task Duplicate_security_plane_is_rejected()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => ToPlatformClaims(claims).Append(new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform)).ToList());

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  [Theory]
  [InlineData("Platform")]
  [InlineData("PLATFORM")]
  [InlineData("Tenant")]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("bogus")]
  public async Task Unknown_or_wrong_case_security_plane_is_rejected(string plane)
  {
    // Even on an otherwise-valid tenant token, a non-exact security_plane value is rejected.
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      extraClaims: [new Claim(JwtClaimTypes.SecurityPlane, plane)]);

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  public async Task Tenant_shaped_token_claiming_platform_plane_is_rejected()
  {
    // Attack: a valid tenant token (with tenant fields) sets security_plane=platform → platform profile
    // forbids tenant_id/tenant_user_id → rejected.
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      extraClaims: [new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform)]);

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  public async Task Platform_shaped_token_claiming_tenant_plane_is_rejected()
  {
    // Attack: a platform-shaped token (no tenant fields) sets security_plane=tenant → tenant profile
    // requires tenant_id/tenant_user_id → rejected.
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => claims
        .Where(claim => claim.Type is not (JwtClaimTypes.TenantId or JwtClaimTypes.TenantUserId))
        .Append(new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Tenant))
        .Append(new Claim(JwtClaimTypes.Permission, "Platform.Support.Administer"))
        .ToList());

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  [Theory]
  [InlineData(JwtClaimTypes.TenantId, "b1b7c1e2-0000-4000-8000-000000000003")]
  [InlineData(JwtClaimTypes.TenantUserId, "9")]
  public async Task Tenant_profile_rejects_duplicate_singleton_claims(string type, string extraValue)
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      extraClaims: [new Claim(type, extraValue)]);

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  private X509SecurityKey ActiveKey() => factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;

  private static List<Claim> ToPlatformClaims(List<Claim> claims) =>
    claims
      .Where(claim => claim.Type is not (JwtClaimTypes.TenantId or JwtClaimTypes.TenantUserId))
      .Append(new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform))
      .Append(new Claim(JwtClaimTypes.Permission, "Platform.Support.Administer"))
      .ToList();

  [Fact]
  public void Jwt_options_validator_requires_a_production_signing_certificate()
  {
    var validator = new JwtOptionsValidator(new TestHostEnvironment("Production"));
    var options = new JwtOptions
    {
      Issuer = HostWebApplicationFactory.Issuer,
      Audience = HostWebApplicationFactory.Audience,
      ClockSkewSeconds = 30
    };

    var result = validator.Validate(null, options);

    Assert.True(result.Failed);
  }

  [Fact]
  public void Production_key_provider_keeps_enabled_overlap_keys_and_excludes_disabled_keys()
  {
    var directory = Path.Combine(Path.GetTempPath(), $"ssas-jwt-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
      const string password = "test-only-password";
      using var active = CreateCertificate("active");
      using var retained = CreateCertificate("retained");
      using var disabled = CreateCertificate("disabled");
      var activePath = Path.Combine(directory, "active.pfx");
      var retainedPath = Path.Combine(directory, "retained.cer");
      var disabledPath = Path.Combine(directory, "disabled.cer");
      File.WriteAllBytes(activePath, active.Export(X509ContentType.Pfx, password));
      File.WriteAllBytes(retainedPath, retained.Export(X509ContentType.Cert));
      File.WriteAllBytes(disabledPath, disabled.Export(X509ContentType.Cert));
      var options = ProductionOptions(activePath, password,
      [
        new VerificationCertificateOptions
        {
          Path = retainedPath,
          Enabled = true,
          RetireAfterUtc = DateTimeOffset.UtcNow.AddMinutes(16)
        },
        new VerificationCertificateOptions { Path = disabledPath, Enabled = false }
      ]);

      using var provider = new SigningKeyProvider(Options.Create(options), new TestHostEnvironment("Production"));

      Assert.Equal(2, provider.Snapshot.EnabledVerificationKeys.Count);
      Assert.Contains(provider.Snapshot.ActiveSigningKey.KeyId, provider.Snapshot.EnabledVerificationKeys.Keys);
      Assert.Contains(Kid(retained), provider.Snapshot.EnabledVerificationKeys.Keys);
      Assert.DoesNotContain(Kid(disabled), provider.Snapshot.EnabledVerificationKeys.Keys);
    }
    finally
    {
      Directory.Delete(directory, true);
    }
  }

  [Fact]
  public void Production_key_provider_rejects_duplicate_kid_and_insufficient_overlap()
  {
    var directory = Path.Combine(Path.GetTempPath(), $"ssas-jwt-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
      const string password = "test-only-password";
      using var active = CreateCertificate("active");
      using var retained = CreateCertificate("retained");
      var activePath = Path.Combine(directory, "active.pfx");
      var activePublicPath = Path.Combine(directory, "active.cer");
      var retainedPath = Path.Combine(directory, "retained.cer");
      File.WriteAllBytes(activePath, active.Export(X509ContentType.Pfx, password));
      File.WriteAllBytes(activePublicPath, active.Export(X509ContentType.Cert));
      File.WriteAllBytes(retainedPath, retained.Export(X509ContentType.Cert));

      var duplicate = ProductionOptions(activePath, password,
        [new VerificationCertificateOptions { Path = activePublicPath, RetireAfterUtc = DateTimeOffset.UtcNow.AddMinutes(16) }]);
      Assert.Throws<InvalidOperationException>(() =>
        new SigningKeyProvider(Options.Create(duplicate), new TestHostEnvironment("Production")));

      var insufficientOverlap = ProductionOptions(activePath, password,
        [new VerificationCertificateOptions { Path = retainedPath, RetireAfterUtc = DateTimeOffset.UtcNow.AddMinutes(15) }]);
      Assert.Throws<InvalidOperationException>(() =>
        new SigningKeyProvider(Options.Create(insufficientOverlap), new TestHostEnvironment("Production")));
    }
    finally
    {
      Directory.Delete(directory, true);
    }
  }

  private async Task<AuthenticateResult> AuthenticateAsync(string token)
  {
    using var scope = factory.Services.CreateScope();
    var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    context.Request.Headers[HeaderNames.Authorization] = $"Bearer {token}";

    return await scope.ServiceProvider
      .GetRequiredService<IAuthenticationService>()
      .AuthenticateAsync(context, JwtBearerDefaults.AuthenticationScheme);
  }

  private static string CreateToken(string signingKey, DateTime expiresAt)
  {
    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(
      issuer: HostWebApplicationFactory.Issuer,
      audience: HostWebApplicationFactory.Audience,
      claims: [new Claim("sub", "test-user")],
      notBefore: expiresAt.AddMinutes(-5),
      expires: expiresAt,
      signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  private static string CreateRs256Token(
    SecurityKey signingKey,
    DateTime expiresAt,
    string? kid = null,
    bool removeKid = false,
    DateTime? notBefore = null,
    string? issuer = null,
    string? audience = null,
    string algorithm = SecurityAlgorithms.RsaSha256,
    IEnumerable<Claim>? extraClaims = null,
    Func<List<Claim>, List<Claim>>? mutateClaims = null)
  {
    var now = DateTimeOffset.UtcNow;
    var claims = CreateRequiredClaims(now).Concat(extraClaims ?? []).ToList();
    if (mutateClaims is not null) claims = mutateClaims(claims);
    var token = new JwtSecurityToken(
      issuer ?? HostWebApplicationFactory.Issuer,
      audience ?? HostWebApplicationFactory.Audience,
      claims,
      notBefore ?? expiresAt.AddMinutes(-5),
      expiresAt,
      new SigningCredentials(signingKey, algorithm));
    if (kid is not null) token.Header[JwtHeaderParameterNames.Kid] = kid;
    if (removeKid) token.Header.Remove(JwtHeaderParameterNames.Kid);
    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  private static IEnumerable<Claim> CreateRequiredClaims(DateTimeOffset now) =>
  [
    new Claim(JwtClaimTypes.Subject, "test-user"),
    new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N")),
    new Claim("iat", now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
    new Claim(JwtClaimTypes.IdentityId, "1"),
    new Claim(JwtClaimTypes.TenantId, Guid.NewGuid().ToString("D")),
    new Claim(JwtClaimTypes.TenantUserId, "2"),
    new Claim(JwtClaimTypes.SessionId, "3"),
    new Claim(JwtClaimTypes.ClientId, AuthenticationClientId.V1Web),
    new Claim(JwtClaimTypes.SecurityVersion, "1")
  ];

  private static List<Claim> Replace(List<Claim> claims, string type, string value) =>
    claims.Where(claim => claim.Type != type).Append(new Claim(type, value)).ToList();

  private static JwtOptions ProductionOptions(
    string activePath,
    string password,
    VerificationCertificateOptions[] verificationCertificates) => new()
    {
      Issuer = HostWebApplicationFactory.Issuer,
      Audience = HostWebApplicationFactory.Audience,
      AccessTokenLifetime = TimeSpan.FromMinutes(15),
      ClockSkewSeconds = 30,
      MaximumEncodedTokenSize = 8192,
      ActiveSigningCertificatePath = activePath,
      ActiveSigningCertificatePassword = password,
      VerificationCertificates = verificationCertificates
    };

  private static X509Certificate2 CreateCertificate(string name)
  {
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest($"CN=SSAS JWT {name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
  }

  private static string Kid(X509Certificate2 certificate) =>
    Base64UrlEncoder.Encode(SHA256.HashData(certificate.RawData));

  private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
  {
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "SSAS.API.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
  }
  // ================================================================================================
  // ⚠ A SIGNING FAILURE IS OPAQUE TO THE CALLER AND VISIBLE TO THE OPERATOR (T-241).
  // ================================================================================================
  //
  // Both `Issue` overloads wrap their body in `catch (Exception)` and return the generic
  // `AccessTokenIssuanceUnavailable`. **The response is right and the silence was not**: the file had no
  // logger at all, so a signing key that failed to load and a routine refusal were indistinguishable to
  // everyone, including the operator watching authentication fail for every user at once.
  //
  // **This test has to assert BOTH HALVES, because either alone is satisfied by the wrong fix.** Asserting
  // only the log would pass if someone also widened the response to name the cause -- which is the leak
  // the generic failure exists to prevent. Asserting only the response is what the code already did.
  [Fact]
  public void A_signing_failure_is_logged_with_its_cause_and_still_answers_the_caller_generically()
  {
    var logger = new CapturingLogger<AccessTokenIssuer>();
    var issuer = new AccessTokenIssuer(
      new ThrowingSigningKeyProvider(),
      Options.Create(new JwtOptions { Issuer = "ssas", Audience = "ssas-web" }),
      logger);

    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var issued = issuer.Issue(new AccessTokenClaims(
      "immutable-subject", 11, Guid.Parse("72064151-a6a5-414b-bced-43083bc88b3c"), 22, 33, client, 4,
      ["a-role"], ["a.permission"]), DateTimeOffset.UtcNow);

    // Outward: unchanged, and carrying nothing about the cause.
    Assert.True(issued.IsFailure);
    Assert.Equal(AuthenticationErrors.AccessTokenIssuanceUnavailable.Code, issued.Error.Code);
    Assert.DoesNotContain("signing key", issued.Error.Message, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain(ThrowingSigningKeyProvider.Marker, issued.Error.Message, StringComparison.Ordinal);

    // Inward: the exception itself, not just a note that something failed. A log line without the
    // exception attached would leave the operator exactly as unable to tell the causes apart.
    var entry = Assert.Single(logger.Entries);
    Assert.Equal(LogLevel.Error, entry.Level);
    Assert.NotNull(entry.Exception);
    Assert.Contains(ThrowingSigningKeyProvider.Marker, entry.Exception!.Message, StringComparison.Ordinal);
  }

  // Throws where the real provider would hand back a key -- the shape of a certificate that failed to
  // load, which is the failure this whole path was silent about.
  private sealed class ThrowingSigningKeyProvider : ISigningKeyProvider
  {
    internal const string Marker = "signing-key-unavailable-marker";

    public SigningKeySnapshot Snapshot => throw new InvalidOperationException(Marker);
  }

  private sealed class CapturingLogger<T> : ILogger<T>
  {
    internal List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
      LogLevel logLevel, EventId eventId, TState state, Exception? exception,
      Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception));
  }
}
