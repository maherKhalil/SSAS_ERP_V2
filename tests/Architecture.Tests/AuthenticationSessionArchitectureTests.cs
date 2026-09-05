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
// ---- ⚠ `AC-AUTH-0035` MOVED DOWN TO THE ONE METHOD THAT BEARS ON IT.
//
// At class scope it claimed the criterion of all six methods here — five of which are about repository
// shape, tenant ownership, query-filter bypass and two migrations, and none of which mentions a refresh
// token. **Moving it strictly reduces the over-claim**, and what the single method does and does not reach
// is stated at that method rather than left for a reader to work out from the class.
public sealed class AuthenticationSessionArchitectureTests
{
  // Admitted ONE AT A TIME BY DECISION, never by pattern: bypassing the tenant filter is the one change
  // that can silently widen a query across tenants, so a new file appearing here must be argued for.
  //
  // TenantAdministratorAuthority.cs joined in Branch foundation B1a for the same reason the membership
  // paths did: it resolves tenant-administrator authority for AUTHENTICATION, which asks before any
  // ambient tenant context exists. Left filtered it would answer "not an administrator" for everyone.
  // It states the tenant explicitly in every clause instead, so nothing is widened.
  private static readonly string[] ApprovedQueryFilterBypassFiles =
  [
    "AccessTokenClaimsProvider.cs", "IdentityTenantMembershipReadService.cs",
    "TenantAdministratorAuthority.cs", "TenantUserRepository.cs"
  ];

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

  // `AC-AUTH-0035` — *"Raw refresh tokens and tenant-selection proofs are reveal-once sensitive results and
  // never enter persistence, ordinary DTOs, command representations, logs, telemetry, exceptions, or
  // events."* **Seven destinations, and this method reaches ONE of them.**
  //
  // *ORDINARY DTOs* is witnessed structurally: the three output types are asserted to carry
  // `SensitiveRefreshToken`/`SensitiveTenantSelectionProof` rather than `string`, and then swept for any
  // remaining `string` property whose name matches `Token|Proof|Secret|Hash|Raw`. **The type assertions are
  // the claim; the sweep is what stops a fourth property being added beside them.**
  //
  // ⚠⚠⚠ **NOT WITNESSED HERE: persistence, command representations, logs, telemetry, exceptions, events.**
  // *Named rather than left for a reader to discover, because a criterion id on a green test is read as the
  // whole criterion proven.* **The reveal-once half is carried by
  // `AuthenticationSessionDomainTests.Refresh_token_is_exactly_formatted_reveal_once_and_redacted`** — which
  // holds the criterion's other clause and, under this convention, cannot say so.
  //
  // ⚠ And the LOGS clause is the one worth building: it is not structurally out of reach the way the others
  // are — a source walk asserting no logger call takes a raw token is constructible here. **Recorded as
  // buildable, not built.**
  [Fact]
  [Trait("Acceptance", "AC-AUTH-0035")]
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

  // ---- ⚠ THIS TEST NEEDS NO FLOOR, AND THE REASON IS WORTH KNOWING (T-248).
  //
  // An audit listed it as a text scan with neither a floor nor a plant. **It turned out to need only the
  // plant**: `Assert.Equal(ApprovedQueryFilterBypassFiles, bypasses)` compares against a NON-EMPTY expected
  // list, so a walk that finds nothing produces an empty array and the comparison fails.
  // **An exact-list assertion is anti-vacuous by construction** in a way `Assert.Empty` never is.
  //
  // Planted both ways rather than argued: pointing the root at `src/PlatformX` throws
  // `DirectoryNotFoundException`, and changing the pattern to `*.csx` — a directory that exists, matching
  // nothing — reddens the comparison. **The root cannot vanish silently; the filter can; and this
  // assertion catches the filter anyway.**
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

  [Fact]
  public void User_logout_migration_changes_only_the_existing_revocation_reason_constraint()
  {
    var migration = Directory.EnumerateFiles(
        Path.Combine(FindRepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence", "Migrations"),
        "*AddUserLogoutSessionRevocationReason.cs")
      .Single(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal));
    var source = File.ReadAllText(migration);

    Assert.Equal(2, Regex.Matches(source, "DropCheckConstraint", RegexOptions.CultureInvariant).Count);
    Assert.Equal(2, Regex.Matches(source, "AddCheckConstraint", RegexOptions.CultureInvariant).Count);
    Assert.Contains("UserLogout", source, StringComparison.Ordinal);
    Assert.DoesNotContain("CreateTable", source, StringComparison.Ordinal);
    Assert.DoesNotContain("AddColumn", source, StringComparison.Ordinal);
    Assert.DoesNotContain("DropColumn", source, StringComparison.Ordinal);
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
