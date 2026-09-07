namespace SSAS.Architecture.Tests;

// ==================================================================================================
// HOW TO WRITE A SWEEP OVER THIS REPOSITORY, AND HOW SWEEPS LIE. (T-184)
// ==================================================================================================
//
// **This file holds a METHOD, not a rule about tests. It has no code and asserts nothing.** Its audience is
// whoever is about to write a grep, a regex sweep or a throwaway probe to answer a question about the corpus
// — which is a thing that happened repeatedly across one session, and which produced *four* wrong answers
// before it produced right ones.
//
// ---- ⚠ WHY IT IS A SEPARATE FILE, RULED RATHER THAN DRIFTED INTO.
//
// These notes accreted inside `AssertionMessageChoice`, whose banner declares TWO rules. **A fourth note
// arriving was the agreed trigger to move them, and the move was decided rather than done as a side effect
// of adding the fourth.** *The test applied was the same one that KEPT that file's two rules together: those
// two interact — rule 2's remedy is "assert the message", which is then governed by every line of rule 1.*
// ***THESE DO NOT INTERACT WITH EITHER: NOBODY FOLLOWING AN INSTRUMENT NOTE IS THEN GOVERNED BY THE TIER
// RULE, AND FORCING AN OUTPUT ENCODING HAS NOTHING TO DO WITH HOW A FAILURE MESSAGE READS.***
//
// ==================================================================================================
// ⚠⚠⚠ 1. AN INSTRUMENT'S FAILURE MODE IS A SHORTER ANSWER, NEVER AN ERROR.
// ==================================================================================================
//
// Three sweeps failed in one session and ***NOT ONE RAISED AN ERROR — each returned a plausible result that
// was shorter than the truth:***
//
//   `grep -P` aborted on this locale       → **ZERO** for five known-dead symbols: a perfect CONFIRMATION
//   the shell ate nested double quotes     → **ZERO** for a symbol that was present: a perfect REFUTATION
//   a probe died mid-print on an emphasis  → **ONE** hit then nothing: a PARTIAL result reading as complete
//   character
//
// ***THE THIRD IS THE ONE TO REMEMBER: THE CRASH WAS CAUSED BY THE WARNING SIGN THIS REPOSITORY USES FOR
// EMPHASIS.*** Printing our own house style through the console's default code page killed the instrument —
// `UnicodeEncodeError: 'charmap' codec can't encode character` — **after it had printed part of its answer.**
// *So: force `PYTHONIOENCODING=utf-8` on any probe that echoes text from this tree.*
//
// ⚠⚠ **THE GENERAL FORM OUTLIVES THE ENCODING: *A ZERO IS SUSPECT, AND SO IS A SMALL NUMBER — ASK WHETHER
// THE INSTRUMENT FINISHED.*** A positive control answers "does it find things" and NOT "did it complete";
// the second needs the run's exit status, or a marker printed after the last row.
//
// ==================================================================================================
// ⚠⚠ 2. THE CONTROL THAT WORKS IS THE DETECTOR WITH ONE CONSTRAINT LOOSENED.
// ==================================================================================================
//
// A matcher for "type-only refusal" **must match `Assert.ThrowsAsync` and `Assert.ThrowsAnyAsync`, not only
// `Assert.Throws<T>`.** *A pattern requiring the generic argument reported **ZERO** — a clean answer on a
// question whose one known instance had been fixed by hand minutes earlier.* **The same detector with the
// citation count relaxed from "more than one" to "at least one" returned FOURTEEN, including a row the zero
// had just denied existed. The true answer was 2.**
//
// ⚠ ***THAT CONTROL TESTS THE MATCHER RATHER THAN THE CORPUS, AND IT IS CHEAPER THAN HUNTING FOR A KNOWN
// CASE.*** *Loosen one constraint and see whether the instrument speaks at all.*
//
// ==================================================================================================
// ⚠⚠ 3. A CENSUS THAT COUNTS ONE FORM OF A MECHANISM UNDER-REPORTS IT.
// ==================================================================================================
//
// A census of silent assertion messages counted `Assert.DoesNotContain` only. **`Assert.Contains(collection,
// predicate)` is equally silent — *"Filter not matched in collection"* — and the capture proving it sat three
// paragraphs above the census, in the same file.** ***THE RULE AND THE MEASUREMENT OF THE RULE WERE ON ONE
// PAGE AND DISAGREED.*** *The numbers were wrong for six hours.*
//
// ⚠ **Enumerate the MECHANISM before counting its instances** — the correction was a wider search, not a
// re-read, because re-reading returns the same answer. *The provenance of that file's own figures stays in
// `AssertionMessageChoice`, where the numbers are; the method lesson is here.*
//
// ==================================================================================================
// ⚠⚠⚠ 4. A WINDOW IS A CLAIM ABOUT THE WINDOW.
// ==================================================================================================
//
// **Every scoped matcher in that session eventually reported the scope rather than the corpus:**
//
//   a twelve-line look-ahead      → three tripwires filed as "no plant evidence"; **all three were
//                                   plant-backed, one at CLASS level where no per-attribute window reaches**
//   a line-scoped delegation scan → **198** "unnamed" delegations; over comment BLOCKS it was 108, and the
//                                   corpus simply writes in paragraphs
//   a vocabulary matcher          → floors filed as BARE whose collapse was named in unlisted words, in a
//                                   DIFFERENT METHOD from the floor
//
// ***SAY WHAT THE WINDOW WAS BEFORE REPORTING WHAT IT FOUND, AND WIDEN IT ONCE BEFORE BELIEVING A COUNT.***
//
// ---- ⚠ AND THE COUNTERPART FOR CITATIONS, MEASURED.
//
// **A SYMBOL name is verifiable at scale: one grep resolved 123 delegation targets and found the two stale
// ones. A `file:line` is not: of 100 line citations, SIX were mechanically checkable, and every "failure"
// the instrument reported was its own.** ***PREFER THE SYMBOL — not because lines rot faster, but because
// nothing can tell you when they have.***
//
// ---- SEE ALSO
//
// `AssertionMessageChoice` — the two rules about how an assertion COMMUNICATES and whether it DISCRIMINATES.
// *Cross-referenced by name in both directions and by line in neither, for the reason in note 4.*
internal static class ProbeAndInstrumentMethod
{
}
