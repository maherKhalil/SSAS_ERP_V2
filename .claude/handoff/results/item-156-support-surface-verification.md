# item 156 — what protects the twelve platform-support routes

**Verification only. Nothing built, nothing edited.**

item 155 found a complete privileged cross-tenant administration surface with no feature contract.
**"No contract" and "no guard" are different things.** This establishes which it is.

## The answer: DOCUMENTATION GAP, NOT A SECURITY GAP

**The surface is guarded better than most of the documented surface**, and its guards are asserted in the
negative direction, universally quantified over the route set.

## Permission, per route

All **nine** authority routes carry `.RequirePlatformPermission(PlatformPermissionNames.AdministerPlatformSupport)`
— reads and mutations alike, uniformly. Authentication: `/login` and `/refresh` are `AllowAnonymous()`
(necessarily — they ISSUE the session); `/logout` is `RequireAuthorization()`.

**Plane separation is structural.** `RequirePlatformPermission` resolves to
`PermissionPolicyNames.PlatformPrefix + name` and is a deliberately separate helper from the tenant-plane
`RequirePermission` (`TenantPrefix`), commented *"Deliberately separate from the tenant helper — the two
must not mix"* (`ADR-015` §8).

## Refusal is tested, and every assertion is universally quantified

`PlatformSupportAuthorityAuthorizationTests`:

- `Every_authority_route_rejects_an_anonymous_request`
- ⚠ `Every_authority_route_rejects_a_tenant_plane_token_carrying_the_administer_name` — **the right
  permission NAME on the wrong PLANE is refused**
- `Every_authority_route_rejects_a_platform_token_without_administer`
- `Every_authority_route_rejects_a_mixed_plane_token`

`Every_authority_route_…`, not a sample — **a route added tomorrow inherits all four.**

`PlatformSupportAuthorityRouteInventoryTests` pins the surface: `The_support_authority_route_surface_is_exactly_the_documented_inventory`
(all nine exactly — a new route fails), `Every_route_requires_the_platform_plane_support_permission`,
`No_support_route_is_gated_on_a_tenant_plane_policy`.

Cross-boundary: `PlatformSupportAuthenticationEndToEndTests` asserts a foreign, non-platform-store refresh
token is `403` with a stated problem code. Architecture:
`Platform_support_auth_surface_is_structurally_separate_from_the_tenant_surface`,
`Platform_logout_command_binds_only_trusted_token_claims`. Lockout recovery is designed, not accidental:
`Revoking_the_last_administer_succeeds_and_makes_administrative_recovery_eligible`.

## ⚠ The inventory test declares its own near-vacuity, unprompted

> *"A mis-gated route is not expressible here… This group has ONE permission. There is no wrong one to
> pick, so that failure mode does not exist and the property is green by construction."*

It then states precisely what it **can** still catch — a gate REMOVED, and a gate replaced by a FOREIGN
policy — and records its plant: *"revoke swapped to the tenant-plane prefix, which failed."* It even names
the shape: *"a vacuous property that does not say so is a floor stated too narrowly."*

**The artefact had assessed itself more precisely than any sweep would have.**

## ⚠ One property the owner should be told — deliberate, not a defect

- `Self_revoke_of_administer_succeeds_and_the_issued_token_KEEPS_ITS_CLAIM_UNTIL_EXPIRY`
- `Self_disable_succeeds_REVOKES_PLATFORM_SESSIONS_and_leaves_security_version_untouched`

**Revoke is not immediate lockout; disable is.** Both are tested and intended. The distinction matters
operationally, because "revoke" reads as immediate to most people: **to cut off a support principal now,
disable.**

## Verified, not assumed

`dotnet test tests/API.Tests/SSAS.API.Tests.csproj --filter "…PlatformSupportAuthority|…PlatformSupportAuthentication"`
→ **61 passed, 0 failed, 0 skipped.**

⚠ **The first run reported EXIT CODE 0 having run nothing** — the project path was guessed as
`API.Tests.csproj`; it is `SSAS.API.Tests.csproj`, and `dotnet` printed `MSBUILD : error MSB1009: Project
file does not exist` **and exited 0**. **The passing COUNT is the evidence, never the exit code.**

## ⚠ NOT examined — do not read this file as covering it

**Rate limiting or brute-force protection on `/support/auth/login`, credential storage, and token lifetime
values.** This covers authorization and plane separation only. Those are item 157.
