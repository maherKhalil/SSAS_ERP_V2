using System.Reflection;
using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// PERSISTENCE STAYS BEHIND THE INFRASTRUCTURE BOUNDARY (T-246).
// ==================================================================================================
//
// ---- ⚠ THIS FILE WAS PROVED TO PASS WHILE MEASURING NOTHING, AND THAT IS WHY IT LOOKS LIKE THIS NOW.
//
// Every test here was `Assert.Empty` over a file walk with **no floor anywhere**. Renaming the one path
// segment the walk filters on — `src` to `sources`, a single plausible layout change — made the walk return
// nothing and **all nine tests passed.** Three real architectural rules were being defended by an
// instrument that could not notice its own absence.
//
// **The failure needed no bug.** A file walk that finds nothing returns an empty set, an empty set contains
// no violations, and no violations is exactly what success looks like. **A green guard produces no prompt
// to ask whether it is measuring anything**, which is why this survived unexamined.
//
// ---- TWO OF THESE WERE NEVER TEXT QUESTIONS, AND THOSE ARE NOW ASKED OF THE COMPILED CODE.
//
// *"Domain and Application remain Entity Framework free"* is an **assembly-reference** question, and
// *"no `IQueryable` on Application boundaries"* is a question about **public signatures**. Reflection
// answers both exactly where text answered them approximately — and, decisively, **reflection cannot fail
// the way the file walk did**: an assembly that cannot be loaded throws, where a directory that matches
// nothing returns empty and reads as success.
//
// That is the general preference this file now embodies: **remove a failure mode rather than detect it.**
// Where the question really is about source text — a naming pattern, a call shape — the scan stays and
// carries a floor instead.
//
// ---- ⚠ PLANT RECORD, WRITTEN HERE RATHER THAN LEFT IN A COMMIT MESSAGE.
//
// An audit of this repository's text-scanning guards found 3 of 5 recorded plants were visible ONLY in git
// history. **A property that can only be established by archaeology stops being established**: the next
// reader sees a green assertion and no reason to trust it, which is how this file reached five unexamined
// rules in the first place.
//
// Each of these was applied, the suite run, and the named assertion observed to fail:
//
//   1. `src` → `sources` in the walk filter — **the original false green, which passed 9 of 9 before this
//      rewrite.** Now reddens all three remaining scans on the file count.
//   2. `Platform` → `PlatformX` — enumeration healthy, path filter dead. Reddens on the second half of
//      `AssertWalkIsIntact`, which is the half a bare count cannot see.
//   3. Assembly glob narrowed to `*.Domain.dll` — reddens the project/assembly cross-check by name.
//   4. Reference prefix pointed at `System.Runtime` — proves the EF check reads real references rather
//      than always finding nothing.
//   5. `IQueryable` widened to `IEnumerable` — proves the signature walk reads real signatures.
//
// **4 and 5 exist because a reflection test that finds nothing is as unfalsifiable as a file walk that
// finds nothing.** Converting away from text removed one failure mode; it did not remove the need to show
// the replacement can fail.
public sealed class PersistenceArchitectureTests
{
  // ⚠ THE ANTI-VACUITY CONTROL IS A CROSS-CHECK, NOT A FLOOR, AND IT IS STRICTLY STRONGER.
  //
  // A floor catches the walk collapsing. **It cannot catch one project quietly dropping out** — eleven
  // assemblies still clear a floor of eight while the twelfth goes unexamined. So the assembly set is
  // compared against a set derived INDEPENDENTLY, by counting `SSAS.*.Domain` and `SSAS.*.Application`
  // directories under `src/`. Two different routes to the same number disagreeing is the signal; either
  // route alone can be silently short.
  [Fact]
  public void Every_domain_and_application_project_is_actually_examined()
  {
    var assemblies = DomainAndApplicationAssemblies();
    var projects = DomainAndApplicationProjectNames();

    Assert.NotEmpty(projects);

    var missing = projects
      .Except(assemblies.Select(assembly => assembly.GetName().Name!), StringComparer.Ordinal)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(missing.Length == 0,
      "a Domain or Application project exists under src/ but its assembly is not loaded here, so every " +
      "rule below silently skips it:\n  " + string.Join("\n  ", missing) +
      "\n\nAdd a project reference from Architecture.Tests, or this file is checking a subset while " +
      "reporting on the whole.");
  }

  // ---- CONVERTED: an assembly-reference question, asked of assembly references.
  //
  // Text-scanning for `Microsoft.EntityFrameworkCore` found a `using`, which is a proxy for the dependency
  // rather than the dependency. A project can reference EF Core and never write the namespace — an
  // extension method reached through a fully-qualified call, or a transitive reference — and the scan
  // would have said nothing. **The reference is the thing the rule is about.**
  [Fact]
  public void Domain_and_application_projects_remain_entity_framework_free()
  {
    var violations = DomainAndApplicationAssemblies()
      .SelectMany(assembly => assembly.GetReferencedAssemblies()
        .Where(reference => reference.Name is not null
          && reference.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
        .Select(reference => $"{assembly.GetName().Name} -> {reference.Name}"))
      .OrderBy(text => text, StringComparer.Ordinal)
      .ToArray();

    Assert.True(violations.Length == 0,
      "a Domain or Application assembly references Entity Framework, so persistence has leaked out of " +
      "Infrastructure:\n  " + string.Join("\n  ", violations));
  }

  // ---- CONVERTED: a question about public signatures, asked of public signatures.
  //
  // The text version matched the WORD `IQueryable` anywhere in an Application file — including inside a
  // comment explaining why `IQueryable` must not be exposed, which is a false positive, and missing a type
  // that exposes it through an alias or a generic parameter, which is a false negative. Reflection reads
  // what the compiler produced.
  [Fact]
  public void Application_boundaries_do_not_expose_iqueryable()
  {
    var violations = new List<string>();

    foreach (var assembly in DomainAndApplicationAssemblies()
      .Where(assembly => assembly.GetName().Name!.EndsWith(".Application", StringComparison.Ordinal)))
    {
      foreach (var type in assembly.GetExportedTypes())
      {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance
          | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
          if (IsQueryable(method.ReturnType)
            || method.GetParameters().Any(parameter => IsQueryable(parameter.ParameterType)))
          {
            violations.Add($"{type.FullName}.{method.Name}");
          }
        }

        violations.AddRange(type
          .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.DeclaredOnly)
          .Where(property => IsQueryable(property.PropertyType))
          .Select(property => $"{type.FullName}.{property.Name}"));
      }
    }

    Assert.True(violations.Count == 0,
      "an Application boundary exposes IQueryable, so a caller can compose a database query across the " +
      "boundary and the persistence technology is no longer swappable:\n  " +
      string.Join("\n  ", violations.OrderBy(text => text, StringComparer.Ordinal)));
  }

  // ---- NOT CONVERTED: genuinely a question about source text, so it keeps a floor instead.
  //
  // A generic repository is a SHAPE in the source — `IRepository<T>` — and a type that was never written
  // does not exist to be reflected over. Reflection could ask "is any interface generic and named
  // Repository", which is a narrower question than the one being asked.
  [Fact]
  [Trait("Scenario", "TS-AUTH-0071")]
  public void Production_source_does_not_define_a_generic_repository()
  {
    var files = ProductionSourceFiles();
    AssertWalkIsIntact(files);

    var matches = files
      .Where(path => Regex.IsMatch(File.ReadAllText(path), @"\b(?:I)?Repository\s*<", RegexOptions.CultureInvariant))
      .ToArray();

    Assert.Empty(matches);
  }

  [Fact]
  public void Platform_identity_access_has_no_physical_delete_operation()
  {
    var files = ProductionSourceFiles();
    AssertWalkIsIntact(files);

    var violations = files
      .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Platform{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Where(path => Regex.IsMatch(
        File.ReadAllText(path),
        @"\bDelete(?:Identity|TenantUser|Role)(?:Async|Command|Handler)?\b|(?:Identities|TenantUsers|Roles)\.Remove\s*\(",
        RegexOptions.CultureInvariant))
      .ToArray();

    Assert.Empty(violations);
  }

  [Fact]
  public void Platform_source_does_not_log_secrets_tokens_or_raw_claims()
  {
    var files = ProductionSourceFiles();
    AssertWalkIsIntact(files);

    var violations = files
      .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Platform{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Where(path => Regex.IsMatch(
        File.ReadAllText(path),
        @"Log(?:Trace|Debug|Information|Warning|Error|Critical)\s*\([^;]*(?:password|secret|token|claims?)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
      .ToArray();

    Assert.Empty(violations);
  }

  // ⚠ THE FLOOR THAT WAS MISSING, AND THE SECOND HALF IS THE PART THAT WAS ACTUALLY BROKEN.
  //
  // The count catches the enumeration failing. The Platform check catches the FILTER failing — the rename
  // that produced the false green left the enumeration healthy and made every `Where` match nothing, which
  // a count alone would not have seen because the count was never taken.
  private static void AssertWalkIsIntact(IReadOnlyCollection<string> files)
  {
    Assert.True(files.Count >= 400,
      $"only {files.Count} production source files were found; the walk has degraded and 'no violations' " +
      "below would mean nothing rather than being reassuring.");

    Assert.True(
      files.Any(path => path.Contains($"{Path.DirectorySeparatorChar}Platform{Path.DirectorySeparatorChar}", StringComparison.Ordinal)),
      "the walk found files but none under Platform, so the path filters the Platform rules depend on are " +
      "matching nothing. This is the exact shape that made this file pass while measuring nothing.");
  }

  private static IReadOnlyCollection<string> ProductionSourceFiles() => [.. Directory
    .EnumerateFiles(FindRepositoryRoot(), "*.cs", SearchOption.AllDirectories)
    .Where(path => path.Contains($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}", StringComparison.Ordinal))];

  private static bool IsQueryable(Type type) =>
    type.Name.StartsWith("IQueryable", StringComparison.Ordinal)
    || (type.IsGenericType && type.GetGenericArguments().Any(IsQueryable));

  private static Assembly[] DomainAndApplicationAssemblies() =>
    [.. Directory
      .EnumerateFiles(AppContext.BaseDirectory, "SSAS.*.dll")
      // `RepositoryPaths.ProjectName` rather than `Path.GetFileNameWithoutExtension`, which
      // `RepositoryPathPortabilityTests` bans outright in this project. The ban is blanket by design:
      // the helper is correct for a local filesystem path and WRONG for an MSBuild `Include`, and the
      // two are indistinguishable at a glance -- which is exactly how the Linux blindness arrived. This
      // use happened to be the safe kind, and a rule that only fires on the unsafe kind needs a reader
      // to classify it correctly every time.
      .Select(RepositoryPaths.ProjectName)
      .Where(name => name is not null
        && (name.EndsWith(".Domain", StringComparison.Ordinal)
          || name.EndsWith(".Application", StringComparison.Ordinal)))
      .Select(name => Assembly.Load(name!))];

  private static string[] DomainAndApplicationProjectNames() =>
    [.. Directory
      .EnumerateDirectories(Path.Combine(FindRepositoryRoot(), "src"), "SSAS.*", SearchOption.AllDirectories)
      .Select(Path.GetFileName)
      .Where(name => name is not null
        && (name.EndsWith(".Domain", StringComparison.Ordinal)
          || name.EndsWith(".Application", StringComparison.Ordinal)))
      .Select(name => name!)
      .Distinct(StringComparer.Ordinal)];

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
