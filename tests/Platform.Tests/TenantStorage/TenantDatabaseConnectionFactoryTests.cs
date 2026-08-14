using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// TS-1C connection construction (ADR-017). The invariant under test is the one that matters most for
// tenant isolation: an unknown ServerKey must FAIL rather than silently fall back to any other connection.
public sealed class TenantDatabaseConnectionFactoryTests
{
  private const string PrimaryConnectionString =
    "Server=primary.example;Database=IgnoredBase;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";

  private const string SecondaryConnectionString =
    "Server=secondary.example;Database=IgnoredBase;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void An_exact_server_key_resolves_trusted_configuration()
  {
    var result = Factory().Create(Route("PrimarySqlServer", "SSAS_Shared_01"));

    Assert.True(result.IsSuccess);
    using var connection = result.Value;
    var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
    Assert.Equal("primary.example", builder.DataSource);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_route_database_name_replaces_the_configured_catalog()
  {
    // The base configuration's catalog is irrelevant; the route decides the database. Applied through
    // SqlConnectionStringBuilder.InitialCatalog rather than string concatenation.
    var result = Factory().Create(Route("PrimarySqlServer", "SSAS_Tenant_Dedicated_42"));

    Assert.True(result.IsSuccess);
    using var connection = result.Value;
    Assert.Equal("SSAS_Tenant_Dedicated_42", new SqlConnectionStringBuilder(connection.ConnectionString).InitialCatalog);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Trusted_authentication_and_encryption_settings_are_preserved()
  {
    var result = Factory().Create(Route("PrimarySqlServer", "SSAS_Shared_01"));

    Assert.True(result.IsSuccess);
    using var connection = result.Value;
    var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
    Assert.True(builder.IntegratedSecurity);
    Assert.True(builder.Encrypt);
    Assert.True(builder.TrustServerCertificate);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void An_unknown_server_key_fails_and_never_falls_back()
  {
    // THE critical invariant. A fallback here would open a connection to the wrong physical database while
    // every upstream layer believed routing had succeeded.
    var result = Factory().Create(Route("NotConfiguredServer", "SSAS_Shared_01"));

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.ServerKeyNotConfigured.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Server_key_lookup_is_ordinal_and_case_sensitive()
  {
    // A near-match must not resolve: silently accepting a different casing would be a fallback by another
    // name, and ServerKey is stored with ordinal collation.
    var result = Factory().Create(Route("primarysqlserver", "SSAS_Shared_01"));

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.ServerKeyNotConfigured.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Distinct_server_keys_reach_distinct_servers()
  {
    var factory = Factory();

    using var primary = factory.Create(Route("PrimarySqlServer", "SSAS_A")).Value;
    using var secondary = factory.Create(Route("SecondarySqlServer", "SSAS_B")).Value;

    Assert.Equal("primary.example", new SqlConnectionStringBuilder(primary.ConnectionString).DataSource);
    Assert.Equal("secondary.example", new SqlConnectionStringBuilder(secondary.ConnectionString).DataSource);
    Assert.NotEqual(primary.ConnectionString, secondary.ConnectionString);
  }

  [Fact]
  [Trait("Decision", "ADR-021")]
  public void A_customer_managed_route_is_rejected_before_any_connection_is_built()
  {
    // Defence in depth: the resolver already refuses, but this layer is what would open a socket.
    var route = new TenantDatabaseRoute(
      Guid.NewGuid(), 25, "PrimarySqlServer", "CustomerERP",
      TenantDatabaseHostingMode.CustomerManaged, TenantDatabaseStorageMode.Dedicated, 1, HealthyRoute);

    var result = Factory().Create(route);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.UnsupportedHostingMode.Code, result.Error.Code);
  }

  [Theory]
  [Trait("Decision", "ADR-017")]
  [InlineData("")]
  [InlineData("   ")]
  public void A_blank_database_name_fails_rather_than_defaulting_to_the_configured_catalog(string databaseName)
  {
    var result = Factory().Create(Route("PrimarySqlServer", databaseName));

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.DatabaseNameInvalid.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void An_overlong_database_name_fails()
  {
    var result = Factory().Create(
      Route("PrimarySqlServer", new string('d', TenantDatabase.DatabaseNameMaximumLength + 1)));

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.DatabaseNameInvalid.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void A_configured_server_with_a_blank_connection_string_fails()
  {
    var options = Options.Create(new TenantStorageOptions());
    options.Value.Servers["EmptyServer"] = new TenantStorageServerOptions { ConnectionString = "  " };

    var result = new TenantDatabaseConnectionFactory(options).Create(Route("EmptyServer", "SSAS_Shared_01"));

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.ServerKeyNotConfigured.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Failure_messages_never_contain_credential_or_connection_material()
  {
    var result = Factory().Create(Route("NotConfiguredServer", "SSAS_Shared_01"));

    Assert.True(result.IsFailure);
    Assert.DoesNotContain("primary.example", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("Integrated Security", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("Password", result.Error.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_route_type_exposes_no_secret_or_connection_property()
  {
    string[] forbidden = ["ConnectionString", "Password", "Username", "Secret", "Credential", "Endpoint", "Token"];

    foreach (var property in typeof(TenantDatabaseRoute).GetProperties())
    {
      Assert.DoesNotContain(forbidden, term =>
        property.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
  }

  private static TenantDatabaseConnectionFactory Factory()
  {
    var options = Options.Create(new TenantStorageOptions());
    options.Value.Servers["PrimarySqlServer"] = new TenantStorageServerOptions { ConnectionString = PrimaryConnectionString };
    options.Value.Servers["SecondarySqlServer"] = new TenantStorageServerOptions { ConnectionString = SecondaryConnectionString };
    return new TenantDatabaseConnectionFactory(options);
  }

  private static TenantDatabaseRoute Route(string serverKey, string databaseName) =>
    new(Guid.NewGuid(), 25, serverKey, databaseName,
      TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseStorageMode.Shared, 1, HealthyRoute);

  // The connection factory does not consult health — gating happens before it — so a verified-healthy
  // snapshot keeps these tests focused on trusted ServerKey lookup.
  private static readonly TenantDatabaseHealth HealthyRoute = new(
    TenantDatabaseConnectivityStatus.Healthy, null,
    TenantDatabaseSchemaCompatibilityStatus.UpToDate, null,
    TenantDatabaseMigrationExecutionStatus.Idle,
    TenantDatabaseMigrationManagementMode.AutomaticByPlatform, null, null);
}
