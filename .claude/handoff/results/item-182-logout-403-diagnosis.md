# item 182 — why the concurrent refresh/logout test returns 403

**Diagnosis only. Nothing fixed, nothing changed** — a behaviour change on an authentication route does
not belong inside a diagnosis. All instrumentation was reverted; the working tree is clean of it.

## ⚠ The defect is the EXPECTATION. The product is correct.

`PlatformAuthenticationPersistenceTests.Concurrent_http_refresh_and_logout_use_validated_transport_and_sql_serialization`
**expired.** It was correct when written and became wrong by the passage of time, with no code change.

**It began failing at 2026-08-30 12:00:00 UTC — about fifteen hours before the run that found it.**

## The response, verbatim, before any reasoning

```
logout   = 403  {"title":"authentication.request_rejected","status":403,
                 "code":"authentication.request_rejected","correlationId":""}
refresh  = 403  {"title":"authentication.request_rejected","status":403,
                 "code":"authentication.request_rejected","correlationId":""}
```

**Both requests are refused identically**, so it is not a race between them — the test simply asserts on
logout first. There is no `detail`, which is correct: `ShowsDetail` fails closed for 401 and 403 (item 123).

## What was eliminated, by instrumentation rather than argument

`IsAuthenticationRequestSecurity.IsAccepted` and the CSRF check both return the same code, so the code
alone does not distinguish them. An echo endpoint mapped under the same path prefix, using the same request
helper, reported what the server actually received:

```
IsHttps=True | origin=https://app.integration.test
cookies=__Secure-ssas-refresh;__Secure-ssas-xsrf | csrfHeader=<present>
configuredOrigins=[https://app.integration.test]
```

**Transport is accepted**: HTTPS true, origin present exactly once and matching the allowlist exactly, both
cookies delivered, CSRF header delivered. That leaves one branch:

```csharp
if (!context.Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) ||
  !csrf.TryValidate(context, refreshToken, out var csrfPayload))
  return Problem(context, 403, "authentication.request_rejected");
```

The cookie is present, so **`TryValidate` is what fails.**

## ⚠ The cause: a time-limited token dated from a frozen clock

`AuthenticationCsrfService` protects with a **time-limited** data protector and validates against the
**real** clock:

```csharp
return protector.Protect(JsonSerializer.Serialize(payload), expiresUtc);   // Create
…
var json = protector.Unprotect(cookie, out var expiresUtc);                 // TryValidate
if (… || expiresUtc <= DateTimeOffset.UtcNow || …) return false;
```

The test builds its CSRF value from the seeded session's refresh-token expiry, and that expiry comes from a
**fixture clock frozen at 2026-07-31 12:00:00 UTC** (`SqlTestDatabase.Clock`, a `MutableClock`) plus
`AuthenticationPolicy.DefaultSessionIdleLifetime` of **30 days**:

**2026-07-31 12:00 + 30 days = 2026-08-30 12:00 UTC.**

Measured in the failing run:

```
csrfExpiry = 2026-08-30T12:00:00.0000000+00:00
realNow    = 2026-08-31T03:31:09.2888311+00:00
expired    = True
```

**So the CSRF token is genuinely expired, and refusing it is exactly what the product should do.**

## Which side is wrong, established from the product's own rules

**The behaviour is right.** `DEC-AUTH-0049`'s neighbours and the CSRF design require a bounded lifetime;
item 157 recorded that CSRF is *"bound to cookie, header, selector and session, and rotates"*, and item 173
established the same principle for expiry generally — a refusal that is correct must not be softened
because a test finds it inconvenient.

**The expectation is wrong**, and specifically: **the test mixes two clocks.** Everything it seeds uses the
frozen fixture clock; the protector it hands the result to uses the wall clock. That is stable only while
`fixtureClock + 30 days` is still in the future, which stopped being true on 2026-08-30.

⚠ **This is a time bomb, not a regression.** Bisection confirmed it: the same failure at `68d08cb`
(pre-175), `ac41854` (pre-164) and `112cb31` — the commit this session started from. **Nothing in this
loop caused it, and nothing in this loop could have prevented it.**

## Why it went unseen for a day

`test-baseline.txt` says so in its own words: *"Integration, and every Release row — NOT YET WRITTEN. Both
are produced only by a green `GATE_SCOPE=PHASE` run, and none has completed since this file was introduced
on 2026-08-27."* The `TASK` gate excludes Integration by design (item 176), so **every merge since
2026-08-30 12:00 has gone green over a red suite.**

## What a fix would have to decide — not taken here

The test needs the CSRF expiry and the validating clock to agree. There are at least three ways, and they
are not equivalent:

1. **Move the fixture clock forward relative to real time** — restores the bomb with a new fuse date.
2. **Seed the expiry from `DateTimeOffset.UtcNow`** rather than the fixture clock, for the CSRF value only
   — makes this one value real-time while the rest of the seed stays frozen.
3. **Let the test drive the protector's clock**, if the data-protection time provider can be substituted —
   the only option that removes the dependency on wall time rather than deferring it.

**Whoever fixes it should say which, and why.** Option 1 is what a hurried fix looks like and is the one
that recurs.

## Scope

- **One test diagnosed.** The Debug leg reported 1 failure of 846; whether other Integration tests carry
  the same frozen-clock-versus-real-clock pattern was not surveyed, and the pattern is generic enough that
  it is worth asking.
- The Release leg reported the same single failure, so this is not configuration-dependent.
- Instrumentation was temporary and is fully reverted; nothing from this diagnosis remains in the tree.
