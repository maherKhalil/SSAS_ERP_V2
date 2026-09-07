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
  // ⚠ REPOSITORY-RELATIVE PATHS, NOT BARE NAMES (T-080). The walk covers all of `src/`, so a bare name
  // would pre-approve any module file that happened to share it. Ordinal comparison and `/` separators, so
  // the guard means the same thing on every machine.
  private static readonly string[] ApprovedQueryFilterBypassFiles =
  [
    "src/Platform/SSAS.Platform.Infrastructure/Persistence/Queries/AccessTokenClaimsProvider.cs",
    "src/Platform/SSAS.Platform.Infrastructure/Persistence/Queries/IdentityTenantMembershipReadService.cs",
    "src/Platform/SSAS.Platform.Infrastructure/Persistence/Queries/TenantAdministratorAuthority.cs",
    "src/Platform/SSAS.Platform.Infrastructure/Persistence/Repositories/TenantUserRepository.cs"
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
  // ⚠ THE PLANT RECORD BELOW WAS UPDATED WITH T-080, BECAUSE ALL THREE OF ITS CLAIMS WENT STALE IN ONE EDIT.
  // It read: *"pointing the root at `src/PlatformX` throws `DirectoryNotFoundException`, and changing the
  // pattern to `*.csx` — a directory that exists, matching nothing — reddens the comparison. The root cannot
  // vanish silently; the filter can."* **The root is no longer `src/Platform`, the matcher no longer reads
  // whole file text, and the comparison is no longer on bare names** — so a record citing those three is a
  // record of a test that no longer exists. *An anchor citing a conclusion is the first thing to rot.*
  // The reasoning it preserved is unchanged and is restated above: an exact-list comparison against a
  // non-empty expected value cannot pass over an empty walk.
  //
  // ==================================================================================================
  // ---- ⚠⚠⚠ T-080: THE NAME CLAIMED A CONFINEMENT AND THE POPULATION WAS ONE TREE OF FIVE.
  // ==================================================================================================
  //
  // This test is called `..._is_confined_...` and **names no tree**. Its grounds — *bypassing the tenant
  // filter is the one change that can silently widen a query across tenants* — carry no clause restricting
  // them to Platform. ***THE WALK NEVERTHELESS READ `src/Platform` ONLY, SO A BYPASS IN `src/Modules` WAS
  // INVISIBLE TO IT PERMANENTLY.*** Zero real bypasses existed there when this was widened — measured, with
  // comments excluded — **so this closed a hole rather than exposing a breach.** ⚠ Three module files
  // nonetheless DISCUSS `IgnoreQueryFilters` in comments, one warning that it *"silently removes it, turning
  // a scoped read into a tenant-wide one"*: module authors know the mechanism and the guard could not see
  // their files.
  //
  // ⚠⚠ **THE SAME DEFECT WAS FOUND THE SAME NIGHT IN AN UNRELATED FILE** — `No_employee_delete_operation_is_exposed`
  // matches four literal spellings of "delete" across two assemblies, so `SoftDeleteEmployeeAsync` is
  // invisible to a test whose name says no delete operation is exposed. ***A NAME CLAIMS A MECHANISM; A
  // PREDICATE MATCHES A VOCABULARY OR A SUBTREE; AND THE NAME IS WHAT EVERY LATER READER TRUSTS.***
  //
  // ---- ⚠⚠⚠ AND WIDENING THE WALK ALONE WOULD HAVE MOVED THE HOLE RATHER THAN CLOSING IT.
  //
  // The comparison was on `Path.GetFileName`. Over one tree that is unambiguous; over `src/` it is not.
  // ***A MODULE FILE NAMED `TenantUserRepository.cs` WOULD HAVE BEEN PRE-APPROVED BY NAME COLLISION*** — the
  // derived set and the expected set would agree, and a bypass nobody approved would ship green. **That is a
  // false-clear-by-collision replacing a false-clear-by-omission.** ⚠ Not hypothetical in this tree:
  // **seven basenames already duplicate across `src/`, led by `ServiceCollectionExtensions.cs` at NINE
  // copies, and three of the seven pairs are Application/Infrastructure twins** — exactly the shape a
  // repository or a claims provider forms. *None of the four approved names collides today, so this was a
  // hazard the widening would have CREATED.* **Hence repository-relative paths, not names.**
  //
  // ---- AND THE WALK EXCLUDES BUILD OUTPUT, WHICH IS NOT PEDANTRY.
  //
  // `EnumerateFiles` reads the filesystem, so widening the root also admits every module's `obj/`. **A
  // sibling instrument on this branch reported a wrong count on exactly this fault** — a grep over `tests/`
  // that matched compiled `.dll` and `.pdb` content. *A filesystem walk has no `.gitignore`; a `git`-based
  // one cannot make this mistake at all.*
  [Fact]
  public void Query_filter_bypass_is_confined_to_explicit_membership_eligibility_paths()
  {
    var repositoryRoot = FindRepositoryRoot();
    var bypasses = Directory
      .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
      .Where(path => !IsBuildOutput(repositoryRoot, path))
      .Where(path => MentionsOutsideComments(File.ReadAllLines(path), "IgnoreQueryFilters"))
      .Select(path => RepositoryRelative(repositoryRoot, path))
      .OrderBy(path => path, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(ApprovedQueryFilterBypassFiles, bypasses);
  }

  // ---- THE THREE HELPERS, SEPARATED SO THE FIXTURES BELOW EXERCISE THE SAME CODE THE GUARD RUNS.

  private static bool IsBuildOutput(string repositoryRoot, string path)
  {
    var relative = RepositoryRelative(repositoryRoot, path);

    return relative.Contains("/obj/", StringComparison.Ordinal) ||
      relative.Contains("/bin/", StringComparison.Ordinal);
  }

  // A COMMENT IS NOT A CALL. Without this, a file that merely WARNS about `IgnoreQueryFilters` — and three
  // module files do — enters the derived set and reddens an exact-set comparison for no reason.
  private static bool MentionsOutsideComments(IEnumerable<string> lines, string token) =>
    lines.Any(line =>
      line.Contains(token, StringComparison.Ordinal) &&
      !line.TrimStart().StartsWith("//", StringComparison.Ordinal));

  private static string RepositoryRelative(string repositoryRoot, string path) =>
    Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');

  // ---- ⚠ THE REVERSE BIND FOR THE PATH COMPARISON. Without this, change 3 ships unproven.
  //
  // A bypass in a module file whose BASENAME matches an approved Platform file must not be cleared. Under
  // the old `Path.GetFileName` projection it would have been; under repository-relative paths it cannot be,
  // and this asserts the difference rather than trusting it.
  [Fact]
  public void A_module_file_sharing_an_approved_basename_is_not_pre_approved()
  {
    const string root = "/repo";
    var approved = RepositoryRelative(root, "/repo/src/Platform/SSAS.Platform.Infrastructure/Persistence/Repositories/TenantUserRepository.cs");
    var impostor = RepositoryRelative(root, "/repo/src/Modules/HR/SSAS.HR.Infrastructure/Persistence/TenantUserRepository.cs");

    Assert.Equal(Path.GetFileName(approved), Path.GetFileName(impostor));
    Assert.NotEqual(approved, impostor);
    Assert.DoesNotContain(impostor, ApprovedQueryFilterBypassFiles);
  }

  [Fact]
  public void A_commented_mention_is_not_a_bypass_and_a_real_call_is()
  {
    Assert.False(MentionsOutsideComments(["// IgnoreQueryFilters() removes the tenant filter"], "IgnoreQueryFilters"));
    Assert.True(MentionsOutsideComments(["    var all = set.IgnoreQueryFilters().ToList();"], "IgnoreQueryFilters"));
  }

  [Fact]
  public void Build_output_is_excluded_from_the_bypass_walk()
  {
    Assert.True(IsBuildOutput("/repo", "/repo/src/Platform/X/obj/Debug/net8.0/Generated.cs"));
    Assert.True(IsBuildOutput("/repo", "/repo/src/Modules/HR/Y/bin/Debug/net8.0/Thing.cs"));
    Assert.False(IsBuildOutput("/repo", "/repo/src/Platform/X/Persistence/Real.cs"));
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
