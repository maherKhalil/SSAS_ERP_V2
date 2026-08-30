# item 159 — sweeping the results trail for rulings never acted on

**Measurement only. Nothing built, nothing edited.** Scope: was `T-201` the only unread ruling among the
139 legacy files in `.claude/handoff/results/`? Known positive: `T-201`. Classify acted-on / superseded /
still-open. Prose, not shape-match.

## The method result, first — a structural method exists AND FAILS ON THE KNOWN POSITIVE

145 files, **19,157 lines, 151,619 words.** Two conventions:

- **`## For the architect`** — in **136 of 145** files
- **`### Follow-ups noticed but not done`** — in **14**, a section named for exactly this question

Together they are **1,299 lines of the 19,157** — a 15× reduction. ⚠ **But `T-201` carries neither.** Its
ruling sits under `## My judgement`. **Exactly three legacy files break the convention — `T-191`, `T-194`,
`T-201` — and the known positive is one of the three.** A structural sweep alone would have reported the
trail clean, which is the failure this item was commissioned to avoid.

So three passes: structure; a **broad** lexical net (482 candidate lines, 312 of them outside any
structural section); and the three convention-breakers read whole. **The net rediscovers `T-201`'s ruling
including the withdrawal** — control holds.

## ⚠ The answer: `T-201` was not typical. Every concrete ENGINEERING recommendation had been acted on.

Verified **in the tree**, not inferred from age:

| ruling | where | now |
|---|---|---|
| `ADR-017`, `ADR-026`, `ADR-027` stand at `Proposed` | T-002, T-010 | all three **`Accepted`** |
| `TS-SUB-0033` red until `PlatformDbContext` gains `PreventAppendOnlyMutation` | T-008, T-013 | **exists**, with `PlatformAppendOnlyGuardTests` |
| `gate.sh` has no memory band | T-010, T-015 | **floors present** — 2048 MB LEAN, 4096 MB FULL |
| record the *measured* gate time, not the documented range | T-014 | header states **~69 minutes**, the measured figure |
| commit and extend `trace-check.py` | T-002, T-004 | **committed**, extended as recently as T-106 |
| no `BR-SUB` prefix in the master register | T-002 | **present** |
| **"Do #1 and stop"** — the department hierarchy lock has no contention evidence | **T-191** | **`DepartmentHierarchyLockContentionSqlServerTests.cs` exists** |
| **"do Attendance's endpoint tests"** | **T-194** | **five Attendance endpoint test files exist** |
| the table's silence about the four existing draft routes | T-098-part1 | **`journal-drafts` now appears 9×** in FP-011's contract |
| `["salaryType"]` should accept `JsonValueKind.Null` | T-111 | **`[String, Number, Null]`** |
| `OD-SUB-0014` open while `DEC-L-034` had ruled it | T-041 | **false sentence removed**; `REQ-SUB-0020`'s stale `UNAUTHORED` line rewritten |

**Partially acted on:** T-127's *"the route inventory covers 4 of 11 assemblies"* — **eight** inventory
files exist today (Attendance, Company, GL, HR, Payroll, Platform Localization, Platform Support
Authority, Tenant User). Substantially closed; **I cannot certify all eleven without T-127's own
enumeration of them**, and I did not reconstruct it.

## ⚠ FOUR ARE STILL OPEN, AND EVERY ONE OF THEM IS AN ORPHAN

| still open | flagged in | verified today |
|---|---|---|
| `docs/02-Functional/Platform/Authentication.md` is **`Status: Draft`** | T-002, T-006 (*"third time"*), T-007 (*"fourth appearance"*) | **still `Draft`**. The *"expired subscriptions cannot login"* clause **is gone** — that half was actioned — but **`Expired Subscription` remains as a failure scenario** at `:157` |
| `docs/02-Functional/Platform/README.md:25` lists **Subscription** as a Platform area with no document behind it | T-002 | **still listed; no `Subscription.md` exists** |
| **`BR-ATT-*` and `BR-PAY-*` were never promoted** to the master `Business-Rules.md` | T-004 | master carries `BR-GL`, `BR-HR`, `BR-PLT`, `BR-RPT`, `BR-SUB` — **neither `BR-ATT` nor `BR-PAY`**. Two delivered modules' rules exist only inside their feature packages |
| **`BR-RPT`** is a master business-rule module **with no feature package at all** | T-005 | **still true** |

⚠ **THE PATTERN IS THE FINDING: AN ITEM THAT BELONGS TO A PACKAGE GETS ACTIONED; AN ITEM THAT BELONGS TO
NO PACKAGE GETS RE-FLAGGED.** All four survivors are register and cross-document hygiene — the class T-002
itself named *"the orphan class"* — and one was raised **four separate times** and never taken. Nothing
here was forgotten for want of a note. **They were noticed repeatedly and had no owner**, which is a
different failure from `T-201`'s and needs a different remedy: `T-201` was unread, these were unassigned.

## What this sweep did NOT read

The 14 `Follow-ups noticed but not done` sections in full, the three convention-breakers in full, and the
strongest lexical candidates — **but not all 1,299 lines of `For the architect`, nor all 482 candidate
lines.** A recommendation phrased without any of ~25 net terms, inside a file carrying the convention,
would be missed. The residue is a reading task, not a scanning one.

**`T-201`'s own lesson stands unchanged and is the reason this item existed:** grep this directory before
measuring anything — see `00-BEFORE-YOU-BUILD-AN-INSTRUMENT.md`.
