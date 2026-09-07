using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY MODULE ROUTE GROUP ATTACHES THE COMPANY-CONTEXT FILTER (T-141).
// ==================================================================================================
//
// ---- ⚠⚠⚠ WHY A SOURCE ASSERTION, WHEN A BEHAVIOURAL ONE EXISTS.
//
// `GlCompanyContextFilterAttachmentTests` drives the establisher to failure through a real pipeline and asserts the
// refusal. **That is the stronger claim where it reaches — and it only reaches where the filter is the ONLY
// thing enforcing.** For HR's positions family it is not: removing that group's attachment left the whole
// API suite green, because the handlers ALSO read `ICurrentCompany`, whose `CompanyId` is null exactly when
// establishment failed. ***TWO MECHANISMS PRODUCING THE SAME 403 AND THE SAME WIRE CODE, WHICH NO BLACK-BOX
// TEST CAN SEPARATE.***
//
// So the attachment is asserted where it is written, at the source. **The two instruments make two different
// claims and neither replaces the other:**
//
//     GlCompanyContextFilterAttachmentTests   the filter RUNS and refuses          — one group, behavioural
//     this file                            the attachment is PRESENT            — every group, structural
//
// ---- ⚠⚠ THE POPULATION IS DERIVED FROM `MapGroup`, NOT LISTED.
//
// **A list of the seven attachment sites would be SILENT on an eighth route group added without the filter**
// — and silence on a new member is the failure direction that is not acceptable for company scope. Counting
// `MapGroup` instead means a new group is IN the population the moment it exists, and reddens until someone
// attaches the filter or argues otherwise.
//
// Measured 2026-09-07: **15 `MapGroup` sites in `src/` — 7 under `src/Modules/`, 8 under `SSAS.Platform.API`.**
//
// ***7 MODULE GROUPS, 7 ATTACHMENTS: THERE IS NO HOLE TODAY. THIS GUARD PREVENTS AN EIGHTH; IT DOES NOT FIX
// A SEVENTH.*** *The gap this closes was in the ASSERTIONS, not in the product — four of the seven
// attachments could be deleted with the whole suite green, and that is what needed fixing.*
//
// ⚠⚠ **AND ONE OF THE SEVEN IS WHY THE POPULATION IS DERIVED RATHER THAN TYPED:
// `DepartmentEndpointRouteBuilderExtensions` declares TWO groups — `:63` and, at `:150`, an
// `EmployeeRoutePrefix` group inside the DEPARTMENTS file.** ***A hand-built list would have had six entries
// and looked complete.*** *That member lives where nobody would look for it, and only counting `MapGroup`
// finds it.*
//
// ---- ⚠⚠⚠ THE PLATFORM PLANE IS OUT OF SCOPE BY PARTITION, NOT BY EXEMPTION LIST.
//
// The eight `SSAS.Platform.API` groups carry no company-context filter, and listing them would be **one
// reason written eight times** — a category, not eight decisions. The partition is house practice rather
// than invented here: **`GatedRouteInventory` defines a module route as one whose handler belongs to the four
// module API assemblies, and `ModuleEnablementCoverageTests.No_platform_plane_endpoint_is_gated` already
// asserts the two planes differ.**
//
// ⚠ **THE COST, STATED: a company-scoped group added under `Platform.API` would be outside this guard.**
// *Closing that needs a positive claim about which platform groups are company-scoped, with grounds per
// entry — a different guard, and not one to invent by widening a directory filter.*
//
// ---- ⚠ WHAT THIS CANNOT SEE.
//
// ***A SOURCE ASSERTION PROVES THE LINE IS PRESENT. IT DOES NOT PROVE THE FILTER RUNS***, that it runs
// before anything that matters, or that it is not undone later in the chain. *The GL row in
// `GlCompanyContextFilterAttachmentTests` is the instrument for that, and it covers one group of seven.*
public sealed class CompanyContextFilterAttachmentArchitectureTests
{
  // The chain, not a line window. `PositionEndpointRouteBuilderExtensions` puts its attachment TEN lines
  // below its `MapGroup(`, and a window wide enough to reach it would run into the next declaration in a
  // file that declares two groups. Matching to the statement's terminating `;` is exact for both.
  private static readonly Regex GroupChain = new(
    @"MapGroup\s*\((?:[^;]*?)\)(?<chain>[^;]*);", RegexOptions.Singleline | RegexOptions.Compiled);

  [Fact]
  public void Every_module_route_group_attaches_the_company_context_filter()
  {
    var groups = ModuleRouteGroups();

    // ---- ⚠⚠⚠ A FLOOR, DELIBERATELY NOT AN EQUALITY — AND THE DIFFERENCE IS WHAT THIS GUARD IS FOR.
    //
    // This walk is a regex over source text, so **a matcher that stopped matching — a renamed method, a
    // reformatted chain, a moved directory — would find NO groups, and every assertion below would pass over
    // an empty population.** The floor is the alarm for that, and that is its ONLY job.
    //
    // ***`== 7` WOULD REDDEN WHEN SOMEBODY LEGITIMATELY ADDS AN EIGHTH MODULE GROUP, AND THE ONLY WAY TO
    // GREEN IT WOULD BE TO EDIT THE NUMBER — WHICH IS EXACTLY THE MOMENT NOBODY CHECKS WHETHER THE NEW GROUP
    // HAS THE FILTER.*** *A guard whose remedy is "increment the constant" teaches its reader to increment
    // the constant.* **With a floor, an eighth group is CHECKED rather than counted, and adding a compliant
    // one needs no edit here at all.**
    Assert.True(
      groups.Count >= 7,
      $"the module route-group scan found {groups.Count} groups and there were 7 on 2026-09-07. This is a " +
      "regex over source text: a renamed method, a reformatted chain or a moved directory makes it match " +
      "nothing, and an assertion over an empty population passes. A number BELOW the floor means the " +
      $"instrument broke, not that route groups were deleted.{Environment.NewLine}" +
      string.Join(Environment.NewLine, groups.Select(group => group.Where)));

    var unattached = groups
      .Where(group => !group.Chain.Contains("CompanyContextEndpointFilter", StringComparison.Ordinal))
      .Select(group => group.Where)
      .OrderBy(where => where, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      unattached.Length == 0,
      "a module route group does not attach a company-context endpoint filter. Every module route group is " +
      "company-owned, so establishing company context is not optional: without the filter the group's " +
      "requests reach their handlers with no company established, and whether anything refuses them then " +
      $"depends on each handler.{Environment.NewLine}{string.Join(Environment.NewLine, unattached)}");
  }

  private static List<(string Where, string Chain)> ModuleRouteGroups()
  {
    var root = RepositoryRoot();
    var modules = Path.Combine(root, "src", "Modules");
    var found = new List<(string, string)>();

    // `Directory.EnumerateFiles` sees `bin` and `obj`, which hold copies of nothing here but would hold
    // generated sources in another tree — excluded rather than assumed absent.
    foreach (var path in Directory.EnumerateFiles(modules, "*.cs", SearchOption.AllDirectories))
    {
      if (path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      {
        continue;
      }

      var text = StripLineComments(File.ReadAllText(path));
      foreach (Match match in GroupChain.Matches(text))
      {
        found.Add((Path.GetRelativePath(root, path).Replace('\\', '/'), match.Groups["chain"].Value));
      }
    }

    return found;
  }

  // ⚠ COMMENTS STRIPPED BEFORE MATCHING. A `MapGroup` quoted in a comment is not a route group, and an
  // attachment quoted in one is not an attachment — this guard would otherwise be satisfiable by prose.
  private static string StripLineComments(string text) =>
    string.Join('\n', text.Split('\n').Select(line => line.TrimStart().StartsWith("//", StringComparison.Ordinal) ? string.Empty : line));

  private static string RepositoryRoot()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}
