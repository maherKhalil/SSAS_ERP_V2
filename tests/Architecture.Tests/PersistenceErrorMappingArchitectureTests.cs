using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY MODULE'S API ERROR MAPPER HANDLES THE PERSISTENCE CODES THAT CAN REACH IT (T-244).
// ==================================================================================================
//
// ---- WHY THE EXISTING EXHAUSTIVENESS ARGUMENT DID NOT COVER THIS.
//
// Each mapper's header says the mapping is *"exhaustive by construction"* — the default arm is a 500, so a
// **domain** error added without a line here surfaces loudly and the mapper-arm tests catch it.
//
// **`Persistence.UniqueConstraint` is not a domain error of any module.** It is raised by the unit of work
// when SQL Server refuses a write with 2601 or 2627, and it arrives from underneath. Every argument about
// exhaustiveness was about errors declared in the module, so a Platform persistence code reaching the
// mapper was outside what anything checked — and it fell to the default arm as a **500**.
//
// ---- ⚠ THE SAME DEFECT WAS FOUND AND FIXED THREE TIMES WITHOUT ANYONE LOOKING AT THE MAPPER.
//
// T-171, T-173 and T-176 each repaired ONE handler, and each left a long comment recording that the loser
// of a race *"answered 500 for a plain business conflict"*. Three discoveries, three local fixes, and the
// class survived all three — because every remediation was keyed to the path it was pointed at.
//
// **The tell was available every time: the same explanation written three times about three different
// paths. When one explanation fits several places, the thing being explained is not special.** This test is
// the class-level check that none of the three asked for.
public sealed class PersistenceErrorMappingArchitectureTests
{
  // Codes the unit of work can return that any module's mapper may therefore receive. Taken from the
  // catch arms of `TenantUnitOfWork` and `PlatformUnitOfWork`, which are the only producers.
  private static readonly string[] PersistenceCodes =
  [
    "Persistence.UniqueConstraint"
  ];

  [Fact]
  public void Every_api_error_mapper_handles_the_persistence_codes_it_can_receive()
  {
    var mappers = MapperFiles();
    var offenders = new List<string>();

    foreach (var file in mappers)
    {
      var source = File.ReadAllText(file);

      foreach (var code in PersistenceCodes)
      {
        // ⚠ GL IS EXEMPT BY A RECORDED DECISION, NOT BY OVERSIGHT — `DEC-DEP-0027` (T-165).
        //
        // GL has **six** unique indexes: account code, fiscal-year code, draft line number, journal
        // number, one-reversal-per-original, and entry line number. **One arm answers the same thing to
        // all six** — a duplicate account code would be told a journal with that number already exists.
        // T-165 ruled that resolution belongs to the caller who knows which index it can reach, and there
        // **the 500 default is the house rule working rather than the bug.**
        //
        // The pair is named here rather than the flat `RecordedUnmapped()` set being consulted, because
        // that set is union-across-all-sites: consulting it would exempt every module at once and this
        // guard would assert nothing. `Every_recorded_exemption_is_still_recorded` below is what stops
        // this copy going stale against the original, which is the `DEC-L-080` hazard.
        if (Path.GetFileName(file) == "GlApiErrorMapper.cs")
        {
          continue;
        }

        // An arm, not a mention: the code has to appear on the left of a switch arm. A mapper that merely
        // discusses the code in a comment has not mapped it, and comments about it are common here.
        var arm = new Regex($@"""{Regex.Escape(code)}""\s*=>", RegexOptions.Compiled);
        if (!arm.IsMatch(source))
        {
          offenders.Add($"{Path.GetFileName(file)}: no arm for {code}");
        }
      }
    }

    // ⚠ ANTI-VACUITY. Ten mappers exist today. A floor rather than an exact count because adding a module
    // is ordinary work, and the number moving is a side effect of that rather than a decision anyone makes
    // about this rule. It catches the discovery collapsing; it cannot catch one mapper being renamed out of
    // view, which is the same limit every floor has.
    Assert.True(mappers.Count >= 8,
      $"only {mappers.Count} API error mappers were discovered; the search has degraded and zero " +
      "offenders would mean nothing.");

    Assert.True(offenders.Count == 0,
      "a module's API error mapper has no arm for a persistence error the unit of work can hand it, so " +
      "that failure falls to the default arm and answers 500 — a business conflict reported as a server " +
      "fault:\n  " + string.Join("\n  ", offenders) +
      "\n\nAdd an arm. A generic 409 for the module is the floor; a handler-level translation naming the " +
      "constraint is better where the path is known.");
  }

  // The staleness check for the exemption above. If `DEC-DEP-0027` is ever revisited and GL's entry is
  // removed from `ModuleErrorMappingArchitectureTests`, this reddens and the skip has to be reconsidered
  // rather than quietly outliving the decision it cites.
  [Fact]
  public void The_gl_exemption_this_test_honours_is_still_recorded_where_it_was_decided()
  {
    Assert.Contains(
      "Persistence.UniqueConstraint",
      ModuleErrorMappingArchitectureTests.RecordedUnmapped());
  }

  private static List<string> MapperFiles()
  {
    var root = RepositoryRoot();
    return [.. Directory
      .EnumerateFiles(Path.Combine(root, "src"), "*ApiErrorMapper.cs", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))];
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
