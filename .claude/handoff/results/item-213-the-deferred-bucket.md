# item 213 — the deferred bucket is ONE criterion, and it is SATISFIED

**Read, not matched.** ⚠ **The expectation was that the transport deferral would account for a visible
block of FP-003's criteria. IT DOES NOT — and the reason is the interesting part.**

## What `AC-TEN-0020` actually defers today

**The criterion carries its own supersession table**, so the deferral is not a static list:

| concern | status |
|---|---|
| company | **superseded** — FP-005 |
| authentication-session, refresh-token, JWT-issuance | **superseded** — FP-002 |
| subscription | **superseded** — `DEC-L-004`, `DEC-L-006`, ratified FP-014 |
| branding, configuration, notification, **tenant endpoint**, Angular, immutable-audit-store | ⚠ **still deferred** |

## ⚠⚠ ALL 93 HEADINGS READ. NO CRITERION HAS A DEFERRED CONCERN AS ITS SUBJECT

**Not one of the 93 is about branding, configuration, notification, Angular, or an immutable audit
store.** The package's criteria are tenant lifecycle, platform-plane authorization, bootstrap, platform
sessions and tokens, and request-plane policy — **all of which shipped.**

**The deferred tenant endpoints are described in `api-contracts.md` and the route table, NOT in the
acceptance criteria** — which is exactly consistent with item 202, where the deferral was found recorded in
`item-152-route-table.md` and `BOARD.md`.

⚠ **So my refusal in item 212 to report a keyword count of 9 was right for a stronger reason than I gave:
the number is not merely imprecise, the bucket is very nearly EMPTY.**

## ⚠⚠ THE ONE MEMBER, AND IT IS SATISFIED RATHER THAN MISSING

**`AC-TEN-0092` — Phase-5 tenant-management boundary retained:**

> *"Phase 4 exposes platform-authority administration only; `Platform.Tenants.View`/`Manage`/`Lifecycle`
> HTTP endpoints remain Phase 5 and are not exposed merely because…"*

⚠ **This is a criterion ASSERTING THE DEFERRAL HOLDS. It is not a description of something unbuilt — it is
a claim that is TRUE TODAY, and would be FALSE if the work landed.**

**Lumping it with "not implemented" would have inverted its meaning entirely**, which is what the ruling
warned about — and the honest reading is stronger than the warning: **the package is not merely behaving as
written, it has a criterion whose satisfaction IS the deferral.**

## ⚠⚠ AND MY OWN ITEM-202 GUARD PINS IT — UNCITED UNTIL NOW

`TenantTransportDeferralTests.The_deferred_tenant_registry_transport_is_not_mapped` asserts precisely
`AC-TEN-0092`. **It cited only `AC-TEN-0020`, the broad milestone scope statement, and `0092` was cited
nowhere in the repository.**

**The guard and the criterion were written independently, three items apart, without either knowing about
the other.** ⚠ **Corrected: the guard now cites `AC-TEN-0092`** — and its Trait key moved from `Decision`
to `Acceptance`, FP-003's dominant convention. **This is Principle 29 finding a mapping that existed only
in two heads.**

## The three buckets, as ruled

| bucket | count | members |
|---|---|---|
| **DEFERRED under `AC-TEN-0020`** | **1** | `AC-TEN-0092` — ⚠ **and it is SATISFIED and now GUARDED** |
| **UNRESOLVED** | **79** | not mentioned in any test; item 212's spot-checks show the bucket is substantially covered |
| **NOT IMPLEMENTED** | **0 established** | ⚠ nothing was found to be missing, **by reading 93 headings and the deferral table** — which establishes an absence of *deferred subjects*, not an absence of defects |
| **AMBIGUOUS** | **1** | `AC-TEN-0083` — *"before platform-session creation is exposed over HTTP (4B), the create-vs-disable item is closed"*. **A precondition on a milestone that has since happened**; whether it now reads as satisfied or as spent is a judgement about a superseded gate, not a measurement. Not forced. |

**Pinned remains 12, unchanged — `0092` was already inside the unmentioned 80, which becomes 79 plus this
one.**

## Scope
- **The classification is by reading each heading, plus the bodies of the three candidates.** I did not
  read all 93 bodies; a criterion whose heading is neutral but whose body turns on a deferred concern
  would be missed.
- The supersession table is `AC-TEN-0020`'s own, dated by its amendments — **not a judgement of mine about
  what FP-005, FP-002 and FP-014 actually delivered.**
