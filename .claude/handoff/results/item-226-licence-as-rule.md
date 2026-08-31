# Item 226 — the licence now states the rule, and the measurement is recorded rather than erased

**Comment-only change to `ApiProblems.cs`. No behaviour change: `ShowsDetail` and `VisibleDetail` are
untouched, as ruled.**

## What the two blocks now say

**First block — the RULE.** Passing the message as `detail` is safe because **`ShowsDetail` is a positive
allowlist on 4xx minus 401/403**, and a 4xx message is addressed to the caller by construction. ⚠ **It
would be exactly as safe if every message in the product interpolated.** **Nothing depends on a census of
the messages that happen to exist.** A pointer to the note beside `ShowsDetail` records that an earlier
version licensed the change with a measurement instead.

**Second block — the MEASUREMENT, kept.** It was true when written, is false now, and never carried the
guarantee. ⚠ **Kept rather than deleted, because it is part of why the design is what it is** — and
because two of the interpolated values are now asserted end to end at the wire **precisely because they
travel**, which is the feature working.

## ⚠⚠ AND THE RULING'S OWN SPLIT WAS BACKWARDS — 3 AND 4, NOT 4 AND 3

**The ruling said *"FOUR interpolate an internal status enum (`PayrollErrors.cs:85`, `:89`, `:93`)"*.**
⚠ **It names three line numbers while saying four**, and the seventh — `PayrollErrors.cs:102`,
`PeriodClosedForPosting` — is **a caller-supplied identifier, not a status.**

| classification | count | sites |
|---|---|---|
| internal status enum | **3** | `PayrollErrors` recalculate / approve / post |
| ⚠ **caller-supplied identifier** | **4** | `AccountErrors.Inactive`, `PayElementErrors.Unmapped`, `PayElementErrors.Inactive`, `PayrollErrors.PeriodClosedForPosting` |

**The comment says 3 and 4.** ⚠ **The distinction was the ruling's own point — *a future author needs
that distinction more than the count* — and it is the identifier class that is the larger one.**

## ⚠ AND THE POPULATION IS NOW BOUNDED, BECAUSE THE FIRST SEARCH WAS NOT

**My original seven came from `--include=*Errors.cs`.** ⚠ **A wider search — any interpolated string
within an error or failure construction anywhere under `src/` — returns TWO MORE**, both in
`TenantStorageBootstrapService`.

**They are outside the population, and the comment says why rather than omitting them:** they are
`InvalidOperationException` texts thrown during startup, and they reach a caller only as a 500 — **which
shows no detail.** ⚠ **The population `ShowsDetail` governs is domain `Error` messages, which is what
`Explaining` carries.** **An unbounded "zero interpolations across `src/`" is exactly the shape of claim
that produced this item; the replacement names its own scope.**

## Not done, deliberately

- **No behaviour change.** The ruling was explicit and I agree with it: the allowlist was never
  load-bearing on the measurement.
- **No edit to the seven messages.** Every interpolated value is the caller's own tenant data behind
  authorization, and 401/403 still fails closed.
