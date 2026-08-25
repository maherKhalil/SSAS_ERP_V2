---
id: ADR-022
title: Tenant Database Backup and Recovery Orchestration
category: Architecture Decision Record
version: 1.3
status: Accepted
date: 2026-08-15
owner: Solution Architecture Team
tags:
  - architecture
  - multi-tenancy
  - persistence
  - operations
  - durability
  - security
depends_on:
  - ADR-013
  - ADR-017
  - ADR-018
  - ADR-020
  - ADR-021
used_by:
  - Platform
---

# ADR-022: Tenant Database Backup and Recovery Orchestration

---

# Status

**Accepted** — 2026-08-25.

Owns the decisions that make a physical tenant database **recoverable**, and the orchestration that keeps it so. `ADR-017` establishes the topology and names `TS-Backup` as a required capability of the platform-managed estate; `ADR-018` establishes the health and migration model this ADR sits beside without merging into; `ADR-020` defines the cutover this ADR gates; `ADR-021` defines whose data — and therefore whose backups — a customer-managed database holds.

**Implementation scope: architecture decided, implementation phased.** Phases A, B and C are delivered — the backup domain, recovery-readiness dimension and run history; single-database SQL Server full, differential and transaction-log execution with mandatory in-flight detection and reconciled provider evidence; and fleet scheduling with due evaluation, keyset discovery, bounded concurrency and multi-instance duplicate-safe orchestration. **Phase D's architecture is defined by v1.2 of this ADR and its implementation has not begun.** Phase E remains future. The sequencing constraint from `ADR-017` is binding and is the reason this ADR exists now rather than later: **`TS-Backup` must exist before dedicated provisioning and cutover become production-capable**, because a dedicated database with no verified backup chain is a data-loss position the shared database did not have.

**Evidence:** Phases A, B and C ship — `SqlServerTenantDatabaseBackupProvider`, `SqlServerBackupEvidence`, `SqlServerBackupVisibility` and the restore-verification providers in `SSAS.Platform.Infrastructure/TenantStorage/` — under four architecture suites (`TenantBackupFoundationArchitectureTests`, `TenantBackupProviderArchitectureTests`, `TenantBackupSchedulerArchitectureTests`, `TenantRestoreVerificationArchitectureTests`) and eight SQL Server integration suites. The §14 session-loss gate this ADR set for itself is **closed** (v1.1), and `ADR-020` gates cutover on the readiness dimension defined here.

**Acceptance applies to the decisions, not to completion of the phasing.** Phase D's implementation has not begun and Phase E is future; the architecture for both is decided here, which is what an accepted ADR asserts. This ADR states no acceptance precondition of its own, so **acceptance is inferred from that use and is named as an inference** (`DEC-L-020`).

**`ADR-021` does not follow it into `Accepted`** and is cited above as authoritative. That citation is a boundary rather than a dependency: `ADR-021` defines what this ADR **excludes** — the platform performs no backup and no restore verification against a customer-managed database — and the excluded case is precisely the one `ADR-021` has not been accepted for.

---

# Context

`ADR-017` places tenant ERP data in physical databases: one or more `PlatformManaged` + `Shared` databases holding many tenants, optional `PlatformManaged` + `Dedicated` databases holding one, and optional `CustomerManaged` + `Dedicated` databases on a customer's own server. Routing resolves a tenant to exactly one physical database through `TenantDatabase` and `TenantDatabaseAssignment`.

`ADR-018` added operational health to the physical database row and — following the `TS-Health-Split` refinement in its v1.6 — established a discipline this ADR inherits wholesale: **each health dimension has exactly one writer, and a check that observes nothing about a dimension writes nothing to that dimension.** Connectivity, schema compatibility and migration execution are independent, never merged, and never summarised into a single settable flag.

What no decision covers today is **durability**. The product can currently tell an operator whether a tenant database is reachable and whether its schema matches the deployed application. It cannot tell them whether that database could be recovered if it were lost, when it was last provably recoverable, or whether promoting a tenant to dedicated storage would move it from a protected position to an unprotected one.

The current estate is a single shared database, whose durability is whatever the deployment's existing database operations provide. That is survivable precisely because it is one database that somebody is already looking after. It stops being survivable at the first dedicated database, which is a new physical artifact nobody has arranged protection for, created by an automated workflow, at the exact moment a tenant's authoritative copy moves onto it.

---

# Problem Statement

Without these decisions:

- A dedicated database could be provisioned, migrated, verified schema-healthy, and cut over — with every health signal green — while having no backup at all. The operation would report success and silently reduce durability.
- Backup arrangements would be assumed rather than recorded, and the assumption would be discovered during an incident.
- "Restore this tenant to yesterday" would be requested on shared storage, where honouring it would roll back every other tenant in that database, and nobody would have written down that this is not what a database restore does.
- Backup would be attempted with the ERP runtime credential, granting the request-serving identity the ability to read the entire database to a file.
- Two platform instances could issue overlapping backups of the same physical database, producing chains whose continuity nobody can reason about.
- A backup chain could be created and never once exercised, so its first restore would be attempted under incident conditions.
- Retention could delete a full backup that retained differentials and logs depend on, destroying recoverability while appearing to tidy up.
- Backup ownership for customer-hosted databases would be inferred from hosting mode, and the platform would either run backups it has no authority to run or assume a customer has arranged something they have not.
- Recovery state would be folded into `SchemaCompatibilityStatus` or a derived `Ready`, making a migrated database look recoverable.

---

# Decision

## 1. Backup and recovery attach to the physical database

Backup policy, execution history and recovery readiness belong to the **physical `TenantDatabase`** — never to a `Tenant`, and never to a `TenantDatabaseAssignment`. This follows from the topology rather than from preference:

| Placement | Backup chain | Recovery readiness |
|---|---|---|
| `PlatformManaged` + `Shared` | **One** chain covering the database, and therefore every tenant in it | One state for the database |
| `PlatformManaged` + `Dedicated` | **Its own independent** chain | Its own state |
| `CustomerManaged` + `Dedicated` | The customer's chain, on the customer's server | Externally owned — see §12 |

A shared database hosting a thousand tenants is **one** backup target, scheduled once, with one chain and one readiness state. Modelling policy per assignment would create rows that can disagree with each other about the same physical database.

## 2. Recovery readiness is a separate dimension

Recovery readiness is a **fourth operational dimension**, independent of the three `ADR-018` defines. It is never merged into `SchemaCompatibilityStatus`, never merged into `ConnectivityStatus`, and never collapsed into a general `IsHealthy` or `IsReady`.

The combinations this separation must express are ordinary, not exotic:

- **schema-compatible but not recovery-ready** — a freshly migrated dedicated database with no baseline backup;
- **recovery-ready but schema-incompatible** — a well-protected database awaiting a release;
- **backup-degraded while serving normally** — a late log backup on a healthy database.

A single overloaded status cannot express any of those truthfully, and an operator shown one would have to guess which problem they have.

## 3. One writer per dimension

The recovery-readiness dimension has **its own writer**, following the discipline established in `ADR-018` v1.6:

- it writes recovery-readiness state and its observation timestamps, and nothing else;
- it never writes connectivity, schema or migration state;
- **an evaluation that observes nothing writes nothing** — a backup check that cannot reach a database records no recovery verdict rather than recording an incorrect one;
- on an optimistic-concurrency conflict it **re-reads the latest row and reapplies only its own dimension's observation**, bounded, never replaying a stale whole aggregate.

This matters more here than it did for two dimensions. Recovery readiness becomes the **third independent writer** on `TenantDatabase`, and cross-dimension clobbering that was survivable with two writers becomes progressively harder to detect with three.

*Implementation guidance, not an architecture decision:* the existing re-read/reapply pattern is currently validated by outcome — concurrent writers demonstrably preserve both dimensions — but the conflict branch itself is not deterministically forced by a test. Deterministic coverage of that branch should be added **before or during** the first backup domain slice, proving that a losing writer retries, re-reads, reapplies only its own dimension, and leaves the winner's other-dimension state intact.

## 4. Policy is a separate entity from observed state

Backup **policy** is modelled as its own entity — conceptually `TenantDatabaseBackupPolicy` — bound to one physical `TenantDatabase`, rather than as a column set on `TenantDatabase` itself.

The reasoning is growth and separation. Policy will accumulate three schedules, retention expectations, a destination reference, verification cadence, maximum tolerable backup age, and later service tiers and provider-specific settings. `TenantDatabase` is already carrying routing metadata plus three health dimensions and is read on the routing path; a dozen policy columns would bloat the row every tenant request touches.

More importantly, three things must stay distinguishable:

| Concept | Answers |
|---|---|
| **Policy** | What protection *should* exist |
| **Run history** | What actually happened |
| **Recovery readiness** | Whether current evidence *meets* the policy |

**Protection is never inferred from `Enabled = true`.** A policy that has never executed protects nothing.

## 5. Backup authority is its own dimension

Who may execute backups is recorded per physical database as **`BackupManagementMode`**, with its own vocabulary:

- **`AutomaticByPlatform`** — the platform schedules, executes, monitors and verifies backups.
- **`PlatformAfterApproval`** — the platform may execute, but only under an explicit per-run approval; absence of approval is **denial**, never default-allow.
- **`CustomerDba`** — the platform **never** executes backups. It records the arrangement and, where a verification mechanism exists, verifies evidence.

This is deliberately **not** `MigrationManagementMode`, and the values are deliberately not inferred from `HostingMode`, `StorageMode`, or each other. `ADR-018` already learned this lesson once: reusing hosting vocabulary for migration authority made "customer-hosted database we are permitted to migrate" read as a contradiction. The same trap exists here, and the combinations are real — a platform-hosted database whose regulated customer requires their own DBA to run backups is an ordinary configuration, as is a customer-hosted database whose customer is happy for us to protect it.

**Defaults:** `PlatformManaged` — shared or dedicated — defaults to `AutomaticByPlatform`. The platform owns durability for infrastructure it operates, and dedicated placement does not transfer that ownership to the customer. `CustomerManaged` defaults to `CustomerDba` per `ADR-021`.

## 6. Recovery readiness states

A closed, actionable set:

| Status | Meaning |
|---|---|
| `Unknown` | Never evaluated, or evaluation could not complete. Pre-verification. |
| `Protected` | Current evidence satisfies the configured policy. |
| `Degraded` | Protected but slipping — a backup type is overdue, or verification is aging toward its limit. |
| `Unprotected` | No usable recovery position: no baseline, or a known-broken chain. |
| `RecoveryModelInvalid` | The database's recovery model cannot support the policy's requirements. |
| `VerificationOverdue` | Backups exist but have not been verified within the policy's interval. |

`Protected` requires **evidence**, not configuration. For SQL Server that means: policy enabled; recovery model appropriate to what the policy requires; a recent successful full baseline; differential and log positions current where the policy requires point-in-time recovery; backup age inside policy bounds; restore verification not overdue; and no known chain break.

**`Protected` is never "the last backup command returned success."**

### What `Protected` claims to an operator *(v1.2)*

Stated plainly, because the distinction becomes load-bearing below and is easy to read past:

**`Protected` means every recovery obligation required by the active platform backup policy is currently satisfied.**

**`Protected` does not, by itself, mean the database has undergone an actual restore verification.** Where the policy requires periodic restore verification, that verification is one of the obligations and `Protected` therefore implies it. Where the policy requires none, it is not an obligation, and `Protected` is reached without it.

**Restore-provenness remains separately observable** — through the last successful restore-verification timestamp, the restore-verified state on the backup set, and verification history. An operator asking "has this database ever actually been restored?" reads that evidence, not the readiness status. The two questions are different, and the model answers both rather than collapsing them.

No additional readiness state is introduced for this. The separate verification evidence already carries the distinction, and a `RestoreProven` status would duplicate it while making the closed readiness set harder to read.

### `VerificationOverdue` — exact semantics *(v1.2)*

`VerificationOverdue` applies when, and only when, **both** hold:

- the policy's restore-verification interval **is set** — periodic actual restore verification is required; **and**
- **either** no successful actual restore verification exists, **or** the current UTC time exceeds the last successful actual restore verification plus that interval.

**A failed verification is not an overdue one.** An attempt that ran and failed is evidence of a recovery problem, not of age. It degrades readiness according to how deep the failure reached, under the mapping in §17 "What a failed verification means for readiness" — which also distinguishes an attempt that *could not begin* because verification infrastructure was unavailable, and which is not evidence about recoverability at all. Labelling a failure `VerificationOverdue` would describe a broken chain as a late one and point the operator at the schedule instead of at the failure.

### Where the policy does not require periodic verification *(v1.2)*

**Where the restore-verification interval is unset, periodic actual restore verification is not required by that policy.** Verification therefore cannot become overdue merely because no restore has happened, and such a database **may** reach `Protected` on its remaining evidence without ever having undergone an actual restore.

This is deliberate, and it is not a weakening. §17 already records that an unset interval is a legitimate configuration for a shared database whose chain is exercised continuously by other means. Treating those databases as permanently overdue would leave the fleet's most heavily used chains reporting amber indefinitely, which erodes the signal precisely where it needs to be readable.

**It does not relax the cutover gate, which is a separate and stricter test.** First production activation of a dedicated target requires at least one successful actual restore verification *in addition to* `Protected` (§18). `ADR-020` lists those as separate prerequisites for the same reason: if `Protected` already implied a real restore, the requirement would be redundant, and it is not. **Periodic readiness and first-cutover evidence are two gates, and neither substitutes for the other.**

## 7. Backup health does not gate ERP traffic

Normal tenant ERP traffic is **not** gated on recovery readiness. `ADR-018`'s traffic gate continues to be driven by connectivity, schema compatibility and migration execution alone.

The reasoning is that a durability problem and an availability problem are different problems with different correct responses. A late log backup means the recovery position has degraded; the data is intact and correct, and the application can read and write it perfectly well. Denying traffic would convert a durability concern into a customer-visible outage — strictly worse, and it would make operators reluctant to report degraded protection.

Any future exception requires an explicit amendment naming the condition.

## 8. Recovery readiness gates lifecycle operations

Where recovery readiness **is** binding is the set of operations whose safety depends on it:

- **Dedicated production activation** — a dedicated database may not begin serving traffic unprotected;
- **Shared → Dedicated cutover** (`ADR-020`);
- future placement promotion and any operation that moves a tenant's authoritative copy.

These **fail closed**. An unknown recovery position is treated as unprotected, because at these moments an unverified assumption is exactly what the gate exists to catch.

## 9. SQL Server semantics

SQL Server remains the **only supported V1 runtime provider** (`ADR-017`). The managed chain is:

**Full baseline + Differential + Transaction Log**, where the policy requires point-in-time recovery.

Binding statements:

- **Transaction-log backups alone are not a backup strategy.** They restore only onto a full baseline.
- **A differential depends on its full baseline.** A chain whose base has been lost or superseded is not restorable as intended.
- **Log recovery requires an appropriate recovery model** and an unbroken chain.

Indicative default schedules — full weekly, differential daily, log every fifteen minutes — are **policy values, not architectural constants**, and higher log frequency is expected where the recovery-point requirement is tighter.

**`BULK_LOGGED` is a valid recovery model for managed backup-chain verification** *(v1.2)*. It supports the log chain, so a policy requiring log backups is not invalid on a bulk-logged database. The caveat is narrow and worth stating once: intervals containing minimally logged operations restrict exact point-in-time recovery semantics within those intervals. **Phase D verifies the selected managed recovery sequence and claims no arbitrary customer-selected point-in-time recovery through such an interval** — consistent with §19, where Level C establishes a chain capability rather than a recovery product.

**Recovery model: detect, report, and degrade readiness — never change automatically.** Switching a database to `FULL` starts transaction-log growth that will fill a disk unless log backups are already running; performing that switch automatically, on a database that by definition is misconfigured, risks converting a durability gap into an outage. Changing recovery model is an explicit administrative act.

**SQL Server options.** `CHECKSUM` on by default. `COMPRESSION` configurable, defaulting on where the edition supports it. **Where the deployed edition does not support backup compression, the provider takes an approved uncompressed backup rather than failing the run** — an unavailable capability is not a policy violation, and classifying it as one would report a healthy database unprotected. The exception is a policy that explicitly marks compression **mandatory**, which is then a genuine policy failure. Capability discovery beyond that is a Phase B implementation concern, not an architectural one. **`COPY_ONLY` is not used for the managed full chain** — a copy-only full does not reset the differential base, so a chain built on one silently produces differentials anchored to an older full. Copy-only remains available as an explicit break-glass capability for ad-hoc backups taken outside the managed chain.

**Backup encryption** is a policy capability. V1 relies on approved storage-level encryption at rest rather than requiring SQL Server native certificate-based backup encryption, and **encryption keys and certificates are never stored in the Platform database**. Native provider encryption remains an extension point.

**Chain metadata.** Enough is recorded to reason about continuity: the provider backup-set identity, `first_lsn`, `last_lsn`, `database_backup_lsn`, and the backup-set GUID where available. Not every `msdb` column.

**Database state** is classified before commands are issued. `ONLINE` is backable; `RESTORING`, `RECOVERY_PENDING`, `SUSPECT` and `OFFLINE` are not, and `READ_ONLY` has its own considerations. The provider classifies and reports rather than issuing commands blindly.

## 10. Provider boundary

Backup operations are reached through a provider abstraction — conceptually `ITenantDatabaseBackupProvider` — with a SQL Server implementation.

**`Full`, `Differential` and `TransactionLog` are SQL Server vocabulary, not universal domain concepts.** They are owned by the SQL Server provider, and a future provider registers its own operation vocabulary rather than being forced into SQL Server's. Oracle would express its chain through RMAN and archived redo; PostgreSQL through WAL and physical or logical mechanisms. Inventing a lossy universal enum now would encode SQL Server's model as the architecture and make the first non-SQL-Server provider a rewrite.

The Application layer never constructs backup SQL. Command generation, quoting and provider metadata extraction are Infrastructure concerns.

`TS-Provider` remains **Deferred**; no Oracle or PostgreSQL runtime is planned.

## 11. Credentials, destinations and secrets

**The ERP runtime credential must not hold backup privileges.** `BACKUP DATABASE` reads the entire database to a file of the caller's choosing; granting that to the identity serving web requests widens the blast radius of any application-level compromise from "what the ERP can query" to "a complete copy of the database, anywhere the server can write".

Three authorities are conceptually distinct and should not be silently merged:

| Authority | Purpose |
|---|---|
| Runtime | Serve tenant ERP requests |
| Migration | Apply DDL (`ADR-018`) |
| Backup / recovery | Execute backups and restore verification |

The current development environment is **integrated-security-only**, so the backup identity in V1 must be expressible as a Windows/service identity rather than assuming a SQL username and password. No specific development identity is fixed by this ADR.

**Destinations** are referenced by a trusted `BackupDestinationKey` resolved from Infrastructure configuration. The Platform database stores the key, a safe artifact reference and provider identifiers — **never** a storage password, access key, SAS token, signed URL, or any credential-bearing path.

**The destination is selected exclusively through that trusted reference.** A caller-, tenant-, or user-supplied filesystem path, UNC path, URL, storage endpoint or credential-bearing reference **must never** determine where a backup is written. The key is the only input the request or Application layer contributes; resolution from the key to a physical location happens entirely in Infrastructure, against trusted configuration. This closes destination injection, which is the sharpest edge of the capability: `BACKUP DATABASE` writes a complete copy of the database to wherever it is told, so an attacker who can influence the destination has exfiltration without needing to read a single row through the application. The Infrastructure provider may additionally validate a resolved destination — existence, writability by the SQL Server service identity, capacity — but validation narrows an already-trusted destination and **must not** become a path by which an untrusted one is accepted.

**An operational constraint worth recording because it surprises people:** SQL Server writes backup files as the **SQL Server service identity**, not as the application process. A destination the application can write to is not necessarily one the server can, and vice versa. Destination validation must be expressed in terms of the server's access.

**Artifact naming** is deterministic and non-secret: physical database identity, timestamp, operation type and run identifier. Customer or company names are not required and should not be used.

## 12. Customer-managed databases

`ADR-021` is authoritative and unchanged: backup execution for a `CustomerManaged` database belongs to the **customer or their DBA** by default, because the platform owns neither the server nor the data's disposition, and creating and retaining backups is an act of custody rather than a technical convenience.

Consequences here:

- The platform **does not attempt** backup execution against a customer-managed database — there is no supported runtime connectivity path to one (`ADR-021`), and inventing one for backups would contradict that.
- Recovery readiness is reported as `Unknown` or an explicit externally-managed state. It is **never** reported `Protected` on the strength of a customer's assertion. This mirrors `ADR-018`'s existing rule that customer-executed migrations are verified against observed history rather than accepted on assertion.
- Cutover to an endpoint whose recovery position is simply unknown is not acceptable either (`ADR-020`); the obligation is to **confirm** the arrangement, not to execute it.

- **The platform performs no actual restore verification against a customer-managed database** *(v1.2)*. Restore ownership for those databases rests with the customer's DBA (`ADR-021` §16), and the platform has no supported connectivity path to execute one. Where platform verification cannot be performed, recovery readiness stays `Unknown` — never `Protected` on the strength of an assertion.

Delegated backup — where a customer explicitly contracts the platform to protect their database — requires the connectivity and credential model `ADR-021` defers, and is not enabled by this ADR.

## 13. Scheduling and fleet orchestration

Scheduling is owned by the **platform orchestration layer** — a background service evaluating policy across the physical database registry — not by SQL Agent.

SQL Agent is per-server: it does not exist on a customer-managed endpoint, it fragments ownership across servers, and it cannot produce a fleet-wide view of which databases are due or protected. It may remain a deployment-level implementation detail; it is not the domain authority.

Scale rules mirror `ADR-018`'s migration orchestration:

- discovery is over **physical `TenantDatabase`**, never over assignments;
- **keyset paging**, not `OFFSET`;
- a shared database is evaluated and backed up **once**, regardless of tenant count;
- no timer per tenant, and bounded execution concurrency.

### Operation precedence when several are due

A physical database may have full, differential and transaction-log backups all due at the same moment. **One operation is dispatched per physical database per sweep**, and the precedence is:

1. **Transaction log**
2. **Full**
3. **Differential**

The remaining operations are reconsidered on a later sweep, against the successful-backup timestamps the completed operation has by then persisted.

**Log first**, because it is the operation that protects the *recovery point*: delaying it directly widens the window of data that cannot be recovered, and a log backup is normally materially less expensive than a full of the same database. It should not queue behind a long full operation.

**Full before differential**, because a full establishes and resets the differential base. A differential taken immediately before a due full is usually work that the full then makes redundant, and once the full succeeds the differential's due time is recomputed from the new baseline.

**This is a scheduling precedence, not a chain-validity rule.** The two are distinct and must stay distinct: SQL Server chain validity is established from provider evidence (§14), while whether the *platform's* schedule has been satisfied is established from the platform's own successful-run timestamps. An externally taken backup may legitimately change SQL Server chain state — an external full resets the differential base — without discharging the platform's scheduling obligation, and **must not** be treated as satisfying it.

## 14. Multi-instance execution ownership

Two platform instances must not run overlapping managed backups of the same physical database.

The **planned** mechanism is the one `ADR-018` validated for migrations: a SQL Server application lock (`sp_getapplock`) at **Session** scope, taken **in the target database**, on the connection issuing the operation. Session scope is the attractive property — SQL Server releases the lock when the session ends, so a crashed holder leaves neither a stuck lock nor a permanently blocked resource, with no lease row, reaper or wall-clock assumption.

**This mechanism is adopted as the intended V1 approach, and its sufficiency for backups is not yet established.** See "Ownership is not the same as server-side operation state" below; the difference between migrations and backups is duration, and duration is exactly what this mechanism's failure mode is sensitive to.

**The resource namespace is distinct from migration's.** Backup and migration locks must not collide by accident or be released by the wrong operation:

```
TenantStorage:Migration:<physical database identity>
TenantStorage:Backup:<physical database identity>
```

**These namespaces are deliberately non-contending.** Two distinct `sp_getapplock` resources do not exclude each other, and that is the intended behaviour, not an oversight. The two locks solve two different problems: migration ownership prevents a second migration of the same database, and backup ownership prevents a second platform-managed backup operation of the same database. Neither is intended to exclude the other, and **no cross-lock arrangement is required or permitted** — no shared coordination resource, no dual acquisition, no migration taking the backup lock, no backup taking the migration lock. A reader who expects these namespaces to serialise migration against backup has misread their purpose.

**Granularity: one backup lock per physical database**, not per backup type. Serialising the managed chain per database is the conservative choice: full, differential and log operations participate in one chain whose continuity is easier to reason about when only one of them can be in flight. Splitting by type is a possible future optimisation once there is evidence it is needed and safe.

### Migration and backup may coexist

**Decision: all managed SQL Server backup types — full, differential and transaction log — may run while a tenant schema migration is in progress, and a migration may begin while any backup type is running.** There is no architectural exclusion between them.

The reasoning is that the feared failure does not exist:

- **SQL Server `BACKUP DATABASE` is an online operation** that produces a transactionally consistent recoverable database image. It captures data pages together with enough transaction log to bring the restored database to a consistent point; it does not photograph whatever half-written state happens to be on disk.
- **EF Core executes SQL Server migration work transactionally according to provider and migration semantics.** A backup overlapping a multi-migration orchestration run may therefore represent a legitimate **intermediate committed migration state** — some migrations applied, others not yet.
- **That state is not corrupt, and it is already a state the architecture names.** On restore, `ADR-018` schema health classifies the database from its actual `tenant.__EFMigrationsHistory` — `PendingMigrations`, `UpToDate`, or another supported compatibility state — and the migration orchestrator can subsequently advance a valid behind database exactly as it would advance any other. A restored database that is behind is a database to migrate, not a database to distrust.

**Therefore overlap with a schema migration is not a reason to declare a backup invalid.** An earlier draft of this ADR asserted that a full or differential taken mid-migration "captures a half-applied schema"; that claim was wrong and has been withdrawn.

**Deliberate limit on the transactionality claim.** This ADR does **not** assert that every possible EF Core migration is a single atomic transaction — that guarantee depends on provider behaviour and on what individual migrations contain, and the architecture must not rest on it. The precise and sufficient statement is narrower: *SQL Server backup yields a transactionally consistent database restore point, and any committed intermediate migration state it contains is evaluated through `ADR-018`'s migration-history compatibility model after restore.* Correctness comes from the backup's consistency plus post-restore classification, not from an assumed migration atomicity.

**Log backups in particular must keep running during a migration.** A migration is when the transaction log grows fastest, which makes it precisely the wrong moment to suspend log backups: doing so risks both uncontrolled log growth and a widening recovery-point gap.

**I/O contention is real, and it is an operational concern rather than a correctness one.** Migration and backup compete for I/O, CPU, storage bandwidth and transaction-log activity, and a large full or differential overlapping a fleet migration window will slow both. A scheduler **may** therefore prefer to avoid scheduling large full and differential backups inside planned fleet migration windows. This is capacity management and belongs to scheduling policy — it is **not** a data-correctness invariant, and it must not be implemented as a hard architectural exclusion or enforced through locking.

### Ownership is not the same as server-side operation state

`sp_getapplock` protects **platform orchestrator ownership**: it establishes which platform worker is entitled to run a managed backup of a given physical database. It does **not**, by itself, prove that no server-side `BACKUP` operation exists — and after a client session is lost, those two facts can diverge.

The potential window:

1. Worker A acquires the session-scoped backup applock in the target database.
2. Worker A issues `BACKUP DATABASE`.
3. Worker A's client connection or session dies.
4. SQL Server releases the applock with the session.
5. The server-side `BACKUP` may still be aborting or winding down.
6. Worker B acquires the now-free backup applock.
7. Worker B starts a second `BACKUP` while A's operation is still active on the server.

Concurrent `BACKUP` commands against one database do not block each other in SQL Server, so this would not surface as an error. Even where it is harmless to database correctness, it produces duplicate work, ambiguous run ownership, contention for the same I/O, and potential confusion in chain and reporting metadata — for example ambiguity about which full anchors subsequent differentials.

**Session-scoped ownership is proven for migrations, which are short; backups are long-running**, and the assumption that a dead client promptly stops a server-side `BACKUP` was inherited rather than demonstrated. v1.0 recorded that window as open and made closing it a binding Phase B requirement. **Phase B has now performed that validation; the observed result is recorded below.**

**Empirical validation performed (Phase B).** The experiment ran against a disposable SQL Server database — never a production or tenant database — with a real `BACKUP DATABASE` large enough to observe, ownership held on the backup session applock, and the client terminated by **abrupt termination of the client process**. That is the scenario this section is about: a platform worker crashing. A server-initiated `KILL` of the session answers a different and weaker question and was deliberately not used as the decisive case. An independent observer on a separate session then polled `sys.dm_exec_requests` for a surviving `BACKUP` request against the target database while repeatedly attempting to acquire the same backup applock.

**Observed result — three conclusive trials.** In each trial the backup was confirmed in flight before the client was killed; a trial in which the backup completed first is inconclusive, not a pass.

| Trial | `BACKUP` request gone | Ownership reacquired |
|---|---|---|
| 1 | 849 ms | 859 ms |
| 2 | 838 ms | 839 ms |
| 3 | 847 ms | 848 ms |

In all three trials, **at the moment another worker acquired the backup applock, no active `BACKUP` request for the target database remained**. Condition A below was therefore observed.

**This is an observation, not a portable guarantee.** The ADR does **not** conclude that SQL Server always aborts a `BACKUP` before releasing the session applock. The observed margins were 10 ms, 1 ms and 1 ms — at the granularity of the observation itself rather than a designed safety margin — measured on one host, at one backup size, on one storage profile. Nothing in this evidence speaks to slower storage, higher load, or a database large enough that aborting takes real work. **Session-scoped ownership alone is therefore not accepted as the sole production defence against duplicate operations.**

**In-flight backup detection — mandatory, not conditional.** v1.0 framed this guard as required only if validation revealed an overlap window. That framing is withdrawn. The guard is **mandatory V1 behaviour** for two independent reasons:

- the measured margin is too small to treat as a safety property, as above; and
- **the applock cannot see backups that never took it.** A DBA, a SQL Agent job or a third-party backup tool acquires no platform lock at all. Ownership coordination is structurally blind to them; only inspection of live server state is not.

Conceptually the guard inspects `sys.dm_exec_requests` and provider-supported request metadata; this ADR deliberately does not bind the implementation to one brittle query.

**Server visibility is a precondition of trusting the guard.** Phase B established a SQL Server behaviour that makes the obvious implementation unsafe: **without sufficient server-level visibility, `sys.dm_exec_requests` does not raise an error — it silently narrows to the caller's own session.** An empty result is then indistinguishable from "nothing is running", and a missing permission becomes a green light.

Therefore, before any platform-managed backup starts, the provider **must**:

1. **establish** that its identity holds sufficient server visibility to observe other sessions;
2. **inspect** for an active backup request against the target physical database;
3. **fail closed** if visibility cannot be established — the backup **must not** start, and the outcome is a controlled failure naming the missing prerequisite, distinct from having observed an in-flight operation;
4. **not start** a second backup if another is active.

On **SQL Server 2022**, the granular permission that grants this visibility is **`VIEW SERVER PERFORMANCE STATE`**. The broader `VIEW SERVER STATE` is **not** required, and `sysadmin` **must not** be used to satisfy it. The permission is granted by deployment; no platform code grants or escalates it.

**The in-flight safety check is not an operational feature flag.** It is a correctness precondition, and neither fleet configuration, scheduler configuration nor deployment configuration may disable it. This matters most in Phase C, where backups run unattended across the estate.

**Guard semantics.** If an existing `BACKUP` operation against that physical database is detected, the worker **must not** start another platform-managed backup. The outcome is a **controlled non-execution**; Phase B settled the vocabulary as `SkippedInFlightOperation`, alongside the run statuses in §15. **It must not be recorded as a backup failure**: nothing failed, and reporting it as failure would degrade recovery readiness on the strength of successful coordination.

**Phase B exit gate — CLOSED.** The gate required one of the following to be proven:

- **A.** Session loss terminates or aborts the server-side `BACKUP` sufficiently promptly that a subsequent ownership acquisition cannot result in overlapping platform-managed backup work; **or**
- **B.** The provider implements an in-flight backup detection and reconciliation guard that prevents duplicate execution after ownership loss.

**Phase B satisfies both.** Condition A was observed in three controlled local trials, with the caveats recorded above; condition B is implemented and retained as mandatory rather than as a fallback. The gate is closed, and **Phase C is no longer blocked by it**.

The reason both were required in practice rather than either: fleet scheduling multiplies single-database ownership ambiguity across every database in the estate, so a window that is rare for one database becomes routine at fleet scale. A 1 ms observed margin is not a basis on which to remove the guard before multiplying the exposure.

**Session loss is loss of ownership.** If the connection carrying the lock dies, ownership is gone. A worker **must never** record `Succeeded` after losing session ownership merely because it previously issued a `BACKUP` command.

**Success requires post-operation evidence, not command submission.** A completed `ExecuteNonQuery`, or a command having been submitted, is not evidence that a usable backup exists. The Phase B provider establishes success by reconciling with SQL Server backup metadata and expected artifact evidence after the operation completes — potentially the `msdb` backup set, the provider backup-set identity, and the chain metadata of §9. The exact reconciliation mechanism is a Phase B implementation concern; that reconciliation is **required** is an architectural one.

## 15. Run history

Backup execution history is **persisted in the Platform database** — conceptually `TenantDatabaseBackupRun`, one record per provider operation.

Unlike migration, where a per-run summary suffices, backup history is required to answer questions no other state can:

- when was the last successful full, differential and log backup?
- is the current chain continuous, and what does the latest differential depend on?
- what failed, when, and with what classification?
- which artifacts have been verified, and when?
- is retention about to remove something the chain still needs?

Recorded per run: physical database, operation type, start and completion, status, safe destination reference, provider backup identity, size where available, provider chain metadata, a **safe error summary**, and verification state.

Run statuses, as established in Phase A: `Pending`, `Running`, `Succeeded`, `Failed`, `SkippedOwnershipHeld`, `SkippedInFlightOperation`, `BlockedByPolicy`, `VerificationFailed`. These describe **execution**; they are not recovery readiness, which is derived from the accumulated evidence. The two skip statuses are **not failures** — they record that coordination worked — and Phase B settled the in-flight case as `SkippedInFlightOperation` (§14).

**`Succeeded` requires post-operation evidence.** A run is recorded successful on reconciled provider evidence that the backup set exists and is what was expected, never on the strength of a command having been submitted or a `ExecuteNonQuery` having returned. This is the run-history expression of the ownership rule in §14: a worker that lost its session lost the right to claim success regardless of what it issued.

**No blind retry.** Transient conditions — connection establishment, lock contention, transient storage errors — may be retried, bounded. A deterministic provider or command failure is recorded, not retried, for the same reason `ADR-018` gives for migrations: retrying a deterministic failure spins instead of failing, turning a clear signal into a hang.

Run history is itself operational data with a finite useful life, and it accumulates fastest of anything in this model — a fifteen-minute log cadence across a growing fleet is the dominant row source. **Backup-run metadata therefore requires a bounded retention or archival policy.** No specific duration is fixed by this ADR: the operative constraint is that history must remain long enough to answer the chain-continuity and verification questions above, which is a policy value rather than an architectural constant.

## 16. Retention — the platform does not delete in V1

**Decision: in V1 the platform does not physically delete backup artifacts.** Physical expiry is delegated to the trusted destination's storage lifecycle configuration. The platform records policy, retention expectation and observed artifact metadata, and can report artifacts outside expectation.

This is deliberately conservative. Chain-aware deletion is the single easiest way to destroy recoverability while believing you are tidying up: removing a full that retained differentials and logs depend on leaves a set of files that look like backups and restore nothing. Shipping backup creation and backup deletion in the same first version would double the blast radius of the capability least able to tolerate it, and deletion is the half that is trivially deferred — storage lifecycle policies already solve it adequately.

If platform-side deletion is added later it requires its own design and **must be chain-aware**. Naive "delete everything older than N days" logic is **prohibited**: retention must be expressed in terms of restore dependencies, not file age.

**Retention must outlast verification** *(v1.2)*. Restore verification can only exercise artifacts that still exist, so the retention expectation is expected to keep the artifacts a scheduled verification needs available long enough to be restored. Where the configured retention expectation is shorter than verification requires, that is **reported as configuration drift** — a database whose next verification will fail for a reason that has nothing to do with its backups. It is deliberately reported rather than enforced: physical expiry belongs to the storage lifecycle, so the platform observes the mismatch rather than pretending to control it, and hard rejection is not adopted here.

## 17. Restore verification

**A successful backup is not evidence of recoverability.** Verification is required at two levels, and they are not substitutes for each other.

**Lightweight verification — `RESTORE VERIFYONLY`, frequently.** Cheap, and it confirms the backup set is readable and structurally intact with its checksums valid.

**It is explicitly not proof of recoverability.** `VERIFYONLY` does not restore anything, does not exercise the chain, and cannot tell you whether the restored database would contain a usable tenant schema. Treating it as sufficient would be the comfortable mistake this section exists to prevent.

**`VERIFYONLY` is supplementary evidence only.** It may record that a backup set is readable, and nothing more. It **must not**, on its own, establish that a backup set is restore-verified, satisfy a policy that requires actual restore verification, or contribute to cutover readiness.

**Actual restore verification — periodically, to a disposable database.** The only evidence that answers the real question. It restores a selected chain and confirms the restore completes, the database comes online, the tenant migration history is readable and at the expected position, and basic schema probes succeed.

### Where verification restores run *(v1.2)*

**Decision: in production, actual restore verification runs on a dedicated verification SQL Server instance — never on the instance hosting the authoritative production `TenantDatabase`.** Controlled non-production environments **may** verify on the same instance, explicitly and by configuration.

**This is an isolation and security decision, not a performance preference.** Four things follow from restoring on the production instance, and the last is the one that settles it:

1. Verification requires materially broader authority than backup execution — creating a database and later dropping it — where Phase B deliberately narrowed the backup identity to the least privilege that could take a backup.
2. A restore writes a full-size second copy of the database and competes for the same I/O, storage and memory as live tenant traffic.
3. Cleanup requires a working `DROP DATABASE` path.
4. Granting database-creation and database-destruction authority on the instance holding every tenant's authoritative data — and then exercising it unattended, on a schedule — puts a destructive automated operation next to the data it must never touch. **The blast radius of a mistargeted restore or cleanup is bounded by which instance holds those privileges**, and that is the property being protected.

**The verification target is selected through a trusted closed configuration key**, resolved in Infrastructure exactly as `BackupDestinationKey` is (§11). A caller-, tenant- or user-supplied server name, connection string or endpoint **must never** select or influence it. Concrete section and property names are an implementation concern.

**An unusable verification target fails closed.** Where the configured target is absent, cannot be resolved, is invalid for the environment, or fails the trust and isolation validation above, **restore verification does not run**. There is **no fallback to the source database's server** — in production that fallback is the precise outcome this decision exists to prevent, and a failure to resolve the dedicated target means verification is unavailable, not that it may proceed somewhere else. The non-production same-instance case is an explicitly configured exception, **never a fallback mode reached by a resolution failure.**

**Restore verification uses its own privileged identity**, distinct from the ERP runtime credential, from the migration credential, and from the backup identity. Reusing the backup identity for convenience would push database-creation rights onto every production instance the backup identity reaches, undoing the containment above.

**The exact minimum grant set is an implementation exit gate, not an architectural fact.** It must be established empirically against a real SQL Server during Phase D and recorded then. This ADR fixes only the binding constraints: least privilege, **no `sysadmin`**, and no silent escalation to make an operation work. Phase B is the precedent — the permission its in-flight guard required was neither the obvious one nor one that failed loudly when absent, and that was discovered by testing rather than by reading.

### How deeply a restore must be verified *(v1.2)*

A policy claims a recovery capability. **Verification must exercise the capability the policy claims, not a cheaper subset of it** — otherwise `Protected` asserts a recovery path that has never been performed, which is the failure §18 exists to prevent, one phase earlier than the cutover it guards.

Three depths, selected by what the active policy requires:

| Depth | Applies when | Restore sequence |
|---|---|---|
| **Level A** | Required recoverability is full-only | The selected platform-managed full backup |
| **Level B** | Differential protection is part of the active policy | The selected full baseline, then the latest **applicable** platform-managed differential based on that baseline |
| **Level C** | Transaction-log recovery is part of the active policy | The selected full baseline; the latest applicable differential **if one applies**; then every required platform-managed log backup after the last restored data backup, in restore order — recovering the database only at the end |

**"Latest applicable" is load-bearing.** A differential is restorable only onto the full baseline it was taken against; one belonging to a different base **must not** be selected. "Latest differential" without that qualification describes a restore that fails.

**A differential is not a precondition for log verification.** Where no differential applies, Level C is the full baseline followed by the required subsequent logs. Preferring the latest applicable differential is a legitimate restore-sequence strategy because it shortens the log tail that must follow; it is a strategy, not a requirement.

**The intended platform recovery point, for Level C, is the latest recovery point represented by the selected contiguous platform-managed log sequence available when the operation selects its chain.** It is not a tail-log capture taken at incident time, not a customer-selected stop-at position, and not an arbitrary point-in-time facility — it is the latest position the managed verification inputs themselves represent.

**A verification validates the selected recovery sequence, not a moving target.** The chain and its recovery point are chosen deterministically when the operation is admitted; log backups produced afterwards belong to the next verification rather than being appended to one already running. This is what keeps a verification finite on a database whose log cadence is measured in minutes.

**This ADR states the guarantee, not the algorithm.** The binding rule is that **the selected artifacts must form a valid, deterministic SQL Server restore sequence reaching the intended platform recovery point.** The mechanics that produce such a sequence — `backupset` metadata interpretation, differential base applicability, log-sequence continuity, LSN boundary semantics and recovery-fork behaviour — **must be validated against SQL Server and proven by real restore tests during Phase D.** No simplified equality rule is adopted here as normative: freezing an untested formula as architecture is precisely the mistake Phase B's permission work exists to warn against.

### Verification proves the platform's own chain *(v1.2)*

**Restore verification proves that the platform's own managed artifacts form a recoverable sequence.** Externally created backups — DBA, SQL Agent, third-party tooling — are **not** selected as restore-verification inputs.

An external operation may legitimately change SQL Server's chain state. Where doing so leaves the platform-owned artifacts no longer forming a complete restorable sequence, the platform **must not** silently skip the missing segment, **must not** quietly fall back to a shallower restore while still reporting the original level verified, and **must not** treat an external artifact as satisfying platform verification. The correct outcome is a recorded **platform chain break** and a readiness degradation under the existing model (§6).

The distinction that matters operationally: an external operation that does not disrupt the platform's recovery path — a copy-only backup, which resets no differential base and truncates no log — **must not** degrade readiness. Platform-managed backups remain subject to the existing rules: the managed full chain is never `COPY_ONLY` (§9), and managed evidence remains quality-validated (§14).

**Restore inputs are resolved from platform-owned metadata** — the destination key, the recorded artifact reference, and provider identity and chain evidence — through trusted Infrastructure configuration. A caller-supplied path, UNC, URL, restore device or credential-bearing endpoint **must never** determine what is restored, exactly as it must never determine where a backup is written (§11).

### What a successful verification must establish *(v1.2)*

A completed `RESTORE` command is not a verified restore. The minimum V1 evidence is:

- the restore sequence completed;
- the verification database is **online**;
- the tenant migration history is readable and at the expected position;
- basic tenant schema probes succeed.

**`DBCC CHECKDB` is not required in V1.** It is the most expensive thing that could be added to each verification, and the probe set above is what the recovery question actually asks. It may become a future verification policy; it **must not** be added silently, and it is **not** part of the first dedicated activation gate (§18) unless separately decided.

### What a failed verification means for readiness *(v1.2)*

A verification can fail at several depths, and they do not mean the same thing. The V1 mapping:

| Failure | Recovery readiness | Because |
|---|---|---|
| The required full baseline cannot be restored | `Unprotected` | The minimum recoverable position cannot be demonstrated at all |
| The baseline restores, but a required differential or log segment cannot be applied or validated | `Degraded` | Recoverability exists, but the deeper capability the active policy claims is not currently proven |
| The restore completes and the database comes online, but a required migration-history, schema or usability probe fails | `Unprotected` | The restored database is not demonstrably usable as the tenant database the platform would need to recover |
| Restore and post-restore validation succeed, but cleanup fails | **unchanged** | Recoverability was proven; a failed drop is an operational and orphan condition, not evidence about the chain |

**A known required-chain break is governed by the chain-break rule, not by the "deeper failure is `Degraded`" row.** Where the platform-managed artifacts cannot reconstruct the required recovery path at all — because a segment is missing, superseded or expired — there is no usable required recovery point, and that is `Unprotected`. The `Degraded` row covers a chain that is present but whose deeper segment failed to apply or validate; it must not be read as softening an established break.

**Infrastructure failure is not evidence about the backup.** Where a verification cannot begin or cannot complete for reasons independent of the artifacts — the verification instance unavailable, credentials or configuration unresolvable, the artifact temporarily unreachable — **the attempt fails, and existing recovery evidence is neither upgraded nor invalidated.** Readiness is recomputed from the evidence already held and its age against policy; if verification is required and now stale, the result is `VerificationOverdue`. **A verification-host outage must not be converted into `Unprotected`**, which would report a well-protected database as unrecoverable on the strength of an unrelated failure.

That is the line the "a failed verification is not an overdue one" rule draws: an attempt that *ran and proved the required recovery path fails* is a recovery problem, while an attempt that *could not begin* is not evidence about recoverability in either direction.

### Verification is a distinct operation with a durable record *(v1.2)*

**Actual restore verification has its own durable operation record, separate from backup-run history.** Its lifecycle is not a backup's: it runs on a different cadence, against a different server, under a different identity, and — uniquely in this capability — **it creates a database that can outlive the process that created it.**

The platform must be able to determine, after an arbitrary process crash, which verification operation created a given database, whether that verification is still active, whether the restore and probes succeeded, and whether cleanup succeeded.

**This persistence is required by the safety rules below, not by scheduling convenience.** Automated destructive cleanup demands durable positive identification, and a process-local `try`/`finally` cannot provide it: a crash, a machine failure or a forced termination leaves the disposable database behind with no in-memory record that it ever existed. That is the case the record exists to cover.

**Safety rules for verification restores** — these are binding:

- a name drawn from a **reserved, system-controlled verification namespace** (see below), never a production name;
- **never** a `TenantDatabaseAssignment` target, never routable, never serving traffic;
- deterministic cleanup after the exercise;
- and because a crashed process can leave one behind, a **maintenance sweep for stale verification databases**. A `finally` block is not sufficient. This repository already carries orphaned test databases from earlier work, which is the practical demonstration of why.

**Reserved verification namespace.** Verification databases are created under a **reserved system-controlled prefix or equivalent structural marker** — conceptually `SSAS_Verify_<system-generated-id>`, though the exact literal should follow repository naming conventions rather than this example. "Deterministic naming" alone is not sufficient, because the sweep that consumes the naming convention is a `DROP DATABASE` operation, and a destructive operation must key on a namespace the platform owns rather than on a pattern a production database might one day match.

**Eligibility for automated cleanup is a conjunction, not a pattern match.** A database may be dropped by the sweep only if it satisfies *all* of:

- it carries the reserved system-controlled verification marker or namespace;
- it matches the platform's known verification creation convention;
- it exceeds an age or staleness threshold;
- it is **not** registered as a `TenantDatabase`;
- it has **no** `TenantDatabaseAssignment`;
- it is not the target of a currently running verification.

**A database must never be dropped solely because a loose string pattern happened to match its name.** The precise mechanism by which each condition is evaluated is a Phase D implementation detail; the boundary of what automated deletion may touch is architectural and is fixed here.

**The same conjunction gates every destructive step, not only the `DROP`** *(v1.2)*. Forcing a database to single-user with immediate rollback is itself destructive against the wrong target, so it is bound by the identical conditions. Cleanup correlates the physical database to its durable verification record before touching it; **a name alone never authorises destruction.**

### Restoring safely into the verification database *(v1.2)*

**`RESTORE ... WITH REPLACE` must not be used.** This is a structural blast-radius invariant, not a stylistic preference: `REPLACE` is the clause that converts a mistargeted restore from a failed operation into the destruction of an existing database. Verification never needs it, because it always restores into a name the platform has just generated for that purpose.

**If the generated verification database name already exists, the verification fails safe.** It does not overwrite, does not replace, and does not blind-drop. The existing database is left for orphan reconciliation, which will remove it only under the full conjunction above.

**Verification databases are named from a reserved, platform-generated vocabulary** — conceptually `SSAS_Verify_<physical database identity>_<verification operation identity>`, with exact token spelling an implementation concern. The name must be platform-generated and never caller-supplied, identifier-safe, unique per verification operation, recognisable as a verification database, and within SQL Server's identifier bounds.

**Restored files are always relocated.** A backup carries the original database's physical file paths, and reusing them would write over the very files the verification exists to protect. Therefore: the backup's file list is read and every logical file is redirected to a verification-owned path, with **multiple data files and multiple log files supported** rather than a single data/log pair assumed.

**Restored data and log roots come from trusted verification configuration**, never from a caller. Generated physical file names are unique per verification operation, and nothing is overwritten.

### Cleanup outcome is not verification outcome *(v1.2)*

These are separate dimensions and must be recorded separately. Where the restore and its probes succeed but cleanup fails, **the recovery evidence remains valid**: the chain was restored and the database was proven usable, and that fact does not become untrue because a `DROP` failed afterwards.

The platform records the successful restore verification, records the cleanup failure independently, and surfaces the orphan operationally. Reporting unqualified success would conceal a database consuming capacity on the verification instance; reporting failure would discard genuine recovery proof and could push a correctly protected database out of `Protected`. **Neither error is acceptable, which is why the two outcomes are not collapsed.**

### Scheduling and ownership of verification *(v1.2)*

Restore-verification scheduling is **separate from high-frequency backup scheduling**, with its own enablement, cadence, concurrency limits, credential and restore target. The two differ in cost by orders of magnitude and in blast radius by more; sharing a loop would force one set of limits onto both.

**For one physical `TenantDatabase` and one due restore-verification state, the deployment must produce at most one effective verification operation across all application instances.**

**The serialization boundary must cover admission of the operation, not only ownership of an already-created one.** This is the precise point, and it is where an earlier draft of this section was insufficient: an atomic claim on a verification record serialises workers competing for *the same record*, which does nothing when two instances each create their own. The sequence that must be impossible is:

> Both instances observe the same database as due; instance A creates its operation and instance B creates a separate one; each successfully claims the record it created; both restore.

Both claims succeed there, because they are claims on different rows. **That is Phase C's stale-decision duplicate re-keyed**, and the lesson it established stands unchanged: *observing that work is due and later executing it is not a multi-instance correctness boundary.* The serialising event must occur **before or as part of admitting the effective operation**.

**The "same due state"** is the restore-verification obligation derived from the physical database, its active backup policy, the current successful verification evidence, and the selected recovery baseline and chain. It is a concept, not necessarily a persisted key.

**The mechanism is deliberately not fixed here.** A due-state-keyed atomic claim, a uniqueness constraint over the active or due operation, an ownership-bound authoritative recheck at admission, or another equivalent transactional mechanism are all acceptable; which one is right should be settled by implementation and proven, not chosen in advance. The invariant is binding; the mechanism is not.

**Audit records for contending or skipped attempts remain permitted** — recording that a second instance found the work already admitted is useful history. Only one actual restore verification may execute for one due state.

**A verification-scoped application lock may protect the physical execution**, and is a reasonable defence for crash-resume. **It does not by itself solve duplicate admission**: two operations created for the same due state can each take a lock scoped to their own target and proceed. **No global fleet lock and no leader election** is required or permitted.

**Verification ownership must not be made to contend with backup or migration ownership** as a correctness rule. Under the production topology above, verification does not touch the source database at all — it reads artifacts and writes a database on another instance. Resource competition is governed by concurrency limits, not by a shared correctness lock, for the same reasons set out in §14.

Cadence for both levels is policy-driven, not fixed here.

## 18. Cutover gate

`ADR-020` requires backup and recovery readiness before a dedicated target becomes production-active. This ADR makes that requirement concrete.

Before **first production activation** of a `PlatformManaged` dedicated target:

1. a backup policy is assigned and enabled;
2. the recovery model is valid for what the policy requires;
3. **at least one successful full backup** has been taken — the chain is initialised, not merely scheduled;
4. where point-in-time recovery is required, the **log chain is established**;
5. **at least one successful actual restore verification** has completed;
6. `RecoveryReadinessStatus` is `Protected`.

**On requirement 5 — requiring a real restore, not merely a successful backup.** This is the most demanding requirement here and it is deliberate. Before cutover the tenant's data sits in the shared database, covered by a chain that has been exercised repeatedly across many tenants. The instant the assignment flips, that coverage stops applying and the authoritative copy lives on a chain that has never been restored even once. Accepting "a backup file exists" as sufficient accepts an unexercised chain at precisely the moment durability responsibility transfers — and the first restore would then be attempted during an incident, which is the worst possible time to discover the chain does not work.

The gate is on `RecoveryReadinessStatus`, **not** on `BackupEnabled = true`. Configuration is not protection.

**Requirements 5 and 6 are independent, and requirement 5 is not implied by requirement 6** *(v1.2)*. Where a policy does not require periodic restore verification, a database may be `Protected` without a real restore ever having occurred (§6). The activation gate is therefore stricter than periodic readiness by design, and both conditions are checked. `ADR-020` lists them separately for the same reason.

**The verification must relate to the recovery path being activated** *(v1.2)*. Activation evidence identifies the current relevant full baseline, the validity of the chain built on it, the successful actual restore verification, **the relationship between that verification and the required baseline and recovery path**, and `RecoveryReadinessStatus`. A verification of a superseded baseline does not demonstrate that the chain now protecting the target has ever been restored, which is the whole content of requirement 5. Phase D produces this evidence; **Phase E consumes it and owns the gate's execution.**

For `CustomerManaged` targets the obligation is to **confirm** the customer's arrangement rather than to execute it, and an unknown recovery position does not satisfy the gate.

## 19. Shared restore semantics

Restated prominently because it is the most likely misreading of this entire capability:

**A shared physical database has one backup chain. Restoring it restores the database — and therefore every tenant in it.**

"Restore tenant A to yesterday" is **not** a physical database restore on shared storage. Performing one would roll back every other tenant sharing that database.

**Restore verification is scoped to the physical database** *(v1.2)*. A shared database is verified once, as one physical database with one chain. The result is never presented as per-tenant restore verification, and Level C verifying the log chain's recoverability establishes a chain capability — **it does not create any customer-facing point-in-time recovery operation.**

**`TS-Backup` does not provide tenant-level point-in-time recovery.** That is a different capability requiring export, extraction or logical reconstruction, and it is **out of scope** for this ADR. Dedicated placement is what makes database-level restore a per-tenant operation, and that remains one of its genuine advantages over the shared default (`ADR-017`).

---

# Decision Drivers

- Durability must be demonstrable, not assumed.
- A dedicated database must never be less protected than the shared database a tenant came from.
- Availability and durability are different concerns and must fail differently.
- The platform must not hold custody of customer data it was not asked to hold.
- The blast radius of a first backup implementation must be bounded.
- Provider-specific mechanics must not become architectural assumptions.
- Secrets must not accumulate in the Platform database.

---

# Alternatives Considered

## 1. Backup settings directly on `TenantDatabase` versus a separate policy entity

**Rejected: columns on `TenantDatabase`.** Simpler initially, and for a single schedule it would be adequate. It was rejected because policy is the part of this model that will grow — three schedules, retention, destination, verification cadence, age tolerance, later tiers and provider settings — and `TenantDatabase` is read on the routing path. A separate entity also keeps the policy/evidence/readiness distinction visible in the model rather than only in prose.

## 2. Reuse `MigrationManagementMode` versus a separate `BackupManagementMode`

**Rejected: reuse.** The values would look identical today, which is exactly what makes the trap attractive. But the authorities are independent: a customer may permit platform migrations while their DBA retains backup responsibility, or the reverse. `ADR-018` already corrected this same conflation once, when hosting vocabulary was reused for migration authority and produced configurations that read as contradictions. Reusing it again would reintroduce a defect the architecture has already paid to fix.

## 3. Runtime credential versus a dedicated backup identity

**Rejected: runtime credential.** Convenient, and it needs no new configuration. Rejected because `BACKUP DATABASE` produces a complete copy of the database at a location of the caller's choosing; granting it to the request-serving identity means any application-level compromise escalates from query access to full data exfiltration. The separation costs configuration; the alternative costs containment.

## 4. SQL Agent versus platform orchestration

**Rejected: SQL Agent as the domain owner.** It is mature, well understood, and operationally familiar. Rejected because it is per-server: it cannot see the fleet, does not exist on customer-managed endpoints, and would split ownership of a policy the platform is accountable for across servers the platform may not administer. It remains usable as a deployment mechanism beneath a platform-owned policy.

## 5. Platform deletes artifacts in V1 versus storage lifecycle ownership

**Rejected: platform deletion in V1.** A complete backup manager arguably owns the whole lifecycle. Rejected on blast radius: chain-aware deletion is subtle, its failure mode is silent and unrecoverable, and storage lifecycle policies already handle expiry adequately. Deletion is also the easiest half to add later, once creation and verification are proven.

## 6. `VERIFYONLY` only versus periodic real restore

**Rejected: `VERIFYONLY` alone.** Cheap and frequent, and it does catch corrupt or truncated backup sets. Rejected as *sufficient* because it proves the file is readable, not that the database is recoverable — it never restores anything and never looks at what a restored database would contain. A chain can pass `VERIFYONLY` indefinitely and still fail its first real restore.

## 7. Successful backup versus actual restore verification before first cutover

**Rejected: successful backup alone.** It is the lower-friction gate and would rarely bite. Rejected because cutover is the specific moment a tenant moves from an exercised chain to an unexercised one; if verification is ever justified, it is justified there. See §18.

## 8. Gate ERP traffic on backup degradation versus lifecycle gates only

**Rejected: gating ERP traffic.** Superficially the "safe" choice. Rejected because it is not safe — it converts a durability concern, where the data is intact and correct, into a customer-visible outage. It would also create an incentive to under-report degraded protection, which is the opposite of what a durability capability needs.

## 9. Universal backup-type enum versus provider-scoped vocabulary

**Rejected: a universal enum.** `Full`/`Differential`/`TransactionLog` is SQL Server's model. Mapping Oracle's RMAN and archived redo, or PostgreSQL's WAL, onto those three names would be lossy in both directions and would encode one provider's semantics as the architecture. Provider-scoped vocabulary costs a little indirection now and avoids a rewrite at the first non-SQL-Server provider.

## 10. Excluding full and differential backups during migration versus permitting coexistence

**Rejected: excluding full and differential backups during a migration.** An earlier draft of this ADR adopted that exclusion, permitting only log backups to overlap. It has the intuitive appeal of every restriction that sounds cautious, and it was rejected for two independent reasons.

The first is that it is **technically unnecessary**. Its stated rationale — that a full or differential taken mid-migration captures a "half-applied schema" — is wrong. `BACKUP DATABASE` is online and yields a transactionally consistent restore point; what it can capture is a committed intermediate migration state, which `ADR-018`'s migration-history model already classifies and the orchestrator already knows how to advance. The exclusion protected against a failure that does not occur.

The second is that it was **unenforceable as specified**. Migration and backup take distinct `sp_getapplock` resources, which by design do not contend; the ADR stated a rule no stated mechanism could realise. Closing that gap would have meant inventing cross-lock coordination — a shared exclusion resource, or one operation acquiring the other's lock — adding machinery, coupling two independent ownership concerns, and creating new deadlock and starvation surface, all to enforce a restriction that was not needed.

**Removing the rule was therefore strictly better than enforcing it**: it resolves the gap by deletion rather than by construction, and it leaves the genuine concern — I/O contention — where it belongs, as a scheduling preference. See §14.

## 11. Same-instance verification restore versus a dedicated verification instance

**Rejected: restoring onto the production instance in V1.** It is markedly simpler — no second instance to provision, no second credential, no cross-instance access to the artifacts — and for a small estate the I/O cost would often go unnoticed. It was rejected because the cost is not primarily I/O. Verification needs authority to create and drop databases, and it exercises that authority unattended on a schedule; granting it on the instance holding every tenant's authoritative data places an automated destructive operation next to the data it must never touch, and no amount of naming discipline reduces the privilege itself. A dedicated instance bounds the blast radius structurally rather than procedurally. Same-instance verification remains available for non-production environments, where the trade-off genuinely does favour simplicity.

## 12. Verifying the full only versus verifying the depth the policy claims

**Rejected: always verifying the full baseline alone.** It is much cheaper, bounded in time, and would satisfy a literal reading of "at least one successful actual restore verification". It was rejected because it makes `Protected` mean less than it appears to. A policy scheduling log backups claims point-in-time recoverability; verifying only its full proves the baseline restores and leaves the differential and log path — the part with the most moving pieces and the most ways to break — entirely unexercised. The first real use of that path would then occur during an incident, which is the exact position §18 was written to prevent.

---

# Rationale

The central decision is that **durability is a separate, independently observable dimension** — separate from schema health, separate from availability, and separate from configuration. Every other decision follows from taking that seriously.

Keeping recovery readiness out of the traffic gate follows from it: a database whose backups are late is a durability problem, not an availability problem, and conflating them would produce outages in response to durability signals. Gating *cutover* on it follows equally: that is the one operation whose safety genuinely depends on demonstrated recoverability.

Requiring evidence rather than configuration is the same principle applied to the model itself. `ADR-018` already established that a metadata column claiming a schema version the database does not have is worthless, and that observed history is the only trustworthy source. Recovery readiness inherits that: `Protected` means evidence exists, and `VERIFYONLY` alone is not the evidence the question requires.

Deferring deletion and deferring RPO/RTO tiers are the same judgement in the other direction — the capability that must land first is the one that establishes protection and proves it, not the one that removes artifacts or classifies service levels.

---

# Consequences

## Positive

- Protection state is visible per physical database, and truthfully distinguishes "never protected" from "protection slipping".
- Shared and dedicated semantics are correct and explicit, including the shared-restore limitation.
- Dedicated durability becomes provable before a tenant depends on it.
- Business availability is insulated from durability degradation.
- Cutover can fail closed on recoverability rather than assuming it.
- Backup credentials are separated from the request-serving identity, containing a real escalation path.
- Secret scope in the Platform database stays limited to keys and references.
- Provider-specific mechanics remain replaceable.

## Negative

- A privileged operational identity must be provisioned and managed, and restore verification adds a second, more privileged one.
- A dedicated verification SQL Server instance must be provisioned, reachable, and given capacity for a full-size restored database.
- Verification depth means a log-protected database's verification restores a chain rather than a single file, which costs materially more time and I/O than verifying a full alone.
- Backup destinations must be configured and validated against the SQL Server service identity's access, which is an unfamiliar constraint.
- Run history grows and needs its own retention.
- Backup operations are long-running, complicating ownership and scheduling relative to migrations.
- Restore verification consumes storage, time and server capacity.
- Verification databases add a cleanup obligation with a known orphan failure mode.
- A fourth health dimension increases the operational model operators must learn.
- Retention split between platform policy and storage lifecycle means two places to look.

---

# Implementation Guidelines

- Build the domain and readiness model before any backup command executes; policy, authority and ownership should exist before provider operations do.
- Add deterministic RowVersion conflict coverage before introducing the recovery-readiness writer as the third writer on `TenantDatabase`.
- Keep provider SQL entirely in Infrastructure; the Application layer never composes backup commands.
- Resolve destinations only from the trusted `BackupDestinationKey`; never accept a path, UNC, URL or endpoint supplied by a caller.
- Validate destinations in terms of the SQL Server service identity's access, not the application process's.
- Record chain metadata from the first backup; reconstructing continuity retrospectively is far harder.
- Treat session loss as loss of ownership, always, and establish success from reconciled provider evidence rather than from a completed command.
- Never mark a customer-managed database `Protected` without verified evidence.
- Give verification databases a reserved system-controlled namespace and a maintenance sweep, not just a `finally` block, and require the full eligibility conjunction of §17 before any automated `DROP`.
- Write the durable verification record **before** the restore begins; a record created afterwards cannot describe the crash it exists to survive.
- Establish the restore, create and drop permission set empirically against a real SQL Server before treating it as known, and never widen it to `sysadmin` to make an operation succeed.
- Prove the restore sequence against SQL Server with real restore tests — including multiple data and log files, an absent applicable differential, and a chain whose platform-owned segment is incomplete — rather than deriving it from chain metadata alone.
- Keep cleanup outcome and verification outcome separate from the first commit; collapsing them is easy to do early and expensive to unpick once readiness depends on the result.

## Phased implementation

| Phase | Content |
|---|---|
| **A — Backup domain and recovery readiness** *(delivered)* | `TenantDatabaseBackupPolicy`, `BackupManagementMode`, recovery-readiness status and observations, `TenantDatabaseBackupRun`, Platform migration, dimension-scoped recovery writer, deterministic RowVersion conflict test. **No backup execution.** |
| **B — SQL Server provider, single database** *(delivered)* | Trusted privileged identity, destination resolution, full/differential/log, `CHECKSUM`, compression policy, provider metadata, target-database ownership, **the session-loss empirical validation and its exit gate (§14, now closed)**, mandatory in-flight detection with a server-visibility precondition, post-operation success reconciliation. |
| **C — Fleet scheduling and orchestration** *(delivered)* | Due evaluation, physical discovery, keyset paging, bounded concurrency, a scheduling background service, multi-instance duplicate-safe orchestration, run state, progress reporting. |
| **D — Verification and retention** *(architecture defined in v1.2; implementation not started)* | `RESTORE VERIFYONLY`, periodic disposable restore on a dedicated verification instance, verification depth by policy, platform-managed-chain-only selection, a durable verification operation record, reserved verification namespace and orphan cleanup eligibility, retention metadata and storage-lifecycle delegation. |
| **E — Cutover integration** *(future)* | Recovery-readiness gate in `ADR-020`, dedicated activation guard, consuming the activation evidence Phase D produces. |

**Phase A was the first delivered slice**, and deliberately not backup execution. Policy, authority, readiness semantics and writer concurrency were settled before any command touched a database, because those were the decisions the provider work would assume — and because Phase A was the slice that closed the outstanding RowVersion test gap while there were still only two live writers to reason about.

**Phase B carried a binding exit gate, and that gate is now closed.** The session-loss behaviour of a long-running `BACKUP` was empirically established on a real SQL Server against a disposable database across three conclusive trials, and the in-flight detection guard was implemented and retained as mandatory rather than as a fallback (§14). Phase C may now begin. The original reasoning for gating it still holds and is why the guard is mandatory rather than optional: fleet scheduling multiplies a single-database ownership ambiguity across the estate, converting a rare window into a routine one, and it was far cheaper to settle at one database than at a fleet.

Backup metadata belongs to `PlatformDbContext`. Phase A introduced the Platform migration that persists backup policy, run history and the recovery-readiness fields; **no tenant migration is expected**, and backup metadata must not be placed in a tenant ERP database. Phase D's durable verification record is expected to extend Platform persistence on the same terms — one Platform migration, no tenant migration — and **every new persisted string must be Unicode (`nvarchar`)**, in line with the project-wide rule. Project-wide Unicode remediation is separate work and is not part of Phase D.

**A deployment prerequisite, recorded as process risk rather than architecture.** Phase D introduces unattended operations that create databases, force them to single-user, drop them, and write restore files to disk. The repository currently has no branch protection, no required checks and no required approvals on its main branch, so Phases B and C each merged without an approving review. That was tolerable for a capability defaulting to disabled; it is a materially larger exposure for one whose failure mode is destructive. **Closing the repository's review and check governance is recommended before production restore verification is enabled.** This ADR records the concern; it does not specify the mechanism, and repository settings are not changed by it.

## Audit

Policy changes follow existing Platform audit conventions; execution runs record a stable operational actor identity.

**Durability-affecting changes are audited explicitly.** These are the changes that alter whether — or by whom — a database is protected, and a change to any of them can move a database from protected to unprotected without any operation failing. Named explicitly so none is treated as ordinary configuration:

- `BackupManagementMode` — who is permitted to execute backups at all;
- `BackupDestinationKey` — where backups are written;
- policy enabled or disabled;
- schedule changes for any backup type;
- retention expectation changes;
- verification requirement and cadence changes;
- any other materially recovery-affecting policy change.

The audit record identifies the actor, the physical database, the previous and new values, and the time. Credential material and resolved physical destinations are **not** recorded — the destination key is, the secret behind it is not (§11).

---

# Compliance Rules

1. Backup policy, run history and recovery readiness **must** attach to the physical `TenantDatabase`, never to a tenant or an assignment.
2. Recovery readiness **must** remain a separate dimension; it **must not** be merged into `SchemaCompatibilityStatus`, `ConnectivityStatus`, `MigrationExecutionStatus`, or any derived general-health flag.
3. The recovery-readiness writer **must** write only its own dimension, and **must not** write when it observed nothing.
4. `BackupManagementMode` **must** be modelled separately from `MigrationManagementMode` and **must not** be inferred from `HostingMode` or `StorageMode`.
5. The ERP runtime credential **must not** hold backup privileges.
6. Credential material, storage keys, tokens and signed URLs **must not** be stored in the Platform database.
7. The platform **must not** execute backups against a `CustomerManaged` database by default, and **must not** report one `Protected` without verified evidence.
8. Normal ERP traffic **must not** be gated on recovery readiness.
9. Dedicated production activation and Shared → Dedicated cutover **must** be gated on `RecoveryReadinessStatus`, and **must** fail closed on `Unknown`.
10. First production activation of a dedicated target **must** require a successful full backup, a valid recovery model, an established log chain where required, and at least one successful actual restore verification.
11. `Protected` **must** be derived from evidence, never from policy being enabled.
12. Transaction-log backups **must not** be presented as a sufficient strategy on their own.
13. The recovery model **must not** be changed automatically by the platform.
14. `COPY_ONLY` **must not** be used for the managed full chain.
15. Managed backup of one physical database **must** hold single-writer ownership, in a resource namespace distinct from migration ownership. The migration and backup namespaces **must not** be made to contend with each other, and no cross-lock coordination between them **may** be introduced.
16. Loss of the owning session **must** be treated as loss of ownership; `Succeeded` **must not** be recorded afterwards, and **must not** be recorded on the strength of a submitted or completed command without post-operation provider evidence.
17. Full, differential and transaction-log backups **may** run concurrently with a tenant schema migration, and a migration **may** begin while a backup is running. Avoidance of large backups during migration windows **may** be a scheduling preference; it **must not** be implemented as a correctness invariant or enforced through locking.
18. The platform **must not** physically delete backup artifacts in V1; any future deletion **must** be chain-aware.
19. Restore verification databases **must never** be routable, assigned, or serve traffic, and **must** be cleaned up deterministically.
20. `RESTORE VERIFYONLY` **must not** be treated as proof of recoverability.
21. Fleet operations **must** discover physical databases with keyset paging and **must** process a shared database once.
22. SQL Server backup-type vocabulary **must** remain provider-scoped.
23. Backup destinations **must** be selected exclusively through a trusted configuration reference such as `BackupDestinationKey`. Caller-, tenant- or user-provided filesystem paths, UNC paths, URLs, storage endpoints and credential-bearing references **must not** determine the physical backup destination.
24. A database **must not** be eligible for automated verification cleanup unless it carries the platform's reserved system-controlled verification namespace or marker **and** satisfies the additional ownership, registration and age criteria of §17. Destructive cleanup **must not** be driven by a loose name-pattern match alone.
25. Phase B **must not** be considered production-ready until SQL Server's behaviour on client/session loss during an active `BACKUP` has been empirically validated against a disposable database, and either that behaviour is proven safe or an in-flight backup detection guard is implemented. Phase C **must not** proceed on an unresolved gate. *(Satisfied: validated across three conclusive trials, with the in-flight guard implemented and retained — §14.)*
26. A detected in-flight backup against the target physical database **must** prevent a second platform-managed backup, and **must** be recorded as a controlled skip rather than as a backup failure.
27. Dedicated production activation and Shared → Dedicated cutover **must** require `RecoveryReadinessStatus` = `Protected`; no other status, including `Degraded` or `Unknown`, satisfies the gate.
28. Durability-affecting policy changes — including `BackupManagementMode` and `BackupDestinationKey` — **must** be audited under existing Platform audit conventions, without recording credential material or resolved physical destinations.
29. In-flight backup detection **must** be performed before every platform-managed backup. It is a correctness precondition, **not** an operational feature flag, and **must not** be disableable by fleet, scheduler or deployment configuration.
30. The provider **must** establish that its identity holds sufficient server visibility *before* trusting an in-flight query result, because `sys.dm_exec_requests` silently narrows to the caller's own session rather than failing when that visibility is absent. If visibility cannot be established the backup **must not** start, and the outcome **must** be distinguishable from having observed an in-flight operation. On SQL Server 2022 the required permission is `VIEW SERVER PERFORMANCE STATE`; `VIEW SERVER STATE` is not required and `sysadmin` **must not** be used to satisfy it.
31. When several backup operations are due for one physical database, the scheduler **must** dispatch exactly one per sweep, in the order transaction log, then full, then differential. An externally taken backup **must not** be treated as satisfying the platform's schedule.
32. In production, actual restore verification **must** run on a dedicated verification instance and **must not** run on the SQL Server instance hosting the authoritative production `TenantDatabase`. Same-instance verification **may** be permitted only in explicitly configured non-production environments.
33. The verification restore target **must** be selected through a trusted closed configuration key resolved in Infrastructure. A caller-, tenant- or user-supplied server name, connection string or endpoint **must not** select or influence it.
34. Restore verification **must** use a privileged identity distinct from the ERP runtime, migration and backup identities, **must** follow least privilege, and **must not** use `sysadmin`. The exact minimum grant set **must** be established empirically against a real SQL Server during implementation and **must not** be frozen as architecture beforehand.
35. Verification depth **must** exercise what the active policy claims: full-only policies restore the full; differential protection additionally restores the latest **applicable** differential based on the selected baseline; log protection additionally restores the required subsequent platform-managed log backups in order, recovering only at the end. A differential **must not** be treated as a precondition for log verification.
36. The selected artifacts **must** form a valid deterministic SQL Server restore sequence to the intended platform recovery point. The mechanics establishing that sequence **must** be validated against SQL Server behaviour and proven by real restore tests; no simplified chain formula is normative.
37. Restore verification **must** select only platform-managed artifacts. Where external activity leaves the platform-owned artifacts unable to form a complete restorable sequence, the platform **must** record a chain break and degrade readiness, and **must not** silently verify a shallower level while reporting the level the policy claims.
38. Restore verification **must not** use `RESTORE ... WITH REPLACE`, **must** relocate every restored database file to a verification-owned path from trusted configuration, and **must** fail safe rather than overwrite when the generated verification database name already exists.
39. Actual restore verification **must** have a durable operation record sufficient to identify, after a process crash, which verification created a database, whether it is active, and whether restore and cleanup succeeded. A process-local `try`/`finally` **must not** be relied upon as the cleanup guarantee.
40. Cleanup outcome **must** be recorded separately from verification outcome. A cleanup failure following a successful restore and probe **must not** discard the recovery evidence, and **must** surface the orphan operationally.
41. `RESTORE VERIFYONLY` **must not** by itself establish restore-verified state, satisfy a policy requiring actual restore verification, or contribute to cutover readiness. Actual restore verification additionally **must** establish that the database is online, the tenant migration history is readable at the expected position, and basic schema probes succeed. `DBCC CHECKDB` is **not** required in V1.
42. Where a policy does not require periodic restore verification, readiness **must not** be reported `VerificationOverdue` for that reason alone, and the database **may** reach `Protected` without an actual restore. First production activation of a dedicated target **must** still require at least one successful actual restore verification related to the baseline being activated.
43. For one physical `TenantDatabase` and one due restore-verification state, at most one effective verification operation **must** execute across all application instances. The serialising event **must** cover admission of the operation and **must not** rely solely on claiming an already-created verification record, since two instances that each create their own record can each claim it successfully. Global fleet locking or leader election **must not** be introduced, and verification ownership **must not** be made to contend with backup or migration ownership as a correctness rule.
44. Where the configured verification target is absent, unresolvable, invalid for the environment, or fails trust and isolation validation, restore verification **must not** run. There **must** be no fallback to the source database's server, and the non-production same-instance exception **must not** be reachable as a fallback from a resolution failure.
45. Readiness following a failed verification **must** follow §17's mapping: an unrestorable required baseline, a failed post-restore usability probe, or an unreconstructable required recovery path is `Unprotected`; a restorable baseline whose deeper required segment fails to apply or validate is `Degraded`; a cleanup-only failure leaves readiness unchanged. A verification that could not begin or complete for reasons independent of the artifacts **must not** be treated as evidence of unrecoverability, and **must not** produce `Unprotected`.

---

# Risks

| Risk | Mitigation |
|---|---|
| Backup runs long and its lock-bearing connection is interrupted | Session-scoped ownership; session loss is ownership loss; no success recorded after it without reconciled provider evidence |
| The applock is released while a server-side `BACKUP` is still winding down, letting a second worker overlap | Validated in Phase B across three conclusive trials: no overlap observed, but at margins of 1–10 ms, which are not treated as a portable guarantee (§14). Mandatory in-flight detection with a server-visibility precondition is retained as the standing defence |
| A backup started outside the platform — DBA, SQL Agent, third-party tooling — overlaps a managed backup | Such operations take no platform lock and are structurally invisible to ownership; mandatory in-flight detection against live server state is the only mechanism that sees them |
| The in-flight guard is disabled to work around a missing permission | The guard is a correctness precondition, not a feature flag, and must not be disableable by configuration; a missing visibility permission fails the backup closed and names the missing prerequisite |
| Verification-database sweep drops a database it should not | Reserved system-controlled namespace plus a conjunction of ownership, registration and age criteria correlated to a durable verification record; pattern-only matching prohibited, and the same conjunction gates single-user forcing as well as the `DROP` |
| A verification restore is mistargeted and destroys a live database | Production verification runs on a dedicated instance that holds no authoritative tenant data; `WITH REPLACE` prohibited; every restored file relocated; an existing target name fails safe rather than being overwritten |
| Database creation and destruction privileges are granted on production tenant instances | Dedicated verification instance in production, with a verification identity distinct from the runtime, migration and backup identities; least privilege, no `sysadmin`, grant set proven empirically rather than assumed |
| Verification reports a recovery level the policy claims but never exercised | Depth follows the active policy — full, plus applicable differential, plus required subsequent logs; an incomplete platform-owned sequence is a recorded chain break, never a silent fallback to a shallower restore |
| A crash leaves a verification database with no record it existed | Durable verification operation record written before the restore begins, correlated by the orphan sweep; a `try`/`finally` is explicitly insufficient |
| A cleanup failure is reported as a verification failure, or hides an orphan | Cleanup outcome recorded separately from verification outcome; recovery evidence is retained and the orphan surfaced operationally |
| A stale verification of a superseded baseline satisfies a first dedicated activation | Activation evidence relates the verification to the current baseline and recovery path; periodic readiness and the cutover gate are separate tests |
| A backup destination is influenced by caller input | Destination resolved only from a trusted `BackupDestinationKey` in Infrastructure; caller-supplied paths, UNC, URLs and endpoints prohibited |
| Durability weakened by a quiet policy change rather than a failed operation | `BackupManagementMode`, destination key, schedules, retention and verification requirements explicitly audited |
| Chain continuity becomes unverifiable | Record chain metadata from the first backup; platform history authoritative over `msdb` |
| Storage lifecycle deletes a chain dependency | Retention expectation recorded and reported; future platform deletion must be chain-aware |
| Restore verification consumes capacity | Policy-driven cadence; disposable databases; separate from frequent lightweight verification |
| Verification databases orphaned by a crash | Deterministic naming and a maintenance sweep, not only `finally` |
| Privileged backup identity is over-granted | Separate authority from runtime and migration; least privilege for the operations required |
| Operators read `Protected` as tenant-level restorability on shared storage | Shared restore semantics stated explicitly in this ADR and in operator-facing reporting |
| A fourth dimension makes health harder to read | Each dimension stays independently observable and actionable; no composite flag |

---

# Future Considerations

- Formal RPO and RTO service tiers (deferred; the operative V1 field is maximum tolerable backup age).
- Platform-side chain-aware retention and deletion.
- Delegated backup for customer-managed databases, requiring `ADR-021`'s connectivity and credential model.
- Tenant-level logical recovery — a separate capability, not an extension of physical backup.
- Cross-region disaster recovery, geo-replication and automatic failover.
- Native provider backup encryption with managed key material.
- Administrative APIs and operator UI, after the execution model is stable.
- Non-SQL-Server backup providers, gated on `TS-Provider`.
- Backup destination capacity monitoring, where the destination type makes it meaningful.

---

# Out of Scope

- Oracle and PostgreSQL backup providers.
- Tenant-level logical or point-in-time restore.
- Cross-region DR, geo-replication, automatic failover.
- Formal RPO/RTO tiering.
- Operator UI and full administrative HTTP API in the first slice.
- Customer-managed backup execution.
- Platform-side artifact deletion in V1.
- Dedicated provisioning and cutover implementation (`ADR-020`).
- Notification, alerting and paging subsystems.

---

# Related Documents

- `ADR-013` — Primary Key and Identifier Strategy
- `ADR-017` — Tenant Storage Topology and Routing
- `ADR-018` — Tenant Schema Health and Migration Orchestration
- `ADR-019` — Dynamic Tenant Placement Policy
- `ADR-020` — Shared-to-Dedicated Tenant Migration and Cutover
- `ADR-021` — Customer-Managed Tenant Database Connectivity and Operations

---

# Review Criteria

This ADR should be reviewed if:

- a second database provider is adopted;
- delegated backup for customer-managed databases is contracted;
- platform-side artifact deletion is proposed;
- tenant-level logical recovery is required;
- formal RPO/RTO tiers are introduced;
- the cutover verification requirement is challenged on operational cost;
- backup health is proposed as an ERP traffic gate.

---

# Decision Summary

| Question | Decision |
|---|---|
| Scope unit | Physical `TenantDatabase` |
| Shared backup | One chain per physical database |
| Dedicated backup | Independent chain per database |
| Policy model | Separate `TenantDatabaseBackupPolicy` entity |
| Run history | Persisted (`TenantDatabaseBackupRun`) |
| Recovery readiness | Separate fourth dimension |
| Backup authority | Separate `BackupManagementMode` |
| Normal ERP traffic gated on backup health | **No** |
| Dedicated cutover gated on recovery readiness | **Yes** |
| First cutover requires full backup | **Yes** |
| First cutover requires actual restore verification | **Yes** |
| Platform deletes artifacts in V1 | **No** |
| Retention owner in V1 | Storage lifecycle; platform tracks expectation |
| `RESTORE VERIFYONLY` | Yes — frequent, not sufficient alone |
| Periodic actual restore | **Yes** |
| Automatic recovery-model change | **No** |
| Runtime credential used for backup | **No** |
| Separate backup identity | **Yes** |
| `CHECKSUM` | On by default |
| `COMPRESSION` | Configurable, default on where supported |
| `COPY_ONLY` for managed chain | **No** |
| Scheduling owner | Platform orchestration, not SQL Agent |
| Ownership mechanism | `sp_getapplock`, Session scope, target database — **planned; sufficiency conditional on Phase B validation** |
| Lock namespace | Distinct from migration, and deliberately non-contending |
| Migration/backup cross-lock | **None — not required, not permitted** |
| Lock granularity | One per physical database |
| Log backup during migration | Permitted |
| Full/differential during migration | **Permitted** |
| Migration/backup scheduling separation | Operational preference only, not a correctness invariant |
| Session-loss behaviour during `BACKUP` | **Validated in Phase B** — no overlap observed in three trials, at 1–10 ms margins; not a portable guarantee |
| In-flight backup detection guard | **Mandatory V1** — not conditional, and not disableable by configuration |
| Server visibility for in-flight detection | `VIEW SERVER PERFORMANCE STATE` (SQL Server 2022); never `sysadmin`; missing visibility fails closed |
| Duplicate-backup outcome | Controlled skip, never a failure |
| Success criterion | Reconciled post-operation provider evidence |
| Phase C start | **Unblocked — Phase B exit gate closed** |
| Operation precedence when several are due | Transaction log → full → differential; one operation per database per sweep |
| Backup destination selection | Trusted `BackupDestinationKey` only; caller-supplied paths/UNC/URLs prohibited |
| Verification database naming | Reserved system-controlled namespace; cleanup eligibility is a conjunction |
| `msdb` role | Supporting evidence; Platform authoritative |
| Customer-managed backup owner | Customer / DBA |
| Shared single-tenant restore | **Not supported** |
| RPO/RTO fields in V1 | Deferred |
| Provider | SQL Server only in V1 |
| `TS-Provider` | Deferred |

---

# Revision History

| Version | Date | Author | Change |
|---|---|---|---|
| 1.0 | 2026-08-14 | Solution Architecture Team | Initial decision: backup and recovery orchestration for physical tenant databases — per-database policy and chains, recovery readiness as an independent fourth dimension with its own writer, separate `BackupManagementMode`, separate backup credential, SQL Server full/differential/log semantics with no automatic recovery-model change, platform orchestration and `sp_getapplock` ownership in a namespace distinct from migration, persisted run history, no platform-side deletion in V1, two-level restore verification, and a binding pre-cutover recovery-readiness gate requiring an actual restore verification. Pre-approval revisions arising from architecture review: managed backups of all types may coexist with schema migration (the earlier full/differential exclusion was unnecessary and unenforceable across distinct lock namespaces, and is withdrawn along with its "half-applied schema" rationale, leaving I/O contention as a scheduling preference); session-scoped ownership is stated as planned rather than proven, with a binding Phase B empirical session-loss validation, an in-flight backup detection guard, a controlled duplicate-backup skip outcome, post-operation evidence as the success criterion, and Phase C blocked on that gate; destination selection restricted to a trusted key by compliance rule; verification-database cleanup bound to a reserved system-controlled namespace and an eligibility conjunction; durability-affecting policy changes explicitly audited |
| 1.1 | 2026-08-15 | Solution Architecture Team | Records the outcome of the Phase B empirical work and unblocks Phase C. The §14 session-loss gate is **closed**: abrupt client-process termination during an active `BACKUP` was validated against a disposable database across three conclusive trials, and in each the server-side request was gone before another worker could acquire ownership. The observed margins — 10 ms, 1 ms, 1 ms — are recorded as a local observation at the granularity of the measurement itself, explicitly **not** as a portable timing guarantee, so session-scoped ownership alone is not accepted as the sole duplicate-operation defence. In-flight backup detection is therefore promoted from conditional to **mandatory V1 behaviour**, on two grounds: the margin is too small to rely on, and backups started outside the platform take no platform lock and are structurally invisible to ownership. A server-visibility precondition is added after Phase B established that `sys.dm_exec_requests` silently narrows to the caller's own session rather than failing when visibility is absent — the provider must confirm visibility before trusting an empty result, must fail closed when it cannot, and on SQL Server 2022 requires `VIEW SERVER PERFORMANCE STATE` rather than `VIEW SERVER STATE` and never `sysadmin`. The guard is stated to be a correctness precondition rather than an operational feature flag, and not disableable by configuration. Adds Phase C scheduler operation precedence — transaction log, then full, then differential, one operation per physical database per sweep — with the distinction that an externally taken backup may change SQL Server chain state without satisfying the platform's schedule. Compliance rules 29–31 added; rule 25 marked satisfied. |
| 1.2 | 2026-08-15 | Solution Architecture Team | Settles the Phase D restore-verification architecture that was blocking implementation. **Topology:** in production, actual restore verification runs on a dedicated verification SQL Server instance and never on the instance hosting the authoritative production database, with same-instance verification permitted only in explicitly configured non-production environments. The target is selected through a trusted closed configuration key, and verification uses a privileged identity distinct from the runtime, migration and backup identities, under least privilege and never `sysadmin`, with the exact grant set an empirical implementation exit gate rather than an architectural assertion. **Depth:** verification must exercise the recovery capability the active policy claims — the full baseline, plus the latest *applicable* differential where differential protection is active, plus the required subsequent platform-managed log backups in order where log recovery is active, recovering only at the end — and a differential is not a precondition for log verification. The ADR fixes the guarantee that the selected artifacts form a valid deterministic SQL Server restore sequence while explicitly leaving the chain-selection mechanics to be validated against SQL Server by real restore tests, adopting no simplified formula as normative. **Chain ownership:** verification proves the platform's own artifacts, never external ones, and an incomplete platform-owned sequence is a recorded chain break with degraded readiness rather than a silent fallback to a shallower restore, while external copy-only activity that disrupts nothing does not degrade readiness. **`Protected` semantics:** where a policy does not require periodic restore verification, readiness is not `VerificationOverdue` for that reason alone and `Protected` is reachable without an actual restore, with first dedicated activation remaining a separate and stricter gate that additionally requires a successful actual restore verification related to the baseline being activated; `VerificationOverdue` is given exact semantics and distinguished from a verification that ran and failed. Also records the durable safety rules that follow: a durable verification operation record sufficient to survive a process crash, destructive cleanup only under positive correlation to that record rather than a name pattern, `RESTORE ... WITH REPLACE` prohibited, mandatory relocation of every restored file to trusted verification-owned paths, fail-safe behaviour when a target name already exists, cleanup outcome recorded separately from verification outcome so a failed drop never discards genuine recovery proof, `VERIFYONLY` as supplementary evidence only, `DBCC CHECKDB` deferred, separate verification scheduling with one effective owner per due verification and no global fleet lock, and retention expected to outlast verification with shortfalls reported as drift. **Multi-instance:** at most one effective verification per physical database per due verification state, with the serialising event required to cover **admission** of the operation rather than only ownership of an already-created record, since two instances that each create their own record can each claim it — the mechanism is left to implementation. **Operator semantics:** `Protected` is stated to mean that the obligations of the active policy are currently satisfied, explicitly **not** that the database has necessarily been restore-tested, with restore-provenness remaining separately observable through verification evidence. **Failure semantics:** a verification failure maps to readiness by depth — unrestorable baseline, failed usability probe or unreconstructable required path is `Unprotected`; a deeper segment failing on a restorable baseline is `Degraded`; a cleanup-only failure leaves readiness unchanged — and a verification that could not begin for reasons independent of the artifacts is not evidence of unrecoverability. Also pins the Level C recovery point to the selected contiguous log sequence rather than a moving target, requires an unusable verification target to fail closed with no same-server fallback, and records `BULK_LOGGED` as chain-valid while claiming no arbitrary point-in-time recovery through a minimally logged interval. Compliance rules 32–45 added; Phase C marked delivered and Phase D marked architecture-defined. |
| 1.3 | 2026-08-25 | Solution Architecture Team | Status corrected from `Proposed` to **Accepted**. No decision changed. Phases A, B and C ship under four architecture suites and eight SQL Server integration suites, the §14 session-loss gate is closed, and `ADR-020` gates cutover on this ADR's readiness dimension. Acceptance is inferred from that use rather than recorded in a closed decision, and is named as an inference (`DEC-L-020`); it covers the decisions, not the completion of Phases D and E. `ADR-021` remains `Proposed` — it bounds what this ADR excludes rather than supporting what it does. |
