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
// **It names no member.** A reader cannot tell which element matched. Measured across
// `tests/Architecture.Tests` in T-082: **166 call sites in 38 files** (2026-09-07; 160 after T-088's
// rewrites landed). ⚠ AND IT CANNOT BE FIXED IN PLACE — there is no overload taking a message; the third
// parameter is an `IEqualityComparer`, which is compile error `CS1503`. `Assert.NotEmpty` is the same
// story with `CS1501`. The only remedy is rewriting the site as
// `Assert.True(!offenders.Any(), $"...")`, at a measured cost of about **+8 net lines per site**.
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
