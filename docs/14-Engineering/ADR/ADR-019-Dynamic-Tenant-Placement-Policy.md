---
id: ADR-019
title: Dynamic Tenant Placement Policy
category: Architecture Decision Record
version: 1.2
status: Proposed
date: 2026-08-13
owner: Solution Architecture Team
tags:
  - architecture
  - multi-tenancy
  - operations
  - policy
  - capacity
depends_on:
  - ADR-017
  - ADR-018
used_by:
  - Platform
---

# ADR-019: Dynamic Tenant Placement Policy

---

# Status

**Proposed**

Depends on the routing model of `ADR-017` and the schema-health model of `ADR-018`. It defines how the platform decides whether a tenant should remain on shared storage or be recommended for dedicated storage, and explicitly separates that **decision** from the **execution** defined in `ADR-020`.

---

# Context

`ADR-017` establishes shared-by-default placement with optional dedicated databases. It deliberately does not define when a tenant should be promoted.

Placement must respond to reality rather than to predictions made at onboarding. Sales-stage estimates of customer size are unreliable, and tenants change materially over time. At the same time, moving a live ERP database is a high-risk operation that must never be triggered by a transient spike or an unreviewed rule.

Capacity is two-sided: a tenant may be entirely ordinary while the shared database hosting it is at its limit. The correct remedy in that case is another shared database, not dedicated isolation.

`ADR-017` further separates **where** a tenant database is hosted (`HostingMode`) from **how shared** it is (`StorageMode`). Placement policy therefore operates over two dimensions, and only one of them is a technical question at all.

---

# Problem Statement

Without a defined placement policy:

- Promotion decisions are ad hoc, undocumented, and inconsistent between operators.
- Thresholds get hard-coded in application code and require a redeployment to tune.
- A single spike could trigger an expensive and risky migration.
- Contractual or regulatory isolation requirements could be silently overridden by a technical rule that says "shared is fine".
- Operators have no machine-readable reason for why a tenant was recommended for promotion.
- A technical rule could recommend relocating a customer-hosted tenant into platform-managed shared storage, breaching a data-residency or contractual commitment on the strength of a metric.

---

# Decision

## Configuration-driven

Placement thresholds are **configuration-driven** and stored as data. Changing a threshold **must not** require application redeployment. Thresholds **must not** be hard-coded in C#.

## Policy model

Platform database configuration, conceptually:

- `TenantStoragePolicy` — `Id`, `Name`, `Version`, `IsActive`, `EvaluationInterval`, `MinimumObservationPeriod`, `RequireManualApproval`, `CreatedUtc`, `UpdatedUtc`.
- `TenantStoragePolicyRule` — `Id`, `PolicyId`, `MetricType`, `Operator`, `ThresholdValue`, `EvaluationWindow`, `RequiredBreaches`, `Priority`, `IsEnabled`.
- `TenantStorageMetricSnapshot` — periodic observed metrics per tenant.
- `TenantStorageOverride` — `TenantId`, `ForcedHostingMode`, `ForcedStorageMode`, `Reason`, `EffectiveUtc`, `ExpiresUtc`, `ChangedByIdentityId`.
- `TenantPlacementRecommendation` — `TenantId`, `CurrentHostingMode`, `CurrentStorageMode`, `RecommendedStorageMode`, `Decision`, `Reasons`, `EvaluatedUtc`, `PolicyId`, `PolicyVersion`, `MetricSnapshotIds`, `RequiresApproval`.

Two deliberate omissions:

- **`RecommendedHostingMode` is absent.** The evaluator never recommends a hosting change, so a field for it would invite one.
- **`AutoMoveEnabled` is absent.** Automatic movement is not a V1 capability, and a persisted flag that no code honours is how such a capability eventually gets wired up without the ADR that was supposed to gate it. Automatic placement execution requires a **future ADR**, at which point adding the field is trivial. It is discussed only under Future Considerations.

*Implementation decision pending: final names and normalisation must follow repository entity conventions.*

## Policy administration is privileged

Policy configuration is itself a privileged, audited surface — not only overrides. Creating or changing a `TenantStoragePolicy` or `TenantStoragePolicyRule` **must** require platform-admin authorization and **must** be audited with actor, timestamp, and **before/after values**. A threshold is a control over the whole estate; protecting overrides while leaving thresholds freely editable would guard the exception and not the rule.

## Policy versioning

Every policy edit **creates or increments a `Version`**. Each recommendation records `PolicyId`, `PolicyVersion`, the **metric snapshot identifiers** evaluated, `EvaluatedUtc`, and its decision reasons.

This is binding because the question an audit or an operator actually asks is *why was this tenant recommended, at that time, under what rules?* — and that is unanswerable if the policy has since been edited in place. Recommendations must be reconstructable from stored data alone.

## Blast radius

Before a new policy or rule version takes effect on production recommendations, the system **must evaluate and report its blast radius**: how many tenants would newly change recommendation under it.

Where that count exceeds a configurable safety threshold, recommendations **must not** activate silently; the change surfaces for `ManualReview` / approval instead. A single threshold edit can otherwise reclassify thousands of tenants at once, and the resulting flood is indistinguishable from a genuine capacity event. The exact threshold is configuration; the requirement to compute and gate on it is not.

## Placement has two dimensions

Placement is `HostingMode` **and** `StorageMode`, giving three supported placements:

- `PlatformManaged` / `Shared`
- `PlatformManaged` / `Dedicated`
- `CustomerManaged` / `Dedicated`

The evaluator's technical rules decide physical placement **for `PlatformManaged` tenants only** — essentially, shared versus dedicated, and which shared database. `CustomerManaged` placement is governed by contract, manual override, or compliance, and is not a technical conclusion.

## Customer-managed placement is not a technical outcome

`CustomerManaged` is never the result of workload promotion. A tenant is customer-hosted because of a customer contract, a data-residency requirement, a regulation, or the customer's own infrastructure policy — none of which the evaluator can observe in a metric.

Consequently the evaluator **must not** recommend moving a `CustomerManaged` tenant into `PlatformManaged` storage, whatever its metrics say. A tenant whose data is required to remain on customer infrastructure cannot be "optimised" back onto a shared platform database; doing so would relocate regulated data as a side effect of a threshold. Where `HostingMode = CustomerManaged`, `RecommendOtherSharedDatabase` and any shared-storage recommendation are invalid outputs, not merely unlikely ones.

The evaluator may still produce **observational** output for customer-hosted tenants — recording metrics, surfacing capacity or performance concerns to operators — but its `RecommendedStorageMode` for such tenants is `NoChange` or `ManualReview`.

## Customer-managed rule precedence

An explicit `CustomerManaged` requirement sits with the override and compliance tiers of the precedence order below, **above** technical policy, shared-shard capacity, and every storage threshold. A technical rule may not downgrade it, just as it may not downgrade a mandatory `Dedicated` requirement.

## Row count alone is insufficient

Placement **must not** be decided by row count alone. Ten million rarely-read audit rows may cost less than five hundred thousand high-frequency GL rows. Decisions must consider actual workload and resource characteristics.

## V1 metric set

The minimal reliable V1 metrics are:

- `EstimatedTenantDataSizeMB`
- `Transactions30d`
- `PeakConcurrentUsers`
- `P95ReportDurationMs`

### Per-metric feasibility

These differ substantially in how obtainable they are, and the difference must not be hidden behind a uniform-looking metric list:

| Metric | Feasibility |
|---|---|
| `PeakConcurrentUsers` | **Available** — reasonably derivable from existing Platform authentication/session data. The cheapest of the four. |
| `Transactions30d` | **Requires application instrumentation** — no database source exists; it must be counted by the application, with its own retention decision. |
| `P95ReportDurationMs` | **Requires tenant-attributed request/report telemetry** — feasible once instrumented, unavailable before. |
| `EstimatedTenantDataSizeMB` | **An estimate on shared storage, not a measurement.** SQL Server reports space per table, index, and database — never per tenant. Inside a shared database, per-tenant size must be derived from row counts and estimated row sizes, which is least accurate exactly where variable-length and large-object columns dominate. |

### Tenant data size is an estimate

The metric is named `EstimatedTenantDataSizeMB` deliberately. On **shared** storage it **must not** be presented as an exact database storage figure, and **must not** be used as the sole promotion trigger. On a **dedicated** database, physical database size is directly measurable and may be reported as such.

If a reliable per-tenant allocation metric is later implemented, the estimate may be replaced — until then the name should keep operators honest about what they are reading.

`LargestTenantTableRows` may be collected for **operator visibility** without being a decision rule. Estimated CPU and IO pressure metrics are **not** required until a reliable collection mechanism exists. *Implementation decision pending: metric collection mechanism, frequency, and retention (initial suggestion: daily snapshots, ~90-day retention).*

For `CustomerManaged` databases, some metrics may be unavailable, less reliable, or collectible only with the customer's permission — server-level pressure in particular is not the platform's to observe. Missing metrics **must** be represented as unknown rather than defaulted, and must not by themselves produce a recommendation.

## Shared database capacity

Evaluation must consider **both** tenant pressure and the capacity of the physical shared database (size, tenant count, storage headroom, connection pressure). A normal tenant on an overloaded `Shared_01` may be recommended to move to `Shared_02` rather than to dedicated storage.

## Technical and business reasons

- **Technical**: data size, transaction volume, concurrency, report pressure, CPU/IO impact, noisy-neighbour effect, shared-database capacity.
- **Business/compliance**: enterprise contract, purchased dedicated isolation, regulatory requirement, data residency, customer infrastructure policy requiring customer-hosted storage, premium SLA, manual platform-admin decision.

## Precedence

Binding precedence, highest first:

1. explicit/manual override
2. regulatory/compliance requirement — including data residency and any `CustomerManaged` hosting requirement
3. contractual/SLA requirement — including a contracted `CustomerManaged` deployment
4. technical policy
5. default placement

A technical rule **must never** downgrade a mandatory Dedicated requirement to Shared, nor a mandatory `CustomerManaged` hosting requirement to `PlatformManaged`.

## Default placement

Default is `PlatformManaged` / **Shared**. Future size **must not** be predicted at onboarding.

## Evaluator

`ITenantStoragePlacementEvaluator` reads the active policy, overrides, and recent metric snapshots; evaluates the configured rules; and produces a recommendation with machine-readable reasons.

It **must not** create a database, copy data, change `TenantDatabaseAssignment`, or perform a cutover. **Decision and execution are separate.** It also **must not** recommend a `HostingMode` change in either direction.

## V1 action model

A technical threshold crossing produces a **recommendation**, not an automatic move. Platform-admin approval is required before any migration. Automatic placement execution is **not** part of V1 and is not represented in the policy model; enabling it requires a future ADR, and any such migration would still have to pass provisioning, schema-health, validation, cutover safeguards, and rollback per `ADR-020`.

## Recommendation states

Minimum V1 decisions: `NoChange`, `RecommendDedicated`, `ManualReview`. Further states are deliberately excluded from V1.

**`RecommendOtherSharedDatabase` has no V1 execution workflow and must not be emitted as an actionable V1 recommendation.** `ADR-020` defines Shared → Dedicated promotion only; Shared → Shared movement is not a supported cutover mode, partly because identifier preservation into a populated shared database is unresolved (`ADR-020`). Emitting a recommendation with no path to act on it produces an operational dead end and invites an improvised migration. The decision becomes available only once an approved Shared → Shared movement workflow exists.

## Breach window

A single transient spike **must not** produce a promotion recommendation. Rules support `RequiredBreaches` and/or a `MinimumObservationPeriod`; a rule must breach consistently across the configured window before it contributes to a recommendation.

## Hysteresis

Promotion and demotion thresholds **must differ**, to prevent oscillation around a single boundary. Concrete values are configuration, not part of this decision.

## Dedicated to shared

Demotion is **manual-only in V1**. Continuous downward rebalancing offers little benefit and carries merge-back risk.

## Cooldown

Placement history retains `LastMovedUtc` (or equivalent), and policy prevents rapid repeated moves via a minimum interval between placement changes.

## Administrative visibility

Future Platform Admin views should present:

- **Tenant grid**: Tenant; Hosting Mode; Storage Mode; server/endpoint display name; Database; Connectivity; Schema Status; Current Migration; Expected Migration; Migration Management Mode; Tenant Data Size (marked as an estimate on shared storage); Transactions; Peak Users; P95 Report Duration; Placement Recommendation; Recommendation Reason; Last Checked.
- **Physical database view**: database; server/endpoint display name; hosting mode; storage mode; tenant count; size/capacity; connectivity status; schema status; migration management mode; last migration; headroom.

`HostingMode`, `Connectivity`, and `Migration Management Mode` are required columns, not optional detail: an operator looking at a degraded tenant needs to know immediately whether the failing component is ours, and whether we are permitted to fix it.

Customer-managed detail views, secret-display prohibitions, and the operational visibility boundary are specified in `ADR-021`.

No UI is implemented by this ADR.

---

# Decision Drivers

- Avoid risky, automatic movement of live ERP data.
- Make placement explainable and auditable.
- Allow tuning without redeployment.
- Honour contractual and regulatory obligations above technical optimisation.
- Prevent oscillation and spike-driven churn.

---

# Alternatives Considered

## Option 1 – No policy; ad hoc operator judgement

### Advantages

- Nothing to build.
- Maximum flexibility per case.

### Disadvantages

- Inconsistent, undocumented decisions.
- No reason trail; no auditability.
- Scales poorly beyond a handful of tenants.

## Option 2 – Hard-coded thresholds in application code

### Advantages

- Simple and deterministic.

### Disadvantages

- Tuning requires redeployment.
- Cannot express business/compliance overrides.
- Encourages single-signal (row count) decisions.

## Option 3 – Configuration-driven policy with recommendation-only output (selected)

### Advantages

- Tunable without redeployment.
- Multi-signal, windowed, hysteresis-aware.
- Business/compliance precedence is explicit.
- Recommendation-only keeps humans in control of live-data movement.
- Machine-readable reasons support auditing and operator UI.

### Disadvantages

- Requires metric collection and a policy model.
- Recommendation-only means promotion is not automatic.

---

# Rationale

Option 3 is selected. The dominant risk in this area is not "a tenant stayed shared slightly too long"; it is "a live ERP database was moved automatically on the strength of a transient signal". Recommendation-only output, breach windows, hysteresis, and cooldown all target that risk directly.

Making thresholds data rather than code is required because the correct values are unknown today and will be discovered operationally; a redeployment cycle per adjustment would guarantee the policy is never tuned.

Explicit precedence is required because technical optimisation and contractual obligation genuinely conflict: a small tenant that has purchased isolation must remain dedicated regardless of its metrics.

Excluding `HostingMode` from the evaluator's output space is a stronger measure than ranking it in the precedence order, and both are applied. Hosting is decided by contract and regulation, never by observation, and a system that *can* express "move this customer-hosted tenant to `Shared_02`" will eventually do so through a misconfigured rule. Removing the output removes the failure mode.

---

# Consequences

## Positive

- Placement decisions become consistent, explainable, and auditable.
- Thresholds are tunable in production without redeployment.
- Contractual and regulatory isolation is structurally protected.
- Spikes and boundary oscillation do not cause migrations.
- Shared-database capacity is considered alongside tenant pressure.

## Negative

- Requires a metric collection pipeline and its retention cost.
- Promotion requires human approval, adding latency to remediation.
- Policy misconfiguration could under- or over-recommend; mitigated by review and audit.

---

# Implementation Guidelines

- Collect the four V1 metrics before building rule evaluation; recommendations without history are not meaningful.
- Persist recommendations with their reasons so operators see *why*, not just *what*.
- Evaluate on a schedule (`EvaluationInterval`), not on the request path.
- Treat shared-database capacity as a first-class rule input, not an afterthought.
- Keep the evaluator free of any write access to routing tables — enforce by dependency shape.

---

# Compliance Rules

1. Placement thresholds **must** be configuration data; changing them **must not** require redeployment.
2. Row count alone **must not** determine placement.
3. The evaluator **must not** create databases, copy data, change assignments, or perform cutover.
4. A manual override **must** take precedence over technical policy, and override changes **must** be audited.
5. Regulatory and contractual requirements **must** outrank technical policy.
6. A single transient breach **must not** produce a promotion recommendation.
7. Promotion and demotion thresholds **must** differ.
8. Dedicated-to-shared movement **must** be manual-only in V1.
9. Automatic movement **must** remain disabled in V1.
10. The evaluator **must not** recommend a `HostingMode` change, and **must not** recommend moving a `CustomerManaged` tenant into `PlatformManaged` shared or dedicated storage.
11. A `CustomerManaged` hosting requirement **must** outrank technical policy, shared-shard capacity, and all storage thresholds.
12. Unavailable metrics **must** be represented as unknown and **must not** be defaulted into a recommendation.
13. Policy and rule changes **must** be platform-admin authorized and audited with actor, timestamp, and before/after values.
14. Every policy edit **must** create or increment a version, and every recommendation **must** record `PolicyId`, `PolicyVersion`, and the metric snapshot identifiers evaluated.
15. A new policy version **must** have its blast radius evaluated and reported, and **must not** silently activate recommendations beyond the configured safety threshold.
16. `AutoMoveEnabled` **must not** exist in the V1 persisted policy model; automatic placement execution requires a future ADR.
17. Tenant data size on shared storage **must** be reported as an estimate and **must not** be the sole promotion trigger.
18. `RecommendOtherSharedDatabase` **must not** be emitted as an actionable V1 recommendation.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Metrics are unreliable or expensive to collect | V1 restricted to four practical metrics; CPU/IO deferred until collection is proven |
| Policy oscillation moves tenants repeatedly | Distinct promote/demote thresholds, required breaches, observation window, cooldown |
| Technical rule overrides a contractual isolation requirement | Binding precedence order with override at the top |
| Recommendation treated as an instruction and executed blindly | Recommendation-only in V1; approval and `ADR-020` preconditions required |
| Shared database saturates while individual tenants look normal | Physical-capacity inputs and `RecommendOtherSharedDatabase` |
| Technical rule relocates a customer-hosted tenant and breaches residency | `HostingMode` excluded from the evaluator's output space entirely, and ranked above technical policy in precedence |
| Missing customer-side metrics are read as zero and misdirect a recommendation | Unknown is represented explicitly and cannot contribute to a recommendation |
| A single threshold edit reclassifies thousands of tenants at once | Blast-radius evaluation and reporting, with a configurable gate that forces manual review |
| A past recommendation cannot be explained after the policy is edited | Policy versioning plus recorded metric-snapshot provenance on every recommendation |
| Policy configuration edited without authorization or audit trail | Policy and rule changes are privileged and audited with before/after values |
| Estimated tenant size on shared storage is treated as measured | Metric named as an estimate and prohibited as a sole trigger |
| An unactionable shared-shard recommendation prompts an improvised migration | `RecommendOtherSharedDatabase` withheld until an approved workflow exists |
| A dormant auto-move flag is enabled without an ADR | Field omitted from the V1 model entirely |

---

# Future Considerations

Revisit if: reliable CPU/IO attribution becomes available; a reliable per-tenant storage allocation metric replaces the shared-storage estimate; automatic shard balancing becomes necessary; a Shared → Shared movement workflow is approved, enabling `RecommendOtherSharedDatabase`; or per-tenant cost accounting is introduced and should influence placement.

**Automatic movement** remains the significant future capability. It is deliberately absent from the V1 model rather than present-and-disabled, and adding it requires a dedicated ADR covering at minimum: what evidence justifies moving live ERP data without a human decision; how a mistaken automatic move is detected and reversed; and how it interacts with the cutover safeguards and rollback regimes in `ADR-020`. It should not be considered until the cutover workflow has an operational track record.

---

# Related Documents

- `ADR-017` — Tenant Storage Topology and Routing
- `ADR-018` — Tenant Schema Health and Migration Orchestration
- `ADR-020` — Shared-to-Dedicated Tenant Migration and Cutover
- `ADR-021` — Customer-Managed Tenant Database Connectivity and Operations

---

# Review Criteria

This ADR should be reviewed if:

- Automatic movement is proposed for production.
- The V1 metric set proves insufficient to distinguish promotion candidates.
- Shared sharding is introduced and shard selection needs policy support.
- Movement between hosting modes becomes a supported workflow, which would change what the evaluator is permitted to output.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | 2026-08-13 | Solution Architecture Team | Initial version — dynamic, configuration-driven tenant placement policy |
| 1.1 | 2026-08-13 | Solution Architecture Team | Added the hosting/storage placement dimensions, customer-managed precedence and evaluator prohibition, unknown-metric handling, and the updated admin grid |
| 1.2 | 2026-08-13 | Solution Architecture Team | Review hardening: made policy administration privileged and audited; added policy versioning and recommendation provenance; added blast-radius gating; documented per-metric feasibility and renamed tenant size as an estimate; removed `AutoMoveEnabled` from the V1 model; withheld `RecommendOtherSharedDatabase` pending an execution workflow |
