---
package: FP-016
title: Platform Support Surface
module: Platform (support plane)
status: DESCRIPTIVE — records a surface that is already built and already pinned by tests
version: 1.0
date: 2026-08-30
---

# FP-016 — Platform Support Surface

## ⚠ Read this first: the tests are the authority, this document is not

**Twelve live routes had no feature contract until this file existed. They were never unpinned.**
`PlatformSupportAuthorityRouteInventoryTests.The_support_authority_route_surface_is_exactly_the_documented_inventory`
asserts the surface **exactly** — a route added or removed **fails the build**, which no document can do.

**This file exists for humans who need to know the surface is there.** Where it disagrees with the tests,
**the tests are right and this file is stale.** Do not treat it as a source of truth, and do not "correct"
the code to match it.

---

# The surface

**A complete privileged cross-tenant administration plane**, discovered on 2026-08-30 by asking a question
nobody had asked: *which built routes appear in no contract document?*

**Authority — nine routes**, all requiring `PlatformPermissionNames.AdministerPlatformSupport`:

    GET   /support/principals                 POST  /support/principals
    GET   /support/principals/{id}            POST  /support/principals/{id}/disable
    GET   /support/principals/{id}/assignments POST /support/principals/{id}/grant
    GET   /support/principals/{id}/permissions POST /support/principals/{id}/reenable
                                              POST  /support/principals/{id}/revoke

**Authentication — three routes:** `POST /support/auth/login` and `/refresh` are `AllowAnonymous`
**by necessity — they issue the session**; `/logout` requires authorization.

---

# How it is protected

**Plane separation is structural, not incidental.** `RequirePlatformPermission` resolves to
`PermissionPolicyNames.PlatformPrefix`, a **deliberately separate helper** from the tenant-plane
`RequirePermission` (`TenantPrefix`), commented *"the two must not mix"* (`ADR-015` §8).

**Refusal is tested and universally quantified** — `PlatformSupportAuthorityAuthorizationTests`:

- `Every_authority_route_rejects_an_anonymous_request`
- **`Every_authority_route_rejects_a_tenant_plane_token_carrying_the_administer_name`** — the right
  permission **name** on the wrong **plane**
- `Every_authority_route_rejects_a_platform_token_without_administer`
- `Every_authority_route_rejects_a_mixed_plane_token`

**`Every_…`, not a sample: a route added tomorrow inherits all four.**

**The anonymous door is limited twice over.** Login: **30/minute per IP** *and* **5 per 15 minutes per
identity+IP**, enforced together. Refresh 10/min, logout 5/min. Keys are HMAC'd. **The limiter's default
switch arm is 1/minute, so an endpoint added without a case is throttled rather than unlimited.**

**Account lockout: 5 failed attempts, 15 minutes, held on the ACCOUNT rather than the IP.** ⚠ The
per-identity limiter key includes the address, so rotation buys 5 attempts per IP — **lockout is not
IP-scoped, so rotation buys nothing against one account.** **The two controls fail in different dimensions,
which is why the second is a backstop and not a duplicate.**

**Credentials:** PBKDF2-HMAC-SHA512, **100,000 iterations**, 128-bit salt, 256-bit subkey — read from the
running assembly, not from documentation. **Compromised-password checking is enabled and OFFLINE**
(`OfflineCompromisedPasswordChecker`), so no credential material leaves the process. **A legacy hash still
authenticates and is re-hashed at current cost on use** — the floor is not a lockout.

---

# Two properties that do not read correctly from their own numbers

**⚠ REVOKE IS NOT IMMEDIATE; DISABLE IS.** Pinned by name:
`Self_revoke_of_administer_succeeds_and_the_issued_token_KEEPS_ITS_CLAIM_UNTIL_EXPIRY` against
`Self_disable_succeeds_REVOKES_PLATFORM_SESSIONS_and_leaves_security_version_untouched`.

**The window is at most fifteen minutes, and `JwtOptionsValidator` refuses any configured value above it —
so misconfiguration cannot widen it.** **During an incident: disable, do not revoke.** *"Revoke" reads as
immediate to almost everyone and it is not.*

**⚠ AND THE TWO COST PARAMETERS ARE FLOORS, NOT COINCIDENCES.** The hasher's 100,000 iterations **equals the
.NET 8 default, so the setting changes no behaviour today** — and `PasswordHasherOptions` is bound from
`Authentication:PasswordHasher` and validated `>= 100_000` with `.ValidateOnStart()`.

**That validator is not a no-op.** It survives **a framework upgrade lowering the default, a configuration
edit, and an environment override**, and it **fails at start-up rather than silently hashing weaker.**
The same is true of the fifteen-minute token cap.

> **A value that cannot be configured past a bound is a different kind of fact from one that merely happens
> to be adequate today — and neither reads that way from the number alone.**

**Do not delete either setting as redundant.**

---

# ⚠ The one thing this surface cannot guarantee about itself

**The rate limiter's window store is an in-process dictionary. It does not span replicas.**

The design knows: **production start-up throws unless the HMAC secret is at least 32 characters AND
`UpstreamDistributedRateLimitingEnforced` is set.**

**⚠ That flag is a DECLARATION, NOT A VERIFICATION. The application cannot check that an upstream limiter
actually exists.** On multiple instances, **the per-IP limits divide by the replica count while the account
lockout does not** — the backstop holds, the front door widens.

**This is a deployment obligation, recorded as `OWNER-DECISIONS.md` entry 19.** **Nothing in this repository
can answer it and nothing in it will ever fail if the answer is no.**

---

# What has not been examined

**Stated so this file is not read as a clean bill of health.**

- **Whether an upstream distributed limiter is actually deployed** — not knowable from the repository.
- **MFA on this surface** — not looked for either way. **Whether it should carry a second factor is a
  product decision, not a measurement.**
- **The hasher figures were read from the shared framework on one machine.** A deployment at a different
  patch level could load a different `Identity.Core`; **the floor validator holds regardless, but the
  default it sits above is version-bound.**

---

# Provenance

Discovered and verified 2026-08-30 by items 155 (built → documented mirror), 156 (authorization and plane
separation), 157 (the anonymous door), 158 (credential storage). **Measurements live in
`.claude/handoff/results/item-155-…`, `item-156-…`, `item-157-…`.** **81 tests over this surface, all
passing, run rather than assumed.**

**This document was written after the surface existed and after it was already pinned. It adds no
guarantee — it adds a reader.**
