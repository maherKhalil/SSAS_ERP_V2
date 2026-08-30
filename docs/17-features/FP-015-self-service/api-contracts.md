---
package: FP-015
title: Self Service — API Contracts
status: DRAFT — established by T-076 measurement
version: 0.1
date: 2026-08-27
---

# API Contracts — FP-015

**Measured, not designed.** T-076 established what a self-service endpoint costs without reading this
package's documents, so nothing below is a confirmation of an assumption made here.

---

## 1. Mounting — `REQ-SS-0008` is true of the tree, not merely intended

**A route added to an existing module group inherits four things free** and declares two:

| Inherited | Declared |
|---|---|
| `RequireModule` — the `BR-PLT-0008` gate and its metadata | `RequirePermission` |
| the company-context filter | `WithName` |
| the response-security filter | |
| tags | |

`ModuleEnablement.cs:86-89` states the reason: *"Applied to the GROUP, never to a route… a route added
to an existing group later cannot forget the gate."*

**So `REQ-SS-0008` costs nothing to satisfy and cannot be forgotten.** Self-service routes are module
routes and are gated as such, structurally.

**One seam recorded because it will read as an oversight later:** the entitlement resolver currently
grants everything (`ModuleEnablement.cs:13-16`), because no subscription data exists yet.
**`BR-PLT-0008` is wired and not yet satisfiable — the gate is real and its answer is always yes
today.** `AC-SS-0013` and `AC-SS-0014` are therefore specified against a seam, not against a behaviour
that can be observed end-to-end now.

---

## 2. The no-identifier contract — `AC-SS-0007` is assertable on all four surfaces

**The criterion was written on the assumption that a contract's members can be enumerated. T-076
confirms it, including the surface expected to fail:**

```
body           reflection over the module's *Request records     AttendanceTransportContractTests.cs:31-36
path           RouteEndpoint.RoutePattern via MappedRoutes()     PayrollRouteInventoryTests.cs:42-56
query/header   Metadata.GetMetadata<MethodInfo>() -> GetParameters()   ModuleEnablementCoverageTests.cs:50
```

**Query and header were the doubtful pair.** Minimal-API parameters do not appear in a route pattern —
but the handler's `MethodInfo` sits in endpoint metadata, **and this suite already reads it** to
attribute endpoints to modules. `GetParameters()` follows from the same handle.

**Recorded limit (T-076's own):** the mechanism is demonstrated **reachable, not used** — no current
test inspects handler parameters. **`TS-SS-0003` is the first.**

**The body surface already exists inside Attendance**, derived rather than listed, and carries its own
non-vacuity assertion — the shape `TS-SS-0003` should follow rather than invent.

---

## 3. Resolving the caller — and the mechanism `REQ-SS-0005` was written against was wrong

**`TenantUserId` is null only when there is no tenant session at all** — unauthenticated, platform
plane, or a background composition. Null on any of seven claim checks, all canonical-form asserted,
never a throw (`CurrentAuthenticationSessionAccessor.cs:16-36`).

**A tenant-authenticated caller always has a `TenantUserId`.**

### So there are two refusals with two causes, and only one is this package's

| Cause | Where it is answered | Whose |
|---|---|---|
| no tenant session | never reaches a handler | the authentication layer's |
| a tenant user with **no linked employee** | the handler, as an ordinary result | **FP-015's** |

**`REQ-SS-0005` says the right thing for the wrong reason.** It reads as though the absence is a null
identity; **the absence is a miss in `UserEmployeeLink`.** Corrected here and in `requirements.md`
rather than left for whoever implements it to discover — **the requirement's behaviour is unchanged;
only its mechanism was wrong.**

---

## 4. The refusal shape — and the trap that would violate `AC-SS-0009`

**The good half:** refusals go out through `Results.Problem` (`ApiProblems.cs:43-59`). **Nothing is
thrown and no logger is touched**, so *no exception, no error-log entry* is satisfied by construction.

**The trap:**

```
AttendanceApiErrorMapper.cs:149   _ => ApiErrors.WriteFailure
ApiErrors.cs:32                   WriteFailure = new(500, "request.failed")
```

**A handler returning a new `Result.Failure` throws nothing, logs nothing, and answers `500
request.failed`** until someone adds a line to a second file. **The handler reads entirely correctly
while doing it.**

**And no guard asserts that a module's errors are mapped rather than falling through** (T-076
searched; none found).

**So `AC-SS-0009`'s no-5xx is one unwritten line away from being violated.** This is FP-006P's shape
and FP-011's shape at once: **an absence, elsewhere, that leaves the first file looking right.**

**Consequence for this package, stated as an obligation rather than a note:** the self-service refusal
error **must have an explicit mapper entry**, and `TS-SS-0007` must assert the **status code**, not
merely the absence of an exception. **A test that only checks "nothing threw" passes against the 500.**

---

## 5. Route inventory — the two modules differ again, and worse than they did on permissions

| | Payroll | Attendance |
|---|---|---|
| exact route inventory | `PayrollRouteInventoryTests.cs:42-56` | **none** |
| every route requires a permission | yes | **none** |
| route requires the permission the inventory names | `:74-84` | **none** |
| occurrences of `api/attendance` in `tests/` | — | **zero** |

**An Attendance route added with no `RequirePermission` would be anonymous and nothing would fail.**
There is no fallback policy — the `RequireAuthenticatedUser()` at
`PermissionAuthorizationPolicyProvider.cs:75` is inside `CreatePolicyBuilder()`, which builds *named*
policies, not `AuthorizationOptions.FallbackPolicy`.

**Nor does the T-072 join catch it**, and T-076 established that by checking rather than assuming: a
permission-less route sorts into the accounting test's *anonymous* bucket and the three-bucket
identity still balances, so it stays green.

**This is a prerequisite for FP-015's Attendance half, not a follow-up** — T-077 plants the claim and
closes the gap.

---

## What this file does not decide

**No URLs, no route names, no request or response shapes.** Those belong with the implementation
slice, and T-076 was explicitly forbidden from proposing them so that this file could rest on
measurement. **The contract asserted here is a negative one — what a self-service route must not
carry — and it is complete as stated.**
