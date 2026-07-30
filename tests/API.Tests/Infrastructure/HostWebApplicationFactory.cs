using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SSAS.API.Tests.Infrastructure;

public sealed class HostWebApplicationFactory : WebApplicationFactory<global::Program>
{
  public const string Issuer = "https://development.ssas.local";
  public const string Audience = "ssas-erp-development";
  public const string SigningKey = "DevelopmentOnlySigningKey-ChangeBeforeProduction-NotASecret";

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Development");
  }
}
