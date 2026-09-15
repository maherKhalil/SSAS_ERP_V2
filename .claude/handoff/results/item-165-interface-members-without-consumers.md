# item 165 — interface members declared in `src/` with no production consumer

**Measurement only. Nothing built, nothing removed, no removals proposed.** Run in an isolated git
worktree at **`7dcbabd`** — the commit before PR #383 — so `ICurrentUser.CompanyId` is present and serves
as the known positive.

## ⚠ The first instrument was wrong, and the known positive is what caught it

**Attempt 1 renamed every member of an interface and treated a `CS1061` as a consumer.** It named two
consumers for `ICurrentUser` and would have declared six members unconsumed.

**The build FAILED with 172 errors — and a failing project stops its DEPENDENTS from compiling at all.**
Every consumer downstream of the first failure was never reported. **A compile-error instrument cannot
enumerate consumers, because its own signal halts the search.** Its zeros were worth nothing.

**Attempt 2 uses a warning instead.** Every interface member gets
`[System.Obsolete("PROBE|<Interface>|<Member>")]`; each consumer site then emits `CS0618` **while the
build succeeds**, so every project compiles and the list is complete. `Directory.Build.props` sets
`TreatWarningsAsErrors=false`, which is what makes this possible.

**Result: build succeeded, 0 errors, 3,342 `CS0618` warnings across 499 members in 193 interfaces.**

⚠ **And the known positive tests the one thing that could still have broken it.** If *implementing* an
obsolete member also warned, every member would look consumed by its own implementers.
**`ICurrentUser.CompanyId` — 100 implementers, no production consumer — came back with ZERO warnings**, so
implementations are silent and the counts are consumption, not declaration.

## The answer

| | |
|---|---|
| interface members probed | **499** |
| **with no production consumer** | **61** |
| of those, consumed only by tests | 5 |
| of those, consumed by nothing at all | 56 |

**At `HEAD` the figure is 60**, since `ICurrentUser.CompanyId` was removed by PR #383. I did not re-run at
`HEAD`; the delta is exactly that one member.

## ⚠ The implementer column is an OVER-count, and by how much is known

Implementers are counted **by member name**, so a common name collects declarations from unrelated types.
`ICurrentUser.CompanyId` reads **141** here against a true **100** measured in item 164 — roughly 40%
inflation on a colliding name. `IBranchTopologyLease.TenantId` at 195 is almost entirely collision.

**Treat the column as an order of magnitude, not a count.** It is still the right column to show, because
Principle 22 makes it the removal cost: a member with many implementers cannot be removed without touching
each one, and each orphan trips `CA1822` into a zero-warning gate.

## What would be lost — by group, not member by member

**None of these is automatically dead.** I assessed the clusters, not all 61 individually.

**1. `ICurrentUser` — 5 remaining members** (`Email`, `Roles`, `SessionId`, `TokenId`, `UserName`), ~100
implementers each. The same interface `CompanyId` came from, and the same shape: declared, implemented
everywhere, read by nothing in production. **`Roles` is the one to think about** — role claims are issued
and validated, so removing the accessor removes the only typed way to read them, even though nothing does
today. The rest are caller-identity conveniences.

**2. `IUnitOfWork` — both members, verified by hand.** `SaveChangesAsync` and `BeginTransactionAsync` have
no production consumer, and **`IUnitOfWork` is not injected anywhere in `src/` at all** — the only
references are its own declaration. `EfUnitOfWork` implements and is registered; nothing resolves the
abstraction. **What would be lost is the seam itself:** an explicit transactional boundary that handlers
could take. Removing it forecloses that without a ruling — the `SetOvertimeTier` shape.

**3. The tenant-database lifecycle family — roughly 25 members** across backup, restore verification,
cutover, connectivity and schema health, and migration running. These are operational capability that
exists and is not driven from production code. **Same class as item 152's 28 "handler but no route"
rows** — removing them deletes finished work whose transport was never built.

**4. `ITenantBranchService` — 5 members** (`CreateAsync`, `ListAsync`, `GetAsync`, `UpdateAsync`,
`DeactivateAsync`, plus `GetOnboardingStateAsync`). Branch management is the area where item 152 found
`Branch.FirstBranchRequired` specified and produced by nothing. **These are the handlers for that unbuilt
surface.**

**5. ⚠ `ITenantEntitlementCache.InvalidatePlan` and `InvalidateTenant`.** These are the cache-invalidation
seam FP-014 needs, and item 162 established there is **no entitlement-lapse event to trigger them** —
expiry is a pure read. So they are unconsumed *for exactly the reason item 162 documented*, and the commit
that first creates a write path is the commit that needs them. **Removing them would delete the mechanism
the design is waiting on.**

## ⚠ What this population excludes

- **Explicit interface implementations** (`Guid? IFoo.Bar => …`) — not probed as declarations.
- **Default interface members** — a member with a body in the interface is implemented there and behaves
  differently under this probe.
- **Generic interfaces** — a member reached through a *derived* or *constructed* interface may attribute
  its warning to the derived declaration, so a member can read as unconsumed while a derived form is used.
- **Reflection, DI resolution and serialization** — a member reached without a compile-time reference
  emits no warning and is invisible here. This is the significant one for repository and service
  interfaces.
- **Consumers in `tests/`** are counted separately and are *not* production consumers, matching item 136's
  population.
- Only **two** of the 61 were verified by hand (`IUnitOfWork.SaveChangesAsync`, and the known positive).
  The rest rest on the instrument.
