# Four ways a guard proves it is not asserting nothing — and the separate question of whether its name is true

**Derived 2026-09-07 from reading 20 of the 99 `.cs` files in `tests/Architecture.Tests`. Nothing here is a
proposal; all four forms were already in use before this was written.**

⚠⚠⚠ **THE EXEMPLARS AND THE FIGURES HAVE DIFFERENT PROVENANCE AND THIS DOCUMENT WILL NOT LEND ONE THE
CREDIBILITY OF THE OTHER.**

- ***EVERY EXEMPLAR AND EVERY QUOTED HEADER BELOW WAS OPENED AND CHECKED*** — audited independently
  2026-09-07 against commit `b83ad39`, including verifying that each named test actually uses the form
  attributed to it rather than merely carrying the right name. **All survived; the two long quotes are
  verbatim, not paraphrase.**
- ⚠ ***THE FIGURES WERE FIRST WRITTEN FROM RECOLLECTION, AND THREE OF THEM WERE WRONG.*** The corrections
  are below, at the numbers. **What was read held; what was recalled did not** — which is the argument for
  the rule rather than an aside about this document.

⚠⚠⚠ **EVERY FIGURE IN THIS FILE ARRIVED BY BEING RETYPED FROM SOMEWHERE ELSE, WHICH MAKES IT A *JOIN*
EVEN WHERE THE ORIGINAL WAS MEASURED.** Provenance, so a reader can tell which kind of number each is:

| figure | kind |
|---|---|
| **15 call sites across 5 files** | ⚠ **MEASURED TWICE, by two instruments with disjoint blind spots** — `git grep -c` (counts matching LINES, blind to two-per-line) and `grep -o` with comments stripped (blind to nothing here). *They agreed; neither author could see their own blind spot at the time, so the agreement was luckier than it looked.* |
| **26 entity names** | **MEASURED** — literals counted in the file |
| **1 · 3 · 11 · 11 · 12×9 · 13×3 = 173, over 16 packages** | **MEASURED** — `uniq -c` over the tracked listing; sums to the walk's own reported total |
| **56 of 147** | ⚠⚠⚠ **NOT RE-DERIVABLE** — see the note at the number |
| **20 of 99 files read** | ⚠ **JOINED** — a running tally kept across a session, never recounted against a list of what was actually opened |
| **4 forms after 4 files, then 4 after 20** | ⚠ **JOINED** — a tally, not a measurement, and the claim it supports is about the *absence* of a fifth form |
| **4 for 4** *(best control invisible to the scanner)* | ⚠ **JOINED, AND AN ENRICHED SAMPLE** — four files chosen *because they looked unprotected*. Sound as a negative, worthless as a rate. |

***THE FOURTH JOIN SHAPE, FOUND 2026-09-07 AND THE SUBTLEST: A FIGURE MEASURED WITH A DIFFERENT PREDICATE
THAN THE INSTRUMENT IT DESCRIBES IS A JOIN, WITH NO ARITHMETIC INVOLVED AT ALL.*** Two file counts were
taken by an outside command excluding `obj` while the walk they described excluded only `Migrations` — a
six-file disagreement. ⚠ **And it is the one that paid: that disagreement is the only reason anyone
discovered the walk was reading its own build output.** *A joined figure was the detector.*

⚠⚠ **Measured 3-for-3 that night: every false number was a join across two predicates; every directly
measured number was right.** *Three is a small sample, and the rule it licenses is **label it or
re-measure it**, not "joins are wrong."*

---

## ⚠⚠⚠ READ THIS FIRST, OR THE TABLE BELOW WILL MISLEAD YOU

**A guard can fail in two independent ways and they are not the same question:**

| | the question | what fails |
|---|---|---|
| **VACUITY** | does the assertion run over anything? | `Assert.Empty(x)` where `x` is empty for the wrong reason |
| **NAME HONESTY** | does the predicate check what the name promises? | a correct assertion over a narrower thing than the name claims |

***THE FIRST DISGUISES THE SECOND.*** Measured here: `EmployeeReadScopeArchitectureTests.No_global_query_filter_scopes_company_or_branch`
carries **form 4, the strongest control found** — and still walks only the tenant context, so every
`PlatformDbContext` filter is invisible to it. **A well-controlled guard reads as trustworthy, so nobody
re-reads its name.**

**Fixing one does not fix the other. `BranchTransfer` needed both**: widening fixed the population, renaming
fixed the claim, and *widening without renaming makes the over-claim more dangerous, because the test now
looks thorough.*

---

## The four forms

### 1 — SOURCE FLOOR ⚠ THE WEAKEST, AND THE ONLY ONE A NAIVE SCAN RECOGNISES

`Assert.NotEmpty(collection)` / `Count >= n` on the collection before the assertion.

**Exemplar:** `ModelWalk.FlooredEntities` / `FlooredProperties`, used at **15 call sites across 5 files**
(BranchTransfer 2 · ConstructorKeyedEntityModel 1 · Payroll 6 · TenantBackupScheduler 2 ·
UnicodeStringPersistence 4 — counted 2026-09-07 over `git ls-files tests/Architecture.Tests/*.cs`, excluding
the two definitions in `ModelWalk.cs` and one comment mention in `CriterionCommentGuardTests:205`). The
population and its floor are taken together and it **cannot be called without the floor being asserted**.

⚠⚠ ***THIS FIGURE WAS PUBLISHED WRONG TWICE, IN OPPOSITE DIRECTIONS, BEFORE ANYONE RAN THE COUNT*** — "8
across 2", corrected to "11 across 3", both from unchecked greps whose corpus included build output. **The
correction was as wrong as the original, and a confident correction is harder to doubt than a first claim.**
*One `git grep -c` settled it; nobody spent it for three revisions.*

⚠⚠ **ITS LIMIT IS STATED IN ITS OWN HEADER AND IT IS THE REASON THE OTHER THREE FORMS EXIST:**

> *"These floors prove the MODEL was read. They cannot prove a PREDICATE still matches, because each ban
> filters the same walk differently — `IsUnicode() == false` and `GetPrecision() != 19` share a root and
> share nothing else. **A shared floor cannot discharge a per-predicate control, and there is no shared
> helper here that pretends otherwise.**"*

***SO: A SOURCE FLOOR SAYS NOTHING ONCE THE ASSERTION FILTERS.*** Measured 2026-09-07 — of 147 absence
assertions "floored elsewhere in the file", **56 filter a population the floor asserted.**

⚠⚠⚠ **THAT PAIR IS NOT RE-DERIVABLE AND THE DOCUMENT SAYS SO RATHER THAN LETTING IT LOOK CHECKABLE.** The
census script that produced it used a **three-bucket** partition that no longer exists; re-run today the same
corpus gives four buckets — 132 in-method / 143 shared-source / 61 no-shared-source / 22 unfloored — and
"floored elsewhere" is **204**, not 147. ***THE BUCKET DEFINITIONS CHANGED BETWEEN THE RUNS, SO 204 DOES NOT
REFUTE 147; IT ANSWERS A DIFFERENT QUESTION.*** **The 56/147 may well have been right when taken and no
instrument that still exists can confirm or refute it.** *Treat the direction as established and the
magnitude as unverified — and if you re-derive 204, that is not evidence this document is stale.*

⚠ **ONE FLOOR PER LAYER, NEVER OVER A UNION** (T-263): a guard floored `fields.Concat(properties)` as one
number, and breaking the field walk left the property walk clearing the floor by itself while a field-held
offender went undetected.

⚠ **NAME THE CALL SITE `Floored…`.** These were `Entities` and `Properties`, and the call site read as data
access, so nothing there said an assertion had happened. *A comment is a note that must be SOUGHT; a name is
a note that is READ.*

### 2 — DISCRIMINATING COMPANION

An assertion beside the ban, pinning a fact the ban's own machinery depends on.

**Exemplar:** `AuthenticationMilestoneArchitectureTests` — `Assert.Equal(typeof(SensitiveActionToken), typeof(GeneratedActionToken).GetProperty("SensitiveToken")?.PropertyType)`
directly after a reflection-driven `Assert.Empty`. If the reflection goes blind — a type renamed, a property
gone — the companion reddens even though the ban still passes.

⚠⚠ **THE COMPANION MUST SHARE THE FIRST DERIVATION'S CONSTRUCTION.** In `TenantStorageRegistryArchitectureTests`
the companion was routed through the *existing* platform-model helper rather than a second construction,
because ***a companion built from its own copy can drift into agreeing for the wrong reason.*** This is
`control-must-share-the-instrument` applied to a companion rather than to a control.

⚠ **IT ALSO APPLIES TO A POPULATION, NOT ONLY A PREDICATE** — and this is the newest member:
`AuthenticationMilestoneArchitectureTests` asserts the file walk still reaches `appsettings.json`. When the
walk was narrowed back to `*.cs` to measure a counterfactual, **that control failed FIRST, before the ban was
reached.** *A narrowing is caught by name instead of by the ban silently passing.*

### 3 — EXACT-LIST EQUALITY AGAINST A NON-EMPTY EXPECTED VALUE

`Assert.Equal([...names...], derived)`.

**Exemplars:** `CutoverManifestArchitectureTests` (**26** entity names, counted 2026-09-07);
`AuthenticationSessionArchitectureTests` (four approved bypass paths);
`EmployeeArchitectureTests.The_only_entity_hr_removes_from_the_database_is_the_department_manager`
(a list of one).

⚠⚠⚠ **THIS DOCUMENT FIRST SAID 35, AND IT DID NOT INVENT THE NUMBER — IT TRANSCRIBED IT FAITHFULLY FROM THE
TEST'S OWN COMMENT, WHICH WAS WRONG.** That comment read *"the runtime model contains all THIRTY-FIVE"* while
sitting **directly beneath a paragraph recording that this very comment once said "all TWENTY" above a list
of thirty-five and was stale by fifteen (269).**

***THE COMMENT DOCUMENTING THE STALE-COUNT DEFECT HAD ACQUIRED THE STALE-COUNT DEFECT*** — and a citation
then carried it into a second document, where it read as independently measured. **Naming a failure mode
confers no immunity on the paragraph that names it.** Corrected at source 2026-09-07; the test itself now
says: *"the list is the assertion and the list is the only thing that has ever been right — if you need the
number, count the literals."*

***ANTI-VACUOUS BY CONSTRUCTION, AND STRONGER THAN A FLOOR: IT PINS MEMBERSHIP WHERE A FLOOR PINS
CARDINALITY.*** A walk that finds nothing produces an empty array and the comparison fails. It reddens in
**both** directions — a new member grows the set, **and a member that stops qualifying shrinks it**, which is
the reverse bind an allow-list otherwise lacks.

⚠ `AuthenticationSessionArchitectureTests`' own record says so in those words: *"An audit listed it as a text
scan with neither a floor nor a plant. It turned out to need only the plant."*

### 4 — COUNT PAST THE FILTER ⚠⚠ THE ONLY FORM THAT ANSWERS THE FILTERING PROBLEM

`var examined = 0;` incremented **inside the loop, after the `continue`**, then asserted.

**Exemplar:** `TenantBackupSchedulerArchitectureTests.Phase_c_adds_no_restore_retention_or_deletion_capability`.
Its comment rejects a collection floor explicitly:

> *"`SchedulerTypes()` is a hard-coded pair of `typeof()`s and cannot go empty, **so a floor there would prove
> nothing** — but `DeclaredOnly` means a refactor that moved these methods onto a base class would leave the
> inner loop with nothing to inspect, and every assertion above would hold trivially."*

***THIS IS THE REMEDY FOR THE 56.*** A floor before the filter proves the source was non-empty; only a
counter after the filter proves anything survived it. **It also survives a refactor that changes the
collection's shape entirely, which a floor on the collection does not.**

---

## ⚠⚠ NOT A FORM — A VACUITY SHAPE THAT LOOKS LIKE A CONTROL

**An invariance comparison between two derivations of the same thing.**

`Assert.Equal(Describe(tenantA), Describe(tenantB))` — ***IF BOTH COME BACK EMPTY, BOTH DESCRIPTIONS ARE
EMPTY AND EQUAL, AND THE TEST PASSES.*** Found in `TenantStorageRegistryArchitectureTests`; the plant that
demonstrated it **was the defect itself** — making `Describe` a constant function left the equality green.

⚠ **IT IS DANGEROUS BECAUSE IT WEARS THE COSTUME OF A TWO-SIDED ASSERTION**: two operands, both derived,
neither hard-coded — the shape that is self-discriminating everywhere else. **It is the one member of that
shape that is not.** *Invariance between two copies of the same breakage is satisfied by the breakage.*

**Repair: keep the invariance, add form 2 (a companion proving the function CAN produce a difference) and a
faithfulness assertion naming a member the output must contain.**

---

## ⚠⚠ An exemption is a guard too — and its scope decides which way it fails

An allow-list is not one of the four forms; it is a hole cut in one, and it needs its own anti-vacuity
control. **`an-exemption-must-assert-its-grounds`, with two measurements from this tree:**

**SCOPE IT TO THE SMALLEST THING THAT IS ACTUALLY APPROVED.** `AuthenticationMilestoneArchitectureTests`
exempts a **snippet**, not a file. *Exempting the file would pre-approve every symmetric construct anyone
adds to it later.* ⚠⚠⚠ **AND THE SCOPING DECIDES THE FAILURE DIRECTION: because the entry must still match
real code to neutralise it, a rotted entry stops neutralising and THE BAN REDDENS. This allow-list fails
LOUD — the opposite of the usual allow-list hazard, and it falls straight out of scoping by snippet rather
than by file.**

**ASSERT THE ENTRY IS STILL TRUE.** An exemption whose text matches nothing neutralises nothing and is *an
approval with no subject, which reads exactly like a real one.*

⚠ **AND AN EMPTY ALLOW-LIST EXERCISES NOTHING.** `RouteConstraintArchitectureTests`'s validation test iterated
an empty `Allowed` and **had never once run in its existence** — in a file whose exemption grounds are
enforced by the tuple type, which is the strongest form here. *Quality suppresses verification: the
best-designed exemption in the suite had the un-run validator.* **A control fixture calling the extracted
predicate with a constructed bad entry settles it; a plant into the real list does not, because it proves it
once and ships nothing.**

## ⚠⚠⚠ A message is part of the guard, and "it fired once" is a property of the STRING

**Every form above eventually produces a failure message, and the message is the only part a stranger ever
reads. Three things measured 2026-09-07 across the messages written that night:**

⚠⚠⚠ ***"RENDERED" EXPIRES THE MOMENT THE TEXT IS REWRITTEN.*** Several messages watched firing that night
were **not the messages left in the files** — the HR floors rendered with their old thresholds, the docs
floor rendered before its rewrite, a population control rendered an hour before being replaced. **We had
been reasoning *"that one fired earlier"* as if the property attached to the ASSERTION. It attaches to the
STRING, and every rewrite resets it.** *So a session of rewrites ends with less verified message text than
it started with, while feeling like the opposite.*

⚠⚠ ***RENDERED IS NOT REVIEWED.*** One control's output was quoted in a report **as evidence the control
worked — which it was** — and nobody noticed the text named nothing. **A message read while checking
whether a control fired is being consumed as one bit.** *`control-must-consume-the-instrument` says an exit
code has one bit and you must consume the error TEXT; here the text WAS consumed, as an exit code.* **Read
it with the question *"would a stranger know what to do"*, or it has not been read.**

⚠ ***THE NEWEST IDEA GETS THE LEAST REVIEW.*** Of the messages added that night, exactly three were silent
forms — and **all three were the population controls invented that same session and praised in the reports
that introduced them.** *Novelty suppressed review harder than routine would have.*

**The checks, applied by reading, cost minutes:** does it print the OBSERVED value or the threshold · is it
one of the forms that accepts no message (`Assert.Contains` / `DoesNotContain(collection, predicate)` /
bare `True` / `NotEmpty`) · does it name the collapse or restate the condition. ⚠ **Reading is weaker than
seeing it render and does not close that gap — it establishes only that nobody shipped a defect visible on
the page.**

## How to choose

| the assertion… | reach for |
|---|---|
| enumerates a population and bans a property, no filter | **1**, floored per layer |
| filters the population before asserting | **4** — a source floor is worthless here |
| depends on reflection, a type name, or a config key | **2**, sharing the first derivation |
| can state its expected result exactly | **3** — strongest available; prefer it |
| compares two derivations for equality | ⚠ **not a control at all** — add 2 |

---

## ⚠⚠⚠ Three things this document cannot tell you

1. ***THE FORM COUNT IS 4 AFTER 20 FILES, AND NOTHING IN THE INSTRUMENT CAN BOUND HOW MANY REMAIN.*** Three
   forms were found in the first five files, chosen because they *looked* unprotected; the remaining fifteen
   produced none. **The discriminator: forms 1, 2 and 4 all reappeared in that unenriched remainder, so the
   sample kept its power to recognise a control and found no new kind.** That is the strongest available
   evidence for closure and it is not proof.
2. ***A CENSUS FOR THESE PRODUCES CANDIDATES, NEVER A COUNT.*** Widening the absence predicate moves the
   population up; widening the control predicate moves the at-risk set down. Both were moved once and the
   answer moved both ways. **On every file opened, the best control was one a five-form scanner could not
   see** — 4 for 4, on an enriched sample, which is the right basis for that negative and the wrong one for a
   rate.
3. ***A PLANT IS FOR REACH; AN ASSERTION SHOULD CARRY ITS OWN DISCRIMINATION.*** Plant when the thing
   unproven is the instrument's **own reach** — a copy of the walk run beside the guard is not the guard. Do
   not plant when the discrimination could ship inside the assertion instead, because then you have paid for
   evidence a file could have held permanently.

---

## Related rulings in the tree

- `tests/Architecture.Tests/ModelWalk.cs` — form 1, and the header that refuses a universal helper
- `tests/Architecture.Tests/AssertionMessageChoice.cs` — what a red must tell its reader, and why 156
  remaining silent sites are **not** a defect count
- `tests/Architecture.Tests/RouteConstraintArchitectureTests.cs` — an exemption whose grounds are enforced by
  the type, plus an inverted guard that fires when its own diagnosis expires
