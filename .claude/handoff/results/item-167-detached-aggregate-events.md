# item 167 — can an aggregate raise an event that dispatch never sees?

**Gated work.** Two tests added to `tests/Platform.Tests/Persistence/DomainEventFlowTests.cs` (now 7),
`GATE_SCOPE=TASK` **green**. No `src/` change.

## ⚠ The answer: the hazard is real, and NO PRODUCTION PATH REACHES IT

`DispatchDomainEventsAsync` collects from `dbContext.ChangeTracker.Entries()` and nowhere else, so an
aggregate that raised events while untracked is invisible to it — no error, no warning, no event.

**203 `AsNoTracking` sites in `src/` across 65 files. 30 of them touch one of the 14 event-raising
aggregate types.** Classified by what the query actually returns:

| | count | why it is safe |
|---|---|---|
| **scalar** — `AnyAsync`, `CountAsync` | **17** | returns a `bool`/`int`; no entity ever leaves the query |
| **projection** — `Select(…)` to a DTO | **5** | the aggregate is never materialised as an entity |
| **read-service entity reads** | **8** | all in `*ReadService` / `*DirectoryService` / `*RosterService`, all returning DTOs, ids or dictionaries |

**And the closing fact: no read service is injected into any command handler.** The 8 entity-shaped reads
cannot reach a caller that mutates and saves, because their callers are query handlers.

⚠ **My first classification over-fired and the refinement is the finding's method.** A window search for
`Set<T>` near `AsNoTracking` reported 26 "returns entity" — including `EmployeeRepository`'s
`EmployeeNumberExistsAsync`, which is an `AnyAsync` returning `bool`. **Classifying by the TERMINAL
OPERATOR rather than by proximity took 26 to 8.** A guard built on the first classification would have
fired on seventeen correct existence checks.

## What was pinned, and what the tests mean

- `An_aggregate_never_attached_is_not_dispatched_from`
- `An_aggregate_read_with_no_tracking_and_then_mutated_is_not_dispatched_from` — the production-shaped
  version: store, re-read `AsNoTracking`, mutate, save.

**Each asserts two things, and the second carries the meaning:** the consumer receives nothing, **and the
events are still on the aggregate afterwards.** That distinguishes *nothing was raised* from *something was
raised and nobody collected it* — the whole difference between a quiet success and a silent drop.

**These pin the drop as current behaviour. They do not assert it is correct.** The behaviour is unreached
today; pinning it gives a future change that starts reaching it something to disagree with.

**Plants:** removing `.AsNoTracking()` and attaching the detached probe reddened exactly those two tests,
which proves they are sensitive to *tracking* rather than passing for some unrelated reason. The standing
control is `A_saved_aggregate_reaches_a_registered_consumer_with_metadata` — dispatch demonstrably works,
so "received nothing" is a fact about tracking.

## ⚠ Which guard shape fits — recommendation, not built

**Not a ban on `AsNoTracking`.** It would refuse 203 sites across 65 files to prevent a hazard that occurs
at none of them. A guard whose false positives outnumber its true ones by two orders of magnitude gets
deleted rather than fixed, and then protects nothing.

**Not a runtime test alone either, and this is the real argument.** A silent drop has no symptom, so a
test only ever covers the sites that exist today. The two tests above pin the *mechanism*; they cannot
notice a new `AsNoTracking` on a write path added next month.

**The shape that fits is narrower than either: a structural assertion that no read-side service RETURNS an
event-raising aggregate type.** The hazard needs the entity to escape to a caller who mutates it — so the
property worth guarding is the escape, not the query. It is true today, it is cheap to check by
reflection over return types, and its false-positive surface is small because read services already return
DTOs by convention.

⚠ **The honest caveat is that I would want it to fail once before trusting it.** My own classification
needed three passes to stop over-firing, and a structural guard that misclassifies a legitimate read
service is exactly the guard someone deletes. **I recommend it; I have not built it, because you asked for
the shape before the code and because "which types count as read-side" is a convention question rather
than a measurement.**

## What this population excludes

- **Detached mutation without `AsNoTracking`** — an entity materialised in one context and mutated against
  another, or reconstructed from a DTO. Not enumerated; `AsNoTracking` is the searchable form of the
  hazard, not the only one.
- **`tests/`** — the search covered `src/` only.
- The 14 event-raising types were derived from files containing `RaiseDomainEvent` (65 sites, matching the
  independently-stated count). A type that raises events through a helper rather than directly would be
  missed.
