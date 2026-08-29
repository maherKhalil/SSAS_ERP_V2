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
  //
  // ---- ⚠ ALL THIRTEEN ARE NAMED, AND THE FIRST VERSION NAMED SIX (T-162).
  //
  // Six was chosen to cover the three FORMATS, and it did — bare lines, split cells, joined cells. **But
  // every one of the thirteen documents yields rows**, so seven were outside the floor entirely: a
  // document that is renamed, emptied or has its table rewritten would have been noticed by nothing.
  //
  // **That is the unicode guard's shape** — a control that samples what it should enumerate — **found in
  // the instrument built the same afternoon to prevent it.** `DEC-L-086`: enumerate rather than match, and
  // here the enumeration is also the shorter argument, because "all of them" needs no justification and
  // "these six" needs one that goes stale.
  //
  // ⚠ **The main guard never skipped the other seven.** `ContractDocuments()` walks every file on disk, so
  // coverage was always complete; what was incomplete was the ANTI-VACUITY CONTROL over that walk. A
  // guard can be comprehensive and still be unable to tell you it stopped working.
  //
  // **A new package with a route table must be added here.** That is deliberate friction: `FP-015` gets
  // its `api-contracts.md` when PR #171 merges, and this list is where it announces itself.
  [Theory]
  [InlineData("FP-001-identity-access")]
  [InlineData("FP-002-authentication-token-lifecycle")]
  [InlineData("FP-003-tenant-lifecycle")]
  [InlineData("FP-004-localization")]
  [InlineData("FP-005-company-legal-entity")]
  [InlineData("FP-006-hr-employee")]
  [InlineData("FP-007-hr-department")]
  [InlineData("FP-008-hr-position")]
  [InlineData("FP-009-hr-employee-import-export")]
  [InlineData("FP-011-gl-foundation")]
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

  // ---- AND THE LIST ABOVE IS EXHAUSTIVE, WHICH ONLY THIS CAN SAY.
  //
  // Naming thirteen fixes today's gap and creates tomorrow's: a fourteenth document added without a line
  // here is scanned by the main guard but never proven to yield rows. **This compares the named set to
  // what is on disk**, so the floor cannot fall behind the corpus it is the floor for.
  [Fact]
  public void The_named_documents_are_every_contract_document_on_disk()
  {
    var named = typeof(ApiContractRowGuardTests)
      .GetMethod(nameof(Every_contract_document_yields_rows))!
      .GetCustomAttributes(typeof(InlineDataAttribute), false)
      .Cast<InlineDataAttribute>()
      .Select(data => (string)data.GetData(null!).Single().Single()!)
      .OrderBy(package => package, StringComparer.Ordinal)
      .ToArray();

    var onDisk = ContractDocuments()
      .Select(document => Path.GetFileName(Path.GetDirectoryName(document))!)
      .OrderBy(package => package, StringComparer.Ordinal)
      .ToArray();

    Assert.NotEmpty(onDisk);
    Assert.Equal(onDisk, named);
  }

  // A fully-qualified permission: three or more dotted PascalCase segments, as the catalogue spells them.
  // `AC-LOC-0042`, `FR-DEP-0101` and `DEC-DEP-0023` do not match, which is why the form is this specific.
  private static readonly Regex QualifiedPermission = new(
    @"[A-Z][A-Za-z]+(?:\.[A-Z][A-Za-z]+){2,}", RegexOptions.Compiled);

  // ===============================================================================================
  // THE SECOND AXIS: A DOCUMENTED ROUTE MUST BE GATED ON THE PERMISSION ITS DOCUMENT NAMES (T-164).
  // ===============================================================================================
  //
  // ---- ⚠ THIS CATCHES THE DEFECT THE ROUTE INVENTORIES STRUCTURALLY CANNOT.
  //
  // T-099 found `/leave-requests/{id}/approve` gated on `ViewLeave` where `ApproveLeave` belonged — **a
  // route present, correctly named, and satisfying every surface comparison while handing approval to any
  // reader.** The inventories could not see it: **they compare the code to itself.**
  //
  // **FP-013 named `Attendance.Leave.Approve` for that route and always had.** The document was right
  // while the code was wrong, so this comparison fails on the day such a defect is written.
  [Fact]
  [Trait("Decision", "DEC-L-085")]
  public void Every_documented_permission_matches_the_route_it_gates()
  {
    var policies = LivePolicies();

    Assert.NotEmpty(policies);

    var disagreements = DocumentedPermissions()
      .Where(row => policies.TryGetValue($"{row.Method} {row.Path}", out var policy) &&
        !string.Equals(policy, row.Permission, StringComparison.Ordinal))
      .Select(row =>
        $"{row.Document}:{row.Line}  {row.Method} {row.Path}  document={row.Permission}  " +
        $"code={policies[$"{row.Method} {row.Path}"]}")
      .OrderBy(entry => entry, StringComparer.Ordinal)
      .ToArray();

    Assert.Empty(disagreements);
  }

  // ---- ⚠ AND WHAT IT CANNOT COVER IS COUNTED RATHER THAN SKIPPED.
  //
  // Not every row names a permission in a comparable form. **FP-004's column says `View`, not
  // `Platform.Localization.View`**, and resolving the short form needs a per-document prefix that is
  // written down nowhere. **Inferring it would put a guess inside a guard.**
  //
  // So the axis covers the fully-qualified rows and this states the shortfall as a number. **A silent
  // 65% looks identical to 100%**; a number that must be edited to fall does not.
  [Fact]
  public void The_permission_axis_states_the_rows_it_cannot_cover()
  {
    var all = ContractDocuments().SelectMany(RowsOf).ToArray();
    var qualified = DocumentedPermissions().Length;

    Assert.NotEmpty(all);

    // Measured 2026-08-29 (T-164). **A RISE in coverage is the good direction and must still be
    // deliberate**: a row gaining a fully-qualified permission is coverage arriving, and it should be
    // seen rather than absorbed.
    //
    // ⚠ **THE UNQUALIFIED COUNT IS ASSERTED, NOT MERELY LISTED, AND THAT IS T-162'S LESSON APPLIED HERE.**
    //
    // Listing them is a report nobody reads. **Asserting the number means a new short-form row reddens
    // this test and someone decides deliberately**; without it the covered fraction erodes while the
    // guard stays green — comprehensive over what it looks at, and unable to say its scope shrank.
    Assert.Equal(197, all.Length);
    Assert.Equal(124, qualified);
    Assert.Equal(73, all.Length - qualified);
  }

  private static (string Document, int Line, string Method, string Path, string Permission)[]
    DocumentedPermissions() => ContractDocuments()
      .SelectMany(RowsOf)
      .Select(row => (row.Document, row.Line, row.Method, row.Path,
        Permission: QualifiedPermission.Match(row.Text).Value))
      .Where(row => row.Permission.Length > 0)
      .ToArray();

  // The policy strings carry a plane prefix — `Permission:` or `PlatformPermission:` — and the documents
  // name the permission alone. The plane is `Every_route_is_mapped_on_the_plane_its_permission_is_scoped_to`'s
  // subject, not this one.
  private Dictionary<string, string> LivePolicies() => PlatformRouteInventory.Under(factory, "/api")
    .Select(endpoint => (
      Key: $"{PlatformRouteInventory.FirstMethodOf(endpoint)} {Normalize(endpoint.RoutePattern.RawText!)}",
      Policy: PlatformRouteInventory.AuthorizationOf(endpoint).Policy))
    .Where(entry => entry.Policy is not null)
    .GroupBy(entry => entry.Key, StringComparer.Ordinal)
    .ToDictionary(
      group => group.Key,
      group => group.First().Policy!.Split(':').Last(),
      StringComparer.Ordinal);

  private HashSet<string> LiveRoutes() => PlatformRouteInventory.Under(factory, "/api")
    .Select(endpoint =>
      $"{PlatformRouteInventory.FirstMethodOf(endpoint)} {Normalize(endpoint.RoutePattern.RawText!)}")
    .ToHashSet(StringComparer.Ordinal);

  private static IEnumerable<string> ContractDocuments() => Directory.EnumerateFiles(
    Path.Combine(FindRepositoryRoot(), "docs", "17-features"),
    "api-contracts.md",
    SearchOption.AllDirectories);

  private static (string Document, int Line, string Method, string Path, bool HasMarker, string Text)[] RowsOf(
    string document)
  {
    var lines = File.ReadAllLines(document);
    var rows = new List<(string, int, string, string, bool, string)>();

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
        Marker.IsMatch(line),
        line[match.Length..]));
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
