# FP-014 — Test scenarios (proposed)

Written from the ruling set of 2026-08-25. Pairs with
[`acceptance-criteria.md`](acceptance-criteria.md).

Layered as the existing modules are: **domain** (no database), **API** (host with stubbed
dependencies), **integration** (real SQL Server), **architecture** (reflection over the built
assemblies).

**Each scenario states what would fail if the behaviour were absent.** A scenario that restates its
requirement proves nothing — the same standard the coder charter applies to real tests, and the
reason FP-013's `TS-ATT-0001` runs three weekend patterns rather than one.

**The commercial plane is Platform-database data**, so its domain and integration scenarios belong to
`tests/Platform.Tests` and `tests/Integration.Tests` rather than to a new module suite. There is no
`Subscription.Tests` project and this package does not propose one — the aggregates live in
`SSAS.Platform.Domain`.

---

## Domain — `tests/Platform.Tests`

| ID | Scenario | AC |
|---|---|---|
| `TS-SUB-0001` | `SubscriptionTerm` construction: `Fixed` with an end after the start accepted; `Fixed` with a **null** end refused; `Perpetual` **with** an end refused; `Fixed` with end equal to start refused. **Four cases, because a nullable end alone cannot distinguish perpetual from not-yet-set** — which is what `OD-SUB-0009`'s word *explicit* was about | `AC-SUB-0027` |
| `TS-SUB-0002` | Append with `EffectiveFromUtc` **later** than the current maximum accepted; **equal** refused; **earlier** refused. The equal case is the one that would slip through a `>=` written where `>` was meant, and it is the case that lets a backdated row rewrite what was in force when an overage was judged | `AC-SUB-0004` |
| `TS-SUB-0003` | Two records at `T1` and `T2`. `EntitlementAt` at `T1−1s` resolves nothing, at `T1+1s` resolves the **first**, at `T2−1s` still resolves the **first**, at `T2+1s` resolves the second. **The third assertion is the one that fails if the code reads "latest record" instead of "latest record not after T"** | `AC-SUB-0002` |
| `TS-SUB-0004` | Modules resolve as `plan ∪ grants`: plan alone, grant alone, both, and a grant duplicating a plan module producing no duplicate in the result | `AC-SUB-0011` |
| `TS-SUB-0005` | Cap resolves as `max(plan, grants)`. A grant constructed **directly** with a value below the plan's cap — bypassing the write-time refusal — leaves the resolved cap at the plan's value. **The write refusal and the resolution rule are tested separately because either alone would let the other rot** | `AC-SUB-0017` |
| `TS-SUB-0006` | A grant whose `LimitValue` is below the plan cap is refused; equal is refused; above is accepted. The **equal** case is the boundary that decides whether "additive" means "strictly raises" | `AC-SUB-0016` |
| `TS-SUB-0007` | A tenant with no subscription record resolves to an **empty** module set — asserted as empty, not as an exception and not as a null that a caller might treat as "unrestricted" | `AC-SUB-0012` |
| `TS-SUB-0008` | One unchanged aggregate, two clock values either side of `TermEndUtc`, two different resolved states, and **zero writes** — asserted by the change tracker being empty | `AC-SUB-0028` |
| `TS-SUB-0009` | An entitlement cached at `TermEndUtc − 1s` does not answer *entitled* at `TermEndUtc + 1s`, **with no invalidation event raised in between**. This is the scenario a cache holding only a boolean fails | `AC-SUB-0032` |
| `TS-SUB-0010` | A retired plan cannot be assigned to a tenant; a subscription record already naming it still resolves | `AC-SUB-0005` |
| `TS-SUB-0011` | Invoice lifecycle: `Draft` accepts edits; `issue` assigns a number and stamps `IssuedUtc`; a post-issue edit is refused; `void` retains the number. **The retained number is the assertion that matters** — reuse is the failure every regulated numbering scheme forbids | `AC-SUB-0038` |
| `TS-SUB-0012` | A period containing a mid-term plan change produces **two** invoice lines naming **two different** `TenantSubscriptionId`s, and their amounts sum to the period total | `AC-SUB-0039`, `AC-SUB-0042` |
| `TS-SUB-0013` | A seat sample records the record in force at its observed instant; appending a **later** subscription record leaves the stamp unchanged | `AC-SUB-0040` |
| `TS-SUB-0014` | An overage in period P is judged against the plan in force during P. Upgrading after P does **not** erase it; downgrading after P does **not** create one. **Two directions, because a single-direction test passes on an implementation that simply reads today's plan when today's plan happens to be larger** | `AC-SUB-0041` |
| `TS-SUB-0015` | Assigning a plan that carries no price row in the tenant's billing currency is refused, and the error names the currency | `AC-SUB-0036` |
| `TS-SUB-0016` | Modules and the resolved cap come back from **one** resolution call. Asserted by the number of repository round-trips, so a second implementation resolving them separately fails | `AC-SUB-0043` |
| `TS-SUB-0017` | Amending a plan's module set leaves every existing `TenantSubscription` row byte-identical — no attribute is copied at assignment | `AC-SUB-0005` |

## API — `tests/API.Tests`

| ID | Scenario | AC |
|---|---|---|
| `TS-SUB-0018` | A request to a gated route of an unentitled module returns `403` with problem type `module-not-enabled`, and **the handler is never entered** — asserted by a probe the handler would set | `AC-SUB-0018` |
| `TS-SUB-0019` | The same problem type comes back from a GL route, a Payroll route, an Attendance route **and** an HR route. **Four modules, because a per-module variant would pass a single-module test** | `AC-SUB-0019` |
| `TS-SUB-0020` | A tenant with no entitlement at all authenticates, selects its tenant, refreshes, logs out, and reaches platform support and the subscription surface. **This is the lock-out scenario** — if it fails, a lapsed tenant cannot be restored without a database edit | `AC-SUB-0021` |
| `TS-SUB-0021` | The enabled-module response body carries the module-key field and **no other property**, asserted over the serialized JSON rather than the DTO, so a field added by a base type is caught | `AC-SUB-0022` |
| `TS-SUB-0022` | A tenant user holding **no** permissions receives a response byte-identical to the one a tenant administrator receives | `AC-SUB-0023` |
| `TS-SUB-0023` | A tenant-plane caller holding every permission the product defines receives `403` from **every** commercial write route, enumerated rather than sampled | `AC-SUB-0007` |
| `TS-SUB-0024` | The same caller receives `403` from every commercial **read** route — plans, subscriptions, grants, invoices, attempts | `AC-SUB-0035` |
| `TS-SUB-0025` | A platform-plane `View` caller receives records for **two different tenants** in one response | `AC-SUB-0034` |
| `TS-SUB-0026` | The access token issued after an entitlement change is decoded and its claim set compared **for equality** with `FP-002`'s approved set. **Equality, not containment** — containment would pass with an entitlement claim added | `AC-SUB-0013` |
| `TS-SUB-0027` | Grant appended; the **same** token is replayed against the newly granted module's route and succeeds. No re-authentication, no restart | `AC-SUB-0014` |
| `TS-SUB-0028` | Two tenants on the same plan. The plan's module set is amended once; **both** tenants' next requests reflect it. **The second tenant is the assertion** — an invalidation keyed on `TenantId` passes for the first and fails for this | `AC-SUB-0015` |
| `TS-SUB-0029` | An expired tenant **signs in and is refused a gated module**; a suspended tenant is refused at authentication. The two produce **different** modelled outcomes. Asserted on the outcome, not the public response, which stays generic under `FP-002` | `AC-SUB-0029` |
| `TS-SUB-0030` | Assigning a permission belonging to an unentitled module to a role is refused at grant time | `AC-SUB-0024` |
| `TS-SUB-0031` | A role granted a permission while entitled: the permission check succeeds, entitlement lapses and the same check fails, entitlement returns and it succeeds again — **with the role assignment unread and unwritten throughout** | `AC-SUB-0025` |
| `TS-SUB-0032` | Every commercial write records the authenticated platform principal and the instant; a write with no principal is refused rather than attributed to a service account | `AC-SUB-0010` |

## Integration — `tests/Integration.Tests`, real SQL Server

Run through `scripts/gate.sh`, which holds the memory preconditions and the catalog reaping.

| ID | Scenario | AC |
|---|---|---|
| `TS-SUB-0033` | A persisted subscription record is modified, then deleted, and **both are refused by the persistence guard** — not by a handler. **This scenario fails today**, because `PlatformDbContext` has no `PreventAppendOnlyMutation`; it is the executable form of that obligation | `AC-SUB-0003`, `AC-SUB-0044` |
| `TS-SUB-0034` | Two concurrent appends for one tenant at the same instant: **one row, one refusal.** Asserted against real SQL because the unique constraint is half the mechanism and the tenant-row lock is the other half | `AC-SUB-0004` |
| `TS-SUB-0035` | After a second record is appended, the first is re-read and compared **field by field** with what was written | `AC-SUB-0001` |
| `TS-SUB-0036` | With the tenant ERP database unreachable, the subscription surface answers and a gated ERP route returns `TenantDatabaseUnavailable`. **The failure this catches is an entitlement read that touches the tenant database** — which would take the tenant's whole API down rather than one page | `AC-SUB-0009` |
| `TS-SUB-0037` | Row counts in a module's tables taken before entitlement is removed and after; identical. Every record is then read back after entitlement is restored | `AC-SUB-0026` |
| `TS-SUB-0038` | After the migration runs against an empty database, `TenantSubscriptions` and `SubscriptionPlans` are **both empty**. **A seeded default plan would pass every other test in this package and silently entitle the estate** | `AC-SUB-0046` |
| `TS-SUB-0039` | Every monetary column in the package's tables reports precision 19 scale 4 from the schema, and a value with four decimal places round-trips unchanged. **Read from the schema, not from a sample row** | `AC-SUB-0037` |
| `TS-SUB-0040` | The plan tables carry no `TenantId` column, asserted from the schema | `AC-SUB-0006` |
| `TS-SUB-0041` | The `Tenant` row is read before and after the expiry boundary is crossed and its `RowVersion` is **unchanged** — nothing wrote to it | `AC-SUB-0030` |

## Architecture — `tests/Architecture.Tests`

| ID | Scenario | AC |
|---|---|---|
| `TS-SUB-0042` | The composed permission catalog contains **no** tenant-plane name matching subscription, plan, grant, invoice or billing administration. **Asserts an absence**, which is the only way to test that there is nothing to grant by mistake | `AC-SUB-0008` |
| `TS-SUB-0043` | **Every** platform-plane permission name reachable in the built host — `Platform.Support.*` included — is used with `RequirePlatformPermission` and **never** with `RequirePermission`, and every tenant-plane name only with `RequirePermission`. **The `Platform.` prefix does not distinguish the planes**: `Platform.Users.View` is tenant-plane, `Platform.Support.Administer` is platform-plane. **Asserted over the whole set per `DEC-L-010`** — a guard covering only this package's six would leave the ambiguity that already shipped untested, and this test is the control that `REQ-SUB-0004` otherwise lacks | `AC-SUB-0045` |
| `TS-SUB-0044` | Reflection over the built host: exactly ten route groups carry the enablement convention and the seven exempt ones do not. **Counted, not sampled** — a new module added without the gate is the failure this catches, and it is the failure that grows every release | `AC-SUB-0020` |
| `TS-SUB-0045` | `TenantStatusChangeReason` contains no member matching a commercial concept. **The guard is what stops `OD-SUB-0010`'s orthogonality being eroded one convenient enum member at a time** | `AC-SUB-0031` |
| `TS-SUB-0046` | No type, property, column or enum member in the package matches *trial*. **Asserts an absence** ruled by `OD-SUB-0014`, and it is the kind of concept that reappears because it seems obviously useful | `AC-SUB-0033` |
| `TS-SUB-0047` | No transport contract type in the package declares a property matching a primary account number, card verification value, cardholder name or expiry date, and no logging call in the package writes a payment request or response body. **A field on a DTO needs no migration and appears in no schema review** — this test is where it is caught | `AC-SUB-0048` |

## Seat admission — `DEC-L-009`

Ruled after this package raised the gap. Two of these belong in `API.Tests` because admission runs on
the tenant-user surface; one is domain.

| ID | Scenario | AC |
|---|---|---|
| `TS-SUB-0048` | **`API.Tests`.** With a resolved cap of *n*: creating the *n*th `TenantUser` succeeds and the *n+1*th is refused. The refusal body names the **cap**, the **current count** and the **plan** — all three asserted, because an error carrying only one of them cannot be acted on by the person who hit it. Reactivating a deactivated user into a full tenant is refused on the same terms, **which is the case a create-only check misses** | `AC-SUB-0049` |
| `TS-SUB-0049` | **`API.Tests`.** A tenant standing **at** its cap and a tenant standing **over** it: every existing user authenticates normally in both. Asserted additionally by the entitlement resolver recording no seat lookup on the authentication path — **the cap must not merely permit the login, it must not be consulted** | `AC-SUB-0050` |
| `TS-SUB-0050` | **`Platform.Tests`.** A tenant with more users than its new plan allows after a downgrade: no user is deactivated, no user is flagged, the excess is reported as a billable quantity, and the count of active users is unchanged before and after the plan change | `AC-SUB-0051` |

Fifty scenarios, `TS-SUB-0001` through `TS-SUB-0050`, contiguous.

---

## Scenarios that cannot be written yet — declared

**The seat-cap consequence is no longer a gap.** `DEC-L-009` ruled it on 2026-08-25 — enforced at
admission, never at login — and `TS-SUB-0048`, `TS-SUB-0049` and `TS-SUB-0050` cover it. It is
recorded here because the gap was declared in this package's first draft and a reader comparing
versions should see it closed rather than dropped.

**No grace-period scenario, and none is needed.** `DEC-L-009` ruled that none exists: a grace period
softens a lapse, and with the cap enforced at admission nothing lapses. `TS-SUB-0049` already forbids
the behaviour a grace period would have softened.

**No payment-capture scenario.** `REQ-SUB-0026` is `T-010`'s. `TS-SUB-0047` tests the **boundary** —
that nothing here carries cardholder data — and it belongs to `AC-SUB-0048` under `REQ-SUB-0025`.
Writing a capture scenario would imply this package specified capture, and it did not.

## What `AC-SUB-0047` needs, and why no scenario carries it

`AC-SUB-0047` is a **release condition**: the enablement gate must not be active in the release that
introduces the migration, because every existing tenant is unentitled the moment it runs.

**No automated suite can observe a release boundary.** A test asserting "the gate is off" would pass
trivially before the gate is built and fail permanently after it is switched on — it would assert the
opposite of the intent. The criterion is verified by whoever schedules the release, and the matrix
marks it as a declared gap rather than pretending a scenario covers it.
