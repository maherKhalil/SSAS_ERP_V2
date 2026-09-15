# item 214 — FP-004: 64 criteria, ZERO cited, and the package is heavily tested

**Report only.** ⚠ **The starkest citation result so far, and it is a finding about the CONVENTION, not
about the package.**

## The denominator — clean, for once

| | |
|---|---|
| `AC-` identifiers **mentioned** in the file | **64** |
| **defined** here, each with its own heading | **64** |
| ⚠ cross-references from other packages | **ZERO** |

**Mentioned equals defined, and every one is `AC-LOC`.** ⚠ **The first package in this sweep where the two
figures agree with nothing to subtract** — FP-002's agreed on layout but FP-003's did not, so this is worth
recording as an established fact rather than assumed.

## ⚠⚠ THE SPLIT: ZERO PINNED

| bucket | count |
|---|---|
| **pinned — a `[Trait]` claims the criterion** | ⚠ **0** |
| **mentioned in prose only — claims nothing** | **1** (`AC-LOC-0042`) |
| **not mentioned in any test** | **63** |
| **control: cited-but-undefined** | **ZERO** |

**Not one of FP-004's 64 criteria is claimed by any test, under any of the four Trait keys.**

## ⚠⚠ AND THE PACKAGE IS COMPREHENSIVELY TESTED — WHICH IS THE WHOLE POINT

**Four suites work on localization**, and the coverage is deep. From `LocalizationResolverTests` and its
neighbours:

- `Post_commit_domain_event_evicts_the_tenant_generation`
- `Cache_population_is_single_flight_and_locks_are_reusable`
- `Version_revalidation_expiry_failure_grace_and_recovery_use_fake_time`
- `Cached_override_is_bypassed_immediately_when_tenant_is_suspended`
- `Cache_keys_are_tenant_complete_and_incompatible_overrides_fall_back`

⚠ **`Post_commit_domain_event_evicts_the_tenant_generation` is exactly the end-to-end flow the ruling
predicted would be uniquely testable here** — FP-004 owns
`LocalizationCacheDomainEventConsumer`, the only registered `IDomainEventConsumer` in the product, so this
one test exercises the post-commit dispatch that items 166–174 established **and** the cache invalidation
it drives.

⚠⚠ **CORRECTED BY ITEM 215 — THE CLAIM BELOW WAS A HEADING MATCH AND IS WRONG.** I wrote that this test's
criterion is `AC-LOC-0019 — Cache coherence`. **Its BODY reads *"version revalidation/eviction observes
15s/30s/5m/60s bounds and never crosses Tenant/culture"* — timing bounds and isolation, NOT post-commit
eviction.** The heading said one thing and the criterion said another, and I matched the heading.
**`AC-LOC-0019` was therefore NOT cited**, and which test satisfies it is unmeasured.

## ⚠ What this measures, precisely

**Zero pinned does NOT mean zero covered.** It means the mapping between 64 criteria and a well-tested
package **exists nowhere in the repository** — not in a Trait, not in a comment, not in a result file.

**Contrast the three packages measured so far:**

| package | defined | Trait-claimed | prose-only | unmentioned |
|---|---|---|---|---|
| FP-002 | 51 | 19 | — | 32 |
| FP-003 | 93 | 12 | 1 | 80 |
| **FP-004** | **64** | ⚠ **0** | 1 | 63 |

⚠ **The convention is not merely inconsistent in its KEY — it is absent from entire packages.** Principle
29's value is that a mapping paid for once stays greppable; **here it was never written down at all, and
the tests that would satisfy these criteria were written by people who evidently knew which criteria they
were satisfying.**

## What would settle the 63

**Reading them against four suites** — smaller than FP-003's 80 against twelve suites, and unusually
tractable because the suites are cohesive and named for their subject.

⚠ **But the cheaper and more durable move is forward, not backward**: the next test written against an
`AC-LOC` criterion should cite it. **Backfilling 63 by hand reproduces the measurement this record already
shows is expensive — three packages, 208 criteria, and 31 citations between them.**

## Scope
- **Trait-claimed and prose-mention were separated**, per item 212's stronger reading.
- **A `[Trait]` asserts a CLAIM, never correctness.** Nothing here reads a body except the five test names
  quoted, which were read as names.
- The test-name evidence is **illustrative of coverage, not a measurement of it** — no criterion was
  matched to a test and verified.
