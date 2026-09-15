# item 174 — the mechanisms by which an aggregate can exist untracked

**Measurement only. Nothing built.** Item 167 closed the `AsNoTracking` form of the detached-aggregate
hazard; this enumerates the **complete mechanism set** and measures each, because a name search cannot be
complete — you cannot enumerate the names you did not think of.

## ⚠ The mechanism set, stated before searching

EF Core tracks an entity **exactly when it is in that context's `ChangeTracker`**. It **enters** by a
tracking query, by `Add`/`Attach`/`Update`/`Remove`, or by navigation fix-up from a tracked entity. It
**leaves** by `Detached` state, by `ChangeTracker.Clear()`, or by the context being disposed.

**That closes the set.** An aggregate is untracked exactly by one of:

| | mechanism |
|---|---|
| **A1** | constructed with `new` and never `Add`ed |
| **A2** | `AsNoTracking()` / `AsNoTrackingWithIdentityResolution()` |
| **A3** | context-level `QueryTrackingBehavior.NoTracking` |
| **A4** | materialised by a projection — `Select(x => new Aggregate(…))` |
| **A5** | deserialised from a document |
| **B1** | `Entry(e).State = EntityState.Detached` |
| **B2** | `ChangeTracker.Clear()` |
| **B3** | the context is disposed |
| **C1** | tracked by context A, saved through context B |

**The set is derived from EF's tracking model, not from what the codebase happens to contain** — which is
what makes it complete rather than merely long.

## What each measures in `src/`

| mechanism | count | reachable with an event-raising aggregate? |
|---|---|---|
| **A2** `AsNoTracking` | **203** | **no** — item 167: 17 scalar existence checks, 5 projections, 8 read services returning DTOs, and no read service is injected into a command handler |
| **A3** `QueryTrackingBehavior` | **0** | — |
| **A4** projection-construction | within the 5 above | no |
| **A5** deserialisation | aggregates are never deserialised; `StrictRequestReader` binds **request DTOs** | no |
| **B1** `EntityState.Detached` | **12** | ⚠ **no — see below** |
| **B2** `ChangeTracker.Clear` | **0** | — |
| **B3** context disposed | 3, all `using var context = new …DbContext(…)` in **model composition** | no |
| **C1** second context | design-time factories and model composition only | ⚠ **no — see below** |
| **A1** `new` without `Add` | the ordinary creation path | **no** — an aggregate never `Add`ed is never persisted at all, which is a louder failure than a lost event |

### ⚠ B1 — the twelve detachment sites, named

`TenantLocalizationSettingsRepository:46`, `TenantCutoverOperationStore:86,254`,
`TenantDatabaseBackupRunStore:133,138`, `TenantDatabaseDimensionWriter:66`,
`TenantDatabaseRestoreVerificationRunStore:219,238,338,339`, `TenantStorageBootstrapService:119,253`.

The entities are `TenantLocalizationSettings`, `TenantCutoverOperation`, `TenantDatabaseBackupRun`,
`TenantDatabase`, `TenantDatabaseRestoreVerificationRun`. **None is among the fourteen types that raise
domain events** (item 167's list). And the one that comes closest —
`TenantLocalizationSettingsRepository` — detaches after a unique-violation rollback and then **discards
the instance**, re-reading the winning row.

**`AuthenticationAccountRepository:37` is the near miss and it is not one.** `AuthenticationAccount` does
raise events, but the call is `Entry(account).ReloadAsync(…)` — that **refreshes a tracked entity**, it
does not detach it.

### ⚠ C1 — and why the production path cannot reach it

`new TenantDbContext` (6), `new PlatformDbContext` (1), `CreateDbContext` (3). All but one are
**design-time factories for EF tooling** or `using var` contexts for model composition.
`TenantDbContextFactory:97` is the production one, and it returns **the scoped context the repositories
share** — `TenantUnitOfWork` caches a single inner `EfUnitOfWork` bound to that same instance, verified in
item 166, with the code stating the reason: *"A second context here would silently discard them."*

### A name collision worth recording

**`.Update(` matches 5 sites and none of them is EF's.** They are domain methods —
`leaveType.Update(…)`, `role.Update(…)`, `draft.Update(…)`. `DbSet.Update`, which attaches a
detached graph and is the classic route into this hazard, **is never called.**

## The known positive

**`AsNoTracking`, rediscovered at 203 sites.** It is the one mechanism independently established as
present, by item 167, before this enumeration existed. **A mechanism sweep that returned zero for it would
have been looking in the wrong place**, and every other zero here would have been worthless.

## ⚠ The answer

**No mechanism in the set is reachable in production with an aggregate that raises domain events.** The
hazard remains real and unreached, now across the **complete** mechanism set rather than the one
searchable form.

## What this does not establish

- **Reachability is judged from the call sites, not by execution.** B1's twelve were read; none mutates a
  detached event-raiser. A path assembling one across several files would need following, and I followed
  none beyond the immediate method.
- **`tests/` was not measured** — the population is production code, as in item 167.
- **Navigation fix-up** can attach a graph and is not a way to become *un*tracked, so it is outside the
  set by construction rather than by measurement.
- The counts are `src/` only; `tools/` was not swept for this item.
