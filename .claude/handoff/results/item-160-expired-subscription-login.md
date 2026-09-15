# item 160 — does login refuse an expired subscription?

**Measurement only. Nothing built, nothing edited.** Question: `Authentication.md:157` lists
**`Expired Subscription`** among its login failure scenarios. Is that current, residue, or a different
refusal wearing this name?

## ⚠ The answer is a fourth possibility: THE REFUSAL IS REAL, AND IT IS NOT AT LOGIN

**Login does not refuse an expired subscription. It cannot — by design, and tests pin that it does not.**
**But expiry does refuse, on gated module routes, with `403`.** The row is not residue; it is **filed
against the wrong surface**.

## What login actually reads

Authentication eligibility is a **total function of `TenantStatus` and nothing else**:

```csharp
// TenantAuthenticationEligibilityReadService
.Where(tenant => tenant.Id == tenantId)
.Select(tenant => (TenantStatus?)tenant.Status)
```

`FromStatus` is exhaustive over `Provisioning | Active | Suspended | Archived` and throws on anything
else. **There is no subscription-derived member, and no subscription reference anywhere in the
authentication application path.**

## Why it cannot be otherwise — the resolver says so itself

`TenantModuleEntitlement` (`Platform.API`), the real entitlement resolver:

> **"It cannot deny authentication."** It answers exactly one question — *is this module enabled for the
> current tenant* — and it is reachable only from `RequireModule`, which is applied to module route groups
> and to nothing else. The platform plane carries no module key (`REQ-SUB-0013`), so **the authentication,
> tenant-selection, refresh, logout, support, localization, identity and company surfaces never consult
> this at all.**

And the domain records the ruling at the point of the property:

> `// Expiry is read, never stored (OD-SUB-0010: orthogonal to TenantStatus, and expiry never writes it).`

`OD-SUB-0010` ratified: *"Orthogonal — independent dimensions, both evaluated on every request"*, and
*"expiry acts through the same enablement gate as every other entitlement."*

## Verified end to end, not read

Existing tests pin the exact behaviour — **14 passed, 0 failed**:

| test | asserts |
|---|---|
| `An_expired_tenant_is_refused_a_gated_route_with_403` | expiry **does** refuse — on a gated module route |
| ⚠ `An_expired_tenant_reaches_a_platform_plane_route_and_is_never_asked` | an expired tenant **reaches** the platform plane; entitlement is never consulted |
| ⚠ `An_expired_trial_tenant_reaches_the_platform_plane_without_entitlement_being_asked` | same, for the trial case |
| `A_request_before_expiry_caches_a_snapshot_that_still_refuses_after_it` | the snapshot refuses after expiry |
| `A_fixed_term_expires_after_its_end` / `A_perpetual_term_never_expires` | expiry is representable and computed |

**Authentication is a platform-plane route.** So the product's own test suite asserts that an expired
tenant logs in and is refused later, at the module boundary.

## ⚠ And the original diagnosis, repeated four times, was wrong about WHY

T-002 called this *"a login refusal for a state the product cannot represent."* **The state is perfectly
representable** — `SubscriptionTerm.HasExpiredAt` is real, tested, and evaluated on every request. The
defect was never that expiry cannot happen. It is that **the document files a real refusal under the wrong
failure surface**, which is a subtler error and survives exactly the reading that "cannot represent"
invites. *(A reading of the session creator would also have missed it: the creator is silent about
subscriptions because the decision was made not to put it there.)*

## The third possibility, ruled out explicitly

Nothing converts expiry into a `TenantStatus` change, so **no expired tenant is refused at login for an
administrative reason that merely looks commercial.** `OD-SUB-0010` states it — *"expiry never writes
it"* — and the ratified text keeps the outcomes distinct: *"A suspended or archived tenant is still
refused at authentication; that is administrative, not commercial."*

## On the document's status — advice, not an edit

The scenario list is **wrong as it stands**, so the row should be corrected or moved before any promotion:
a reader today concludes an expired subscription produces a login failure, and it produces a `403` on a
business module instead. **While a known-wrong row remains, `Draft` is the honest status.**

Once the row is corrected, `Draft` has no remaining justification I can see from the code: `FR-PLT-0001`
is delivered and carries 81 passing tests across authentication and the support plane (items 156–157).
**But status is a claim about the document, and the rest of its content is outside what I measured** — I
checked this one row.
