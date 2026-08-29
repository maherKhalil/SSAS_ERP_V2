using System.Text.RegularExpressions;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// EVERY ROUTE ROW IN EVERY api-contracts.md EITHER EXISTS OR SAYS WHY NOT (T-161).
// ==================================================================================================
//
// ---- WHAT THIS REPLACES, AND WHY IT IS A TEST RATHER THAN A CONVENTION.
//
// T-157 through T-160 reconciled four contract documents by hand and found FP-014 describing twenty-four
// routes that have never answered a request. The obvious follow-up was to write the marker convention into
// `Feature-Package-Template.md` — **and that template has ZERO referrers and four packages were built after
// it without consulting it** (T-161 establish). `DEC-L-002`: no gate reads prose. **A convention with no
// instrument is the shape this whole sweep existed to find.**
//
// So the convention IS this test. A row that describes a route which does not exist must say so inline.
//
// ---- ⚠ THE THREE FORMATS, EACH OF WHICH DEFEATED MY OWN SWEEP FIRST.
//
// ```
// bare line     POST /api/platform/tenants                     FP-001, FP-003, FP-013, FP-014
// table row     | POST | `/api/payroll/...` |                  FP-012
//               | `GET /api/platform/localization/...` | ...   FP-004
// payload head  POST /api/gl/journals  followed by `{`         FP-011 — NOT a contract row
// ```
//
// **The path-only sweep that produced these reconciliations reported FP-004 and FP-012 as fully accurate
// while extracting zero rows from them**, because it read bare lines only. A guard that did the same would
// be green and vacuous on two documents. **`DEC-L-070`: `Every_contract_document_yields_rows` exists so
// that an extractor which stops working fails here rather than passing everywhere.**
//
// ---- AND MARKERS ARE NOT ALL ONE STATE.
//
// `[NOT ROUTED - handler: X]`, `[BUILT as ...]`, `[SERVED BY ...]`, `[DEFERRED - AC-TEN-0020]` and
// `[SUPERSEDED - ...]` mean materially different things — deferred is "not yet", superseded is "never", and
// "built under another path" is not a gap at all. **This test does not adjudicate WHICH marker is right;
// it only requires that an unbuilt row carries one.** Choosing the marker is a human reading the code.
public sealed class ApiContractRowGuardTests(HostWebApplicationFactory factory)
  : IClassFixture<HostWebApplicationFactory>
{
  private static readonly Regex BareLine = new(
    @"^(GET|POST|PUT|DELETE|PATCH)\s+(/api/\S+)", RegexOptions.Compiled);

  // `| POST | `/api/...` |` — verb and path in separate cells.
  private static readonly Regex SplitCell = new(
    @"^\|\s*`?(GET|POST|PUT|DELETE|PATCH)`?\s*\|\s*`?(/api/\S+?)`?\s*\|", RegexOptions.Compiled);

  // `| `GET /api/...` | ... |` — verb and path in one cell.
  private static readonly Regex JoinedCell = new(
    @"^\|\s*`?(GET|POST|PUT|DELETE|PATCH)\s+(/api/\S+?)`?\s*\|", RegexOptions.Compiled);

  private static readonly Regex Marker = new(@"\[[A-Z][A-Z ]+\b", RegexOptions.Compiled);

  [Fact]
  [Trait("Decision", "DEC-L-085")]
  public void Every_documented_route_row_is_live_or_carries_a_marker()
  {
    var live = LiveRoutes();

    // Without this the comparison below passes against an empty live set — the failure that would make
    // every row look unbuilt (`DEC-L-070`).
    Assert.NotEmpty(live);

    var unexplained = ContractDocuments()
      .SelectMany(RowsOf)
      .Where(row => !row.HasMarker)
      .Where(row => !live.Contains($"{row.Method} {row.Path}"))
      .Select(row => $"{row.Document}:{row.Line}  {row.Method} {row.Path}")
      .OrderBy(entry => entry, StringComparer.Ordinal)
      .ToArray();

    Assert.Empty(unexplained);
  }

  // ---- THE EXTRACTOR MUST KEEP WORKING, AND ONLY THIS SAYS SO.
  //
  // A regex that stops matching makes the guard above green on every document at once. **Naming the
  // documents that must yield rows means a broken extractor fails here** rather than silently retiring the
  // guard — the same reasoning as `Known_real_dependencies_are_actually_discovered`.
  [Theory]
  [InlineData("FP-001-identity-access")]
  [InlineData("FP-003-tenant-lifecycle")]
  [InlineData("FP-004-localization")]
  [InlineData("FP-012-payroll")]
  [InlineData("FP-013-attendance")]
  [InlineData("FP-014-subscription")]
  public void Every_contract_document_yields_rows(string package)
  {
    var document = Path.Combine(
      FindRepositoryRoot(), "docs", "17-features", package, "api-contracts.md");

    Assert.True(File.Exists(document), $"{package} has no api-contracts.md");
    Assert.NotEmpty(RowsOf(document));
  }

  private HashSet<string> LiveRoutes() => PlatformRouteInventory.Under(factory, "/api")
    .Select(endpoint =>
      $"{PlatformRouteInventory.FirstMethodOf(endpoint)} {Normalize(endpoint.RoutePattern.RawText!)}")
    .ToHashSet(StringComparer.Ordinal);

  private static IEnumerable<string> ContractDocuments() => Directory.EnumerateFiles(
    Path.Combine(FindRepositoryRoot(), "docs", "17-features"),
    "api-contracts.md",
    SearchOption.AllDirectories);

  private static (string Document, int Line, string Method, string Path, bool HasMarker)[] RowsOf(
    string document)
  {
    var lines = File.ReadAllLines(document);
    var rows = new List<(string, int, string, string, bool)>();

    for (var index = 0; index < lines.Length; index++)
    {
      var line = lines[index];
      var match = BareLine.Match(line);
      if (!match.Success)
      {
        match = SplitCell.Match(line);
      }

      if (!match.Success)
      {
        match = JoinedCell.Match(line);
      }

      if (!match.Success)
      {
        continue;
      }

      // ⚠ A REQUEST-BODY EXAMPLE IS NOT A CONTRACT ROW, AND FP-011 IS WHY THIS IS HERE.
      //
      // `POST /api/gl/journals` under "Shapes that depend on an owner decision" is the header of a JSON
      // payload sample. Treating it as a row would demand a marker on an illustration — and the route it
      // illustrates is deliberately absent (GL posts through journal-drafts).
      if (NextMeaningfulLineOpensJson(lines, index))
      {
        continue;
      }

      rows.Add((
        Path.GetFileName(Path.GetDirectoryName(document))!,
        index + 1,
        match.Groups[1].Value,
        Normalize(match.Groups[2].Value),
        Marker.IsMatch(line)));
    }

    return rows.ToArray();
  }

  private static bool NextMeaningfulLineOpensJson(string[] lines, int index)
  {
    for (var next = index + 1; next < lines.Length; next++)
    {
      if (lines[next].Trim().Length == 0)
      {
        continue;
      }

      return lines[next].TrimStart().StartsWith('{');
    }

    return false;
  }

  // Placeholder names differ between a document and a route pattern — `{tenantId}` against `{id}` — and
  // neither is more correct. The comparison is on shape.
  private static string Normalize(string path)
  {
    var trimmed = Regex.Replace(path, @"\{[^}]*\}", "{}");
    var query = trimmed.IndexOf('?', StringComparison.Ordinal);
    if (query >= 0)
    {
      trimmed = trimmed[..query];
    }

    trimmed = trimmed.TrimEnd('`', '.', ',', ')', ':', ';');
    return trimmed.Length > 1 ? trimmed.TrimEnd('/') : trimmed;
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
