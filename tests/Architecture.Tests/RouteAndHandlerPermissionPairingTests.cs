using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// WHERE A ROUTE AND ITS HANDLER BOTH ENFORCE A PERMISSION, THEY MUST ENFORCE THE SAME ONE (T-207).
// ==================================================================================================
//
// ---- ⚠ THE DANGER IS NOT THE DUPLICATION. IT IS THAT NEITHER SITE KNOWS THE OTHER EXISTS.
//
// Eighteen read routes in GL and Payroll carry a route-level `RequirePermission(P)` AND a handler that
// calls `resolver.ResolveAsync(P)`. The pattern is deliberate: a read resolves the SCOPE of companies and
// branches the caller may see, and that resolution doubles as the authorization check.
//
// **So each site is individually removable by someone who has checked the other.** The route gate goes
// because the handler checks anyway; later, the handler check goes because the route requires it. **Each
// removal is correct in isolation and the pair leaves no defence at all** — and the second remover cannot
// see the first removal's reasoning, because it was true when it was written.
//
// That is the self-vindicating record across TIME and across TWO SITES, which no single reader catches.
//
// ---- WHY A TEST AND NOT A COMMENT AT EACH SITE.
//
// A comment cannot stop the second removal — it can only inform someone who reads it, and a comment at the
// route saying *"the handler also checks"* **arms the first removal as readily as it warns against it.**
// This reddens on either removal without anyone reading anything.
//
// It is derived, not declared: the route registration names its handler, the handler names the permission
// it resolves, and the pairing falls out. Nothing here is a list to maintain.
//
// ---- ⚠ AND THE BOTH-GONE CASE IS COVERED ELSEWHERE, WHICH WAS VERIFIED RATHER THAN ASSUMED.
//
// If the route gate AND the handler resolve both disappear, there is no pairing left to break and this
// test has nothing to say. **`GlRouteInventoryTests.Every_gl_route_requires_a_permission` and
// `Every_route_requires_the_permission_the_inventory_names` catch it**, and each module's inventory carries
// the same pair. Planted: removing only the route gate leaves this test green and reddens both of those.
//
// **That cross-reference is the difference between a composition and a coincidence** — two instruments each
// covering the other's gap without either saying so is the arrangement this very guard exists to prevent,
// one level up.
//
// ---- ⚠ WHAT IS DELIBERATELY NOT CHECKED.
//
// That a handler resolves SOMETHING is not required — most routes are gated once, at the route, and that is
// correct. This only asserts that **where both exist, they agree.** Requiring the pattern everywhere would
// be inventing a convention rather than protecting one.
//
// HR resolves an `EmployeeScopeRequest` in two handlers, which is a DATA scope deciding which employees are
// visible rather than a permission deciding whether the caller may act. Those are not pairings and the
// pattern below does not match them — the two read identically at a grep and are not the same thing.
public sealed class RouteAndHandlerPermissionPairingTests
{
  // `.MapGet("/x", HandlerAsync)` followed by `.RequirePermission(Names.Permission)`.
  private static readonly Regex Route = new(
    @"\.Map(?:Get|Post|Put|Delete|Patch)\(\s*""([^""]+)""\s*,\s*(\w+)\s*\)\s*\.RequirePermission\(\s*(\w+)\.(\w+)\s*\)",
    RegexOptions.Compiled | RegexOptions.Singleline);

  // A resolve keyed on a PERMISSION NAME. `ResolveAsync(new EmployeeScopeRequest(...))` does not match,
  // and must not: it resolves data visibility, not authority.
  private static readonly Regex Resolve = new(
    @"Resolve(?:CompanyOnly)?Async\(\s*(\w+PermissionNames)\.(\w+)",
    RegexOptions.Compiled);

  // ⚠ ANTI-VACUITY, BOTH DIRECTIONS. If the route pattern stops matching, nothing is scanned and the guard
  // passes silently; if the resolve pattern stops matching, every pairing disappears and it passes just as
  // silently. Two floors, because either regex can rot alone.
  private const int MinimumRoutes = 90;

  // ⚠ AN EXACT COUNT, NOT A FLOOR, AND THE DIFFERENCE IS THE WHOLE GUARD.
  //
  // A floor catches the scan rotting and NOTHING ELSE. Removing one half of one pairing takes 18 to 17,
  // which clears any floor — so the guard would have gone green on exactly the removal it exists to catch.
  // **I built it with a floor first and only found that by planting both removals.**
  //
  // An exact count reddens on either removal at any of the eighteen, and on a NEW double-guarded read that
  // nobody meant to add. It is still derived rather than declared: one number, no list of routes.
  private const int ExpectedPairings = 18;

  [Fact]
  [Trait("Decision", "DEC-L-085")]
  public void A_route_and_its_handler_never_require_different_permissions()
  {
    var files = Directory.EnumerateFiles(
        Path.Combine(FindRepositoryRoot(), "src"), "*EndpointRouteBuilderExtensions.cs",
        SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
          StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
          StringComparison.Ordinal))
      .ToArray();

    Assert.NotEmpty(files);

    var routes = 0;
    var pairings = 0;
    var disagreements = new List<string>();

    foreach (var path in files)
    {
      // Comments stripped: a comment naming a permission beside a route would pair a route with prose,
      // which is the failure this loop has met in three different documents.
      var source = string.Join(
        '\n',
        File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

      foreach (Match route in Route.Matches(source))
      {
        routes++;
        var handler = route.Groups[2].Value;
        var required = route.Groups[4].Value;

        var body = HandlerBody(source, handler);
        if (body.Length == 0)
        {
          continue;
        }

        var resolved = Resolve.Match(body);
        if (!resolved.Success)
        {
          continue;
        }

        pairings++;
        if (!string.Equals(resolved.Groups[2].Value, required, StringComparison.Ordinal))
        {
          disagreements.Add(
            $"{Path.GetFileName(path)}  {route.Groups[1].Value}  route requires {required}, " +
            $"{handler} resolves {resolved.Groups[2].Value}");
        }
      }
    }

    Assert.True(
      routes >= MinimumRoutes,
      $"only {routes} route registrations matched, below the floor of {MinimumRoutes}: the route scan " +
      "has degraded rather than the product having shrunk");

    Assert.True(
      pairings == ExpectedPairings,
      $"{pairings} route/handler pairings found, expected {ExpectedPairings}. FEWER means one half of a " +
      "pairing has gone — the route stopped requiring, or the handler stopped resolving — which is the " +
      "removal this guard exists to catch. MORE means a new double-guarded read arrived and should be " +
      "deliberate. Either way someone decides rather than the number drifting.");

    Assert.True(
      disagreements.Count == 0,
      "a route and its handler enforce DIFFERENT permissions, so one of them is not the gate anyone " +
      "thinks it is:" + Environment.NewLine + string.Join(Environment.NewLine, disagreements));
  }

  private static string HandlerBody(string source, string name)
  {
    foreach (Match match in Regex.Matches(source, @"\b" + Regex.Escape(name) + @"\s*\("))
    {
      var head = source.LastIndexOf('\n', Math.Max(0, match.Index - 1));
      var window = source[Math.Max(0, head - 160)..match.Index];
      if (!window.Contains("Task<IResult>", StringComparison.Ordinal))
      {
        continue;
      }

      var open = source.IndexOf('{', match.Index);
      if (open < 0)
      {
        continue;
      }

      var depth = 0;
      for (var index = open; index < source.Length; index++)
      {
        if (source[index] == '{')
        {
          depth++;
        }
        else if (source[index] == '}')
        {
          depth--;
          if (depth == 0)
          {
            return source[open..(index + 1)];
          }
        }
      }
    }

    return string.Empty;
  }

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
