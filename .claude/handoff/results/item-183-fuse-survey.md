# item 183 — surveying the fuse before fixing the one that went off

**Measurement only. Nothing fixed.**

## ⚠ The mechanism set, stated before searching

A fixture timestamp can only fail a test by reaching something that judges it against **wall time**. The
complete set of ways that can happen in this product:

| | mechanism |
|---|---|
| **M1** | a **time-limited data protector** — `Protect(payload, expiresUtc)` / `Unprotect`, which refuses a lapsed payload against the real clock |
| **M2** | **JWT `exp` / `nbf`** validated by the bearer handler |
| **M3** | **cookie expiry** written from a seeded instant |
| **M4** | **cache absolute expiry** set to an absolute instant |
| **M5** | production code reading **`DateTimeOffset.UtcNow` / `DateTime.UtcNow` directly** instead of `IDateTimeProvider`, in a path a test seeds around |
| **M6** | **SQL server-side time** — `SYSUTCDATETIME()`, `GETUTCDATE()`, `GETDATE()` |
| **M7** | **`TimeProvider.System`** |

**Bounded by how time enters a decision, not by what the codebase happens to contain.**

## ⚠ Freezing a clock is NOT the hazard — reaching a validator is

Four test fixtures freeze an instant, and **three of them are not members of this class**:

| fixture | frozen at | reaches a validator? |
|---|---|---|
| `PlatformAuthenticationPersistenceTests.cs:1102` | 2026-07-31 12:00 | ⚠ **yes — M1** |
| `PlatformIdentityAccessPersistenceTests.cs:28` | **2026-07-31 12:00 — the same instant** | **no** — 0 references to CSRF, token issuance or HTTP |
| `PlatformIdentityAccessSqlServerBehaviorTests.cs:293` | 2026-07-31 12:00 | **no** — 0 |
| `UserEmployeeLinkSqlServerTests.cs:205` | 2026-07-31 12:00 | **no** — 0 |
| `PersistenceFoundationTests.cs:183` | 2026-07-30 12:00 | **no** — persistence only |

The three identical instants are pure persistence tests: the frozen clock feeds **domain data**, which
nothing re-judges against wall time. **A survey keyed on "who freezes a clock" would have reported five
suspects; keyed on the mechanism it reports one.**

## The fuse table

| mechanism | sites in `src/` | fused test sites | fuse date |
|---|---|---|---|
| **M1** time-limited protector | **1** — `AuthenticationCsrfService` | **1 of 3** | ⚠ **2026-08-30 12:00 UTC — ALREADY PAST** |
| **M2** JWT lifetime | `ValidateLifetime = true`, `ClockSkew` 30s | **0 of 9** | — |
| **M3** cookie expiry | written from the same CSRF value | — | same fuse as M1, not a separate one |
| **M4** cache expiry | `LocalizationMemoryCache` ×2 | **0** | uses a **duration from now**, never an absolute instant |
| **M5** direct clock read | 20 files | **0 active** | see the finding below |
| **M6** SQL server time | 5 files | **0 active** | see the finding below |
| **M7** `TimeProvider.System` | **0** | — | not used anywhere |

**The only fused site is the one that already fired.** M1's other two call sites —
`AuthenticationCsrfTests.cs:32` and `:55–57` — pass `DateTimeOffset.UtcNow.AddMinutes(…)`, real time, no
fuse. All nine M2 issue-sites pass `DateTimeOffset.UtcNow`.

## ⚠ Two latent findings of a different shape — not fuses, but the same root

**1. One decision, two clocks.** `TenantModuleEntitlement` evaluates expiry through an injected
`IDateTimeProvider` (`snapshot.IsModuleEnabledAt(moduleKey, clock.UtcNow)`), while
`TenantEntitlementReader:37` selects the in-force subscription records with a bare
`var now = DateTimeOffset.UtcNow;`. **Half the entitlement decision is controllable by a test and half is
not.** It cannot fire today because nothing seeds future-dated records, but a test that did would find its
injected clock ignored — and that is exactly how item 182's defect was written.

**2. The trial seed cannot be time-controlled at all.** `TrialSubscriptionSeed:73` anchors the term to
SQL server time — `DECLARE @now datetimeoffset(7) = TODATETIMEOFFSET(SYSUTCDATETIME(), 0)`. **This is the
inverse hazard:** it can never expire prematurely, because it is always "now", but a test asserting a term
boundary against a fixture clock would be asserting against real wall time without saying so.

## What this means for 184

**The class has exactly one active member, and it is the known one.** A fix that removes the wall-clock
dependency for M1 closes the survey — there is no second site to sweep up.

⚠ **But M5's split clock is the same root cause in a place a fix for M1 will not touch**, and it is the
one that would produce the next item 182. It is a design inconsistency rather than a red test, so it is
reported rather than dispatched.

## Scope

- **Membership was judged by mechanism reference, not by execution.** A fixture reaching a validator
  through several hops — seeded value stored, re-read, then validated — would show as 0 references in the
  crude count I used for the three persistence fixtures. I checked their references, not their call graphs.
- **The 20 M5 files were classified by role**, not individually traced: the provider itself, design-time
  factories, hosted services, signing-key rotation, and the entitlement reader. Only the last is in a
  request path a test seeds around.
- `tools/` was not surveyed.
