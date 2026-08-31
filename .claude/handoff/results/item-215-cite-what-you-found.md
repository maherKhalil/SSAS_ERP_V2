# item 215 — two cited, one refused, and the refusal is the finding

**Gated work.** Two citations added, both confirmed against a test **body**. ⚠ **The third was WRONG and
item 214 is corrected in place.**

## ⚠⚠ `AC-LOC-0019` WAS A HEADING MATCH, AND I PUBLISHED IT

Item 214 stated that `Post_commit_domain_event_evicts_the_tenant_generation`'s criterion is
**`AC-LOC-0019 — Cache coherence`**. **The heading says "Cache coherence". The BODY says:**

> *"Version revalidation/eviction observes **15s/30s/5m/60s bounds** and never crosses Tenant/culture."*

**Timing bounds and tenant/culture isolation — NOT post-commit eviction.** ⚠ **The heading and the criterion
say different things, and I matched the heading**, in the item whose own conclusion was that layout counts
mislead.

**So it is not cited, and which test satisfies `AC-LOC-0019` is unmeasured.** ⚠ **Had the ruling not
insisted that a plausible match is the confidently-wrong grep, this would have shipped as a citation** —
and a wrong citation is worse than none, because the grep then answers.

**Item 214 is corrected in place**, marked as a heading match rather than silently amended.

## The two that hold, each read as a body

| criterion | test | what the body actually does |
|---|---|---|
| **`AC-TEN-0076`** — *disabling a `PlatformSupportPrincipal` revokes that principal's active sessions* | `Disable_revokes_all_active_platform_sessions_of_the_principal_only` | seeds **two** principals, three sessions on one and one on the other, disables one — the *only* in the name is the second principal surviving |
| **`AC-AUTH-0002`** — *one active membership is selected automatically* | `Begin_tenant_access_automatically_selects_one_revalidated_membership` | adds **one** eligible membership, asserts `TenantSelectedAutomatically` and that the tenant matches |

**Both were found by spot-check while measuring something else — items 212 and 208 — which is exactly the
moment the rule names.**

## ⚠ Keys matched, not chosen

`PlatformAuthenticationSessionFlowSqlServerTests` already uses **`AcceptanceCriteria`**;
`AuthenticationSessionApplicationTests` uses **`Acceptance`**. **Each citation matched its own file rather
than imposing one spelling** — ⚠ **introducing a fifth key to tidy four would have made the grep worse
while looking like cleanup.**

## What was NOT done

**FP-004's other neighbours** — single-flight cache population, version revalidation under fake time,
suspended-tenant bypass, tenant-complete cache keys — **are not cited.** ⚠ **I listed them in item 214 as
evidence of coverage, not as mappings**, and citing them now would mean reading four criteria and four
bodies. **That is re-measuring, which the rule excludes.**

**No search was run to find more.**

## Scope
- **A citation asserts a CLAIM, never correctness.** Both bodies were read; neither was executed against
  its criterion.
- ⚠ **`AC-TEN-0076`'s closing assertions were read rather than inferred**, after I caught myself
  caveating something I could simply check: **3 sessions seeded on the disabled principal, `COUNT(*) = 0`
  active after the disable, and `COUNT(*) = 1` still standing on the other principal.** That is the *only*
  in the test's name, asserted in SQL. **A caveat that a single command can remove is not a scope note.**
