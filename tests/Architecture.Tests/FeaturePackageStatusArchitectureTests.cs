using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// A FEATURE PACKAGE HAS ONE STATUS, AND IT LIVES IN THE README (T-101).
// ==================================================================================================
//
// ---- THE CONVENTION THIS DISSOLVED, AND WHY REPAIRING IT WAS THE WRONG ANSWER.
//
// Every FP document used to carry its own status in its title — `(proposed)`, `(RATIFIED)`, `Proposed
// requirements`. **Twenty-seven of them, and five of six packages carried it STALE:**
//
//   FP-006   0 of 12 documents said "proposed"
//   FP-011   5 of 12        while the README said APPROVED
//   FP-009   5 of 12        while the README said Approved for Implementation
//   FP-013   8 of 13        while the README said DELIVERED
//   FP-014   8 of 13        while the README said RATIFIED
//   FP-012   9 of 13        while the README said DELIVERED
//
// **The history proves nobody maintained it.** `837eea6 docs(T-004): reconcile the FP-012 and FP-013 README
// status with what shipped` moved both READMEs and left every document title untouched.
//
// **And nothing mechanical read it.** A sweep of `.cs`, `.sh`, `.ps1`, `.yml` and `.yaml` outside `docs/`
// for every status term found only prose in comments. **So a stale marker misleads a reader and breaks
// nothing — which is exactly why it sat wrong for a whole package unnoticed.**
//
// Repairing twenty-seven titles would have fixed one instance of a convention nobody follows, and it would
// have been stale again by the next delivery. **`UnroutedFamilies` was the same shape and was dissolved
// rather than patched, for the same reason.**
//
// ---- WHAT SURVIVES, AND THE DISTINCTION IS THE WHOLE OF THIS GUARD.
//
// **A status word naming a DOCUMENT is not a status marker.** `decisions-ratified.md` is titled *"Ratified
// decisions"* because that is what the document IS — a record of the decisions that were ratified — and
// deleting the word would rename the file's subject rather than remove a claim about its state.
//
// **The filename is the test, not an allow-list.** A title may lead with a status word exactly when the
// document's own name carries it; that needs no maintained exception and cannot go stale, because the two
// move together or the file is renamed.
public sealed class FeaturePackageStatusArchitectureTests
{
  private static readonly string[] StatusTerms =
    ["proposed", "draft", "approved", "ratified", "delivered", "closed"];

  // ---- A TRAILING `(...)` MARKER. The shape the convention actually used.
  [Fact]
  public void No_feature_document_title_carries_a_parenthesised_status_marker()
  {
    var offending = FeatureDocuments()
      .Where(document => StatusTerms.Any(term => Regex.IsMatch(
        document.Title,
        $@"\(\s*{term}\s*\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
      .Select(document => $"{document.Package}/{document.File}  ::  {document.Title}")
      .OrderBy(line => line, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      offending.Length == 0,
      "A feature document's title carries its own status. **A package has ONE status and it lives in the " +
      "README** — a per-document marker is a second claim that nobody updates, and five of six packages " +
      "carried it stale before it was dissolved. Put the status in the README or leave it out:" +
      $"{Environment.NewLine}{string.Join(Environment.NewLine, offending)}");
  }

  // ---- AND THE ADJECTIVE FORM, WHICH THE PARENTHESIS RULE ALONE WOULD MISS.
  //
  // `# FP-013 — Proposed requirements` made exactly the same claim with different punctuation. **A guard
  // that caught only the bracketed form would have left three of the twenty-seven standing** and read as
  // though the convention were gone.
  [Fact]
  public void No_feature_document_title_leads_with_a_status_word_its_filename_does_not_carry()
  {
    var offending = FeatureDocuments()
      .Select(document => new
      {
        document.Package,
        document.File,
        document.Title,
        Leading = Regex.Match(document.Title, @"^#\s*FP-\d+\s*[—-]\s*(\w+)", RegexOptions.CultureInvariant)
      })
      .Where(document => document.Leading.Success)
      .Where(document => StatusTerms.Contains(document.Leading.Groups[1].Value.ToLowerInvariant()))
      // THE FILENAME IS THE JUSTIFICATION, NOT AN EXCEPTION LIST. `decisions-ratified.md` may be titled
      // "Ratified decisions" because the word names the document; `requirements.md` may not be titled
      // "Proposed requirements" because it does not.
      .Where(document => !document.File.Contains(
        document.Leading.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
      .Select(document => $"{document.Package}/{document.File}  ::  {document.Title}")
      .OrderBy(line => line, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      offending.Length == 0,
      "A feature document's title leads with a status word its filename does not carry, which is the same " +
      "claim the bracketed form made. A status word is allowed only where it NAMES the document — " +
      $"`decisions-ratified.md` titled \"Ratified decisions\" — and refused where it describes its state:" +
      $"{Environment.NewLine}{string.Join(Environment.NewLine, offending)}");
  }

  // ---- THE README IS UNTOUCHED BY BOTH RULES, AND THAT IS DELIBERATE.
  //
  // Dissolving the per-document marker makes the README's status line the SINGLE source. A guard that also
  // policed the README would be policing the thing it just made authoritative.
  //
  // This asserts only that the source exists — a package with no status at all would leave the marker's
  // dissolution having removed the last statement rather than the duplicate one.
  [Fact]
  public void Every_feature_package_states_its_status_in_its_readme()
  {
    var packages = Directory.EnumerateDirectories(FeaturesRoot())
      .Where(directory => Path.GetFileName(directory).StartsWith("FP-", StringComparison.Ordinal))
      .OrderBy(directory => directory, StringComparer.Ordinal)
      .ToArray();

    // NOT VACUOUS. A features root that stopped matching would leave this passing over nothing.
    Assert.NotEmpty(packages);

    var silent = packages
      .Where(directory =>
      {
        var readme = Path.Combine(directory, "README.md");

        return !File.Exists(readme) || !Regex.IsMatch(
          File.ReadAllText(readme),
          @"^\s*status\s*:",
          RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
      })
      .Select(Path.GetFileName)
      .ToArray();

    Assert.True(
      silent.Length == 0,
      "A feature package states no status in its README. Since T-101 that is the ONLY place a status is " +
      $"recorded, so a package without one records none at all:{Environment.NewLine}" +
      string.Join(Environment.NewLine, silent));
  }

  private static (string Package, string File, string Title)[] FeatureDocuments()
  {
    var documents = Directory
      .EnumerateFiles(FeaturesRoot(), "*.md", SearchOption.AllDirectories)
      .Where(path => !string.Equals(Path.GetFileName(path), "README.md", StringComparison.OrdinalIgnoreCase))
      .Select(path => (
        Package: Path.GetFileName(Path.GetDirectoryName(path))!,
        File: Path.GetFileName(path),
        Title: File.ReadLines(path).FirstOrDefault() ?? string.Empty))
      .Where(document => document.Title.StartsWith('#'))
      .ToArray();

    // NOT VACUOUS, and it belongs here rather than in each test: a sweep that stopped finding documents
    // would make both rules above pass over an empty set, which is the failure mode every register in this
    // suite has had to be protected from.
    Assert.NotEmpty(documents);

    return documents;
  }

  private static string FeaturesRoot() => Path.Combine(RepositoryRoot(), "docs", "17-features");

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
