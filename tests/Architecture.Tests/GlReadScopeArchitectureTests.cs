using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY GL READ OF A COMPANY-OWNED ENTITY COMPOSES AN EXPLICIT COMPANY PREDICATE (255).
// ==================================================================================================
//
// The obligation is `Architecture-Principles.md`, "A scope predicate is owed by the entity's ownership
// classification, not by the module's preference". ⚠ THERE IS NO `[Trait("Criterion", ...)]` ON THIS FILE
// AND ITS ABSENCE IS A DECISION, NOT A GAP: `AC-DEP-0044` exists only because FP-007 happened to write one,
// and minting a per-module criterion for each of the seven read services is exactly what a derived
// obligation removes the need for. THE OBLIGATION IS ARCHITECTURAL, NOT PER-FEATURE.
//
// ---- WHY COMPANY AND NOT TENANT.
//
// Tenant is carried by a GLOBAL QUERY FILTER — `PersistenceDbContext.ConfigureTenantFilter` applies it to
// every `ITenantOwnedEntity`, so a read that omits an explicit tenant predicate is still tenant-scoped.
// THERE IS NO EQUIVALENT COMPANY FILTER, deliberately (`ADR-025` decision 10), so the company predicate is
// owed EXPLICITLY BY THE QUERY or it does not exist at all.
//
// ---- ⚠⚠ THE POPULATION IS DERIVED FROM OWNERSHIP, WHICH IS WHAT KEEPS IT FROM FIRING FALSELY.
//
// `Account` and `JournalLine` are `ITenantOwnedEntity` ONLY — the chart of accounts is tenant-wide — so
// their reads owe NO company predicate and correctly have none. A guard that demanded one of every GL read
// would be wrong about them on its first run, and A FALSE ALARM ON A SCOPE GUARD IS HOW A SCOPE GUARD GETS
// SWITCHED OFF. Membership here is decided by `ICompanyOwnedEntity`, read off the type.
//
// ---- ⚠ ANCHORED ON THE COLUMN, NEVER ON THE LOCAL VARIABLE THAT PRODUCES IT.
//
// The two existing scope guards match `scope.Companies.CompanyIds.Contains`. `AttendanceReadService` binds
// its scope as `resolved.Value` and `readScope`, and `GlReadService` as plain `scope` with a FLAT
// `scope.CompanyIds` rather than `scope.Companies.CompanyIds` — so a guard copied from those would have
// fired falsely here on day one. This matches `.CompanyId`, THE COLUMN THE QUERY MUST PRODUCE, because
// that is the thing the rule is actually about.
//
// ---- ⚠⚠⚠ WHAT THIS CANNOT COVER.
//
// AN ARCHITECTURE GUARD ASSERTS THE SHAPE OF A QUERY AND NEVER THAT THE SHAPE WAS APPLIED AT RUNTIME. It
// proves the predicate is written, not that the SQL filtered a row. `GlReadService` is the service that
// carried two live production defects found because nothing had ever CONSTRUCTED it — untested and wrong
// are correlated, and a source-text guard cannot break that correlation. The behavioural cover is the SQL
// suite's business and this file is not evidence about it.
public sealed class GlReadScopeArchitectureTests
{
  private const string ReadService =
    "src/Modules/Finance/SSAS.GL.Infrastructure/Persistence/GlReadService.cs";

  [Fact]
  [Trait("Decision", "ADR-025")]
  public void Every_gl_read_of_a_company_owned_entity_composes_an_explicit_company_predicate()
  {
    var source = StripComments(File.ReadAllText(Path.Combine(
      FindRepositoryRoot(), ReadService.Replace('/', Path.DirectorySeparatorChar))));

    var reads = EntitySetReads(source);

    // ANTI-VACUITY. A derivation that stopped matching `Set<T>()` would find no reads and every assertion
    // below would hold trivially — the guard reporting success over nothing.
    Assert.True(
      reads.Count >= 5,
      $"only {reads.Count} entity-set reads were found in the GL read service; the derivation has stopped " +
      "matching and this guard is asserting nothing rather than passing");

    // ⚠ AND THE POPULATION UNDER TEST IS THE COMPANY-OWNED SUBSET, WHICH MUST NOT BE EMPTY EITHER. If no
    // read resolved to a company-owned type, the offender list below is empty for the wrong reason.
    var companyOwned = reads
      .Where(read => IsCompanyOwned(read.Entity))
      .ToArray();

    Assert.True(
      companyOwned.Length >= 1,
      "no read of a company-owned entity was found at all — either the ownership lookup has broken or the " +
      "service no longer reads one, and in both cases this guard is vacuous");

    var unscoped = companyOwned
      .Where(read => !read.Query.Contains(".CompanyId", StringComparison.Ordinal))
      .Select(read => $"{read.Entity} at offset {read.Offset}")
      .ToArray();

    Assert.True(
      unscoped.Length == 0,
      $"these read a company-owned entity with no explicit company predicate: {string.Join(", ", unscoped)}. " +
      "There is no global company filter, so such a read is scoped by the tenant filter alone and returns " +
      "every company's rows.");
  }

  // A read is `Set<T>()` plus the query chain that follows it, up to the statement terminator. That is the
  // MECHANISM — not a method name, and not whether the query already looks scoped.
  private static List<(string Entity, string Query, int Offset)> EntitySetReads(string source)
  {
    var reads = new List<(string, string, int)>();

    foreach (Match match in Regex.Matches(source, @"Set<(\w+)>\(\)"))
    {
      var start = match.Index;
      var end = source.IndexOf(';', start);
      var query = end < 0 ? source[start..] : source[start..end];

      reads.Add((match.Groups[1].Value, query, start));
    }

    return reads;
  }

  // Ownership is read off the TYPE, so the obligation cannot drift from what the entity declares.
  //
  // ⚠ THE DIRECTORY IS READ RATHER THAN `AppDomain.CurrentDomain.GetAssemblies()`, AND THE FIRST VERSION OF
  // THIS FILE GOT THAT WRONG. The domain assembly is not loaded until something touches it, so the lookup
  // resolved NOTHING and every entity read as not-company-owned. The offender list was then empty for the
  // wrong reason — and only the second floor below caught it. Reading the directory finds every assembly
  // the project references, loaded or not, which is the same fix `TenantModelEntityCountArchitectureTests`
  // records for this mechanism.
  private static readonly Lazy<Type[]> DomainTypes = new(() => Directory
    .EnumerateFiles(AppContext.BaseDirectory, "SSAS.*.dll")
    .Select(LoadOrNull)
    .Where(assembly => assembly is not null)
    .SelectMany(assembly => SafeTypes(assembly!))
    .ToArray());

  private static bool IsCompanyOwned(string entityName)
  {
    var type = DomainTypes.Value.FirstOrDefault(candidate => candidate.Name == entityName);

    return type is not null && typeof(ICompanyOwnedEntity).IsAssignableFrom(type);
  }

  private static System.Reflection.Assembly? LoadOrNull(string path)
  {
    try
    {
      return System.Reflection.Assembly.LoadFrom(path);
    }
    catch (BadImageFormatException)
    {
      return null;
    }
    catch (FileLoadException)
    {
      return null;
    }
  }

  private static IEnumerable<Type> SafeTypes(System.Reflection.Assembly assembly)
  {
    try
    {
      return assembly.GetTypes();
    }
    catch (System.Reflection.ReflectionTypeLoadException loaded)
    {
      return loaded.Types.Where(type => type is not null)!;
    }
  }

  // ⚠ Comments are stripped first. This service's own prose names `CompanyId` repeatedly, and a guard over
  // raw text would be satisfied by that while the query composed nothing — the mirror every other guard in
  // this suite strips against.
  private static string StripComments(string source) =>
    string.Join(
      '\n',
      source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

  private static string FindRepositoryRoot()
  {
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
         directory is not null;
         directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}
