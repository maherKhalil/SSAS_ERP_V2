using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// AN ERROR THE OPERATOR CAN RECEIVE MUST HAVE A MAPPER ARM, WHETHER OR NOT A HANDLER NAMES IT (T-200).
// ==================================================================================================
//
// ---- ⚠ THIS EXISTS BECAUSE TWO LIVE 500s SAT ONE STEP PAST WHERE THE EXISTING WALK STOPS.
//
// `ModuleErrorMappingArchitectureTests` walks the errors a SITE NAMES — the codes that appear in a
// handler's own source. Both defects found on 2026-08-29 were invisible to it for the same reason:
//
//   `Attendance.LeaveSubmissionBusy`   returned by an INFRASTRUCTURE lock, propagated as `result.Error`
//   `Payroll.RunAlreadyReversed`       returned by a DOMAIN aggregate, propagated as `result.Error`
//
// Neither appears in any handler's source, so neither was ever checked, and both fell through to
// `ApiErrors.WriteFailure` — **500 `request.failed`** — for conditions that are ordinary and expected. A
// double-clicked submit button reaches the first. Reversing an already-reversed run reaches the second.
//
// **`RunAlreadyReversed` is the most legible instance: `PayrollRun.MarkReversed()` returns exactly two
// errors, the handler propagates both and names neither, one was mapped and one was not — so the two halves
// of one method answered 409 and 500.**
//
// ---- WHAT THIS CHECKS, AND WHY IT DOES NOT TRY TO PROVE REACHABILITY.
//
// Establishing statically that a given error can reach a given route is expensive and, for propagated
// errors, close to impossible without whole-program analysis. **This does not attempt it.** It asserts
// something cruder and sufficient: within a module that ships an API surface, EVERY WIRE CODE THE MODULE
// RETURNS ANYWHERE must have an arm in that module's mapper.
//
// That is deliberately over-strict — a code returned only on a path no route takes still needs an arm. The
// cost of the over-strictness is one line in a mapper; the cost of the gap was two production 500s.
//
// ---- ⚠ SCOPED TO THE FOUR MODULES THAT SHIP AN API SURFACE, AND THAT IS NOT AN EVASION.
//
// A survey on 2026-08-29 found 147 unmapped-but-produced codes across `src/`: 99 TenantStorage, 17
// Subscription, 12 Branch. **Those are subsystems with no route to fall through on** — the same capability
// gap the completeness audit's first axis measures — so requiring mapper arms for them would demand
// transport for things deliberately not exposed yet. Including them would have meant a 128-entry exemption
// list, and a guard that must be told what to ignore is the stale record this loop keeps finding.
//
// **When one of those subsystems gains an API surface, add it here.** The list below is the trigger.
public sealed class PropagatedErrorMappingTests
{
  // One place states the scope; every assertion derives from it.
  private static readonly ModuleSurface[] Surfaces =
  [
    new("GL", Path.Combine("Modules", "Finance"), "SSAS.GL.API", "GlApiErrorMapper.cs"),
    new("HR", Path.Combine("Modules", "HR"), "SSAS.HR.API", null),
    new("Payroll", Path.Combine("Modules", "Payroll"), "SSAS.Payroll.API", "PayrollApiErrorMapper.cs"),
    new("Attendance", Path.Combine("Modules", "Attendance"), "SSAS.Attendance.API",
      "AttendanceApiErrorMapper.cs"),
  ];

  // ⚠ ANTI-VACUITY. If either regex stops matching — a formatting change, a new error idiom — every module
  // reports zero returned codes and the guard passes while checking nothing. These floors are what turn
  // that into a red build. They are minimums, not counts, so ordinary growth does not touch them.
  private static readonly Dictionary<string, int> MinimumReturnedCodes = new(StringComparer.Ordinal)
  {
    ["GL"] = 20,
    ["HR"] = 20,
    ["Payroll"] = 25,
    ["Attendance"] = 30,
  };

  // ⚠ CAPTURES THE MEMBER NAME AS WELL AS THE WIRE CODE, AND THE FIRST VERSION DID NOT.
  //
  // **They are not the same string and assuming so is silent.** `AccountErrors.NotFound` carries
  // `"Gl.AccountNotFound"`; `CalendarErrors.InvalidCode` carries `"Gl.FiscalYearCodeInvalid"`. Matching a
  // `XErrors.Member` reference against the code's own suffix therefore resolves almost nothing in GL and
  // only coincidentally in Payroll — which is exactly what the floor below caught, reporting 0 of 20.
  private static readonly Regex Definition = new(
    @"static\s+(?:readonly\s+)?Error\s+(\w+)[^""]*?new\(\s*""([A-Z][A-Za-z]+\.[A-Za-z][A-Za-z0-9]*)""",
    RegexOptions.Compiled | RegexOptions.Singleline);

  private static readonly Regex Arm = new(
    @"""([A-Z][A-Za-z]+\.[A-Za-z][A-Za-z0-9]*)""\s*=>",
    RegexOptions.Compiled);

  private static readonly Regex Returned = new(
    @"\b([A-Za-z]+Errors)\.([A-Za-z][A-Za-z0-9]*)\b",
    RegexOptions.Compiled);

  [Theory]
  [MemberData(nameof(SurfaceNames))]
  public void Every_error_a_module_returns_has_a_mapper_arm_even_when_no_handler_names_it(string surfaceName)
  {
    var surface = Surfaces.Single(entry => entry.Name == surfaceName);
    var moduleRoot = Path.Combine(FindRepositoryRoot(), "src", surface.RelativeRoot);

    Assert.True(Directory.Exists(moduleRoot), $"{moduleRoot} does not exist");

    var sources = Directory.EnumerateFiles(moduleRoot, "*.cs", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
          StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
          StringComparison.Ordinal))
      .ToArray();

    Assert.NotEmpty(sources);

    // MEMBER NAME -> wire code, so a `PayrollErrors.RunAlreadyReversed` reference resolves to whatever
    // string that member actually carries rather than to a name derived from it.
    var definedByShortName = new Dictionary<string, string>(StringComparer.Ordinal);
    var mapped = new HashSet<string>(StringComparer.Ordinal);
    var returnedShortNames = new HashSet<string>(StringComparer.Ordinal);

    foreach (var path in sources)
    {
      // ⚠ COMMENTS ARE STRIPPED, AND THIS FILE IS THE REASON IT IS NOT OPTIONAL. The mappers now carry
      // comments naming the very codes under test — `Attendance.LeaveSubmissionBusy` is written out in a
      // Payroll comment explaining the shared seam — so prose would satisfy every match below.
      var source = WithoutComments(File.ReadAllText(path));
      var fileName = Path.GetFileName(path);

      if (fileName.EndsWith("Errors.cs", StringComparison.Ordinal))
      {
        foreach (Match match in Definition.Matches(source))
        {
          definedByShortName[match.Groups[1].Value] = match.Groups[2].Value;
        }

        continue;
      }

      if (fileName.EndsWith("ErrorMapper.cs", StringComparison.Ordinal))
      {
        foreach (Match match in Arm.Matches(source))
        {
          mapped.Add(match.Groups[1].Value);
        }
      }

      foreach (Match match in Returned.Matches(source))
      {
        returnedShortNames.Add(match.Groups[2].Value);
      }
    }

    // The definitions file for a module also names its own errors; a code is only "returned" if some OTHER
    // file in the module references it.
    var returned = returnedShortNames
      .Where(definedByShortName.ContainsKey)
      .Select(shortName => definedByShortName[shortName])
      .OrderBy(code => code, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      returned.Length >= MinimumReturnedCodes[surface.Name],
      $"{surface.Name}: only {returned.Length} returned codes found, below the floor of " +
      $"{MinimumReturnedCodes[surface.Name]}. The scan has degraded rather than the module having shrunk.");

    // HR has no single mapper file; its three mappers are picked up by the ErrorMapper.cs suffix above.
    //
    // ---- ⚠ AND THE DELIBERATELY-UNMAPPED CODES ARE CITED, NOT RE-DECIDED.
    //
    // `ModuleErrorMappingArchitectureTests` already argues each one. `Payroll.OneOffPaymentConsumingRunRequired`
    // is the live example: `OneOffPayment.MarkConsumedBy` refuses a `Guid.Empty` run id, no route can
    // produce that, and mapping it would add a dead arm — which T-095 established is worse than an honest
    // gap. **A second copy of that list here would go stale against the first**, so this reads it.
    var recorded = ModuleErrorMappingArchitectureTests.RecordedUnmapped();
    var unmapped = returned.Where(code => !mapped.Contains(code) && !recorded.Contains(code)).ToArray();

    Assert.True(
      unmapped.Length == 0,
      $"{surface.Name} returns codes with no mapper arm, so they answer 500 `request.failed`:" +
      Environment.NewLine + string.Join(Environment.NewLine, unmapped.Select(code => "  " + code)));
  }

  public static TheoryData<string> SurfaceNames()
  {
    var data = new TheoryData<string>();
    foreach (var surface in Surfaces)
    {
      data.Add(surface.Name);
    }

    return data;
  }

  private static string WithoutComments(string source) => string.Join(
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

  private sealed record ModuleSurface(
    string Name, string RelativeRoot, string ApiProject, string? MapperFile);
}
