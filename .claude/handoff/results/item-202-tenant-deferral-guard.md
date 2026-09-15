# item 202 — the tenant deferral now fails instead of resting on a document

**Gated work.** `TenantTransportDeferralTests` — 7 tests (1 ban, 1 control, 5 discriminator cases), all
green; both plants verified.

## What it asserts, and why not the way its predecessor did

**Over ROUTES ACTUALLY MAPPED BY THE RUNNING HOST**, from `EndpointDataSource` via
`HostWebApplicationFactory` — the established pattern of `AttendanceRouteInventoryTests`.

⚠ **Its predecessor scanned SOURCE FOR DECLARATION SPELLINGS and passed for the wrong reason**: it looked
for `CompanyProvision` after `Company` had shipped, and `TenantController` could never fire because this
codebase declares no controllers. **A transport that exists is a mapped route, whatever its types are
called.**

## ⚠ THE DISCRIMINATOR IS A SEGMENT, NOT A PREFIX — AND THAT WAS NEARLY THE SAME BUG AGAIN

`TenantUserEndpointRouteBuilderExtensions` maps **`/api/platform/tenant-users/{id}/…`, and those routes
SHIPPED.** A `StartsWith("/api/platform/tenant")` check would have reddened the gate on working code — the
exact class of error that retired the last guard, arriving from the other side. **Caught before writing the
file**, by enumerating the live platform route literals first.

So the ban matches `/api/platform/tenants` **exactly, or followed by `/`**, and five `[InlineData]` cases
pin that boundary — including `/api/platform/tenant-users/{tenantUserId}/deactivation` as a **required
negative drawn from live code**.

## The failure message is the deliverable

It names `AC-TEN-0020`, says the omission is **deferred rather than forgotten**, points at
`item-152-route-table.md` and at `agent/T-155-tenant-transport` — *"a complete implementation already
exists… whose own commit says BLOCKED on AC-TEN-0020"* — and then:

> **"IF YOU JUST BUILT THIS, THE WORK IS PROBABLY FINE AND THE DEFERRAL IS THE THING TO SETTLE.** Retire
> AC-TEN-0020's tenant-endpoint row consciously… **and then DELETE THIS TEST in the same commit.** It
> exists only to make the deferral fail loudly instead of resting on a document being read, and it has no
> purpose the day the work lands."

⚠ **A guard that does not name its own retirement condition becomes the next `DEC-L-030` casualty.** This
one names it.

## ⚠ Both plants, and the second is the one that matters

| plant | result |
|---|---|
| map a real `/api/platform/tenants` route | **the ban reddens**, message printed above; controls stay green |
| break the matcher (`PlatformPrefix` → a prefix nothing matches) | ⚠ **the CONTROL reddens while the BAN PASSES VACUOUSLY** |

**The second plant reproduces exactly how the predecessor died** — a ban whose enumeration finds nothing is
indistinguishable from a ban that holds — **and shows the control catching it.** The known positives
(`/api/platform/auth`, `/api/platform/companies`, `/api/platform/tenant-users`) are **live routes, not
planted ones.**

Both plants restored from the index; 7 of 7 green after.

## Scope
- **The ban covers the ROUTE surface.** A tenant registry exposed under a different path — a GraphQL field,
  a gRPC service, a route not under `/api/platform` — would not be caught. The deferral is written in terms
  of those routes, so this matches what `AC-TEN-0020` actually says.
- It cannot force the deferral to be *retired consciously*; it can only make ignoring it fail. **That is
  the difference between a guard and a decision.**
