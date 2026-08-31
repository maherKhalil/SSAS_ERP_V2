# item 208 — FP-002 measured, and the instrument's limit is the headline

**Report only.** ⚠ **51 criteria. 19 pinned by direct citation. THE OTHER 32 ARE UNRESOLVED BY THIS
INSTRUMENT — and I can prove at least one of them IS pinned, so "uncited" must not be read as "uncovered".**

## The denominator, stated before the split

**51 distinct `AC-AUTH-[0-9]+` identifiers** (88 mentions), confirmed **two ways**:

- format-blind identifier count: **51**
- bullet-form lines: **51**, and ⚠ **the set of identifiers not on a bullet line is EMPTY**

**So the layout count and the identifier count agree here** — which is the first package where they do, and
is why the diagnosis is still worth running: agreement is a fact, not an assumption. **My item-205 figure
of 51 is confirmed, not corrected.**

## The split

| bucket | count | how established |
|---|---|---|
| **pinned — cited by ID in a test** | **19** | `AC-AUTH-0001, 0009, 0013–0018, 0020, 0021, 0024, 0029, 0031, 0032, 0034, 0035, 0046, 0047, 0051` |
| ⚠ **unresolved by this instrument** | **32** | see below — **NOT a bucket of unpinned criteria** |
| not implemented | **0 established** | no criterion's subject is missing; see below |
| subject undefined | **0** | — |
| vacuously satisfied | **0 established** | — |

**Control:** ⚠ **no test cites an `AC-AUTH` id that the spec does not contain.** A citation pointing at a
criterion that does not exist would make the 19 worthless, and there are none.

## ⚠⚠ WHY THE 32 ARE NOT A FINDING, DEMONSTRATED RATHER THAN ARGUED

**ID citation is SUFFICIENT for "pinned" and not NECESSARY.** A test can pin a criterion perfectly and
never name it. **Spot-checking three of the 32 settles the direction:**

| criterion | a test matching its subject |
|---|---|
| `AC-AUTH-0002` — *one active membership is selected automatically* | ⚠ **`Begin_tenant_access_automatically_selects_one_revalidated_membership`** — an exact match, uncited |
| `AC-AUTH-0011` — *logout-all revokes every active session* | several logout tests exist; **none is unambiguously logout-ALL** — the closest asserts the opposite scope (*"revokes only the bound session"*) |
| `AC-AUTH-0040` — *strict JWT validation: RS256, known `kid`, exact issuer/audience* | **no name match**, which establishes nothing either way |

**So at least one of the 32 is pinned, and the bucket is genuinely mixed.** ⚠ **Reporting "32 uncovered"
would have been an absence claim on an instrument that cannot see absence** — the same error item 206
avoided and item 207 then had to correct anyway, because my four candidate files there included a homonym.

## No criterion is "not implemented", and the method is stated

**Every subject the criteria name exists in force:**

| subject | src files | test files |
|---|---|---|
| `AuthenticationSession` | **98** | **32** |
| `AuthenticationAccount` | 63 | 13 |
| `RefreshTokenRecord` | 41 | 9 |
| `AccountActionToken` | 44 | 7 |

**Established by type-name reference count, which is a weak instrument for behaviour and a strong one for
existence** — and existence is all this bucket claims.

## ⚠ The milestone boundaries are NOT status claims, and reading them as such would produce a false finding

FP-002's README says **"Milestone 2 does not implement `AuthenticationSession`, `RefreshTokenRecord`, JWT
issuance…"** — and **a Milestone 3 boundary follows immediately** that implements exactly those.

⚠ **Read out of context that sentence looks like FP-015's false claim. It is not: it is scoped to a
milestone, and the next heading supersedes it.** This is why item 205 recorded FP-002 as making *no
implementation-status claim* rather than a false one. **A boundary statement and a status claim are
different objects, and only the second decays.**

## What would settle the 32, and what it costs

**Read each of the 32 criteria against the bodies of the eight authentication suites.** The suites are
`AuthenticationApplicationTests`, `AuthenticationDomainTests`, `AuthenticationSecurityTests`,
`AuthenticationSessionApplicationTests`, `AuthenticationSessionDomainTests`,
`AuthenticationSessionArchitectureTests`, `PlatformAuthenticationPersistenceTests`, and the session
SQL Server suites. ⚠ **That is a real measurement, not a sweep** — and it is the only thing that converts
32 unresolved into a split.

**A cheaper permanent fix exists and is not mine to choose:** ⚠ **cite the criterion ID in the test**, as
19 already do. **The 19 were free to measure; the 32 cost a day.** The difference is a convention nobody
enforced.

## Scope
- **The 19 are established by citation, which proves a test CLAIMS the criterion — not that it asserts it
  correctly.** This loop has found named tests that do not prove what they name (`T-191`'s own subject).
  **A stronger form would read all 19 bodies; I read none of them.**
- The three spot-checks are **illustrative, not a sample** — chosen to test the instrument's limit, not to
  estimate a rate.
