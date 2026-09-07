# Four ways a guard proves it is not asserting nothing — and the separate question of whether its name is true

**Derived 2026-09-07 from reading 20 of the 66 corpus-walking `.cs` files in `tests/Architecture.Tests`.
Nothing here is a proposal; all four forms were already in use before this was written.**

⚠⚠ **THE DENOMINATOR WAS FIRST WRITTEN AS `20 of 99` AND THAT UNDERSTATED THE COVERAGE BY HALF.** Only 66 of
those 99 files walk a corpus at all; the other 33 **could not exhibit any of these forms under any
circumstances.** ***A DENOMINATOR THAT INCLUDES MEMBERS INCAPABLE OF THE PROPERTY IS NOT CONSERVATIVE — IT IS
WRONG, AND IT IS WRONG IN THE DIRECTION THAT LOOKS HUMBLE.*** *Measured: 30% of the population where the
shape can exist, not 20% of a set the question does not apply to.*

⚠⚠⚠ **AND THE SCOPE IS NOT THE REPOSITORY. `tests/Architecture.Tests` IS 724 TESTS OF 3,454.** Every sweep
behind this file covered that one suite. **Corpus-walking files per suite, measured 2026-09-07:**

    Architecture   66 of  99   ***67%***          HR            1 of 14    7.1%
    API             9 of 103     8.7%             Attendance    0 of 12    ***0%***
    Platform        4 of  88     4.5%             Payroll       0 of  8    ***0%***
                                                  Finance       0 of  5    ***0%***

***ARCHITECTURE IS AN ORDER OF MAGNITUDE ABOVE THE NEXT SUITE, AND THREE SUITES HAVE NO CORPUS WALK AND NO
FLOOR AT ALL — the anti-vacuity question does not arise in them.*** ⚠ **But the FORMS are not an artefact of
one suite's style: `Platform.Tests/CompanyDomainTests` uses form 3 and says so in its own words — *"the same
shape of criterion, thirty lines of a sibling file away. This is not a new design; it is a solved one that
had not travelled."*** *Every corpus walk in Platform was already controlled, by forms 2 and 3, before this
file existed.*

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
| **15 call sites across 5 files** | ⚠⚠ **MEASURED TWICE, AND THE SECOND MEASUREMENT PROVES LESS THAN IT LOOKS.** `git grep -c` counts matching LINES and is blind to two calls on one line; `grep -o` with comments stripped is not. They agreed — ***but no line in the corpus carries two calls, so the differing predicate COULD NOT HAVE DIFFERED HERE.*** **The blind spot had no occupant; the second instrument was never exercised.** *This is not luck, it is vacuity — `N-for-N means nothing if the falsifying shape was absent from the sample`, and it applies to a corroborating measurement exactly as it applies to a test.* |
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

## ⚠⚠⚠ A plant proves ONE assertion — the one that FAILED. Record which.

***`PLANT-BACKED` MEANS "THE METHOD WENT RED" AND READS AS "THIS METHOD IS PROVEN". THOSE ARE DIFFERENT
CLAIMS AND THE MARKER CANNOT TELL YOU WHICH.*** Measured 2026-09-07 across 11 plant records: **5 were
single-assertion and safe by construction; 6 were multi-assertion, and in 3 of those the plant reddened an
early assertion and the later ones NEVER EXECUTED.**

**AN ASSERTION HAS THREE STATES UNDER A PLANT, NOT TWO:**

| state | what it proves about that assertion |
|---|---|
| **never executed** — an earlier assertion failed first | ***nothing*** |
| ⚠ **executed and PASSED** | ***nothing about its detection*** |
| **executed and FAILED** | proven |

***"EXECUTED" IS NOT "EXERCISED".*** *A control whose first assertion fails proves that first assertion and
leaves every later one exactly as unproven as before the plant ran.*

**THE RECORD FORMAT, so the ambiguity cannot recur:**

> `PLANT: <what changed>. RED at <assertion>, <verbatim failure text>. UNREACHED: <assertions after it>.
> REVERT → GREEN.`

⚠⚠ **A CONTROL WRITTEN TO PROVE TWO THINGS USUALLY PROVES ONE.** *A tripwire's control was built to pin both
the comparison's direction AND the message's content; inverting the comparison failed the first assertion and
the three message checks never ran. **It took a SECOND plant — leaving the predicate correct and corrupting
the message — to reach them.*** **Two claims, two plants.**

⚠ **AND THE AXIS THAT PREDICTS WHICH RECORDS NEED THIS MOST** — from the corpus, not from us:
***AN ABSENCE GUARD CAN ONLY EVER BE SHOWN BY A PLANT, BECAUSE IT CANNOT FAIL WHILE THE ABSENCE HOLDS —
"which is forever, until the one day it matters". A BIND OVER A LIVE TYPE can fail for its stated reason on
any ordinary day.*** *The first kind's plant record is its only evidence; the second kind has other days.*

## ⚠⚠⚠ A FLOOR PLACED FIRST IS NEVER PROVEN BY THE PLANTS THAT PROVE THE ASSERTION BEHIND IT

***THE HOUSE ORDERING IS "FLOOR THE POPULATION, THEN ASSERT THE PROPERTY". THAT ORDERING MAKES THE FLOOR
EXECUTED-AND-PASSED BY EVERY PLANT OF THE THING IT FLOORS — AND EXECUTED-AND-PASSED PROVES NOTHING.***

*Measured: a guard whose route ban was proven by four separate plants has an anti-vacuity floor sitting above
it that **not one of those four plants exercised**, because each of them reddened the ban and the floor ran
clean on the way past.*

⚠ **So the floor and the ban need DIFFERENT plants, and only the ban's is ever written:** *the ban's plant
adds the forbidden thing; **the floor's plant must make the POPULATION COLLAPSE** — point the walk at a wrong
root, break the pattern, empty the source.* **Raising a floor temporarily until it fails is the cheap form,
and it also prints the population, which is the only way to know the matcher did not over-match.**

⚠⚠ ***THE GENERAL SHAPE: WHENEVER TWO ASSERTIONS IN ONE METHOD GUARD DIFFERENT FAILURES, ONE PLANT CANNOT
COVER BOTH — and the one that runs FIRST is the one that never gets its own.***

## ⚠⚠ A "STICKY PIN" IS NOT A DETECTOR — IT GUARDS AGAINST THE TEST'S OWN MAINTAINER

**A guard asserted an exact member set, then asserted four of those same members individually. The four are
LOGICALLY IMPLIED — if the set assertion passes they cannot fail — so *no change to the product can redden
them*, and a sweep for unfalsifiable assertions flags them.**

***THEY ARE STILL CORRECT, AND THE FILE SAYS WHY: "a comment saying 'Term is forbidden' rots the day someone
renames it; this fails."*** ⚠ **The exact-set list is DESIGNED TO BE EDITED when members legitimately change;
the individual pins are designed NOT to be — so a rename dutifully applied to the first is caught by the
second.** *The subject is the maintainer of the test, not the product.*

**Do not "fix" these and do not count them as vacuous.** ***ASK WHAT A CHANGE WOULD HAVE TO LOOK LIKE TO
BREAK THE ASSERTION: if the answer is "an edit to this test file", it is a sticky pin and it is doing its
job.***

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
- `tests/Architecture.Tests/AssertionMessageChoice.cs` — what a red must tell its reader, and why the
  remaining silent sites are **not** a defect count. ⚠⚠⚠ **THIS DOCUMENT PREVIOUSLY CITED `156` AND
  "tier 1 is zero". BOTH WERE WRONG, CORRECTED 2026-09-07 TO *251 SITES AND AT LEAST 14 TIER-1*.** The
  census counted `Assert.DoesNotContain(collection, predicate)` only — ***and `Assert.Contains(collection,
  predicate)` IS EQUALLY SILENT, a fact recorded verbatim in that same file three paragraphs above the
  census that ignored it.*** ⚠ **14 is a FLOOR:** both instruments classify by the **call-site expression**,
  so a walk bound to a local first is invisible to them — one was caught only because its variable name
  happened to contain a matched substring. ***BOTH BLIND SPOTS UNDER-REPORT, SO EVERY "TIER 1 IS ZERO"
  PRODUCED BY THAT INSTRUMENT LEANED THE SAME WAY.***
  **It was found by giving a zero a positive control** — running the census against a file known to contain
  tier-1 sites, which returned **13 in one file against a reported 9 for the whole suite.** *The control did
  not validate the zero; it falsified the instrument.*
- `tests/Architecture.Tests/RouteConstraintArchitectureTests.cs` — an exemption whose grounds are enforced by
  the type, plus an inverted guard that fires when its own diagnosis expires
