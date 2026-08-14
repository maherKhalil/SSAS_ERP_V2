---
id: ADR-018
title: Tenant Schema Health and Migration Orchestration
category: Architecture Decision Record
version: 1.7
status: Proposed
date: 2026-08-13
owner: Solution Architecture Team
tags:
  - architecture
  - multi-tenancy
  - persistence
  - migrations
  - deployment
depends_on:
  - ADR-011
  - ADR-017
used_by:
  - Platform
  - HR
  - GL
---

# ADR-018: Tenant Schema Health and Migration Orchestration

---

# Status

**Proposed**

Depends on the physical database model established by `ADR-017`. It defines how multiple physical tenant databases are kept schema-compatible with the deployed application, and how the system behaves when they are not.

---

# Context

`ADR-017` introduces a Platform database plus one or more physical Tenant ERP databases, with `TenantDatabase` describing each physical database and `TenantDatabaseAssignment` mapping tenants onto them.

Today there is a single EF migration stream for `PlatformDbContext`, with `__EFMigrationsHistory` in the `platform` schema, applied to one database. Once tenant ERP data lives in separate physical databases, a release must bring **every** tenant database to the schema version the deployed application expects, and the application must not serve requests against a database it cannot correctly read or write.

A core operational requirement is that adding a new ERP table, column, or index must not require an engineer to run scripts manually against each tenant database.

`ADR-017` further establishes that a physical tenant database may be `PlatformManaged` or `CustomerManaged`. That distinction does not change *what* must be verified, but it does change *who is permitted to change* a database, and it introduces a failure mode the current single-database design has never had: a database that is perfectly healthy schema-wise but simply unreachable, for reasons outside platform control.

---

# Problem Statement

Without a defined schema-health and migration mechanism:

- A release could partially migrate the estate, leaving some tenants on an old schema and producing runtime errors that surface as raw SQL exceptions to end users.
- An older application instance could serve a database migrated by a newer instance, silently reading or writing an incompatible schema.
- A database with an unexpected migration lineage could have migrations appended blindly, corrupting its history.
- Two deployment or application instances could migrate the same database concurrently.
- Operators would have no reliable answer to "is the estate healthy and at which version?"
- The orchestrator could apply DDL to a customer-owned database that the customer's own DBA is contractually responsible for.
- An unreachable database would be indistinguishable from a schema-incompatible one, because one overloaded status cannot express both.

---

# Decision

## Two migration streams

- **Platform database**: the existing Platform migration stream (`PlatformDbContext`), unchanged.
- **Tenant ERP databases**: a new, independent **Tenant migration stream** (`TenantDbContext`).

Tenant migrations apply to **physical tenant databases**, never to tenants.

## Physical database count is the migration unit

Tenant count and migration-target count are different quantities. For example, 10,000 tenants may map to 4 shared databases plus 20 dedicated databases — **24 Tenant migration targets**, not 10,000. Migration planning, reporting, and timing are expressed in physical databases.

## Schema health service

A **read-only** service, conceptually `ITenantDatabaseSchemaHealthService`, is responsible for:

- discovering the distinct physical `TenantDatabase` records;
- connecting with trusted, server-side credentials;
- reading each database's migration history;
- comparing it to the deployed application's Tenant migration set;
- returning a schema-compatibility result;
- optionally updating cached operational health state in the Platform database.

It **must not** apply migrations. A health check running must never cause a schema change.

## Source of truth

The **deployed Tenant EF migration catalog**, compared against each target database's `__EFMigrationsHistory`, is the primary migration-version truth.

Platform database fields such as `LastAppliedMigration`, `SchemaStatus`, and `LastSchemaCheckUtc` are **cached operational metadata only** and are never the sole authority for a correctness decision.

## Four orthogonal status dimensions

Operational health is **not** one status. A single overloaded enum mixing connectivity, compatibility, lifecycle, and execution cannot express real states — a database may be schema-current and unreachable, or reachable and incompatible — and forces an operator to guess whether a failure is a network incident or a release problem.

The model is therefore **four independent dimensions**, never merged:

| Dimension | Values |
|---|---|
| **`ProvisioningStatus`** | `Registered`, `Provisioning`, `Onboarding`, `Ready`, `Disabled` |
| **`ConnectivityStatus`** | `Unknown`, `Healthy`, `Unreachable`, `AuthenticationFailed` |
| **`SchemaCompatibilityStatus`** | `Unknown`, `UpToDate`, `PendingMigrations`, `AheadOfApplication`, `MigrationHistoryMismatch` |
| **`MigrationExecutionStatus`** | `Idle`, `Migrating`, `Succeeded`, `Failed`, `BlockedPendingCustomer` |

Names may be adjusted to repository conventions; the **separation** is binding, the spelling is not.

**`Ready` is derived, never independently writable.** It is a conclusion drawn from the four dimensions together with valid routing and resolvable credentials. A manually settable `Ready` flag would inevitably drift from the dimensions it summarises and would then be trusted in preference to them.

`TlsFailure` and `NetworkBlocked` are recorded as future refinements of `ConnectivityStatus`, valuable mainly for `CustomerManaged` endpoints where they materially shorten diagnosis. *Implementation decision pending: whether the data-access provider distinguishes TLS and network-block failures dependably, or whether they collapse into `Unreachable`.*

## Connectivity health service

A `ITenantDatabaseConnectivityHealthService` (or repository-conventional equivalent) is warranted, separate from the schema-health service. Its responsibilities:

- discover physical databases from `TenantDatabase`;
- attempt a safe connection using trusted routing metadata and resolved credentials;
- perform a **minimal** health query only;
- record result, latency, and check timestamp;
- classify failure as network, authentication, database-unavailable, or TLS where it can do so reliably.

Constraints:

- It **must not** run expensive or business queries. A trivial probe is the whole contract.
- It **must not** leak secrets: failure detail recorded or displayed must never contain credential material, and must never echo a connection string.
- Like the schema-health service, it is **read-only** with respect to schema.

Its separation from schema health matters because the two answer different questions, fail for different reasons, and run on different cadences — and because a connectivity failure must not be reported as a schema problem.

### One writer per dimension

The separation is binding at the **write** boundary, not merely in the reported values. Each dimension has exactly one writer, and **a check that observes nothing about a dimension writes nothing to that dimension**:

| Writer | Owns |
|---|---|
| Connectivity health | `ConnectivityStatus`, `LastConnectivityCheckUtc` |
| Schema health | `SchemaCompatibilityStatus`, `AppliedMigration`, `TargetMigration`, `LastSchemaCheckUtc` |
| Migration orchestration | `MigrationExecutionStatus` and its timestamps/error |

The consequence that motivated stating this explicitly: a schema check that **cannot connect** has observed nothing about schema. It must record connectivity and leave the previous schema verdict — and its `LastSchemaCheckUtc` — exactly as it found them. Overwriting them with `Unknown` would destroy precisely the observation the bounded stale-compatible policy exists to keep serving, turning a transient network blip into an avoidable denial that outlives it.

`LastSchemaCheckUtc` therefore means *the last time schema was actually observed*, and is never advanced by a connectivity result. Schema freshness is computed from it alone; a frequent connectivity cadence must never make a stale schema verdict look fresh.

Because the dimensions share one physical row, concurrent writers will contend on `RowVersion`. A losing writer **re-reads and reapplies only its own dimension's observation**, bounded; it never replays a stale whole aggregate, which would be last-write-wins across dimensions. This property becomes load-bearing when recovery readiness (`TS-Backup`) adds a third writer.

None of this weakens the deny rules: `PendingMigrations`, `AheadOfApplication`, `MigrationHistoryMismatch` and `Unknown` all continue to deny, and staleness never rescues them. Only a previously-observed `UpToDate` participates in the bounded stale-compatible policy.

## Tenant database unavailable

The Platform database can be fully available while a tenant ERP database is not — most obviously when the tenant database is customer-hosted and the customer's network or server is down.

Consequently a user may authenticate successfully, receive a valid token, and resolve their platform authority and tenant membership, while ERP module access cannot be served. That asymmetry is intentional (`ADR-017`).

ERP request paths in that condition **must** terminate in a controlled result — conceptually `TenantDatabaseUnavailable`, alongside the `DatabaseUpgradeRequired` / `DatabaseUpgrading` conditions below, or the repository-conventional equivalent — surfaced through the established problem/response conventions. Normal API execution **must not** fall through to a raw `SqlException`, connection timeout, or `InvalidOperationException`. The response must not disclose endpoint, credential, or infrastructure detail.

## Ahead-of-application databases

A database whose migration history is **newer** than the running application's migration set **must not** be treated as `UpToDate`. It is `AheadOfApplication`. An older application instance must not blindly serve a newer, potentially incompatible tenant database; the behaviour is gated per `Traffic gating` below.

## Migration history mismatch

An unexpected migration lineage — a history containing migrations the application does not know, or diverging from the expected order — **fails closed** as `MigrationHistoryMismatch`. Migrations **must not** be appended blindly to an unknown history. Resolution requires controlled manual investigation.

## Health caching and freshness

`__EFMigrationsHistory` **must not** be queried on every business request, and a cached `UpToDate` **must not** be trusted indefinitely. Both extremes are wrong; the resolution is an explicit freshness model.

Every cached health record carries **`LastCheckedUtc`** and is evaluated against a **configured freshness window** and a longer **hard-stale bound**. Binding behaviour:

- **Background refresh** runs on a cadence independent of request traffic.
- **Event-driven refresh** occurs immediately after any migration or cutover — a successful migration publishes the new status rather than waiting for the next poll.
- **A stale *incompatible* status continues to DENY.** Staleness never upgrades a known-bad database.
- **A stale *compatible* status may continue to ALLOW** during the configured grace window while a refresh runs. This asymmetry is deliberate: denying on staleness would let a failure of the background checker take down an otherwise healthy estate.
- **Past the hard-stale bound, DENY** regardless of the last known value.
- **`Unknown` denies**, being the pre-verification state.

Exact durations are configuration; the existence of both bounds and the asymmetric treatment are binding.

## Traffic gating

Normal ERP traffic **must not** be routed to a known-incompatible or unreachable tenant database. The system exposes controlled conditions — conceptually `DatabaseUpgradeRequired`, `DatabaseUpgrading`, `TenantDatabaseUnavailable`, or the repository-conventional equivalent — surfaced through the established problem/response conventions. Raw SQL errors **must not** be the gating mechanism, and gating **must not** fall back to a different database (`ADR-017`).

Binding V1 behaviour:

| Condition | ERP traffic |
|---|---|
| `Ready` + `Healthy` + `UpToDate`, fresh | **ALLOW** |
| `Ready` + `Healthy` + `UpToDate`, stale within hard-stale bound | **ALLOW**, and trigger refresh |
| Beyond hard-stale bound | **DENY** |
| `SchemaCompatibilityStatus` = `Unknown` | **DENY** |
| `PendingMigrations` (including pending customer DBA action) | **DENY** |
| `MigrationExecutionStatus` = `Migrating` | **DENY** |
| Cutover in progress | **DENY** |
| `AheadOfApplication` | **DENY** |
| `MigrationHistoryMismatch` | **DENY** |
| `Unreachable` | **DENY** |
| `AuthenticationFailed` | **DENY** |
| No Ready assignment | **DENY** |

**No read-only compatibility mode in V1.** A schema the application cannot write correctly is generally one it cannot read correctly either, and a partial mode would double the state space and the testing burden for a marginal availability gain. Denial with a clear, controlled result is the decision.

## Migration orchestrator

A separate, **write-oriented** service, conceptually `ITenantDatabaseMigrationOrchestrator`, is responsible for:

- discovering physical databases;
- preflight schema-health evaluation;
- determining pending migrations;
- applying approved migrations;
- recording progress per database;
- retrying and resuming safely;
- post-verifying the resulting migration history;
- producing a deployment summary.

Health checking and migration remain **separate responsibilities**; the orchestrator consumes the health service.

## Migration ownership

**Deployment tooling owns estate migration.** It is responsible for mass tenant database migration, estate-wide orchestration, pre-release schema preflight, and post-migration verification.

**Application runtime must not auto-migrate the tenant database estate.** Serving instances race each other, startup time would scale with estate size, a customer-managed endpoint would block startup on a network outside our control, and DDL authority should not live in the process that serves requests.

Application startup **may** perform a **read-only compatibility preflight** for the databases it is expected to serve, and **must not** advertise healthy status while any assigned database is known incompatible. Startup **must not** migrate.

## Migration management mode

Who is permitted to apply DDL to a physical database is a **separate concept** from who hosts it, and is recorded per physical database as `MigrationManagementMode`:

- **`AutomaticByPlatform`** — the orchestrator is normally allowed to migrate this database as part of a release.
- **`PlatformAfterApproval`** — the orchestrator may migrate, but only after an explicit per-run approval; pending migrations are detected and reported, and nothing is applied until approved.
- **`CustomerDba`** — the platform **never** applies DDL. It detects and reports pending migrations; a customer DBA executes them.

These names are deliberately **not** reused from `HostingMode`. An earlier draft used `PlatformManaged`/`CustomerManaged` for both concepts, which made the perfectly ordinary combination "customer-hosted database that we are permitted to migrate" read as a contradiction in configuration, logs, and operator tooling. The concepts are independent and their vocabularies are now distinct.

| `HostingMode` | Typical `MigrationManagementMode` | Also valid |
|---|---|---|
| `PlatformManaged` | `AutomaticByPlatform` | `PlatformAfterApproval` for sensitive estates |
| `CustomerManaged` | `PlatformAfterApproval` — customer permits us to migrate under an agreed window | `CustomerDba` where the customer's DBA requires control |

A customer-hosted database whose customer is happy for us to run migrations is a normal and expected configuration; so is a platform-hosted database that a regulated customer wants gated behind explicit approval. The mode is configurable per physical database according to the customer agreement.

## Customer-managed migration operating models

**Model A — platform executes.** The customer permits SSAS ERP to apply approved tenant migrations to their database. The flow is unchanged from the platform-managed case: preflight health → migrate → post-verification → Ready. It requires a migration-capable credential (see `ADR-021`) and an agreed maintenance window.

**Model B — customer DBA executes** (`CustomerDba`). The platform:

1. detects pending migrations through the normal schema-health comparison;
2. makes **no** modification to the database;
3. identifies/provides the required approved migration package;
4. waits while the customer DBA executes it;
5. re-runs schema health and verifies the **actual** `__EFMigrationsHistory`;
6. only then treats the database as compatible.

Verification is on observed history, never on the customer's assertion that the migration was applied. Migration package generation is **not** implemented now; the requirement is recorded so the schema-health and reporting model can accommodate it.

## Migration authority

The orchestrator **must** respect `MigrationManagementMode` and **must not** execute DDL against a database whose mode denies it. Where the mode is `CustomerDba`, the orchestrator's only permitted actions are read-only detection, reporting, and post-hoc verification. Where the mode is `PlatformAfterApproval`, absence of approval is treated as denial, not as a default-allow.

A deployment run that encounters such databases completes successfully and reports them as blocked-pending-customer-action rather than failing the release or forcing the migration.

## Schema health applies to every database

Migration ownership does **not** affect the obligation to verify. **Every** physical tenant database — platform-hosted and customer-hosted alike — is checked against the deployed Tenant EF migration catalog versus its actual `__EFMigrationsHistory`. Customer-hosted databases are **not** exempt.

For example, where the application expects `M047` and a customer database reports `M045`, the result is `PendingMigrations` / upgrade-required, and that tenant's ERP traffic is gated exactly as it would be for a platform-hosted database. The only difference is who is expected to act.

## Migration-history compatibility versus schema drift

Two distinct checks, deliberately kept apart:

- **`MigrationHistoryCompatibility`** — does the recorded migration history match the deployed catalog. **Required in V1**, for every database.
- **`SchemaDrift`** — do the actual columns, indexes, constraints, and other objects match the model. **Optional** in V1.

Customer-managed databases materially raise drift risk, because a customer DBA can alter a table, column, index, constraint, or procedure without any migration being recorded — leaving the history perfectly compatible while the schema is not.

### Mandatory drift floor for customer-managed databases

History comparison alone is **not sufficient** to onboard a customer-managed database. Before the first `CustomerManaged` onboarding, the following minimum verification is **mandatory** — for that hosting mode only:

1. **Expected table existence** across the tenant schema.
2. **Expected column existence, type, and nullability.**
3. **Primary key and unique constraint verification** — these carry the identity and isolation guarantees.
4. **A stored schema fingerprint** (below).
5. **Trigger detection on tenant-owned tables** — a customer trigger can silently rewrite audit values or block writes.
6. **Required database settings verification**, per the environment matrix below.

These are catalog queries and a hash, not a table scan; the cost is small relative to operating a database we do not control. Full schema diffing remains unnecessary.

For the **platform-managed** estate, drift detection remains an optional future capability, since no third party can alter those databases outside the migration stream.

### Schema fingerprint

A **schema fingerprint** is a hash computed over a stable, ordered projection of the expected tenant schema shape as read from the database catalog — tables, columns, types, nullability, keys, unique constraints, and indexes. Captured at onboarding and recompared on the schema-health cadence, it detects unexpected customer-side change even where the shape still looks superficially plausible.

*Implementation decision pending: the exact catalog projection, ordering, and hashing approach. The projection must be stable across servers that differ only in irrelevant ways, or the fingerprint will produce false alarms and be switched off.*

## Tenant SQL environment compatibility matrix

A **Tenant SQL Environment Compatibility Matrix** is required before the first `CustomerManaged` onboarding. Once the platform no longer chooses the server, "whatever the customer has" is not an acceptable environment specification.

It must eventually cover: SQL Server major versions; edition constraints; database compatibility level; collation; case sensitivity; ANSI settings; RCSI/isolation settings; TLS versions; database naming constraints; required capabilities and features; runtime credential permissions; and migration credential permissions.

**No version numbers are decided here** — the repository and product documentation establish none, and inventing them would be worse than leaving the matrix explicitly outstanding. *Requires follow-up decision: the concrete matrix contents.* Environment verification is part of customer-managed Ready criteria (`ADR-021`), so the matrix must exist before onboarding can be completed, not merely before it is convenient.

Collation deserves particular attention: the existing schema already depends on collation behaviour for normalized-code and normalized-email uniqueness and for a binary-collated currency column, so a server whose collation differs is not a cosmetic difference.

## Azure SQL

**Azure SQL is out of scope for V1 Tenant ERP databases.** Future support requires an ADR amendment.

This is resolved now rather than deferred because it is not merely an onboarding checklist item — it changes migration locking semantics, the connection and failover model, available server-level features, and cutover assumptions. Leaving it ambiguous would mean designing the orchestrator against two different platforms at once.

No repository or product document establishes Azure SQL as a supported target, and the current baseline runs on SQL Server. Nothing in this package should be read as implying Azure SQL compatibility.

## Isolation level and RCSI

The required isolation configuration **must be declared explicitly by the environment matrix**, set by platform provisioning, and verified at customer onboarding and on the schema-health cadence.

RCSI is **not** blanket-mandated here. The platform-plane concurrency work in this repository was deliberately built to be correct **independent** of `READ_COMMITTED_SNAPSHOT`, and is tested in both regimes; mandating a setting on no evidence would contradict that design. Tenant ERP concurrency requirements are not yet established, and whoever establishes them owns this value.

What is binding is that **no database may silently inherit whatever default its creator happened to use.** This matters concretely: EF Core's SQL Server database creator enables RCSI on databases it creates, while SQL Server's own default is off — so every platform-created and test database will differ from a typical customer-created one, and the tenant schema would otherwise first meet the opposite regime in production. The declared value must also be the value the tests exercise.

## Migration concurrency

Migration of a single physical database requires **single-writer ownership**. Two deployment or application instances **must not** migrate the same database concurrently.

The locking primitive may remain an implementation choice, but these invariants are binding regardless of mechanism:

1. **Mutual exclusion is scoped to one physical database**, not the estate — one slow database must not serialise the whole release.
2. **Ownership is acquired before any DDL.**
3. **Ownership is held through post-verification**, not released after the last statement.
4. **Acquisition has a bounded timeout.**
5. **Failure to acquire is a clean skip-and-report** — never a forced proceed.
6. **Loss of ownership mid-run aborts the run** and marks the database failed; it is never treated as probably-fine.
7. **Ownership is crash-safe**: a crashed holder must neither leave the database permanently locked nor permit a second writer.
8. **Post-verification runs under the same ownership**, so no concurrent writer can slip in between apply and verify.

A SQL application lock in the target database is the likely implementation and has direct precedent in the repository's existing serialization work, but it is **not** binding here.

## Post-migration verification

A successful migration API call is **not** sufficient evidence. After migrating, the orchestrator re-reads `__EFMigrationsHistory` and verifies the expected end state. Only then may the database be marked `UpToDate` / Ready.

## New database readiness

A newly created tenant database is **not** Ready after `CREATE DATABASE`. Ready requires: database created; all current Tenant migrations applied; required seed data applied; schema-health verification passed; and valid routing metadata registered.

For `CustomerManaged` databases, `CREATE DATABASE` may not be the platform's action at all — the customer's DBA may own creation — and reachability is not a given. Their Ready criteria are correspondingly broader and are defined in `ADR-021`; platform-managed **provisioning** and customer-managed **onboarding** are distinct workflows and their assumptions must not be applied to each other.

## Deployment report

Each orchestration run produces a summary covering: physical databases discovered; already current; migrated successfully; failed; ahead of application; history mismatch; unreachable; ready; blocked. Databases blocked pending customer DBA action are reported as their own category, distinct from failure.

## Schema drift

Migration-version compatibility (history comparison) is **required in V1** for every database. Full schema-drift detection — comparing actual columns, indexes, and constraints against the model — is an **optional future operational capability** for the platform-managed estate, and is **strongly recommended** for customer-managed databases as described above.

---

# Decision Drivers

- Releases must be safe and repeatable across a growing estate.
- Operators need a truthful, per-database health answer.
- Failure must be visible and fail-closed, not surfaced as raw SQL errors.
- Manual per-database scripting must be eliminated.
- Cost must scale with physical databases, not tenants.

---

# Alternatives Considered

## Option 1 – Migrate on application startup, per database

### Advantages

- Simple; no separate orchestration component.
- Databases converge without a deployment step.

### Disadvantages

- Multiple instances race to migrate the same database.
- Startup time grows with estate size; a failure blocks the instance.
- No estate-level reporting or retry.
- Encourages serving traffic while migrations run.

## Option 2 – Manual DBA scripts per release

### Advantages

- Full human control; no new code.

### Disadvantages

- Directly violates the requirement that new tables/columns not require manual per-database work.
- Error-prone and unscalable beyond a handful of databases.
- No reliable version truth; drift becomes normal.

## Option 3 – Dedicated orchestrator plus read-only health service (selected)

### Advantages

- Explicit separation of "is the estate healthy?" from "change the estate".
- Estate-level reporting, retry, and resumption.
- Enforces single-writer migration and post-verification.
- Enables traffic gating on cached, trustworthy status.
- Accommodates databases the platform may observe but must not modify, which is the only model compatible with customer-owned databases.

### Disadvantages

- New components to build, test, and operate.
- Deployment gains a mandatory orchestration step.
- A release can complete with part of the estate legitimately un-migrated, awaiting customer action.

---

# Rationale

Option 3 is selected because the failure modes that matter — partial estate migration, concurrent migration of one database, ahead-of-application databases, and unknown lineage — are precisely the ones Options 1 and 2 cannot detect or prevent. Separating the read-only health service from the write-oriented orchestrator keeps "observe" and "change" independently testable and makes it structurally impossible for a health check to mutate schema.

Making the deployed migration catalog compared against `__EFMigrationsHistory` the source of truth, with Platform fields as cache only, avoids the classic failure where a metadata column claims a version the database does not actually have. That property becomes essential once a customer DBA applies migrations we did not run: observed history is the only trustworthy evidence, and self-reported status is worthless.

Separating `MigrationManagementMode` from `HostingMode` is deliberate. The tempting shortcut — "customer-hosted therefore customer-migrated" — is wrong in both directions: most customer-hosted deployments will prefer that we run migrations under an agreed window, and some platform-hosted regulated estates will want approval gating. Modelling permission explicitly means the orchestrator asks "am I allowed?" rather than inferring it from where the server happens to live.

Separating `ConnectivityStatus` from `SchemaStatus` is likewise not cosmetic. Under one combined status, a customer VPN outage and a failed release are indistinguishable, and the operator response to those two is completely different.

---

# Consequences

## Positive

- New ERP tables/columns reach every tenant database without manual scripting.
- Estate health is observable and reportable per physical database.
- Incompatible or unreachable databases are gated rather than failing with raw SQL errors.
- Migration effort scales with physical databases.
- Network/authentication problems are diagnosable as such, separately from schema problems.
- Databases the platform may not modify are fully supported without weakening verification.

## Negative

- Additional components and a mandatory deployment step.
- Cached status introduces a freshness window that must be reasoned about.
- Gated states must be represented in API/UI behaviour.
- Ahead-of-application handling constrains rollback of application versions.
- Three status dimensions plus a management mode make the operational model larger to learn and to display.
- Estate convergence is no longer purely within platform control; customer-executed migrations introduce coordination latency.

---

# Operational recovery readiness is not schema readiness

**Deferred capability — documented now so the two are not conflated. Implemented in `TS-Backup` (`ADR-017`).**

Schema compatibility and **operational recovery readiness** are distinct dimensions, and neither implies the other. A database can be fully migrated, `UpToDate`, reachable, and serving traffic while having **no usable backup chain at all**.

**A successful migration is not evidence of recoverability.** The four status dimensions this ADR defines — provisioning, connectivity, schema compatibility, migration execution — answer "can the application correctly read and write this database". None of them answers "could this database be restored if it were lost". That question requires separate evidence: that a policy exists, that the chain is initialised, that the recovery model supports it, and — the only evidence that actually counts — that a **restore has been verified**, not merely that backups have been written.

The practical failure this prevents: a migration orchestrator reports a green estate, a release is judged safe, and a database that has never had a verifiable backup is carrying production data. Recovery readiness therefore belongs alongside the health dimensions as a **separate** reported state when `TS-Backup` lands, never folded into `SchemaCompatibilityStatus` or into a derived `Ready`.

**`ADR-022` owns that dimension.** It defines recovery readiness as an independent fourth dimension with its own writer, following the one-writer-per-dimension discipline established below. Backup and recovery fields are **not** added to the schema-health model, and the schema-health service neither reads nor writes them: this ADR remains scoped to connectivity, schema compatibility and migration execution.

Migration is also a moment when recovery matters most: applying DDL to a tenant database is precisely when a restore point is most likely to be needed. Backup verification and migration orchestration are separate responsibilities, but a migration run against a database with no verified recovery position is an operational decision that should be taken knowingly rather than by omission.

---

# Deployment / migration application procedure

**Status: superseded for the platform-managed estate.** The orchestrator described above is now implemented, and it is the normal path for applying the Tenant stream across physical databases. The commands below are **retained as the development and break-glass procedure** — scaffolding a migration, working against a local database, and recovering a single database when orchestration is unavailable.

What has NOT changed: the Platform stream is still applied by deployment tooling, and neither stream is applied automatically at host startup.

**Normal platform-managed estate migration** now runs through `ITenantDatabaseMigrationOrchestrator`, which discovers physical databases, evaluates health as preflight, acquires single-writer ownership per database, applies the Tenant stream, verifies the resulting history, and reports per-database outcomes. Databases it may not migrate — `CustomerDba`, or `PlatformAfterApproval` without approval — are reported as blocked, not failed.

The rest of this section remains accurate for the manual path.

## There are two independent migration streams

| Stream | Owns | Migration history |
|---|---|---|
| `PlatformDbContext` | Platform-plane schema — tenancy, identity, authentication, membership, roles, localization, platform-support authority, tenant-storage registry | `platform.__EFMigrationsHistory` |
| `TenantDbContext` | Tenant ERP schema — `Company` today, HR/GL and the rest later | `tenant.__EFMigrationsHistory` |

**Applying one stream does not imply the other has been applied.** They have separate histories, separate snapshots, and no shared migration identifiers. A deployment that runs only the platform stream leaves the tenant schema entirely absent, and nothing in the running application will correct that.

## Nothing applies these automatically

There is, today:

- **no automatic migration on host startup** for either stream;
- **no fleet migration orchestration** — no discovery of physical tenant databases, no rolling application, no concurrency coordination;
- **no background migration worker.**

`TenantMigrationRunner` is **single-target tooling** for one explicitly-named tenant: it reports applied and pending migrations and can migrate that one routed database. It is not registered for automatic execution and must not be described or used as fleet orchestration, startup auto-migration, or rolling-deployment coordination.

## Applying each stream

Both contexts live in `SSAS.Platform.Infrastructure`, which is also its own startup project for design-time commands (the API host does not reference `Microsoft.EntityFrameworkCore.Design`).

**Platform stream** — requires the `ConnectionStrings__Platform` environment variable; the design-time factory fails fast without it:

```
ConnectionStrings__Platform="<platform connection string>" \
dotnet ef database update \
  --project src/Platform/SSAS.Platform.Infrastructure/SSAS.Platform.Infrastructure.csproj \
  --startup-project src/Platform/SSAS.Platform.Infrastructure/SSAS.Platform.Infrastructure.csproj \
  --context PlatformDbContext
```

**Tenant stream** — target database supplied via `SSAS_TENANT_MIGRATION_SQLSERVER`. Run this **once per physical tenant database** that is intended to serve tenant ERP traffic:

```
SSAS_TENANT_MIGRATION_SQLSERVER="<tenant database connection string>" \
dotnet ef database update \
  --project src/Platform/SSAS.Platform.Infrastructure/SSAS.Platform.Infrastructure.csproj \
  --startup-project src/Platform/SSAS.Platform.Infrastructure/SSAS.Platform.Infrastructure.csproj \
  --context TenantDbContext
```

Connection strings are supplied by the deployment environment and **must never be committed to documentation, configuration in source control, or logs**.

## Shared topology: same catalog, still two streams

In the current shared topology the Platform database and the shared tenant ERP database are the **same physical SQL Server catalog**. That changes nothing about the procedure. The two streams remain logically independent, write to separate history tables in separate schemas, and **both commands must still be run** against that catalog. Sharing a catalog is a deployment fact, not a merging of the streams.

## Dedicated topology

A `PlatformManaged` + `Dedicated` tenant ERP database receives the **Tenant stream only**. The Platform stream is not applied to it: the platform plane lives in the Platform database, and a dedicated tenant database holds tenant ERP data alone. Should a future decision require any platform-side object inside tenant databases, that decision must be recorded explicitly; it is not implied today.

## Recommended sequence

For operational simplicity, use one standard order:

1. Apply the **Platform** stream.
2. Apply the **Tenant** stream to every physical tenant database that will serve traffic.
3. **Validate** (below).
4. Only then serve tenant ERP traffic.

**A note on ordering tolerance.** The `Company` transition migrations were deliberately written to survive either order — the platform migration renames `platform.Companies` rather than dropping it, and the tenant migration copies from whichever source table exists. That is a property of **this specific transition**, achieved on purpose, and **must not be generalised**: future migrations are not automatically order-independent, and any future cross-stream data movement must have its ordering safety established for itself.

## The Company transition makes this concrete

The consequence of stopping halfway is specific and worth stating plainly:

- The platform migration renames `platform.Companies` → `platform.Companies_MigratedToTenant`. **No data is lost**; the rows are intact under the retained name.
- The tenant migration creates `tenant.Companies` and copies those rows across.

If **only** the platform stream is applied, the data is safe but `tenant.Companies` does not exist, and **every `Company` operation fails**. The failure is loud and fully recoverable by applying the tenant stream — but it is an invalid deployment state, not a degraded one, and it will not repair itself.

## Validation before serving ERP traffic

Until the health service exists, verify manually:

- the Platform stream is at the expected version (no pending migrations);
- the Tenant stream is at the expected version on each tenant database;
- `tenant.__EFMigrationsHistory` exists on each tenant ERP database;
- the `tenant` schema exists, and for this slice `tenant.Companies` exists;
- `TenantStorage:Servers` contains an entry for every `ServerKey` the registry routes to.

Pending state can be read per stream with `dotnet ef migrations list --context <PlatformDbContext|TenantDbContext>` using the parameters above.

## This procedure is temporary

The health service and orchestrator specified in this ADR replace it. Once implemented, physical `TenantDatabase` discovery, schema-compatibility reporting, controlled migration execution, concurrency coordination, and operational progress reporting take over what is currently a manual per-database command. Note also that **migration success remains distinct from backup/recovery readiness** (above): completing this procedure says nothing about whether the resulting database is recoverable.

---

# Implementation Guidelines

- Build the health service before the orchestrator; the orchestrator consumes it as preflight.
- Until the orchestrator exists, apply both migration streams explicitly per the deployment procedure above; neither is applied automatically at startup.
- Keep recovery readiness a separate reported dimension from schema compatibility; never let a successful migration imply a recoverable database.
- Make orchestration idempotent and resumable; re-running after partial failure must be safe.
- Record per-database outcome, timestamps, and the failure reason on `TenantDatabase`.
- Treat "unknown/stale status" conservatively in the request path.
- Keep the Tenant migration stream physically separate from the Platform stream from the first migration.
- Consult `MigrationManagementMode` before any DDL path, not as a late guard inside it.
- Run connectivity checks on a shorter cadence than schema checks; connectivity changes far more often.
- Report blocked-pending-customer-action distinctly so a release is not judged failed because a customer has not yet acted.

---

# Compliance Rules

1. Tenant migrations **must** target physical databases discovered from `TenantDatabase`, never a tenant list.
2. The health service **must not** apply migrations.
3. `__EFMigrationsHistory` compared to the deployed migration catalog **must** be the authoritative version source; Platform metadata is cache only.
4. A database ahead of the application **must not** be reported or treated as `UpToDate`.
5. An unrecognised migration lineage **must** fail closed and **must not** receive appended migrations.
6. Migration of one physical database **must** have single-writer ownership.
7. Migration success **must** be confirmed by re-reading migration history before marking Ready.
8. A new database **must not** be marked Ready until migrated, seeded, verified, and registered.
9. Known-incompatible or unreachable databases **must not** receive normal ERP traffic, **must not** surface raw SQL errors as the gate, and **must not** be substituted with another database.
10. `MigrationManagementMode` **must** be modelled separately from `HostingMode`.
11. The orchestrator **must not** execute DDL against a database whose `MigrationManagementMode` denies it; absent approval **must** be treated as denial.
12. Schema-health verification **must** apply to every physical tenant database; customer-hosted databases are **not** exempt.
13. A customer-executed migration **must** be verified against observed `__EFMigrationsHistory`, never accepted on assertion.
14. `ProvisioningStatus`, `ConnectivityStatus`, `SchemaCompatibilityStatus`, and `MigrationExecutionStatus` **must** remain separate dimensions; `Ready` **must** be derived, not independently writable.
15. Connectivity probes **must** be minimal, **must not** run business queries, and **must not** record or expose credential material.
16. Application runtime **must not** auto-migrate the tenant database estate; deployment tooling owns estate migration. Startup preflight **must** be read-only.
17. Traffic gating **must** follow the state table; **no** read-only compatibility mode exists in V1.
18. A stale *incompatible* status **must** continue to deny; only a stale *compatible* status may allow within the configured grace window, and never beyond the hard-stale bound.
19. Migration ownership **must** satisfy the eight lock invariants regardless of the primitive chosen.
20. `MigrationManagementMode` values **must not** reuse `HostingMode` vocabulary.
21. Customer-managed onboarding **must** satisfy the mandatory drift floor in addition to migration-history compatibility.
22. The environment matrix **must** exist before the first customer-managed onboarding, and the required isolation/RCSI setting **must** be declared, set, and verified rather than inherited.
23. Azure SQL **must not** be treated as a supported Tenant ERP target without an ADR amendment.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Partial estate migration leaves mixed versions | Per-database status, retry/resume, and estate deployment report; traffic gating on incompatible databases |
| Concurrent migration of one database corrupts history | Mandatory single-writer ownership per physical database |
| Cached status is stale and traffic is misrouted | Conservative treatment of stale/unknown status; re-check triggers; post-migration verification |
| Ahead-of-application database served by an old instance | Explicit `AheadOfApplication` state with gating |
| Full drift goes undetected | Accepted for V1 for the platform-managed estate; strongly recommended before customer-managed databases go to production, where DBA changes make drift materially likelier |
| Orchestrator applies DDL to a customer-owned database without authority | `MigrationManagementMode` is mandatory and consulted before any DDL; absent approval means denial |
| Customer DBA reports a migration as applied when it was not | Verification reads observed `__EFMigrationsHistory`; assertions are never accepted |
| Connectivity failure mis-reported as a schema failure | Separate `ConnectivityStatus` dimension and a dedicated connectivity health service |
| Customer database stalls estate convergence indefinitely | Blocked-pending-customer-action is a distinct report category; traffic gating protects correctness while coordination proceeds |
| Connectivity diagnostics leak credentials into logs or UI | Probe failure detail is classified, never verbatim; secret display prohibited (`ADR-021`) |
| Background health-checker outage denies traffic to a healthy estate | Asymmetric staleness: stale-compatible allows within a grace window, stale-incompatible always denies |
| Runtime auto-migration races across serving instances | Deployment tooling owns estate migration; startup preflight is read-only |
| Customer database is onboarded with undetected manual schema changes | Mandatory drift floor plus stored schema fingerprint before first onboarding |
| Customer database silently runs the opposite isolation regime from every tested database | Required setting declared by the matrix, set on provisioning, verified at onboarding and on cadence |
| Azure SQL assumed compatible and surfaces differences during cutover | Declared out of scope for V1; amendment required |
| Migration lock released before verification lets a writer slip in | Ownership held through post-verification as a binding invariant |

---

# Future Considerations

Revisit if: automated schema-drift detection becomes an operational requirement; blue/green or rolling deployments require simultaneous support of two adjacent schema versions; the estate grows beyond what a single orchestration run can complete inside a deployment window; or zero-downtime migration of large tenant databases becomes necessary.

---

# Related Documents

- `ADR-011` — Unit of Work
- `ADR-017` — Tenant Storage Topology and Routing
- `ADR-019` — Dynamic Tenant Placement Policy
- `ADR-020` — Shared-to-Dedicated Tenant Migration and Cutover
- `ADR-021` — Customer-Managed Tenant Database Connectivity and Operations

---

# Review Criteria

This ADR should be reviewed if:

- Deployment strategy changes to require multi-version schema support.
- The estate size makes synchronous orchestration impractical.
- Drift detection is promoted from optional to mandatory.
- Customer-managed databases enter production use, at which point drift detection and the refined connectivity states should be reassessed.
- Migration package generation for customer-DBA execution becomes a requirement.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | 2026-08-13 | Solution Architecture Team | Initial version — tenant schema health and migration orchestration |
| 1.1 | 2026-08-13 | Solution Architecture Team | Added `MigrationManagementMode`, customer-managed migration models and authority, separated connectivity/schema/provisioning status dimensions, connectivity health service, and `TenantDatabaseUnavailable` behaviour |
| 1.2 | 2026-08-13 | Solution Architecture Team | Review hardening: replaced the combined status enum with four orthogonal dimensions; added the traffic-gating table and health-cache freshness model; assigned estate migration to deployment tooling; added the eight migration-lock invariants; renamed migration modes to `AutomaticByPlatform`/`PlatformAfterApproval`/`CustomerDba`; added the mandatory customer-managed drift floor and schema fingerprint; required the environment matrix; declared Azure SQL out of scope for V1; clarified the RCSI/isolation policy |
| 1.3 | 2026-08-14 | Solution Architecture Team | Added operational recovery readiness as a dimension distinct from schema compatibility; a successful migration does not imply a recoverable database. Deferred to `TS-Backup` |
| 1.4 | 2026-08-14 | Solution Architecture Team | Documented the explicit two-stream (Platform / Tenant) migration deployment procedure, including per-stream commands, shared-catalog and dedicated behaviour, ordering-tolerance scope, and pre-traffic validation. No architecture decision changed |
| 1.5 | 2026-08-14 | Solution Architecture Team | Recorded that schema health and migration orchestration are implemented: reclassified the manual two-stream commands as development/break-glass, and named the orchestrator as the normal path for the platform-managed estate. No architecture decision changed |
| 1.6 | 2026-08-14 | Solution Architecture Team | Documented one-writer-per-dimension: connectivity and schema observations have independent writers, a check that observes nothing about a dimension writes nothing to it, LastSchemaCheckUtc advances only on an actual schema observation, and concurrent writers reapply only their own dimension. No architecture decision changed |
| 1.7 | 2026-08-14 | Solution Architecture Team | Recorded that `ADR-022` owns recovery readiness as an independent fourth dimension; backup fields are not added to the schema-health model. No decision changed |
