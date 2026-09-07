// ==================================================================================================
// ⚠⚠⚠ THE RULE. APPLY IT WHEN YOU WRITE THE ASSERTION; EVERYTHING BELOW IS ONLY THE EVIDENCE FOR IT.
// ==================================================================================================
//
//   ***IF THE COLLECTION IS ANYTHING BUT A LITERAL OR A SINGLE TYPE'S MEMBERS, USE `Assert.Empty` OR
//   `Assert.True` WITH THE OFFENDER INTERPOLATED — NEVER `Contains` / `DoesNotContain` WITH A PREDICATE.***
//
//   ***IF YOU CANNOT SEE THE WHOLE COLLECTION IN THE TEN LINES ABOVE THE CALL, IT IS TIER 1.***
//
// That second line is the operative half. It needs no census, no tiering script and no list of sites: it is
// answerable by looking up from the line you are typing.
//
// ---- ⚠⚠⚠ WHY A HABIT AND NOT A SWEEP — AND THIS IS MEASURED, NOT ARGUED.
//
// Three censuses were built for this, on 2026-09-07. Each finished, reported that tier 1 was retired, and
// was wrong:
//
//     instrument 1  `DoesNotContain` at the call site        ->  9 sites,  "zero remain"
//     instrument 2  + `Contains` (equally silent, proven)    -> 14 MORE,   "zero remain"
//     instrument 3  + walks bound to a local first           -> 10 MORE,   "zero remain"
//                                                    TOTAL:  ***33, AND ALL THREE ERRORS UNDER-REPORTED.***
//
// ***THAT IS NOT THREE MISTAKES. IT IS ONE PROPERTY: A CENSUS BUILT FROM THE SHAPES YOU HAVE ALREADY SEEN
// CANNOT COUNT THE SHAPE YOU HAVE NOT.*** ⚠ So the inference is NOT "write a fourth instrument". **The
// number was never the deliverable.** A fourth would find a fourth batch and produce a fourth figure
// indistinguishable from the first three — and the marginal remaining site is now in the tail, while the
// rule above prevents new ones at zero cost.
//
// ⚠⚠ AND THE ONE BLIND SPOT THAT IS KNOWN AND UNFIXED: an assertion over a local bound to a walk is
// invisible to any call-site matcher, because the walk is not at the call site. Resolving it needs the
// method body, which is a different tool. **`sharedApiTypes` was caught only because its NAME happened to
// contain a substring the matcher looked for.** *Stated so nobody reads the cleanup as complete.*
//
// ==================================================================================================
// WHEN A SILENT ASSERTION MESSAGE IS WORTH CONVERTING, AND WHEN IT IS NOT (T-082, T-087, T-089)
// ==================================================================================================
//
// This file holds a RULING and no code. It exists because the ruling has no other home: the choice it
// governs is made at ~160 call sites, and a rule that lives at one of them is a rule the other 159 cannot
// see. It carries the literal strings a future sweep would search for — `Assert.DoesNotContain`,
// `Filter matched in collection` — so that grepping for the defect finds the decision about it.
//
// ---- ⚠⚠⚠ THE MEASUREMENT, SO NOBODY RE-RUNS IT.
//
// `Assert.DoesNotContain(collection, predicate)` fails with exactly this and nothing else:
//
//     Assert.DoesNotContain() Failure: Filter matched in collection
//
// **It names no member.** A reader cannot tell which element matched.
//
// ==================================================================================================
// ⚠⚠⚠ THE NUMBERS BELOW WERE WRONG FOR SIX HOURS, AND THIS FILE CONTAINED THE PROOF (T-118)
// ==================================================================================================
//
// The census that produced them counted `Assert.DoesNotContain` ONLY. ***`Assert.Contains(collection,
// predicate)` IS EQUALLY SILENT — "Assert.Contains() Failure: Filter not matched in collection" — AND THAT
// CAPTURE IS THREE PARAGRAPHS ABOVE, IN THIS FILE, IN THE TABLE OF VERBATIM MESSAGES.***
// **The rule and the measurement of the rule sat on one page and disagreed.**
//
//     lambda-form sites, tests/Architecture.Tests    156  ->  ***251***   (both forms)
//     tier 1                                           0  ->  ***14***    (all now converted; 0 today)
//
// ⚠⚠ AND A SECOND BLIND SPOT, INDEPENDENT OF THE FIRST: the census classifies by the COLLECTION EXPRESSION
// AT THE CALL SITE, so a walk bound to a variable first is invisible —
//     var sharedApiTypes = typeof(ApiError).Assembly.GetTypes();   <- the walk
//     Assert.DoesNotContain(sharedApiTypes, type => …);            <- the call site says only a name
// **That one is a `DoesNotContain`, inside the census's own predicate, and was still missed.**
//
// ⚠⚠⚠ SO TREAT "14" AS A FLOOR WITH A NAMED CAUSE, NEVER A COUNT. Blind spot 2 is unfixed and will not be
// fixed by a third regex: **resolving a variable to its walk needs the method body, which is a different
// tool, and building one to rescue a census is how the census gets trusted again.** `sharedApiTypes` was
// caught only because its NAME happens to contain a substring the matcher looks for. ***A walk bound to a
// plainly-named local is still invisible to every instrument here.***
//
// ⚠ HOW IT WAS FOUND, because the method is the reusable part: a sweep of four other suites returned ZERO,
// and a zero got a positive control — the same instrument run against a file known to hold such sites.
// **It found THIRTEEN IN ONE FILE against a census reporting NINE FOR THE WHOLE SUITE.** *The control did
// not validate the zero; it falsified the instrument.* `give any zero a positive control`.
//
// ---- THE NUMBER, AND ENOUGH PROVENANCE TO RE-DERIVE IT RATHER THAN TRUST IT.
//
// **166 call sites in 38 files**, measured 2026-09-07 (156 after the T-088/T-089 rewrites). CORPUS:
// `tests/Architecture.Tests/*.cs`, line comments blanked — that suite has no block comments, checked
// rather than assumed. INSTRUMENT: locate `Assert.DoesNotContain(`, walk to the matching close paren,
// classify as the lambda overload if the argument text contains `=>`.
//
// ⚠⚠⚠ A SECOND INSTRUMENT DISAGREED AT **173**, AND THE DISAGREEMENT IS WHY 166 IS TRUSTWORTHY. Diffing
// the MEMBERS rather than comparing the counts named all seven extras, in three files. Every one is a
// STRING-OVERLOAD site misfiled as a lambda site, because a paren walk that does not skip string literals
// runs past the real close and swallows a `=>` belonging to a later call:
//
//     Assert.DoesNotContain("AddAsync(", source, StringComparison.Ordinal);
//                                    ^ unbalanced paren INSIDE a literal
//
// So a re-derivation must skip string literals. ⚠ Anyone re-counting with a naive scan will get 173 and
// conclude this file is stale; it is not, and the seven are listed above by mechanism so the difference
// is recognisable on sight.
//
// ⚠ AND IT CANNOT BE FIXED IN PLACE — there is no overload taking a message; the third
// parameter is an `IEqualityComparer`, which is compile error `CS1503`. `Assert.NotEmpty` is the same
// story with `CS1501`. The only remedy is rewriting the site as
// `Assert.True(!offenders.Any(), $"...")`, at a measured cost of about **+8 net lines per site**.
//
// ---- ⚠⚠⚠ THE RULE IS GENERAL. THE MEASUREMENTS ARE ONE SUITE'S. (T-115)
//
// Everything below was DERIVED in `tests/Architecture.Tests` and every figure here counts that suite only.
// ***THE RULE ITSELF CONTAINS NOTHING ABOUT WHICH PROJECT AN ASSERTION LIVES IN*** — it is about how many
// candidates a reader must examine by eye, and a `DoesNotContain` over every route literal in
// `SSAS.Platform.API` is tier 1 by that reasoning wherever it is written.
//
// ⚠ THIS FILE PREVIOUSLY STATED A CORPUS WHERE IT SHOULD HAVE STATED A SCOPE, which is the same defect as
// a denominator with no scope attached: `156` is one suite's count and reads like the repository's.
//
// MEASURED PER SUITE, 2026-09-07 — `Platform.Tests`: 88 files, **4 walk a corpus**, 3 carry a floor, and
// exactly TWO tier-1 silent sites existed (both on the platform route walk, both now converted). *The
// shape is largely ABSENT there — 84 of 88 files are behavioural — so a low count is a fact about the
// suite's nature, not evidence that it was audited harder.*
//
// ---- ⚠⚠⚠ AND THE PART THAT MATTERS: THE RAW COUNT IS NOT THE SIZE OF THE PROBLEM.
//
// The cost of a silent message is HOW MANY CANDIDATES A READER MUST EXAMINE BY EYE to find the offender.
// That is a property of the COLLECTION EXPRESSION and is statically visible:
//
//   TIER 1  the collection is a CORPUS WALK — files on disk, every type in an assembly, every entity in a
//           model. Hundreds of elements. The reader cannot reconstruct the offender at all.   **9 sites**
//   TIER 3  single-type reflection (`type.GetProperties()`), a literal array, a handful of members. The
//           whole collection is visible in the line above the assertion.                    **157 sites**
//
// ***ALL NINE TIER-1 SITES WERE CONVERTED (T-087, T-089). THE 157 ARE DELIBERATELY LEFT.***
//
// ---- THE EXEMPTION, AND ITS GROUNDS, BECAUSE AN EXEMPTION WITHOUT GROUNDS READS AS AN OVERSIGHT.
//
// A tier-3 site loses nothing by staying silent: `Assert.DoesNotContain(type.GetProperties(), p => …)`
// fails over a collection the reader is already looking at, and "which property matched" is answered by
// reading five lines. Converting all 157 would add ~1,250 lines to buy nothing, and a diff that large
// stops being reviewable — which is how a sweep starts lying about its own coverage.
//
// ⚠ SO: IF A LATER AUDIT REPORTS "166 GUARDS FAIL WITH A MESSAGE THAT NAMES NOTHING", THAT NUMBER IS
// TRUE AND IT IS NOT A DEFECT COUNT. The defect count was nine and it is zero. Re-tier before re-fixing.
//
// ==================================================================================================
// ⚠⚠ A SECOND TRUE NUMBER THAT IS NOT A DEFECT COUNT: COUNT-THEN-PLURAL AGREEMENT (T-105)
// ==================================================================================================
//
// `$"only {n} files were walked"` renders *"only 1 files were walked"*. Real, and found the way it had to
// be found: a plant drove a parse to ONE rather than to zero, and ***THE ZERO CASE READS CORRECTLY WHILE
// THE ONE CASE DOES NOT.*** Reading cannot catch this class — the message had been read twice, including
// while the file around it was being rewritten.
//
// **Measured 2026-09-07: ~75 sites in `tests/Architecture.Tests` interpolate a count immediately before a
// bare plural noun.** THIRTEEN WERE REPHRASED and the rest were LEFT DELIBERATELY.
//
// ---- THE GROUNDS, BECAUSE AN EXEMPTION WITHOUT THEM READS AS AN OVERSIGHT.
//
// The thirteen were rewritten because they were being rewritten anyway — tonight's floors, walks and
// controls. ⚠ **The remainder are not worth a sweep, and the comparison is the argument: an agreement slip
// costs a reader ALMOST NOTHING.** *"1 properties" still names the offender, still names the collapse,
// still says what to do.* Set that beside the defect this file exists for — a message naming NOTHING,
// where a reader must open the file and re-run to learn which member matched. **They are not the same size
// of problem, and a uniform sweep would treat them as one.**
//
// ⚠⚠ SO: A LATER AUDIT REPORTING "~75 MESSAGES DISAGREE IN NUMBER" HAS FOUND A TRUE NUMBER THAT IS NOT A
// DEFECT COUNT. Same shape as the 156 below, same instruction: **re-tier before re-fixing.**
//
// THE FORM, if you are writing a new one: put the noun BEFORE the number as a label — `file count: {n}`,
// `type count across the three assemblies: {n}`. It cannot disagree at any value.
//
// ---- ⚠ WHAT NOT TO CONVERT, AND ONE CORRECTION WORTH KEEPING.
//
// **`Assert.Empty(offenders)` PRINTS THE COLLECTION and therefore already names every offender.** It looks
// bare — no message argument — and is not. That was believed the other way round until it was run, and a
// sweep that "fixes" the 120 `Assert.Empty` sites would be undoing the good form.
//
// ⚠⚠ ITS ONE LIMIT, MEASURED TWICE ON DIFFERENT TESTS: the rendering truncates each element at **fifty
// characters**, with a `···` marker —
//
//     Collection: ["src/Modules/Attendance/SSAS.Attendance.API/Transie"···]
//
// So an element string must carry its IDENTITY FIRST. Two defects on the same subject whose text differs
// only after character fifty render as visually identical rows, and a reader assumes a duplicate and
// fixes one thing. ⚠ This applies ONLY to assertions that render a collection; an `Assert.True` message
// is a plain string and prints whole, so a converted site escapes the cut entirely.
//
// ---- ⚠⚠⚠ "IDENTITY FIRST" IS NOT "AS WRITTEN FIRST", AND THE DIFFERENCE IS TOTAL FOR TYPE NAMES (T-138).
//
// **The rule above says put the identity first. For a FULLY-QUALIFIED TYPE NAME the identity is at the END,
// and every element shares the prefix — so obeying the rule literally produces the worst possible message
// for one of the most common collections we assert on.** Measured on `IHostedService`, third independent
// observation of the fifty-character cut:
//
//     Actual: ["Microsoft.AspNetCore.DataProtection.Internal.DataP"···,
//              "Microsoft.AspNetCore.Hosting.GenericWebHostService",
//              "Microsoft.Extensions.Diagnostics.HealthChecks.Heal"···,
//              "SSAS.Host.API.Authentication.SigningKeyStartupVali"···,
//              "SSAS.Platform.Infrastructure.Localization.Localiza"···, ···]
//
// ***FIVE ROWS, AND THE ONLY ONE THAT SURVIVES INTACT IS THE ONE WHOSE NAME HAPPENS TO FIT.*** The two
// `SSAS.Platform.Infrastructure.*` entries are cut before their short names begin — **so a reader learns
// which NAMESPACE is involved, which they already knew, and nothing about WHICH SERVICE.** *A whole-list
// elision (`···` as the final element) then hides the rest entirely.*
//
// ⚠⚠ **A REFINEMENT, NOT A REVERSAL: front-load the DISTINGUISHING part, which for a type name is the SHORT
// name. Put the namespace AFTER it if it matters at all.** The general form of the original rule is *"the
// first fifty characters must be the part that differs BETWEEN ELEMENTS"* — for a file path that is usually
// the leading segments, for a type name it is never the leading segments, and the two cases look identical
// until the message is rendered.
//
// ⚠ **AND THE CHEAPEST FIX IS OFTEN NOT TO REORDER BUT TO LEAVE THE COLLECTION RENDERER.** `Assert.Equal`
// on two sets of type names is where the truncation is TOTAL, because the surviving fifty characters are
// exactly the part every element shares. `HostedServiceRegistrationTests` therefore asserts with
// `Assert.True` and interpolates BOTH set differences by hand — full names, uncut — rather than reordering
// strings to survive a renderer.
//
// ==================================================================================================
// ⚠⚠⚠ BEFORE ASSERTING AN OUTCOME, ASK WHAT ELSE PRODUCES THAT OUTCOME (T-158).
// ==================================================================================================
//
// ***IF MORE THAN ONE CAUSE REACHES THE OUTCOME YOU ARE ASSERTING — AND ESPECIALLY IF THOSE CAUSES BELONG TO
// DIFFERENT CRITERIA — THE TYPE IS NOT THE ASSERTION. THE MESSAGE IS.***
//
// **The measured instance:** `AuthenticationTransportServices.Validate` throws `InvalidOperationException`
// from FIVE sites — duplicate origins, an unrecognised `ProxyMode`, TrustedProxy without proxies or
// networks, the rate-limit clause, and the Data-Protection clause. `Production_transport_without_shared_…`
// asserted the TYPE and was cited on **two** authentication criteria. ⚠ **Planted by making a different
// clause fire, it produced `Actual: "ProxyMode must be Direct or TrustedProxy."` against an expected
// rate-limit message — so *a typo in the origin list satisfied a test citing two criteria*.** It is now two
// tests, one arrangement per clause, each asserting its own message.
//
// ---- ⚠⚠ WHAT THIS RULE DOES **NOT** SAY.
//
// **MULTI-CITATION IS NOT A DEFECT.** A test may cite five criteria and be impeccable: 73 of the 586
// criterion-carrying tests cite more than one, and **the two with the worst citations-per-assertion ratio
// are among the best-documented files in the suite** — `CutoverManifestArchitectureTests` states *"this
// criterion is discharged one clause by a fixture, one clause by nobody"*, and `EmployeeArchitectureTests`
// states *"⚠ PARTIAL for `0002`"*. ***A SWEEP RANKED BY RATIO PUTS THE MOST HONEST FILES AT THE TOP OF THE
// SUSPECT LIST.*** *Do not split a multi-cited test because it is multi-cited; split one whose assertion
// cannot tell its criteria apart.*
//
// ⚠ Equally: one assertion covering several criteria is fine when the outcome has ONE cause.
// `Expired_jwt_is_rejected_by_the_registered_authentication_handler` cites two criteria on a single
// behaviour — an expired token being refused — which genuinely satisfies a clause of each.
//
// ---- ⚠⚠⚠ METHOD NOTE, BECAUSE THE DETECTOR FOR THIS SHAPE LIES BY DEFAULT.
//
// A matcher for "type-only refusal" **must match `Assert.ThrowsAsync` and `Assert.ThrowsAnyAsync`, not only
// `Assert.Throws<T>`.** *A pattern requiring the generic argument reported **ZERO** multi-cited tests of this
// shape — a clean answer on a question whose one known instance had been fixed by hand minutes earlier.*
// **The same detector with the citation count relaxed to one returned FOURTEEN, including a two-criterion
// row the zero had just denied existed. The true answer was 2.**
//
// ⚠ **The control that worked was the detector itself with ONE CONSTRAINT LOOSENED — not a hunt for a known
// case.** *That tests the matcher rather than the corpus, and it is cheaper than either.*
