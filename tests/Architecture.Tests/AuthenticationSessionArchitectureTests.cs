using System.Reflection;
using System.Text.RegularExpressions;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Authentication;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Authentication;

namespace SSAS.Architecture.Tests;

[Trait("Scenario", "TS-AUTH-0070")]
[Trait("Scenario", "TS-AUTH-0071")]
[Trait("Scenario", "TS-AUTH-0073")]
[Trait("Scenario", "TS-AUTH-0090")]
[Trait("Acceptance", "AC-AUTH-0035")]
public sealed class AuthenticationSessionArchitectureTests
{
  private static readonly string[] ApprovedQueryFilterBypassFiles =
    ["IdentityTenantMembershipReadService.cs", "TenantUserRepository.cs"];

  [Fact]
  public void Session_repositories_are_narrow_and_expose_no_delete_or_queryable_boundary()
  {
    var repositoryTypes = new[]
    {
      typeof(IAuthenticationSessionRepository),
      typeof(ITenantSelectionTransactionRepository)
    };

    Assert.All(repositoryTypes, repositoryType =>
    {
      Assert.False(repositoryType.IsGenericType);
      Assert.DoesNotContain(repositoryType.GetMethods(), method =>
        Regex.IsMatch(method.Name, "Delete|Remove", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
      Assert.DoesNotContain(repositoryType.GetMethods(), method =>
        method.ReturnType.ToString().Contains("IQueryable", StringComparison.Ordinal));
    });

    Assert.Null(typeof(IAuthenticationSessionRepository).Assembly.GetType(
      "SSAS.Platform.Application.Abstractions.Persistence.IRefreshTokenRecordRepository"));
  }

  [Fact]
  public void Global_pre_tenant_authentication_records_are_not_tenant_owned()
  {
    Assert.False(typeof(ITenantOwnedEntity).IsAssignableFrom(typeof(AuthenticationSession)));
    Assert.False(typeof(ITenantOwnedEntity).IsAssignableFrom(typeof(TenantSelectionTransaction)));
    Assert.False(typeof(ITenantOwnedEntity).IsAssignableFrom(typeof(RefreshTokenRecord)));
  }

  [Fact]
  public void Refresh_tokens_and_selection_proofs_cross_outputs_only_as_sensitive_wrappers()
  {
    Assert.Equal(typeof(SensitiveRefreshToken), typeof(SessionCreated).GetProperty("RefreshToken")?.PropertyType);
    Assert.Equal(typeof(SensitiveRefreshToken), typeof(RefreshSucceeded).GetProperty("RefreshToken")?.PropertyType);
    Assert.Equal(
      typeof(SensitiveTenantSelectionProof),
      typeof(TenantSelectionRequired).GetProperty("SelectionProof")?.PropertyType);

    var outputTypes = new[] { typeof(SessionCreated), typeof(RefreshSucceeded), typeof(TenantSelectionRequired) };
    var ordinaryStrings = outputTypes.SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
      .Where(property => property.PropertyType == typeof(string))
      .Where(property => Regex.IsMatch(property.Name, "Token|Proof|Secret|Hash|Raw", RegexOptions.IgnoreCase))
      .Select(property => $"{type.Name}.{property.Name}"));

    Assert.Empty(ordinaryStrings);
  }

  [Fact]
  public void Query_filter_bypass_is_confined_to_explicit_membership_eligibility_paths()
  {
    var repositoryRoot = FindRepositoryRoot();
    var bypasses = Directory
      .EnumerateFiles(Path.Combine(repositoryRoot, "src", "Platform"), "*.cs", SearchOption.AllDirectories)
      .Where(path => File.ReadAllText(path).Contains("IgnoreQueryFilters", StringComparison.Ordinal))
      .Select(Path.GetFileName)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(ApprovedQueryFilterBypassFiles, bypasses);
  }

  [Fact]
  public void Session_migration_is_scoped_and_contains_append_only_database_guards()
  {
    var migration = Directory.EnumerateFiles(
        Path.Combine(FindRepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence", "Migrations"),
        "*AddAuthenticationSessionsAndTenantSelection.cs")
      .Single(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal));
    var source = File.ReadAllText(migration);

    Assert.Equal(3, Regex.Matches(source, "migrationBuilder.CreateTable", RegexOptions.CultureInvariant).Count);
    Assert.DoesNotContain("AddColumn", source, StringComparison.Ordinal);
    Assert.DoesNotContain("AddForeignKey", source, StringComparison.Ordinal);
    Assert.DoesNotContain("InsertData", source, StringComparison.Ordinal);
    Assert.Contains("TR_AuthenticationSessions_PreventDelete", source, StringComparison.Ordinal);
    Assert.Contains("TR_RefreshTokenRecords_PreventDelete", source, StringComparison.Ordinal);
    Assert.Contains("TR_TenantSelectionTransactions_PreventDelete", source, StringComparison.Ordinal);
  }

  private static string FindRepositoryRoot()
  {
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln"))) return directory.FullName;
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}
