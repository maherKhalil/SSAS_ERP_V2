# item 212 — FP-003 measured: 93 criteria, 12 pinned, and a FOURTH Trait key

**Report only.**

## ⚠ THE DENOMINATOR IS 93, NOT 94 — and the difference is the fault you predicted

**94 `AC-` identifiers are MENTIONED in the file. 93 are DEFINED there** — each with its own heading. **The
94th is `AC-AUTH-0045`, a prose cross-reference to FP-002**, and it is the same mention-versus-definition
fault item 210 found from the other side.

⚠ **So a per-file identifier count over-reports any package that cites its neighbours.** The instrument is
still format-blind, which is what it was for — **but "distinct identifier in the file" and "criterion this
package defines" are different quantities, and only the second is a denominator.**

**FP-003 defines 93.**

## ⚠⚠ AND A **FOURTH** TRAIT KEY EXISTS

Item 210 found three. FP-003's citations use a fourth:

| key | uses carrying an `AC-TEN` id |
|---|---|
| `[Trait("Acceptance", …)]` | 15 |
| ⚠ **`[Trait("AcceptanceCriteria", …)]`** | **2** |
| `[Trait("Decision", …)]` | 2 |

**Four keys now, for one relationship.** ⚠ **This is the fifth occurrence today of the same family, and it
is the strongest argument yet that the grep must key on the ID TEXT** — which is how this measurement was
taken, so the numbers below do not inherit the fault.

## The split

| bucket | count | how established |
|---|---|---|
| **pinned — a `[Trait]` claims the criterion** | **12** | `0001, 0002, 0003, 0005, 0006, 0010, 0014, 0017, 0020, 0030, 0066, 0093` |
| ⚠ **mentioned in prose only — NOT a pin** | **1** | `AC-TEN-0068`, named in a comment, which claims nothing |
| **not mentioned in any test** | **80** | see below — **not a bucket of uncovered criteria** |
| not implemented | **0 established** | — |
| subject undefined | **0 established** | — |
| vacuously satisfied | **0 established** | — |

**Control: ZERO cited-but-undefined.** No test points at an `AC-TEN` the spec lacks.

⚠ **The prose-only case is worth separating: an ID-text search finds MENTIONS, and a mention in a comment
is not a claim that the test pins the criterion.** Distinguishing them cost one grep and moved one
criterion out of "pinned" — **the same distinction as `AC-AUTH-0045`, one level down.**

## ⚠⚠ THE 80 ARE UNRESOLVED, AND THE SPOT-CHECKS SAY THE BUCKET IS SUBSTANTIALLY COVERED

Sampled every 16th, spread across the range:

| criterion | a test matching its subject |
|---|---|
| `AC-TEN-0076` — *proactive revocation on principal Disable* | ⚠ **`Disable_revokes_all_active_platform_sessions_of_the_principal_only`** — and two more beside it |
| `AC-TEN-0059` — *platform token with `tenant_id` is rejected* | `A_tenant_scoped_permission_name_cannot_be_satisfied_by_a_platform_token`, `An_empty_tenant_id_fails_closed` |
| `AC-TEN-0043` — *administration permission un-self-grantable* | grant-refusal tests exist; **none unambiguously self-grant** |

**So the 80 are mixed, exactly as FP-002's 32 were** — and reporting them as uncovered would be an absence
claim on an instrument that cannot see absence.

## ⚠ The deferred transport is NOT in the "not implemented" bucket, and I did not put it there

Item 205 found FP-003's status claim **true and precise** — *"HTTP transport: Deferred"* — and item 202
built the guard that now fails if it ships. **Those criteria are DEFERRED, which is a decision, not an
omission.**

⚠ **I did NOT count them**: a keyword scan of headings for transport words returns 9, but it catches
`AC-TEN-0021` *"Tenant token rejected on platform routes"*, which is authentication and almost certainly
live. **A keyword count of a heading is a layout count wearing different clothes**, and I am not reporting
a number I would have to withdraw.

## What a full split costs

**Reading 80 criteria against the twelve suites that already cite FP-003.** That is a real measurement, and
it is the single largest one left: **FP-003 alone is 93 of the 606.**

⚠ **The cheaper permanent fix is Principle 29 applied forward** — the 12 pinned here were free to measure;
the 80 would cost days. **And a fifth Trait key would make even that unreliable, which is the argument for
naming ONE key rather than adding a fifth.**

## Scope
- **Citation split by whether the ID appears inside a `[Trait(...)]` attribute** — a stronger reading than
  item 208's, which counted any ID text and would have reported 13 rather than 12 here.
- **A `[Trait]` asserts that a test CLAIMS the criterion, never that it asserts it correctly.** No body was
  read for the 12.
- **The three spot-checks are illustrative, not a sample**, and were chosen to test the instrument's limit
  rather than to estimate a rate.
