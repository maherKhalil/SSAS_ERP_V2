# The work queue — authoritative

**This file is the queue. Messages are notifications about it, not the queue itself.**

⚠ **Read this before going idle, and read it whenever a message from the architect seems to be missing.**
Roughly half the architect's inbound messages are lost, and the coder's outbound reports are not always
received. **A silence is a lost message, not a decision.** This file is in the repository, so neither
window can lose it.

⚠ **IF `git fetch` FAILS, READ THIS FILE FROM DISK INSTEAD — IT DOES NOT NEED THE NETWORK:**
`C:/Users/User/Documents/SSAS_ERP_V2/SSAS_ERP_V2-board/.claude/handoff/QUEUE.md`

**That is the architect's worktree, on this machine, always current with its last commit.** This file was
put in the repository so a dropped message could not stall the coder — **and was then read through `origin`,
the one dependency that fails at the same time as everything else.** On 2026-08-30 a GitHub outage left the
coder believing the queue held one item when it held three. **A local path closes that.**

⚠ **GREP `.claude/handoff/results/` BEFORE BUILDING ANY INSTRUMENT.** On 2026-08-30 **nine instruments were
built over two days to re-derive a conclusion that was already written, already corrected, and already
reasoned through** — `T-201.md`, 142 lines, with the members, the method, the per-package breakdown and two
self-corrections. **It was never searched.** ⚠ **It had even stated the general rule one window later
"discovered": *"an instrument that reads only the machine-readable columns will miss the corrections people
wrote in the margin — and so will a reader who stops at the first word."*** **The results trail is cheaper
than every instrument in this thread combined.**

⚠ **EVERY ITEM PRODUCES A COMMITTED RESULT FILE IN `.claude/handoff/results/`, INCLUDING MEASUREMENT-ONLY
ITEMS.** One file per item, named for it. **Findings and their scope, not narrative** — the board carries the
reasoning, the results carry the measurements.

⚠ **AND THE FILE MUST LIST THE MEMBERS, NOT ONLY THE COUNT AND THE GROUPS.** On 2026-08-31 item 170 needed
165's 61 members and **the result file held only the number and the group names** — recovering them depended
on a probe log surviving in `%TEMP%`, **one cleanup away from unreproducible.** ⚠ **That is the `T-201`
failure in a file written FOUR ITEMS after its lesson was recorded**, by the window that recorded it.
**A remainder you can NAME is a finished measurement; one you can only COUNT is an unfinished one** — and
the members are the part the next item needs, every time.

**⚠ STATE THE BLIND SPOTS IN THE FILE, NOT ONLY IN THE MESSAGE.** Every report in this loop has named what
its population excluded, **and that caveat is the part most likely to be lost when a number is reused by
someone who was not here.** *"Zero real over 139"* was true and became misleading **precisely because its
scope did not travel with it.**

**Why this rule exists:** on 2026-08-30 items 150–157 ran for **78 minutes producing no commit at all** —
the phantom classification, the complete 199-route table, the built→documented mirror, the support-surface
verification — **all of it living only in messages and in board prose.** ⚠ **The 199-route table is the most
reusable artefact of the day and would have vanished with the session.** **That is the same failure as a
count written into a summary while its members stayed nowhere, which this loop had already diagnosed — and
was then doing to its own work.**

**Standing authority:** work down the list without waiting between items. If one is blocked, skip it, start
the next, and say which in the following message. Every item here is gated (`src/` + `tests/`) unless
marked otherwise, so **`DEC-L-007` applies: green gate, merge immediately — no `MERGE` word is required.**

⚠ **AN EMPTY QUEUE IS NOT A REASON TO STOP — IT IS A REASON TO PULL FROM `BACKLOG.md`.** Take the top item,
start it, and say which one in your next message. **No permission needed.** If the backlog is also empty,
pick the highest-value thing you can defend, state the reasoning in one line, and start.

**Never send a message whose only content is that you have no work.** If you are reporting an empty queue,
you should already be working and naming what.

**Refill:** the architect refills at TWO remaining. ⚠ **That rule has now failed ten times, and the reason
is structural: it fires only when the architect is looking.** A reactive refill always leaves a gap between
the coder finishing and the architect noticing — and the earlier wording covered *"drops to two with no
refill"*, which is the case that does not happen, while saying nothing about **zero**, which is the case
that does. **`BACKLOG.md` is the part that depends on nobody being awake.**

## ⚠ CORRECTION — ITEM 90 WAS FINISHED, NOT IN FLIGHT. THE WORKING TREE IS CLEAN.

**An earlier version of this section said 90 was part-done and uncommitted on `ClaudeBranch`, and listed
eight files to inspect. That was wrong.** 90 gated green and merged as **PR #347** in the same minutes; the
files seen modified were the finished change immediately before its commit. **Nothing is uncommitted and
there is nothing to recover.**

**Recorded rather than deleted, because it is the failure this file exists to prevent** — a status written
from a snapshot taken at the wrong instant, published as fact, three minutes after the architect finished
correcting the same class of staleness elsewhere. **A working-tree observation is a measurement with a
timestamp, and it decays in seconds.**

**True state: `ClaudeBranch` carries #345, #346 and #347. 91, 92 and 93 are unstarted and fully specified
below. Nothing is in flight.**
## Session ended 2026-08-30 — where to pick up

**The owner signed out. Both windows stopped cleanly.** ⚠ **This section records COMMITTED state only** — a
working-tree observation decays within one commit and has already misled this loop once today.

⚠ **ITEM 129 IS ON A BRANCH THAT IS DELIBERATELY RED AND MUST NOT BE MERGED:
`agent/T-268-precondition-code-RED`, commit `0d05986`, pushed.** The name carries the warning. **The gate
refused it and was right to.**

**Failure 1 — a real placement error, and its cause is worth reading before fixing it.**
`ProjectDependencyArchitectureTests.The_shared_api_project_names_no_business_concept` reddened on
`["ApiProblems.cs -> Company"]`: `CompanySelectionRequired` was put in the shared `ApiErrors` in
BuildingBlocks, **and the shared API project may not name a business concept.** ⚠ **That is why
`company.scope_denied` is declared separately in six module mappers rather than once centrally — apparent
duplication that is the rule being obeyed.** The correct placement is **a per-mapper constant in each of the
four mappers, matching the `CompanyScopeDenied` idiom exactly.** Small and mechanical.

**Failure 2 — not a mistake. It is the contract change 129 asks for.**
`D30_A_missing_or_malformed_company_header_is_a_validation_failure` asserts the CURRENT behaviour, that this
condition answers `request.invalid`. **It must be updated to expect `company.selection_required` and to say
why a precondition is distinct.** It was deliberately left untouched: **rewriting a wire-contract assertion
at sign-out is the exact edit that should be made carefully rather than quickly.**

**Sound and reusable on that branch:** the four mapper arms, the Platform alias, and
`PreconditionCodeArchitectureTests`, **whose plants both pass** — a module collapsing back to the generic
code reddens the ban, and a broken arm regex reddens both the floor and its own control.

**130 has no edits at all. Nothing of it is lost.**

**Both rulings are inline in the table below and neither needs re-deriving.**

**What remains after 129 and 130 is owner-gated, and that is an enumeration result rather than an empty
queue:** five ERP decisions blocking 41 capability rows, and three HIS placement decisions. `BACKLOG.md`
is empty of open B-items **deliberately** — the loop spent 2026-08-30 on instrument hardening because that
is where the defects were, and inventing another sweep to avoid saying *"this needs the owner"* would be
the failure this board has recorded under several other names.

**Also open, unchanged, and not urgent:** the 13 local-only `codex/*` branches on a PUBLIC repository, and
`agent/T-072-spec-and-authorization-model`, whose content was landed directly on 2026-08-30 and whose PR
was closed with an explanation — **the branch is left in place on purpose.**

## ⚠ CORRECTION — THE "QUEUE IS EMPTY" CLAIM ABOVE WAS FALSE AS WRITTEN

**An earlier version of this section listed nine closed sweeps as evidence that nothing remained.** ⚠ **Every
one of them was inside a single subject — guards, instruments, error codes — and the section implied the
product.** **The capability axes were never re-checked.**

**What that missed, found in one grep:** `NEXT-SESSION.md` records Axis 1 decomposing from 67 rows to
*"41 owner-gated, 15 already deferred, 9 our own doc errors, 2 real"* — ⚠ **and the two real ones are named
in no file in this repository.** A count survived and its members did not.

**The rule this loop wrote today and the architect then broke: a remainder you can NAME is a finished
measurement; one you can only COUNT is an unfinished one.** And: **a complete enumeration of the wrong set
reads exactly like a complete enumeration.**

⚠ **AND A SECOND BLIND SPOT, FOUND BY 160 ON 2026-08-30: EVERY SWEEP ASKED WHAT THE CODE CONTAINS, AND NONE ASKED WHETHER THE DOCUMENTS' CLAIMS ABOUT THE CODE STILL HOLD.** One question about one row turned up a **ratified** package whose README says *"No code and no schema. Nothing here is implemented"* while its first migration is dated **the next day**. **Eight enumerations ran with the tree in front of them and none could see it, because none was pointed at the documents.** Now Principle 20. **Item 161 is that measurement.**

**147 re-derives the whole decomposition — all four buckets, not just the two — because the count is a day
old and three items have shipped against Axis 1 since.**


| # | item | status | detail |
|---|---|---|---|
| **173** | ⚠⚠ **FIX IT: A COMMITTED COMMAND MUST NOT BE REPORTED AS FAILED. RULED, AND FROM THIS PRODUCT'S OWN REASONING.** 166 established that dispatch happens AFTER commit deliberately, **because an event announcing rolled-back work is worse than no event. The symmetric statement is that a COMMITTED WRITE MUST NOT BE REPORTED AS FAILED** — so a consumer failure must not fail the command. **Your recommendation is taken: move dispatch OUTSIDE the `try`, after the transaction is complete.** ⚠ **AND BOTH HALVES OF THE MASKING, BECAUSE YOU PROVED A ONE-HALF FIX LEAVES IT:** removing the rollback from the `catch` reddens the masking test **and leaves the disposal test passing**, since `completed` is still false and the field is still cleared. **Fix the `completed`/disposal half too.** ⚠ **NOT SWALLOWED — SURFACED AS ITSELF: log the consumer failure with correlation id, event type and consumer type, and let the command succeed.** The one registered consumer invalidates a cache, and a silent failure there is a stale cache nobody sees. ⚠ **CONSUMER ISOLATION IS IN SCOPE: you noted a throw abandons the remainder of the loop, so one bad consumer stops every other. Isolate each.** **The three tests pin CURRENT behaviour and must be rewritten to pin the correct behaviour — say at each declaration what changed and why.** **Plant every half.** | **open — ruled** | BOARD 2026-08-31 |
| **174** | ⚠ **THE UNSEARCHABLE FORMS OF THE DETACHED-AGGREGATE HAZARD** (was `B16`.) 167 closed the `AsNoTracking` form; **`AsNoTracking` is the SEARCHABLE form of the hazard, not the only one.** **An entity materialised in one context and mutated against another, or REBUILT FROM A DTO, raises events dispatch never sees.** Also unsearched: `tests/`, and aggregate types that raise **through a helper** rather than calling `RaiseDomainEvent` in their own file — 167's 14 types came from files containing that call. ⚠ **APPLY PRINCIPLE 24 HERE, BECAUSE A NAME SEARCH CANNOT WORK: enumerate the MECHANISMS by which an aggregate can exist un-tracked, not the places you imagine it happening.** `AsNoTracking`, a second `DbContext`, `Attach`/`Entry` manipulation, construction from a DTO, `Detached` state — **say what the complete mechanism set IS before searching for members of it.** **Then say whether any is reachable in production, and validate the search against a known positive.** **Report only.** | **open — ruled** | BOARD 2026-08-31 |

**Closed 2026-08-31:** 172 (**PR #389 — ⚠⚠ A REAL DEFECT: the commit succeeds, the data is written, THE CALLER IS TOLD THE COMMAND FAILED, and the consumer's exception is DESTROYED — `RollbackAsync` on the committed transaction throws first, so `throw;` is never reached, and disposal then rolls back AGAIN. ⚠ The plant was the candidate fix, which is how it was found INCOMPLETE. Ruled and being fixed under 173**), 171 (**PR #388 — a module is a project under `src/Modules/`, read from the LAYOUT, so no second hand-written list was needed and the stop condition did not fire. ⚠ The directory name is NOT the assembly prefix — `Finance/` holds `SSAS.GL.*` — and a folder-name predicate would have PASSED WHILE MISSING A MODULE. ⚠ And naming is not redundant: it catches a module MOVING where a scan passes vacuously**), **Closed 2026-08-31:** 170 (**NONE of the 61 unconsumed members is reached without a compile-time reference, every answer MEASURED — by enumerating the MECHANISM (4 dynamic sites, 2 of them JSON lookups) rather than the names, where `TenantId` alone matches 2,236 literals. ⚠ The architect's requested DI control HAD NO SUBJECT: a container resolves TYPES, never interface MEMBERS. ⚠ And 165's result file recorded the COUNT but not the MEMBERS — the `T-201` failure four items after its own lesson**), 169 (**PR #387 — 14 hand-written assembly lists, 3 module-shaped; the sorting question is *would a new module be missing*, not *is it floored*. ⚠ THE CONTROL FAILED ON FIRST RUN, naming two assemblies 168's guard had never scanned. ⚠ Output directory not `GetReferencedAssemblies()`, because the compiler omits an unused reference and the check would have agreed with the stale list FOR THE WRONG REASON**), **Closed 2026-08-30:** 168 (**PR #386 — the read-side escape guard, three controls, three disjoint plants. ⚠⚠ AND IT DISPROVED 167'S CLOSING FACT: EIGHT command handlers DO take a read service — the search filtered on files named `*CommandHandler*.cs` and handlers here are named for their aggregate, PLURAL. The conclusion survives and the reason does not; `ADR-009` corrected. ⚠ Plant two emptied the population and THE GUARD PASSED — only the independent control and inventory failed**), 167 (**PR #385 — the detached-aggregate hazard is REAL and NO PRODUCTION PATH REACHES IT: 203 `AsNoTracking` sites, 30 touching event-raising aggregates, 8 entity-shaped, all in read services no command handler injects. ⚠ The first classification over-fired 26 → 8 — PROXIMITY IS NOT SEMANTICS, and a guard on that pass would have fired on 17 correct existence checks. ⚠ Guard shape ruled: NEITHER a ban nor a test alone — guard the ESCAPE. Pinned as current behaviour, not as correct**), 166 (**PR #384 — the withdrawal EXERCISED, not read: five tests on real infrastructure, consumer reached with metadata populated. ⚠ Events raised inside a transaction are WITHHELD AND RELEASED, not dropped — and rollback never dispatching is the POINT of the design. ⚠ The inverse bug — dispatching before commit — does not occur only because `TenantUnitOfWork` caches ONE inner `EfUnitOfWork`, which reading `EfUnitOfWork` alone cannot settle. ⚠⚠ And the cleanest statement of the 17 failure: THE INSTRUMENT WAS RIGHT AND THE QUESTION WAS WRONG**), 165 (**61 of 499 interface members have no production consumer; 5 test-only, 56 nothing. ⚠ The demanded known-positive validation KILLED the first instrument: a compile-error probe cannot enumerate consumers, because a failing project stops its dependents compiling and its own signal halts the search. ⚠ Attempt 2 — `[Obsolete]` at WARNING level, build succeeds, list complete — and the known positive proved the counts are consumption, not declaration. ⚠⚠ AND IT SURFACED THAT OWNER DECISION 17 WAS FALSE**), 164 (**PR #383 — `ADR-025` decision 4 ENFORCED rather than stated: `TenantForbiddenClaims`, and `ICurrentUser.CompanyId` removed. ⚠ The architect's "three references" was true of CONSUMERS and wrong about IMPLEMENTERS — there were ONE HUNDRED, and removing an interface member ORPHANS them into `CA1822` rather than breaking them. ⚠⚠ An INCREMENTAL build reported zero warnings while four orphans stood, because those projects were never recompiled. ⚠ Full out-of-set rejection COSTED AND DECLINED, reasoning and expiry condition recorded in the ADR**), 163 (**PR #382 — NO DIVERGENCE: the tenant token complies with `DEC-AUTH-0049` exactly, and the claim set is now pinned AS A SET rather than by a denylist. ⚠ The architect's dispatch was wrong that it was unpinned — it was PARTLY pinned, and the set was the missing part. ⚠⚠ The finding is `ADR-025` decision 4: a BINDING prohibition, guarded on the platform plane and UNGUARDED on the tenant plane it names — annotated in the ADR, being closed by 164**), 162 (**PR #381 — `EntitlementPermissionCouplingTests`, 7 tests: entitlement and the tenant permission set are UNCOUPLED, proven by exercising the path rather than searching it, with a STRUCTURAL pair that reddens on any future collaborator. ⚠ `AC-SUB-0026` cannot be exercised at all — there is no lapse event — and was ruled into a fifth state with `0008`; ⚠ a third proposed move, `AC-SUB-0045`, was REFUSED because its six permissions are a parenthetical and its subject is the whole 28-name set**), 161 (**⚠ FP-014: 20 PINNED · 11 IMPLEMENTED-UNPINNED · 19 NOT IMPLEMENTED · 4 SUBJECT UNDEFINED. The line falls between what a tenant MAY USE and what a tenant IS CHARGED — the entitlement half built and well tested, the billing half absent entirely. README rewritten and dated; `AC-SUB-0020`'s stale counts corrected and its TEST deliberately left alone**), 160 (**⚠ A FOURTH POSSIBILITY: login CANNOT refuse an expired subscription — eligibility is a total function of `TenantStatus` — but expiry DOES refuse, `403` at the module boundary. The row was filed against the WRONG SURFACE, not residue, and deleting it would have removed the product's only mention of a real refusal**), 77, 78, 159 (**every concrete recommendation in the results trail was acted on; four survive and all four were UNASSIGNED rather than unread**), 157 + 158 (**the anonymous door is well defended; PBKDF2-SHA512 at 100,000 iterations, CONFIGURED and FLOORED rather than inherited**), 156 (**DOCUMENTATION GAP, NOT A SECURITY GAP — the support surface is guarded better than most of the documented one**), 155 (**THE MIRROR: 26 of 155 mapped routes appear in no `api-contracts.md` — and the largest group is a privileged cross-tenant admin surface**), 154 (**exactly 2 true phantoms — and one of them sat OUTSIDE the population the architect scoped**), 153 (**ZERO of the 31 are documentation errors — 24 are the document annotating ITSELF, 7 were the matcher's fallback**), 152 (**AXIS 1 CLOSED on a stated, complete population: zero real over 200 distinct routes — and every one of the 62 previously invisible routes is BUILT**), 151 (**three route formats not four, nine packages MIXING them, and 62 routes invisible to every sweep in the thread**), 150 (**THREAD CLOSED — the row is in no documented route in the product, and may not exist at all**), 149 (**139 documented routes classified — the population was never 44 — and ZERO real gaps, so the missing row is prose-only**), 148 (**method killed by its own first result: the capability→endpoint link is NOT IN THE DOCUMENTS — one package of fifteen has both halves**), 147 (**one of the "2 real" rows was already SHIPPED; the second is unrecoverable, and the reason is the finding**), 146 (**all five explained — none a defect, none unfinished; ten of eleven never-raised codes accounted for**), 145 (**PR 379 + 380 — both published contracts now honoured**), 144 (**7 of 63 documented codes have no producer — and TWO are DIVERGENCES rather than absences**), 143 (**11 of 535 declared codes never raised — 2% — and the codebase had ALREADY RECORDED two of them, including 135's mechanism**), 141 (**PR 378 — the field sweep CLOSED: 63 rows, six modules, 77 reads, ZERO wire-contract errors, 42% rejection**), 142 (**PR 377 — `field` is a JSON path; and the population was TWO modules, not three, which did not matter because the population was never the load-bearing premise**), 140 (**collection-valued requests exist in FOUR modules, not GL alone — and the SPLIT matters more than the count**), B13 (**dissolved: all three "orphan" arms are alive; the instrument filtered on a FILENAME**), 137 (**PR 376 — Payroll and GL: ZERO wire-contract errors across all 38 rows, and GL's 80% rejection is a MECHANISM limit, not a candidate problem**), 138 (**INERT — `LimitAt` has no production caller at all, so the grant-or-deny question never arises**), 139 (**⚠ THE OPPOSITE of a recorded deferral: three ACCEPTED ADRs specify a dispatcher the product does not have — ADRs annotated, owner decision pending**), 136 (**17 Domain methods with no production caller, in FOUR classes — and most of the subscription cluster is owner decision 11, reached independently from the code**), 134 (**PR 375 — Attendance: 17 proposed, 11 accepted, 35% rejected, and ZERO wire-contract errors across 27 rows**), 135 (**MEASURED: Attendance-driven HOURLY overtime cannot be paid in production — locked twice, independently**), 133 (**the attributable population is ~71, not 16 — and the architect's "value object" premise was wrong: only 2 of 16 are value objects**), 132 (**PR 374 — the `field` extension, SIXTEEN codes not seventeen, guard running in THREE directions**), 131 (**17 of 19 clean, and the correspondence is NOT mechanically derivable — but a curated map with automatic verification is**), 129 (**PR 373 — the precondition code, placed per-mapper; RED branch deleted**), 130 (**both designs costed; the field identifier is STATIC PER CODE, which removes the plumbing cost**), 127 (**PR 372 — load-bearing controls renamed for what they hold up**), 128 (**28 of 129 are candidates, in five classes**), 126 (**the remaining matcher controls — and the count went 25 → 11 → 5, every step down from READING**), 125 (**PR 369 — route precedence pinned for the three literal/parameter pairs**), 124 (**PR 368 — the eight matcher controls; the FIRST one failed on its first run and found a hole where the canonical JWT type should have been**), 121 (**pagination split at both layers — and it surfaced a FOURTH condition: an export ceiling refused with the pagination code**), 123 (**PR 366 — `detail` shipped, and a second fail-closed class was found by an existing test**), 122 (**the domain message NEVER reaches a caller — no `detail` in any of 40 problem-document call sites**), 120 (**#363 — six structural guards floored, 34 tests, every guard planted twice; and probing the seventh caught the PROBE lying**), 115 (**all five FP-015 docs re-verified: 4, 3, 0, 0, 1 — every discrepancy an absence claim**), 118 + 119 (**#362 — the model-only tests moved, and the timing tell is now a guard set at the measured cliff**), 112 (**#360 — sampler path; the Process.Start sweep found nothing**), 113 (**FP-015 authorization-model: four of eight sections overtaken, all absence claims**), 114 (**8 → 1 on a current corpus — and the one is NEW, so the class regenerates**), 116 (**catch census closes at 11, all Group A**), 109 (**#358 — shared `TestSupport.CutoverModel`; the six C6 checks now run in every gate**), 110 (**#359 — TWO reasons, not one gap: empty scaffolds counting an accurate zero, and rows a mechanism postdating the only qualifying run by two hours could not have written**), 111 (**one real site, and it REFUSES TO START rather than relocating**), 106 (**#355 — two moved, six blocked on a shared type**), 107 (**#356 — the TRX escape REPRODUCED and fixed**), 108 (**#357 pending — guard built; the arm deliberately NOT added, and the architect overruled itself**), 105 (**no handler disagrees with the generic arms — 14 translations across 12 handlers, every reachable one resolving to 409; one LATENT gap found**), 103 + 104 (**#354 — and the timing tell found ZERO false greens in 806 tests; what it found instead was eight structural assertions misfiled into Integration**), 102 (**and TWO OF THE FOUR FLAGGED GUARDS NEEDED NOTHING — an exact-list assertion and a fixed type array are anti-vacuous by construction**), 97 + 99 (**#353 — EF DOES log it: one Error entry, exception attached, category `Microsoft.EntityFrameworkCore.Update` and NOT the predicted `Database.Command`; closes as a comment, no remedy**), 100 (**`correlationId` is populated in production — the empty value was the Attendance test host omitting `UseCorrelationId`; the architect's concern INVERTED, and cost nothing because it was reported unverified**), 101 (**#352 — the false green closed, two rules converted to reflection, five plants recorded IN THE FILE**), 95 (**⚠ demonstrated a LIVE FALSE GREEN: `PersistenceArchitectureTests` passes all nine when its file walk finds nothing; 18 text-matching guards, 12 assert an absence, and ALL TWELVE have no recorded plant**), 98 (**#351 — GL under the amended DEC-DEP-0027**), 94 (**212 clauses, 16 unreasoned, groups unchanged in SHAPE — and the architect's published 291 was worse than the original 207**), 93 (**#349 — no route registered with a non-literal pattern**), 96 (**#350 — 409 for Attendance and Payroll; GL STOPPED against a recorded decision**), 97 (**measured: no logger on either unit of work, and `Error(Code, Message)` cannot carry the `SqlException` that holds the index name**), 92 (**measured: a duplicate key returns 500 in three modules; and the architect's premise was FALSE — the unit of work does discriminate 2601/2627 from a deadlock, so Group B is harmless**), 91 (**#348 — 13 teardowns reasoned, tests-side unreasoned 13 to 0, and a REFUSABLE guard replacing the vacuous one the architect specified; it found a genuinely empty body whose reason sat on a `#pragma` line**), 90 (**#347 — the auth-path defect: both overloads log the cause, the caller's answer is unchanged, and the test asserts BOTH halves because either alone is satisfied by the wrong fix; the other nine group-C catches were sound and now say so**), 89 (**31 literal-matching instruments, only ONE exposed — the rest are structurally immune because C# forbids composing a type name in a type position**), 79 (docs — architect's, #341), 80 (#342/#343), 81 (**dissolved**), 82
(**cancelled — already measured**), 83 (#344), 84 (**overturned 80's premise**), 85 (**#345 — 71 constraints
removed, product-wide total 0, architecture test planted**), 86 (**#346 — 3 real gaps called, 2 allowlisted
with the segment check; the tightening caught a false green in its own implementation**), 87 (**the ruling
survived falsification and is pinned by a test**), 88 (**census: 207 catch clauses, 94 discard, 40
unreasoned; two instrument defects found and corrected before the number was trusted**).

**Group C of the 88 census is CLOSED (#347). Group A — the ~13 where the exception type IS the reason — is ruled LEAVE.** No comments
there; **the single collective statement is the architect's to write.**

**Division of labour, set by the owner 2026-08-30:** the coder does **coding and testing only**; planning
and documentation are the architect's. **A docs-shaped item is pushed back, not done.** When something
found while coding belongs in a document, **report the finding and the architect writes it.**
