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
  // **A new package with a route table must be added here.** That is deliberate friction, and it worked:
  // FP-015's `api-contracts.md` landed on disk without a line here, and
  // `The_named_documents_are_every_contract_document_on_disk` turned the integration branch red until it
  // was reconciled. **The list announcing a package is the point, not an inconvenience.**
  //
  // ⚠ **BUT FP-015 COULD NOT SIMPLY BE ADDED, AND THAT IS THE INTERESTING HALF.** This theory asserts
  // every named document YIELDS ROWS. FP-015's declares **zero routes on purpose** — its closing section
  // says *"No URLs, no route names, no request or response shapes… the contract asserted here is a
  // NEGATIVE one — what a self-service route must not carry"*. FP-014's document has 27 route rows;
  // FP-015's has none.
  //
  // So naming it here would have moved the failure rather than fixed it. It is named in
  // `RoutelessContractDocuments` below instead, which keeps it inside the on-disk comparison — the
  // property that caught this — while exempting it from a row expectation it was written not to meet.
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

  // ⚠ THE ONE PACKAGE WHOSE CONTRACT IS DELIBERATELY ROUTELESS.
  //
  // An exemption from the ROW expectation only. It stays in the on-disk comparison below, so it cannot
  // drop out of the corpus unnoticed — which is the property that caught it arriving in the first place.
  //
  // **Its own falsifier is `A_routeless_document_that_grows_routes_loses_its_exemption`**: the day FP-015
  // gains a route table it must join the theory above, and this list must not quietly outlive the reason
  // for it. An exemption that cannot expire is a hole.
  private static readonly string[] RoutelessContractDocuments = ["FP-015-self-service"];

  // The exemption expires by itself. A routeless document that starts declaring routes is a document that
  // should be inside the row guard, and nothing else would notice the change.
  [Fact]
  public void A_routeless_document_that_grows_routes_loses_its_exemption()
  {
    foreach (var package in RoutelessContractDocuments)
    {
      var document = Path.Combine(
        FindRepositoryRoot(), "docs", "17-features", package, "api-contracts.md");

      Assert.True(File.Exists(document),
        $"{package} is exempted from the row expectation but has no api-contracts.md at all.");

      Assert.True(RowsOf(document).Length == 0,
        $"{package} is listed as routeless and now yields rows. Move it into " +
        $"{nameof(Every_contract_document_yields_rows)} and drop it from " +
        $"{nameof(RoutelessContractDocuments)}.");
    }

    Assert.NotEmpty(RoutelessContractDocuments);
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
      .Concat(RoutelessContractDocuments)
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
  // Not every row names a permission in a comparable form, and the shortfall is stated as a number rather
  // than left to erode quietly. **A silent 65% looks identical to 100%**; a number that must be edited to
  // fall does not.
  //
  // ---- ⚠ THIS COMMENT USED TO ASSERT AN ABSENCE THAT NOBODY HAD CHECKED, AND THE CHECK CHANGED IT (T-192).
  //
  // It said resolving a short form "needs a per-document prefix that is WRITTEN DOWN NOWHERE", and that
  // inferring one would put a guess inside a guard. The second half was right and the first was false:
  // `LivePolicies()`, in this same file, already reads each route's actual policy. For any row whose method
  // and path match a live route, **the fully-qualified permission was available from the running
  // application all along** — not inferred, read.
  //
  // ---- THE 73 WERE ALSO NOT WHAT THE COMMENT DESCRIBED, WHICH MATTERED MORE THAN THE ABSENCE.
  //
  // Measured by running the extraction rather than by reading the documents: 37 of the 73 matched a live
  // route and 36 did not, and by KIND they were
  //
  //   30  a status marker where a permission would go — `[BUILT]`, `[NOT ROUTED - handler: X]`
  //   22  a bare `METHOD /path` line carrying no columns at all
  //   14  a prose note about response shape
  //    7  an actual short-form permission cell
  //
  // **So only 7 of 73 were the case this comment described**, all in FP-004, all matching a live route, and
  // every one already agreeing with its policy's last segment. A suffix-only guard would therefore have
  // locked in seven currently-true facts, caught nothing known, and still been blind to a wrong PLANE —
  // which is the half of a permission that actually decides who gets in.
  //
  // **So the seven were QUALIFIED IN FP-004 INSTEAD**, which moves them onto the axis above and covers the
  // plane too. The count below fell from 73 to 66 for that reason and no other.
  //
  // The 66 that remain are not short forms and no prefix would help them. The 30 markers are the honest
  // half: those rows document capability that has no route, `FP-001` holding 17 that name their handler
  // outright. That is a product gap, not a permission one.
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
    Assert.Equal(132, qualified);
    Assert.Equal(65, all.Length - qualified);

    // ---- ⚠ AND THE 66 ARE DECOMPOSED, BECAUSE A RESIDUAL HAS NO OPINION ABOUT ITS CONTENTS (T-196).
    //
    // `all.Length - qualified` cannot be wrong about its SIZE and says nothing whatever about what is in
    // it. Two windows spent a day reasoning about "73 unqualified permission rows" on the strength of the
    // comment above, and the set turned out to be four different things — **the count was exactly right
    // and its label was completely wrong.**
    //
    // **Being asserted as a number is what let it go unexamined**: it was the rigorous-looking part of the
    // file. An anti-vacuity control protects the SIZE of a set and nothing else, so a well-defended count
    // is somewhere a wrong label lives undisturbed.
    //
    // Four numbers instead of one. If the bare rows grew by 18 while the markers fell by 18, the total
    // above would not move at all and these would.
    var unqualified = all.Where(row => QualifiedPermission.Match(row.Text).Value.Length == 0).ToArray();

    Assert.Equal(65, unqualified.Length);

    // A status marker standing where a permission belongs — `[BUILT]`, `[NOT ROUTED - handler: X]`. These
    // are the honest half: they document capability that has no route, and FP-001 holds 17 that name the
    // handler outright.
    Assert.Equal(29, unqualified.Count(row => KindOf(row.Text) == RowKind.Marker));

    // A bare `METHOD /path` with no columns at all. These document NOTHING.
    Assert.Equal(22, unqualified.Count(row => KindOf(row.Text) == RowKind.NoCell));

    // Prose about response shape sitting in the permission column. A shape defect, not a permission gap.
    Assert.Equal(14, unqualified.Count(row => KindOf(row.Text) == RowKind.Prose));

    // ⚠ **ZERO, AND THAT IS THE ONE WORTH ASSERTING.** The short forms were the only kind this axis could
    // ever have covered, and T-192 qualified all seven in FP-004 rather than teaching the guard to accept
    // them. A new one reddens here immediately instead of being absorbed into a residual.
    Assert.Equal(0, unqualified.Count(row => KindOf(row.Text) == RowKind.ShortForm));
  }

  // ================================================================================================
  // THE THIRD AXIS: A DOCUMENTED ROUTE THE APPLICATION DOES NOT SERVE (T-202).
  // ================================================================================================
  //
  // ---- ⚠ THIS NUMBER LIVED IN A REPORT AND MOVED TWICE BEFORE IT WAS COMMITTED.
  //
  // The completeness audit reported 67 rows documenting a route that does not exist, and the owner was told
  // that was the capability gap. It is not: **25 of the 67 say so in their own notes** and the count did not
  // read them.
  //
  // A number that only exists in a report cannot redden, so it drifts and is re-derived by hand each time.
  // This is the same decomposition the permission axis carries, for the same reason — **a residual has no
  // opinion about its contents**, and this residual turned out to be three different things.
  [Fact]
  public void The_documented_routes_that_do_not_exist_are_decomposed_rather_than_counted()
  {
    var live = LiveRoutes();
    var all = ContractDocuments().SelectMany(RowsOf).ToArray();

    Assert.NotEmpty(live);
    Assert.Equal(197, all.Length);

    var absent = all.Where(row => !live.Contains($"{row.Method} {row.Path}")).ToArray();

    Assert.Equal(131, all.Length - absent.Length);
    Assert.Equal(66, absent.Length);

    // ⚠ CAPABILITY THAT EXISTS UNDER ANOTHER PATH, AND THE ROW SAYS SO. `[BUILT as ...]` and
    // `[SERVED BY ...]` are used consistently across these documents and explained in their own legend.
    // `/departments/{id}/parent` shipped as `/move` and `/move-to-root` under `DEC-DEP-0023`;
    // `/users/{id}/deactivate` as `/tenant-users/{id}/deactivation`. **These are not gaps and must not be
    // rewritten to match the route** — the row records what was specified, what was built, and the ruling
    // that explains the difference, and only the first and third would survive a rewrite.
    Assert.Equal(10, absent.Count(row =>
      row.Text.Contains("BUILT as", StringComparison.Ordinal) ||
      row.Text.Contains("SERVED BY", StringComparison.Ordinal)));

    // Decided and recorded already: fourteen tenant-lifecycle rows deferred by `AC-TEN-0020`, and one
    // delete route superseded by `DEC-TEN-0007` because no delete exists.
    Assert.Equal(15, absent.Count(row =>
      row.Text.Contains("DEFERRED", StringComparison.Ordinal) ||
      row.Text.Contains("SUPERSEDED", StringComparison.Ordinal)));

    // ---- ⚠ WHAT IS LEFT IS THE HONEST CAPABILITY GAP, AND IT IS ALMOST ENTIRELY THE OWNER'S.
    //
    // 24 are the commercial plane (owner decision 11), 16 the administration transport (decision 2), 1 the
    // attendance bulk import (decision 5). **The permissions for the 16 are already catalogued and their
    // handlers already built** — 28 platform permissions catalogued, 12 required by a live route, 16 by
    // none — so that decision is about cost, not design.
    var undecided = absent.Where(row =>
      !row.Text.Contains("BUILT as", StringComparison.Ordinal) &&
      !row.Text.Contains("SERVED BY", StringComparison.Ordinal) &&
      !row.Text.Contains("DEFERRED", StringComparison.Ordinal) &&
      !row.Text.Contains("SUPERSEDED", StringComparison.Ordinal)).ToArray();

    Assert.Equal(41, undecided.Length);
  }

  // ================================================================================================
  // THE FOURTH AXIS: A LIVE ROUTE NO TEST EVER CALLS (T-209).
  // ================================================================================================
  //
  // ---- ⚠ THE NUMBER THAT FOUND ATTENDANCE, COMMITTED SO IT STOPS DRIFTING.
  //
  // A completeness audit measured 63 uncalled routes, then 42 after the instrument was corrected, then 8
  // after four slices of endpoint tests. **It moved three times in a day while living only in reports**,
  // and a number nobody re-runs is a number nobody can trust.
  //
  // It is what found the largest gap of the day: `AttendanceApiTestHost` mapped 25 routes — the surface as at
  // 2026-08-29; it is 27 as at 2026-08-30 — over a container
  // that had never heard of their handlers. **Nothing else could have** — an unregistered handler is not a
  // dependency of anything registered, so service-provider validation had nothing to say, and the routes
  // had existed too long for any change to trigger it. Only issuing a request finds that.
  //
  // ---- ⚠ WHY EIGHT AND NOT ZERO, AND WHY THE EIGHT ARE ASSERTED RATHER THAN LISTED.
  //
  // Six of the eight are the UNDO half of a pair that is tested — reject beside approve, deactivate beside
  // activate, holidays/remove beside holidays. They share a handler shape, a permission and a mapper arm
  // with a route that has tests, so the marginal evidence is small.
  //
  // That is a judgement, not a proof, and the counter-argument is real: today's prior says the second time
  // you do something is the first time nobody has tried. What settles it is that the specific failure —
  // an unmapped code answering 500 — is now closed by `PropagatedErrorMappingTests`, and the permission
  // pairings examined today were correct three times out of three.
  //
  // **A remainder we chose to stop at is a set WE control and whose membership is the point**, so it is an
  // exact count: a ninth uncalled route reddens rather than sitting in a report nobody re-runs.
  //
  // ---- THE PROXY, AND ITS ONE CORRECTION.
  //
  // "Addressed" means a test source contains a string that could address the route. Route INVENTORIES are
  // excluded deliberately: they enumerate routes without calling them, and counting them as coverage is how
  // a route list becomes mistaken for a test suite.
  //
  // **String constants are inlined first.** `DepartmentEndpointTests` declares `const string Route` and
  // builds `$"{Route}/{id}/move"`, so the path never appears contiguously — the first version of this scan
  // reported 63 and 21 of them were tested. It reads the corpus the way the corpus is written.
  [Fact]
  public void Every_live_route_is_addressed_by_some_test_that_is_not_an_inventory()
  {
    var live = LiveRoutes();

    // ---- ⚠ TWO NUMBERS, TWO KINDS, AND GETTING EITHER WRONG REPORTS HEALTH WHILE MEASURING NOTHING.
    //
    // The DENOMINATOR is a corpus that grows with ordinary work, so it takes a FLOOR — and that floor is
    // the anti-vacuity control: **if route discovery breaks and finds nothing, zero uncalled routes is
    // green and perfect.** Computing it live also retires the stale-denominator problem rather than
    // managing it.
    //
    // The EXCEPTIONS are a set we control and whose membership is the point, so they take an EXACT count.
    Assert.True(
      live.Count >= 140,
      $"only {live.Count} live routes discovered, below the floor of 140: route discovery has degraded, " +
      "and every count below it is meaningless rather than reassuring");

    var corpus = ExercisingTestSources();
    Assert.True(corpus.Length > 200_000, "the test corpus looks truncated; this scan cannot be trusted");

    // ⚠ `Regex.Escape` ESCAPES `{` AND NOT `}`, so an escaped `{}` placeholder becomes `\{}` and neither
    // `{}` nor `\{\}` matches it. Replacing after escaping silently matched nothing and reported 8 uncalled
    // routes as 93 — an instrument claiming five-sixths of the product was untested.
    //
    // A SENTINEL SUBSTITUTED BEFORE ESCAPING cannot be wrong about what the escaper did, which is the point:
    // the previous two attempts were both reasonable guesses about someone else's escaping rules.
    var uncalled = live
      .Where(route => !Regex.IsMatch(corpus, SegmentPattern(route.Split(' ', 2)[1])))
      .OrderBy(route => route, StringComparer.Ordinal)
      .ToArray();

    // ⚠ 8 → 5 → 0 (T-236, then T-240), AND THE GUARD IS WHAT MADE EACH STEP DELIBERATE.
    //
    // T-236 covered three Attendance routes. T-240 covered the last three genuine gaps — the working-days
    // query, GL account deactivation, and employee activation — and found that **two of the remaining five
    // were never gaps at all**; see `CompositionBlindSpots` below.
    var exempt = uncalled.Where(route => CompositionBlindSpots.Any(entry => entry.Route == route)).ToArray();
    var genuine = uncalled.Except(exempt.Select(route => route)).ToArray();

    Assert.True(
      exempt.Length == CompositionBlindSpots.Length,
      $"{CompositionBlindSpots.Length - exempt.Length} exemption(s) no longer describe an unseen route. " +
      "The exemption exists ONLY because the matcher cannot see a composed path -- if the route is now " +
      "visible the entry is stale and must go, or it will outlive the blind spot it documents:" +
      Environment.NewLine + string.Join(Environment.NewLine,
        CompositionBlindSpots.Select(entry => entry.Route).Except(exempt.Select(route => route))));

    Assert.True(
      genuine.Length == 0,
      $"{genuine.Length} live routes are addressed by no test, expected 0. A route arrived that nothing " +
      "calls -- the shape that left 25 attendance routes mapped over a container with no handlers:" +
      Environment.NewLine + string.Join(Environment.NewLine, genuine));
  }

  // ================================================================================================
  // ⚠ TWO ROUTES THIS SCAN CANNOT SEE, AND THEY ARE FULLY TESTED (T-240).
  // ================================================================================================
  //
  // **A STATIC MATCHER IS BLIND TO ANYTHING COMPOSED.** This scan reads source text for a literal path;
  // `CompaniesMutationEndpointTests` builds `$"/api/platform/companies/{id}/{action}"` where `action`
  // arrives from `[InlineData]`. The request is real, the route is exercised twice over — and the final
  // segment never appears in the source, so the matcher reports it as untested.
  //
  // **THE MECHANISM WAS ESTABLISHED BY A CONTROLLED COMPARISON RATHER THAN ARGUED.** In that one file, with
  // the same helper and the same request path, `/activate` is spelled out literally and counts as called
  // while `/archive` and `/deactivate` go through `{action}` and do not. **One variable: whether the
  // segment was written out.**
  //
  // The converse was checked rather than assumed — a route could count as CALLED because a comment merely
  // mentions the path, since the corpus is raw source. Replicating this scan and rerunning it with comment
  // lines stripped returned the same count. **The instrument over-reports and does not under-report**, and
  // knowing WHICH direction is what makes the number usable at all.
  //
  // ---- WHY AN EXEMPTION AND NOT A FIX.
  //
  // Teaching the corpus to expand `[InlineData]` would fix the class rather than these two instances — but
  // the class has two members, and a substituter guesses at someone else's parameterisation. **The last two
  // guesses about escaping in this very method returned 93 where the answer was 8.** An entry that has to
  // name a real method and a segment found inside it cannot be quietly wrong. **Revisit at four or five
  // entries**, which would mean the class is real and worth teaching the instrument about.
  //
  // ⚠ **AND NAMING THE METHOD IS NOT ENOUGH ON ITS OWN.** A rename would be caught, but ROT would not: if
  // the method survives while its `[InlineData]` row is deleted, the exemption goes on asserting coverage
  // that no longer happens, with the authority of a named method. So the entry carries the SEGMENT too, and
  // the guard below reads that method's source and requires the segment to be in it.
  // Newline-agnostic on purpose: these sources are CRLF and a literal newline escape silently matches
  // nothing but a file boundary.
  private static readonly Regex BlankLine = new(
    @"\r?\n[ \t]*\r?\n", RegexOptions.Compiled);

  private static readonly Regex NextMember = new(
    @"\r?\n  (?:\[|public|private|internal)", RegexOptions.Compiled);

  private static readonly (string Route, string TestMethod, string Segment)[] CompositionBlindSpots =
  [
    ("POST /api/platform/companies/{}/archive", "Lifecycle_happy_path_returns_200", "archive"),
    ("POST /api/platform/companies/{}/deactivate", "Lifecycle_happy_path_returns_200", "deactivate")
  ];

  // The exemption's own falsifier: each entry must name a method that exists AND that still mentions the
  // segment. Without this the list is a place to put inconvenient routes.
  [Fact]
  public void Every_composition_exemption_names_a_live_test_that_still_calls_its_route()
  {
    var corpus = ExercisingTestSources();

    foreach (var (route, method, segment) in CompositionBlindSpots)
    {
      var declaration = Regex.Match(corpus, @"public\s+async\s+Task\s+" + Regex.Escape(method) + @"\s*\(");
      Assert.True(declaration.Success,
        $"{route} is exempted by `{method}`, which no longer exists — the exemption is asserting coverage " +
        "by a test that has been renamed or deleted.");

      // ⚠ TWO WAYS TO GET THIS REGION WRONG, AND BOTH OF THEM PASS QUIETLY.
      //
      // FIRST: for a `[Theory]` the segment lives in the `[InlineData]` rows, which sit BEFORE the
      // method. A region beginning at the declaration excludes exactly the thing it looks for.
      //
      // SECOND, AND IT IS THE ONE THAT COST A FALSE GREEN: **the sources are CRLF, so a literal
      // "\n\n" never matches a blank line at all.** It matches only where the corpus builder appends
      // its own separator after a file ending in CRLF -- a FILE BOUNDARY. The region then began in some
      // other test file and ran on for thousands of characters, and `Contains` passed on a segment that
      // belonged to somebody else's source. It survived a deliberate plant looking perfectly healthy.
      //
      // So both bounds are newline-agnostic patterns rather than literals.
      var before = BlankLine.Matches(corpus[..declaration.Index]);
      var from = before.Count > 0 ? before[^1].Index : declaration.Index;

      // Bounded ahead by the next member rather than by brace counting: a string literal carrying a
      // brace would defeat the counter, and several of these bodies contain JSON.
      var rest = corpus[from..];
      var afterDeclaration = declaration.Index - from + declaration.Length;
      var next = NextMember.Match(rest[afterDeclaration..]);
      var region = next.Success ? rest[..(afterDeclaration + next.Index)] : rest;

      // ⚠ THE ANTI-VACUITY CONTROL FOR THIS TEST'S OWN INSTRUMENT. A region that ran across a file
      // boundary is how the false green happened, and it is invisible from the assertion below -- a
      // wrong region and a right one both just say `Contains` passed. One method with its attributes
      // does not reach 4,000 characters.
      Assert.True(region.Length < 4_000,
        $"the extracted region for `{method}` is {region.Length} characters, which means it has run past " +
        "the method and the segment below may be found in unrelated source.");

      Assert.Contains(segment, region, StringComparison.Ordinal);
    }

    Assert.NotEmpty(CompositionBlindSpots);
  }

  // `/api/gl/accounts/{}/balance` -> a regex matching any single segment where the placeholder is, with
  // every other character literal. The sentinel survives escaping because it contains no metacharacters.
  private static string SegmentPattern(string path)
  {
    const string Sentinel = "SEGMENTPLACEHOLDER";
    return Regex.Escape(path.Replace("{}", Sentinel, StringComparison.Ordinal))
      .Replace(Sentinel, "[^/\"]+", StringComparison.Ordinal);
  }

  // Every test source except the inventories and this file.
  private static string ExercisingTestSources()
  {
    var root = Path.Combine(FindRepositoryRoot(), "tests");
    var builder = new System.Text.StringBuilder();

    foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
    {
      var name = Path.GetFileName(path);
      if (path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        name.Contains("Inventory", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("RowGuard", StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      var text = File.ReadAllText(path);
      foreach (Match constant in Regex.Matches(text, ConstantPattern))
      {
        text = text.Replace(
          "{" + constant.Groups[1].Value + "}", constant.Groups[2].Value, StringComparison.Ordinal);
      }

      builder.Append(text).Append('\n');
    }

    return builder.ToString();
  }

  // `const string X = "value";` -- inlined into `{X}` interpolations before the corpus is searched.
  private const string ConstantPattern =
    @"const\s+string\s+(\w+)\s*=\s*""([^""]*)""";

  private enum RowKind
  {
    NoCell,
    Marker,
    Prose,
    ShortForm,
  }

  // Three mechanical tests on the row text, in order, with no judgement call in any of them — which is why
  // the taxonomy above can be asserted without rotting.
  private static RowKind KindOf(string text)
  {
    var trimmed = text.Trim();
    if (trimmed.Length == 0)
    {
      return RowKind.NoCell;
    }

    if (trimmed[0] == '[')
    {
      return RowKind.Marker;
    }

    var cell = trimmed.Split('|')[0].Trim();
    return cell.Length > 0 && cell.All(char.IsLetter) ? RowKind.ShortForm : RowKind.Prose;
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
