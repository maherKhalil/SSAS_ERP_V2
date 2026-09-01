namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY DEPARTMENT READ COMPOSES ITS OWN TENANT AND COMPANY PREDICATE (253, `AC-DEP-0044`).
// ==================================================================================================
//
// `AC-DEP-0044` says every department query composes an explicit tenant and company predicate, that none
// relies on a global filter alone, AND THAT AN ARCHITECTURE GUARD ASSERTS IT. The guard is the half that
// did not exist: `DepartmentApplicationArchitectureTests` proves every read TAKES a scope
// (`AC-DEP-0045`), and its own comment already draws the distinction this file closes — TAKING A SCOPE IS
// NOT APPLYING IT. A read could accept a `DepartmentReadScope`, ignore it entirely, and pass that guard.
//
// The model is `EmployeeReadScopeArchitectureTests`, which asserts the same two things for Employee.
//
// ---- ⚠ THE PREDICATE DOES NOT NAME THE ASSERTION.
//
// Membership is REACHES A DEPARTMENT ENTITY SET — a mechanism, decided by `Set<Department>()` in the
// source. The assertion is COMPOSES AN EXPLICIT PREDICATE AND DOES NOT BYPASS THE FILTERS. Had the
// population been "reads that carry a company predicate", A READ THAT OMITTED THE PREDICATE WOULD SIMPLY
// LEAVE THE POPULATION — the guard would be strongest exactly where nothing was wrong, and blind to the
// only defect it exists to catch. That self-selecting shape was caught in 248 and is recorded there.
//
// ---- ⚠⚠ WHAT THIS CANNOT COVER, AND IT IS THE BOUND THAT ALREADY COST US ONCE.
//
// AN ARCHITECTURE GUARD ASSERTS THE SHAPE OF A QUERY. IT NEVER ASSERTS THAT THE SHAPE WAS APPLIED AT
// RUNTIME. This reads source text: it proves the predicate is WRITTEN, not that the resulting SQL filtered
// any row. A total, cheap, entirely passing scope guard of exactly this kind sat above two read services
// that were broken, because no test ever CONSTRUCTED them — untested and wrong are correlated, and a
// source-text guard cannot break that correlation. The behavioural cover is the SQL suite's business, and
// this file is not evidence about it.
//
// ---- COMMENTS ARE STRIPPED BEFORE ANYTHING IS ASSERTED.
//
// ⚠ `DepartmentReadService` opens with a comment containing `TenantId = @tenant AND CompanyId IN
// (@companies)`. A guard that searched raw text could be satisfied by that PROSE while the query below it
// composed nothing at all — the same mirror `JournalPostingOrderTests` and `FiscalPeriodStateWriterFenceTests`
// both strip against.
public sealed class DepartmentReadScopeArchitectureTests
{
  private const string ReadService = "DepartmentReadService.cs";

  [Fact]
  [Trait("Decision", "ADR-026")]
  [Trait("Criterion", "AC-DEP-0044")]
  public void Every_department_read_composes_an_explicit_tenant_and_company_predicate()
  {
    var readPaths = DepartmentReadPaths();

    // ANTI-VACUITY. If the derivation stops matching — the service is renamed, moved, or the entity set is
    // reached some other way — every assertion below holds trivially over an empty set and this guard
    // reports success while asserting nothing.
    Assert.True(
      readPaths.Count >= 1,
      "no source that reaches a Department entity set was found at all — the derivation has stopped " +
      "matching, and this guard is asserting nothing rather than passing");

    var source = readPaths.Single(path => path.Key.EndsWith(ReadService, StringComparison.Ordinal)).Value;

    // ONE ENTRY POINT. A second `Set<Department>()` is how a read comes to be written without the
    // predicate — it would not have to bypass the scoped query, only to never call it.
    Assert.Equal(1, CountOccurrences(source, "Set<Department>()"));

    var scoped = source[source.IndexOf(
      "private static IQueryable<Department> Scoped(", StringComparison.Ordinal)..];

    Assert.Contains("department.TenantId == scope.TenantId", scoped, StringComparison.Ordinal);
    Assert.Contains("scope.Companies.CompanyIds.Contains", scoped, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  [Trait("Criterion", "AC-DEP-0044")]
  public void No_department_read_bypasses_the_filters_it_does_have()
  {
    var readPaths = DepartmentReadPaths();

    Assert.True(
      readPaths.Count >= 1,
      "no source that reaches a Department entity set was found at all — the derivation has stopped " +
      "matching, and this guard is asserting nothing rather than passing");

    // Collected rather than asserted one at a time: a failure should name every offender, not the first.
    var bypassing = readPaths
      .Where(path => path.Value.Contains("IgnoreQueryFilters", StringComparison.Ordinal))
      .Select(path => Path.GetFileName(path.Key))
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      bypassing.Length == 0,
      $"these reach a Department entity set and call IgnoreQueryFilters: {string.Join(", ", bypassing)}. " +
      "One call turns a scoped read into a tenant-wide one, which is the failure the explicit predicates " +
      "exist to make impossible to write by accident.");
  }

  // A department read path is any HR production source that reaches a Department entity set. That is the
  // MECHANISM — not a name, not a folder, and not whether it already carries a predicate.
  private static Dictionary<string, string> DepartmentReadPaths()
  {
    var paths = new Dictionary<string, string>(StringComparer.Ordinal);

    foreach (var path in Directory.EnumerateFiles(
      Path.Combine(RepositoryRoot(), "src", "Modules", "HR"), "*.cs", SearchOption.AllDirectories))
    {
      if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
          path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      {
        continue;
      }

      var source = StripComments(File.ReadAllText(path));

      if (source.Contains("Set<Department>()", StringComparison.Ordinal) ||
          source.Contains("Set<DepartmentManager>()", StringComparison.Ordinal))
      {
        paths[path] = source;
      }
    }

    return paths;
  }

  private static string StripComments(string source) =>
    string.Join(
      Environment.NewLine,
      source
        .Split('\n')
        .Select(line =>
        {
          var comment = line.IndexOf("//", StringComparison.Ordinal);
          return comment >= 0 ? line[..comment] : line;
        }));

  private static int CountOccurrences(string source, string value)
  {
    var count = 0;
    for (var index = source.IndexOf(value, StringComparison.Ordinal);
      index >= 0;
      index = source.IndexOf(value, index + value.Length, StringComparison.Ordinal))
    {
      count++;
    }

    return count;
  }

  private static string RepositoryRoot()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
         directory is not null;
         directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new InvalidOperationException("Repository root not found.");
  }
}
