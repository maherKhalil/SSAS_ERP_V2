# FP-014 — Acceptance criteria

Written from the ruling set of 2026-08-25. Closes the package alongside
[`test-scenarios.md`](test-scenarios.md) and [`traceability-matrix.md`](traceability-matrix.md).

Each `AC-SUB-####` is stated so it can be **failed**. Criteria that depend on an unruled decision say
which and are marked as declared gaps rather than guessed, and the three build obligations this
package discovered are carried here as criteria rather than left in prose — they are the things most
likely to be dropped between an analysis package and an implementation prompt.

---

## The subscription record

| ID | Criterion | Req |
|---|---|---|
| `AC-SUB-0001` | Appending a second subscription record makes it the one in force, and the first remains readable and unchanged — byte for byte, including its `EffectiveFromUtc` | `REQ-SUB-0001` |
| `AC-SUB-0002` | `EntitlementAt(tenant, T)` for a `T` earlier than the second record's `EffectiveFromUtc` resolves the **first** record. History is queryable by instant, not only as an audit list | `REQ-SUB-0001` |
| `AC-SUB-0003` | An attempt to update or delete a subscription record is **refused**, not silently ignored, and the refusal comes from the persistence guard rather than from a handler that remembered to check | `REQ-SUB-0001` |
| `AC-SUB-0004` | Appending a record whose `EffectiveFromUtc` is **equal to or earlier than** the tenant's current maximum is refused. Two concurrent appends produce one record and one refusal, never two records | `REQ-SUB-0001` |
| `AC-SUB-0005` | A plan is referenced by many tenants; amending it changes no subscription record, and no plan attribute is copied into a subscription row at assignment | `REQ-SUB-0002` |
| `AC-SUB-0006` | The plan tables carry **no `TenantId` column**, and no route reachable on the tenant plane can create, amend or retire a plan | `REQ-SUB-0003` |
| `AC-SUB-0007` | A tenant-plane caller holding **every permission the product defines** receives `403` from every subscription, plan, grant and invoice write route | `REQ-SUB-0004` |
| `AC-SUB-0008` | **No tenant-plane permission name for subscription administration exists in the composed catalog.** The criterion is the absence — there is nothing to grant by mistake | `REQ-SUB-0004` |
| `AC-SUB-0009` | With the tenant's ERP database unreachable, the subscription read and administration surface still answers, and a gated ERP route fails with a modelled `TenantDatabaseUnavailable` rather than the whole API becoming unreachable for that tenant | `REQ-SUB-0005` |
| `AC-SUB-0010` | Every subscription, grant and invoice write records **who** and **when**, and the actor is the authenticated platform principal rather than a service account | `REQ-SUB-0006` |

## Entitlement resolution

| ID | Criterion | Req |
|---|---|---|
| `AC-SUB-0011` | For a `(tenant, module)` pair the product answers enabled or not from `plan ∪ grants`, and the answer is the same whether asked through the enablement gate or the enabled-module endpoint | `REQ-SUB-0007` |
| `AC-SUB-0012` | A tenant with **no** subscription record resolves to **no modules** — not an error, and emphatically not all modules. A missing record is a state, not a failure to configure | `REQ-SUB-0007` |
| `AC-SUB-0013` | The access token issued after an entitlement change carries **exactly** the `FP-002` claim set — no subscription, billing, plan, module or entitlement claim, and no additional claim of any name | `REQ-SUB-0008` |
| `AC-SUB-0014` | Appending a grant makes the granted module reachable on the **next request**, with the same token and without restarting the host | `REQ-SUB-0009` |
| `AC-SUB-0015` | Amending a **shared plan's** modules changes entitlement on the next request for **every tenant whose in-force record names that plan** — not only for a tenant that is separately touched | `REQ-SUB-0009` |
| `AC-SUB-0016` | A grant whose `LimitValue` is at or below the plan's current cap for that key is **refused at write**, with an error naming the plan's value | `REQ-SUB-0010` |
| `AC-SUB-0017` | The resolved cap is `max(plan, grants)`. A grant row carrying a **lower** value than the plan — however it came to exist — **does not lower the resolved cap** | `REQ-SUB-0010` |

## Enforcement

| ID | Criterion | Req |
|---|---|---|
| `AC-SUB-0018` | A request to a gated route of a module the tenant is not entitled to is refused **before the handler runs**, with `403` and problem type `module-not-enabled` | `REQ-SUB-0011` |
| `AC-SUB-0019` | That problem type is **identical on every gated route of every module**. A per-module variant is a failure of this criterion even if each variant is individually correct | `REQ-SUB-0012` |
| `AC-SUB-0020` | Exactly the **ten** gated route groups carry the enablement convention and the **seven** exempt ones do not, asserted by reflection over the built host rather than by reading `Program.cs` | `REQ-SUB-0012` |
| `AC-SUB-0021` | A tenant with **no entitlement at all** can still authenticate, select its tenant, refresh, log out, and reach platform support and the subscription surface | `REQ-SUB-0013` |
| `AC-SUB-0022` | The enabled-module response contains module keys and **nothing else** — no price, plan name, term, cap, invoice or payment state. The criterion is failed by any additional field, including one that seems harmless | `REQ-SUB-0014`, `REQ-SUB-0021` |
| `AC-SUB-0023` | An authenticated tenant user holding **no permissions** receives their tenant's enabled-module set, and the response is identical to the one an administrator receives | `REQ-SUB-0014` |
| `AC-SUB-0024` | Assigning a permission belonging to a module the tenant is not entitled to is **refused at grant time** | `REQ-SUB-0015` |
| `AC-SUB-0025` | A role holding a permission granted while entitled **stops satisfying** that permission check when entitlement lapses, and satisfies it again when entitlement returns — with the role assignment unchanged throughout | `REQ-SUB-0015` |
| `AC-SUB-0026` | Losing entitlement to a module deletes **no row** in that module's tables — counts before and after are identical — and every record is readable again on re-entitlement | `REQ-SUB-0016` |

## Term, expiry and tenant status

| ID | Criterion | Req |
|---|---|---|
| `AC-SUB-0027` | A `Fixed` term requires an end **after** its start; a `Perpetual` term requires a null end. `Fixed` with a null end and `Perpetual` with an end are both refused at construction | `REQ-SUB-0017` |
| `AC-SUB-0028` | Advancing the clock past `TermEndUtc` changes the resolved commercial state from `InTerm` to `Expired` **with no row written and no job run** | `REQ-SUB-0017`, `REQ-SUB-0018` |
| `AC-SUB-0029` | A tenant whose term has expired **authenticates successfully** and is refused every gated module. A **suspended or archived** tenant is still refused at authentication, and the two outcomes remain **distinct** — one is commercial and reversible by the customer, the other administrative | `REQ-SUB-0018`, `REQ-SUB-0019` |
| `AC-SUB-0030` | Expiry writes nothing to `Tenant`. The tenant's `TenantStatus` remains `Active`, and a suspended tenant that is paid up remains `Suspended` | `REQ-SUB-0019` |
| `AC-SUB-0031` | `TenantStatusChangeReason` contains **no commercial member** — no `NonPayment`, no `Expired`, no `SubscriptionLapsed`. Asserted over the enum, so adding one fails the build's guards rather than review | `REQ-SUB-0019` |
| `AC-SUB-0032` | A cached entitlement entry **does not outlive `TermEndUtc`**. A tenant cached as entitled at `TermEndUtc − 1s` is refused at `TermEndUtc + 1s` without any invalidation event having occurred | `REQ-SUB-0018` |
| `AC-SUB-0033` | **No trial state, flag, column or enum member exists anywhere in the package.** A trial is a plan with a short term and nothing else. The criterion is the absence | `REQ-SUB-0020` |

## Reading and disclosure

| ID | Criterion | Req |
|---|---|---|
| `AC-SUB-0034` | A platform-plane caller with `Platform.Subscriptions.View` reads records for **more than one tenant** in a single response — the read is not tenant-filtered | `REQ-SUB-0022` |
| `AC-SUB-0035` | A tenant-plane caller receives `403` from every commercial read route — plans, subscriptions, grants, invoices, payment attempts | `REQ-SUB-0021` |

## The commercial record

| ID | Criterion | Req |
|---|---|---|
| `AC-SUB-0036` | Assigning a plan to a tenant whose billing currency has **no price row** on that plan is refused, with an error naming the currency | `REQ-SUB-0023` |
| `AC-SUB-0037` | Every monetary column in the package is `decimal(19,4)` and every monetary response field round-trips four decimal places without loss — asserted over the model, not sampled | `REQ-SUB-0024` |
| `AC-SUB-0038` | An issued invoice refuses every edit. Its number is assigned **at issue**, is unique across all tenants, and is **not reused** after the invoice is voided | `REQ-SUB-0025` |
| `AC-SUB-0039` | An invoice covering a period containing a mid-term plan change carries **one line per subscription record in force during that period**, each naming the record it bills against | `REQ-SUB-0025`, `REQ-SUB-0028` |
| `AC-SUB-0040` | A seat usage sample records the `TenantSubscriptionId` in force **at the observed instant**, and that stamp does not change when a later subscription record is appended | `REQ-SUB-0027` |
| `AC-SUB-0041` | An overage for a past period is judged against the plan in force **then**. Upgrading to a larger plan afterwards does not erase it, and downgrading afterwards does not create one | `REQ-SUB-0027` |
| `AC-SUB-0042` | A mid-term plan change is prorated for the unused portion of the term, and the two lines it produces sum to the amount the tenant owes for the period | `REQ-SUB-0028` |
| `AC-SUB-0043` | The resolved cap for a limit key is available at the enforcement point in the same call that resolves module entitlement — one resolution, not two | `REQ-SUB-0027` |

## The three build obligations

**These are criteria, not notes.** Each was discovered by reading the code during T-006 or T-007, each
is invisible from inside this package's documents alone, and each is the kind of thing that survives
as an acceptance criterion and evaporates as prose.

| ID | Criterion | Req |
|---|---|---|
| `AC-SUB-0044` | **`PlatformDbContext` refuses `Modified` and `Deleted` for `IAppendOnlyEntity`**, by the same mechanism `TenantDbContext.PreventAppendOnlyMutation` uses and called from its own `SaveChangesAsync`. **No FP-014 entity may carry `IAppendOnlyEntity` until this exists** — the interface without the guard is the appearance of immutability with none of it | `REQ-SUB-0001` |
| `AC-SUB-0045` | **Every permission name in the platform-plane set** — `Platform.Support.*` included, not only this package's six — is used **only** with `RequirePlatformPermission` and never with `RequirePermission`, and every tenant-plane name is used only with `RequirePermission`. The `Platform.` prefix does not distinguish the planes: `Platform.Users.View` is tenant-plane and `Platform.Support.Administer` is platform-plane. So `REQ-SUB-0004` is enforced by this guard or by careful reading, and careful reading is not a control. **Widened from this package's four prefixes to the whole set by `DEC-L-010`** — guarding only the new names would have left the ambiguity that already shipped unasserted | `REQ-SUB-0004` |
| `AC-SUB-0046` | The migration that creates these tables **inserts no subscription row and seeds no plan.** Immediately after it runs every existing tenant is unentitled, which is correct under `CON-0001` and is exactly why `AC-SUB-0047` exists | `REQ-SUB-0007` |
| `AC-SUB-0047` | **RELEASE CONDITION.** No release leaves a tenant reachable by a gated route with no subscription record — otherwise that deployment locks out the entire estate. **The gate (T-040) and the trial seed (T-041) ship in the SAME release, and the seed must not precede the gate within it.** *(Amended 2026-08-26 — the original clause read "the enablement gate is not active in the release that introduces the migration", which held the gate back from the migration's release. The order actually taken inverts that: the gate shipped first and the seed follows. **The condition is unchanged and the ordering clause is not** — see the note below, which records what the original required and why the substance survives.)* | `REQ-SUB-0011` |
| `AC-SUB-0048` | **No request body accepted, no response body returned, and no log statement written by this package carries a primary account number, card verification value, cardholder name or expiry date.** Asserted over the transport contract types by reflection, so a field added later fails a test rather than a review | `REQ-SUB-0025` |

### What the ordering clause required, and why amending it does not weaken the condition

**The original clause was written when the resolver did not exist.** With nothing reading the tables, the
only safe sequence visible from where it was written was: create the tables, populate them, and *then*
switch the gate on. It said so, and `AddSubscriptionCommercialPlane`'s own `THROW` message repeats it.

**The sequence actually taken is the mirror image.** T-040 switched the resolver first, with the interim
state — a tenant holding no record reaches no gated module — asserted as the ruled outcome rather than
left implicit; T-041 then seeded. Nothing deployed in between, so **the merge order was sound and the
release order is the thing that matters**.

**What the condition has always been about is the deployment, not the branch.** Either sequence satisfies
it, because the seed migration runs at deploy time and therefore before any request reaches a gated route.
**Shipping the two apart does not satisfy it in either direction**, and that is now the binding half:
T-040 without T-041 locks every tenant out of every module, and T-041 without T-040 writes commercial
records that nothing reads.

**Its verification is still a human one and its tests cell is still a declared gap** — see
[`test-scenarios.md`](test-scenarios.md). What T-041 added is not a test of *this* criterion but of its
outcome: `AC-SUB-0052`–`AC-SUB-0054` below assert that after the migrations run, no tenant is left
without a record.

## Seat admission — the cap is enforced at the grant, never at login

**`DEC-L-009`**, ruled 2026-08-25 after this package raised the gap. `OD-SUB-0017` ruled *seats plus
limits* and left the consequence of exceeding a cap unstated; the ruling resolves it, and it resolves
it by rejecting both of the answers originally on the table.

| ID | Criterion | Req |
|---|---|---|
| `AC-SUB-0049` | Creating or activating a `TenantUser` that would take the tenant **past** its resolved seat cap is **refused at that moment**, and the error names the **cap**, the **current count** and the **plan**. All three, because an error saying only "seat limit reached" cannot be acted on by the person who just hit it | `REQ-SUB-0027` |
| `AC-SUB-0050` | **Login is never refused for a seat cap.** A tenant standing at or above its cap authenticates every one of its users normally, and no seat check runs on the authentication path at all | `REQ-SUB-0027` |
| `AC-SUB-0051` | A plan change that puts a tenant **over** its new cap deactivates nobody and blocks nobody. The excess is **billed and reported**, and every existing `TenantUser` keeps working | `REQ-SUB-0027`, `REQ-SUB-0028` |

**Why the enforcement point is the grant. This no longer contrasts with `AC-SUB-0029`, and that is a
change.** It used to: expiry blocked login and a seat cap did not, and the asymmetry needed defending.
**`DEC-L-033` (2026-08-26) amended `OD-SUB-0009` so expiry gates modules instead**, which means **no
commercial event blocks authentication at all** and the rule is now uniform. The reasoning below is
kept because it is still why a seat cap is refused at the grant, and it is the argument the owner
applied to expiry when reconsidering it:

- **Expiry is dated, foreseeable and whole-tenant.** Everyone is affected at an instant everyone
  could see coming, and the tenant's administrator can renew.
- **A seat excess is incremental and arbitrary in who it would hit.** Blocking a login would enforce
  against whichever user happened to sign in next — someone who did nothing, at the moment they sat
  down to work, in an ERP of record. It converts a commercial disagreement into an operational
  outage for a person with no power to resolve it.

**Refusing at the grant tells the person who caused the excess, immediately and specifically, and
they can act on it.** `AC-SUB-0049` is that refusal, and `AC-SUB-0050` is the guarantee that it is
the *only* place the cap bites.

**No grace period, and none is needed.** A grace period softens a lapse; nothing lapses here. There
is no criterion asserting a grace period and none asserting its absence beyond `AC-SUB-0050`, which
already forbids the behaviour a grace period would soften.

## The trial — a plan with a short term, and one rule for every tenant

**`DEC-L-034`**, ruled 2026-08-26, using `OD-SUB-0014`. These criteria are the positive content of
`REQ-SUB-0020`; `AC-SUB-0033` is its negative half and the two are meant to be read together — **what the
trial *is*, and what the model must never grow to say it is.**

| ID | Criterion | Req |
|---|---|---|
| `AC-SUB-0052` | Every tenant existing when the seed migration runs holds the **all-module plan on a `Fixed` 14-day term**. **No status filter** — suspended and archived tenants are seeded like any other, because `OD-SUB-0010` made subscription state and `TenantStatus` orthogonal and a filter here is that coupling. **No history is reconstructed**: `EffectiveFromUtc` is the instant the seed ran, never the tenant's creation date | `REQ-SUB-0020` |
| `AC-SUB-0053` | Tenant creation issues **the same plan and the same term**, in the tenant's **own transaction**. One rule for existing and new tenants (`DEC-L-034`), so there is one thing to explain to a customer — and a single transaction makes *"tenant exists, trial does not"* unrepresentable rather than merely unlikely | `REQ-SUB-0020` |
| `AC-SUB-0054` | **Re-running the seed issues nothing further.** A tenant already holding **any** subscription record is left untouched, plan and effective instant unchanged. The failure this prevents is not a duplicate row: the record in force is the one with the greatest `EffectiveFromUtc`, so a trial appended after a purchased plan **silently becomes the plan that tenant is on** | `REQ-SUB-0020` |

**Why the third one is stated as "any record" rather than "any trial".** A guard checking for an existing
*trial* would re-issue to a tenant that had moved onto something else, which is the expensive case. The
cheap case — a duplicate trial — is the one a narrower guard would have caught.

**And why there is no grace period in any of the three.** `DEC-L-009` ruled none, and `DEC-L-033` bounds
what expiry costs: gated modules stop resolving and **login is untouched**, so a lapsed tenant can still
reach the surface it converts from. Fourteen days is short for an ERP and is the owner's ruling, not an
oversight.

Fifty-four criteria, `AC-SUB-0001` through `AC-SUB-0054`, contiguous.

---

## Criteria that cannot be written yet — declared, not guessed

### Payment capture — `REQ-SUB-0026` has no criterion here, deliberately

`OD-SUB-0016` ruled that the product captures payment itself, which puts cardholder data in PCI-DSS
scope and sits in tension with `ADR-001` Modular Monolith. **`T-010` owns that decision.**

The honest chain is `REQ-SUB-0026 → BR-SUB-0020 → T-010`, and the matrix carries it that way. What
this package **does** assert is the boundary — `AC-SUB-0048`, no cardholder datum anywhere on the
transport surface or in a log — and that criterion belongs to `REQ-SUB-0025`, the invoice, not to
`REQ-SUB-0026`.

**An acceptance criterion invented for `REQ-SUB-0026` would claim coverage this package does not
have**, and would read to a future implementer as though payment capture had been specified. A
declared gap is worth more than a fabricated criterion.
