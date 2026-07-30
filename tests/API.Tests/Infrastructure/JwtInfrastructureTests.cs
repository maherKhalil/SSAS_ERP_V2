using System.IdentityModel.Tokens.Jwt;
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

namespace SSAS.API.Tests.Infrastructure;

[Collection(HostIntegrationTestGroup.Name)]
public sealed class JwtInfrastructureTests(HostWebApplicationFactory factory)
{
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
    var token = CreateToken(HostWebApplicationFactory.SigningKey, DateTime.UtcNow.AddMinutes(-1));

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Fact]
  public void Jwt_options_validator_rejects_the_development_placeholder_outside_development()
  {
    var validator = new JwtOptionsValidator(new TestHostEnvironment("Production"));
    var options = new JwtOptions
    {
      Issuer = HostWebApplicationFactory.Issuer,
      Audience = HostWebApplicationFactory.Audience,
      SigningKey = "DevelopmentOnlySigningKey-ChangeBeforeProduction-NotASecret",
      ClockSkewSeconds = 30
    };

    var result = validator.Validate(null, options);

    Assert.True(result.Failed);
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

  private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
  {
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "SSAS.API.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
  }
}
