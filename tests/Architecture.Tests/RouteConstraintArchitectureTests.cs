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

  // ⚠ A GROUP PREFIX IS A NAMED CONSTANT, NOT A LITERAL, AND BOTH CHECKS READ THE CALL SITE (T-259).
  //
  // `MapGroup(RoutePrefix)` carries no string for either check to read. Without resolution the
  // constraint check cannot see the prefix's pattern at all, and the literal-pattern check reports the
  // constant as "built rather than written" -- which is a false red: a named constant is exactly how a
  // prefix SHOULD be written, and it is still a compile-time literal.
  //
  // `ApiContractRowGuardTests` already resolves constants this way for the same reason. Substituting
  // them here makes a group prefix visible to both checks without loosening either.
  private static readonly Regex ConstantDeclaration = new(
    @"const\s+string\s+(\w+)\s*=\s*""([^""]*)""", RegexOptions.Compiled);

  private static string WithConstantsResolved(string source)
  {
    foreach (Match declaration in ConstantDeclaration.Matches(source))
    {
      source = source.Replace(
        "(" + declaration.Groups[1].Value + ")",
        "(\"" + declaration.Groups[2].Value + "\")",
        StringComparison.Ordinal);
    }

    return source;
  }

  private static readonly Regex ConstrainedParameter = new(
    @"\{[A-Za-z0-9_]+:[^}]+\}", RegexOptions.Compiled);

  // ⚠ `Group` IS IN THIS LIST BECAUSE A GROUP PREFIX IS PART OF EVERY ROUTE UNDER IT (T-259).
  //
  // The registration forms actually used in `src/` are `MapGet` (57), `MapPost` (85), `MapPut` (13),
  // `MapGroup` (15) and `MapHealthChecks` (3). `MapDelete`, `MapPatch`, `MapMethods` and `MapFallback`
  // are not used at all -- the first two are matched anyway, which costs nothing.
  //
  // **`MapGroup` was the gap.** A prefix like `MapGroup("/api/hr/{id:guid}")` would put a constraint on
  // every route in the group, and neither the constraint check nor the literal-pattern check could see
  // it. All fifteen prefixes resolve to plain literals today -- eleven written directly and four passed
  // as a constant through a helper -- so the exposure is zero, which is the cheapest moment to close it.
  //
  // ⚠ AND A NEAR-MISS FOR ANYONE GREPPING: `src/` contains 83 bare `.Map(` calls and NOT ONE is a route.
  // They are `ApiErrorMapper.Map(result.Error)`. A regex widened to bare `Map` would match all 83 and
  // report a route surface three times its real size.
  //
  // `MapHealthChecks("/health")` stays out: it takes no route parameters, so there is nothing for a
  // constraint to attach to.
  private static readonly Regex MapCall = new(
    @"\.Map(?:Get|Post|Put|Delete|Patch|Group)\(\s*""([^""]*)""",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

  // The same call WITHOUT requiring a literal to follow. The gap between the two counts is the whole
  // subject of `Every_route_is_registered_with_a_literal_pattern`.
  private static readonly Regex AnyMapCall = new(
    @"\.Map(?:Get|Post|Put|Delete|Patch|Group)\s*\(",
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
      var source = WithConstantsResolved(File.ReadAllText(file));

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
      var source = WithConstantsResolved(WithoutComments(File.ReadAllText(file)));

      if (GroupPrefixThroughHelper.Contains(Path.GetFileName(file), StringComparer.Ordinal))
      {
        continue;
      }

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

  // ⚠ ONE GROUP PREFIX ARRIVES THROUGH A PARAMETER, AND THIS IS THE CASE THE MESSAGE ABOVE ANTICIPATES.
  //
  // `PositionEndpointRouteBuilderExtensions` has a private `Group(endpoints, prefix, tag)` helper so that
  // four route groups -- positions, job grades, salary grades and employee positions -- share one setup
  // instead of copying it. Its `MapGroup(prefix)` therefore has no literal at the call site.
  //
  // **This is a helper factoring out duplication, not a pattern being computed.** Every one of the four
  // callers passes a `const string`, so the prefixes are still compile-time literals -- they are simply
  // one hop away. Rewriting it to satisfy the scan would restore four copies of the group setup, which
  // is moving the artefact to suit the instrument.
  //
  // `Every_group_helper_caller_passes_a_constant` is the falsifier: the day a caller computes a prefix,
  // this exemption stops being true and says so.
  private static readonly string[] GroupPrefixThroughHelper =
    ["PositionEndpointRouteBuilderExtensions.cs"];

  [Fact]
  public void Every_group_helper_caller_passes_a_constant()
  {
    foreach (var name in GroupPrefixThroughHelper)
    {
      var file = EndpointFiles().SingleOrDefault(path => Path.GetFileName(path) == name);
      Assert.True(file is not null, $"{name} is exempted but no longer exists.");

      var source = WithoutComments(File.ReadAllText(file!));
      var constants = ConstantDeclaration.Matches(source)
        .Select(declaration => declaration.Groups[1].Value)
        .ToHashSet(StringComparer.Ordinal);

      var calls = Regex.Matches(source, @"Group\(endpoints,\s*(\w+)\s*,");
      Assert.True(calls.Count >= 4,
        $"only {calls.Count} calls to the group helper were found in {name}; the exemption is being " +
        "checked against nothing.");

      foreach (Match call in calls)
      {
        var argument = call.Groups[1].Value;
        Assert.True(constants.Contains(argument),
          $"{name} passes `{argument}` to the group helper and it is not a `const string`. The prefix is " +
          "no longer a compile-time literal, so the constraint guard cannot see it and this exemption " +
          "must go.");
      }
    }
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
