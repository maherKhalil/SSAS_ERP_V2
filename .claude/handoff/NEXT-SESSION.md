# Where to pick up — written 2026-08-30, at T-232 / PR #338

**This file was four days and roughly 180 tasks stale before this rewrite.** It described T-048 and said
*"nothing is in flight"*; the loop was at T-229. **Derived from `git log origin/ClaudeBranch` rather than
recalled** — 232 merges since 2026-08-27.

Read `BOARD.md` for the reasoning. This is what a cold window needs to act.

---

## The one thing that is urgent to somebody outside this loop

⚠ **TWO SECURITY PROPERTIES OF THE CUSTOMER'S LIVE HIS DATABASE**, found while planning its migration.
They are facts about *their running system*, not about the migration, and they are the only findings here
with any urgency:

- **`[dbo].[SPSearch]` and `[Finance].[SPSearch]`** take `@Table`, `@Fields`, `@Conditions` and
  `@Description`, and **execute `@Description` as a procedure name** — `exec('exec ' + @Description + …)`.
  **A generic SQL execution engine published as a stored procedure.**
- **`[Finance].[SPTransfearData]`** splices `@ToDataBAse` into **sixteen `DELETE FROM` statements** — it
  deletes Finance tables in a database of the caller's choosing.

**17 of 42 dynamic-SQL procedures splice an unconverted string parameter; 7 do it structurally.** Whether
they are reachable from application code **needs the app source and is not knowable from the schema.** The
architect has these in the owner's brief.

---

## The ERP

**Green and its three completion axes are closed.** `GATE_SCOPE=TASK` runs seven suites, ~3,075 tests, in
about 72 seconds.

- **Axis 1, documented capability with no endpoint:** decomposed 2026-08-29 from 67 to **41 owner-gated,
  15 already deferred, 9 our own doc errors, 2 real**. ⚠ **That count is stale and its MEMBERS were never
  recorded — read the correction below before using any of those figures.**

  **⚠ CORRECTED AGAIN 2026-08-30, AND THE PREVIOUS CORRECTION WAS THE WRONG ONE.** An earlier note here
  said the decomposition's members were *"NOT RECOVERABLE"*. **They are recorded in full, and always were:
  `.claude/handoff/results/T-201.md`, 142 lines, with the members, the method, the per-package breakdown and
  two self-corrections.**

  **The true decomposition is 41 owner-blocked, 15 deferred or superseded, 10 documentation drift, 1
  unblocked** — **not 9 and 2. T-201 had already corrected itself from 9-and-2 to 10-and-1**, finding the
  tenth in its own text: a row that *"begins **New.**"* and ends `[BUILT as POST .../change-department]`.
  Its words: *"this report fell into the trap it describes, in the paragraph above the one describing it."*

  ⚠ **The single unblocked row is `GET /api/platform/permissions`, the permission-catalogue read, and it
  SHIPPED 2026-08-29.** **So the residual is ZERO because its one member was built — not "within the error
  of an unrecoverable measurement".** **That earlier closing conceded an ignorance this project does not
  have, and it is withdrawn.**

  ⚠ **AND THE TEN ARE NOT DOCUMENTATION ERRORS TO FIX. T-201 explicitly withdraws that recommendation:**
  *"The first version of this report recommended rewriting the ten paths to the real routes. That was wrong
  and is withdrawn."* **Each row carries what was specified, what was built, AND the deciding reference —
  `DEC-DEP-0023`, `-0024`, `-0025` — so rewriting the path column would delete two of those three to fix a
  count.** **Its verdict: *"The documents are not wrong. The instrument was."*** **Nine instruments were
  built over two days to re-derive a conclusion that had already been written, corrected and reasoned
  through.**

- **Axis 2, endpoints with no behavioural test:** Attendance went **0 → 25 of 25 routes** issued a request;
  product-wide uncalled routes went **63 → 8**, and 8 is now an asserted number rather than a report.
- **Axis 3, owner decisions:** see below.

**Three live defects were found and fixed by issuing the first-ever HTTP request at those surfaces** —
`Attendance.LeaveSubmissionBusy` → 500, `Payroll.RunAlreadyReversed` → 500, and a missing `EmploymentType`
migration that caused **103 Integration failures**.

## The Integration suite: 43.9 → 24.2 minutes

**It carried 145 true failures unread for eight days**, because `GATE_SCOPE=TASK` never runs it. That is a
fact about the gate's scope, not about the tests.

- root cause of the 145: **one commit added a mapped property with no migration**
- **43.9 → 24.2 min, 855 passing.** Work fell 20,912 → 17,653 test-seconds; **effective parallelism rose
  7.94× → 12.17×**, and the parallelism rise bought nearly twice what the work reduction did
- **the suite is now exactly latency-bound** — 1,450 s wall against a 1,447 s longest class
  (`EmployeeBoundary`). Splitting it buys ~2 minutes; **16 cores floor the whole suite near 18. Stop here.**

⚠ **`Pooling = false` is LOAD-BEARING in 12 fixtures and must not be swept.** A `Session`-owned
`sp_getapplock` **survives disposal of a pooled connection** (measured: `APPLOCK_TEST` 0 pooled, 1
unpooled). Only a fixture whose *path* takes no session-scoped applock can be pooled — **and checking the
test file is not checking the path.**

## The HIS migration plan

`scripts/his-catalogue/MIGRATION-PLAN.md`, ~800 lines, **self-checking**: every number is emitted by the two
scripts beside it, the parser refuses a parse whose artefact counts drift from the source manifest, and
13 assertions verify every subset sums to its headline.

**Headline: the rebuild is ~71 logic artefacts, not 513** — 96% of the logic procedures sit in modules our
ERP replaces outright. **The logic that looks most intimidating is the logic we are not taking, and the
logic that survives sits in the artefact type that announces itself least** (63 rule-encoding views).

---

## What is waiting on the owner

**Five ERP decisions** (#2, #3, #4, #5, #11) block **41 of the 67 capability rows**. Decision #3
(`EmploymentType`) is already half-landed: the migration ships `defaultValue: 0` = `FullTime`, stated as an
assumption in the migration itself rather than left as an artefact.

**Test cadence is NOT on that list and is OURS** — `OWNER-DECISIONS.md`'s own scope section excludes
*"test shape"* as engineering-owned, and when to run our own suite is inside that. **An earlier draft of
this file escalated it with the phrasing *"it is their compute and their time"*, which is how an abdication
reads as deference.** The measured position: **24.2 minutes (T-234 board row 1095, 855 passing)** — not
per-task, comfortably a pre-merge or nightly gate where 44 minutes was not. **The loop decides it.**

**Three HIS decisions (D1–D3)** settle **130 of the 159 crossing foreign keys**. D1 (is `GeneralStores` a
shared service?) also decides whether 25 of the 54 clinical rule-views are ours or theirs.

⚠ **AND OWNER DECISIONS LIVE IN TWO PLACES, WHICH IS ITSELF WORTH KNOWING.** `OWNER-DECISIONS.md` holds the
**eleven ERP entries** and states its own exclusions. **The HIS decisions D1–D3 are in
`scripts/his-catalogue/MIGRATION-PLAN.md` and appear on neither list**, as does the prior question of
whether the HIS migration proceeds at all. **A window reading `OWNER-DECISIONS.md` alone will conclude
there are eleven; there are eleven plus three plus one.**

---

## Two operational hazards this loop has already paid for

⚠ **THE WORKING TREE IS SHARED IN TWO DIMENSIONS.** `git commit` commits the **index**, so `git add <mine>`
does not scope a commit — use `git commit -m "…" -- <paths>`. And **a branch checkout is as shared as the
index**: one incident swallowed 441 lines of the coder's staged work, a second destroyed the architect's
unpushed commits. The architect now commits from a separate detached worktree.

⚠ **A PLANT IS A DELIBERATELY BROKEN TEST IN THE TREE FOR ABOUT A MINUTE.** If the other window commits
inside that window it reaches the branch under someone else's message, and `git checkout --` then restores
the plant rather than removing it.

---

## If you are the coder

Run `.claude/roles/PROTOCOL.md` and `.claude/roles/CODER.md`. **Do not pick your own work** — the architect
dispatches. The queue was at zero when this was written; the architect enumerates rather than declaring
zero, and that has produced non-empty answers every time it was tried.
