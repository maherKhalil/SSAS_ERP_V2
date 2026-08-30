# item 157 — the anonymous door: rate limiting, lockout, token lifetimes

**Verification only. Nothing built, nothing edited.** Completes `item-156-support-surface-verification.md`,
which covered authorization and plane separation and explicitly excluded these.

Two routes on the support plane need no credential to reach: `POST /api/platform/support/auth/login` and
`/refresh`. **Both are defended, and the defences are tested at their exact boundaries.**

## Rate limiting — `AuthenticationEndpointRateLimiter`

| endpoint | limit |
|---|---|
| **login, per IP** | **30 / minute** |
| **login, per identity + IP** | **5 / 15 minutes** |
| refresh | 10 / minute |
| logout | 5 / minute |
| tenant selection | 10 / 5 minutes |
| anything else | 1 / minute (deny-by-default) |

**Both login limits are enforced together** — `Login_enforces_both_identity_and_trusted_ip_limits`. Keys
are **HMAC'd**, not raw IPs or usernames. The default arm is 1/minute, so an endpoint added without a case
is throttled rather than unlimited.

Tested: `Login_and_logout_limits_reject_without_queueing_after_the_approved_counts`, and
`Partitioned_endpoint_limits_reject_the_first_request_above_the_exact_limit` — **the first request above
the limit, not eventually.**

### ⚠ The limiter is per-instance, and production start-up demands a declaration it cannot verify

The window store is a `ConcurrentDictionary` — **in-process, so it does not span replicas.** The design
knows this: outside Development, start-up **throws** unless `RateLimitHmacSecret.Length >= 32` **and**
`UpstreamDistributedRateLimitingEnforced` is set (and unless the Data Protection key-ring and certificate
exist).

**But that flag is a DECLARATION, not a verification.** The application cannot check that an upstream
distributed limiter actually exists. ⚠ **This is a deployment obligation, and it is the one thing here an
owner or operator must satisfy outside the code.**

## Lockout — `AuthenticationPolicy`

**5 failed attempts → 15-minute lockout**, held on the account (`AuthenticationAccount`, raising
`AuthenticationAccountLocked`). Password length 12–128.

⚠ **This is the backstop the rate limiter cannot be**: the per-identity rate-limit key includes the IP, so
an attacker rotating IPs gets 5 attempts per IP — but **account lockout is not IP-scoped**, so rotation
does not buy more attempts against one account.

## Token lifetimes

| | |
|---|---|
| **access token** | **15 minutes — and `JwtOptionsValidator` REFUSES any value above 15 minutes** |
| session idle / absolute | 30 days / 90 days, max 10 active sessions |
| tenant selection | 5 minutes |
| invitation / password reset | 24 hours / 30 minutes |

⚠ **This bounds item 156's finding.** *"Self-revoke keeps its claim until expiry"* means **at most 15
minutes**, capped by a validator that rejects a longer configuration — not an open-ended window. **Disable
is still the immediate action**, but the exposure from revoke is bounded and cannot be misconfigured wider.

## Also asserted

- **Refresh rotates and denies the previous token** — `Platform_refresh_rotates_the_continuation_and_denies_the_previous_token`
- **Cross-plane cookie confusion refused** — `A_platform_refresh_cookie_presented_under_the_tenant_cookie_name_is_refused`
- **No user enumeration** — `Every_ordinary_authority_failure_returns_the_same_generic_401`
- **Transport** — HTTPS and an exact configured origin required; `Invalid_origin_configuration_fails_startup`
- **CSRF** — exact cookie, header, selector and session binding; rotates; rejects missing, malformed,
  expired, wrong-selector and wrong-client values
- **Signing failures are opaque outward and logged inward**, deliberately — `AccessTokenIssuer` documents
  why the broad catch stays: *"a missing key, an unusable algorithm and an oversized token are all things
  an attacker would like distinguished"*, and before T-241 the failure was silent to operators too.

## Verified, not assumed

`--filter "FullyQualifiedName~AuthenticationCsrf"` → **20 passed, 0 failed.** With item 156's 61, **81
tests over this surface, all passing.** *(The passing count is the evidence: a run against a mistyped
project path prints `MSB1009` and still exits 0.)*

## Verdict

**The anonymous door is well defended, and nothing here changes item 156's conclusion: a documentation
gap, not a security gap.** The single item needing a human is the deployment obligation above.

## ⚠ NOT examined

- **Whether an upstream distributed rate limiter is in fact deployed** — not knowable from this repository.
- **The hashing algorithm's parameters.** `AspNetPasswordHashingService` delegates to ASP.NET Core
  Identity's `IPasswordHasher<object>`; this is the framework default, not a custom scheme, and its
  iteration count and version were not read.
- **Multi-factor authentication** — not looked for either way.
