# item 184 — the CSRF fuse is removed, not postponed

**Gated work.** `GATE_SCOPE=TASK` green. The 14 tests in `PlatformAuthenticationPersistenceTests` all
pass, including the one that had been failing since 2026-08-30 12:00 UTC.

## ⚠ Option (3) is infeasible — no public seam

The ruling's preferred fix was to substitute the data-protection time provider.
**`ITimeLimitedDataProtector` exposes only `Protect(plaintext, expiration)` and
`Unprotect(protectedData, out expiration)`.** The implementation is internal and reads
`DateTimeOffset.UtcNow`; there is no clock to inject.

Enforcing expiry in `AuthenticationCsrfService` instead — it already carries
`ExpiresUnixTimeSeconds` in its payload — would mean **weakening a security mechanism's own enforcement to
suit a test**, so it was not done.

## The fix taken, and why it is not option (1)

**The fixture clock is anchored to the run instead of to a date:**

```csharp
public MutableClock Clock { get; } = new(DateTimeOffset.UtcNow);   // was: new DateTimeOffset(2026, 7, 31, …)
```

| option | effect |
|---|---|
| (1) a later frozen instant | **resets the fuse** with a new date |
| (2) one real value in a frozen seed | a mixture **production never has** |
| (3) substitute the protector's clock | **no public seam** |
| **taken** | every seeded expiry is `run + 30 days` — **there is no date at which this breaks** |

**This is not a deferral.** The defect was two clocks, not a wrong date; one clock now feeds the whole
seed, which is what production does.

**It changes no assertion.** Nothing in the file asserts an absolute date, and this clock is never
advanced — 50 uses, all relative offsets from whatever instant it holds. Verified by running all 14 tests
in the file, not just the repaired one.

## The plants, both directions as ruled

| plant | result |
|---|---|
| wall clock already past 2026-08-30 12:00 (it is — today is 2026-08-31) | ⚠ **does not redden** — the fixed test passes |
| restore the frozen instant | **reddens**, `Expected: NoContent, Actual: Forbidden`, exactly as before |

**So the anchoring is what makes the difference**, and the fix is verified against the very date that broke
it rather than against a hypothetical future one.

## ⚠ A trap I hit while planting, recorded because it cost real work

Planting item 185 I reverted with `git checkout --` **without having staged the fix first**. The index held
the *pre-fix* file, so the restore silently reverted my own work rather than the plant — the fix was gone
and the file looked plausible. **This is the unstaged-plant failure this loop recorded weeks ago, hit
again by the window that recorded it.** Detected by checking the file after restoring rather than assuming;
re-applied and staged before continuing.

## Scope

- **One site**, which item 183 established is the entire active population of this class.
- The fixture clock is now non-deterministic across runs in the sense that its absolute value differs. That
  is safe **here** because nothing asserts an absolute date; it would not be safe in a file that did, and
  no other fixture was changed.
