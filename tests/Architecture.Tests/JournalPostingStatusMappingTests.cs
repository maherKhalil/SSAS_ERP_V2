using SSAS.GL.Contracts.Posting;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY MEMBER OF A CROSS-MODULE STATUS ENUM IS NAMED BY EVERY CONSUMER THAT MAPS IT (249).
// ==================================================================================================
//
// `JournalPostingStatus` is GL's contract and Payroll switches on it. Before 249 those two sites were
// TERNARIES — `Status == PeriodClosed ? closed : refused` — so a new member routed silently into the
// generic refusal. No default arm to notice, no switch for an analyser to inspect.
//
// ---- ⚠⚠⚠ AND THE COMPILER CANNOT DO THIS, WHICH IS NOT OBVIOUS AND WAS MEASURED.
//
// A switch expression over an enum that covers every NAMED member still warns, because the underlying
// integral type admits unnamed values. MEASURED 2026-09-01 by deleting the discard arm and building with
// `--no-incremental`:
//
//   warning CS8524: The switch expression does not handle some values of its input type (it is not
//   exhaustive) involving an unnamed enum value. For example, the pattern '(JournalPostingStatus)7' is
//   not covered.
//
// So a discard arm is FORCED — this repository gates at zero warnings, so CS8524 is a red build.
// ⚠⚠ AND THE DISCARD THAT SILENCES CS8524 IS THE SAME DISCARD THAT DISABLES CS8509, the diagnostic for a
// missing NAMED member. THE TWO ARE IN TENSION: satisfying one disables the other. COMPILER
// EXHAUSTIVENESS IS NOT AVAILABLE FOR THIS SHAPE AT ALL.
//
// ⚠ The first attempt to measure this was VOID: an incremental build reported success because MSBuild
// skipped the up-to-date project and never re-emitted the warning — the same trap `scripts/gate.sh`
// documents and passes `--no-incremental` to avoid.
//
// ---- SO THIS IS A TEXT ASSERTION, AND IT IS WEAKER THAN EXHAUSTIVENESS. SAYING SO IS THE POINT.
//
// It proves each member is NAMED in the consumer's mapping source. It does not prove the mapping is
// correct, or that the arm is reachable. It is the only CI-time detection this shape permits, and a test
// that implied the compiler was helping would be worse than none.
public sealed class JournalPostingStatusMappingTests
{
  private const string Consumer =
    "src/Modules/Payroll/SSAS.Payroll.Application/Runs/PayrollRunCommandHandlers.cs";

  [Fact]
  [Trait("Decision", "DEC-PAY-0018")]
  public void Every_journal_posting_status_is_named_by_the_payroll_mapping()
  {
    // ---- THE POPULATION IS THE ENUM ITSELF, WHICH CANNOT DRIFT OUT FROM UNDER THE TEST.
    //
    // ⚠ AND THE PREDICATE DOES NOT NAME THE ASSERTION: membership is "is a member of the enum", never
    // "is a member that happens to be mapped". A population defined by the property under test would go
    // quiet exactly when a member stopped being handled.
    var members = Enum.GetNames<JournalPostingStatus>();

    // Anti-vacuity: a reflection call that returned nothing would satisfy every assertion below.
    Assert.True(members.Length >= 7, $"the enum has {members.Length} members; it should have at least 7");

    var source = File.ReadAllText(Path.Combine(
      FindRepositoryRoot(), Consumer.Replace('/', Path.DirectorySeparatorChar)));

    var mapping = string.Join(
      '\n',
      source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    // Comments are stripped first, for the reason the journal-posting order test records: a comment
    // naming the members in prose satisfies every search, and the comments here NAME these very members.
    var unmapped = members
      .Where(member => !mapping.Contains(
        $"JournalPostingStatus.{member}", StringComparison.Ordinal))
      .ToArray();

    Assert.True(
      unmapped.Length == 0,
      $"PayrollRunCommandHandlers names no arm for: {string.Join(", ", unmapped)}. " +
      "A member nobody maps falls to the discard and is reported as a generic ledger refusal, which for " +
      "a retryable condition tells the operator to stop when they should retry.");
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
