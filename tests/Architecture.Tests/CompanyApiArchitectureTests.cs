using SSAS.BuildingBlocks.Api.Transport;
using SSAS.Platform.API.Transport;

namespace SSAS.Architecture.Tests;

// Durable structural rules for the Company HTTP transport slice.
public sealed class CompanyApiArchitectureTests
{
  [Fact]
  public void Company_transport_contracts_carry_no_owning_tenant_or_company_id_input()
  {
    var source = ReadCompanyApiSource("CompanyTransportContracts.cs");

    Assert.DoesNotContain("TenantId", source, StringComparison.Ordinal);
    // The request DTO must not accept a client-supplied company id.
    Assert.DoesNotMatch(@"CreateCompanyRequest\([^)]*CompanyId", source);
  }

  [Fact]
  public void Company_route_builder_has_no_persistence_or_query_filter_leakage()
  {
    var source = ReadCompanyApiSource("CompanyEndpointRouteBuilderExtensions.cs");

    Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
    Assert.DoesNotContain("DbSet", source, StringComparison.Ordinal);
    Assert.DoesNotContain("EntityFrameworkCore", source, StringComparison.Ordinal);
    Assert.DoesNotContain("IgnoreQueryFilters", source, StringComparison.Ordinal);
  }

  [Fact]
  public void Company_route_builder_exposes_no_delete_reactivate_restore_or_suspend_route()
  {
    var source = ReadCompanyApiSource("CompanyEndpointRouteBuilderExtensions.cs");

    Assert.DoesNotContain("MapDelete", source, StringComparison.Ordinal);
    foreach (var forbidden in new[] { "reactivate", "restore", "suspend" })
    {
      Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
    }
  }

  [Fact]
  public void Company_api_uses_no_localization_transport_types()
  {
    foreach (var file in new[]
      {
        "CompanyTransportContracts.cs",
        "CompanyEndpointRouteBuilderExtensions.cs",
        "CompanyApiErrorMapper.cs"
      })
    {
      Assert.DoesNotContain("Localization", ReadCompanyApiSource(file), StringComparison.Ordinal);
    }
  }

  [Fact]
  public void Company_api_reuses_the_shared_transport_primitives_and_stays_in_the_api_layer()
  {
    // The Company transport contracts live in the Platform API assembly alongside the shared codec.
    // Anchored on a PLATFORM-owned transport type. RowVersionCodec no longer identifies this assembly: it
    // moved to the shared API project in FP-006C5, and anchoring on it would silently retarget this test.
    var apiAssembly = typeof(ProblemResults).Assembly;
    var contract = apiAssembly.GetType("SSAS.Platform.API.Companies.CreateCompanyRequest");
    Assert.NotNull(contract);
    Assert.Equal("SSAS.Platform.API", apiAssembly.GetName().Name);

    // No company-owned-entity abstraction is introduced by the transport layer.
    Assert.DoesNotContain(apiAssembly.GetTypes(), type => type.Name.Contains("ICompanyOwnedEntity", StringComparison.Ordinal));
  }

  private static string ReadCompanyApiSource(string fileName) => File.ReadAllText(Path.Combine(
    FindRepositoryRoot(), "src", "Platform", "SSAS.Platform.API", "Companies", fileName));

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
