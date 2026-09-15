using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY ERROR CODE IS A STRING LITERAL AT ITS CONSTRUCTION SITE.
// ==================================================================================================
//
// ---- ⚠⚠⚠ THIS GUARDS AN INSTRUMENT, NOT A FEATURE, AND THAT IS WHY IT EXISTS.
//
// The product's refusal surface is auditable — *which refusals does this product produce, and does anything
// witness them?* — **only because the set of error codes is COMPLETABLE by reading the source.** Measured
// while building that audit: **536 codes, and ZERO `new Error(` calls take a first argument that is not a
// string literal.**
//
// ***THAT COMPLETABILITY IS A PROPERTY OF THE MEASURING APPARATUS, NOT OF THE PRODUCT — AND IT IS CURRENTLY
// A FACT THAT CAN LAPSE RATHER THAN AN INVARIANT.*** One `new Error(SomeConstant, …)` and every future
// census silently undercounts: the code exists, the product raises it, and no scan of the tree can name it.
//
// ⚠ APPLY THE SILENCE TEST TO THE LAPSE ITSELF: **if this property broke, what would anyone see? NOTHING.**
// No build error, no failing test, no behavioural change — just a census that is quietly short, and a
// refusal nobody knows to ask about. *That is the exact shape this audit was built to find, occurring in
// the audit's own foundation.*
//
// ---- WHAT IS ASSERTED, AND WHY THE TWO SPELLINGS.
//
// `Error` is constructed two ways in this tree — explicitly as `new Error("X.Y", …)` and target-typed as
// `= new("X.Y", …)` or `=> new("X.Y", …)` where the declared type supplies it. **Both are checked**: a rule
// that covered only the explicit spelling would be silently vacuous over the target-typed one, which is the
// commoner form here.
//
// ⚠⚠ THE THREE DECLARATION SHAPES ARE NOT THE SUBJECT AND ARE WORTH NAMING SO A READER DOES NOT LOOK FOR
// THEM HERE: a code may be a static field, a parameterised factory method, or constructed inline at the use
// site. **All three construct with a literal first argument, which is what makes them enumerable at all,
// and this test is indifferent to which of the three a given code uses.**
public sealed class ErrorCodeLiteralArchitectureTests
{
  // `new Error(` and the target-typed `new(` when an `Error` is being declared or returned. The second
  // pattern is deliberately anchored on `Error` appearing in the same declaration, so it cannot drift into
  // matching every target-typed construction in the tree.
  private static readonly Regex ExplicitConstruction = new(
    @"\bnew\s+Error\s*\(\s*(.)", RegexOptions.Compiled);

  private static readonly Regex TargetTypedConstruction = new(
    @"\bError\s+\w+(?:\s*\([^()]*\))?\s*=>?\s*new\s*\(\s*(.)", RegexOptions.Compiled);

  [Fact]
  public void Every_error_is_constructed_with_a_string_literal_code()
  {
    var offenders = new List<string>();
    var constructions = 0;

    foreach (var path in ProductionSourceFiles())
    {
      var source = File.ReadAllText(path);

      foreach (var pattern in new[] { ExplicitConstruction, TargetTypedConstruction })
      {
        foreach (Match match in pattern.Matches(source))
        {
          constructions++;

          // ⚠ ONE EXEMPTION, ON GROUNDS RATHER THAN BY NAME: a construction whose code is `string.Empty`.
          //
          // `Error.None` is the type's own "no error" sentinel — `new(string.Empty, string.Empty)`. **An
          // EMPTY code is not a code**: it names no refusal, no mapper can route it, and a census that
          // failed to enumerate it would have missed nothing. *The exemption is therefore about what the
          // value IS, not about which declaration it happens to sit in* — a second sentinel added elsewhere
          // would be exempt for the same reason, and a computed code would not be exempt for any.
          if (source.Substring(match.Index, Math.Min(60, source.Length - match.Index))
            .Contains("string.Empty", StringComparison.Ordinal)
            && match.Groups[1].Value == "s")
          {
            continue;
          }

          // The first argument's opening character. A literal starts with a quote; anything else — an
          // identifier, an interpolation, a `$"` — is a code this tree's audits cannot enumerate.
          if (match.Groups[1].Value != "\"")
          {
            var line = source.Take(match.Index).Count(c => c == '\n') + 1;
            offenders.Add($"{Path.GetFileName(path)}:{line}  starts with '{match.Groups[1].Value}'");
          }
        }
      }
    }

    // ⚠ THE ANTI-VACUITY FLOOR. Both patterns are source-text matches over a directory walk — the exact
    // shape that passed nine of nine tests in this file's neighbour while measuring nothing. 536 codes were
    // catalogued when this was written; the floor sits well below with room for consolidation, and its
    // purpose is that ZERO CANNOT PASS.
    Assert.True(constructions >= 400,
      $"only {constructions} Error constructions were found; the patterns or the file walk have stopped " +
      "matching and the check below would judge nothing.");

    Assert.True(offenders.Count == 0,
      "an Error is constructed with a code that is not a string literal. The refusal surface of this " +
      "product is auditable only because every code can be read out of the source, and a computed or " +
      "interpolated code is invisible to every such audit — the census silently undercounts and the " +
      "refusal it misses is one nobody knows to ask about:\n  " + string.Join("\n  ", offenders));
  }

  private static IReadOnlyCollection<string> ProductionSourceFiles() =>
  [
    .. Directory
      .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
      .Where(path =>
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
  ];

  private static string FindRepositoryRoot()
  {
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}
