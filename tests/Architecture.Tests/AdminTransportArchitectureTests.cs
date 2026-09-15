using SSAS.BuildingBlocks.Api.Transport;
using System.Reflection;
using SSAS.Host.API.Authorization;
using SSAS.Platform.API.Transport;

namespace SSAS.Architecture.Tests;

// Durable structural rules for the shared Platform admin HTTP transport foundation.
public sealed class AdminTransportArchitectureTests
{
  [Fact]
  public void RowVersion_codec_is_neutral_and_lives_in_the_shared_api_transport_namespace()
  {
    // It moved to the shared API project in FP-006C5 so HR could use the SAME codec: a module-owned codec
    // would have meant two rowversion encodings on one wire format.
    Assert.Equal("SSAS.BuildingBlocks.Api.Transport", typeof(RowVersionCodec).Namespace);
  }

  [Fact]
  public void No_duplicate_or_feature_specific_rowversion_codec_remains()
  {
    var codecs = typeof(RowVersionCodec).Assembly.GetTypes()
      .Where(type => type.Name.Contains("RowVersionCodec", StringComparison.Ordinal))
      .Select(type => type.FullName)
      .ToArray();

    Assert.Equal(["SSAS.BuildingBlocks.Api.Transport.RowVersionCodec"], codecs);
  }

  [Fact]
  public void Platform_api_does_not_reference_infrastructure_persistence_or_ef_directly()
  {
    // ⚠⚠ THE THREE BANNED PREFIXES SPLIT INTO TWO KINDS, AND THE SPLIT IS STRUCTURAL (272).
    //
    // DECLARABLE: a `ProjectReference` and a `PackageReference` this repository really does declare
    // elsewhere, so DECLARED is the stronger reading — it catches the capability at merge time, before any
    // type is used.
    var declarable = new[]
    {
      "SSAS.Platform.Infrastructure",
      // The shared transport project must not drag persistence in either.
      "Microsoft.EntityFrameworkCore"
    };

    // ⚠⚠⚠ TRANSITIVE ONLY: `Microsoft.Data.SqlClient` reaches this tree through
    // `EntityFrameworkCore.SqlServer` and appears in NO `.csproj` of ours. **A declared check on it would
    // pass vacuously**, so emitted is the correct instrument for this branch and that is a decision rather
    // than an omission.
    var transitiveOnly = new[] { "Microsoft.Data.SqlClient" };

    var forbidden = declarable.Concat(transitiveOnly).ToArray();

    // One exercise per declarable branch, against projects that legitimately declare each.
    //
    // ⚠ DERIVED FROM `declarable` RATHER THAN RESTATED BESIDE IT (278) — a control that hardcodes its terms
    // cannot notice a term ADDED to the ban, which would then hold over a prefix nothing witnesses. The
    // inline `StartsWith` stays: see the control section in `DeclaredDependencies` for why only narrowing
    // a match is silent and widening it is loud.
    var witnessOf = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      ["SSAS.Platform.Infrastructure"] = "SSAS.Host.API",
      ["Microsoft.EntityFrameworkCore"] = "SSAS.BuildingBlocks.Infrastructure"
    };

    Assert.All(declarable, term =>
    {
      Assert.True(witnessOf.TryGetValue(term, out var witness),
        $"'{term}' is banned but no project is named as its declared witness. Add one, or move the term " +
        "to `transitiveOnly` with grounds — an unwitnessed term bans nothing and reads as coverage.");
      Assert.Contains(
        DeclaredDependencies.Of(witness!), name => name.StartsWith(term, StringComparison.Ordinal));
    });

    var violations = typeof(RowVersionCodec).Assembly.GetReferencedAssemblies()
      .Where(reference => forbidden.Any(prefix => reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true))
      .Select(reference => reference.Name)
      .ToArray();

    // The emitted read sees something, so an empty violation set means "none of the three" rather than
    // "no references read" — the control the transitive branch depends on, having no declared witness.
    Assert.NotEmpty(typeof(RowVersionCodec).Assembly.GetReferencedAssemblies());

    Assert.Empty(violations);

    Assert.DoesNotContain(
      DeclaredDependencies.Of(typeof(RowVersionCodec).Assembly),
      name => declarable.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));
  }

  [Fact]
  public void RequirePermission_convention_matches_the_host_policy_prefix()
  {
    Assert.Equal(PermissionAuthorizationDefaults.PolicyPrefix, SSAS.BuildingBlocks.Api.Authorization.PermissionPolicyNames.TenantPrefix);
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
