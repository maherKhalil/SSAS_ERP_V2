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

# Related Documents

- All accepted ADRs (001-012)
- Solution Architecture Document
- Development Standards
- Coding Standards
- Functional Specifications
- Sprint Documentation

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | YYYY-MM-DD | Solution Architecture Team | Initial version |
