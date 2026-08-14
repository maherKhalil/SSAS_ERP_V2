using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// Startup validation of tenant-storage configuration (ADR-017). This became necessary in the TenantDbContext
// slice: while routing was inert a bad server map cost nothing, and now it would fail every tenant request.
public sealed class TenantStorageOptionsValidatorTests
{
  private const string ValidConnectionString =
    "Server=primary.example;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void A_well_formed_configuration_is_accepted()
  {
    var options = Options("PrimarySqlServer", ("PrimarySqlServer", ValidConnectionString));

    Assert.True(Validate(options).Succeeded);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void An_empty_server_map_is_accepted_so_the_platform_plane_can_still_start()
  {
    // Deliberate: ADR-017 makes platform availability independent of tenant storage. Refusing to start
    // would take down login and tenant selection for a deployment that simply serves no tenant ERP data
    // yet — a worse outcome than fail-closed routing errors on the requests that actually need it.
    Assert.True(Validate(Options("PrimarySqlServer")).Succeeded);
  }

  [Theory]
  [Trait("Decision", "ADR-017")]
  [InlineData("")]
  [InlineData("   ")]
  public void A_blank_default_server_key_is_rejected(string defaultServerKey)
  {
    var result = Validate(Options(defaultServerKey));

    Assert.True(result.Failed);
    Assert.Contains(result.Failures, failure => failure.Contains("DefaultServerKey", StringComparison.Ordinal));
  }

  [Theory]
  [Trait("Decision", "ADR-017")]
  [InlineData("")]
  [InlineData("   ")]
  public void A_blank_connection_string_is_rejected(string connectionString)
  {
    var result = Validate(Options("PrimarySqlServer", ("PrimarySqlServer", connectionString)));

    Assert.True(result.Failed);
    Assert.Contains(result.Failures, failure => failure.Contains("ConnectionString", StringComparison.Ordinal));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void A_malformed_connection_string_is_rejected_at_startup_rather_than_per_request()
  {
    var result = Validate(Options("PrimarySqlServer", ("PrimarySqlServer", "this is not=a=valid;;connection")));

    Assert.True(result.Failed);
    Assert.Contains(result.Failures, failure => failure.Contains("not a valid SQL Server connection string", StringComparison.Ordinal));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void A_default_server_key_with_no_matching_server_entry_is_rejected()
  {
    // The likeliest real misconfiguration: bootstrap would stamp a ServerKey that routing can never resolve.
    var result = Validate(Options("PrimarySqlServer", ("SecondarySqlServer", ValidConnectionString)));

    Assert.True(result.Failed);
    Assert.Contains(result.Failures, failure =>
      failure.Contains("DefaultServerKey", StringComparison.Ordinal) &&
      failure.Contains("no matching entry", StringComparison.Ordinal));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Server_keys_differing_only_by_case_are_rejected()
  {
    // Lookup is ordinal, so these are two servers to the factory and almost certainly one to whoever wrote
    // the configuration. Honouring the ordinal reading of an obvious typo would be the wrong kind of literal.
    var options = Options(
      "PrimarySqlServer",
      ("PrimarySqlServer", ValidConnectionString),
      ("PRIMARYSQLSERVER", ValidConnectionString));

    var result = Validate(options);

    Assert.True(result.Failed);
    Assert.Contains(result.Failures, failure => failure.Contains("differing only by case", StringComparison.Ordinal));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Failures_never_disclose_connection_or_credential_material()
  {
    // Validation messages are logged. A leak here would be worse than the misconfiguration it reports.
    var options = Options(
      "MissingServer",
      ("PrimarySqlServer", "Server=secret.internal;User ID=sa;Password=hunter2;Encrypt=True"));

    var result = Validate(options);

    Assert.True(result.Failed);
    foreach (var failure in result.Failures)
    {
      Assert.DoesNotContain("hunter2", failure, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("secret.internal", failure, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("Password", failure, StringComparison.OrdinalIgnoreCase);
    }
  }

  private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(TenantStorageOptions options) =>
    new TenantStorageOptionsValidator().Validate(null, options);

  private static TenantStorageOptions Options(
    string defaultServerKey,
    params (string Key, string ConnectionString)[] servers)
  {
    var options = new TenantStorageOptions { DefaultServerKey = defaultServerKey };
    foreach (var (key, connectionString) in servers)
    {
      options.Servers[key] = new TenantStorageServerOptions { ConnectionString = connectionString };
    }

    return options;
  }
}
