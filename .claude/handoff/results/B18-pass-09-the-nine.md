# B18 pass 09 — the nine re-checked, and `AC-EMP-0035` corrected in the OTHER direction

**TASK gate green, 0 warnings. Control: 147 cited, zero dangling. ⚠ FP-006 → 39 of 47.**

## ⚠⚠ FIRST: MY PASS-08 CITATION WAS ALSO WRONG, AND THE RULING CAUGHT IT

`AC-EMP-0035` is **PARTLY PINNED**, not pinned. **I verified the objection against the body and it holds:**

**`Point_in_time_attribution_differs_from_the_current_branch` calls `.OrderByDescending(...)` ITSELF.**
⚠ **The sort is the test's, not the product's — so it passes unchanged if the API returns the history in
arbitrary order**, and the criterion's clause 1 is *"the history is **returned in effective order**"*.

**So I recorded the criterion unresolved twice, then pinned, and both were wrong in opposite directions.**
⚠ **The second error is the more dangerous: a citation makes a thing look settled.**

**Corrected in place, with all four clauses enumerated in the comment:**

| clause | state |
|---|---|
| 1 — returned in effective order | ⚠ **UNASSERTED** |
| 2 — point-in-time selection | ✅ `Point_in_time_attribution_differs_from_the_current_branch` |
| 3 — never updated or deleted | ✅ `A_history_row_cannot_be_updated_or_deleted` |
| 4 — current-state vs point-in-time reads | ✅ the same test asserts both sides |

⚠ **And clause 3 IS pinned, which the ruling had not established** — it said *"not asserted there either"*,
true of that test. **The same one-test-to-criterion inference, in the other window.** Cited on its own test.

## The nine, re-checked by mechanism

| criterion | outcome |
|---|---|
| **`AC-EMP-0009`** | ⚠ **RECOVERED** — `A_national_id_is_unique_within_a_company_but_may_be_absent_many_times` asserts a duplicate refused **and** an employee with no national id created. **Both clauses, one body.** |
| **`AC-EMP-0018`** | ⚠ **RECOVERED** — **three clauses, three tests**: creation records `Created`; a transition using `Created` is refused; a transfer using `InitialAssignment` is refused. |
| `AC-EMP-0001`, `0002`, `0003`, `0004`, `0007`, `0013` | still unresolved — searched by symbol, not settled |
| **`AC-EMP-0016`** | ⚠ **unresolved, and now a CANDIDATE GAP** — see below |
| `AC-EMP-0044` | unresolved after three mechanism searches (pass 08) — **candidate gap** |

**Two of the nine recovered. The error rate on my old dispositions was therefore ~22%** — measured, not
estimated, and **on a population I named before checking it.**

## ⚠⚠ `AC-EMP-0016` IS A CANDIDATE GAP, AND THE MECHANISM SHOWS WHY

*"Search without a status filter returns only `Active` and `Inactive`."*

**`The_search_defaults_are_the_documented_ones` asserts `Assert.Null(reads.LastCriteria.Statuses)`** —
⚠ **the handler passes NO filter at all.** So the exclusion of `Terminated` must happen **downstream in the
read service**, and **nothing asserts that it does.**

⚠ **The default is not "exclude terminated"; the default is "no filter". Those are the same thing only if
something downstream makes them so** — and that something is unasserted. **Sixth by-product.**

## What this pass says about the method

⚠ **The mechanism search is better than the name search and is not sufficient.** It recovered two of nine
here and two in pass 08 — **but six of the nine remain unresolved after it**, and `AC-EMP-0035` shows a
mechanism search can also produce an over-claim.

**What actually caught the over-claim was a second reader disagreeing with a citation.** ⚠ **That is the
control the negative half still lacks, and it does not scale: it worked here because one criterion drew
two windows' attention.**

## Scope
- **Six criteria remain unresolved after a mechanism search** and should now be treated as *searched twice
  and not found*, which is a stronger statement than any earlier pass could make.
- **Every citation added this pass was body-confirmed**; the `0035` correction was verified against the
  body before being accepted.
