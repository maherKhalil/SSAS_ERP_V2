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
