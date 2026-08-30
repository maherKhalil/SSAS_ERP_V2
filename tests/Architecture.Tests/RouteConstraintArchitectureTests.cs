using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// NO ROUTE PARAMETER CARRIES A TYPE CONSTRAINT, SO A MALFORMED IDENTIFIER IS ALWAYS A 400 (T-238).
// ==================================================================================================
//
// ---- WHY A CONSTRAINT CHANGES BEHAVIOUR AND NOT ONLY VALIDATION.
//
// A route constraint is part of route MATCHING, not of parameter binding. `{id:guid}` means `not-a-guid`
// matches no route at all and ASP.NET answers **404 before any of our code runs**. Without the constraint
// the value reaches parameter binding, fails to bind to `Guid`, and becomes a **400**.
//
// **A 404 makes a malformed identifier indistinguishable from an absent record.** A caller cannot separate
// *"your GUID is not a GUID"* from *"that record is gone"* — and until T-236/T-238 that was true on 71
// routes across five modules, permanently, with no way for a client to tell the two apart.
//
// ---- ⚠ WHY THIS IS A GUARD RATHER THAN SEVENTY-ONE TESTS.
//
// The convention was previously held by **one** test, on Company, and contradicted by 71 parameters that
// nobody had counted. **It was stated in prose and enforced nowhere**, so the next module to add
// `{id:guid}` would have reintroduced the divergence in silence — exactly how it arose.
//
// **This behaviour is CONFIGURED rather than written**, which is why inspection never found it: there is no
// handler to read, only a token in a route string inside a table. A guard that reads the route strings is
// the only instrument that looks where the behaviour actually lives.
//
// ---- THE ALLOWLIST IS EMPTY, AND THAT IS A MEASURED CLAIM RATHER THAN A DEFAULT.
//
// A constraint would be legitimate where two sibling routes differ only by it — `/x/{id:guid}` and
// `/x/{code}` — because removing one changes which handler serves `/x/ABC123`. **Every route in the product
// was enumerated for that shape with group prefixes resolved: 151 routes, zero method-and-shape groups
// holding two different patterns.** So no constraint was load-bearing for disambiguation, and the list
// below is empty because nothing needed to be in it.
//
// **If a future route genuinely needs one, add it here WITH ITS SIBLING NAMED** — the entry has to say what
// it disambiguates, because "we needed it" is the reasoning that produced the original divergence.
public sealed class RouteConstraintArchitectureTests
{
  // ⚠ EVERY ENTRY MUST NAME THE SIBLING ROUTE IT DISAMBIGUATES. An allowlist that accepts a bare route is
  // a blanket ban with extra steps.
  private static readonly (string Route, string Sibling, string Why)[] Allowed = [];

  private static readonly Regex ConstrainedParameter = new(
    @"\{[A-Za-z0-9_]+:[^}]+\}", RegexOptions.Compiled);

  private static readonly Regex MapCall = new(
    @"\.Map(?:Get|Post|Put|Delete|Patch)\(\s*""([^""]*)""",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

  [Fact]
  public void No_route_parameter_carries_a_type_constraint()
  {
    var offenders = new List<string>();
    var scanned = 0;
    var routes = 0;

    foreach (var file in EndpointFiles())
    {
      scanned++;
      var source = File.ReadAllText(file);

      foreach (Match map in MapCall.Matches(source))
      {
        routes++;
        var pattern = map.Groups[1].Value;
        if (!ConstrainedParameter.IsMatch(pattern))
        {
          continue;
        }

        if (Allowed.Any(entry => entry.Route == pattern))
        {
          continue;
        }

        offenders.Add($"{Path.GetFileName(file)}: {pattern}");
      }
    }

    // ⚠ ANTI-VACUITY, AND IT IS THE WHOLE GUARD. A scan that stopped finding endpoint files, or a `Map`
    // pattern that stopped matching, would report zero offenders and read exactly like success. The floors
    // are deliberately well below the measured 151 routes across 16 files, so ordinary growth does not
    // touch them and a collapsed walk does.
    Assert.True(scanned >= 12,
      $"only {scanned} endpoint files were scanned, so this guard is inspecting almost nothing.");
    Assert.True(routes >= 120,
      $"only {routes} routes were found across {scanned} files; the `Map` pattern has stopped matching.");

    Assert.True(offenders.Count == 0,
      "a route parameter carries a type constraint, so a malformed identifier there answers 404 rather " +
      "than 400 and becomes indistinguishable from an absent record:\n  " +
      string.Join("\n  ", offenders) +
      "\n\nRemove the constraint, or add the route to `Allowed` NAMING THE SIBLING it disambiguates.");
  }

  // Every allowlist entry must justify itself, or the list becomes a place to put inconvenient routes.
  [Fact]
  public void Every_allowlist_entry_names_a_sibling_and_a_reason()
  {
    foreach (var (route, sibling, why) in Allowed)
    {
      Assert.False(string.IsNullOrWhiteSpace(sibling),
        $"{route} is allowlisted without naming the sibling route it disambiguates.");
      Assert.False(string.IsNullOrWhiteSpace(why),
        $"{route} is allowlisted without a reason.");
    }
  }

  private static IEnumerable<string> EndpointFiles()
  {
    var root = RepositoryRoot();
    return Directory
      .EnumerateFiles(Path.Combine(root, "src"), "*EndpointRouteBuilderExtensions.cs",
        SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
  }

  private static string RepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
    {
      directory = directory.Parent;
    }

    Assert.NotNull(directory);
    return directory!.FullName;
  }
}
