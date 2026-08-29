using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// A COMMENT THAT CITES AN IDENTIFIER MUST CITE ONE THAT EXISTS (T-182).
// ==================================================================================================
//
// ---- WHY THIS IS A GUARD AND NOT A SWEEP.
//
// T-180 measured the rate: **1 broken citation in 70, and BOTH known breaks were written that same day** —
// one naming a code deleted three tasks earlier, one naming `ICalendarRepository` where the type is
// `IWorkingCalendarRepository`, typed from memory four hours before. **This class does not decay from age;
// it REGENERATES, and fastest in the comments written to explain a fix.** A sweep measures a rate. A guard
// holds it at zero.
//
// This sweep found four more across `src/`, all of the same shape: a test renamed and three citations left
// behind, and a file that was split in two.
//
// ---- ⚠ THE FIRST VERSION OF THIS CHECK REPORTED PERFECT HEALTH, AND THAT IS WHY IT RESOLVES CODE ONLY.
//
// It resolved citations against a blob that INCLUDED comments, so a comment citing a dead identifier found
// that identifier **in its own line**. It reported 0 unresolvable out of 70; stripping comments found 2.
// **An instrument whose search space contains the thing being checked is not an instrument, it is a
// mirror** — and it returns a clean number rather than an error.
//
// ---- WHAT IT DELIBERATELY DOES NOT CHECK.
//
// Rule ids (`BR-GL-0002`, `DEC-L-084`, `AC-TEN-0020`) and prose references. **Resolving those needs a
// registry that does not exist, and inferring one would put a guess inside a guard** — the same refusal
// made on the permission axis, where 73 rows stayed uncovered rather than have their prefix guessed.
public sealed class CommentCitationGuardTests
{
  // Backticked, and long enough not to catch prose: `Type.Member`, `A_test_name_like_this`, `File.cs`.
  private static readonly Regex Cited = new("`([A-Za-z][A-Za-z0-9_.]{3,})`", RegexOptions.Compiled);
  private static readonly Regex MemberLike = new("^[A-Z][A-Za-z0-9]*\\.[A-Za-z][A-Za-z0-9]*$", RegexOptions.Compiled);
  private static readonly Regex TestLike = new("^[A-Z][a-z][A-Za-z0-9]*(_[a-z0-9]+){2,}$", RegexOptions.Compiled);
  private static readonly Regex FileLike = new("^[A-Za-z][A-Za-z0-9_.-]*\\.[a-z]{2,4}$", RegexOptions.Compiled);

  // ---- RECORDED RATHER THAN RESOLVED, EACH WITH ITS REASON — the `KnownUnmapped` shape.
  //
  // ⚠ **Two of these are DELIBERATE references to things that no longer exist**, and a guard that refused
  // them would be a guard that forbids recording a removal. **Naming the removed thing is the point of
  // those comments.** The other two are this test's own classifier being wrong, reported rather than
  // quietly excluded — **a false positive reported is a floor measurement; a false positive silently
  // dropped is a guard learning to lie.**
  private static readonly HashSet<string> KnownUnresolvable = new(StringComparer.Ordinal)
  {
    // DELIBERATE: T-179 records that this code was deleted in T-168. The citation IS the record.
    "Payroll.CompensationNotFound",

    // DELIBERATE: T-125 records two constants that "used to live in this file and neither should have".
    "Payroll.OneOffPaymentPayElementNotFound",
    "Payroll.OneOffPaymentNotFound",

    // CLASSIFIER FAULT: an illustrative placeholder in prose about upload filenames, not a file.
    "x.csv",

    // CLASSIFIER FAULT: a permission NAME in lowercase-dotted form, which `FileLike` cannot tell from a
    // filename. Tightening the pattern to exclude it would also exclude real lowercase filenames.
    "payroll.payslip.view.self",
  };

  [Fact]
  [Trait("Decision", "DEC-L-085")]
  public void Every_cited_identifier_resolves_or_is_recorded()
  {
    var unresolved = Unresolved()
      .Where(entry => !KnownUnresolvable.Contains(entry.Cited))
      .Select(entry => $"{entry.File}:{entry.Line}  {entry.Kind}  {entry.Cited}")
      .OrderBy(entry => entry, StringComparer.Ordinal)
      .ToArray();

    Assert.Empty(unresolved);
  }

  // ---- AND THE RECORDED SET MUST NOT OUTLIVE ITS ENTRIES.
  //
  // A name left here after its citation is fixed makes the list a place where anything can hide. This
  // fails when an entry stops being needed — the same reasoning as `KnownUnmapped`'s "if one was just
  // mapped, remove it in the same commit".
  [Fact]
  public void Nothing_is_recorded_that_no_longer_needs_to_be()
  {
    var actuallyUnresolvable = Unresolved().Select(entry => entry.Cited).ToHashSet(StringComparer.Ordinal);

    Assert.Empty(KnownUnresolvable.Where(known => !actuallyUnresolvable.Contains(known)));
  }

  // ---- THE FLOOR, AND IT IS A FLOOR RATHER THAN AN EQUALITY ON PURPOSE.
  //
  // The failure this guards against is an extractor that **stops finding citations** — a regex edit, a
  // comment style change — which turns the assertion above green everywhere at once. A floor catches that.
  //
  // **An exact count would also fire on every new comment**, which is churn rather than signal: a citation
  // added is coverage arriving, and it is already checked by the assertion above. A number bumped
  // reflexively is worse than no number — it trains its readers to update without reading.
  //
  // ---- ⚠ AND THE FLOOR IS TIGHT ON PURPOSE. IT WAS 300 AGAINST A MEASURED 357 AND THAT WAS TOO LOOSE.
  //
  // **Extractors rarely stop dead; they DEGRADE** — a pattern stops seeing one comment style, one file
  // convention, one syntax. A 16% margin catches catastrophic failure and passes for exactly the partial
  // degradation that is the likely case. **Every one of the six instrument failures logged on 2026-08-29
  // was partial; not one returned nothing, and that is why each was believed.**
  //
  // ---- ⚠ AND IT BUYS INSTRUMENT HEALTH, NOT COVERAGE. THE TWO WERE CONFLATED WHEN THIS WAS TIGHTENED.
  //
  // **A floor proves the instrument still works and says nothing about the subject.** Delete seven
  // citations from source and 350 still passes — this test would not notice, and it is not meant to. What
  // it notices is the EXTRACTOR going quiet.
  //
  // The distinction is between a corpus whose size is outside our control and grows with ordinary work —
  // comments, error codes, routes, which take a floor — and a set WE control whose membership is the
  // point, which takes an exact count because fewer means a member left. **Comments are the first kind**,
  // so a floor is right here and an exact count would be churn.
  //
  // Recorded because the tightening from 300 to 350 was described in terms that implied both, and a guard
  // believed to protect coverage while protecting only liveness is the more dangerous of the two errors.
  //
  // A tight floor costs no churn: it never needs raising as comments accumulate, only attention when
  // citations are legitimately removed — which is rare and deliberate. Measured at 357 across 1,127 files
  // on 2026-08-29.
  [Fact]
  public void The_citation_population_has_not_collapsed()
  {
    Assert.True(
      Citations().Count() >= 350,
      $"only {Citations().Count()} citations found; the extractor has probably stopped matching");
  }

  private static IEnumerable<(string File, int Line, string Kind, string Cited)> Unresolved()
  {
    var code = CodeWithoutComments();
    var names = RepositoryFileNames();

    foreach (var entry in Citations())
    {
      var ok = entry.Kind switch
      {
        "file" => names.Contains(entry.Cited),
        "test" => Regex.IsMatch(code, "\\b" + Regex.Escape(entry.Cited) + "\\b"),
        _ => Resolves(code, entry.Cited),
      };

      if (!ok)
      {
        yield return entry;
      }
    }
  }

  private static bool Resolves(string code, string cited)
  {
    var parts = cited.Split('.');
    return Regex.IsMatch(code, "\\b" + Regex.Escape(parts[0]) + "\\b") &&
      Regex.IsMatch(code, "\\b" + Regex.Escape(parts[1]) + "\\b");
  }

  private static IEnumerable<(string File, int Line, string Kind, string Cited)> Citations()
  {
    foreach (var path in SourceFiles())
    {
      var lines = File.ReadAllLines(path);
      for (var index = 0; index < lines.Length; index++)
      {
        if (!lines[index].TrimStart().StartsWith("//", StringComparison.Ordinal))
        {
          continue;
        }

        foreach (Match match in Cited.Matches(lines[index]))
        {
          var value = match.Groups[1].Value;
          var kind =
            FileLike.IsMatch(value) ? "file" :
            TestLike.IsMatch(value) ? "test" :
            MemberLike.IsMatch(value) ? "member" : null;

          if (kind is not null)
          {
            yield return (Path.GetFileName(path), index + 1, kind, value);
          }
        }
      }
    }
  }

  // ⚠ COMMENTS STRIPPED. See the header: resolving against a blob containing the comments lets a citation
  // confirm itself, and the first version of this check did exactly that and reported zero.
  // ⚠ AND THIS FILE IS EXCLUDED, WHICH IS THE SECOND MIRROR AND WAS FOUND BY RUNNING THE THING.
  //
  // **A blob containing THIS file lets `KnownUnresolvable` vindicate its own entries** — each is a string
  // literal here, and the resolver only asks whether the identifier appears in code.
  // `Nothing_is_recorded_that_no_longer_needs_to_be` caught it on the first run, which is the argument for
  // that test existing: **an allow-list that can vindicate itself is not a record, it is a loophole.**
  private static string CodeWithoutComments() => string.Join(
    '\n',
    AllCsharpFiles()
      .Where(path => !path.EndsWith(nameof(CommentCitationGuardTests) + ".cs", StringComparison.Ordinal))
      .SelectMany(File.ReadAllLines)
      .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

  // ⚠ THE WHOLE REPOSITORY, NOT `.cs` ONLY. A `.md` cited from source is a real reference, and the first
  // version of this check reported `Constraints.md` as broken because it only knew C# filenames.
  private static HashSet<string> RepositoryFileNames() => Directory
    .EnumerateFiles(FindRepositoryRoot(), "*", SearchOption.AllDirectories)
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
    .Select(Path.GetFileName)
    .ToHashSet(StringComparer.Ordinal)!;

  private static IEnumerable<string> SourceFiles() =>
    AllCsharpFiles().Where(path => path.Contains(
      $"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

  private static IEnumerable<string> AllCsharpFiles() => Directory
    .EnumerateFiles(FindRepositoryRoot(), "*.cs", SearchOption.AllDirectories)
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

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
