# item 163 — the tenant access token's claim set

**Gated work.** `tests/API.Tests/Infrastructure/TenantAccessTokenClaimSetTests.cs`, **15 tests**,
`GATE_SCOPE=TASK` **green**. No `src/` change.

## ⚠ There is no divergence. The code complies with `DEC-AUTH-0049` exactly.

`DEC-AUTH-0049` specifies exactly one occurrence of `iss`, `aud`, `sub`, `jti`, `iat`, `nbf`, `exp`,
`identity_id`, `tenant_id`, `tenant_user_id`, `session_id`, `client_id`, `security_version`, plus zero or
more `role` and `permission`, and excludes email, display name, `TenantName`, `TenantStatus`, `CompanyId`,
subscription or billing information.

**The issuer emits precisely that set.** The set-equality assertion passed on its first run, and the
plants below prove it was capable of failing. So neither side is wrong, and there was nothing to rule on.

## ⚠ Correction: the tenant token was PARTLY pinned, not unpinned

The item was dispatched on the basis that the tenant claim set is *"pinned by no test at all"*. That
overstates the gap. `JwtInfrastructureTests.Access_token_issuer_emits_rs256_known_kid_and_exact_trusted_bindings`
already pins the subject, `tenant_id`, deduplicated and ordinally sorted roles and permissions, and the
absence of `email`, `name` and `security_plane`.

**What no test asserted is the SET.** A denylist refuses only the exclusions someone thought of, and the
existing one names two. The new test asserts set equality, which subsumes every denylist and — the part
that matters — **reddens when a claim is ADDED**, which is the direction the risk actually runs.

## ⚠ The finding: a binding prohibition is unasserted, and the two planes are asymmetric

`ICompanySelection` carries this, in the code:

> **WHAT MUST NEVER IMPLEMENT THIS: a JWT `company_id` claim or `ICurrentUser.CompanyId`.** A claim would
> be a client-presentable assertion of scope that survives revocation until the token expired; `ADR-025`
> decision 4 makes that prohibition **binding rather than advisory**.

Three facts sit behind that sentence, and none of them was asserted anywhere:

1. **`StrictAccessTokenValidator` guards the platform plane against `company_id` and not the tenant plane.**
   `PlatformForbiddenClaims` is `tenant_id`, `tenant_user_id`, `role`, **`company_id`**, and the platform
   profile rejects any of them. **The tenant profile has no forbidden-claim list at all** — it checks that
   its critical claims appear exactly once and validates their formats, and is silent about extras. **A
   tenant token carrying `company_id` would pass strict validation today.**
2. **`ICurrentUser.CompanyId` still exists and reads that claim** — the very member the prohibition names.
3. **Nothing consumes it.** The only references are its own declaration, its implementation, and the
   prohibition itself.

**This is not a vulnerability.** The issuer never emits the claim, and the token is RS256-signed, so no
caller can introduce one without the signing key. **It is an unguarded prohibition**: the thing `ADR-025`
decision 4 forbids is prevented today by nobody having written it, and the new set-equality test now
closes the ISSUER half of that. The validator half and the vestigial property remain open.

## What was NOT asserted, deliberately

**No test here says the tenant profile accepts an extra claim.** That is today's behaviour, and asserting
it would pin the weaker side as correct. Whether the tenant profile should gain a forbidden-claim list —
and whether `ICurrentUser.CompanyId` should be removed — is a design decision, not a measurement, and is
reported rather than taken.

## The plants — three, each reddening only its own assertions

| plant | result |
|---|---|
| added `company_id` to the tenant claim list | `The_tenant_token_carries_exactly_the_specified_claim_types_and_no_others` **and** `The_tenant_token_carries_no_excluded_claim(company_id)` **FAILED**, 13 passed |
| removed `.Distinct().OrderBy()` from roles | `Role_and_permission_values_are_deduplicated_and_ordinally_sorted` **FAILED**, 14 passed |

⚠ **The first plant is the one that matters: the pre-existing denylist would NOT have caught it**, because
it names only `email` and `name`. The set equality did, and so did the `company_id` case that exists
precisely because the prohibition is by name.

Both reverted, 15 green. The test file was staged before planting so the revert restored it from the
index.

## Scope

- This pins what the **issuer** emits, not what the **validator** accepts.
- `iss`, `aud`, `nbf` and `exp` are written by the token descriptor rather than the issuer's claim list;
  `DEC-AUTH-0049` counts them, so they are in the specified set here.
- The platform token's claim set is pinned separately by `JwtInfrastructureTests`; this file does not
  touch it.
