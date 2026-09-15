# item 164 — closing the unguarded prohibition (`ADR-025` decision 4)

**Gated work.** `GATE_SCOPE=TASK` **green**; condition 4 satisfied — 2 of 7 suite totals moved against 14
non-comment `src/` lines changed. **103 files touched**, and that number is the first finding.

## (a) A tenant token carrying `company_id` is now refused

`StrictAccessTokenValidator` gains `TenantForbiddenClaims = [company_id]`, enforced in
`IsValidTenantProfile` alongside the existing cardinality checks.

**A denylist is the right mechanism here and the objection raised in item 163 does not apply.** That
objection was to a denylist as a *general* guard — it refuses only what someone thought of. **Here nobody
is guessing:** `ADR-025` decision 4 names this claim by hand and makes the prohibition binding. An entry
implementing a named binding rule is enforcement, not prediction. The comment in the file says so, so the
next reader does not "improve" it into a general mechanism without costing it.

## (b) `ICurrentUser.CompanyId` is removed — ⚠ and it had 100 implementers, not 3

The dispatch said *"Nothing consumes it — declaration, implementation and the prohibition are its only
references."* **That is true of consumers and wrong about implementers.**

| | |
|---|---|
| **consumers** (code reading the member) | **1**, a test assertion in `RequestContextTests` |
| **implementers** (stubs satisfying the interface) | **100** — 95 in `tests/`, 4 in `src/` and `tools/`, plus the real `CurrentUser` |

Removing an interface member does not break an implementer; it **orphans** it. The orphan then trips
`CA1822` (*"does not access instance data"*), and `DEC-L-008` condition 1 is zero warnings — **so the gate
turns red for a reason that has nothing to do with the change's correctness.**

**The compiler enumerated the consumer; only a clean build enumerated the implementers.** My first build
was incremental and reported zero warnings while four `src/` and `tools/` orphans stood, because those
projects were not recompiled. ⚠ **An incremental build hides warnings in every project it does not
rebuild** — the gate caught it, and a `--no-incremental` build reproduces it.

**The removal was scoped, not textual.** `ICurrentCompany.CompanyId` is a different member, is the
legitimate one, and stays — and both declare `public Guid? CompanyId => …`. Lines were removed only inside
classes whose declaration names `ICurrentUser`, two multi-line declarations were handled by hand after the
build named them, and each edit was a byte-level line removal rather than a split/rejoin. **93 test files
changed by exactly one line each**; the only files with a larger diff are the two carrying new tests.

## Guards, and both were planted

| guard | plant | result |
|---|---|---|
| `CompanyScopeClaimArchitectureTests.ICurrentUser_declares_no_company_scope_member` | re-added `CompanyId` to `ICurrentUser` as a **default interface member**, so the 100 stubs still compile and the plant is not void | **FAILED**, control passed |
| `JwtInfrastructureTests.Tenant_token_carrying_company_id_is_rejected` | removed the `TenantForbiddenClaims` line from `IsValidTenantProfile` | **FAILED**, control passed |

Both controls held throughout: `ICurrentCompany_still_declares_the_legitimate_company_member` (the same
reflection finding the legitimate member — without it, "absent" is indistinguishable from looking in the
wrong place) and `The_same_tenant_token_without_company_id_is_accepted` (the identical token minus the
claim, so the refusal is the claim's doing).

## ⚠ (c) Full out-of-set rejection — costed, NOT built

**What it would be:** `IsValidTenantProfile` rejects any claim type outside an allowed set, rather than a
named few.

**The hazard is narrower than feared, for a reason worth stating: `AccessTokenIssuer` and
`StrictAccessTokenValidator` are in the SAME assembly** — `SSAS.Host.API` — and therefore the same
deployment unit. They cannot drift apart between releases, which removes the ordering constraint in its
general form.

**What remains is a rolling-deploy window.** During a rolling update, new instances issue and old
instances validate. If a release adds a claim, a token minted by a new instance is rejected by an old one
for as long as both are serving.

- **Blast radius: total, not partial.** Every request carrying an affected token fails authentication.
- **Duration:** the length of the rollout, plus up to the 15-minute access-token lifetime afterwards for
  tokens already minted.
- **Symptom:** intermittent `401`s that resolve on retry once the caller lands on a matching instance —
  the hardest shape to diagnose, because it looks like flapping rather than a bad release.
- **Cost of getting it wrong:** an authentication outage, not data loss. Recovery is to complete or roll
  back the deploy; no state is corrupted.

**The mitigation is standard expand/contract in two releases:** widen the allowed set first (accept the
claim, do not emit it), emit it in the next. **The cost is therefore not the code — it is that every
future claim addition becomes a two-release change, permanently.**

**And the trade is a real one rather than obvious:** today an unexpected claim is accepted silently;
afterwards it fails loudly. Loud is better for a security property and worse for availability, and which
matters more here is a judgement about how often claims change, which I have no basis to make.

## Scope

- `email` and `name` are **not** in `TenantForbiddenClaims`. `ADR-025` decision 4 names `company_id`; the
  others are excluded by the issuer and pinned as a set by item 163's test. Adding them would be widening
  beyond the ruling.
- The four `src/` and `tools/` stubs are design-time and maintenance identities, not request-path code.
