using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY PERIOD-STATE WRITER TAKES THE EXCLUSIVE FENCE (250).
// ==================================================================================================
//
// 249 closed a measured defect: a journal could be committed into a period that closed between the
// poster's read and its write. The fence closes it ONLY IF BOTH SIDES TAKE IT — posters take it Shared,
// and whatever changes a period's state takes it Exclusive so that in-flight posts drain first.
//
// A SECOND period-state writer that skipped the fence would reopen the defect in full, and nothing would
// notice: the posters would still be correct, the existing tests would still pass, and the new writer
// would simply close periods underneath them.
//
// ---- ⚠ THE PREDICATE DOES NOT NAME THE ASSERTION, WHICH IS WHY THIS ONE IS BUILDABLE AT ALL.
//
// Membership is TRANSITIONS A FISCAL PERIOD'S STATE. The assertion is TAKES THE EXCLUSIVE FENCE. Had the
// population been "types that take the fence", a writer that skipped it would have left the population
// and the guard would have been strongest exactly where nothing was wrong — the self-selecting shape
// caught in 248 and recorded there.
//
// ---- ONE MEMBER TODAY, WHICH IS THE REASON TO BUILD IT RATHER THAN A REASON NOT TO.
//
// `SetFiscalPeriodStateCommandHandler` is the only caller of `FiscalPeriod.Close()`/`Reopen()` in the
// tree. THIS GUARD EXISTS FOR THE SECOND ONE. A floor of one keeps it from silently becoming zero: a
// derivation that stopped matching would otherwise assert nothing and pass.
//
// ---- ⚠⚠ THE RESIDUAL, STATED RATHER THAN DISCOVERED.
//
// AN APPLICATION LOCK IS A PROTOCOL, NOT A CONSTRAINT. This binds the APPLICATION'S writers and can bind
// nothing else: raw SQL, a DBA at a console, and a migration all change period state without taking any
// fence, and no test in this repository can prevent that. The same is true of every application lock
// here, including `TenantCutoverWriteFence` which 249's fence is modelled on.
public sealed class FiscalPeriodStateWriterFenceTests
{
  private const string ApplicationRoot = "src/Modules/Finance/SSAS.GL.Application";

  [Fact]
  [Trait("Decision", "BR-GL-0003")]
  public void Every_type_that_transitions_a_fiscal_period_takes_the_exclusive_fence()
  {
    var writers = StateWriters();

    // Anti-vacuity. One member today; a derivation that stopped matching would find none and every
    // assertion below would hold trivially.
    Assert.True(
      writers.Count >= 1,
      "no period-state writer was found at all — the derivation has stopped matching, and this guard " +
      "is asserting nothing rather than passing");

    var unfenced = writers
      .Where(writer => !writer.Value.Contains("AcquireForStateChangeAsync", StringComparison.Ordinal))
      .Select(writer => writer.Key)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      unfenced.Length == 0,
      $"these change a fiscal period's state without taking the exclusive fence: " +
      $"{string.Join(", ", unfenced)}. A poster can then commit a journal into a period this closes " +
      "underneath it, which is the defect 249 measured and fixed.");
  }

  // A state writer is any type under the GL application that calls `Close()` or `Reopen()` on a period.
  // That is the MECHANISM the rule turns on — not a name, and not a base type.
  private static Dictionary<string, string> StateWriters()
  {
    var writers = new Dictionary<string, string>(StringComparer.Ordinal);

    foreach (var path in Directory.EnumerateFiles(
      Path.Combine(FindRepositoryRoot(), ApplicationRoot.Replace('/', Path.DirectorySeparatorChar)),
      "*.cs",
      SearchOption.AllDirectories))
    {
      foreach (var (name, body) in Classes(File.ReadAllText(path)))
      {
        if (Regex.IsMatch(body, @"\.(Close|Reopen)\(\)"))
        {
          writers[name] = body;
        }
      }
    }

    return writers;
  }

  // ⚠ Comments are stripped first: this file's own prose names `AcquireForStateChangeAsync`, and a
  // handler that merely MENTIONED the fence in a comment would otherwise satisfy the assertion. The same
  // mirror `JournalPostingOrderTests` and `FiscalYearDefinitionOrderTests` both guard against.
  private static IEnumerable<(string Name, string Body)> Classes(string source)
  {
    var stripped = string.Join(
      '\n',
      source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    var declarations = Regex.Matches(stripped, @"\b(?:public|internal)\s+sealed\s+class\s+(\w+)");

    for (var i = 0; i < declarations.Count; i++)
    {
      var start = declarations[i].Index;
      var end = i + 1 < declarations.Count ? declarations[i + 1].Index : stripped.Length;

      yield return (declarations[i].Groups[1].Value, stripped[start..end]);
    }
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
