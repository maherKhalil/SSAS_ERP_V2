using System.Reflection;
using SSAS.Host.API.Authorization;
using SSAS.Platform.API.Transport;

namespace SSAS.Architecture.Tests;

// Durable structural rules for the shared Platform admin HTTP transport foundation.
public sealed class AdminTransportArchitectureTests
{
  [Fact]
  public void RowVersion_codec_is_neutral_and_lives_in_the_platform_api_transport_namespace()
  {
    Assert.Equal("SSAS.Platform.API.Transport", typeof(RowVersionCodec).Namespace);
  }

  [Fact]
  public void No_duplicate_or_feature_specific_rowversion_codec_remains()
  {
    var codecs = typeof(RowVersionCodec).Assembly.GetTypes()
      .Where(type => type.Name.Contains("RowVersionCodec", StringComparison.Ordinal))
      .Select(type => type.FullName)
      .ToArray();

    Assert.Equal(["SSAS.Platform.API.Transport.RowVersionCodec"], codecs);
  }

  [Fact]
  public void Platform_api_does_not_reference_infrastructure_persistence_or_ef_directly()
  {
    var forbidden = new[]
    {
      "SSAS.Platform.Infrastructure",
      "Microsoft.EntityFrameworkCore",
      "Microsoft.Data.SqlClient"
    };
    var violations = typeof(RowVersionCodec).Assembly.GetReferencedAssemblies()
      .Where(reference => forbidden.Any(prefix => reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true))
      .Select(reference => reference.Name)
      .ToArray();

    Assert.Empty(violations);
  }

  [Fact]
  public void RequirePermission_convention_matches_the_host_policy_prefix()
  {
    Assert.Equal(PermissionAuthorizationDefaults.PolicyPrefix, AdminAuthorizationConventions.PermissionPolicyPrefix);
  }

  [Fact]
  public void Identity_access_endpoint_builder_has_no_persistence_or_query_filter_leakage()
  {
    var source = ReadPlatformApiSource(Path.Combine("IdentityAccess", "IdentityAccessEndpointRouteBuilderExtensions.cs"));

    Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
    Assert.DoesNotContain("DbSet", source, StringComparison.Ordinal);
    Assert.DoesNotContain("EntityFrameworkCore", source, StringComparison.Ordinal);
    Assert.DoesNotContain("IgnoreQueryFilters", source, StringComparison.Ordinal);
  }

  [Fact]
  public void Identity_access_transport_contracts_never_carry_a_tenant_id()
  {
    var source = ReadPlatformApiSource(Path.Combine("IdentityAccess", "IdentityAccessTransportContracts.cs"));

    Assert.DoesNotContain("TenantId", source, StringComparison.Ordinal);
  }

  private static string ReadPlatformApiSource(string relativePath) => File.ReadAllText(Path.Combine(
    FindRepositoryRoot(), "src", "Platform", "SSAS.Platform.API", relativePath));

  private static string FindRepositoryRoot()
  {
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}
