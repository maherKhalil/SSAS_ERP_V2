---
id: ADR-020
title: Shared-to-Dedicated Tenant Migration and Cutover
category: Architecture Decision Record
version: 1.3
status: Proposed
date: 2026-08-13
owner: Solution Architecture Team
tags:
  - architecture
  - multi-tenancy
  - persistence
  - operations
  - data-migration
depends_on:
  - ADR-013
  - ADR-017
  - ADR-018
  - ADR-019
used_by:
  - Platform
---

# ADR-020: Shared-to-Dedicated Tenant Migration and Cutover

---

# Status

**Proposed**

Depends on the routing model of `ADR-017`, the schema-health model of `ADR-018`, and the recommendation/approval model of `ADR-019`. It defines the workflow that physically moves one tenant's ERP data from a shared database to a dedicated database and switches routing safely.

---

# Context

`ADR-017` makes a tenant's physical placement a routing concern and retains `TenantId` in dedicated databases so that the entity model, identifiers, and application code are unchanged by placement. `ADR-019` produces a recommendation and requires platform-admin approval but explicitly performs no movement.

What remains undefined is the execution: how a tenant's rows are copied, validated, and cut over without data loss, split-brain routing, or identifier changes. This is the highest-risk operation in the storage architecture because it moves live ERP data belonging to a paying customer.

---

# Problem Statement

Without a defined migration and cutover workflow:

- Routing could be switched before data is validated, exposing a tenant to an incomplete database.
- In-flight writes during the copy could be silently lost.
- Identifiers could change, breaking references held by the application, exports, integrations, or documents.
- Foreign keys could be violated by an arbitrary copy order.
- A failure midway could leave routing partially switched, with some traffic on each database.
- Source data could be deleted before the move is proven, eliminating rollback.

---

# Decision

## Scope of movement

The workflow defined here is **`PlatformManaged` / `Shared` → `PlatformManaged` / `Dedicated`**. That is the normal promotion path and the only one in V1 scope.

Movement **across hosting modes** is explicitly **future, not V1**:

| Movement | V1 |
|---|---|
| `PlatformManaged` Shared → `PlatformManaged` Dedicated | **Supported** — this workflow |
| `PlatformManaged` Dedicated → `PlatformManaged` Shared | Manual-only (`ADR-019`) |
| `PlatformManaged` → `CustomerManaged` | **Future** — not V1 |
| `CustomerManaged` → `PlatformManaged` | **Future** — not V1 |

Cross-hosting movement transfers customer ERP data across an organisational boundary and therefore needs separate data-transfer, security, and contractual decisions that this ADR does not make. It must not be attempted by extending this workflow informally.

## Decision separated from execution

`ADR-019` recommends; this workflow executes, and only after explicit approval. The placement evaluator **must not** change routing, and **must not** recommend a hosting change at all. `TenantDatabaseAssignment` is modified **only** by this migration/cutover workflow.

## Target preconditions

Before data movement, the target dedicated database must:

- exist and be reachable;
- be at a schema version compatible with the source and the deployed application;
- contain the required seed data;
- pass schema-health verification (`ADR-018`);
- be empty of, or otherwise appropriate for, the target tenant's data.

## Source preconditions

The source shared database must:

- be reachable;
- be schema-compatible;
- have a trusted current assignment for the tenant;
- contain the expected tenant data.

Source and target schema versions **must** be compatible before any data movement. Cutover between mismatched tenant schemas is prohibited.

## Cutover flow

1. Approve the placement change.
2. Create a migration/cutover record.
3. Provision the target database.
4. Migrate the target to the current Tenant schema version.
5. Verify schema health on the target.
6. Enter the tenant cutover state.
7. Stop or coordinate writes for that tenant.
8. Copy all tenant-owned ERP rows for `TenantId = X`.
9. Preserve primary keys.
10. Preserve foreign-key relationships.
11. Validate the copied data.
12. Atomically switch `TenantDatabaseAssignment`.
13. Invalidate/refresh resolver caches.
14. Resume traffic.
15. Monitor.
16. Retain a rollback window.
17. Purge or archive source tenant rows only later, through an explicit process.

## Identifier preservation

Primary and foreign identifiers **must** be preserved. Placement is a storage concern and must not change the identity of business records referenced by the application, exports, integrations, or printed documents.

- `Guid` keys are copied unchanged.
- Identity/sequence keys require controlled `IDENTITY_INSERT` during copy followed by a correct reseed of the target table.

This is a **required capability** of the migration tooling, not an optional optimisation. `ADR-013` makes `BIGINT IDENTITY` the default for new identifiers, so most future ERP entities will depend on it.

### Scope: empty dedicated targets only

The preservation guarantee applies to **Shared → empty Dedicated**, which is the V1 promotion path and the only supported cutover mode.

It **must not** be read as a general guarantee for movement **into an already populated shared database**. With `BIGINT IDENTITY` keys, identity values are allocated across all tenants sharing a database, so a preserved key from another database may **collide** with a row belonging to a different tenant. Remapping keys instead is not an acceptable escape — this ADR forbids identifier changes precisely because they propagate into exports, integrations, documents, and audit history.

Consequently **Shared → Shared** and **Dedicated → Shared** are **not supported V1 cutover modes**. They require a separate identity-collision strategy — per-tenant key ranges, separate sequences, or an equivalent — and their own workflow decision. `ADR-019` correspondingly withholds `RecommendOtherSharedDatabase` as an actionable recommendation.

### Rowversion is not an identifier

SQL `rowversion` values are **database-local** and cannot be meaningfully preserved across databases. They are **concurrency tokens, not identifiers**, and are explicitly **outside** the preservation guarantee:

- Target rowversion values are **regenerated**.
- **Validation must not compare source and target rowversion values.**
- Clients holding an `expectedRowVersion` token issued before cutover **must refresh** it afterwards; a concurrency conflict from a stale pre-cutover token is **expected behaviour**, not a defect.

This is mandatory rather than theoretical: `Company` already uses `rowversion` today, exposes it to clients as `expectedRowVersion`, and requires it back on update and delete — and `Company` is the proposed pilot and first cutover subject. The maintenance state around the freeze is the natural place to force a client refresh.

## Foreign-key order

The copy must respect foreign-key dependency order **within** the Tenant ERP database. Ad-hoc `SELECT INTO` or arbitrary table ordering is prohibited.

## Object types the copy must handle

Migration tooling must explicitly account for:

- **`IDENTITY` columns** — controlled insert and correct reseed.
- **Sequences** — independent of identity columns and equally in need of correct positioning on the target.
- **Computed columns** — must not be inserted.
- **`rowversion`** — regenerated, never copied (above).
- **Temporal / system-versioned tables** — history cannot be inserted through the normal path and requires an explicit decision.
- **Large objects, and future file/document storage** — out-of-row or out-of-database content that a row copy will not move.
- **Target triggers** — see below.
- **Database defaults** — see below.

Where an object type is not supported, the tooling **must fail fast** rather than copy it incorrectly. A silent partial copy is the worst available outcome, because everything downstream will treat it as complete.

## Audit and historical value preservation

Historical `CreatedUtc`, `CreatedBy`, `ModifiedUtc`, `ModifiedBy`, and all business timestamps **must be copied verbatim**.

**The copy must not use a persistence path that rewrites these fields.** This is a concrete hazard, not a precaution: the shared persistence pipeline in this repository stamps `CreatedUtc`/`CreatedBy`/`ModifiedUtc`/`ModifiedBy` from the current clock and current user on **every** save, inserts included, and separately enforces that tenant-owned entities carry the trusted current tenant. A copy routed through the ordinary save path would therefore rewrite an entire tenant's audit history to the migration run's timestamp and actor — and none of the row-count, key-uniqueness, or referential checks would notice.

The copy mechanism must therefore operate through a **controlled migration path that deliberately bypasses the application's audit-stamping and tenant-assignment rules**, writing historical values as they are. This is the narrow, reviewed exception to the tenant-context rule in `ADR-017`.

### Target defaults and triggers

Target-side defaults and triggers **must not** rewrite historical audit values, rewrite `TenantId`, or introduce any other migration side effect. Triggers on tenant-owned tables must be identified and disabled for the copy, and their absence or inertness verified before it begins.

## Validation

Validation runs before the routing flip and **must not be able to pass vacuously**. Minimum set:

1. **`TenantId`-filtered row counts per table**, compared source to target — not whole-table counts.
2. **Expected non-empty tables must not pass by both sides being empty.** A copy that silently moved nothing must fail, not succeed.
3. **The target contains exactly one distinct `TenantId`** for a dedicated target.
4. **That `TenantId` is exactly the migrated tenant.**
5. **Primary-key uniqueness.**
6. **Referential closure** — every foreign key value present resolves within the copied set, catching partially-copied parents rather than merely satisfied constraints.
7. **Critical business aggregate counts.**
8. **Selected checksums/hashes** where practical and useful.
9. **Audit-field fidelity** — `CreatedUtc`, `CreatedBy`, `ModifiedUtc`, `ModifiedBy` verified on representative and boundary records rather than assumed.
10. **Boundary-row comparison** — oldest and newest rows of each major table compared field by field, catching truncation and ordering errors that aggregates hide.

A full database-wide checksum is **not** mandatory where impractical.

Checks 2, 3, and 4 exist specifically because the original set could be satisfied by a copy that did nothing, or by a copy that brought the wrong tenant's rows.

## Write consistency

In-flight writes must be addressed explicitly. It is **not** permitted for source writes to continue while a stale copy is promoted, unless a specifically approved online-capture strategy exists.

For V1, a **controlled, short write-freeze cutover window** for the affected tenant is the accepted approach, and is explicitly preferred over hidden dual-write complexity.

### The freeze covers every writer

The freeze applies to **all tenant-scoped writers**, without exception: HTTP/API writes, scheduled jobs, background services, message consumers, webhook processors, imports, asynchronous workflows, integration callbacks, and any other ERP writer.

**The freeze must not be implemented at the transport layer.** A request-path-only freeze would leave every non-HTTP writer running against the source during the copy, and everything they wrote after the copy passed their table would be lost — silently, and invisibly to the validation checks below. The **tenant-write boundary** must consult cutover/maintenance state, so that a writer is blocked wherever it originates.

This rule is recorded now precisely because no tenant-scoped background writer exists yet. Establishing it before the first one is written costs nothing; retrofitting it across a fleet of jobs later is a different exercise entirely.

### Drain before copy

Before the copy begins, all in-flight tenant writes must **complete or roll back**. The copy must not start while tenant state is still changing — a copy against a moving target produces a result no validation can trust.

### Freeze scope

The freeze applies to the **target tenant only**. It must **never** freeze the entire shared database or affect co-tenants. One tenant's promotion is not an outage for everyone sharing its database.

### Freeze failure safety

- The freeze has a **bounded timeout**.
- Exceeding it **aborts** the cutover automatically.
- The freeze is **released on every exit path**, including every failure path.
- The tenant is placed in an **explicit, visible maintenance state**, not a generic error.

A failed cutover must never leave a tenant permanently frozen. Freeze release is the one step that must be as reliable as the copy itself.

### Freeze budget

A **measured, acceptable freeze budget must exist before the first real promotion**, and be communicated to the affected customer in advance. The duration is an empirical property of tenant size and copy throughput and cannot be settled here; what is binding is that it be measured rather than discovered during a customer's business hours. *Implementation decision pending: the freeze mechanism itself and the measured budget.*

## Mapping switch

`TenantDatabaseAssignment` changes **only after** target data validation succeeds. The routing flip is the authoritative cutover point, and occurs **while the tenant is still frozen** (see split-brain prevention below).

The flip must satisfy the assignment concurrency requirements of `ADR-017`: a database-level filtered unique constraint guaranteeing one active assignment per tenant, `RowVersion` optimistic concurrency, a monotonic `RoutingVersion`, and a **single Platform-database transaction** covering the assignment change, the version increment, and the migration/cutover record.

## Split-brain prevention

A cutover can otherwise leave old requests writing to the source while new requests read the target. Preventing that requires more than cache invalidation. The binding sequence:

1. Enter the tenant maintenance/freeze state.
2. Prevent new tenant writes, at the tenant-write boundary.
3. Drain in-flight work.
4. Perform the copy.
5. Validate.
6. **Flip the authoritative assignment while still frozen.**
7. Increment `RoutingVersion`.
8. Broadcast invalidation.
9. All serving nodes and background workers converge on the new `RoutingVersion`.
10. **Only then** resume tenant traffic.

**Traffic must not resume between the mapping flip and routing convergence.** That interval is the entire split-brain window, and steps 6–9 exist to keep it inside the freeze.

### Multi-node routing

No process may hold a tenant's database location indefinitely. Every API node and background worker must **either re-resolve routing at each unit of work, or validate its cached `RoutingVersion`** before using cached routing. A worker that resolved routing once at startup is a split-brain source by construction.

### Connection pools

Open pooled connections to the source database may outlive the routing change, since pools are keyed by connection string and are unaware of routing. Requirement: **work beginning after a routing-version change must use the target connection information**, and the mere existence of a source connection pool must never make the source authoritative. Global pool clearing is **not** required unless implementation proves it necessary.

## Resolver cache

Resolver caches **must not** continue serving stale routing after cutover. Both mechanisms are required, and they play different roles:

- **`RoutingVersion` is the correctness mechanism.** A cached routing entry is valid **only** for the `RoutingVersion` it was resolved under; any node observing a higher version must discard it.
- **Explicit invalidation and a short TTL are propagation and performance mechanisms.**

An earlier draft permitted either mechanism. That is unsafe: broadcast invalidation alone is best-effort, and a node that is starting, partitioned, or restarting can miss the signal and serve stale routing indefinitely — writing tenant data to the pre-cutover database. Version validation alone is safe but pushes a lookup onto every resolve. Correctness rests on the version; the broadcast makes convergence fast.

*Implementation decision pending: cache technology, TTL, and the invalidation transport.*

## Failure and rollback

**Before the mapping switch**, the source remains authoritative. Any failure leaves the tenant on the existing shared database, and the partially populated target is discarded or reset. This case is safe by construction and needs no special procedure.

**After the mapping switch there are two distinct regimes**, and conflating them is a data-loss path:

### Regime A — before the first post-cutover write

The source is still current, because nothing has changed on the target. Rollback is genuinely simple and safe:

- flip routing back;
- increment `RoutingVersion` and invalidate caches;
- resume on the source.

This is the window worth actively protecting — for example by keeping the tenant in a verification state before releasing full write traffic, so that a problem discovered immediately is cheap to undo.

### Regime B — after the first post-cutover write

The source is now **stale**. **A simple routing flip-back is prohibited**: it would silently discard every write made since cutover, while appearing to succeed.

Recovery in this regime requires an **explicit operational restore and reconciliation procedure**, with a **data-loss assessment** and **approval/sign-off**. It is an incident procedure, not a button.

Reverse synchronisation, delta copy-back, and automatic merge are **not V1 capabilities** and must not be assumed available by anyone planning a cutover.

### Post-cutover write marker

The migration/cutover record **must** track whether a post-cutover write has occurred. Which rollback regime applies is then a **recorded fact**, not a judgement call made under incident pressure by whoever is on shift — which is exactly when the wrong answer would be most costly.

Routing **must never** be left half-switched. There is exactly one authoritative assignment at any time.

## Source cleanup and retention

Source rows **must not** be deleted at the moment of cutover. A controlled rollback/verification period is retained, after which purge or archive proceeds through an explicit, audited process.

**Retention policy is selected by migration reason**, which the migration record therefore must carry:

- **Capacity or performance promotion** — the normal rollback retention window is appropriate and desirable.
- **Compliance, data residency, or contractual isolation** — retaining a full copy of the tenant's data in the shared database may itself violate the requirement that motivated the move. Retention becomes a **contractual parameter requiring an explicit decision**, not an operational default.

A retention window that is correct for a capacity promotion can be a compliance breach for a residency-motivated one. The reason must drive the policy rather than the policy being uniform.

## Reverse migration

Dedicated-to-shared migration automation is **not** required in V1. It is manual-only, consistent with `ADR-019`, and is a future workflow.

## Requirements for future cross-hosting cutover

If `PlatformManaged` → `CustomerManaged` movement is ever supported, the following are binding in addition to everything above, and are recorded now so the workflow is not designed into a corner:

- **Connectivity and schema on the target must be verified before any data copy.** A customer endpoint that is reachable today may not be during the copy; proving the path first is not optional, and a customer database whose schema is not compatible cannot receive data at all.
- **Data movement must occur over the approved secure connection** defined in `ADR-021`. Ad-hoc exports, file transfers, or temporary public exposure of either endpoint are prohibited.
- **No routing flip until validation succeeds**, unchanged from the platform-managed case.
- **No automatic fallback after cutover.** Once a tenant is customer-hosted, an outage of the customer endpoint produces a controlled unavailability result; it must never silently revert routing to the platform-managed source, even where that source's data is still retained.
- **Source retention and purge must respect the contract**, since retaining a copy of customer ERP data on platform infrastructure after a residency-motivated move may itself be what the customer was trying to avoid. The retention window becomes a contractual question, not purely an operational one.

The reverse direction (`CustomerManaged` → `PlatformManaged`) carries the same requirements plus an explicit customer authorisation to relocate their data onto platform infrastructure.

## Audit

Each migration/cutover is auditable, conceptually recording: `TenantId`, `SourceDatabaseId`, `TargetDatabaseId`, `Reason`, `PlacementPolicyId`, `PolicyVersion`, `ApprovedByIdentityId`, `ActingPlatformSupportPrincipalId`, `SessionId`, `ExecutedByIdentityId`, `StartedUtc`, `CutoverUtc`, `CompletedUtc`, `PostCutoverWriteOccurred`, `RetentionPolicy`, `Status`, `FailureReason`.

**Approver and executor may legitimately differ** and are recorded separately. Where source and target hosting modes differ, both are recorded, because a cross-boundary data movement is an event a compliance review will ask about specifically. Audit records **must never** contain credential material.

---

# Decision Drivers

- Zero tolerance for tenant data loss.
- No identifier churn caused by storage placement.
- Deterministic, reversible failure behaviour.
- Human approval and auditability for a high-risk operation.
- Preference for a simple, well-understood freeze window over complex dual-write machinery.

---

# Alternatives Considered

## Option 1 – Online copy with dual writes, no freeze

### Advantages

- No customer-visible downtime.
- Attractive for very large tenants.

### Disadvantages

- Substantial complexity: write interception, conflict handling, replay, and verification.
- Failure modes are subtle and hard to test.
- Disproportionate risk for the expected promotion frequency.

## Option 2 – Backup/restore the whole shared database, then delete other tenants

### Advantages

- Uses native database tooling.
- Preserves identifiers trivially.

### Disadvantages

- Copies every other tenant's data onto the target, even temporarily — a serious isolation and compliance problem.
- Deletion of other tenants is slow and error-prone.
- Unusable where the shared database is large.

## Option 3 – Row-level copy with a short, controlled write freeze (selected)

### Advantages

- Copies only the tenant's own rows; no cross-tenant exposure.
- Identifier preservation is explicit and testable.
- Failure before the flip is trivially safe: the source is untouched and authoritative.
- Validation is straightforward and meaningful.

### Disadvantages

- Requires a customer-visible freeze window for that tenant.
- Requires FK-ordered copy tooling and identity reseeding.
- Freeze duration grows with tenant size.

---

# Rationale

Option 3 is selected. The dominant requirement is that a failure must never lose or expose tenant data, and Option 3 achieves this structurally: until the assignment flips, the source database remains the single authority and nothing about the tenant has changed.

Option 2 is rejected outright on isolation grounds — temporarily materialising other tenants' data inside a customer's dedicated database is unacceptable regardless of how quickly it is deleted.

Option 1 is rejected for V1 because its complexity is not justified by the expected frequency of promotions, and its failure modes are precisely the ones that are hardest to test. It remains the natural evolution if freeze windows become unacceptable for very large tenants.

Identifier preservation is mandated rather than left to tooling choice because the alternative — remapping keys — would propagate into exports, integrations, documents, and audit history, turning a storage operation into a data-integrity event.

---

# Consequences

## Positive

- A tenant can be promoted to isolated storage without changing its data, identifiers, or application behaviour.
- Failure before cutover is inherently safe and leaves no trace.
- The pre-write verification window permits a safe routing rollback before the first target write; source retention after writes resume supports investigation and reconciliation, but does not make a simple flip-back safe.
- Every promotion is approved, audited, and explainable.

## Negative

- A customer-visible write freeze is required for the affected tenant.
- Migration tooling must handle FK ordering and identity reseeding correctly.
- Source data is retained temporarily in two places, with the associated storage and compliance considerations.
- Freeze duration scales with tenant data size, limiting the approach for very large tenants.

---

# Implementation Guidelines

- Build and rehearse the workflow against a non-production tenant before any customer promotion, including a deliberate failure and freeze-release rehearsal.
- Derive the copy order from the model's foreign-key graph rather than a hand-maintained list.
- Make every step idempotent and resumable; record progress on the migration record.
- Validate before the flip and again after resuming traffic.
- Treat the assignment switch as a single, atomic, audited write.
- Measure the freeze budget on realistic data volumes, and communicate the expected window to the customer in advance.
- Note that the `Company` pilot uses client-assigned `Guid` keys, so it will **not** exercise the `IDENTITY_INSERT` path. Plan a deliberate test of identity preservation and reseeding rather than assuming the pilot covered it.
- Verify tenant-owned uniqueness is `TenantId`-scoped (`ADR-017`) before the first cutover; a bare natural-key constraint will work in the dedicated target and break portability afterwards.

---

# Compliance Rules

1. The placement evaluator **must not** change routing; only this workflow may modify `TenantDatabaseAssignment`.
2. Migration **must** require explicit platform-admin approval.
3. Source and target schema versions **must** be verified compatible before data movement.
4. For the supported V1 **Shared → empty Dedicated** cutover, primary **and** foreign identifiers **must** be preserved. `rowversion` is **excluded**, being a concurrency token rather than an identifier (Rule 17). This rule **must not** be read as supporting movement into an already populated shared database — see Rule 24, which excludes Shared → Shared and Dedicated → Shared from V1 because `BIGINT IDENTITY` values may collide with other tenants' rows.
5. The copy **must** respect foreign-key dependency order.
6. Validation **must** succeed before the routing flip.
7. Source writes **must not** continue against a stale copy without an approved online-capture strategy.
8. Resolver caches **must** be invalidated at cutover.
9. Routing **must never** be left half-switched.
10. Source data **must not** be deleted at cutover; purge/archive is a later, audited process.
11. Every migration **must** be audited with approver, executor, timestamps, outcome, and failure reason, and **must not** contain credential material.
12. Cross-hosting-mode movement **must not** be performed in V1, and **must not** be improvised by extending this workflow.
13. A cutover **must never** install an automatic fallback to the source database; unavailability after cutover is a controlled result, not a routing revert.
14. The write freeze **must** cover every tenant-scoped writer, enforced at the tenant-write boundary rather than in a transport layer.
15. In-flight tenant writes **must** be drained before the copy begins.
16. The freeze **must** be scoped to the target tenant only, bounded by a timeout, and released on every exit path including failure.
17. `rowversion` **must not** be preserved, **must not** be compared during validation, and clients **must** refresh concurrency tokens after cutover.
18. The copy **must** bypass the application's audit-stamping and tenant-assignment rules, and target defaults/triggers **must not** rewrite copied values.
19. Audit-field fidelity **must** be a validation check, not an assumption.
20. Validation **must not** be able to pass vacuously; the target **must** contain exactly one `TenantId`, and it **must** be the migrated tenant.
21. The routing flip **must** occur while the tenant is frozen, and traffic **must not** resume until all nodes have converged on the new `RoutingVersion`.
22. Resolver caches **must** be validated against `RoutingVersion`; explicit invalidation alone is insufficient.
23. No process may hold tenant routing indefinitely; routing **must** be re-resolved or version-validated per unit of work.
24. Identifier preservation applies to **empty dedicated targets**; Shared → Shared and Dedicated → Shared **must not** be performed in V1.
25. Rollback after the first post-cutover write **must not** be a routing flip-back; the migration record **must** track whether such a write has occurred.
26. Source retention policy **must** be selected by migration reason.
27. Unsupported object types **must** cause the copy to fail fast rather than proceed.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Writes lost during copy | Controlled write freeze for the tenant; validation before flip; no dual-write in V1 |
| Identifier changes break references | Mandatory identifier preservation with `IDENTITY_INSERT` and reseed; validated by key-uniqueness checks |
| FK violations from wrong copy order | Copy order derived from the model FK graph; local FK constraints verified post-copy |
| Split-brain routing | Single authoritative assignment enforced by a filtered unique constraint; atomic flip inside the freeze; `RoutingVersion` as the correctness floor (a cached entry is valid only for the version it was resolved under), with explicit invalidation and a bounded TTL for propagation; traffic stays frozen until serving nodes and workers converge on the new `RoutingVersion`. Cache invalidation alone is **not** sufficient |
| Post-cutover recovery loses writes because the retained source is treated as still current | Two explicit regimes: before the first post-cutover write, controlled flip-back is permitted; after it, simple flip-back is prohibited and recovery requires restore/reconciliation, data-loss assessment, and approval. Retention supports investigation, not automatic rollback |
| Freeze window too long for large tenants | Measure during rehearsal; escalate to an online strategy decision if exceeded |
| Cross-tenant exposure during migration | Row-level copy scoped to `TenantId`; whole-database restore approach explicitly rejected |
| Cross-hosting movement improvised from this workflow | Declared out of V1 scope with its additional requirements recorded; needs separate data-transfer, security, and contract decisions |
| Retained source data after a residency-motivated move breaches the very requirement | Retention selected by migration reason; compliance-motivated moves require an explicit retention decision |
| Background jobs keep writing to the source during the copy | Freeze enforced at the tenant-write boundary, covering every writer type, not at the transport layer |
| Copy rewrites the tenant's entire audit history via the normal persistence path | Copy required to bypass audit-stamping and tenant-assignment rules; audit fidelity added to validation |
| Validation passes against a copy that moved nothing | Non-vacuity checks: expected-non-empty tables, exactly one `TenantId`, and it is the right one |
| Stale routing cache writes tenant data to the pre-cutover database | `RoutingVersion` as the correctness mechanism, with invalidation and TTL for propagation |
| Traffic resumes before all nodes converge, splitting writes across both databases | Flip occurs inside the freeze; convergence precedes resume |
| Post-cutover flip-back silently discards writes made on the target | Two explicit rollback regimes with a recorded post-cutover-write marker |
| Client concurrency tokens break unexpectedly after cutover | `rowversion` regeneration documented as expected; refresh forced via the maintenance state |
| Preserved identity keys collide when moving into a populated shared database | Preservation scoped to empty dedicated targets; shared-target moves excluded from V1 |
| Freeze failure leaves a tenant permanently unable to write | Bounded timeout, automatic abort, release on every exit path |

---

# Future Considerations

Revisit if: freeze windows become unacceptable for the largest tenants (triggering an online-capture decision); dedicated-to-shared demotion needs automation; **movement between hosting modes is contracted**; multi-region moves are required; or change-data-capture becomes available and makes near-zero-downtime movement practical.

---

# Related Documents

- `ADR-013` — Primary Key & Identifier Strategy
- `ADR-017` — Tenant Storage Topology and Routing
- `ADR-018` — Tenant Schema Health and Migration Orchestration
- `ADR-019` — Dynamic Tenant Placement Policy
- `ADR-021` — Customer-Managed Tenant Database Connectivity and Operations

---

# Review Criteria

This ADR should be reviewed if:

- A tenant's freeze window would exceed the agreed operational limit.
- Online migration without a freeze becomes a business requirement.
- Reverse (dedicated-to-shared) migration is required at scale.
- Movement to or from a customer-managed database is contracted.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | 2026-08-13 | Solution Architecture Team | Initial version — shared-to-dedicated tenant migration and cutover |
| 1.1 | 2026-08-13 | Solution Architecture Team | Scoped movement to platform-managed promotion; recorded cross-hosting movement as future with its additional connectivity, secure-transfer, no-fallback, and retention requirements |
| 1.2 | 2026-08-13 | Solution Architecture Team | Review hardening: freeze extended to all tenant writers with drain, scope, timeout and release rules; added split-brain sequence, multi-node routing and connection-pool rules; required `RoutingVersion` **and** invalidation; excluded `rowversion` from preservation and required client token refresh; required the copy to bypass audit-stamping with audit-fidelity validation; added object-type coverage and fail-fast; added non-vacuity validation; replaced rollback with two explicit regimes and a post-cutover-write marker; made retention reason-aware; scoped identifier preservation to empty dedicated targets |
| 1.3 | 2026-08-13 | Solution Architecture Team | Editorial: replaced stale rollback wording in Risks and Consequences that implied source retention alone makes post-cutover flip-back safe; updated the split-brain risk to the `RoutingVersion`-plus-invalidation model; scoped Compliance Rule 4 to the V1 Shared → empty Dedicated path and excluded `rowversion`. No decision changed |
