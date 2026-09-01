---
id: ENG-ARCH-001
title: Architecture Principles
category: Engineering Standards
version: 1.0
status: Approved
owner: Solution Architecture Team
---

# Architecture Principles

## Purpose

This document defines the mandatory architectural principles governing the design and implementation of SSAS ERP V2.

Unlike Architecture Decision Records (ADRs), which explain *why* architectural decisions were made, this document specifies *how* all developers and AI coding agents must implement the system consistently.

These principles are mandatory for every module, service, and feature.

---

# Guiding Principles

The architecture shall prioritize:

- Maintainability
- Scalability
- Modularity
- Security
- Testability
- Performance
- Simplicity
- Consistency
- AI-assisted development

---

# Principle 1 – Modular Monolith

The application shall be implemented as a Modular Monolith.

Modules communicate through well-defined contracts and shall not access each other's internal implementation.

Reference: ADR-001

---

# Principle 2 – Clean Architecture

Dependencies always point inward.

```
Presentation
    ↓
Application
    ↓
Domain
    ↑
Infrastructure
```

The Domain layer must not depend on any external framework.

Reference: ADR-003

---

# Principle 3 – CQRS

Commands modify state.

Queries return data.

A request shall never perform both responsibilities simultaneously.

Reference: ADR-004

---

# Principle 4 – Multi-Tenant by Default

Every business operation executes within a tenant context.

Every tenant-owned entity shall include `TenantId`.

Cross-tenant access is prohibited unless explicitly approved.

Reference: ADR-005

---

# Principle 5 – Security First

Authentication uses JWT Bearer Tokens.

Authorization is claims- and permission-based.

Never trust client-provided identity or authorization data.

Reference: ADR-006

---

# Principle 6 – Angular Frontend

All web user interfaces shall use Angular.

Business logic belongs in backend services, not Angular components.

Reference: ADR-007

---

# Principle 7 – Entity Framework Core

EF Core is the standard ORM.

Use Fluent API configuration.

Use migrations for schema evolution.

Avoid Lazy Loading.

Reference: ADR-008

---

# Principle 8 – Domain Events

Modules communicate through Domain Events.

Events represent completed business facts.

Publish events only after successful transaction commits.

Reference: ADR-009

---

# Principle 9 – Repository Pattern

Repositories encapsulate persistence for aggregate roots.

Business logic shall never exist inside repositories.

Reference: ADR-010

---

# Principle 10 – Unit of Work

Every request executes within a single Unit of Work.

Transactions are committed once.

Domain Events are published after a successful commit.

Reference: ADR-011

---

# Principle 11 – Branch-Scoped Execution Context

Tenant ownership answers *whose data is this*. Branch ownership answers *which operating location inside that tenant produced it*. They are independent dimensions.

Every tenant entity shall be **explicitly classified** as tenant-global or branch-owned. There is no default, and unclassified is a defect: an entity that should have been branch-scoped and was not is readable by every branch in the tenant, and nothing about it looks wrong.

Branch-owned entities shall implement `IBranchOwnedEntity` in addition to `ITenantOwnedEntity`, and carry both `TenantId` and `BranchId`.

`BranchId` shall be assigned by the server from the authenticated execution context. It shall never be accepted from a request DTO, header, form field, or token claim, and shall never change after the record is created.

Branch authorization shall be re-evaluated against live state on every branch-owned write and shall fail closed.

Branch-scoped queries shall carry an explicit `BranchId` predicate over the current branch or an authorized branch set. Omitting the predicate is a defect, not an optimization.

Reference: ADR-023

---

# General Rules

Developers and AI coding agents shall:

- Keep controllers thin.
- Keep business logic in Application and Domain layers.
- Never bypass tenant isolation.
- Never bypass branch scoping on branch-owned data.
- Never accept `BranchId` from client-supplied request data.
- Never inject DbContext into controllers.
- Never expose entities directly through APIs.
- Always use DTOs.
- Use asynchronous APIs where supported.
- Prefer constructor injection.
- Keep classes focused and cohesive.

---

# Naming Conventions

- One class per file.
- One public type per file.
- PascalCase for types.
- camelCase for local variables.
- Feature-first folder structure.
- One repository per aggregate.
- One handler per command or query.

---

# Dependency Rules

Allowed dependencies:

- Presentation → Application
- Application → Domain
- Infrastructure → Application
- Infrastructure → Domain

Forbidden dependencies:

- Domain → Infrastructure
- Domain → Presentation
- Application → Presentation

---

## Composition Root Exception

SSAS.Host.API may reference approved Platform, HR, and GL API and Infrastructure projects solely for dependency injection, configuration, middleware coordination, and endpoint mapping. This exception does not permit business logic in the Host. Module API projects must not reference Infrastructure. Module Application and Domain projects must not reference Host or Infrastructure. Cross-module business communication must use approved public contracts, integration events, or explicitly authorized module-facing abstractions. Direct references to another module's internal Domain, Application, API, or Infrastructure assemblies are forbidden.

---

# AI Coding Guidelines

AI coding agents shall:

- Follow all ADRs.
- Never introduce architectural shortcuts.
- Preserve module boundaries.
- Reuse shared abstractions.
- Generate tests for new functionality.
- Document public APIs.
- Respect coding standards.
- Avoid duplicate implementations.

---

# Code Review Checklist

Every pull request should verify:

- Architecture compliance.
- Tenant isolation.
- Branch classification of every new tenant entity.
- Authorization checks.
- Unit tests.
- Naming conventions.
- Logging.
- Error handling.
- Performance considerations.
- Documentation updates.

---

# Exceptions

Any deviation from these principles requires approval from the Solution Architect and, where appropriate, a new ADR documenting the change.

---

# Principle 12 – Malformed Input Is a 400, Including in the Route Path

**A syntactically invalid value returns `400 Bad Request` with a problem document, wherever it arrives** —
route path, query string, header, body or rowversion.

**Route paths were the exception until 2026-08-30.** 71 route parameters carried type constraints such as
`{id:guid}`. A constraint is evaluated during route *matching*, so a malformed value matched no route and
the framework answered **404 before any module code ran**. The constraints have been removed; the value now
reaches parameter binding, fails to bind, and produces a 400 naming the offending parameter.

**The reason is not consistency for its own sake. A 404 makes a malformed identifier indistinguishable from
an absent record** — a caller cannot separate *"your GUID is not a GUID"* from *"that record is gone"*. A
400 with a problem document can say which. This mattered on 71 routes, permanently, and cost nothing to fix
while the product has no external consumers.

**Enforced, not merely stated:** `RouteConstraintArchitectureTests` reddens when a constrained route
parameter is reintroduced, and product-wide the count is **0**. **An exception requires an allowlist entry
carrying a stated reason** — a route constraint is legitimate where it disambiguates two sibling parameter
routes, and the check that establishes this is a measurement, not an assumption.

# Principle 13 – When the Exception Type Is Already the Reason

**A discarded exception normally needs a stated reason. It does not need one when the type at a parse
boundary IS the reason.**

`FormatException`, `JsonException`, `BadHttpRequestException` and `DecoderFallbackException`, caught at a
parsing or deserialisation boundary and answered with `false` or a 400, are self-describing. **A comment
there restates the catch**, and comments that restate their code teach readers to skip comments — including
in the places where a real reason is the only thing standing between a maintainer and a silent failure.

**This exemption is narrow and does not extend by analogy.** It covers a parse boundary where the type
names the failure and the response is a refusal. It does **not** cover:

- **a broad `Exception` catch**, where distinct causes collapse into one observable — the same defect
  Principle 12 removes from the routing layer;
- **a persistence exception**, where the inner SQL error carries the difference between a unique violation
  and a deadlock. **This codebase preserves that difference** with a `when` filter on the inner
  `SqlException` number, and only one of the two can succeed on retry — so a catch that discards it is
  discarding a retry decision, not merely a message;
- **a teardown or cleanup catch**, where the reason is *"this must not fail the test"* and the cost of
  omitting it is a fixture that starts failing for a new reason and says nothing;
- **code that looks careless and is not** — where the generic message is deliberate, the reason is
  required, because the reader's default inference is wrong.

**Measured 2026-08-30. The figure is 212 catch clauses — 114 in `src/`, 98 in `tests/` — of which 94 discard
the exception and 16 discard it with no stated reason, all 16 in `src/`.** Those 16 are the two groups above:
11 parse boundaries where the type is the reason, and 5 persistence mappings. `tests/` is at zero unreasoned
and holds all 21 completely-empty bodies; `src/` has none, which Principle 14's guard keeps true.

**⚠ Two earlier figures were published and both were wrong, and the sequence is the lesson.** A first census
reported **207**. A correction reported **291**. The truth is **212** — so the *correction* was 37% high while
the original was within 2.5%.

**The original was nearly right by CANCELLATION, not by correctness.** It made two errors in opposite
directions: it **missed** clauses — `when (F(x) is { } y)` nests parentheses a `\([^)]*\)` capture cannot
span, and a `#pragma` between the declaration and its brace defeats `\s*\{` — while also **counting prose**,
matching the word `catch` inside comments and string literals. The two offsets happened to be similar in size.

**⚠ THE RULE: A CORRECTION THAT FIXES ONE OF TWO OFFSETTING ERRORS LEAVES THE NUMBER WORSE THAN BOTH ERRORS
TOGETHER.** The second pass fixed the under-count and not the over-count, breaking the cancellation. **A
result that improves when a defect is fixed is expected; a result that gets worse means another defect was
holding it up — and that is a reason to keep looking, not to revert.**

**One shape holds regardless of totals: a substantial minority of reasons are written above the enclosing
`try`, or above a chain's first arm, rather than above each `catch`.** Both are legitimate and common here.

**⚠ Do not re-derive these counts with the classifier that produced them.** Its window was wrong four times
in two sittings — too narrow, then too wide, then too narrow again, and once measuring on a normalised
buffer. **The 16 is trustworthy because the residue was checked by hand, not because the heuristic is sound**,
and a later run cannot tell those apart. Treat it as a dated observation, not a repeatable measurement.

## Diagnosing a persistence conflict — where the failure is actually recorded

**The application logs nothing of its own for a failed save.** Neither unit of work takes a logger, and
`Error(Code, Message)` cannot carry the `SqlException` that names the index. **The operator is nevertheless
not blind: EF Core records it.**

**Measured 2026-08-30 against a real server and a real 2627 on the working-calendar unique index:** exactly
**one** `Error`-level entry, with the exception attached, under category
**`Microsoft.EntityFrameworkCore.Update`**. `CorrelationIdMiddleware` plus `"Enrich": ["FromLogContext"]`
ties it to the originating request.

**⚠ The category is `…EntityFrameworkCore.Update`, NOT `…EntityFrameworkCore.Database.Command`.** The
`Database.Command` category — the intuitive guess, and the one predicted from EF's `CommandError` event —
**logs nothing at `Error` on this path.** Both clear the `"Microsoft": "Warning"` override, so the practical
answer is the same either way; **but the category is what an operator filters on, and a runbook naming
`Database.Command` would return nothing and read as "no such failure ever happened".**

# Principle 14 – A Guard Must Be Refusable

**Before adding a guard, establish that the thing it forbids currently occurs, or has occurred.** A guard
over a set that has always been empty asserts only that its own scan still runs. It is indistinguishable
from a working guard, it goes green forever, and it consumes the attention a real check would have earned.

**Worked example, 2026-08-30.** A rule was specified as *"count bare `catch` with an empty body in `src/`,
and if it reaches zero, assert zero"*. `git log -S` showed the construct **had never appeared in `src/` in
the repository's history**. The rule was replaced with one asserting that no catch in `src/` has a
completely empty body — **and `tests/` contains 23 such bodies, which is what makes the `src/` rule
meaningful: the codebase writes that pattern fluently where it is correct, so the assertion can be
refused.** A rule nobody could break is not enforcement.

**Two supporting practices.** **Pin the boundary in both directions** — the rule above is *"said nothing at
all"*, not *"has no statement"*, so a comment-only body passes; that was fixed by a plant proving a
comment-only body passes and a plant proving the same body reddens once the comment is removed. **And a new
guard should find something.** The one above found `TenantCutoverRoutingFlipService.SafeRollbackAsync`,
whose reason lived on a `#pragma warning disable` line where no reader looks. A guard that finds nothing on
its first run has not yet been shown to work.


## ⚠ The converse, 2026-08-31 — ask whether the failure is CONSTRUCTIBLE before demanding a guard

**A guard must be refusable. The corollary is that some invariants must NOT be guarded, because nothing
can break them from outside.**

**The case.** A filtered unique index over `AccountActionToken [Purpose, TenantId?, TenantUserId?]` filters
on `[TenantUserId] IS NOT NULL` and never mentions `TenantId?`. **Read alone that is the defect shape.** It
is correct because the aggregate admits exactly two bindings — Invitation with both set, PasswordReset with
neither. **The instruction given was: establish whether a test pins that invariant, and if nothing does,
pin it.**

⚠ **Nothing pins it, and nothing should.** The type does not defend the rule at run time — **it makes the
violation unconstructible.** There are exactly two public factories: one takes `Guid tenantId, long
tenantUserId` — **non-nullable value types, both mandatory** — and the other takes neither. No public
rehydrate, restore or update; the remaining constructors are private. **A test asserting *a mixed binding
throws* cannot be written without reflection, because no caller can produce the input.**

**A test would assert a runtime rejection. The signatures make the input impossible.** ⚠ **Writing that
test would create a check that cannot fail for the reason it claims to test — the thing this principle
exists to ban, arriving through an instruction to add a guard.**

**So the question to ask first is not *is this pinned?* but *can this be broken from outside?***

- **If yes — guard it, refusably.**
- **If no — say so, and identify what a change to the impossibility would look like.** Here it is widening
  a parameter to a nullable type: **a deliberate signature edit the compiler forces someone to write out.**

⚠ **And when the dependent thing cannot be made to redden, guard the DEPENDENCY.** The index cannot check
its own correctness. **A reflection test asserting those two parameters are non-nullable value types can** —
no parsing, no exception list, and it reddens exactly when the property the index rests on is weakened.
**Guard the thing the dependency rests on**, the same move as *guard the escape, not the query*.

**Residual, and worth stating whenever a type-system argument is used:** the ORM materialises through a
private parameterless constructor and private setters, **so a row already in the database with an invalid
binding would load without complaint.** ⚠ **The guarantee is a property of the WRITE PATH, not of the
TABLE** — sound while the write path is the only writer, and live the day an import or a second writer is
added.

# Principle 15 – Locating and Measuring Must Not Share a Text

**A source-scanning check does two different things: it LOCATES a construct, and it MEASURES a property of
what it found. Locating needs a normalised text; measuring needs the original. They must not run over the
same buffer.**

**Locating a C# `catch` requires blanking comments and string literals**, or the word matches inside them —
an early version inflated a clause count by roughly 25% that way, producing no false failure but quietly
weakening the floor the count existed to support.

**Then measuring emptiness on that same blanked text inverts the rule.** A comment-only body is blank once
comments are blanked, **so precisely the catches the rule permits became the ones it flagged.** The
normalisation that makes locating correct destroys the property being measured.

**The general form: any normalisation applied in order to FIND a thing is a distortion OF the thing found.**
Locate on the normalised text, record positions, then measure the original at those positions.

**And treat a text-scanning guard's own correctness as the thing needing proof.** Five defects were found in
such guards over two sittings — a `when` filter's property pattern read as a body, a nested-parenthesis
capture undercounting by a quarter, a `#pragma` between declaration and brace, a CRLF-blind literal matching
a file boundary, and the normalisation error above. **None was carelessness: a text scanner simply has more
ways to be wrong than the property it checks.** Prefer a compiler-enforced or structural check wherever one
exists, and plant every text-based guard against a real instance before trusting it.

# Principle 16 – A Green Guard Produces No Prompt to Ask Whether It Is Measuring Anything

**Measured 2026-08-30.** `PersistenceArchitectureTests` passed **all nine of its tests when its file walk
found nothing.** Renaming one path segment — a plausible layout change — was enough. Three of the nine are
`Assert.Empty` over that walk with no floor: *no generic repository in production source*, *no Entity
Framework in Domain or Application*, *no `IQueryable` on Application boundaries*. **Three real architectural
rules, defended by an instrument that reports success when it inspects an empty set.**

**The population, across this repository's guards that match against file text:**

| | |
|---|---|
| guards matching file text | **18** |
| with an anti-vacuity floor | 14 |
| with recorded plant evidence | 5 |
| asserting an ABSENCE | **12** |
| absence-asserting **and** no recorded plant | **12 — all of them** |

**The combination that matters is absence-asserting, text-scanning and unplanted**, because such a guard
reports zero violations both when the rule holds and when the instrument is broken, and those two outputs
are identical.

**Why the class accumulates has nothing to do with difficulty.** Demonstrating the failure took under a
minute and no cleverness: change what the walk looks for, confirm the suite stays green. **It accumulates
because a passing test asks nothing of anyone.** A failure interrogates itself; a success is
self-certifying — which is why this class collects in the tests written by the most careful people, whose
guards pass from the day they are written.

**Four rules follow.**

**Ask a structural question structurally.** *"Domain and Application remain EF-free"* is an
**assembly-reference** question and *"no `IQueryable` on Application boundaries"* is a question about
**public method signatures**. Reflection answers both exactly, and **an assembly that fails to load throws,
where a file walk that finds nothing returns an empty set and reads as success.**

**⚠ But conversion removes one failure mode; it does not remove the need to show the replacement can fail.
A reflection test that finds nothing is exactly as unfalsifiable as a file walk that finds nothing.** The
conversion above was accepted only with two plants proving the replacements read what they claim to: the
reference prefix pointed at `System.Runtime`, and the `IQueryable` signature walk widened to `IEnumerable`.
**⚠ And the rule is not really about absence. The question is whether a test has ever been observed to fail
for the reason it claims to test.** A *presence*-asserting check measured on 2026-08-30 — *"at least one
Error-level log entry exists"* — needed the plant just as badly, because its failure mode was not *finds
nothing and passes* but **passes without running at all.** Absence-asserting checks are the largest
population of this hazard, not the whole of it. **Every check earns a plant, whatever it asserts and however
it is implemented.**

**⚠ And every plant must COMPILE. A plant that breaks the build tests nothing and looks like evidence.** On
2026-08-30 a plant added a **required** parameter; the build failed, the runner executed a **stale
assembly**, and the test passed — **a green certifying the previous binary**, about to be written down as
proof the guard worked. **That is worse than an unplanted guard, because it produces a recorded claim of
verification.** Check the build result before reading the test result, and prefer a break that cannot fail
to compile: an optional parameter, a changed literal, a widened pattern, a renamed path segment.

**⚠ And the same rule applies to a PROBE, where it is harder to see: a mutation that never landed is void,
and unlike a broken build it announces nothing.** On 2026-08-30 a probe replaced `GetTypes()` with an empty
enumeration across a set of files to find guards that pass over zero types. **One file contained no
`GetTypes()` at all**, so the edit matched nothing, exited zero, changed nothing — **and the suite passed,
which the probe recorded as "stays green over zero types".** True, meaningless, and identical in the output
to a real finding.

**A broken build shouts. A `sed` that matches nothing is silent.** So a mutation step must **assert it
changed something before the suite runs** — `git diff --quiet <file>` should *fail* after the edit, and if
it succeeds the probe is void and must report that instead of a result. **Three separate defects in one
session came down to the same unasked question: did the mutation actually land?**

**A cheap tell, short of a plant: does the clock agree the work happened?** That same test passed on its
first run reporting `Duration: < 1 ms` **for something that creates a database.** The timing was the only
thing out of place, and inverting the assertion proved the run was real. **A green whose duration does not
match the work it claims to have done is worth breaking on purpose** — it costs a minute and needs no second
implementation.

**⚠ The vulnerable shape is `Assert.Empty`, not "scanning text". An assertion satisfied by an empty input is
the hazard, whatever produced the input.** Three shapes are anti-vacuous by construction and need no floor:
a comparison against a **non-empty expected list** (`Assert.Equal(ApprovedFiles, actual)` fails outright when
the walk returns nothing), an enumeration over a **fixed type array** the compiler keeps populated, and a
**reflection call that throws** rather than returning empty. **Establishing which checks are already safe is
as much of the answer as finding the broken ones** — of four guards flagged as unprotected on 2026-08-30,
**two needed nothing**, and adding floors to them would have been ceremony.

**⚠ FLOOR THE QUANTITY THE ASSERTION ACTUALLY READS.** Not the files enumerated, not the types loaded — **the
number the failing assertion is computed from.** `Assert.True(routes >= 120)` where `routes` is what the
regex matched; `Assert.True(clauses >= 70)` where `clauses` is what the token search found.

**This supersedes an earlier formulation of "floor the post-filter set", and it is strictly better.**
Post-filter flooring requires you to *identify the filters*, and the identification is the step that goes
wrong. **Flooring what the assertion reads requires nothing: that quantity is downstream of every filter by
construction, so filter coverage comes free.** It is also what you naturally write if you ask the right
question — *could this assertion have meant anything?*

**The cheap test for any floor: if the floored quantity is not the one the failing assertion reads, it is in
the wrong place.** A root cannot vanish silently and a filter can — pointing a walk at a missing directory
throws, while a renamed segment empties a predicate with every directory still present — **but you do not
need to reason about which is which if the floor sits on the asserted quantity.**

**Measured 2026-08-30: five guards floored this way all redden when their real filter is broken** — a
namespace fragment, a file-extension predicate, a route-matching regex, a token search. **The six guards that
failed the same probe had no floor at all, so there was nothing for a filter to be downstream of.**

**Prefer a CROSS-CHECK to a floor wherever a second derivation of the set exists.** A floor catches the walk
collapsing; it **cannot catch one item dropping out** — eleven assemblies clear a floor of eight while the
twelfth goes unexamined. The guard above instead compares its assembly set against an independently derived
one, the `SSAS.*.Domain` and `SSAS.*.Application` directories under `src/`. **Two routes to the same set
disagreeing is the signal; either route alone can be silently short.** Where no second derivation exists, a
floor is the fallback, not the goal.

**Record plant evidence in the test file, not only in the commit message.** Three of this repository's five
recorded plants were visible **only in git history**. A property that can be established only by archaeology
stops being established — the next reader sees a green assertion and has no reason to trust it. **A short
comment naming what was broken and what reddened is greppable, survives a rebase, and is present at the
moment someone decides whether to believe the test.**

# Principle 16b – A Measurement Can Be True and License Nothing

**The test of a measurement is not whether the number is right. It is whether it answers the question the
risk actually poses.**

**Worked example, 2026-08-30.** Before exposing internal error messages in API responses, the safety case
rested on a careful measurement: **no message anywhere in `src/` carries a runtime value — zero
interpolations, zero concatenations, zero variables.** The measurement was correct, was re-run after the
intervening edits, and held both times.

**It licensed nothing.** The danger was never data interpolated into a message. It was a **hand-written
constant describing our own infrastructure** — *"no route to the tenant database"* — which is entirely
static and still a leak. An existing test caught it: a real storage failure was reaching an authorization
refusal and taking that sentence with it. **"Contains no runtime value" and "safe to disclose" are different
properties, and only the first had been measured.**

**The remedy divided by AUDIENCE rather than by status code.** A 4xx is addressed to **the caller** and says
what they did wrong. A 5xx says **something broke on our side**: that message is addressed to an operator,
who already has it through the log and the correlation id, and the response body is not its delivery route.
**Reasoning from audience survives a new status code appearing; a rule reading "not 401 or 403" would not.**

**⚠ And it is written as an ALLOWLIST over 4xx, not a blocklist of 401/403/5xx.** A blocklist admits **the
status class nobody thought about**; an allowlist refuses it. 502 and 504 are closed without anyone having
declared them. **This is the same fail-closed argument as an opt-in default, applied to the category rather
than the member.**

**Before relying on a measurement, state the risk in one sentence and check that the measurement is about
that sentence.** A number taken carefully, about the mechanism you happened to imagine, is the most
persuasive way to be wrong.

# Principle 16d – Controls Are Not Interchangeable, and a Floor Over a Union Is Half Blind

**⚠ A FLOOR OVER A UNION CANNOT SEE ONE OF ITS MEMBERS COLLAPSE.** Measured 2026-08-30: a guard floored
`fields.Concat(properties)` as a single number. **Breaking the field walk left the test green** — the
property walk cleared the floor by itself — **so an offender held in a field would have gone undetected
while the test reported success.** Floor each member separately. The plant is the only thing that found it,
**and the defect was in the fix written an hour earlier for exactly this class.**

**The control must match the SHAPE of what it protects:**

| the guard | its control |
|---|---|
| a **regex matcher** | `Assert.Matches` on strings it must match, **plus a negative** so the pattern is not merely `.*` |
| a **name test** | run the same `Name.Contains` for a term that must be present in the same collection |
| a **type comparison** | floor the **enumerated members**, not the types walked |
| a **reflected property** | **construct** a known positive and assert the filter selects it |

**⚠ A guard over a legitimately empty set cannot borrow its control from the product.** No declared error code
opts into detail, so the only way to prove that predicate is still read was to **build** an
`ApiError(403, …, DetailAllowed: true)` and assert it is selected. **A known positive that does not exist must
be constructed — and a guard whose subject is legitimately absent is the one most likely to have no control
at all.**

**⚠ AND ONE DEFECT CAN HIDE ANOTHER: FIXING THE FIRST IS WHAT MAKES THE SECOND OBSERVABLE.** A ban's pattern
carried a `\b` that could not match `JwtSecurityTokenHandler` — **the canonical type of the family it
existed to exclude.** Widening it immediately produced a false red on a file whose comment **explains at
length why it must not take that dependency**: the scan did not strip comments, and **the hole had been
hiding the prose-blindness behind it**, because the old pattern could not match that name anywhere at all.
**A fix that surfaces a new failure is evidence the fix worked, not that it broke something.**

**And when a new control fails on its first run, the prior is that the CONTROL is right.** It was written
against the requirement; the pattern was written against whatever its author had in mind that day.


## ⚠ Second instance, 2026-08-31 — and the better question is not whether the list is floored

Fourteen files in `tests/` build an assembly, project or module list **by hand.** Asking *"is it floored?"*
sorted them badly. ⚠ **THE QUESTION THAT SORTS THEM IS: WOULD A NEW MODULE BE MISSING FROM THIS LIST?**
Most are single-area by subject, or derive their population from types rather than a named array. **Three
are module-shaped** — per-module API assemblies, per-module enablement types, per-module Application and
Infrastructure assemblies — **and all three carried floors over their TYPE populations while none was
cross-checked against the assembly set.**

⚠ **A floor cannot catch this, and that is the whole point.** The read-side guard's floor counts across the
**whole union**, so a new module contributing three unscanned services **still clears 20.** **A FLOOR OVER A
UNION CANNOT SEE ONE MEMBER OF THE UNION COLLAPSE** — it is not merely half blind, it is blind in exactly
the direction the product grows.

**The known positive was real rather than planted, which is the strongest kind.** A control comparing the
scanned list against the assemblies the build actually ships **failed on its first run**, naming two
assemblies the guard had never scanned. Its author's account: *"I wrote that list by naming the five
modules I was thinking about"* — **the failure the item existed to close, found in the guard that named
it.**

⚠ **And the source of truth matters more than the comparison.** The check reads the **build output
directory**, not `GetReferencedAssemblies()`. **The compiler omits a reference whose types are never used**,
so a project referencing a new module but touching none of its types would report it missing — **and the
check would agree with the stale list for the wrong reason.** A control that can be satisfied by the same
defect it tests for is worse than none.


## ⚠ Third instance, 2026-08-31 — a comparison between two hand-maintained sets is blind to anything added to both

The two guards that already existed over the module lists compare a **named list** against a **declared
set**. They fire when a module is **removed** from one and still named by the other. ⚠ **A module ADDED
moves BOTH SIDES OF THE COMPARISON TOGETHER, and is invisible to them.** Union collapse again — this time
not inside one union but **between two lists that grow and shrink in step.**

⚠ **And the fix does not make the hand-written lists redundant, which is the part worth keeping.** Naming a
module explicitly **catches a module MOVING, where a scan would pass vacuously.** Naming **cannot catch a
module ADDED.** **Two directions, two mechanisms, and neither substitutes for the other** — the instinct to
replace the list with the scan would have traded one blind spot for the opposite one.

**What closed it was a predicate the filesystem already enforces:** a module is a project under
`src/Modules/`, read from the layout at test time, so a new module appears **the moment its directory
exists, with nobody deciding it is one.** The non-modules are **siblings of that directory, not entries in
an exclusion list.** ⚠ **When a control needs a convention, look for one the repository layout already
commits to before writing one down.**

⚠ **And the trap inside that fix, which is the general lesson: THE DIRECTORY NAME IS NOT THE ASSEMBLY
PREFIX.** `src/Modules/Finance/` holds the `SSAS.GL.*` projects. **A predicate keyed on the module folder
name would have reported `SSAS.GL.API` as not-a-module and PASSED WHILE MISSING A MODULE** — the exact
failure the control exists to catch, **arriving through the control built to catch it.** Read the project
directory names, not the group directory.

# Principle 16e – A Load-Bearing Control Must Say What It Holds Up

**A control can exist, work, and still be one refactor from deletion — because nothing records that anything
depends on it.**

**Measured 2026-08-30.** `UnicodeStringPersistenceArchitectureTests` carries two bans:
`Assert.Empty(NonUnicodeStringColumns(PlatformModel()))` and the same for the tenant model, over a walk with
**three filters** — `GetEntityTypes().SelectMany(GetProperties).Where(IsStringProperty).Where(IsNonUnicode)`.
Read alone, nothing establishes any of those still yields, and each of a failed model, an `IsStringProperty`
that stopped recognising the CLR type, and an `IsNonUnicode` that stopped reading the store type is **green**.

**They are in fact guarded.** A third test runs the same walk with exemptions off and asserts that every one
of the seven acknowledged columns is found — **a known-positive control, and a good one.**

**⚠ But the coupling is invisible from both ends. The bans do not mention the control; the control does not
mention the bans.** It reads as a test of the exemption list. **Delete it as redundant and the other two
silently become unfatalsifiable** — no failure, no signal, two assertions that can no longer fail.

**This is a distinct category from a missing control, and it fails twice over:** it loses protection during a
refactor, **and it produces false negatives in an audit** — a reviewer reading only the two bans reports the
file as unprotected.

**The remedy is a sentence at each end, and the name of the control is the part that carries it.** Name the
test for the relationship — *"…still selects real columns, which is what makes the two bans meaningful"* —
because **the name is what a deleter reads, it appears in runner output, it survives comment-stripping, and
it cannot be skimmed past the way a file header can.**

**⚠ And this qualifies Principle 16's usual scepticism about notes.** A note that must be RECALLED later does
not prevent anything. **A note at the point of decision does** — the person who would delete that test is
looking directly at it when they decide. **The distinction is whether the reader must remember the note or
merely read what is in front of them.**

# Principle 16c – Validate an Instrument Against Its Known Positive Before Trusting Its Negatives

**An instrument that cannot flag its own known positive reports absences it has not earned.**

**Worked example, 2026-08-30.** A scan was written to generalise a defect already found: an error message
naming one domain while being returned for every module. Its first predicate asked *"is this raised from a
file outside the module that declares it"* — and against the known case that worked **only by accident.**
The error is declared in Platform and raised in Platform; **it crossed a module boundary in exactly one
incidental place.** Remove that one crossing and **the instrument built to generalise the finding could not
have found the finding.**

**The diagnosis is the transferable half: the property was never the DIRECTORY, it was REACH.** Ten mappers
in ten modules translate that code, so one message explains it to callers of all ten. **The directory was a
proxy that correlated once.**

**So before believing a scan's empty result, feed it the case you already know.** It is the analysis-time
form of the known-positive control a guard carries: **a guard's control proves the matcher still matches; a
scan's known positive proves it could have matched anything at all.**

**⚠ And regex instruments over structured code over-report — treat that as structural, not as luck.** Four
separate instruments on 2026-08-30 produced four over-reports and no under-reports: a lambda's brace read as
a method body's end, a comparison read as a raise, one constant name unioning five distinct codes, and prose
in comments counted as call sites. **The mechanism: you write the pattern from a known positive, so it
matches that one by construction, and every error is in what ELSE it matches. Under-reporting would require
the pattern to be too narrow for the very case you checked.** The corollary: **a young regex instrument
returning zero is the result to trust least.**

**And scope an absence rather than implying one.** *"Only codes with a mapper arm — 348 of 521 declarations
— only literal declarations, a 21-word noun list"* is evidence. **The same empty result without its boundary
is a claim about the whole codebase that nobody made deliberately.**

# Principle 16f – A Criterion Met by Universal Absence Is Not Evidence of the Thing It Protects

Principle 16 asks whether a *guard* has ever been observed to fail. **The same question applies to an
acceptance criterion, and it fails in a way that is harder to see, because a criterion has no test run to
inspect — only a status.**

`AC-SUB-0008` requires that **no tenant-plane subscription permission exist**. It is satisfied. ⚠ **It is
satisfied because the package defines no subscription permissions on EITHER plane** — all 28 platform
permission names were enumerated and none is one. The criterion exists to protect a *plane separation*; it
is currently met by there being nothing to separate. Its sibling `AC-SUB-0045`, which speaks of *"this
package's six"* permissions, **is not satisfiable at all**, because those six do not exist.

**A status table would show the first green and say nothing useful, and the second would have to be either
green, red, or absent — and all three would be wrong.** The honest report is a fourth state.

**How to apply:** for any criterion phrased as *"no X exists"* or *"X and Y are distinct"*, ask **whether
X exists at all**. If it does not, the criterion is *vacuously* met and must be reported as such —
alongside, not inside, the criteria that are met by construction. **The distinction a vacuous criterion
protects has not been implemented; it has merely not yet been violated, and it will be violated by the
commit that first creates X.**

---

# Principle 17 – When a Blanket Ban Beats an Exemption

**Most guards in this codebase carry an allowlist: an exemption with a stated, checkable reason.
`RepositoryPathPortabilityTests` deliberately does not** — it bans `Path.GetFileNameWithoutExtension`
outright, and on 2026-08-30 it correctly reddened a **safe** use of it: a real filesystem path, not an
MSBuild `Include`. **The right response was to comply, not to carve an exemption.**

**The reason is the whole principle. A rule that fires only on the unsafe kind requires a reader to classify
each use correctly, every time — and that classification is precisely what failed in the original defect**,
where an MSBuild path was treated as a filesystem path and the build broke on Linux. **An exemption
mechanism reintroduces the exact judgement whose unreliability created the rule.**

**So the boundary between the two is not strictness, it is who does the classifying.**

- **Use an exemption with a stated reason when the guard itself can check the claim** — that a route has a
  sibling parameter route, that a named test method exists and contains the segment, that an error code is
  recorded unmapped where it was decided. **The guard verifies the exemption; a stale one reddens.**
- **Use a blanket ban when the distinction can only be drawn by a human reading the code**, and especially
  when that reading has already been got wrong. **The cost is occasional friction on safe code. The benefit
  is that nobody has to be right about a subtle case under time pressure.**

**A guard catching correct code is not automatically a false positive.** Ask which of the two situations
applies before adding an exemption — and note that "this use is obviously fine" is the same sentence the
original defect's author would have written.


## ⚠ The limit, added 2026-08-30 — a ban whose false positives outnumber its true ones protects nothing

**The principle above is about who classifies. It is not a licence to ban broadly, and the boundary has a
measurable form.**

A hazard was found in domain-event dispatch: an aggregate read `AsNoTracking` raises events nothing
collects. The obvious guard is a ban on `AsNoTracking`. ⚠ **That would refuse 203 call sites across 65
files to prevent a hazard occurring at NONE of them** — 17 of the nearby ones are existence checks whose
result is a `bool`, 5 are DTO projections, and the 8 entity-shaped reads all sit in read services that no
command handler injects.

⚠ **A GUARD WHOSE FALSE POSITIVES OUTNUMBER ITS TRUE ONES BY TWO ORDERS OF MAGNITUDE GETS DELETED RATHER
THAN FIXED — AND THEN IT PROTECTS NOTHING.** Friction on safe code is the price of Principle 17; it stops
being a price and becomes the whole cost when almost every firing is safe code. **The ban that survives is
the one people can live with.**

**The shape that fits here is narrower than either a ban or a test: guard the ESCAPE, not the query.** The
hazard needs the entity to reach a caller that mutates it, so the assertion is that **no read-side service
returns an event-raising aggregate type** — true today, cheap by reflection over return types, and with a
small false-positive surface because read services already return DTOs by convention.

⚠ **And the method that produced the right number is worth more than the number.** The first pass
classified by **proximity** — `Set<T>` and `AsNoTracking` within a window — and reported 26 entity-returning
sites, including an `AnyAsync` that returns `bool`. **Classifying by the TERMINAL OPERATOR — by what the
expression actually produces — took 26 to 8.** A guard built on the first pass would have fired on
seventeen correct existence checks. **Proximity is not semantics, and a classifier that over-fires is how a
ban earns its deletion.**

# Principle 18 – Errors Carry a Code and a Literal Message, and That Is Deliberate

`Error(string Code, string Message)` in the domain, `ApiError(int StatusCode, string Code)` at the
transport. **Two fields at both ends, and no structured detail anywhere in the pipeline.**

**Measured 2026-08-30, across `src/`: 345 declared error codes, 534 `static readonly Error` declarations,
21 constructed inline — and ZERO with an interpolated message, a concatenation, or a variable of any kind.**
Every error message in the product is a string literal. **A codebase that has not once reached for
interpolation across 345 opportunities is not enduring a constraint; it is keeping a convention.**

**The constraint is productive, and that is the reason to keep it.** Interpolated prose is not branchable —
no client can switch on *"page number or page size"*. **So each time someone needed to say more, the
two-field shape forced them to mint a CODE**, which is stable, machine-readable, and the thing a caller can
act on. **A free-text detail field would have absorbed exactly that pressure and made the easy path the
useless one.**

**Detail goes to one of three places, never into the message:**

1. **A distinct constant per condition — the primary mechanism.** The 345 codes exist because whenever a
   distinction mattered, someone minted a code rather than parameterising one. **The cost is vocabulary
   size, not lost information.**
2. **Per-handler translation.** The handler that knows *which* constraint lost returns a specific code,
   because a generic arm cannot (see Principle 13 and `DEC-DEP-0027`).
3. **The log, not the response.** EF records the violated index name; `AccessTokenIssuer` records why signing
   failed. **The caller is deliberately told less — that is a security posture, not an omission.**

**⚠ AMENDMENT 2026-08-30 — THE MESSAGE FIELD NEVER REACHES A CALLER, AND THE CODE FIELD IS DISCARDED FOR
37% OF DOMAIN CODES.**

**`Error.Message` is documentation for the developer reading the constant. It is not a response payload.**
The problem document is built as `Results.Problem(type, statusCode, title: error.Code, extensions: code /
correlationId / resourceKey)` — **no `detail`, and not one of 40 `Results.Problem(` call sites passes one.**
So all 344 message literals, including the 21 naming several conditions in prose, are domain-internal.

**And the code fares better but not well: 344 distinct domain codes reach 86 distinct wire outcomes, with
129 collapsing into `request.invalid` alone.** A caller receives the same code for a malformed body, an
unknown property, a stale row version and an out-of-range page size.

**The obvious escape hatch is not one.** `resourceKey` has **eight distinct values across the whole
product**, one per module surface — **it identifies which module refused, not what was wrong**, and carries
strictly less than the code beside it.

**The wire contract, end to end: 344 domain codes → 86 wire codes → 8 resource keys, with no prose anywhere
in the response.**

**⚠ "129 codes collapse" is a MEASUREMENT, not a defect count.** `request.invalid` for a malformed request
is a defensible contract, and **some collapses are deliberate security postures** — an invitation-token
error answers five different conditions identically so an attacker cannot learn which tokens exist. **The
defect count is however many describe a condition the caller could fix and cannot identify.**

**The codes were minted. The mapper is where they stop paying.** Treat a domain-only split as cosmetic, and
before adding a wire code ask whether a caller can act on the distinction — for pagination it plainly can,
because **a paging client is exactly the caller that retries in a loop.**

**⚠ A MESSAGE IS WRONG WHEN IT NAMES A DOMAIN THAT IS NOT THE SUBJECT OF THE FAILURE — WHICH IS NOT THE SAME
AS NAMING A DOMAIN AT ALL.** Two categories of noun behave completely differently and a guard that conflates
them produces false reds:

- **Tenancy scaffolding** — *company*, *tenant*, *branch*. **Cross-cutting, and legitimately named in any
  module.** *"A trusted company context is required"* names the company context, **which is exactly what
  failed**, whichever module the caller was standing in. Four such messages reach three or four modules each
  and are all correct.
- **Bounded-context vocabulary** — *employee*, *payroll*, *journal*, *identity*. **Wrong outside its own
  context.** *"The requested identity or access value already exists"*, returned for a duplicate holiday,
  named something the caller was not doing.

**Only the second is ever a defect.** `SharedErrorMessageArchitectureTests` is safe because it scopes to
`Persistence.*`, which is entirely the second category — **but its noun array carries this trap for whoever
widens it, and the distinction is recorded at the array for that reason.** A guard that flags correct
scaffolding messages is a false red, **and a false red is what teaches people to weaken guards.**

**Where a message names several conditions, the remedy is another code, not another field.** 21 of the 345
messages name more than one condition. `InvalidPagination` — *"page number **or** page size"* — is the clear
defect, because **a client fixing the wrong one retries and fails again**; the fix is two codes. **For most
of the 21 the caller is an internal guard clause that branches on nothing, and an ambiguous message to a
caller that ignores it costs nothing.**

# Principle 19 – A Wrong Explanation Survives Repetition Better Than No Explanation

**A symptom that is re-flagged repeatedly and never fixed usually has a plausible wrong diagnosis attached
to it.** Each re-flagging re-endorses the diagnosis instead of re-testing it, so the wrong explanation is
*strengthened* by the attention that ought to have destroyed it. **An unexplained symptom keeps its
question open; an explained one closes it, and a wrong closure is invisible.**

**The case that produced this.** `Authentication.md` listed `Expired Subscription` among its login failure
scenarios. `OD-SUB-0009` diagnosed it in 2026-08 as *"a login refusal for a state the product cannot
represent"*. That sentence was written once and quoted, in effect, four times over the following weeks —
each sweep found the row, matched it to the recorded explanation, and moved on.

**The diagnosis was false.** `SubscriptionTerm.HasExpiredAt` is real, tested, and evaluated on every
request. Expiry *is* representable, and it *does* refuse — with `403`, at the module boundary, because
`DEC-L-033` amended the ruling on 2026-08-26 precisely so a lapsed customer could still log in and renew.
**The row's defect was never representability. It was filing a real refusal under the wrong failure
surface** — a subtler error, and one that *"cannot represent"* actively hides, because a reader who
believes the state is impossible has no reason to go looking for where it is handled.

**Two operational consequences:**

- ⚠ **When a finding is being raised for the second time, re-test its explanation, not its symptom.** The
  symptom is confirmed by the fact that you found it again. The explanation is the part that has never
  been checked and is the reason nothing was done.
- **A follow-on edit named in prose is not an assigned edit.** `OD-SUB-0009` closed with *"If the ruling is
  anything other than the first, `Authentication.md` needs the corresponding edit — a separate task, since
  that file is outside this package."* The ruling *was* other than the first. **The sentence recorded the
  dependency and gave it no owner, and for four months nobody made the edit.** A cross-package edit named
  inside a package that cannot make it needs a queued item, not a sentence.

---

# Principle 20 – A Document's Claims About the Code Rot First, and Its Absence Claims Rot Fastest

**Descriptive prose about a design survives revision for a long time. A claim about the *state* of the
implementation is falsified by the next commit.** The two sit in the same paragraph and are read with the
same confidence, and only one of them is still true.

**The sharpest instance on record.** FP-014's README, dated **2026-08-25**, states plainly:

> **No code and no schema.** Nothing here is implemented.

The commercial plane's first migration, `AddSubscriptionCommercialPlane`, is dated **2026-08-26** — *the
next day*. Eight domain types, two EF configurations, two migrations and a passing gate suite now exist.
**The sentence was written to be admirably explicit, in a section headed *"stated plainly rather than left
to be inferred from a status word"*, and it was wrong within twenty-four hours.** Its own precision is what
made it durable: it reads as a considered statement rather than a status field, so nobody re-checked it.

**The rule:** an implementation-status claim carries a date and is re-derived, never inherited. Where a
document must say what does not exist, it says *as at* a date, and the reader treats it as a measurement
that decays — because that is exactly what it is.

⚠ **And the search that finds these is not another instrument over the code.** Every sweep this loop has
run asked what the code contains. **None asked whether the documents' claims about the code still hold** —
which is why a four-day-old falsehood in a ratified package survived eight enumerations that all had the
tree in front of them.


## ⚠ A BOUNDARY STATEMENT IS NOT A STATUS CLAIM, AND ONLY THE SECOND DECAYS

**Added 2026-08-31 after a sweep of sixteen packages, because the two look identical out of context and
confusing them produces false findings in both directions.**

FP-002 says *"Milestone 2 does not implement `AuthenticationSession`, `RefreshTokenRecord`, JWT issuance."*
⚠ **Read alone that is FP-015's false claim word for word.** **It is immediately followed by a Milestone 3
boundary that DOES implement them.**

**A BOUNDARY STATEMENT says what a numbered phase excluded, and is true forever because the phase is over.
A STATUS CLAIM says what the product contains, and is true only until the next commit.**

⚠ **So the sweep recorded FP-002 as making NO status claim rather than a FALSE one** — and it would have
been wrong either way had it matched on wording alone. **Check what the sentence is ABOUT: a phase, or the
tree.** **The tell is a date or a milestone number in the subject.**

# Principle 21 – An Undecided Decision Filed as Unbuilt Work Reads as Engineering Debt and Is Not

**"Not implemented" and "nobody has said what this means" look identical in a status table and are
opposite kinds of item.** The first is work someone can start today. The second **cannot be started at
all**, and reporting it as though it could is how a decision goes on not being made — because every
reader assumes it is queued behind the other work.

**The case.** Four of FP-014's acceptance criteria — `AC-SUB-0040`, `0049`, `0050`, `0051` — all rest on
the **seat**, which `DEC-L-009` names and never defines. `AC-SUB-0049` names `TenantUser` **because that is
the only reading available, not because it was ruled.** It was flagged in T-008, again in T-013, and is
still open. ⚠ **Filing those four under "not implemented" would have presented a decision nobody made as
engineering not yet done — in a document an owner reads to decide whether the product can be sold.**

**So a measurement of implementation status needs a fourth bucket**, not three:

| bucket | who can act |
|---|---|
| pinned by a test | nobody — it is done and defended |
| implemented, unpinned | an engineer, today |
| not implemented | an engineer, today |
| ⚠ **subject undefined** | **only the owner, and no engineering can proceed until then** |

## ⚠ A fifth state, added 2026-08-30 after measuring the same package twice

**A criterion can be defective in its PREMISE rather than in its status**, and a status column expresses
neither — green, red and absent all mislead. Two of 54 turned out to be this:

| criterion | why it is met | who can act |
|---|---|---|
| *no tenant-plane subscription permission exists* | the package defines none on **either** plane | **nobody now** — whoever builds the missing half must re-check it |
| *losing entitlement deletes no row; counts before and after are identical* | **there is no lapse event.** Expiry is a pure read; nothing is written when a term ends, so there is no moment at which a deletion could occur and no before-and-after to count | same |

**Both guarantees are real and neither is evidence of anything, because the commit that first creates the
mechanism is the commit that can violate them.** They are notes attached to future work, not work — a
sixth actor the four-bucket table has no row for.

⚠ **AND THE TEST FOR THIS STATE MUST BE APPLIED CAREFULLY, BECAUSE IT OVER-FIRES.** A third criterion was
proposed for the same treatment on the grounds that it mentions six permissions that do not exist. **It
was refused: those six are a PARENTHETICAL, and the criterion's subject is the whole 28-name permission
set, which exists.** The criterion is fully evaluable and simply unmet. **A criterion that names something
absent is not thereby defective — read what it QUANTIFIES OVER, not what it mentions.**

**And the two weak forms of the third bucket must be separated too.** *"A whole-tree symbol search found
no such type"* and *"I did not find the seam that would couple these"* are different claims: the first is
close to proof, the second is an admission. **Four of FP-014's nineteen are the second kind and say so.**
See **Principle 20** — an absence claim is a measurement, and the weaker its method the faster it rots.

---

# Principle 22 – Removing an Interface Member Orphans Its Implementers; the Compiler Only Tells You About Consumers

**Deleting a member from an interface does not break the classes that implement it. It orphans them.** The
implementations still compile — they are now ordinary methods nobody calls — and the failure arrives from
analysis, not from the type system: an orphaned member trips **`CA1822`** (*does not access instance
data*), and `DEC-L-008` condition 1 is **zero warnings**. ⚠ **So the gate goes red for a reason that has
nothing to do with whether the change was correct.**

**The measured case.** `ICurrentUser.CompanyId` was removed under `ADR-025` decision 4. It had **one
consumer** — a single test assertion — **and one hundred implementers**: 95 stubs in `tests/`, four in
`src/` and `tools/`, plus the real one. **The dispatch that ordered the removal said its only references
were the declaration, the implementation and the prohibition. That was true of consumers and wrong by two
orders of magnitude about implementers.**

⚠ **A compile-error search finds consumers and is blind to implementers — the exact inverse of what
*"find every call site"* leads you to expect.** Before removing an interface member, enumerate the types
that *declare* it, not the code that *reads* it.

## ⚠ And an incremental build is not evidence about code it did not recompile

The same change built **clean, zero warnings, with four orphans standing in `src/` and `tools/`.** The
build was **incremental**: those projects were never recompiled, so they never re-emitted their warnings.
The gate, which builds from scratch, caught it; `--no-incremental` reproduces it locally.

**A green build is a statement about the projects the compiler actually looked at.** For any change that
can produce *analysis* warnings rather than *compile* errors — interface member removal, visibility
changes, dead-parameter removal — **build with `--no-incremental` before believing the zero.** This is the
build-system form of the rule that a passing guard whose file walk found nothing has told you nothing.

---

# Principle 23 – A Wrong Question Passes Every Check a Wrong Instrument Fails

**Instrument defects are catchable.** Plant a known positive, run a control, check the floor — this
codebase now has a discipline for all of it. ⚠ **None of it detects a correct measurement of the wrong
thing, because every control validates the instrument against the question you already asked.**

**The case, and it cost an owner decision and three annotated `Accepted` ADRs.** An instrument searched
for production readers of `DequeueDomainEvents` and found none. **That was true, and it is still true.**
`AggregateRoot` declares both `DequeueDomainEvents` and `ClearDomainEvents`, **and the dispatch path uses
the `DomainEvents` property and `ClearDomainEvents()`.** The member searched for was real, unused, and not
the one the mechanism runs on.

⚠ **The instrument was right and the question was wrong — a cleaner failure than a wrong instrument, and
much harder to notice**, because everything downstream is sound. There is no red to find.

**Two things make it worse, and both are avoidable:**

- ⚠ **Restating one query three ways reads as three corroborating measurements.** The published finding
  said *"nothing consumes them"*, *"there is no dispatcher"* and *"checked three ways"*. **That is one
  measurement wearing three coats. Corroboration requires a different METHOD, not a different sentence.**
- **A negative was published without opening the thing that would have used the mechanism.** One file —
  the unit of work, whose entire job is this — settles it in thirty seconds.

**The check that catches it:** for a claim of the form *"the product does not do X"*, **do not only
enumerate the members you believe X would use. Open the type whose responsibility X is, and read it.**
An absence claim about a mechanism is verified at the caller, not at the member.

⚠ **And the confirming half is what makes an exercise worth more than a reading.** When the flow was
finally exercised, the test that mattered — *committing the transaction dispatches what the save withheld*
— **passed on its first run.** Had events genuinely been lost, it would have failed then, rather than
needing a plant to prove it could fail. **A green that could only have been produced by working code is
evidence; a green that a plant had to justify is merely not-yet-disproved.**

---


## ⚠ Second instance, 2026-08-30 — and the tell is a SCOPE inherited from a naming convention

The same failure recurred within hours, on a different subject, and it is worth having both because the
shape of the wrong question changed.

A measurement closed with *"no read service is injected into any command handler."* **False: eight command
handlers take one, across four services.** The search looked for three interface names **in files named
`*CommandHandler*.cs`** — and handlers in this codebase live in files named for their aggregate,
`LeaveCommandHandlers.cs`, **plural.** ⚠ **The instrument enumerated a subset perfectly and reported it as
the whole.**

**First instance: the wrong MEMBER. Second: a file-naming assumption the codebase does not follow.**
⚠ **AN ENUMERATION SCOPED BY A NAMING CONVENTION INHERITS THAT CONVENTION'S FALSITY, SILENTLY** — a filter
that excludes the wrong files produces a smaller, entirely correct answer to a question nobody asked.
**Before trusting a population, ask what the filter EXCLUDED and name one member of the excluded set.**

⚠ **And note which half survived. The conclusion held — the hazard is still unreachable — but for a
different reason: read services hand over DTOs, not aggregates.** **A false premise under a true
conclusion is the more dangerous of the two, because nothing fails until someone reasons from the
premise** — and it was caught only by an independent check aimed at a different question.

# Principle 24 – Enumerate the Mechanism, Not the Names

**A search for names cannot be complete, because you cannot enumerate the names you did not think of.
A search for the MECHANISM can be, because a language offers a finite number of ways to do the thing.**

**The case.** The question was which of 61 interface members are reached without a compile-time reference —
by reflection, container resolution or serialization. **Searching for the members' names was tried and is
worthless:** `TenantId` matches **2,236** string literals, almost all EF column names in configurations and
migrations; `CompanyId` 1,188; `EmployeeId` 392. **A name search cannot tell `ICurrentUser.CompanyId` from
a `CompanyId` column.**

⚠ **Enumerating the MECHANISM closed it in one pass.** `src/` and `tools/` contain **exactly four dynamic
member-access sites** — a `GetMethod(nameof(...))` on a *private* method of a concrete class, its `.Invoke`,
and two `JsonElement.GetProperty` calls **which are JSON document lookups, not .NET reflection at all**.
`InvokeMember`, `CreateDelegate`, `GetMembers`, `GetInterface`: **zero.** **None targets an interface
member.** The answer is *none*, and it is measured rather than inferred.

**Why the mechanism set is closeable and the name set is not:** the ways to reach a member without naming
it in code are enumerable from the runtime's API surface. **The things someone might have called a member
are not.**

⚠ **And a control over a mechanism search must validate the SEARCH, not the thing being searched.** The
known positive used here was a genuinely reflective private method — **deliberately not one of the 61** —
because without it *"zero dynamic sites"* is indistinguishable from *"the search looked in the wrong
place."*

## ⚠ The corollary that caught me: a specified control can have no subject

I required *"validate against a member known to be DI-resolved."* ⚠ **No such member exists, and cannot.**
**A container resolves TYPES; it never calls interface MEMBERS.** It constructs `Foo` for `IFoo`, and every
`foo.Bar()` afterwards is an ordinary compile-time reference any probe sees. **"DI-resolved" is a blind
spot for types and not for members.**

**So the right response to an impossible control is to say the question was wrong, not to approximate it.**
A control invented to satisfy a specification, over a subject that does not exist, would have passed and
meant nothing — the vacuity failure arriving through the specification rather than through the code.

---

# Principle 25 – When Two Fixes Ship Together, Plant Each: the Second May Hide the First

**Ruling two remedies for one defect feels safe. It costs you the ability to say which one you bought.**

**The case.** A committed command was being reported as failed. Two fixes were ruled: **move the dispatch
outside the `try`**, and **isolate each consumer behind its own `catch`**. Both shipped; twelve tests
green. ⚠ **Then the first plant reddened NOTHING: moving the dispatch back inside the `try` left every test
passing.** With per-consumer catches in place **nothing throws from that position**, so the `try` boundary
makes no observable difference. **The remedy named as the headline was not load-bearing.**

⚠ **AND THE PLANT THAT REDDENS NOTHING IS THE VALUABLE ONE, BECAUSE IT TELLS YOU WHERE THE GUARANTEE
ACTUALLY LIVES.** Here it lives in the dispatcher never throwing — **a contract nothing enforces.** A test
injecting a dispatcher that throws outright, which no per-consumer catch can intercept, **failed.** The
ordering change stops the rollback masking; **it does not stop a dispatch-level exception reaching the
caller over a durable write.**

**So the rule has two halves:**

- **Plant each fix separately.** A plant that reddens nothing means either the fix is redundant or the
  guarantee rests somewhere you have not named — **and you cannot tell which without asking.**
- ⚠ **When a fix turns out to be redundant, find what is doing its job and ask what enforces THAT.** The
  redundancy is not the finding; **the unguarded contract underneath it is.**

**And a scope surprise is reported, not absorbed.** The remedy for the remaining hole needed a logger
threaded through **33 test construction sites, 30 of them in a suite the task gate does not run.** It was
built, measured and **reverted**, and reported as one commit to be taken deliberately — **at phase scope,
because a diff whose majority lands outside the gate that would be run is a diff nobody verified.**

---

# Principle 26 – What Is Never Materialised Is Never Checked

**A test suite that builds a model but never opens a connection asserts what the mapping SAYS, never what
it MEANS.** The two are different, and the gap is invisible because both live in the same file and read
like one statement.

**Measured 2026-08-31.** Across the seven suites the task gate runs, there are **eight**
`EnsureCreated`/`Migrate` calls; the suites that name `UseSqlServer` point at
`"Server=model-only;Database=none"`. **Integration alone holds 144.** ⚠ **So everything the persistence
configuration means at the database level is asserted in exactly one suite — 36% of the repository's
assertions — and it is the suite the merge gate skips.**

⚠ **THE SHARP EDGE IS THE RAW STRING LITERAL — with a correction that sharpens it further.** A unique
index carries `.HasFilter("[NormalizedNationalId] IS NOT NULL")`. **Deleting that call changes nothing: EF
Core's SQL Server provider adds the same filter BY CONVENTION for any unique index over a nullable
column** — measured by removing the declaration and reading the built model. ⚠ **Writing
`.HasFilter(null)` DOES remove it, because that explicitly overrides the convention**, and the model then
reports no filter at all.

**Either way the point stands: it compiles, the argument is an expression nothing type-checks, the model
still builds so every model-shape assertion passes, and no gate-run suite creates a schema, so nothing
observes an index.** The gate goes green.

**And the consequence is a data defect.** SQL Server treats NULLs as **equal** in a unique index, so
without the filter **the second row with a NULL in that column is refused** — for a column that is
optional by design.

⚠ **The correction is itself the principle's best evidence.** It was found because the guard's author
**planted the deletion and the guard stayed green** — and reported the plant rather than trusting it.
**A convention that silently supplies what a declaration omits is invisible to source reading and visible
only in the built model: `IIndex.GetFilter()` returns what will be CREATED, not what was WRITTEN.**
**Read the model, not the source, for anything a framework may supply on your behalf.**

**The rule this yields, which is more general than schemas:**

- ⚠ **Any configuration expressed as a string the compiler does not parse — a SQL filter, a connection
  fragment, a route template in a literal, a JSON path, a regex — is checked by NOTHING until the thing it
  configures is actually built.** Type-checking, model-shape tests and architecture guards all pass over
  it unchanged.
- **So a structural guard over the CONFIGURATION is worth having even where an integration test already
  covers the behaviour** — because the structural one runs where the integration one does not.

**What still holds, so this is not overstated:** the task gate builds the whole solution, **so a change
that fails to compile anywhere cannot merge.** The exposure is runtime behaviour and configuration-only
analysis, not compilation.

---

# Principle 27 – An Instruction Attached to a Non-Event Has Nothing to Fire On

**A process rule only runs if it hangs on an action somebody actually performs.** Rules attached to states,
to moods, or to the absence of activity **do not run at all** — and they fail silently, because nobody can
point at the moment they were skipped.

**The case, measured from inside the loop that suffered it.** A queue file carried the instruction *read
this before going idle.* ⚠ **Going idle is not an action anybody takes: there is no moment where a worker
decides to stop — the turn simply ends.** Over an entire working session the file was opened **twice**,
both times because a message told the reader to open it. **The instruction had nothing to fire on.**

**Three lines below it sat a rule that worked: *grep the results trail before building any instrument.***
**That one hangs on an action somebody performs, and it fired repeatedly.** The difference is not wording,
emphasis or placement. **It is whether the trigger exists.**

**The repair is not a louder instruction. It is re-anchoring to a real event:** *reading the queue is the
LAST STEP OF COMPLETING AN ITEM.* Completing an item is something that happens; going idle is not.

⚠ **Apply this to every process rule you write:** name the action it hangs on. **If you cannot name one, it
will not run**, however prominent, however agreed. **A reader can accept a rule completely and still never
execute it, because agreement is not a trigger.**

**And know the residual honestly.** Re-anchoring here converts *stopped with work outstanding* into
*stopped with an empty queue at last look* — **smaller and more honest, and still not zero, because the
last completion always ends somewhere.** ⚠ **A worker that cannot self-wake needs an external trigger for
the final gap, and no rewording supplies one. Say so rather than implying the rule closed it.**

---

# Principle 28 – A Fix Belongs at Every Site of the Mechanism, Not at the Site of the Symptom

**When two places share a mechanism and only one of them hurts, the repair goes to the one that hurt.**
The other keeps the defect, and keeps it *invisibly*, because nobody has a symptom to attach to it.

**The case, and it is this repository doing it to itself, three times.** Two integration tests need the
same fact: that a database backup is in flight while a permission check runs. One closes that window **by
construction** — a time-bounded loop and two overlapping competitors, with a comment recording **0 misses
in 506,102 samples.** The other closes it **by probability**: one competitor, one backup, and 240 MB of
filler data whose only purpose is to make the backup slow enough to be caught.

⚠ **Both competitors were written in the SAME COMMIT, and one was already a loop while the other was a
bare one-shot.** A later commit made the loop time-bounded — *a fixed count was an elapsed-time dependence
hiding inside the loop.* A later one still added the second overlapping competitor. **The one-shot site
received none of the three.**

**And its own sibling states the diagnosis in prose: *a one-shot competitor turns the test into a race it
usually loses.*** The knowledge was present, committed, and adjacent for two weeks. **What was absent was
anyone asking where else the mechanism lived.**

**The check, and it costs one grep:** when you fix something, **name the mechanism rather than the file,
and find every site of it.** *Where else does this pattern occur?* — asked at the moment of the fix, when
the mechanism is loaded in your head and never afterwards.

⚠ **The tell that you are about to make this mistake: the fix is easy and obviously correct.** An easy
fix produces no pause, and the pause is where the question would have been asked. **Every one of the three
commits above was correct in what it did.**

**Related, and the reason this is its own principle rather than a note under 16d:** union collapse is a
*measurement* failing to see one member. **This is a REMEDY failing to reach one member — same shape,
opposite half of the work, and no amount of measuring catches it, because the measurement was never wrong.**


## ⚠ The qualification, learned the same day: the SITES are shared, the REMEDY may not be

**Finding every site of a mechanism is the first half. Assuming one fix serves them all is a new way to
do damage.**

**Immediately after this principle was written, a sweep found a fifth site of the same *a child process
discards its evidence* mechanism.** The obvious move was to apply the abstraction built for the other
four. ⚠ **It would have deadlocked.** That helper drains each stream with a single `ReadToEndAsync`,
which is **incompatible with a line-by-line handshake** — the fifth site blocks on a marker the child
prints and then keeps running. **Using the fix there would have replaced a pipe-buffer hazard with a
handshake hazard.**

⚠ **AND THE SEVERITY DIFFERED TOO, WHICH DECIDED IT.** At the four converted sites the lost evidence was
the difference between *the work finished* and *the process never started* — **a wrong verdict.** At the
fifth, the verdict is reported as inconclusive either way: **what is lost is the REASON, not the
correctness.** **A design change to a fixture whose timing was deliberately tuned, to buy the reason for a
rare inconclusive trial, is not obviously worth it — so it was proposed and not made.**

**So the discipline is two questions, not one:**

1. **Where else does this mechanism live?** — always ask it, at the moment of the fix.
2. ⚠ **Does the same remedy FIT there, and does the failure cost the same?** — **a site can share the
   mechanism and deserve a different answer, including none.**

**Record the finding at the site regardless.** The unread output exists; the next reader should learn that
from the code rather than from a sweep. **Naming what would make the fix worth doing — here, inconclusive
trials becoming frequent — is what turns a decision not to act into something later readers can revisit.**

---


## ⚠⚠ Count the IDENTIFIER, not the LAYOUT — 2026-08-31

**The same principle at the level of documents. An identifier is an invariant; the shape it sits in is an
accident, and counting the shape gives a plausible zero.**

**Fourteen packages record acceptance criteria in at least FOUR layouts.** Table rows in three packages;
markdown headings in six; ⚠ **bullet lists in three more — FP-002, FP-007 and FP-008, holding 171 criteria
between them**; and two whose identifiers match none of those patterns at all.

⚠ **A grep for table rows reported *0 criteria* for eleven packages. A grep for headings would have
reported 0 for the bullet three. Neither errors. Both look like an answer.** And the architect published
the two-format story one turn before a third and fourth were found — **in the item that existed to warn
about exactly this.**

**The measurement that works is FORMAT-BLIND: count distinct `AC-[A-Z]+-[0-9]+` identifiers. Layout cannot
hide an identifier.** **607 across the fourteen packages.**

⚠ **And even the closest layout undercounts: one package has 93 headings and 94 identifiers.** **THE
LAYOUT CENSUS IS A DIAGNOSIS; THE IDENTIFIER COUNT IS THE MEASUREMENT** — the first tells you why a naive
number was wrong, the second is the number.

**Generalises past criteria:** wherever a thing has a stable identifier and an unstable presentation —
requirements, error codes, ADR numbers, test names — **key on the identifier and treat the surrounding
shape as noise.** ⚠ **If your regex mentions punctuation, you are counting layout.**

# Principle 29 – A Test That Claims a Criterion Must Cite It

**Measuring which acceptance criteria a suite actually pins is either free or it costs a day, and the
difference is one comment.**

**Measured 2026-08-31 on FP-002.** Of 51 criteria, **19 were pinned by tests that CITE the criterion id in
their body.** Those nineteen took minutes: grep the id, read the assertion, done. ⚠ **The remaining 32
took a day and did not finish**, because citation is **sufficient** for *pinned* and **not necessary** —
so their absence proves nothing, and each has to be chased by hand:

- one is an **exact match that never names the criterion** — the assertion is the criterion, reworded;
- one has near neighbours where **the closest asserts the OPPOSITE scope**;
- one has no name match at all, **which establishes nothing either way.**

⚠⚠ **SO *32 UNCOVERED* WOULD HAVE BEEN AN ABSENCE CLAIM MADE ON AN INSTRUMENT THAT CANNOT SEE ABSENCE.**
The honest bucket is **UNRESOLVED**, and it is expensive to leave that way.

**The convention: when a test is the thing that pins an acceptance criterion, name the criterion in the
test.** A comment is enough. **It costs one line at the moment the mapping is in your head, and it is the
only moment anybody knows it.**

⚠ **THE ENFORCEABLE HALF, WHICH IS NARROWER AND ALREADY EXISTS: a cited id must RESOLVE.** FP-002's
measurement carried that control — **no test cites an `AC-AUTH` id the specification lacks** — and without
it all nineteen would have been worthless, because a citation pointing at a criterion that does not exist
reads exactly like one that does.

**What is NOT proposed: requiring every test to cite something.** Most tests pin no criterion, and a rule
demanding otherwise would produce ceremonial citations — **which is worse than none, because it makes the
grep stop working.**

---


## ⚠⚠ And the grep must be KEY-AGNOSTIC — search the ID, not the attribute

**Learned within the hour of adopting this principle, on the principle itself.**

**Three trait keys in this repository carry criterion ids:** `[Trait("Acceptance", …)]` with **56** uses,
`[Trait("Criterion", …)]` with **24**, and `[Trait("Decision", …)]` with **23**. ⚠ **A grep on any ONE key
finds about a third of them** — and a measurement that had looked at one package's convention concluded a
second package cited nothing, when **seven of its fourteen already did.**

⚠ **THE PRINCIPLE SURVIVES; THE INSTRUMENT DOES NOT. Search the ID TEXT — `AC-[A-Z]+-[0-9]+` — NOT THE
ATTRIBUTE THAT CARRIES IT.** The id is the invariant; the trait key is the layout, and this is Principle
24's rule arriving one level down: **if your regex mentions the wrapper, you are counting the wrapper.**

**Two corollaries paid for the same day:**

- ⚠ **Adding a citation must never REPLACE one.** A test found citing what looks like the wrong criterion
  is **reported, not corrected** — rewriting a recorded mapping on a reading of a name is invention.
- ⚠ **Where no test asserts the criterion, SKIP AND LIST IT.** Citing the nearest neighbour makes the grep
  **confidently wrong**, which is strictly worse than the silence it replaces.

**And a backfill that would require re-deriving the mapping is not a backfill.** Where a prior measurement
recorded test CLASSES and a citation needs METHOD granularity, **deriving it is re-measuring — and
re-measuring under a backfill's name is how a wrong citation gets written.**


## ⚠ A mention in a comment is not a claim — and there is a FOURTH key

**Refined 2026-08-31 by applying the principle rather than theorising about it.**

**Measuring FP-003, the split that matters is between an id a `[Trait]` CLAIMS and an id a comment merely
MENTIONS.** Twelve criteria are claimed; **one more is named in prose and asserts nothing about it.** ⚠ **A
method that counted mentions would have reported thirteen — and thirteen would have been wrong in the
direction that flatters the suite.** **Count claims; list mentions separately.**

⚠⚠ **AND A FOURTH TRAIT KEY EXISTS — `AcceptanceCriteria`, alongside `Acceptance`, `Criterion` and
`Decision`. FOUR KEYS FOR ONE RELATIONSHIP.** This is the fifth time in one day that a thing with a stable
identifier and an unstable wrapper produced a plausible wrong number: padded versus unpadded keys, four
criteria layouts, three trait keys, a homonym, and now a fourth key. ⚠ **Every one of them returned a
NUMBER rather than an error.** **Key on the id text. Always. The wrapper is never the thing.**


## ⚠⚠ THE MOST DANGEROUS VOID PLANT: A CONFIDENT GREEN FROM A BUILD THAT NEVER HAPPENED

**Four ways a plant can fail to be evidence were recorded across this codebase, and all four produce an
ABSENCE:** a revert to nothing because the file was never staged; a project the incremental build never
recompiled; a probe inserted into a nested scope (`CS0106`); a rename that would not compile.

⚠⚠ **THE FIFTH PRODUCES A POSITIVE, AND IT IS WORSE.** A `// PLANT` comment was inserted before a
`.WithName(…)` continuation, **breaking the fluent chain — `CS1002`. The build FAILED. `dotnet test
--no-build` then ran the PREVIOUS binaries and reported `Passed! 7`.**

**The plant did not merely fail to redden. It returned a green from a build that never happened**, on code
that does not compile — **and a green is exactly what a correctly-behaving control looks like.**

⚠ **STANDING RULE: AFTER ANY EDIT, READ THE BUILD RESULT BEFORE THE TEST RESULT.** `--no-build` is safe
only when nothing has changed since the last successful build, **and after an edit that is precisely the
thing you cannot assume.** **It was caught only because the build output was read first.**


## The enforceable form: A MEASUREMENT THAT DISCOVERS A MAPPING CITES IT

**Measured across three packages 2026-08-31: FP-002 has 51 criteria and 19 citations, FP-003 has 93 and
12, FP-004 has 64 and ZERO. Three packages, 208 criteria, thirty-one citations.**

**The convention is not merely inconsistent in its key. It is ABSENT FROM ENTIRE PACKAGES.** And FP-004 is
heavily tested: **zero cited does not mean zero covered. It means the mapping exists NOWHERE in the
repository** — not in a trait, not in a comment, not in a result file — **while the tests were plainly
written by people who knew exactly which criteria they were satisfying.**

**Backfilling by hand reproduces the expensive thing, so the move is forward. But forward on its own hangs
on no event, and Principle 27 says it will not run.**

**SO THE RULE TAKES AN EVENT THAT EXISTS: WHEN A MEASUREMENT DISCOVERS THAT A TEST PINS A CRITERION, IT
CITES IT THEN — not later, not in a report.** Not backfill-everything; **cite what you found while you were
there.** A measurement has already read the assertion and formed the judgement, so **the citation costs one
line at the only moment anybody holds both halves.**

**The evidence that this is the right event:** three mappings, one per package, were each discovered by a
spot-check, named in a report, and left uncited. **They cost real work and survive only in result files,
which this repository has proved go missing twenty-seven at a time.**

**And the first live catch came free: a guard written in one item pinned a criterion cited NOWHERE, while
that criterion was written in another item three apart. Neither knew about the other, and the mapping
existed only in two heads.**


## The heading is not the criterion, and a caveat a command can remove is not a scope note

**Two rules from 2026-08-31, both learned by nearly shipping their opposite.**

**`AC-LOC-0019` is headed *Cache coherence*. Its body reads: *version revalidation/eviction observes
15s/30s/5m/60s bounds and never crosses Tenant/culture.*** A test about post-commit domain-event eviction
matches the heading perfectly and **has nothing to do with the criterion.** The mapping was published,
then caught before it was written into the code.

**THE HEADING IS A LABEL. THE BODY IS THE CRITERION.** A heading is chosen for scanning and is as much
layout as a table row or a trait key — **the same family that produced padded-versus-unpadded keys, four
criteria formats, four trait keys and a homonym, one level deeper.** ⚠ **Read the body. Every time. A
heading match is a hypothesis, not a finding.**

**And the counterweight to a day spent praising stated limits:**

⚠ **A CAVEAT A SINGLE COMMAND CAN REMOVE IS NOT A SCOPE NOTE.** A report was about to record that an
assertion was *inferred*; one command settled it instead. **A limit that exists because nobody spent thirty
seconds is not honesty — it is unfinished work wearing honesty's clothes.** **Before writing a caveat, ask
whether a command you could run right now would remove it.**

**A related discipline, from the same item: MATCH THE EXISTING KEY, DO NOT CHOOSE ONE.** Adding a fifth
spelling to tidy four spellings **makes the grep worse while looking like cleanup** — and the tidying
reflex fired twice in one turn, in the item that had just warned against it.


## Write the heading as a CLAIM, not a LABEL — measured 2026-08-31

**Sampling 46 criteria across the six packages that have a heading/body split: 40 agreed, 5 were narrower
but compatible, and ONE misled. Heading scanning is safe.** (Bullet and table forms have no split at all —
the label IS the text — so four packages are structurally immune.)

**But the divergences concentrate, and the concentration names the fixable thing.** Four of the six
non-agreements come from one package, **including the only misleading one — and that package's headings
average 2.4 WORDS against another's 6.5, which had no divergences at all.**

⚠ **HEADING LENGTH PREDICTS RELIABILITY.** *Cache coherence* and *Restore default* are LABELS over dense
bodies. *A mapped identity holding the self permission reads its own records* is a CLAIM — **and a claim
cannot diverge from a body, because it already IS one.**

**So when writing a criterion, write its heading as the claim it makes.** The cost is a few words; the
benefit is that the heading and the body cannot drift apart, because there is only one statement.

⚠ **And the usage rule, since the existing headings are not being rewritten: HEADINGS ARE LICENSED FOR
ORIENTATION AND NOT FOR ESTABLISHING A MAPPING.** The one miss in forty-six was exactly that misuse.

## And a synonym misleads as readily as a heading

⚠⚠ **A criterion required that two employee numbers normalising alike CANNOT BOTH BE CREATED. A test named
`Employee_numbers_that_normalize_alike_are_equal` matches those words almost exactly — and asserts VALUE
EQUALITY, which a product can satisfy WHILE HAPPILY PERSISTING BOTH.** The real pin was in a different
suite entirely, asserting a conflict on the second insert.

**Uniqueness and equality are synonyms in English and opposites in a database.** ⚠ **Both near-misses in
this sweep — a heading and a synonym — were caught only by reading WHAT THE TEST ASSERTS, never by what it
is called.**


## An unconditional label is not a reading of the result

**A command was written as `grep … ; echo "[empty = no such test]"`. The echo prints whatever the grep
found.** ⚠ **Three times in one day it announced that a non-empty result was empty — most recently claiming
a package had no concurrency or deletion test WHILE THE GREP DIRECTLY ABOVE IT LISTED FIVE, two of which
became citations minutes later.**

⚠⚠ **A LABEL THAT DOES NOT DEPEND ON THE RESULT IS NOT A READING OF THE RESULT.** It is a caption written
before the evidence arrived, and it wins, because a human eye reads the sentence and not the rows above it.

**This is the same family as a test run against stale binaries reporting `Passed!` after a failed build:
both produce a CONFIDENT POSITIVE that was never conditioned on anything.** **Absence-shaped failures get
caught eventually because somebody eventually looks for the thing. A false positive closes the question.**

**Two habits:**

- **Never annotate output you have not seen.** If a summary line is wanted, derive it — `[ -s file ] &&
  echo FOUND || echo NONE` — **so the words cannot contradict the rows.**
- ⚠ **When a report says *nothing found*, check whether that sentence was PRINTED or DECIDED.** The two are
  indistinguishable in a transcript and opposite in meaning.


## Before writing a guard, look for the one that already exists — and cite it as a superset

**A ruling asked for four structural bans to be asserted. Reading first found THREE OF THE FIVE ALREADY
GUARDED** — an endpoint ban by a whole-surface route inventory, a cascade ban by a whole-model foreign-key
test, and a persistence ban already cited. **Only three of the five clauses needed anything new, and the
new test covered them all.**

⚠ **A DUPLICATED GUARD IS TWO PLACES TO EDIT AND ONE PLACE TO FORGET.** The narrower copy is the one that
gets updated, the wider one silently stops matching, and both go green.

**So the disposal is: cite the existing guard AS A SUPERSET, and resist writing the narrower version.** A
whole-model assertion covering your case **asserts more than an aggregate-specific one would**, and
replacing it with a local copy is a downgrade wearing the costume of precision.

## ⚠ A guard with two reflection paths needs a control per path

**The new ban walks types AND gathers constants — two different reflection paths in one test.** ⚠ **The
constant-gathering path could return an empty catalog while the type walk works perfectly, and the guard
would pass over half a population with nothing to show for it.**

**So it ships with a control per path**: the type matcher must find delete-shaped names that DO exist in
live code, **and the permission catalog must be reachable and non-empty.** ⚠ **One control over a two-path
guard leaves half of it unmeasured — and the unmeasured half is the one nobody thought to plant.**

## A test can inherit its limit from the criterion rather than owning it

**The ban is NAME-shaped: a method called `PurgeEmployee` satisfies it and violates the intent.** ⚠ **But
the criterion is itself written in terms of names — *no delete command… exists* — so the test matches the
criterion's own form.**

**THE LIMIT IS THE CRITERION'S, NOT THE TEST'S** — and that distinction decides who should fix it. **A test
that is exactly as strong as the thing it pins is correct; making it stronger would be asserting something
nobody specified.** **Record the limit against the specification, and leave the test alone.**


## A test can assert the right rule in the wrong STATE

**Three near-misses in this sweep were about identity — a heading that named a different subject, a synonym
that meant the opposite, a test of another aggregate entirely. The fourth is subtler and harder to see.**

**A criterion required that a terminated employee's national ID stays reserved. A national-ID uniqueness
test already existed and refused a duplicate.** ⚠ **It uses TWO LIVE EMPLOYEES — so it establishes the
constraint between actives and CANNOT SEE WHETHER TERMINATION RELEASES THE VALUE.** Same subject, same
rule, **a state the criterion cares about and the test never reaches.**

⚠ **And releasing it is the PLAUSIBLE mistake:** a terminated row looks like one that no longer needs its
identifiers. **The criterion exists because someone might reasonably do the wrong thing — and the existing
test would have stayed green while they did.**

**So when matching a test to a criterion, check the STATE the criterion is about, not only the rule it
names.** A rule proven in one state says nothing about another.

## Design the test so a pass can only come from the thing under test

**The new test creates an employee, terminates it, then creates a second with a DIFFERENT employee number
and the SAME national ID.** ⚠ **The differing number is the whole design: it makes the national ID the only
thing that can refuse the create, so a pass cannot come from the number constraint.**

**Without that, a green proves *something* refused the insert and leaves you to guess which.** **A test
that can pass for two reasons has measured neither.**

⚠ **And take the plant from the failure mode rather than inventing one:** here it was `&& Status !=
Terminated` added to the production existence check — **literally the mistake the clause exists to catch.**
**A plant that is the real error proves the test guards the real error; a plant invented for the occasion
proves only that the test can fail.**


## The more convincing the name match, the more it needs the body read

**Four criteria were titled `V:`, `W:`, `X:`, `Y:`. Four tests were named `V_…`, `W_…`, `X_…`, `Y_…`, and
the file's own section comments repeated the letters.** A deliberate, exact, four-way correspondence —
⚠ **and it existed ONLY as a shared prefix, where nothing can query it.**

**The reading was done anyway, and the rule is the reason:** *a letter is a name, and the discipline does
not get suspended because the name looks convincing.*

⚠ **THE FOUR NEAR-MISSES IN THIS SWEEP ALL TURNED ON A NAME — a heading naming a different subject, a
synonym meaning the opposite, a test of another aggregate, and a rule proven in the wrong state.** **The
temptation to skip the body scales with how good the name looks, and the name is exactly what has been
wrong every time.**

**The reading paid here too: one test asserts the create throws AND that the row count is still zero —
which is what makes the behaviour *refused* rather than *silently rewritten*, the exact distinction its
criterion draws. A name match would have missed that the second assertion is the point.**

## A control that is a step in the arrangement cannot drift away from what it controls

**Three revocation tests each carry their own anti-vacuity control without naming it one: each CREATES
SUCCESSFULLY FIRST, then revokes, then fails** — so the refusal is provably the revocation **rather than a
boundary that refuses everything.**

⚠ **A separate control test can be deleted, renamed or left behind when the thing it controls moves. A
control built into the arrangement travels with it.** Where the shape allows it, **prefer the arrangement.**

**The strongest of the three first asserts the actor holds ZERO scope rows, so the create that succeeds is
PROVABLY running on implicit scope** — the criterion's actual subject, established rather than assumed.

## Defer a forecast, do not withdraw it, when it fails in the safe direction

**A prediction that the yield would fall was contradicted: 0.40, then 0.60, then 0.70.** ⚠ **The cause was
visible rather than guessed — the criteria in that range are exactly what one test file was written
against — so the forecast was DEFERRED rather than withdrawn, and the range where it should bite was
named.**

***Not yet* and *no* are different answers.** **Saying which costs nothing at the time and everything
later, because a forecast quietly dropped after one good result is indistinguishable from one that was
never made.**


## A forecast has two halves, and they can fail separately

**A prediction was made that a yield rate would fall as the work moved from domain tests to architecture
tests.** The shift happened exactly as predicted. **The rate did not fall.**

**So the MECHANISM was right and the CONSEQUENCE was wrong** — and the correct disposal is to **withdraw
the consequence, keep the mechanism, and say which is which.**

**The reason the consequence inverted is worth more than the forecast: AN ARCHITECTURE TEST IS EASIER TO
CITE THAN A DOMAIN TEST, BECAUSE IT HAS TO SAY WHAT STRUCTURAL CLAIM IT MAKES.** A name like
`Employee_is_ordered_after_company_and_branch_and_history_after_employee` states its criterion almost
verbatim; `A_transfer_moves_the_employee_and_appends_exactly_one_record` **can mean three things.**

**The practical consequence: when mapping tests to specifications, START WITH THE ARCHITECTURE SUITE.**

**And the discipline generalises past forecasts. When a prediction fails, separate WHAT YOU EXPECTED TO
HAPPEN from WHY YOU EXPECTED IT TO MATTER.** Dropping both is how a useful model gets discarded with a
wrong conclusion attached to it; **keeping both is how a wrong conclusion survives on a good model credit.**

## Cite one test twice rather than splitting a single assertion

**Two criteria were both pinned by one test, because the assertion is a single exact expected list naming
both entities.** **Splitting it would have produced two tests each asserting half of one list — weaker than
the one that exists, and asserting something nobody specified.**

**This is the mirror of the superset disposal.** A superset cites one broad guard for a narrower criterion;
this cites one test for two criteria. **Neither is a compromise. Both are refusals to manufacture a shape
the code does not have, in order to make a mapping look tidier than the thing it maps.**

## A residue is evidence about the search, not about what is left

**A sweep set aside the criteria it could not resolve and called them the residue. The next pass resolved
six of seven, at the highest rate of the whole sweep.** They were never the hard remainder; **they were the
part a wrong search order had skipped.**

⚠ **The word does the damage. Calling a leftover set the *residue* attributes its difficulty to the items,
when the only thing actually demonstrated is that one method failed on them.** A forecast built on that
word then predicts the tail will be slow, and cannot see that the tail is slow only while the method is.

**So when a remainder resists, change the search before concluding anything about the remainder.** Here the
change was a ruling written the previous day - start from the architecture suite - and applying it for the
first time resolved three of the seven in one command.

### And withdrawing a model needs a stopping rule, or the split becomes unfalsifiable

**The same forecast was refuted twice before and survived both times by keeping its mechanism and dropping
its consequence.** That is the honest disposal, and it is recorded a section above. **On the third
refutation it was withdrawn entirely - mechanism included - because a model that gets a fresh consequence
after every failure is being fitted to the number rather than tested against it.**

⚠ **A prediction has two halves that can fail separately; that licenses ONE careful split, not a standing
right to keep the half you like.** **Decide in advance how many corrections the model gets, or it will
never be wrong about anything.**

## A pointer only counts in the direction somebody reads

**Two criteria named their own tests in prose: *verified by executable architecture guards*, *the two
architecture guards assert*. The tests named no criterion at all.**

⚠ **The link existed and was still useless, because it runs from the specification to the test, and the
question people actually hold is the other one: given this test, what does it prove somebody promised?**

**This is the strongest form of the citation case so far, precisely because somebody TRIED.** The failure is
not forgetfulness. **A one-way link looks like a link from the side that has it, so the missing direction is
invisible to exactly the person who would otherwise add it.**

## An unscoped absence criterion is falsified, not violated

**A criterion read *“No route, command, handler, permission, or table exists for rehire, employee
documents, import, or export.”* The decision it implements defers those requirements **out of this
package**, and goes on to describe what a future import and export must satisfy.**

⚠ **So the criterion bans product-wide what its own decision merely places elsewhere.** The day another
package builds the subject, the criterion becomes **false** — and nothing was violated. **The team that
did the right thing takes the failure, and the only available fixes are to weaken a guard or to argue with
a spec that was never meant to say this.**

**The tell is available at writing time and costs one comparison: the two criteria immediately above it,
deferring the same way, both said *“FP-006 introduces no…”*.** An unscoped absence sitting between
scoped ones is a slip, not a stricter intent.

### Read a criterion against the decision it cites before accepting its scope

**A criterion is an implementation of a ruling, and it can be stricter than the ruling.** The reflex when
one looks unsatisfiable is to treat it as a boundary question needing an owner. **Check the citation first:
here the governing decision was in the same package, already scoped, and settled it outright.**

## The suite that holds a structural claim is chosen by the mechanism, not by the package

**A criterion in the employee package was pinned by a test in the branch-session architecture suite.** A
search scoped to the feature's own module cannot reach it at any level of effort.

**A structural claim is asserted where the structure lives.** Feature-scoped searching assumes the two
coincide, and for architecture tests they routinely do not — so run the architecture sweep across the
whole test tree, then narrow.

### The evidence for architecture-first, and why the confound runs the right way

**Two passes ordered architecture-first averaged 0.86 citations per criterion examined; the three before
them averaged 0.57.** Small numbers — but **the new method was applied to the leftovers of the old one**,
the set already demonstrated to resist it. ⚠ **A method that wins on its predecessor's residue is not
winning on an easier sample**, which is the one confound that would otherwise explain the gap away.

## A criterion can be wrong in either direction, and the ruling it cites is the fixed point

**Two acceptance criteria were corrected on the same day, in opposite directions, and both were settleable
from the decision printed in their own third column.**

- **One stated an ABSENCE WIDER THAN ITS RULING**: it banned a subject product-wide where the decision
  merely deferred that subject out of the package.
- **One stated a COUNT NARROWER THAN ITS RULING**: it said *“all five tables”* where the decision says
  *“every tenant-owned entity”*, and the package had since grown to seven.

⚠ **A criterion is an IMPLEMENTATION of a ruling, and an implementation can be wrong.** The reflex on
finding one that does not match the code is to treat the code as suspect, or to escalate the criterion as
a boundary question. **Look at the ruling first. It is the fixed point, the lookup costs one search, and in
both of these cases it settled the matter outright.**

### A counted form of a universal ruling has a shelf life the ruling does not

**The correction was NOT to write *seven*.** Seven goes stale exactly as five did. **When the governing
decision is a universal — *every tenant-owned entity joins the manifest* — a criterion that counts the
members has converted something permanent into something dated, and gained nothing by it.**

⚠ **The failure it produces is worse than a plain error: the property still holds, so nothing breaks, and a
reader auditing the criterion against the code finds a mismatch WITHOUT ANY WAY TO TELL WHICH SIDE IS
WRONG.** Note that the guard here was count-free all along — **the specification was the weaker artefact,
not the test.**

## Prior work is a liability when its perishable half is the half you need

**A package was recommended as the cheapest place to start because a prior item had already measured it.
The measurement is what made it expensive:** 23 of its 54 criteria are *correctly* uncitable — most of
them because nothing implements them yet — **so the work begins by re-establishing which 23 to skip.**

⚠ **Those are absence claims, the class that rots first**, in a package that had gained code the day after
a document called it unimplemented. **A prior split reduces cost only if it can be trusted WITHOUT
re-checking. Otherwise it is not a head start; it is an extra step, with a plausible answer already written
into it.**

**The alternative chosen instead was smaller and completable — and a package that can be CLOSED is worth
more than a bigger one that cannot, because the closing is what converts a sweep into a fact.**

### A floor catches the collapse, not the drift

**A second test suite, by a different author with no shared vocabulary, reached the
control-inside-the-arrangement pattern independently — asserting a population is at least twelve before
checking every member of it.** That convergence is the best evidence the pattern is discovered rather than
invented.

⚠ **Recorded with its limit, which the pattern's other instances do not share: these are FLOORS.** A
reflection query that silently returns thirty of forty columns still passes a floor of twelve. **A floor
proves the query found SOMETHING; only a cross-check proves it found EVERYTHING.**

## A sweep with a control on its positive claims has none on its negative ones

**A citation sweep runs a control every pass: every cited id must exist in the specification, and the count
of dangling citations is reported.** ⚠ **Nothing whatever checks a criterion recorded as UNRESOLVED.**

**So the errors are asymmetric by construction.** A wrong citation is caught by a machine on the next pass.
**A wrong disposal is accepted on the strength of the reading that produced it, and it is invisible
afterwards, because *unresolved* and *genuinely unpinned* look identical in the record.**

**The remedy is cheap and belongs before the disposal, not after it: SEARCH THE MECHANISM BEFORE RECORDING
A CRITERION UNRESOLVED.** The symbol the criterion is about, and its call sites — not only test names.

### The worked example, and why a name search could not have found it

**A criterion said *"the compensation in force on a date is the record with the greatest effective date not
after it"*. Two tests were found, both asserting what happens OUTSIDE the range, and the criterion was
recorded unresolved with the reasoning that neither asserts the rule itself.**

⚠ **The pinning test was four lines above them in the same file**, named *the record in force is the latest
one not after the date* — the criterion almost verbatim, four positive assertions including the boundary
date. **One command — grep the domain method across the test tree — returns seven call sites, four of them
positive assertions.**

**The failure is a familiar one wearing new clothes: the observation was TRUE of the tests that were read,
and the conclusion quantified over the file.** ⚠ **A true statement about the members examined, generalised
to the set — and a name search could never have corrected it, because the pinning test's name shares no
significant word with the two that were found.**

### And two boundary tests are only non-vacuous because of the test beside them

**Both of the tests found assert that the resolver returns NOTHING. A function that always returned nothing
would pass both.** They are sound precisely because the positive case sits beside them.

⚠ **So reading a guard without its neighbours can make a complete pair look like a missing rule** — the
inverse of the near-miss error, and the same cure: establish what the FILE asserts before concluding what
it does not.

## The strong controls are two-sided

**A floor asserts a population is at least N before checking its members. A two-sided control asserts both
that the thing happened and that its counterpart did not** — succeed, then revoke, then fail; or stored AND
flagged AND surfaced.

⚠ **A two-sided control fails if EITHER side moves. A floor fails only if EVERYTHING does.** Both are worth
having and they are not interchangeable, and the vocabulary should not blur them: **a floor proves the query
found something; only a cross-check proves it found everything.**

### Say which kind of claim you are making, before it is tested

**An expectation stated as an expectation cost nothing when it turned out wrong: there was nothing to
withdraw, and no model was left standing on credit it had not earned.** The same content stated as a
forecast had previously cost three refutations and an eventual full withdrawal.

**The label is not modesty. It sets in advance what being wrong will cost**, and it can only be applied
honestly before the result is known.

## Ratifying an unchecked disposal makes it permanent

**A sweep recorded a criterion unresolved. The architect read the report, agreed, and wrote that it should
stay uncited.** ⚠ **The criterion was pinned, by a test found later with one search.**

**The endorsement is the part worth studying.** A disposal in a report is a reading, and its author will
revisit it — this one did, twice over, once the method improved. **A disposal repeated by the architect is a
RULING, and nobody revisits a ruling.** ⚠ **So an unchecked negative claim is durable, and ratifying one
makes it permanent.**

**The specific error was smaller than it looks and worse than it sounds: the criterion had four clauses, and
the ratification reasoned about exactly one of them** — *nothing asserts the ORDER, only the scope* — **and
then disposed of the whole criterion.** ⚠ **Quantify what you actually examined before endorsing an
absence, or the endorsement covers ground the reasoning never reached.**

### And the ratified clause was right, which is why the citation must stay partial

**The recovered test sorts the history itself before selecting from it, so it would pass unchanged if the
API returned those records in arbitrary order.** **The *returned in effective order* clause really is
unasserted — and the new citation's own body is the proof.**

**So the criterion is PARTLY pinned. Recording it as pinned would replace one wrong disposal with another,
in the opposite direction and with a citation to make it look settled.**

## Improving an instrument converts the old instrument's results into a liability

**A search method was corrected mid-sweep. Two criteria immediately came back wrong.** ⚠ **Every earlier
result of the old method now carries the same unknown error rate — and the honest statement is that the
nine remaining unresolved must be ASSUMED wrong at that rate until re-checked.**

**That is a cost of the improvement, not a confession.** ⚠ **The alternative — quietly applying the better
method going forward — leaves a body of results whose quality nobody can state, and makes the sweep's
totals a mixture of two methods with no way to tell which produced which row.**

**Name the affected population when you change a method. Here it was nine, and knowing the number is what
makes the re-check schedulable instead of aspirational.**

## Naming a failure mode confers no immunity from it

**A message corrected another window's error — reading one test, finding a clause absent from it, and
concluding nothing asserted that clause — and made the identical error two sentences later, about a
different clause of the same criterion.** ⚠ **The diagnosis was correct, current, and the author's own. It
did not fire.**

**A criterion with four clauses is four claims. Every disposal that treats it as one is right or wrong by
luck**, and the luck runs out silently, because a single citation makes the whole criterion look settled.

**The instance is worth keeping precisely because nothing was missing from the process:** the rule existed,
was written down, was being applied to somebody else's work in the same breath. **What was missing was an
instrument, and no amount of understanding substitutes for one.**

## A negative claim cannot be controlled, but it can be made cheap to re-check

**The sweep's positive claims are challenged mechanically on every pass. Its negative claims — a criterion
recorded UNRESOLVED — are challenged by nothing.** ⚠ **The one over-claim that was caught was caught by a
SECOND READER disagreeing with a citation, and that does not scale: it worked because a single criterion
happened to draw two readers' attention.**

**Two measures do not replace the reader, and are worth taking anyway, because both convert the cost of
BEING the second reader from *read everything again* into *re-run one command*.**

**One: a disposal records the search that produced it** — the symbol, the command, the number of hits — so
a later reader falsifies it by re-execution rather than by re-reading. **A negative claim with no recorded
method is unfalsifiable in practice, whatever its author intended.**

**Two: a citation names the CLAUSE it satisfies, not only the criterion.** ⚠ **Under that rule the
over-claim here was not possible to write down: closing a four-clause criterion would have required naming
the clause whose only evidence was a sort the TEST performed itself.** The enumeration stays a judgement —
this automates nothing — **but it converts a silent over-claim into an explicit one, and an explicit
judgement is the thing a later reader can disagree with.**

### An implemented-and-unasserted clause looks exactly like a missing one from the test side

**A criterion said a search with no status filter returns only the active and inactive. The handler asserts
it passes NO filter at all — so the exclusion, if it happens, happens downstream.** ⚠ **It does: the read
service defaults the status set when the caller names none. The behaviour is real and nothing asserts it.**

**The distinction is not academic. A missing behaviour is a defect and a missing assertion is a test gap,
and the test side cannot tell them apart** — which is why the disposal has to reach the production code
before it says which one it found.

## The exception gets the guard; the rule runs unasserted

**A criterion said a search with no status filter returns only the current employees. Nothing asserted it.
The ADJACENT case — that a terminated employee is still retrievable by identifier — had a test, a comment,
and a criterion reference.**

⚠ **The exception is the memorable case. The rule is the one that runs.** Somebody was once surprised that a
terminated employee remained reachable, and wrote a guard; nobody was ever surprised that a routine search
excluded terminated rows, so the behaviour every caller depends on went unasserted.

**The check is cheap and worth making a habit: WHEN A TEST EXISTS FOR THE EXCEPTION, ASK WHAT ASSERTS THE
RULE.** A guard on the surprising case is evidence that somebody thought about the pair — **and evidence
that they wrote down only the half they found interesting.**

### A rule applied where it does nothing is how a convention drifts

**A proposal to have every citation name the clause it satisfies was bounded rather than adopted whole: on
a single-clause criterion, naming the clause restates the criterion's only content.**

**The argument came from this repository's own history. Four trait keys carry criterion ids — `Decision`,
`Criterion`, `Acceptance`, `AcceptanceCriteria` — and none of them was unreasonable where it was
introduced.** ⚠ **A convention does not usually drift by being broken. It drifts by being applied
mechanically in places where it adds a line and no information, until the line is what people copy.**

**Bound the rule to the population that motivated it.** Here: name the clause when the criterion has more
than one, which is precisely the set where a single citation can silently close several claims.

⚠ **And record where the bound will fail: *single-clause* is itself a judgement — the same judgement that
missed four claims in a criterion that reads as one sentence.** When the count is arguable, enumerate.

## An anchor chosen by content can land in the wrong structural position

**An insertion matched the right text and landed between an attribute and the declaration it decorated. The
compiler refused it in seconds.**

**The lesson is not about that language.** ⚠ **Every document edit made from a script uses the same
mechanism — find a heading, insert before it — and prose has no compiler.** A markdown insertion that lands
inside a table, between a heading and the paragraph that qualifies it, or after the closing fence of a code
block, produces a file that renders and is wrong.

**So read the diff for STRUCTURE, not only for content**, and prefer an anchor that is unique and
structurally unambiguous over one that merely matches.

## The right test, the same code path, a different property

**A criterion required that a refusal disclose nothing about the ownership dimension it enforced. An
existing test drove the same handler, through the same code path, with the same exception type — and
asserted the status code and the correlation id.** ⚠ **It never asserted what the body does NOT contain,
and absence was the entire content of the criterion.**

**The existing test is not weak. It is about something else.** Every earlier near-miss in this record was a
wrong subject, a wrong state, or a convincing name over a narrower body. **This one is the right event and
the wrong property, which no amount of reading test NAMES can distinguish.**

⚠ **And it is why coverage cannot find these: both tests execute the same line.** The path was fully
covered while the claim was entirely unasserted. **The only instrument that reaches it is reading the
criterion and asking what it FORBIDS — a question a passing test never raises.**

### A claim about absence needs a test written for absence

**A test that asserts what a response contains cannot speak to what it must not contain**, and the two look
identical in every report that counts tests, lines, or branches.

**When a criterion's content is a prohibition, the test has to name the thing that must not appear** — and
then assert something POSITIVE alongside it, or an empty response satisfies the prohibition perfectly.

## One string, two audiences, and only one of them is the criterion's subject

**A boundary threw with a message naming the dimension it enforced. That looked like a disclosure hazard
and was reported as one.** ⚠ **The message never reaches a caller: the handler maps the exception to a
generic title and writes THAT, never the exception's own text.**

**The message is correct where it lives.** Developers read it in test failures and logs, where naming the
dimension is the entire point. **The criterion is about what a CALLER sees.**

⚠ **So a true observation about a string implies nothing about disclosure until the string has been
followed to a channel.** The hazard is a property of the path, not of the text — **and the check is one
search: find where the string is written, not where it is thrown.**

## A criterion that indexes other criteria is verified by verifying them

**One criterion stated the whole creation outcome in a sentence: a nonempty identifier, a trusted tenant, a
trusted company, a stamped branch, a normalized number, an initial state.** ⚠ **Every one of those clauses
is specified again, on its own, in a criterion below it.**

**It cannot be cited honestly.** Attaching it to one test presents a summary as a single assertion;
attaching it to six repeats what the six already say. **Left visibly uncited with the reason recorded, it
is accurate. Given a manufactured mapping, the totals look better and the record is worse.**

**Mark the roll-up in the specification, so the next reader does not re-derive this.** ⚠ **And mark it
WITHOUT listing the test names: say where the mapping lives and when it was made.** A list of test names
inside a specification goes stale the first time somebody renames one — **the durable half belongs in the
document, the perishable half in the dated artefact.**

### An untested member of a generically-guarded set

**A guard was written over a dimension rather than a type, and it is asserted for two aggregates and not
for a third.** The guard almost certainly holds for the third: nothing in it is type-specific.

⚠ **That is neither *pinned* nor *unguarded*, and both of those labels would be a lie in a different
direction.** *Partly pinned* is the accurate one, and the remedy is usually one more case in the theory
that already exists rather than a new test.

## Close a set of criteria together when they share a mechanism

**The last six criteria of a package were closed in one pass rather than one at a time, and a single search
resolved the same clause in three of them** — they state one prohibition over three dimensions, and the
command they constrain has one parameter list.

⚠ **Examined serially, that search would have been re-derived three times, and three separate readers of
the record would each have seen a criterion resolved by an argument they could not see the shape of.**

**The grouping is by MECHANISM, not by number or by adjacency.** It is the constructive form of the
residue lesson: **the order and the grouping of a search are properties of the method, and they decide the
result more often than the difficulty of what is being searched for.**

## A grouping is a search strategy, not a licence

**Sorting a set of unresolved criteria into mechanism groups before searching is a real improvement: one
search resolves several criteria that state the same rule from different sides.** ⚠ **It also creates a
hazard that the ungrouped method did not have.**

**A group is an argument that several criteria SHARE A MECHANISM. Once the search returns a test, the group
starts to look like a reason to cite that test for every member** — and a criterion that sits in the group
for a different clause gets cited on evidence that never touched it.

**The discipline is unchanged by the grouping: the group decides the ORDER of the search, and the BODY
decides the citation.** ⚠ **A group that justifies citations is the name-match problem with extra steps —
the same false confidence, arrived at by a better route.**

### Stopping with the grouping done costs nothing; stopping mid-search costs the search

**A pass formed seven groups and searched two.** The five unsearched groups are not wasted work: **the next
pass begins from named mechanisms rather than loose numbers.**

**A partial product that survives the interruption is worth more than a partial search that does not.** The
grouping is state, written down and re-readable; **a half-finished search lives in the searcher's head and
evaporates.** ⚠ **Prefer the order of work that leaves a durable artefact at every stopping point** — it is
the same argument that makes a per-citation activity safe to interrupt, one level up.

## A control arrived at independently four times is a fact about the problem

**Four test authors, in four packages, with no shared vocabulary, wrote the same shape: assert the property,
then assert something that fails if the population is empty.** A balance assertion beside a *greater than
zero*; a refusal beside a preceding success; a floor beside a whole-population check.

**That is not a house style, and it does not need a champion.** ⚠ **It is what the problem forces on anybody
who notices that an empty set satisfies a universal claim perfectly.**

### And the second site of the mechanism usually does not have it

**The same balance equality is asserted in two places. One carries the positive control; the other does not,
and an empty line set satisfies it — nought equals nought — under a test name promising the journal
balances.**

⚠ **The fix is one line copied from the other test, and the cheapness is the trap: there is no pause in
which to ask where else the mechanism lives.** **When a control is found missing at one site, enumerate the
sites before fixing the one in front of you.**

## A plant is informative only where the failure is far enough from the defect

**The planting rule — break the code deliberately, confirm the test reddens, restore — has an exemption, and
it is not laziness.** ⚠ **Where the assertion IS the mechanism, the plant proves nothing.**

**An assertion that a sum is greater than zero passes exactly when the sum is greater than zero.** Breaking
production so the sum is zero and watching the test redden demonstrates arithmetic, not that the test
guards anything.

**Plant where the path from the defect to the failure is long enough to be doubted** — a rule enforced three
layers away, a filter applied downstream, a guard reached by reflection. **Do not plant where the assertion
and the defect are the same statement**; the result is a tautology with the shape of evidence, and it costs
the same as a real one.

## The most dangerous name match is the criterion's own wording

**A criterion required a refusal AND that the response name the period concerned. The test is called
*approval into a closed period is refused and names the period*. Its body asserts the status code and the
problem code — the condition. Nothing asserts the period.**

⚠ **The name is not careless. It is the criterion's own sentence**, which is why a name search closes the
criterion as fully pinned in one step and no reviewer feels the need to open the body.

**The tell is available without reading much: the ARRANGEMENT supplies the value the name promises** — the
period name is constructed three lines above — **and nothing reads it back.** A test that sets up a
distinguishing value and never asserts on it is either incomplete or the value is decoration.

⚠⚠ **CORRECTED 2026-08-31, AND THE CORRECTION IS THE BETTER FINDING.** This section first read: *"Its
sibling fourteen lines below asserts exactly the corresponding value — one file, one convention, two
criteria of the same shape, one of them complete, so the presence of a convention is not evidence that it
was followed."* **That contrast was false.** The sibling asserted a status and a problem code and nothing
else; **the assertion on the named element lives in a DOMAIN test, one layer down, which constructs the
error and reads its message.** The comparison crossed two layers without saying so, and it made one
endpoint look careless beside a diligent neighbour.

⚠ **What was actually true is stronger: NO API test in either file asserted a named subject, because none
of them could.** The problem document carried a code, a correlation id, a resource key and a field name —
and a field carries the NAME of an input, never a VALUE. **A refusal that had to say WHICH period, WHICH
element, WHICH account had no channel to say it through.** Those tests asserted everything that was
assertable when they were written.

**The audit's rate says the same thing without ambiguity: of eleven such tests, the SEVEN whose promised
value is a compile-time constant all kept their promise, and the FOUR whose promised value is a runtime one
did not.** ⚠ **A perfect split along the line of what the transport could carry — which is a fact about the
transport, not about the authors.**

### And the shape gives a cheap audit

**Tests whose names promise that a message NAMES something are enumerable.** Search the test tree for the
phrase, read each body, and check that the value the name promises is asserted rather than merely arranged.
**ELEVEN such names exist here across eight files** (the first count of ten was mine and was short by one — two files carry more than one) — a search and ten bodies, for a shape that no coverage
measure and no name search can see.

## When a transport gains a field, the backlog it creates is the claims that were previously unsayable

**Eleven tests promised, in their names, that a refusal names the thing it concerns. Seven kept the
promise; four did not** — ⚠ **and the split is exact: every promised value that is a compile-time constant
was asserted, and every promised value that is a runtime one was not.**

**The four were not careless.** The problem document carried a code, a correlation id, a resource key and a
field name, and a field carries the NAME of an input, never a VALUE. **A refusal that had to say which
period, which element, which account had no channel to say it through.** Those tests asserted everything
that was assertable when they were written.

⚠⚠ **Then the transport gained a detail field, and nothing came back for them.** **The assertions a new
capability unblocks are invisible: the tests that want them already exist and already pass.** Nothing is
red, nothing is reported missing, and no coverage or citation instrument points at the set.

**So a capability change has a second half that nobody schedules.** ⚠ **When a transport, a contract or an
API gains a field, the work it creates is not only the code that fills it — it is the set of claims that
were previously unsayable, and that set is discoverable only from the intentions people wrote down while
they could not act on them.** Test names, comments and criteria are where those intentions are.

### A test that asserts a value must make the value unobtainable from anywhere else

**One of the four assertions could be satisfied without the code under test doing anything right.** The
fixture named the closed period with the same string as the run's own period, and the handler falls back
from one to the other — **so a handler ignoring the closed period entirely would have produced the expected
text.**

**The arrangement had to be changed before the assertion meant anything, and the PLANT is what exposed it:
the deliberate break did not redden the test.** A plant that fails to fail is not a wasted step; it is the
step working.

## A stated measurement decays into a licence

**A comment licensed a transport change with a measurement: no message in the product carries a runtime
value, zero interpolations, zero concatenations. It was true when written. Seven interpolated messages
exist now, in three files.**

⚠ **The guarantee never rested on it.** The control is a positive allowlist — client errors may explain
themselves, everything else fails closed — and it would be exactly as safe if every message interpolated.
**So this is a comment defect, not a security one.**

**It is still worth fixing, and the reason is what comments are FOR.** ⚠ **The paragraph a future author
reads before adding an error factory now reads as an all-clear about a condition that no longer holds** —
and the class it warns against, a new refusal shipping detail by default, has stopped being hypothetical.

**Write the licence as the RULE that makes it safe, not as the MEASUREMENT that happened to hold when it
was written.** A rule is checkable forever; a measurement is true until somebody commits.

## An absence at file level is visible only as an asymmetry between peers

**A guard existed in two modules, four times over, and in a third module the test FILE did not exist.** ⚠ **(Corrected 2026-08-31: *four times over* was counted from test NAMES. Two of the four assert the EMPTY-SET case, not the out-of-set one — see the section below on a mechanism whose two cases are not equally threatening. The file-level asymmetry stands; the equivalence did not.)** ⚠
**No search over the test tree can find that.** A name search reads names, a citation search reads
citations, and coverage measures lines that were written — **none of them has anything to read where
nothing was written.**

**The only instrument that reaches it is a comparison across peers: enumerate the implementations of a
mechanism, then ask which of them has a test at all.** ⚠ **Absence shows up as an ASYMMETRY, never as a
failure.**

### And the module that argued hardest had asserted the least

**The untested module is the one whose own source comment states the stakes most strongly — that elsewhere
a forgeable scope is an authorization defect, and here it is a personal-data breach.**

⚠ **The strength of a stated rationale is not evidence of coverage, and it may be worse than neutral:
writing the paragraph feels like discharging the duty.** **Where a comment argues hardest for a guard, check
hardest for the guard.**

### Defence in depth hides which layer is actually doing the work

**An inner read applied both the authorized set and the caller's requested company, which alone narrows to
the empty set — an empty success, not a refusal, and precisely what the module's own comment argues
against. It is harmless only because an outer resolver rejects the request first.**

⚠ **So the refusal has two guards and the degradation has none.** **When two layers cover one behaviour,
the test worth writing is the one that pins the layer that would be WRONG on its own** — because that is
the layer a future refactor removes without noticing.

## A silent no-match makes a plant look like proof of vacuity

**Three plant attempts matched nothing and three test runs reported green. Read as *the plant did not redden
the test*, that is evidence the new assertions are worthless — and the correct response to it would have
been to delete them.**

⚠ **The cause was mechanical: newline-delimited anchors against a file stored with carriage returns.
Single-line anchors had worked all day; the first MULTI-LINE anchor silently matched nothing.**

**Two habits saved three real assertions, and both are nearly free:**

- ⚠ **The edit script ABORTS unless it matches exactly once.** A script that used a forgiving replace and
  shrugged would have reported success while changing nothing.
- ⚠ **The instrument's output was read before the experiment's.** Both lines were on the same screen; the
  ORDER they were read in decided the outcome.

**A stale binary makes a plant look like it PASSED. A silent no-match makes a plant look like it PROVED
NOTHING. Both are quiet, and both die to the same habit: confirm the instrument acted before believing what
it reported.**

### A plant that does not compile is void

**Where the deliberate break cannot be made to build — it would require changing a signature the test's own
helper depends on — there is no plant to run, and pretending otherwise produces a green run that means
nothing.**

**Put the control inside the test instead.** Here a negative assertion is paired with a positive one over
the same reflection query, so a query that returns nothing FAILS rather than passing.

## Enumerate the implementers of a mechanism and compare their guards

**The comparison that found a missing test file between MODULES found a missing assertion between
AGGREGATES the same day.** One aggregate's tests asserted that no reactivate operation exists on it;
the sibling aggregate, bound by the same rule, had nothing.

⚠ **So the instrument generalises, and the granularity is a parameter.** Modules, aggregates, handlers,
endpoints, migrations — **wherever one rule has several implementers, a missing guard is visible only as an
ASYMMETRY between them.** It is never visible as a failure, and never inside the implementer that lacks it.

**This is the most productive instrument in this record.** Two findings in one day that a name search, a
citation search and a coverage measure were all structurally incapable of reaching — **because each of them
reads what was written, and the finding is about what was not.**

## A "nothing else changed" assertion needs a witness that anything changed

**An operation was asserted to leave the company, the branch, the identity fields and the assignment records
alone.** ⚠ **Every one of those assertions is satisfied perfectly by an operation that returns success and
does nothing at all.**

**Only asserting that the intended change DID happen separates *changed nothing else* from *changed
nothing*.** It is the two-sided rule specialised to negative claims, and it is the easiest control to omit,
because each individual assertion looks like a real check.

### And a count is not an identity check

**The assignment records were compared by the surviving record's destination, not by how many there were.**
⚠ **A transition that deleted one row and wrote another leaves the count unchanged** — so a count assertion
passes through exactly the defect the criterion is about.

**Where an assertion can be written over the identity of a thing, prefer it to one written over the number
of things.**

## Draw the plant from the shape of the code, not the shape of the assertion

**A guard was written generically: every tenant-owned entity whose owner changes after creation is refused.
The obvious plant is to delete the guard — and it proves almost nothing, because it would redden every test
of every aggregate at once.**

⚠ **The plant that was used excluded ONE aggregate from the shared check by name.** The new test reddens;
the sibling aggregate's test stays green. **That proves what the row existed to prove: the test pins the
rule FOR THIS AGGREGATE, not the guard in general.**

**A generic guard's invited failure is a per-type exclusion, not a deletion** — and it is what a later change
actually looks like. **The plant should be the mistake the design makes easy, not the one the assertion
makes obvious.**

### Assert the state as well as the refusal

**A test that only asserts a throw passes on a guard that throws AFTER writing** — a different and worse
defect than one that does not throw at all, and one a throw-only assertion cannot distinguish.

## A census over mentions can be moved by writing about the subject

**A coverage question was answered by counting which types appeared in test files. Every implementation was
referenced, so the class looked closed.** ⚠ **Then a commit added a COMMENT naming two of those types in a
third module's test file, and the census moved — in the direction of looking better covered, on a file that
asserts nothing about them.**

**A reference count reads prose.** It cannot separate an assertion from an explanation, and **prose is
exactly where people write about the things they consider important**, so the bias runs toward the subjects
that most deserve scrutiny.

⚠ **Measure what only the BEHAVIOUR produces.** Here the strong instrument was the error constant the
refusal returns: a test that does not exercise the refusal cannot name it, and no comment plausibly would.
**Every real gap was found by that grep, and none by the type-name census.**

### Verifying existence is not verifying equivalence

**Four tests were reported as the same guard in two modules. Their names were checked; two of them arrange
the empty case, not the out-of-set case, and assert something materially weaker.**

⚠ **The claim *this guard exists here too* is a claim about a BODY.** Checking that a plausible name exists
answers a different question, and the answer looks identical in a report.

## A mechanism with two cases can have the safe one asserted and the threatening one not

**A scope refusal has two shapes.** An EMPTY authorized set means *this caller reaches nothing*. An
OUT-OF-SET identifier means *this caller reaches something, and not that*. ⚠ **Only the second is what an
attempt to widen a scope produces. The first cannot be provoked by a caller at all.**

**A module asserted the first and not the second, under a test name that reads like coverage of the whole
mechanism** — *refused rather than served an empty page*. **The name is accurate about its own body. It is
the MECHANISM that has two cases, and nothing in the name says which one is inside.**

⚠ **So when a guard protects against an action, ask what the ATTACKER'S input looks like, and check that
the test arranges THAT.** A test arranging the degenerate case exercises the same code and proves the wrong
half.

## When a comment and the code disagree, ask whether the comment describes something worth having

**Two comment-versus-code mismatches in one day resolved in opposite directions, and the test is the same
question each time.**

**One comment stated a MEASUREMENT — no message in the product carries a runtime value — which had decayed
into falsehood while the code stayed correct.** **The measurement was never the guarantee: fix the comment.**

**The other states a DESIGN INTENT — that a refusal is distinguishable, so an operator can tell which grant
is missing — and the code delivers it on one path and not the other.** ⚠ **That is worth having: somebody
debugging a permissions problem is otherwise sent to grant the wrong thing. Fix the code.**

⚠⚠ **And do not pin the defect while it stands.** A test written against the wrong behaviour, to make the
suite describe reality, converts a defect into a requirement — and the next person to fix it will have to
argue with a green test.

## Membership in a rule is decided by its premise, not by a signature that resembles it

**A population was built by searching for methods named `Activate` and `Deactivate`. It was wrong in both
directions — two members had one method and not the other, two more were missed — and the deeper error was
not the pattern.**

**The rule was *no separate reactivate operation exists*, and its stated reasoning is that a created entity
is ALREADY ACTIVE.** ⚠ **An entity created in a pending state, carrying all three methods, is CORRECT to
have a reactivate: its premise is false, so the conclusion does not apply to it.** A signature search cannot
see that, and a signature search is what most population questions start as.

⚠⚠ **And matching the SHAPE of a rule is not membership in it.** One aggregate matched exactly — created
active, no reactivate — and is still out, because its activation methods are deliberately unconditional and
its package's criteria say nothing about lifecycle. **Where the shape fits and no rule claims it, the
missing thing is a CRITERION, not a test.**

### A correction is a claim like any other

**The correction to that population was itself incomplete in both halves.** ⚠ **Re-running the enumeration
is cheap and it is the only thing that settles it; accepting a correction because it corrects YOUR error is
the same deference that let the original stand.**

## The gap can be a structural class rather than a list of names

**A guard applied to an interface with 42 declaring types, three of which were asserted. The obvious remedy
is a grid: 39 more tests.**

⚠⚠ **The 42 divide into 26 aggregate roots and 16 CHILD entities, and all three asserted types are roots. A
guard that walked only roots would pass every one of them — and every further per-type test drawn from that
same class.** **The risk was never per-type. It was one `if`, one class, one test on a member of the class
nobody had tested.**

**So before filling a grid, ask what the members have in common that could make the whole column pass at
once.** ⚠ **A per-member sweep over a population that shares a failure mode measures the same thing N times
and finds nothing the first member did not.**

### And choose the member that isolates the mechanism

**The child chosen was the one NOT covered by a second guard.** On the others, an append-only check runs
first and the save is refused for a different reason — **the test would have gone green while proving
nothing about the rule it names.**

## A test of WHICH mechanism refuses must assert the refusal's identity, not its type

**A test asserting only that an operation throws went green on the wrong guard.** Three guards sit on the
path; the arrangement was missing a company context, so the company guard refused before the guard under
test was ever reached.

⚠ **In a test whose entire subject is which mechanism fires, the exception TYPE is not the assertion — the
identity is**, and the cheapest identity available is usually the message. **Otherwise the test passes
whenever anything at all refuses, which on a well-guarded path is nearly always.**

## Test at the level the mechanism operates at

**A guard over 42 entity types looked untestable as a set, because every aggregate assigns its keys and
invariants through a factory with a different signature. Constructing 42 valid aggregates is a project;
that is what made a grid of hand-written tests look unavoidable.**

⚠⚠ **The guard never sees an aggregate. It inspects a TRACKED ENTRY.** So the test enumerates the types
from the model, creates each without running any constructor, attaches it, and moves the owning
identifier — **and the construction rules that dominated the estimate are simply not the test's problem.**

**Before accepting that a mechanism can only be tested per-instance, ask what it actually reads.** A rule
that operates on a representation — an entry, a descriptor, a model node, a serialized form — can be tested
against that representation, and the population then costs one loop.

### A path with several guards can only test the last one if you enter below the others

**Three times in two days a test went green, or would have, because an EARLIER boundary refused first:** a
missing company context refusing before the tenant guard; an append-only check refusing before the
ownership check; and, at 35 types, most of them refusing on company-ownership before reaching the rule
under test.

⚠ **On a well-guarded path, *something threw* is almost always true.** Enter at the layer where the rule
under test is the first thing that can refuse — here a test-only context on the base class — **or assert the
refusal's identity so that the wrong guard is a failure rather than a pass.** Both were needed: the plant
that excluded one type failed with a DATABASE error, and only the message assertion turned that into red.

## A restore restores to the baseline, and the baseline is not your work

**A plant script broke the code, ran the test, and restored the file — to HEAD. The uncommitted fix the
plant existed to validate went with it.** ⚠ **The next plant's *anchor matched nothing* abort is the only
reason anybody noticed; otherwise the run would have measured ORIGINAL code and the result file would have
described a fix that was no longer in the tree.**

**The rule — stage the file before planting — was already written down, and had been applied to test files
and not to the source fix.** ⚠⚠ **A rule adopted in one lane and not carried across is the same failure as
a fix applied at one of two sites sharing a mechanism**, and it is quiet in the same way: the lane where it
was applied keeps working, so nothing signals the lane where it was not.

## Classify by the observed behaviour, not by a prediction of it

**A test had to separate the entities a guard protects from those it never sees. The first version predicted
the split from metadata — is this property part of the PRIMARY key — and was wrong, because *part of a key*
also covers alternate keys and identifying foreign keys.** ⚠ **The test then failed on a property it had
already classified as ordinary.**

**The version that works asks the mechanism.** Attempt the change; if the framework refuses before the guard
is reachable, that type is in the other class. ⚠⚠ **A classification derived from the mechanism's own answer
cannot miss a case nobody thought of; a predicate over metadata is a HYPOTHESIS about the mechanism, and it
fails exactly where understanding is thinnest.**

**This is *assert the identity, not the type* one layer up: ask what happened, not what should have.**

### Where a test classifies, assert the partition as well as flooring the population

**A floor stops a population silently emptying. It does not stop one BRANCH swallowing all of it.** A test
that checks a guard for some types and excuses others passes perfectly if every type drifts into the
excused branch.

⚠ **`guarded + excused == total`, asserted, closes that** — and it is not a hypothetical risk here: four of
seven members sit in the excused branch today.

## Rank by whether the failure is constructible, not by what it would cost

**Four types were nominated as the highest-consequence members of a population, on the grounds that a change
of owner on an authorization row is the worst version of the defect.** ⚠ **Two of them cannot suffer it:
their owning identifier is part of a key, so the change is refused before the guard is a question.**

**The consequence was ranked and the CONSTRUCTIBILITY was not.** ⚠⚠ **A guard demanded for a failure nobody
can build is a cost with no benefit, and the check is one question asked before the ranking: can this
actually happen?**

**It is also worth stating in the other direction: the key-immutable types are not a coverage gap. They hold
a STRONGER guarantee than the guard does** — structural rather than procedural — **and recording them as
untested would misdescribe the safest members of the set as the riskiest.**

## Taking a parameter is not applying it

**An architecture test reflects over a read interface and asserts that every method REQUIRES a scope
parameter. It passes, it is cheap, it is total over the interface — and it says nothing about whether any
method ever USES the scope it is handed.**

⚠⚠ **The concrete implementations of two of those interfaces are referenced by no test file and constructed
in no test host.** The stub registered by the API fixture is never displaced, because the real registration
lives in an infrastructure assembly the fixture does not call. **The line that filters by the caller's
authorized companies has never executed under test.**

**A structural guarantee reads as behavioural coverage, and it is the cheapest kind to write** — which is
exactly why it accumulates around the mechanisms people are most anxious about. ⚠ **Where a guard is
enforced by a value flowing through a method, the signature is the arrangement and the filter is the
subject; a test that stops at the signature has tested the arrangement.**

### The concrete class, not the interface, is the population

**An interface can be asserted, reflected over, stubbed and satisfied while its only production
implementation is untouched.** When asking whether a mechanism is tested, **enumerate the concrete types and
ask which of them a test ever constructs** — and compare across peers, because the module that has none
looks identical, from inside, to the module that has four.

## Search before waiting

**A run was queued to catch a named regression. The regression could not be produced: the suite substitutes
a stub for the component that was changed, so the changed code never executes there.**

⚠ **Ranking a risk is not checking it.** Twice in one session a risk was ranked by what it would cost if it
happened, without the one question that settles whether it can happen at all — **and the second time was
within an hour of writing that rule down.**

⚠⚠ **Knowing a failure mode confers no immunity from it. The habit that works is procedural: before waiting
on a hypothesis, spend the one search that would refute it.** A refuted hypothesis costs a minute; a
believed one costs the wait, and then costs again when the clean result is read as evidence of safety.

## Assert the exclusions, or they are re-derived as suspected defects

**Three reads in a module deliberately carry no company predicate: the chart of accounts is tenant-level by
ruling, and the entity has no company column at all.** ⚠ **They were nearly reported as a cross-company
leak, and were saved by one check.**

**A silent exclusion has no evidence attached to it.** The next reader sees a guard applied in five places
and absent in three, and the natural conclusion is the wrong one — **and the natural remedy for the wrong
conclusion is to add a predicate to correct code.**

⚠⚠ **So assert the exclusion positively: the second company SEES the shared row.** It costs one case, it is
two-sided by construction, and it converts *nobody filtered here* into *filtering here is forbidden*, which
is what the ruling actually says.

### A comment that explains a decision does not decay like one that states a measurement

**The type carried a comment saying exactly why it is tenant-owned and not company-owned, ending: *a reader
who sees only the absent `CompanyId` will not see that difference, which is why it is stated here*.** **It
worked, on the first reader who needed it.**

**Set against the same day's comments that had gone stale, stated a measurement that was no longer true, or
promised a distinction the code did not make** — ⚠ **the difference is that this one explains a DECISION and
its consequence.** A decision's rationale stays true while the decision stands; a description of the world
is true until somebody commits.

## Count the sites, not the methods

**Six methods applied one guard, and it looked like one risk. There are five sites: two shared helpers, and
three hand-written copies of the same predicate.** ⚠ **A test through either helper says nothing whatever
about the copies.**

**And in the neighbouring module there is no helper at all — every predicate inline.** **The unit that a
test covers is the SITE, and the unit a reader counts is usually the method.**

### Verify before consolidating, not the other way round

**The obvious response to three copies is to extract them, and it would make the test cheaper.** ⚠ **It also
changes five sites at once while nothing asserts any of them.**

**Verify first, consolidate after** — and then the tests that exist are exactly what makes the consolidation
safe. **A refactor undertaken to make testing easier, before the tests exist, spends the safety it is
trying to buy.**

## A distribution of symptoms usually has one cause at the composition point

**Six modules, and a guard was exercised in four of them and not two. Reported as a distribution — four
covered, two uncovered — it reads as two independent omissions and invites two repairs.**

⚠ **There was one cause: two of the six test hosts compose the module's registration extension and not its
infrastructure one, so the real implementation is never constructed and the fixture's stub is never
challenged.** Every number in the distribution follows from that single difference.

**When a population splits unevenly on a property nobody chose, look at what composes its members before
counting what is missing from each.** ⚠ **N repairs at the symptoms leave the cause in place, and the next
member added inherits it.**

### ⚠ And finding ONE cause is not finding THE cause (added an hour later, on a counterexample)

**The very next check found a second cause for the same symptom.** Two of the three uncovered members were
uncovered because their host never composed the real registration; **the third composes it correctly and
then registers a stub over the top.**

⚠⚠ **A single remedy aimed at the first cause would have left a third of the population untouched WHILE
LOOKING COMPLETE** — which is worse than the two-repairs reading it replaced, because a cause that explains
most of a distribution is convincing enough to stop the search.

**So the rule has two halves: look for the shared cause, and then CHECK IT EXPLAINS EVERY MEMBER.** A member
it does not explain is not noise; it is the second cause.

### And the diagnosis had already been written down, at one site

**One host carried a comment recording that this exact defect had been found there weeks earlier: the
infrastructure half was never called, so the module's routes were mapped and unreachable, and nothing in
the folder could tell.**

⚠⚠ **The failure mode was diagnosed, documented, and fixed — at the one site where it was noticed. Nobody
asked whether the other five had it. Two did.** **A written diagnosis is not a control: it makes the site
that has it safer and says nothing about the sites that do not, and the clarity of the writing does not
help, because the people who need it are not reading that file.**

## A reference count cannot separate reflection from exercise

**A census asked which test files name each implementation.** ⚠ **Of one type's four references, two are
architecture tests that reflect over it; another module's single reference is a route inventory. Neither
constructs anything.**

**And one module's host composes the real infrastructure and then registers a stub over the top anyway**, so
even a composition check would have passed while the concrete type stayed unexercised.

⚠ **Mentions, reflections, registrations and exercises all look identical to a grep.** The question worth
asking is the narrow one — **what constructs this and runs a method on it** — and it usually cannot be
answered by counting at all.

## A negative assertion needs a neighbour that stays positive

**A privacy redaction was to be tested two ways: the privileged caller sees the sensitive value, the
unprivileged one does not.** ⚠ **Both are satisfied by a service that redacts EVERYTHING for the
unprivileged caller.**

**The assertion that separates *redacts the sensitive one* from *redacts* is a third: a NON-sensitive row in
the SAME response stays visible.** The rule the code states is that sensitivity is a property of the TYPE
rather than of the REQUEST — **and only a mixed response can assert that at all.**

**Generalising: where a rule discriminates between members, the test needs both kinds present at once.** ⚠
**A test that arranges only the members that should be suppressed cannot distinguish a discriminating rule
from a blanket one**, and the blanket one usually looks safer, so nothing prompts the question.

### And the exemption is the clause a well-meaning change breaks

**One route deliberately does NOT redact: the protection exists for the subject of the record, and on that
route the subject is the caller. The permission that gates the administrative view is one no ordinary
employee holds, so applying the same rule there would redact a person's own medical absence from
themselves.**

⚠⚠ **It is ruled, it has a written rationale, and nothing asserts it.** **A future change reading *redact
sensitive types unless the caller is an administrator* would sound safer and would be adopted without
argument** — right up to the point where an employee's own sick leave shows as a nameless gap.

**Assert the exemptions of a protective rule as deliberately as the rule.** The failure they prevent is
invisible to everyone except the person harmed by it.

### Care in the implementation can put a behaviour beyond every cheap suite

**The redaction is applied in the SQL projection rather than after materialization, so that the value never
crosses the wire or reaches a query log.** ⚠ **That decision is right, and it is exactly why no in-memory
test could ever have covered it** — the behaviour only exists against a real database.

**When an implementation is careful in a way that moves it below the seam the fast tests run at, the
verification cost moves with it.** Recognise that at the time, or the most carefully written code in a
module ends up the least tested.

## Schedule the full run on a cadence, not on a hypothesis

**A long run was queued to catch a named regression. The regression turned out not to be constructible — the
suite substitutes a stub for the component that changed — so the search settled the question and the run
was, on its stated grounds, unnecessary.**

⚠⚠ **It found something else: eight baselines for the second build configuration were stale, and nothing had
compared the configurations in eight generations of the suite. There was no drift — and that is a result
nobody could have stated beforehand.**

**The merge gate runs one configuration and excludes the slowest suite. So the property the two-configuration
split exists to verify is precisely the one the merge gate structurally cannot see**, and it degrades
silently, because nothing that runs on every merge has any opinion about it.

⚠ **A run queued on a hypothesis is only ever scheduled when somebody has a hypothesis.** The value of a
periodic full run is in the questions nobody thought to ask — **which is exactly the value a
justification-driven schedule can never capture.**

### A correct action on a refuted premise earns no credit for the premise

**The row was right and its stated reason was wrong.** ⚠ **The value of an action and the justification given
for it are separate claims**, and conflating them is how a lucky call becomes evidence for a bad method.

**Say both: the reason was refuted, the action paid, and here is what it actually bought.**

## A first-execution run reports the first defect, not the count

**A test that constructs a production class for the first time found a query that could not execute. The
fix let the test reach the next site, which also could not execute.**

⚠ **The second defect was invisible until the first was cleared** — the run stops at the first throw, so
every later site on the same path is unmeasured. **The number of defects is not observable until the run
completes clean.**

**So report *at least one*, and say the enumeration is outstanding.** ⚠ **"One defect found" on a path being
executed for the first time is a statement about where the run stopped, not about what is wrong.**

### And enumerate the class by mechanism before claiming it is closed

**Two defects of one shape were found. The claim that there are exactly two rests on reading all 31 ordering
sites across the six equivalent services** — not on the two that happened to break. **Only those two order
over a client-constructed object; the rest order on entity properties, which translate.**

**The correct pattern was already in the product**, in the one service that several tests construct. ⚠ **The
divergence was not a knowledge gap. It was a feedback gap.**

## Untested and wrong are correlated, and the correlation is the argument

**Six equivalent services. Two had never been constructed by any test. Those two are the two that carried
defects — one of them twice, in a financial report.**

⚠⚠ **That is not a coincidence to be noted; it is the mechanism, and it converts an argument about coverage
into a measurement.** A structural test asserted that every method of the interface required the right
parameter: total, cheap, passing — **and silent about whether any method could run at all.**

**When a population splits into "exercised" and "never exercised", the second half is not a documentation
debt. It is where the defects are**, and the cost of finding out is one test that constructs the thing.

## A zero from an instrument never observed to fire is worth nothing

**A sweep reported that a defect class is empty across the whole tree: 109 candidate sites, four flagged,
all four verified as correct code.** ⚠ **That result is only meaningful because the same scanner was run
against the source as it stood BEFORE the two real defects were fixed, and it flagged both.**

**Every null result needs that step.** A scanner with a broken pattern, a wrong path, or a silent exclusion
reports exactly the same clean sweep as a working one — **and the clean sweep is the outcome everybody
wanted, so nobody looks twice.**

**Version control makes the control cheap: the pre-fix source is one command away, and a defect that was
real yesterday is the best possible known positive.**

### And a one-off sweep is not a guard

**The scanner's precision on a clean tree was zero true positives and four false ones.** ⚠⚠ **Shipped as a
permanent check it would meet every future reader with four failures that are all correct code — and the
natural response to a wrong failure is to weaken or delete the check.**

**The stronger reason not to ship it is that the behavioural control already exists.** A query that cannot
execute now fails a test that runs it against a real database. ⚠ **A source-shaped guard would duplicate a
behavioural one at worse precision** — and the two are not interchangeable: **the parser approximates what
the test demonstrates.**

**Answer the question once, record the answer, and let the artefact go.** A guard is a different artefact
with a different bar, and it earns its place by precision on the tree it will actually run against.

## A name is not an exercise, and a proxy's floor should be reported as a floor

**A census asked which production types no test constructs, using a syntactic search for construction. It
missed every type built with a target-typed `new(...)`** — including three the previous item had just
covered.

**Recounted with a deliberately generous test — a type is seen if its name appears anywhere in the test tree
— the residue fell from 41 to 7.** ⚠ **The generosity is the point: the number is a FLOOR, and it still
over-counts coverage, because a name in a test is not an execution.**

⚠⚠ **The complete instrument for *is this code ever executed* is coverage instrumentation, and nothing else
is.** Every syntactic proxy answers a different, narrower question — **so name the instrument that would
settle it, and say whether that instrument is available.**

### ⚠⚠ CORRECTED THE SAME NIGHT: THE PROXY WAS NOT A FLOOR, AND CALLING IT ONE WAS WRONG IN BOTH DIRECTIONS

**This section originally said to report such proxies AS FLOORS. Coverage was then run, and the proxy's
seven was wrong in BOTH directions, with OPPOSITE causes that do not cancel:**

- ⚠ **THREE it called dead are LIVE.** They are registered as `AddScoped<IInterface, Type>()` and executed
  through the container in end-to-end tests. **The caller names the INTERFACE, the container supplies the
  type, and the type's own name appears in no test file.** ⚠⚠ **EXECUTION THROUGH A CONTAINER IS NAMELESS.**
- ⚠ **TWO it called live are dead.** Their names appear in the test tree as **STRING LITERALS** — a filename
  in a source-reading architecture test, and a bare name in a ban list. ⚠⚠ **A REGEX OVER IDENTIFIERS CANNOT
  TELL A TYPE REFERENCE FROM A FILENAME IN QUOTES, so a test that READS a type's source is indistinguishable
  from one that RUNS it.**

**Two error directions with independent causes is not a bound. It is a DIFFERENT MEASUREMENT THAT HAPPENS TO
CORRELATE**, and describing it as a floor claims a guarantee it never had. ⚠ **Before calling a proxy a
bound, name the error it CANNOT make — and if both directions are available, it is not a bound in either.**

## Check for an unused affordance before building a seam

**Two types could not be reached from a test, apparently for want of a fixture seam. Both were already
reachable: the fixture held the context and the factory needed, as PRIVATE members.** ⚠ **Nothing had to be
built. The seam existed and nobody had walked through it.**

**The same day, three infrastructure assemblies were found carrying `InternalsVisibleTo` for the integration
test project — granted, unused.**

⚠⚠ **An affordance nobody uses is invisible in exactly the way a missing one is**, and the cost of the
confusion is a build: somebody adds a second seam beside the first, and now there are two.

**So before adding a seam, search for one.** The evidence is cheap — a field, an attribute, an existing
partial — and the search costs less than the smallest thing you would otherwise write.

### A binary measurement can be sound while its magnitude is meaningless

**A coverage report gave every type a hit count. The counts are not comparable: async method bodies compile
into generated state-machine classes which the report's noise filter strips, so a type's own entry counts
little more than its constructor and field initialisers.**

**Never constructed still reads zero, and constructed still reads more than zero** — ⚠ **the binary the
measurement was taken for is intact, and the number beside it is not a measure of how much ran.**

⚠⚠ **A report that answers one question will be read for the other one it appears to answer**, especially
when it prints a number. **Say which question it answers, in the report, beside the number** — and if a
comparison has already been published on the strength of the wrong reading, correct it where it was
published.

## A measurement validated only where the work happened is validated on the wrong sample

**A sweep reported its progress for sixteen passes with a text search for criterion identifiers in the test
tree.** ⚠⚠ **That search counts a criterion NAMED IN A COMMENT exactly as it counts one CITED IN A TRAIT.**

**The inflation is not spread evenly, and its distribution is the diagnosis: every package the sweep had
actually worked through is exactly clean under both counts, and every unit of inflation sits in a package
nobody has read.** ⚠ **Four packages read as having citations and have precisely zero.**

**The metric was correct wherever anybody had checked it, and wrong everywhere else** — which is why it
survived sixteen reports. ⚠⚠ **Where a measurement is spot-checked BY DOING THE WORK, the sample is exactly
the population least able to expose it.** Validate an instrument where nobody has been, or not at all.

### Fix the instrument, not the practice it mismeasures

**One inflating mention was a comment naming a neighbouring criterion to distinguish it from the one under
test. That comment does real work for the next reader.**

⚠ **The temptation is to ban the practice so the metric reads true.** The metric was the thing that was
wrong: **count strictly, and keep writing the comment.** A rule that degrades the artefact to flatter the
measurement has the two backwards.

**And where a strict and a loose count differ, report both.** The gap is itself information — here it named
the four packages nobody had swept.

## A diagnostic's remedy ages faster than its detection

**A precondition check aborted a run: free memory below a calibrated floor, exit before any suite started,
with the message citing the historical incident that motivated the floor.** ⚠ **The detection was exact.
The advice — *quiet the box (editors, browsers)* — named the wrong culprit.**

**The actual cause was eighteen compiler build servers holding about a gigabyte, accumulated across thirty
builds in one long session, doing precisely what they are designed to do.** One command released them, and
they respawn on the next build.

⚠⚠ **A detection measures the world; a remedy assumes WHO IS STANDING IN IT.** The floor still measures
correctly long after it was written. The advice was written for somebody sitting at a workstation with a
browser open, and the only kind of session that now runs this gate has been building all day.

**So when a diagnostic fires and its advice does not fit, the check is usually still right.** Fix the
sentence, keep the threshold — and prefer advice that names a mechanism over advice that names a habit.

### And a session can exhaust its own preconditions

**The work itself was the cause.** Thirty builds is a normal day's activity, and its accumulated cost
surfaced as an environment failure at the moment the longest job of the day tried to start.

⚠ **Anything that accumulates per action and is released only on demand will eventually block the action
that needs the most of it** — and it will do so at the worst moment by construction, because that action is
the one with the largest requirement.

## Opening a file for writing destroys it before the content is computed

**A script rebuilt a record file as `open(path, "w").write(header + read(other))`. The inner read failed.
The file had already been truncated: 1909 lines to zero.**

⚠ **`open(path, "w")` truncates on the CALL, not on the WRITE.** Every expression that computes the content
runs afterwards, and any failure in it leaves an empty file where the original was.

**Build the content first, then open.** Two statements instead of one, and the file is never open while
anything can still go wrong.

⚠⚠ **What made it a near-miss rather than a loss was that the file's last content was COMMITTED**, so
`git checkout --` restored it whole. **Commit often enough that a restore is a recovery and not a decision
about how much to lose.**

## A hand-listed guard excludes everything added after it was written

**Three criteria in one package were unpinned, and all three failed the same way: the guard that should
have covered them enumerates its subjects BY HAND.** A five-file allowlist. A predicate naming one entity
type. A test asserting one property of seven.

⚠ **Each was correct when written. None of them says anything about the package added afterwards** — and
nothing goes red, because a hand-written list cannot notice an omission.

⚠⚠ **CORRECTED ON MEASUREMENT: THESE LISTS HAD NOT GONE STALE. THEY WERE COMPLETE OVER THEIR OWN
POPULATIONS — eight of eight commands, twice, and a seven-of-eight whose exemption is documented.** **What
was narrow was the POPULATION, not the list: the guard's subject is one package's entity, and it always
was.** **A STALE LIST IS MISSING A MEMBER OF ITS OWN POPULATION; A NARROW GUARD HAS THE WRONG POPULATION.**
⚠ **Both are cured by deriving, and only the second explains why a sibling package's criteria can be
written on the model of an existing one while the coverage does not follow.** **Say which you have found —
reporting a narrow guard as a stale one names a defect that is not there.**

**The cost lands on whoever adds the next package: to be covered, they must find and edit every
hand-listing guard in the tree, and nobody has that enumeration.** ⚠⚠ **So the criteria get copied across
packages — they are cheap and they read as intent — and the guards do not.**

**The contrast was in the same sweep: a sibling guard DERIVED its subjects from the composed model, and it
covers the new package automatically, with nobody touching it.**

**Prefer derivation to enumeration wherever the model can answer.** Where a list is genuinely necessary,
**assert its SIZE against a derived count**, so that adding a type fails the guard rather than escaping it.

⚠⚠ **AND THE PREFERENCE IS NOW A MEASUREMENT RATHER THAN A TASTE.** In one package, four criteria were
written on a sibling package's model. **Three of them name guards that hand-list their subjects, and none of
those three travelled. The fourth names a guard that DERIVES its population from the composed model, and it
covered the new package without anybody touching it.** **Three against one, in the same specification, on the
same day.**

### And a partition needs its total asserted even when both halves are hand-written

**Two guards divided a set between them: one asserting a property of two members, the other asserting its
absence for the remaining six. Two plus six is exactly eight, which is the whole set today.** ⚠⚠ **A ninth
member joins NEITHER list, and both guards stay green.**

**Both halves existed. What was missing was the sentence tying them to the population** — `guarded + exempt
== total`, derived. **A partition is the one arrangement where completeness is free to assert and easy to
omit**, because each half looks like a finished guard on its own.

### Derivation cannot promote a structural guard into a behavioural one

**Asking which hand-lists could be derived produced a better answer than the question deserved: two could,
and the third's list could — while the criterion still could not.**

⚠ **Its second clause is about what a REQUEST does — that identifiers supplied in a body are ignored in
favour of the caller's own — and no structural guard, however derived, can reach it.**

**So derivation buys survival against new types, and nothing else.** ⚠⚠ **The next package inherits the
derived guard for free AND STILL OWES THE REQUEST TEST**, and a sweep that reports the guard as derived
without saying so leaves the behavioural half looking covered.

### The specification was copied and the guard was not

⚠ **This is not a near-miss in the usual sense.** Nothing was mislabelled, misread, or over-claimed. **The
test whose name says *employee* never pretended to cover anything else.** A criterion was written for the
new package on the model of the old one, and the work behind it was simply not done — **and the criterion
now asserts that it was.**

**When a package's criteria are derived from a sibling's, check each one against the guard it names, not
against the sibling's guard.** The wording travels for free; the coverage does not.

## A guard's population and its predicate derive independently

**Two guards looked like the same defect: each named a single type where a family exists.** ⚠ **One derives
in an afternoon and the other must not be derived at all.**

**The POPULATION — which subjects are judged — almost always derives from the model or the assembly.** The
copy-plan guard names one entity by hand while asking a question every entity can answer: *does this table
exclude the concurrency token it carries*. Derive the population and the guard simply covers everything.

**The PREDICATE — what is asserted of each subject — derives only if the property is genuinely uniform.**
The read-scope guard's predicate asserts **exactly one query root** and a **helper located by name**, then
inspects that helper's text. ⚠⚠ **Two sibling modules were measured and do not have that shape: one has two
helpers and three inline copies, another has no helper and seven inline sites.** **Deriving the population
would impose one author's structure on four modules and fail honestly-written code.**

⚠ **The tell is not visible in the shape of the list.** Both are a hand-named subject. The difference is
whether the assertion would still be TRUE, not merely runnable, for a subject that solved the problem
differently.

### The predicate fails to generalise when it rules a mechanism rather than a behaviour

**One root, a helper of a given name, three predicates in that helper's body — those are one author's
CHOICES.** The obligation is that every query is scoped. ⚠⚠ **A guard written against the mechanism can
only ever have one subject, because the mechanism is a choice and the behaviour is the requirement.**

**So widening a narrow guard has a precondition: rewrite the predicate to state the behaviour, or leave it
alone.** A guard that names an implementation detail is not narrow by accident; it is narrow by
construction.

### And check whether the behavioural half already exists before writing a structural one

**A real-database test already proved the property for one path.** That does not retire the structural
guard — **the behavioural test proves ONE query and the guard proves EVERY query root, including one added
next year** — but it changes what the guard is for, and it should be cited for what it covers rather than
left unmentioned.

## A rule demanding more detail creates pressure to invent it

**A status line reported only that a long job was running, which left its reader unable to tell a live run
from a stall. The rule was changed: report the LEG and the ETA, in the same one line.**

⚠⚠ **The next report named a leg that had not been observed.** The job was in an earlier stage entirely, and
the artefact that answers the question — a counts file listing the stages completed — was one read away and
was not read.

**Adding a field to a report does not add the observation behind it.** ⚠ **It adds a blank that the writer
now feels obliged to fill, and the cheapest filler is an inference from elapsed time, which is always
available and never wrong about anything.**

**The remedy is not to withdraw the rule.** The reader genuinely needs the leg. **It is that a field the
report demands must name the artefact it was read from — or be left explicitly empty.** *"Integration/Debug
per `counts.txt`"* and *"leg unknown, not checked"* are both honest; a bare stage name is not.

### ⚠ A truncated identifier is a blank of the same kind, and worse

**The same failure appeared in the other direction two hours later: a search's output was cut mid-token —
`…refuses_the_next_emplo` — and the name was completed from expectation and committed.** The completion was
plausible, adjacent, and wrong: the real name ended `_write` where the writer supplied `_read`, which
reversed a finding.

⚠⚠ **A truncated identifier is worse than an empty field, because a blank invites a question and a
half-name ANSWERS one.** It arrives looking like a measurement.

**So: WHEN OUTPUT IS TRUNCATED, THE IDENTIFIER IS UNREAD.** Re-run with an extractor that prints whole
tokens, or widen the cut — **never complete it from what it obviously must say.** The width that truncated
this one was 140 characters and the name ended at 141.

### ⚠⚠ And a display limit becomes a MEASUREMENT the moment the output is counted

**The same defect appeared once more, in its fourth form and in the other direction: a search was piped
through `head -5` and its result reported as five sites. There are six.** The sixth was a distinct entity
that the output never showed.

⚠ **`head`, `cut`, `-First`, `| head -3` are all RENDERING decisions, and none of them announces itself in
the number that comes out.** A truncated line loses a name; a truncated result set loses a member — **and
the second is worse, because a partial list still looks like a list.**

**IF THE OUTPUT WAS LIMITED, THE COUNT IS UNREAD.** Re-run with `wc -l` or an aggregate, and never report
the rows you happened to display.

## Editing a running script corrupts the run, not the file

**A long job was executing a shell script. The script was edited thirteen minutes in — a valid edit, syntax
checked, committed. Every expensive stage then passed, and the run died at the very end with a syntax error
on a line that is syntactically correct.**

⚠⚠ **A shell reads a script INCREMENTALLY, BY BYTE OFFSET. It does not load the file.** An insertion shifts
every offset after it, so the running interpreter resumes mid-statement and sees a fragment — a condition's
tail without its `if`.

**The file was never wrong. Exactly one process in the world held the corrupted view, and no static check
can see it**: the syntax checker reads the file, and the file is fine.

⚠ **And the failure lands at the END, after everything expensive has succeeded, because that is when
execution finally reaches past the shifted point. It is maximally expensive by construction.**

### A rule you cannot reliably evaluate is not a rule you can follow

**The obvious rule is *do not edit the script while it is running*.** ⚠ **One of the two parties could not
reliably tell when it was running** — its status check was minutes-granular, and it had recently reported a
stage it had not measured.

**So the rule that binds is the one that removes the hand from the file: the party who cannot observe the
run does not edit the script at all.** It writes the change; the party that owns the run applies it,
between runs. **A rule whose precondition you cannot check is an intention, not a control.**

### And the lock guarded the resource, not the code that reads it

**The gate held an instance lock, so a second gate could not start.** ⚠⚠ **Nothing protected the SCRIPT from
an editor** — the lock covers the database, the ports, the artefacts, and not the text being interpreted.

**A tool that can be modified while it runs should snapshot itself and execute the copy.** One line at
startup removes the entire class, and it costs a file copy.

## Audit identifiers by machine; a recollection is not an instrument

**Two records were checked for invented test names. One author re-read the names they REMEMBERED
publishing and found none. The other extracted every published identifier and diffed it against the source,
and found four wrong out of forty-two.**

⚠⚠ **The memory check cannot work, and not because memory is weak.** The identifiers that are wrong are
precisely the ones that read perfectly — a plausible completion or a small mutation survives rereading by
construction, because it is what the reader expects to see.

**The check is one pass: pull every backticked identifier out of the published record, concatenate the
source tree, and report the ones that do not appear verbatim.** ⚠ **It needs no judgement, no recollection,
and no knowledge of which entries were risky.**

### Two ways to publish a name that does not exist

**COMPLETION.** A search's output is truncated mid-token and the writer finishes it from expectation. The
result is adjacent and can reverse a finding — `_write` supplied as `_read`.

**MUTATION.** The writer capitalises part of an identifier for emphasis, inside the quoting that promises it
is verbatim. The name still reads correctly and no longer matches anything.

⚠ **Both produce a citation a reader greps and does not find, which is the only property that matters.**
**Inside the quoting, verbatim; put the emphasis outside it.**

### And a name that matches nothing is unusable whatever the cause

**The audit surfaces renamed tests and invented ones identically, and that is a feature.** A published
identifier that resolves to nothing cannot be checked by the next reader — **whether it was wrong when
written or right until somebody renamed the test is a separate question, and the record is broken either
way.**

## Refusing to work is not automatically rigour

**A long session built one discipline after another, and every one of them rewarded caution: do not cite
past a near-miss, do not override a floor, do not edit a script while it runs, do not report a count you did
not measure.** ⚠⚠ **Then a precondition failure was read as global when it was scoped, and both parties stood
down for two hours on work that was never blocked.**

**Standing down FELT like the disciplined choice.** It has the shape of every correct decision that came
before it — declining to act on insufficient evidence — **and it was itself an unevidenced decision.**

⚠ **A STOP IS A CLAIM ABOUT THE WORLD AND CARRIES THE SAME BURDEN AS A START.** *Nothing can be done* is a
statement of fact, checkable in the same way and by the same instruments, and it is the one form of
inaction that never looks like it needs justifying.

### The disconfirming evidence was published by the party that then contradicted it

**The tool announces the scope of its own decision, in the same line as the number:** *free physical memory
before Debug: 1954 MB (no floor: Integration is not in scope)*. **That line had been quoted verbatim into a
committed result file, from a green run, below the floor.**

⚠⚠ **So this was not a missing instrument, a hidden one, or an ambiguous one. It was read, quoted,
committed — and then contradicted by its own author.** **A fact stops being consulted once it has been
turned into a conclusion**, and the conclusion is what gets carried forward.

### And a shared premise stops being re-derived

**One party generalised from the case in front of them; the other adopted the generalisation and had no
reason to re-derive it.** ⚠ **A premise held by two people is checked by neither** — the first has already
decided, and the second treats the first's having decided as the evidence.

**The remedy is not more caution. It is that a claim which stops work must be stated as a claim, with its
scope, so that it can be contradicted by the same kind of measurement that produced it.**

## A warning addressed to somebody else is invisible in the file that contains it

**A script's header carries an explicit note about a shell hazard: a pipeline loses its exit status unless
the calling shell sets a particular option.** ⚠⚠ **Nine hundred lines later, the same script builds its most
important measurement through exactly that construct, and the only mention of the option anywhere in the
file is the warning itself.**

**The mechanism of the miss is better than forgetfulness.** ⚠ **The advice was correctly aimed OUTWARD —
at whoever invokes this script — so it never presented itself as a question about THIS file.** A warning
about how others should call you does not read as a warning about how you call others.

**So when a file documents a hazard, search the file for the hazard.** The note is evidence that somebody
understood it once, which makes the file MORE likely to contain an instance, not less — the author was
thinking about that failure while writing in that style.

### And silence needs every mechanism; the fix needs all of them

**Three separate things had to be true for a failed measurement to be invisible: the error text discarded,
the exit status discarded, and an empty result converted to a value worse than any real reading.**

⚠ **Fixing one leaves the other two.** Capturing the error text still leaves a pipeline that reports success
on failure, and a fallback that turns a partial read into an unconditional abort. **Where silence is
produced by a chain, the repair is the whole chain or none of it.**

### A finding gets more dangerous as it gets more elegant

**Three mechanisms stacked in three consecutive lines is a satisfying result.** ⚠⚠ **That is precisely the
moment it will be attached to the nearest unexplained incident** — and the incident here had a real
measurement behind it, not a failed one.

**Write the disclaimer while the elegance is fresh**, in the same paragraph as the finding, rather than
after somebody has drawn the conclusion it invites.

## A fallback must fail toward the safe outcome for ITS OWN consumer

⚠⚠⚠ **THIS SECTION FIRST READ *A SENTINEL MUST BE OUTSIDE THE DOMAIN IT STANDS IN FOR*, AND ONE LINE OF THE FILE THAT PROMPTED IT REFUTES THAT RULE.** Two consecutive lines, one variable, two different fallbacks: `${LEFT:-?}` for the message and `${LEFT:-1}` for the branch. **`1` is squarely inside the domain of a catalog count, and it is CORRECT** — an unmeasured reap must read as *not clean* and abort. **The rule as written would have flagged the best line in the function.**

**THE RULE IS ABOUT DIRECTION, NOT DOMAIN: the fallback must be chosen so that an UNMEASURED value produces the SAFE OUTCOME FOR THAT PARTICULAR CONSUMER.**

- **For a DISPLAY, the safe outcome is *say you do not know*** — so an out-of-domain sentinel is right, and
  that is where the ten correct uses in that file live.
- **For a BRANCH there is no out-of-domain value**, so pick the in-domain one that fails TOWARD the guard:
  a count that must be zero defaults to one; a check for interlopers defaults to *some*; a floor on free
  memory defaults to ABOVE the floor.

⚠⚠ **THE SAME VARIABLE CAN NEED TWO DIFFERENT FALLBACKS IN TWO CONSECUTIVE LINES**, and that is not
inconsistency — it is two consumers with two safe directions.

⚠ **And the last case inverts what the two readers of this file first assumed: the safe fallback for an
unmeasured memory reading is NOT zero.** Zero aborts the run the floor exists to protect, on no evidence.
**The same rule that makes an unmeasured catalog count ABORT makes an unmeasured memory reading PROCEED,
because the consumers differ.**

### The old formulation, kept because it names where to look

**A script measures things by running a command and defaulting the result when it comes back empty. Ten of
those defaults use a value that could never be a real reading — a question mark where a count belongs — and
the unmeasured case is displayed as unmeasured.** ⚠ **Two use a number that is a perfectly ordinary reading
of the thing being measured, and then branch on it.**

**Zero free memory is a quantity of free memory. Zero running processes is the normal case.** In both, the
sentinel is indistinguishable from a datum, so *could not measure* silently becomes *measured, and here is
the answer*.

⚠⚠ **The rule is sharper than "check your fallbacks", and it says where to look: THE DEFECT IS AVAILABLE
ONLY WHERE THE DOMAIN HAS NO SPARE VALUE.** Where a spare exists — a sentinel string, a negative, a null —
the author will usually reach for it, and did, ten times in the same file.

### And the direction of the failure decides which instance is worse

**One of the two aborts a healthy run when the measurement fails: loud, wrong, and it sends somebody hunting
for a cause that was never there.** ⚠⚠ **The other lets a precondition PASS when the measurement fails — the
guard against a sibling test run admits one, and the comment above it records that proceeding then can
destroy data in use.**

**A guard that cannot measure and therefore REFUSES is expensive. A guard that cannot measure and therefore
ADMITS is the failure it was built to prevent.** ⚠ **When auditing this class, sort by direction before
severity** — and note that the permissive one is the harder to notice precisely because nothing ever
complains.

## A demonstration requirement is a design check, not a verification step

**A fix was specified for five sites: change a fallback so an unmeasured value fails toward safety. The
requirement attached to it was that each fix must DEMONSTRATE its unmeasured path — force the measurement
to fail and show the new behaviour.**

⚠⚠⚠ **The requirement fired before a line of the fix was written, and it changed the answer.** Three of the
five fallbacks are **unreachable**: those pipelines end in a counter that prints a number even when
everything upstream fails, so the default has never executed and never can.

**Without the requirement, three sites would have been "fixed" by changing a default that cannot run** —
shipped, reviewed, and permanently reassuring. ⚠ **The demonstration would simply have been impossible to
write, and that impossibility is the signal.**

**So ask for the demonstration when the change is SPECIFIED, not when it is reviewed.** A fix whose
correctness cannot be shown is usually a fix aimed at the wrong mechanism.

### And the real defect at those sites is the opposite shape

**A fallback catches an EMPTY value. A counter in the pipeline guarantees the value is never empty** — it
manufactures a legitimate-looking zero out of a failed upstream, and the exit status that would have
revealed it is discarded by the pipe.

⚠ **No fallback can catch a value that is never absent.** Where a pipeline ends in something that always
produces output, the only remedy is to keep the exit status; the two defects look identical in the source
and have no remedy in common.

### The harness is an instrument and can be wrong in the same way

**The first extraction of one block ended one line short and could not run at all, returning the same
failure code on both the good and the bad case** — which momentarily read as *the guard fires even on a
successful measurement*.

⚠⚠ **A demonstration that exercises only the failure path cannot tell a working guard from a broken
harness.** Run both sides every time: the unmeasured path AND a real value. **And extract the block from
the patched file verbatim — a demonstration of retyped code proves nothing about what ships.**

## Measure the defect in the unpatched code first

**A fix was written for a check that reports success when its own measurement fails. Before applying it,
the author extracted the check verbatim from the UNPATCHED source, injected a failure, and recorded what it
printed: the same reassuring note as the healthy case, in all three variants.**

⚠⚠ **Without that step the fix proves nothing.** Patch first, show the new code printing *not compared*, and
there is no evidence the old code did anything different — **the new message could have been right for an
unrelated reason, and nobody would ever know the old one was wrong.**

**THE BEFORE-MEASUREMENT IS THE CONTROL.** A fix demonstrated only after the fact belongs to the same class
as a guard nobody has watched fail: plausible, green, and unexamined.

### Run the healthy case beside the failing one, every time

**If the failing run and the working run print the same thing, the fix has not worked** — and that is
precisely what the unpatched code did. ⚠ **A demonstration of the failure path alone cannot distinguish a
repaired check from an unchanged one.**

### And a presence check is not a success check

**The function already refused to claim in three cases — no totals captured, no tool on the path, no
merge-base.** ⚠⚠ **The missing case is the one where the tool EXISTS, the base EXISTS, and the command still
fails.** The safe vocabulary was already there; only this path was routed to *ok*.

**Where a check verifies that something is available before using it, ask separately whether the use
succeeded.** Availability and success are different measurements, and the second is the one the result
depends on.

# Related Documents

- All accepted ADRs (001-012)
- Solution Architecture Document
- Development Standards
- Coding Standards
- Functional Specifications
- Sprint Documentation

---

---

## An argument from necessity cannot be wrong, which is why it proves nothing

**When a code path cannot be reached by any ordinary test, the case for a stand-in instrument writes
itself: *this is the only thing that reaches it.* That sentence is true and it is worthless as evidence,
because it would be equally true of a bad model.** An argument from **necessity** establishes that the
stand-in is the only option; it says nothing about whether the stand-in is **accurate**. Those are
different claims and the first is routinely accepted in place of the second.

**Worked instance, 2026-09-01.** The gate's memory-precondition abort could not be exercised without
genuinely exhausting the machine, so its behaviour was demonstrated by extracting the block and running it
against injected values. The justification on the record was *the only instrument that reaches this code*.
That night the real path fired for the first time, on a genuinely full box — **and the demonstration had
predicted the production output exactly: same lines, same order, different numbers.**

**The model was accurate. But nothing available at the time it was trusted established that**, and it could
have been checked only by the event it existed to substitute for.

**What follows:**

- **Say which argument you are making.** *Nothing else reaches this path* is a statement about coverage.
  *This reproduces what the path does* is a statement about fidelity, and it needs its own support —
  usually that the stand-in executes the **same text** rather than a transcription of it.
- ⚠ **When the real event finally occurs, check the stand-in against it and record the result.** This is
  the only occasion on which the fidelity claim is testable, it arrives without warning, and it is
  ordinarily spent celebrating that the path worked.
- **One confirmation on one path is not a general verification.** Five other demonstrated paths remain
  unobserved. What the confirmation buys is that the *practice* of extracting and injecting has been shown
  once to model the real thing — which is worth more than it looks, because the practice is what gets
  reused.

## A diagnostic earns its keep by changing a decision, not by reading well

**A message that improves on its predecessor and leaves the reader doing the same thing is decoration.**
The test is counterfactual: name the action the old output would have produced and the action the new one
produces, and check that they differ.

**Worked instance, 2026-09-01.** Three aborts on the same precondition had been indistinguishable in the
output, and each one invited the same response — retry, or run `dotnet build-server shutdown`. One of those
shutdowns was run three times for nothing. Two discriminators were added: the **spread** across five
samples, and the **count of live test processes**. On their first real firing they reported a 19 MB spread
and three processes, which together say *the box is genuinely full and there is nothing idle to reclaim* —
**so the run was not restarted, where every earlier abort had been.**

⚠ **The value was not the better-worded abort. It was forty minutes not spent reaching the same wall, and a
question escalated to the owner instead of retried a fourth time.** Where a diagnostic cannot name the
decision it changes, it is a candidate for deletion rather than improvement.

---

---

## Every check we run is positive-only: none of them can see work that was planned and dropped

**A gate verifies what was written. A commit message reports what was applied. A diff shows what changed.
All three take the artefact as given and ask whether it is sound — and not one of them can ask whether it
is COMPLETE against an intention held outside the tree.** Work that was planned and silently dropped
produces a green gate, an accurate commit message, and a clean diff.

**Worked instance, 2026-09-01.** A citation batch planned eighteen criteria and wrote seventeen. The commit
message said seventeen, so **the record was accurate and the intent was not** — and the discrepancy existed
only between the plan and the tree. Every instrument in the pipeline reported success, correctly. **The one
thing that found it was recounting the tree against the specification.**

**The same mechanism, at a different scale, on the same day:** a `git show --stat` check used to confirm
that nothing of one window's work had been lost could only confirm the files it named, and so could not see
an uncommitted file nobody had thought to name. **A positive-only instrument surveys a list you supply and
reports on its members; the members you failed to supply are indistinguishable from members that do not
exist.**

**What follows:**

- ⚠ **A batch reports the population, then the count, then the difference** — *18 planned, 17 written, and
  here is the one* — rather than the count alone. A bare count is consistent with any omission.
- **The completeness check must derive its population from an INDEPENDENT source**: the specification, the
  route table, the model — never from the same list the work was drawn from. Re-reading your own plan
  confirms only that you read it.
- **Report an omission you found yourself, in the artefact.** It costs a comment and it is the only
  evidence that the recount is being run at all.

## Absence of the cause is not a demonstration of the effect

**A criterion of the form *X must fail with E* is not satisfied by a test asserting that X is not present.**
The absence test says the bad state does not exist today; the criterion says the system RESPONDS to the bad
state. **Nothing about today's shape establishes what happens when the shape changes — which is the only
occasion the criterion is about.**

**Worked instance, 2026-09-01.** A cutover criterion required that a model carrying a direct manager foreign
key be shown to fail with `CutoverCopyOrderUndecidable`. The nearest test asserts the foreign key's
absence. **The design decision resting on that criterion therefore rests on an argument rather than a
test**, and the test's own comment says it cannot do more.

⚠ **The move that resolves this is the one validated the same night on the gate's abort paths: CONSTRUCT
THE STATE THE PRODUCTION CODE WILL NOT PRODUCE, AND RUN THE REAL CODE AGAINST IT.** Injection is what
reaches a path the system is designed never to enter. Where the code under test genuinely cannot accept a
constructed input, that is itself the finding — say **documented, not tested**, and stop calling it covered.

---

---

## A negative assertion is only as good as the proof that its predicate can match

**`Assert.DoesNotContain(xs, x => x.Name == "Foo")` passes in two situations: the thing is genuinely
absent, and the predicate matches nothing at all.** The assertion cannot tell them apart, and neither can
the reader — **both are zero matches, and both are green.** The same holds for `Assert.Null` over a
`FirstOrDefault`, `Assert.Empty` over a `Where`, and `Assert.False` over an `Any`. **Every negative
assertion over a predicate has a failure mode in which it asserts nothing and reports success.**

**Measured, 2026-09-01, and proven rather than argued.** One entity name in the cutover suite was planted
as a misspelling — `"Departmentt"` — and the suite returned **PASSED, 6 of 6, zero failures**. A rename, a
typo, a moved namespace or a renamed property all produce that state silently.

⚠⚠ **The near-miss is what makes this class hard to find.** Nine other sites in the same file compared
entity names through a helper ending in `Assert.True(index >= 0, …)`, and **those redden correctly** — so a
spot-check of the file meets the loud sites first and concludes it is safe. **Two independent reviewers
described that file's literals as failing silently; it was true of four sites and false of nine.** The
mixture is the hazard, not the presence of literals.

**Two remedies, and the first is strictly better:**

1. ⚠ **Make a wrong name a COMPILE ERROR** — `nameof`, a typed reference, an enum member. It needs no
   maintenance, cannot go stale, and turns the whole class into a build failure.
2. **Where the predicate must stay dynamic, add a POSITIVE COMPANION** proving it matches in the case
   where the subject IS present. Without one, the negative test is an unfalsifiable claim.

**This is the anti-vacuity control at the scale of a single assertion** — the same defect as a derived
population with no floor, and the same remedy. **Do not report a negative assertion as defective merely
for being negative: the defect is a predicate that nothing proves can match.**

## When a ruling turns on a mechanism, first ask what fraction of the population goes through it

**Converting a question of taste into a measurable one is the right move and it is not sufficient — the
measurement still has to be aimed at the population rather than at a mechanism inside it.**

**Worked instance, 2026-09-01.** A change was scoped on a single question: *does this helper throw for an
unknown name, or return a sentinel?* — with a stated consequence for each answer. **The helper throws, so
the rule returned the narrow scope.** ⚠ **But only nine of the seventeen sites went through that helper at
all, and four of the remaining eight carried the entire defect.** The procedure was sound and the answer
was wrong, because **a question about one mechanism cannot bound a population whose members do not all use
it — and the coverage of the mechanism was never asked for.**

⚠ **One clause fixes it: name the mechanism, then ask what fraction of the sites go through it, before
letting the answer decide anything.** It costs one enumeration, and here it inverted the ruling.

---

---

## When the error cannot name its cause, difference against a matched control

**One `Error` value raised from three unrelated conditions is a merge of three diagnoses performed at the
throw site.** No assertion downstream can undo it: a test that observes the value proves *something* went
wrong and is read as proving *the specific thing* went wrong. **That gap between what is proven and what is
read is the whole defect, and it is invisible in a green suite.**

**Worked instance, 2026-09-01.** A copy planner raised the same error for *no primary key*, *no tenant
column*, and *the foreign-key cycle* — and only the cycle was the behaviour five production design
decisions rested on.

⚠⚠ **THE TEST WAS BUILT BY DIFFERENCING INSTEAD OF BY ASSERTING.** Two probe entities were made identical
in every property the other two conditions test — primary key, copyable columns, tenant column — **differing
by exactly one foreign key.** The acyclic one is asserted to SUCCEED; the cyclic one to fail.

⚠ **THE CONTROL'S PASS IS THE LOAD-BEARING HALF.** If either confounding condition could fire for these
entities, the control would fail too — **so its success is what excludes them.** The failing test alone
proves nothing; the pair proves it. **State the bound in the file: the other two sites remain unexercised,
and this file must not be read as closing them.**

## A harness defect can wear the costume of the finding

**The worst kind of broken test is not one that fails wrongly — it is one whose failure looks exactly like
the product defect it was written to detect.** The correct conclusion and the false one produce the same
red, and the false one is the interesting result, so it is the one that gets reported.

**Worked instance, 2026-09-01.** A first attempt used one contributor type carrying a boolean flag to
produce two different models. **The model cache is keyed on the ordered set of contributor TYPES, not
instances** — so both variants shared one cache signature and the second test received the first's model.
**It passed alone and failed after its control**: silent, order-dependent, and its symptom read as *the
planner did not detect the cycle* — a false product defect in the exact area under investigation.

- ⚠ **The prohibition was already written down.** The contributor interface forbids varying a mapping by
  ambient state, **and a constructor flag is ambient state**; the cache factory's own comment predicted
  this failure verbatim. **Read the commentary attached to the mechanism before instrumenting it.**
- ⚠⚠ **Run a new test BOTH with its neighbours AND alone, and record that you did.** Order-dependence is
  what this class produces, and a control that only ever runs beside its partner cannot reveal that the
  partner is what makes it pass.
- **When a test reports the finding you were hoping for, suspect the harness before publishing.** Here the
  hoped-for finding and the harness bug were the same colour.

---

---

## Before changing a guard that blocks you, ask whether the change would be right if the work did not exist

**Editing a guard to unblock your own work is the shape that destroys guards, and it is indistinguishable
at the moment of writing from correcting a guard that is genuinely wrong.** The distinction is not the
diff; it is the counterfactual. ⚠ **ASK: WOULD THIS CHANGE BE RIGHT IF THE BLOCKED WORK DID NOT EXIST?**
Answer it on grounds that never mention the blocked work, or do not make the change.

**Worked instance, 2026-09-01.** An entity census discovered its contributors by enumerating every
matching assembly in its own output directory — **which included the test assembly it lives in.** The first
test in the tree ever to define a contributor added two entities to the composed model and reddened the
guard.

**Two answers that never mention the blocked work:**

- **The population disagrees with the name.** *The composed tenant model* is what production composes; a
  contributor defined by a test double is not part of it.
- ⚠⚠ **The guard had been passing on an absence rather than on its logic.** The population was wrong from
  the day it was written, and no member of the wrong part existed. **A guard whose correctness rests on
  nobody having yet done a legal thing is already broken; the first person to do it merely reveals it.**

**And the property that makes the narrowing safe rather than an accommodation: IT REMOVES ZERO PRODUCTION
COVERAGE.** No production entity lives in a test assembly, so the guard's actual job is untouched. **Where
a proposed narrowing would remove real coverage, the counterfactual test fails and the answer is to move
the new work, not the guard.**

⚠⚠ **The refusal is the other half, and it leaves no artefact.** The cheapest green available was to raise
the expected count — **making the alarm accommodate the thing it exists to catch** — and it was declined.
**A commit that was not made is invisible to every instrument in the pipeline, so a refusal has to be
written down or it did not happen.**

### ⚠⚠⚠ AMENDED WITHIN THE HOUR, BY THE PERSON APPLYING IT: THE TEST IS NECESSARY AND NOT SUFFICIENT

**The narrowing above passed the counterfactual test and was still the wrong action.** An enumeration —
demanded before the edit, and only because three earlier rulings that day had failed by not asking what
fraction of a population goes through a mechanism — found that **a test-defined contributor already existed
in a different test project and had never broken the census**, because that assembly is not in the
discovering project's output directory. **The blocked file simply belonged in that other project.** It was
moved; nothing shared was touched; the guard is green and unmodified.

⚠ **BOTH GROUNDS FOR THE CHANGE STILL HOLD.** The population still disagrees with the name; the guard is
still passing on an absence. **The change is correct and was unnecessary, and the counterfactual test
cannot tell those apart — it asks whether a change is JUSTIFIED, never whether it is NEEDED.**

**SO THE TEST TAKES A SECOND CLAUSE: BEFORE TOUCHING A SHARED GUARD, ENUMERATE THE ALTERNATIVES THAT DO NOT
TOUCH IT.** A cheaper move that makes the question moot is not screened out by any test applied to the
change itself; it has to be looked for.

⚠⚠ **And a correct change made under pressure to unblock is a different act from the same change made
deliberately, even when the diff is identical** — the pressure selects which correct changes get made and
which get examined. **A defect found this way belongs on the backlog, where it will be judged on its own
merits, and not in the path of the work that found it.**

---

---

## A population predicate must not contain the property under test

**A derived population is the right answer to a hand-written list — but the derivation has a failure mode
the list does not have: if the selection predicate shares a term with the assertion, MEMBERSHIP BECOMES
CONDITIONAL ON COMPLIANCE.** The check then examines only the members that already pass, and a member
carrying the defect is not merely missed — **it is excluded by the defect itself.**

**Worked instance, 2026-09-01.** A guard was built to assert that every handler which reads a fiscal period
opens its transaction BEFORE the read. The population was derived as *types that open a transaction and
read a fiscal period.* ⚠ **That set cannot contain a handler which reads a period and fails to open a
transaction**, which is exactly the defect the guard exists to find. Three members were found and all three
passed. **A fourth type, in a file the derivation did reach, reads a period and opens no transaction at
all** — invisible to the check by construction.

**The shape is easy to miss because the predicate reads as a scoping decision.** *Only handlers that do X*
sounds like relevance; it is relevance only when X is independent of the assertion. Here X WAS the
assertion.

- ⚠ **State the population as the SUBJECT of the rule, never as its satisfaction.** *Reads a fiscal period
  and writes* is the subject; *opens a transaction and reads a fiscal period* is the subject filtered by
  compliance.
- **An exclusion BY REASON is sound and belongs in the file** — a handler that reads no period is outside
  the rule, and saying so is what makes the boundary reviewable. **An exclusion by the property under test
  is not an exclusion; it is a blind spot with a justification attached.**
- ⚠⚠ **The test: name a member the predicate would exclude, and ask whether it would be defective.** If a
  defective member could not be in the set, the set is wrong.

## Every conditional subset of a derived population needs its own floor

**A floor on the derived population proves the derivation found something. It says nothing about a branch
that applies to only some members.** Where an assertion is guarded by a condition — *check the ordering
only where this call is made* — **the condition can come to match nothing, and the branch then applies to
zero members and passes.**

⚠ **The population floor does not cover this: it counts members, not members that reach the branch.**
Conditioning is usually correct — a member with no reason to make a call should not be asserted against —
so the remedy is not to remove the condition but to floor the subset it selects. **A conditional assertion
is one that can quietly stop applying, and only a count of the members that exercise it will say so.**

---

---

## An exemption must assert its own grounds

**Every derived population acquires exemptions — a member the rule genuinely does not reach, or reaches by
a different mechanism. Recording the reason in a comment is the normal practice and it is not enough: THE
GROUNDS OUTLIVE THE COMMENT.** The member changes, the mechanism is removed, and the exemption remains,
now protecting exactly what the guard was built to catch.

**Worked instance, 2026-09-01.** A handler that reads an accounting period and writes it opens no
transaction — and is correct, because the entity's row version is mapped as a concurrency token, the
repository read is tracked, the conflict is translated, and the API maps it. **Four links, any one of which
would have made the exemption false.**

⚠ **The exemption was written as a test, not as a comment.** It asserts two things: that the handler is
**still in the derived population** — so the exemption describes something real rather than a member that
has since moved — and that **the concurrency token is still mapped.** Removing the token turns the
exemption red. Proven by planting exactly that removal.

- **Assert membership, not just the grounds.** An exemption for a member that has left the population is
  dead code that reads as a considered decision.
- ⚠⚠ **Assert the grounds MECHANICALLY, not by restating them.** *This is safe because of optimistic
  concurrency* is a claim; `IsRowVersion()` still being mapped is a fact a test can hold.
- **This is the general answer to every "this one is different because" comment in a codebase.** The
  comment states the difference; the test makes the difference falsifiable.

---

---

## An instrument that skips work reports on the work it did, not on the work you asked about

**Three instances in one session, and in every one the missing step was UPSTREAM of the thing being
measured while the output still looked like a result** — not like an error, not like nothing, **like an
answer.** That is what makes the family dangerous.

- **An incremental build reporting success for code it never recompiled.** MSBuild skipped an up-to-date
  project, so a warning that exists in the source was never re-emitted. Happened twice: once while
  confirming a compiler diagnostic, once while checking a file clean that the gate then reddened on
  `CA1859`.
- ⚠ **A plant that broke the build, so the test never ran — and the ABSENT `Failed!` line read as *the
  plant did not fire*.** A false record of "this guard is vacuous" was one step away.

**RULES, ABOUT THE INSTRUMENT RATHER THAN ABOUT REMEMBERING:**

- ⚠⚠ **A TARGETED BUILD IS NOT EVIDENCE ABOUT WARNINGS.** Only `--no-incremental`, or the gate, builds
  honestly enough to answer that question. `dotnet build -v q` is not a warning check.
- ⚠ **AN ABSENT FAILURE LINE IS NOT A PASSING TEST.** Grep for the POSITIVE result — the line naming that
  test — and never infer from the absence of one.
- **If a deliberate break makes the code uncompilable, ask whether the test needs the build at all.** A
  test that reads source text runs correctly under `--no-build`.

**And the standing consequence: the zero-warning gate reads as bureaucracy right up to the moment it is
the only instrument in the loop that builds honestly.** It found, twice in one session, something a faster
local check structurally could not see.

---

---

## A scope predicate is owed by the entity's ownership classification, not by the module's preference

**Every tenant entity already declares what it is scoped by — `ITenantOwnedEntity`, `ICompanyOwnedEntity`,
`IBranchOwnedEntity`.** That declaration is an obligation on every read path that reaches the entity:
**for each dimension the entity declares, the query composes an explicit predicate on that dimension.**
`ADR-025` decision 10 rejects a global company filter because a filter pinned to one company makes
authorized multi-company reads unexpressible — **so the predicate must be EXPLICIT rather than ambient, and
that is the whole reason the obligation exists.**

**Measured 2026-09-01.** Seven module read services; **all seven compose explicit tenant and company
predicates and none calls `IgnoreQueryFilters`.** Attendance additionally composes branch — **not a module
preference, a consequence of what its entities declare.** ⚠ **Nothing is broken and nothing holds any of
it in place: exactly one acceptance criterion in the whole documentation tree requires this, and it belongs
to the module whose guard was written last.**

⚠⚠ **A GUARD WITH NO STATED OBLIGATION IS A RULE INVENTED BY WHOEVER WROTE THE TEST**, and it is
indistinguishable from a specified one until somebody looks for the criterion and finds nothing. The most
elaborate scope guard in this repository was enforcing a rule written down nowhere.

**Deriving the obligation from the ownership interfaces settles both halves at once: the population is
every read path reaching a declaring entity, and the required dimensions are whatever that entity
declares.** Nobody has to state a per-module rule, and no module can be given one it did not already owe.

### ⚠⚠⚠ ANCHOR ON WHAT THE CODE MUST PRODUCE, NOT ON HOW IT NAMES WHAT PRODUCES IT

**A guard for this rule must match the COLUMN — `TenantId`, `CompanyId`, `BranchId` — and never the local
variable that carries the scope.** Two services bind `scope.Companies.CompanyIds`; another binds
`resolved.Value.TenantId` and `readScope.TenantId`. **A matcher keyed to the first spelling reports the
third as having no tenant predicate at all, and it has three.**

⚠ **That mistake was made while MEASURING and caught before it shipped. Had it shipped inside the guard it
would have fired falsely on day one — and a false alarm in a report costs a correction, while a false alarm
in a guard costs the guard**, because a guard that cries wolf is switched off and takes its whole class
with it. **Strip comments before matching, too:** three read services open with a comment containing the
SQL predicate verbatim, and a text guard would be satisfied by the prose while the query composed nothing.

---

---

## Three remedies, one control at three levels — and an absence assertion admits only the third

**Every negative assertion needs proof that it COULD have failed. Which proof is available depends on the
assertion's shape, and the three are not interchangeable:**

- **A FLOOR proves the COLLECTION can be non-empty** — for a negative assertion over a collection that may
  legitimately be empty.
- **A COMPILE-TIME REFERENCE proves the IDENTIFIER exists** — `nameof`, `typeof`, an enum member — for a
  thing that must be PRESENT and is named by a string.
- ⚠⚠⚠ **A MATCHED CONTROL proves the VOCABULARY CAN MATCH** — for a thing that must be **ABSENT**, and it
  is the only one of the three that works there.

**Worked instance, 2026-09-01.** Thirteen assertions asserting that no property name contains a given
substring. ⚠ **A compile-time reference is IMPOSSIBLE BY CONSTRUCTION — you cannot take `nameof` a property
whose entire point is that it must not exist.** And **a floor is irrelevant: a misspelt substring matches
nothing over a fully populated collection exactly as happily as over an empty one. A FLOOR CLOSES VACUITY,
AND THIS IS NOT VACUITY.** The collection was a hard-coded array of `typeof()`s that could not go empty.

**The remedy that works: hoist every literal to a shared constant, and add one control asserting each
constant matches something.**

- ⚠⚠ **THE CONTROL MUST USE THE SAME SYMBOLS THE ASSERTIONS USE.** A control carrying its own copy of each
  literal proves nothing — misspell the assertion site and the control sails on. With shared symbols, a
  misspelt constant fails the control and a misspelt call site fails to compile. **Two failure directions,
  one from a test and one from the compiler, neither reachable by the other.**
- ⚠ **THE CONTROL MUST FAIL FOR THE REASON THE ASSERTION COULD FAIL, NOT MERELY FAIL.** Where the predicate
  is a substring match, the control's members carry a suffix — one that only ever matched whole names would
  never exercise the thing under test.
- **And the control is floored on its own size**, because it is the same trap one level down.

**An absence assertion whose vocabulary nothing proves can match is UNFALSIFIABLE BY CONSTRUCTION.** It is
not a weak test; it is not a test.

⚠⚠ **AND CLASSIFY BY REMEDY, NEVER BY SYMPTOM.** A sweep that ranked files by "negative assertion with no
floor" put this file second — **and it survived classification at 0 of 13 for that hole and 13 of 13 for a
different one.** A candidate list whose highest-ranked member survives at zero is not a number to reason
from: **report how many survive classification per file, not how many matched.**

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | YYYY-MM-DD | Solution Architecture Team | Initial version |
