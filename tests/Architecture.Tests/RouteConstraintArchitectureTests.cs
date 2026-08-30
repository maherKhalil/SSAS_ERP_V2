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

  // The same call WITHOUT requiring a literal to follow. The gap between the two counts is the whole
  // subject of `Every_route_is_registered_with_a_literal_pattern`.
  private static readonly Regex AnyMapCall = new(
    @"\.Map(?:Get|Post|Put|Delete|Patch)\s*\(",
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

  // ================================================================================================
  // ⚠ EVERY ROUTE IS REGISTERED WITH A LITERAL PATTERN, WHICH IS WHAT MAKES THE GUARD ABOVE HONEST.
  // ================================================================================================
  //
  // `No_route_parameter_carries_a_type_constraint` reads route strings out of source. **It can therefore
  // only see constraints in patterns that are WRITTEN OUT.** `MapGet(Prefix + "/{id:guid}")` or
  // `MapGet($"{Prefix}/{{id:guid}}")` would carry a constraint past it in complete silence.
  //
  // ---- WHY THE FLOOR ABOVE DOES NOT ALREADY COVER THIS, WHICH IS THE PART WORTH UNDERSTANDING.
  //
  // That guard asserts it found at least 120 routes across 12 files. **A floor detects the walk COLLAPSING;
  // it is blind to one item stepping out of view.** 151 literal routes clear a floor of 120 comfortably
  // while a single composed route hides completely — the corpus still looks healthy, which is precisely
  // what makes selective invisibility more dangerous than total failure.
  //
  // ---- THIS IS A FORECLOSURE, NOT A REPAIR.
  //
  // **There are zero non-literal registrations today**, and `src/` contains no interpolated string
  // constants at all, so nothing is currently hidden. It is asserted anyway because the constraint rule is
  // published as ENFORCED, and a claim of enforcement obliges the mechanism to be able to see what it
  // claims to check. **A zero maintained by house style lasts until someone is in a hurry**, and this is
  // the cheapest moment it will ever be fixable.
  //
  // If a composed pattern is ever genuinely needed, this test is the place to decide that — and whoever
  // does will have to state how the constraint guard is meant to see it.
  [Fact]
  public void Every_route_is_registered_with_a_literal_pattern()
  {
    var offenders = new List<string>();
    var total = 0;

    foreach (var file in EndpointFiles())
    {
      // ⚠ COMMENTS STRIPPED, STRING LITERALS KEPT — and the order matters more than it looks. These files
      // discuss `MapGet` in prose constantly, and a comment mentioning one would count as a registration
      // with no literal after it. But a naive comment stripper would cut `"https://httpstatuses.com/400"`
      // in half at its `//`, so the stripper has to know it is inside a string.
      var source = WithoutComments(File.ReadAllText(file));

      var all = AnyMapCall.Matches(source).Count;
      var literal = MapCall.Matches(source).Count;
      total += all;

      if (all != literal)
      {
        offenders.Add($"{Path.GetFileName(file)}: {all} route registrations, {literal} with a literal " +
          $"pattern — {all - literal} built rather than written");
      }
    }

    Assert.True(total >= 120,
      $"only {total} route registrations were found; the scan has degraded and a count of zero " +
      "non-literal registrations would mean nothing.");

    Assert.True(offenders.Count == 0,
      "a route is registered with a pattern that is BUILT rather than written out, so the constraint " +
      "guard in this class cannot read it and a type constraint could be reintroduced there invisibly:\n  " +
      string.Join("\n  ", offenders) +
      "\n\nWrite the pattern as a literal, or change the constraint guard to understand the construction " +
      "and say here how.");
  }

  // Blanks the CONTENT of `//` and `/* */` comments while stepping over string literals, preserving length
  // so nothing downstream shifts.
  private static string WithoutComments(string text)
  {
    var buffer = text.ToCharArray();
    var i = 0;

    while (i < buffer.Length)
    {
      if (buffer[i] == '"')
      {
        buffer[i] = buffer[i];
        i++;
        while (i < buffer.Length && buffer[i] != '"')
        {
          if (buffer[i] == '\\' && i + 1 < buffer.Length)
          {
            i++;
          }

          i++;
        }

        i++;
        continue;
      }

      if (buffer[i] == '/' && i + 1 < buffer.Length && buffer[i + 1] == '/')
      {
        while (i < buffer.Length && buffer[i] != '\n')
        {
          buffer[i++] = ' ';
        }

        continue;
      }

      if (buffer[i] == '/' && i + 1 < buffer.Length && buffer[i + 1] == '*')
      {
        while (i < buffer.Length && !(buffer[i] == '*' && i + 1 < buffer.Length && buffer[i + 1] == '/'))
        {
          if (buffer[i] != '\n')
          {
            buffer[i] = ' ';
          }

          i++;
        }

        for (var j = i; j < Math.Min(i + 2, buffer.Length); j++)
        {
          buffer[j] = ' ';
        }

        i += 2;
        continue;
      }

      i++;
    }

    return new string(buffer);
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
