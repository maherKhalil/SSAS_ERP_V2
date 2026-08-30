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

**⚠ And put the floor on the POST-FILTER count, because a walk rooted at a directory cannot vanish silently
— a filter can.** Pointing a walk at a missing `src/PlatformX` throws `DirectoryNotFoundException`; renaming
a project is caught by an exception, not by an empty result. **The silent failure is always downstream of
the root**: a changed search pattern, a widened exclusion, a filter on a path segment that no longer
matches — where every directory still exists and the array comes back empty anyway. That is precisely how
the `PersistenceArchitectureTests` false green worked: it enumerated from the repository root, which always
exists, and filtered on a segment. **A floor on the pre-filter count would have looked prudent and caught
nothing.**

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
