---
id: ADR-021
title: Customer-Managed Tenant Database Connectivity and Operations
category: Architecture Decision Record
version: 1.1
status: Proposed
date: 2026-08-13
owner: Solution Architecture Team
tags:
  - architecture
  - multi-tenancy
  - persistence
  - connectivity
  - security
  - operations
depends_on:
  - ADR-015
  - ADR-016
  - ADR-017
  - ADR-018
  - ADR-019
  - ADR-020
used_by:
  - Platform
---

# ADR-021: Customer-Managed Tenant Database Connectivity and Operations

---

# Status

**Proposed**

Owns the decisions specific to tenant ERP databases that are **physically hosted by the customer**. `ADR-017` establishes `HostingMode` and the routing model; this ADR defines what `CustomerManaged` actually requires to operate — connectivity, credentials, authentication, health, outage behaviour, migration ownership, environment compatibility, backup/restore/monitoring ownership, onboarding, and the support boundary.

**Implementation scope: architecture-ready, implementation deferred.** Nothing here is scheduled for V1. The model exists so that a customer-hosted deployment is a configuration and operations exercise rather than a redesign, and so that decisions made now (routing, health, migration authority) do not foreclose it. This ADR moves to Accepted, and its pending items are resolved, when a customer-hosted deployment is actually contracted.

---

# Context

`ADR-017` introduces two independent placement dimensions — `HostingMode` (`PlatformManaged` / `CustomerManaged`) and `StorageMode` (`Shared` / `Dedicated`) — and permits three combinations, of which `CustomerManaged` + `Dedicated` is the one this ADR governs.

The scenario is specific: the customer consumes the SSAS ERP Web/API **hosted by us**, while their ERP business database runs on **their own SQL Server, inside their own infrastructure**. The application tier is ours; the data tier is theirs.

Everything that follows from that is operational rather than architectural. The entity model, migrations, repositories, `TenantId` filtering, and business code are identical to any other tenant database (`ADR-017`). What changes is that the platform no longer owns the server, the network path, the credentials, the backup regime, or — potentially — the authority to change the schema.

The current product baseline runs on a single platform-operated database. No repository or product document records a committed customer-hosted deployment, which is why this ADR is architecture-ready rather than V1-scoped.

---

# Problem Statement

Without these decisions:

- Customer endpoint credentials would have nowhere safe to live, and would end up in connection strings in the Platform database or in configuration.
- The application would attempt to reach customer databases over whatever network path happened to work, including the public internet.
- A customer-side outage would surface as raw SQL exceptions, or worse, as an inability to log in at all.
- The orchestrator would apply DDL to databases the customer's own DBA is contractually responsible for.
- Operators could not tell whether a failing tenant was our problem or the customer's, and support would have no boundary.
- Backup and restore expectations would be assumed rather than agreed, and discovered during an incident.
- Onboarding would be improvised per customer, with no definition of when the database is actually usable.

---

# Decision

## 1. What `CustomerManaged` means

`HostingMode = CustomerManaged` means the physical SQL Server hosting the tenant ERP database is **owned and operated by the customer**. SSAS ERP connects to it as a client over an approved secure network path. The platform does not own the server, its patching, its capacity, its backups, or its availability.

The application tier, Platform database, authentication, platform authority, and tenant membership remain entirely platform-operated and unaffected.

## 2. `CustomerManaged` implies `Dedicated`, and `Dedicated` implies an owner

A customer-managed database serves **exactly one customer's tenant(s)**. `CustomerManaged` + `Shared` is invalid and must be rejected wherever `TenantDatabase` rows are created, not merely discouraged: placing another customer's rows inside a database the customer controls, backs up, and can query directly is not an acceptable outcome under any configuration.

That prohibition alone is **not sufficient**. It prevents *many* tenants sharing a customer database; it does not prevent the *wrong single* tenant being placed there. Every `CustomerManaged` `TenantDatabase` therefore carries a trusted **authorized-owner binding** (`ADR-017`), and assignment creation **must validate** that the assigned `TenantId` matches it.

No platform administrator may assign Tenant B to Tenant A's customer-owned database — by error or by intent — merely because both are `Dedicated`. Ownership transfer is deliberately **out of scope**; it would require an explicitly designed transfer workflow, not a metadata edit.

`CustomerManaged` + `Dedicated` therefore means **dedicated to the authorized customer**, not merely "only one assignment happens to exist".

## 3. Supported connectivity models

The application infrastructure must be able to reach the customer SQL Server over a **secure, non-public** path. Acceptable models, in rough order of preference:

- private VPN or site-to-site VPN between platform and customer networks;
- private network peering;
- private endpoint / private link connectivity where the hosting environment offers it;
- fixed application egress IP combined with a customer firewall allow-list.

Any other path requires explicit approval as part of onboarding. The connectivity model is agreed per customer and recorded with the endpoint.

Where VPN or private connectivity is used, the endpoint **must be bound to the registered network attachment** for that customer. Being merely *reachable* is not sufficient — reachability is a property of the network at a moment in time; the binding is what makes the route intentional.

## 4. Public SQL connectivity policy

**Public internet SQL connectivity is prohibited in V1.**

There is no "exceptional, with sign-off" path. An earlier draft permitted one, and that is the wrong shape for this control: a documented exception path is precisely what gets taken during a first onboarding when the private connectivity is late and the go-live date is fixed, by people under pressure who are not weighing the security argument. Private or customer-approved network connectivity is **required**.

Future support requires an explicit **ADR amendment** with a named decision-maker — not a configuration flag and not an operational judgement call.

## 4a. Endpoint network targeting controls

A `CustomerManaged` `Endpoint` is not merely a connection setting; it is **network-routing configuration that the application's SQL connector will dial**. Left unconstrained it turns the data-access layer into a general-purpose internal network probe, reachable by anyone who can edit storage configuration. The following controls are binding.

**Approved ranges.** An endpoint may resolve **only** into registered, approved, customer-specific network ranges established during onboarding. Free-form host strings are not acceptable.

**Rejected targets.** The following must be rejected outright:

- loopback (`127.0.0.0/8`, `::1`);
- link-local and cloud instance metadata addresses (`169.254.0.0/16` and equivalents);
- platform-internal infrastructure ranges;
- another customer's approved ranges;
- arbitrary internet addresses outside the approved set.

**Port allow-list.** Only explicitly approved SQL port(s) may be used. The port is part of the approved endpoint registration; arbitrary destination ports are prohibited.

**Protocol.** Only the approved SQL Server provider and protocol may be selected. Endpoint configuration **must not** act as a generic URI, a provider selector, or a network proxy.

**When validated.** Validation occurs **both** when the endpoint is configured **and** before or at connection time. Configuration-time validation alone is insufficient, because what a name resolves to can change after it is approved.

**DNS re-validation.** Where DNS names are permitted, resolved addresses must remain inside the approved customer network boundary. Resolution is **re-validated periodically and on connectivity health checks**, and a hostname that later resolves outside the approved range **fails closed**. This is largely moot where private connectivity is mandatory — one more reason to require it — but must hold wherever DNS is in the path.

## 5. TLS and certificate trust policy

Customer-managed connectivity **requires encrypted transport** — `Encrypt=True` or the platform-equivalent secure SQL transport setting.

Minimum production trust requirements, binding:

1. **Encrypted transport is required.**
2. **The server certificate chain must validate to an explicitly trusted root.**
3. **The certificate hostname must match the configured endpoint.**
4. **A customer or enterprise CA may be explicitly onboarded** into the platform trust store and recorded against that endpoint. This is the accommodation for customers who do not use publicly trusted certificates — an explicit, audited trust decision rather than a blanket bypass.
5. **`TrustServerCertificate=true` is prohibited in production.** It discards the authentication half of TLS while keeping only encryption, leaving the connection encrypted to whoever answered.
6. **No emergency production override path exists in V1.** A "temporary" exception here is permanent in practice, and would be taken during exactly the incident where it matters most.

*Implementation decision pending: the PKI product, whether pinning is used in addition to chain validation, and how certificate rotation is coordinated with the customer without an outage.*

## 6. Secret and credential handling

The Platform database stores **only** an opaque `CredentialSecretReference` (conceptually `tenant-db/customer-9001`) alongside the trusted routing metadata. It **must never** store a username/password connection string, a plaintext SQL password, a certificate, or a private key.

The secret store resolves that reference to whatever authentication material the endpoint requires. The reference itself carries no authority: it is safe to persist, safe to log, and useless without access to the secret store.

That last claim holds **only if the reference is constrained**. Three binding controls make it true:

**6a. Constrained format and namespace.** A `CredentialSecretReference` **must** match a validated format and resolve **only** within a dedicated tenant-database credential namespace. It **must not** accept an arbitrary URI, relative traversal, an absolute secret-store path, or any foreign namespace.

**6b. Secret-store permission containment.** The application identity that resolves tenant database credentials **must** have access **only** to that tenant-database credential namespace. It **must not** be able to resolve unrelated secrets — JWT signing keys, other application secrets, other connector credentials — merely because a reference string points at them.

Together these close a serious escalation path. Without them, the reference is a string the platform hands to a secret store; a crafted or mistaken value could reach material with nothing to do with tenant storage, turning the ability to edit storage configuration into the ability to read signing keys. Containment must be enforced by the **store's own access control**, not only by validating the string — validation is the first gate, the IAM scope is the boundary.

**6c. Binding to the owning database.** A `CredentialSecretReference` is **bound to its owning `TenantDatabase`**. Changing or reassigning it is an explicit, platform-admin-authorized, audited action, after which **connectivity is re-validated**. Cross-database credential swaps must not happen silently, and must not look like ordinary metadata edits.

Repository and deployment standards govern which secret store is used; this ADR does not select one. *Implementation decision pending: secret-store selection, reference naming convention, and credential rotation procedure — including how rotation is coordinated with a customer who owns the credential.*

## 7. Supported authentication modes

`AuthenticationMode` is recorded per endpoint (`ADR-017`). The architecture accommodates `PlatformCredential`, `SqlAuthentication`, `WindowsIntegrated`, `ManagedIdentity`, and `Certificate`.

**Architectural extensibility is not a V1 support promise.** The minimal initial recommendation for `CustomerManaged` endpoints is **`SqlAuthentication` resolved through `CredentialSecretReference`** — the one mode that works across arbitrary customer network topologies without requiring a shared directory, a common cloud identity plane, or platform membership in the customer's domain.

`WindowsIntegrated`, `ManagedIdentity`, and `Certificate` are added only when a concrete customer requires them, each with its own trust and rotation design.

## 8. Runtime versus migration credentials

Customer-managed credentials follow **least privilege**. The application's runtime credential **must not** be assumed to be `sysadmin` or `db_owner`.

**Runtime credential — minimum conceptual permission set:**

- `CONNECT` to the database;
- `SELECT`, `INSERT`, `UPDATE`, `DELETE` on the tenant ERP schema;
- `EXECUTE` **only** if future approved stored procedures require it.

**Explicitly not granted:** `CREATE`, `ALTER`, `DROP`, or any other DDL; `db_owner`; `db_ddladmin`; `sysadmin`; server-level roles; `VIEW SERVER STATE`. Permissions should be granted through a **dedicated database role**, not a fixed database role.

Note that `SELECT` on `__EFMigrationsHistory` falls inside this set, so **schema health requires no additional credential** — which removes one common reason to over-grant.

**Migration credential** — distinct from the runtime credential wherever possible. It carries approved DDL rights over the tenant schema plus the ability to write migration history, is permitted **only** where `MigrationManagementMode` allows the platform to migrate, and is ideally available only during the migration window. It **must not** be embedded in or merged with the runtime credential.

Splitting them means the always-connected credential is not the powerful one, and lets a customer DBA who will not grant DDL rights still grant runtime access.

**Where a customer provides a runtime credential only**, `MigrationManagementMode` is necessarily **`CustomerDba`**. The platform cannot and must not silently escalate, attempt DDL, or treat the absence of a migration credential as a temporary condition to work around.

*Implementation decision pending: the exact permission set per credential, expressed as a script or role definition the customer's DBA can review and apply. This must be produced before the first customer onboarding; do not overbuild it earlier.*

## 9. Connectivity health checks

Connectivity health is checked by the service defined in `ADR-018` (`ITenantDatabaseConnectivityHealthService`): a minimal probe, no business queries, recording result, latency, and timestamp, classifying failure as network, authentication, database-unavailable, or TLS where it can do so reliably, and never recording or displaying credential material.

`ConnectivityStatus` is a **separate dimension** from `SchemaStatus` and `ProvisioningStatus` (`ADR-018`). A customer database can be perfectly schema-current and simply unreachable; a single overloaded status could not express that, and operators could not distinguish a customer network incident from a failed release.

Customer-managed endpoints warrant a **shorter check cadence** than platform-managed ones, because the failure modes they add — VPN drops, firewall changes, customer maintenance — occur without any notification to us.

## 10. Outage behaviour

The Platform database remains available when a customer database does not. Therefore, during a customer-managed outage the following **continue to work**:

- **login** and **token refresh**;
- **platform authority evaluation**;
- **tenant membership resolution**;
- **platform support and administration surfaces**;
- **account, subscription, and other platform-only pages**, where the caller is otherwise authorized.

**ERP business operations fail in a controlled way** — a `TenantDatabaseUnavailable` result (or repository-conventional equivalent) surfaced through the established problem/response conventions.

Normal API execution **must not** fall through to a raw `SqlException`, connection timeout, or `InvalidOperationException`. The response must not disclose endpoint, credential, or infrastructure detail to the caller.

Equally binding, a storage outage **must not** be transformed into:

- an **authentication failure**;
- **session invalidation** or shortened session lifetime;
- **tenant reassignment** or re-evaluation of placement.

A user must be able to sign in, reach their platform-only surfaces, see a clear statement that their ERP data store is unavailable, and — critically — not be told that their account is broken. The distinction between *your account is fine, your data store is unreachable* is the entire practical value of keeping authentication Platform-database-only, and it must be implemented as a requirement rather than left to emerge.

## 11. Schema health is mandatory

Customer-hosted databases are **not exempt** from schema verification. Every physical tenant database is checked against the deployed Tenant EF migration catalog versus its actual `__EFMigrationsHistory` (`ADR-018`). Where the application expects `M047` and the customer database reports `M045`, the result is `PendingMigrations` / upgrade-required and that tenant's ERP traffic is gated exactly as it would be for a platform-hosted database.

Migration ownership changes **who acts**, never **whether we verify**.

## 12. Migration management modes

`MigrationManagementMode` is recorded per physical database and is **independent of `HostingMode`** (`ADR-018`), with a deliberately distinct vocabulary so the two never read as the same setting. For customer-managed endpoints both of these are normal:

- **`PlatformAfterApproval`** — the customer permits SSAS ERP to apply approved migrations, under an agreed window and with a migration-capable credential.
- **`CustomerDba`** — the customer's DBA requires control; the platform never applies DDL.

The orchestrator **must** consult the mode and **must not** execute DDL where it is denied. Absent approval is denial, not default-allow.

## 13. Customer DBA migration workflow

Where the customer's DBA executes migrations (`ADR-018` Model B):

1. the platform detects pending migrations through normal schema-health comparison;
2. the platform makes **no** modification to the database;
3. the required approved migration package is identified/provided;
4. the customer DBA executes it in their own change process;
5. the platform re-runs schema health and verifies the **actual** `__EFMigrationsHistory`;
6. only then is the database treated as compatible and ungated.

Verification is on observed history, never on assertion. Migration package generation is **not** implemented now; the requirement is recorded so reporting and health can accommodate it.

Customer-managed databases also carry materially higher **schema-drift** risk, since a DBA can alter a table, column, index, constraint, or procedure with no migration recorded — leaving history compatible while the schema is not. `ADR-018` keeps `MigrationHistoryCompatibility` and `SchemaDrift` distinct for exactly this reason.

**Migration-history compatibility alone is not sufficient to onboard a customer-managed database.** Ready additionally requires the **mandatory drift floor** defined in `ADR-018`: expected table existence; expected column existence, type, and nullability; primary key and unique constraint verification; a stored schema fingerprint; trigger detection on tenant-owned tables; and verification of the required database settings. These are catalog queries and a hash — a small cost against operating a database we do not control, and against fielding support for symptoms that look like product defects.

## 14. SQL Server compatibility policy

Customer-managed support requires an **explicit environment compatibility policy**, because we no longer choose the server. It must define:

- supported SQL Server versions and editions;
- required database compatibility level;
- required features and any prohibited configurations;
- collation expectations, where the model depends on them;
- **RCSI requirements**, where tenant persistence assumes read-committed snapshot behaviour — note that EF Core's SQL Server database creator enables RCSI by default on databases it creates, so a customer-created database may differ from every database the product has been tested on;
- TLS version support;
- any minimum/maximum server settings the product relies on.

Exact version numbers are **not** decided here, because the current product baseline does not state them. *Requires follow-up decision: the concrete supported-environment matrix.* It **must** be produced and verifiable before the first customer onboarding — verification of the environment is part of the Ready criteria below, and cannot be performed against an undefined policy.

**Azure SQL is out of scope for V1 Tenant ERP databases**, consistent with `ADR-018`. Future support requires an ADR amendment. This is settled rather than deferred because it changes migration locking, the connection and failover model, feature availability, and cutover assumptions — nothing in this ADR should be read as implying Azure SQL compatibility for a customer-hosted endpoint.

## 15. Backup ownership

For customer-managed databases, **backup responsibility belongs to the customer** unless a contract explicitly states otherwise. The platform does not control the server, its storage, its backup schedule, or its retention.

This must be stated contractually and reflected operationally: platform tooling **must not** imply platform-managed backup capability for a `CustomerManaged` endpoint, and any backup/restore affordance in operator tooling must be visibly unavailable or explicitly marked as customer-owned for those rows.

## 16. Restore ownership

Restore likewise requires **customer DBA involvement** for customer-managed databases. The platform can state what schema version a restored database must reach to be usable, and can verify it afterwards, but cannot perform or guarantee the restore.

Point-in-time recovery expectations, recovery time objectives, and who initiates a restore are contractual, not platform-determined.

## 17. Monitoring responsibilities

Split explicitly:

| Concern | Owner |
|---|---|
| Application tier, Platform database, authentication | SSAS ERP |
| Reachability and authentication *from* the platform *to* the endpoint | SSAS ERP (connectivity health) |
| Schema compatibility of the tenant database | SSAS ERP (schema health) |
| SQL Server health, capacity, storage, patching, performance | Customer |
| Network path availability on the customer side | Customer |
| Backup execution and verification | Customer |

The platform monitors what it can observe **through** the connection, and reports it. It does not monitor the customer's server.

## 18. Incident and support boundary

The boundary follows ownership. When connectivity health reports `Unreachable`, `AuthenticationFailed`, or a TLS failure for a customer-managed endpoint, the probable cause is on the customer side and the platform's obligation is to **detect, classify, report, and communicate** — not to resolve.

Operator tooling must make `HostingMode` immediately visible so an incident is triaged correctly from the first minute, rather than being investigated as a platform fault. Support agreements should state response expectations for both sides, since a customer-side outage produces a customer-visible product outage that we did not cause and cannot fix.

## 19. Onboarding requirements

Customer-managed onboarding is a **distinct workflow** from platform-managed provisioning, and their assumptions must not be applied to each other. Platform-managed provisioning creates the database; customer-managed onboarding connects to and verifies a database the customer may have created.

Conceptual flow:

1. create/approve the `Tenant`;
2. create the `CustomerManaged` `TenantDatabase` record;
3. configure the secure network connectivity path;
4. configure the credential secret reference;
5. test connectivity;
6. verify SQL Server environment compatibility;
7. verify or create the expected database per the contract;
8. apply or verify the tenant schema, per `MigrationManagementMode`;
9. seed required system data, where permitted;
10. run schema health;
11. run connectivity health;
12. only then create the `TenantDatabaseAssignment` and mark the endpoint Ready.

Not implemented by this ADR.

## 20. Ready criteria

A `CustomerManaged` database is **Ready** only when **all** hold:

- routing metadata is valid, and the endpoint passes the network targeting controls of decision 4a;
- the authorized-owner binding is present and matches the assigned tenant;
- the credential secret is configured, resolvable, and bound to this database;
- the network path is reachable;
- authentication succeeds;
- the database exists;
- the SQL Server environment is verified against the supported-environment policy;
- the schema is compatible per `__EFMigrationsHistory`;
- the mandatory drift floor passes and a schema fingerprint is stored;
- required seed data is present;
- a valid `TenantDatabaseAssignment` exists.

`CREATE DATABASE` is **not applicable** as a readiness step where the customer's DBA owns database creation. Ready is a conclusion drawn from the status dimensions plus routing and credential validity — never an independently writable flag.

## 21. Data residency implications

Customer-managed hosting is the natural answer where a customer requires ERP data to remain inside their own infrastructure or jurisdiction.

The routing model supports this **without any change to HR, GL, Payroll, or other business code** — placement is a routing fact, not a domain fact, and the entity model, migrations, and `TenantId` filtering are identical (`ADR-017`). Residency is achieved by *where the connection points*, not by a variant of the application.

### Customer data deletion

**Platform deletion of a tenant or account does not imply deletion of a customer-managed database.** The platform does not own that data and has no authority to destroy it.

On tenant deletion the platform may: remove the assignment, disable the endpoint, revoke the credential secret reference, and stop connecting. **The customer retains their data and its disposition**, unless a contract explicitly governs another process. A tenant-deletion workflow that drops, truncates, or otherwise destroys a customer-owned database is prohibited (`ADR-017`).

## 22. No automatic storage fallback

If a customer-managed database is unavailable, the tenant **must not** be routed to platform-managed shared storage, platform-managed dedicated storage, or any other database. The correct behaviour is the controlled unavailability of decision 10.

Database placement is an explicit **security and data-residency boundary** (`ADR-017`). A fallback would read or write customer ERP data in a location the customer did not agree to — precisely the outcome customer-managed hosting exists to prevent — and would do so silently, during an incident, when nobody is watching. An outage is the better failure.

## 23. Administrative visibility

Future Platform Admin tooling shows, per the `ADR-019` grid: Tenant, Hosting Mode, Storage Mode, server/endpoint display name, Database, Connectivity, Schema Status, Current Migration, Expected Migration, Migration Management Mode, and the placement/metric columns.

A customer-managed detail view shows conceptually:

```
Hosting Mode:            Customer Managed
Database endpoint:       display-safe host / server alias
Database:                CustomerERP
Authentication:          configured / missing        (never the secret)
Connectivity:            Healthy / Unreachable / Auth Failed / ...
Schema:                  M047  (expected M047)
Migration Management:    Customer DBA
Last connectivity check: <timestamp>
Last schema check:       <timestamp>
Last migration verified: <timestamp>
```

**Secret display is prohibited.** Operator tooling must **never** show a password, a full connection string containing a secret, or a private certificate or key. It may show `Credential configured: YES/NO`, and the secret reference alias only where that is safe and useful to platform operators.

## 24. Audit requirements

Because customer-managed endpoints and credentials are security-sensitive platform configuration, the following are audited: creating or registering an endpoint; changing endpoint address or database name; changing the credential secret reference; changing `AuthenticationMode` or `MigrationManagementMode`; changing the authorized-owner binding; running a connectivity test; disabling or re-enabling an endpoint; creating, changing, or removing a `TenantDatabaseAssignment`; and any migration applied by the platform.

Each audit record carries: **`ActorIdentityId`**, **`ActingPlatformSupportPrincipalId`** (the authority under which the action was taken), **`SessionId`** for correlation, **before and after values** for configuration changes, an **approval reference** where the action required one, the **execution actor**, **timestamp**, **outcome**, and **failure reason**. Approver and executor may legitimately differ and are recorded separately.

Actor identity alone is insufficient: for a platform-plane action the meaningful question is under which *authority* it was taken, which is what `ADR-016` governs.

**Audit records must never contain credential material** — no password, resolved secret, connection string containing a secret, private key, or authentication token. A secret reference identifier or alias may be recorded where safe.

**Only authorized Platform administrators** may create, change, test, disable, or reassign customer-managed database routing, consistent with the platform-plane authority model of `ADR-015`/`ADR-016`. Tenant ordinary users **must not** control physical connectivity, endpoint configuration, or placement in any form.

### Storage administration permissions

Storage administration **should not** permanently reuse `Platform.Support.Administer`. That permission governs platform-support authority itself; conflating the two would mean anyone able to administer support principals could also repoint a customer's database.

*Requires follow-up decision: a dedicated permission family following the existing `Platform.{Resource}.{Action}` catalog convention* — conceptually `Platform.TenantStorage.View`, `Platform.TenantStorage.Administer`, and a separately-gated `Platform.TenantStorage.Cutover`, or the repository-conventional equivalent. **Cutover approval should be separable from ordinary storage metadata administration**, because its blast radius is data loss rather than misconfiguration. No permission catalog entries are added by this ADR.

## 25. Relationship to ADR-017/018/019/020

- **`ADR-017`** — owns `HostingMode`/`StorageMode`, the `TenantDatabase` model, `ServerKey` versus endpoint address, `CredentialSecretReference`, the routing trust chain, fail-closed routing, and the no-fallback rule. This ADR details the customer-managed side of those.
- **`ADR-018`** — owns schema health, connectivity health, the status dimensions, `MigrationManagementMode`, migration authority, and the `TenantDatabaseUnavailable` gate. This ADR applies them to customer-hosted endpoints and adds the environment and drift requirements.
- **`ADR-019`** — owns placement policy; `CustomerManaged` is fixed by contract/compliance and is excluded from the evaluator's output space.
- **`ADR-020`** — owns tenant movement; cross-hosting movement is future, not V1, with additional requirements recorded there.

---

# Decision Drivers

- Customer ERP data must never leave the location the customer agreed to.
- No credential material in the Platform database, under any hosting mode.
- A customer-side outage must degrade the product predictably, not catastrophically.
- Verification obligations must not weaken because we do not own the server.
- The support and responsibility boundary must be agreed before an incident, not during one.
- Customer-hosted deployments must not fork the application or the domain model.

---

# Alternatives Considered

## Option 1 – Do not support customer-hosted databases

### Advantages

- No connectivity, credential, environment, or support-boundary work at all.
- The entire estate stays within platform control and observability.
- Simplest possible operations and testing.

### Disadvantages

- Customers with binding infrastructure or residency policies cannot be served at all.
- The constraint would be discovered at contract stage, with no architectural answer available.
- Retrofitting later means revisiting routing, health, and migration authority — the exact decisions being made now.

## Option 2 – Ship the application on-premise to the customer

### Advantages

- Solves residency completely; data and application both stay with the customer.
- No cross-boundary connectivity or credential problem.

### Disadvantages

- A fundamentally different product and delivery model: per-customer deployment, versioning, upgrade, and support.
- Loses the hosted-service economics and release cadence entirely.
- Enormously more expensive than the actual requirement, which is only about where the *data* lives.

## Option 3 – Architecture-ready customer-managed hosting, implementation deferred (selected)

### Advantages

- The decisions that are expensive to reverse — routing model, health model, migration authority, credential handling — are made now, at negligible cost.
- No V1 implementation cost, no unused machinery, and no speculative operational surface.
- A real customer requirement becomes a configuration and operations exercise, not a redesign.
- Keeps one application, one domain model, and one release cadence.

### Disadvantages

- Nothing is proven until a real endpoint exists; the design carries unvalidated assumptions.
- Several decisions are deliberately left pending, so the ADR is not implementation-complete.
- Some model surface (`HostingMode`, `Endpoint`, `AuthenticationMode`, `MigrationManagementMode`) exists before it is exercised.

---

# Rationale

Option 3 is selected.

The asymmetry is decisive: the decisions that are cheap now and expensive later are precisely the architectural ones. Whether `HostingMode` exists separately from `StorageMode`, whether the orchestrator asks permission before applying DDL, whether connectivity is a distinct health dimension, and whether credentials are referenced rather than stored — all of these are nearly free to get right today and require re-interpreting persisted data and rewriting orchestration to add later. Meanwhile the genuinely expensive parts — VPN setup, certificate trust policy, per-customer permission scripts, environment matrices, support agreements — are all customer-specific and cannot usefully be built before a customer exists.

Option 1 is rejected because the cost of the architectural readiness is close to zero, while the cost of discovering the gap during a contract negotiation is a redesign of exactly the components being specified in `ADR-017` and `ADR-018`.

Option 2 is rejected because it answers a much larger question than the one asked. The requirement is that ERP *data* stay on customer infrastructure, not that the application do so; shipping the whole product on-premise changes the delivery model, the economics, and the support burden to solve a data-location problem.

Keeping authentication entirely within the Platform database is what makes the outage story acceptable. Without it, a customer VPN failure would mean users cannot log in — indistinguishable from a total product failure. With it, they log in, see a specific and honest message, and support knows within seconds which side of the boundary the fault is on.

The no-fallback rule deserves its emphasis. Falling back to platform storage during an outage is the kind of resilience feature that seems obviously good and is in fact a residency breach executed automatically, during an incident, without a human decision. Prohibiting it outright — rather than defaulting it off — is the only durable form.

---

# Consequences

## Positive

- Customers with infrastructure or residency constraints become addressable without forking the product.
- Credential material never enters the Platform database, for any hosting mode.
- A customer-side outage produces a clear, honest, correctly-triaged degradation instead of a mysterious failure.
- Verification obligations are uniform across the estate regardless of who owns the server.
- Responsibility for backup, restore, monitoring, and incident response is agreed in advance.
- Business code, migrations, and `TenantId` enforcement are completely unaffected.

## Negative

- A meaningful operational surface must eventually be built: connectivity health, endpoint configuration, secret resolution, environment verification, admin visibility.
- Product availability becomes partly dependent on infrastructure outside platform control.
- Several decisions are deferred, so a first onboarding will require resolving them under time pressure unless they are addressed early.
- Support becomes a shared-responsibility conversation, which is harder than owning the whole stack.
- Estate convergence during a release can be blocked by customer action we cannot compel.
- Model surface exists ahead of use, carrying a small ongoing comprehension cost.

---

# Implementation Guidelines

- Do not build any of this before a customer requirement exists; keep the model shape, defer the machinery.
- When a requirement does arrive, resolve the pending decisions in this order: supported-environment matrix → connectivity model → credential permission sets → certificate/trust policy → rotation procedure.
- Rehearse against a deliberately hostile simulated endpoint — one that is slow, drops connections, and fails authentication — before touching a real customer server.
- Verify the outage story explicitly: login must succeed and ERP must fail cleanly, as a tested behaviour rather than an assumption.
- Never let a connectivity diagnostic echo a connection string or credential into logs, telemetry, or an API response.
- Treat the first onboarding as a documented, reviewed runbook, not a one-off exercise.

---

# Compliance Rules

1. `CustomerManaged` **must** imply `Dedicated`; `CustomerManaged` + `Shared` **must** be rejected as invalid.
2. Every `CustomerManaged` `TenantDatabase` **must** carry an authorized-owner binding, and assignment **must** validate the tenant against it. Ownership transfer is out of scope.
3. Plaintext passwords, credentialed connection strings, certificates, and private keys **must not** be persisted in the Platform database; customer credentials **must** be reached through `CredentialSecretReference`.
4. `CredentialSecretReference` **must** match a validated format within a dedicated tenant-database credential namespace, and **must not** accept arbitrary URIs, traversal, absolute store paths, or foreign namespaces.
5. The application identity resolving tenant database credentials **must** be scoped to that namespace only, and **must not** be able to resolve unrelated platform secrets.
6. A credential reference **must** be bound to its owning `TenantDatabase`; reassignment **must** be authorized, audited, and followed by connectivity re-validation.
7. Endpoints **must** resolve only into registered, approved, customer-specific network ranges; loopback, link-local/metadata, platform-internal, other customers' ranges, and arbitrary internet targets **must** be rejected — validated at configuration time **and** at connection time.
8. Only approved SQL port(s) and the approved SQL provider/protocol may be used; endpoint configuration **must not** act as a generic URI, provider selector, or proxy.
9. Where DNS is used, resolution **must** be re-validated and **must** fail closed if it moves outside the approved boundary.
10. **Public internet SQL connectivity is prohibited in V1**; future support requires an ADR amendment.
11. Customer-managed connectivity **must** use encrypted transport, **must** validate the certificate chain to an explicitly trusted root, and **must** match the certificate hostname to the endpoint; `TrustServerCertificate=true` is **prohibited in production** with no override path.
12. The runtime credential **must not** hold DDL rights, `db_owner`, `db_ddladmin`, `sysadmin`, or server-level roles; the migration credential **must** be distinct and permitted only where `MigrationManagementMode` allows.
13. Where only a runtime credential exists, `MigrationManagementMode` **must** be `CustomerDba`; the platform **must not** escalate.
14. Schema-health verification **must** apply to customer-hosted databases; they are **not** exempt.
15. Customer-managed Ready **must** additionally satisfy the mandatory drift floor of `ADR-018`.
16. The orchestrator **must not** apply DDL where `MigrationManagementMode` denies it; customer-executed migrations **must** be verified against observed `__EFMigrationsHistory`.
17. A customer-database outage **must not** prevent login, token refresh, platform authority evaluation, tenant membership resolution, or access to platform-only surfaces, and **must not** invalidate sessions or trigger reassignment.
18. ERP failure due to tenant-storage unavailability **must** surface as a controlled result, never a raw data-access exception, and **must not** disclose endpoint or credential detail.
19. A tenant **must never** be automatically routed to a different database when its assigned database is unavailable.
20. Operator tooling **must not** display passwords, credentialed connection strings, or private keys, and **must not** imply platform-managed backup capability for customer-managed endpoints.
21. Only authorized Platform administrators may create, change, test, disable, or reassign customer-managed database routing; tenant users **must not** control physical connectivity or placement.
22. Audited actions **must** record actor identity, acting platform-support principal, session, before/after values, approval reference, executor, timestamp, and outcome, and **must never** contain credential material.
23. Platform deletion of a tenant or account **must not** destroy a customer-managed database.
24. Azure SQL **must not** be treated as a supported customer-managed target without an ADR amendment.
25. A customer-managed database **must not** be marked Ready until every criterion in decision 20 holds.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Customer network or server outage makes the product appear down | Authentication stays Platform-database-only; controlled `TenantDatabaseUnavailable` result; connectivity health identifies the cause immediately |
| Credentials leak via Platform DB, logs, telemetry, or admin UI | Only opaque `CredentialSecretReference` persisted; classified (not verbatim) probe failures; secret display prohibited; credential material forbidden in audit |
| **Crafted secret reference reaches unrelated platform secrets, including signing keys** | Validated reference format, dedicated credential namespace, and secret-store IAM containment scoped to that namespace |
| Credential reference silently swapped between databases | Reference bound to its owning `TenantDatabase`; reassignment authorized, audited, and re-validated |
| **Endpoint configuration used to reach internal systems or cloud metadata** | Approved customer network ranges, explicit rejection of loopback/link-local/internal/other-customer targets, port allow-list, SQL-protocol only, validated at configuration and connection time |
| Approved hostname later resolves elsewhere | DNS re-validation on the health cadence, failing closed outside the approved boundary |
| One customer's tenant assigned to another customer's endpoint | Authorized-owner binding validated at assignment; transfer out of scope |
| Public SQL exposure is adopted for onboarding speed | **Prohibited in V1**; no sign-off path exists, and enabling it requires an ADR amendment |
| `TrustServerCertificate` is enabled to make a connection work | Prohibited in production with no override path; chain and hostname validation binding, customer CA onboarding provided as the legitimate accommodation |
| Customer DBA changes schema without a migration | `MigrationHistoryCompatibility` and `SchemaDrift` kept distinct; mandatory drift floor and schema fingerprint required before onboarding |
| Platform tenant deletion destroys customer-owned data | Deletion defined as revoke-and-disconnect; destruction of customer-owned databases prohibited |
| Unsupported SQL Server version or configuration causes subtle failures | Environment compatibility policy required and verified as part of Ready criteria |
| RCSI or collation differs from every tested database | Explicitly named in the compatibility policy, including EF Core's RCSI-on-create default |
| Fallback to platform storage is added "for resilience" and breaches residency | Prohibited outright in `ADR-017` and here; recorded as a security boundary, not a preference |
| Incident is mis-triaged as a platform fault | `HostingMode` prominent in operator tooling; explicit ownership and support boundary |
| Backup is assumed to be ours and discovered otherwise during data loss | Ownership stated contractually; tooling must not imply platform backup capability |
| Pending decisions are resolved hastily during a first onboarding | Resolution order recorded; matrix and permission sets required before onboarding begins |

---

# Future Considerations

Revisit when: a customer-hosted deployment is actually contracted (moving this ADR to Accepted and resolving its pending items); additional authentication modes are required by a specific customer environment; movement between hosting modes becomes a requirement (`ADR-020`); schema-drift detection is promoted from recommended to mandatory; or the number of customer-managed endpoints grows enough that per-customer onboarding needs to become a self-service or templated process.

---

# Related Documents

- `ADR-015` — Platform-Plane Authentication and Authorization
- `ADR-016` — Platform-Support Bootstrap, Lifecycle, and Authority Administration
- `ADR-017` — Tenant Storage Topology and Routing
- `ADR-018` — Tenant Schema Health and Migration Orchestration
- `ADR-019` — Dynamic Tenant Placement Policy
- `ADR-020` — Shared-to-Dedicated Tenant Migration and Cutover

---

# Review Criteria

This ADR should be reviewed if:

- A customer-hosted deployment is contracted, or a concrete customer requirement emerges.
- A required authentication mode is not `SqlAuthentication`.
- The supported-environment matrix is defined, or a customer environment falls outside it.
- The support/responsibility boundary proves unworkable in a real incident.
- Movement between hosting modes is proposed.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | 2026-08-13 | Solution Architecture Team | Initial version — customer-managed tenant database connectivity and operations; architecture-ready, implementation deferred |
| 1.1 | 2026-08-13 | Solution Architecture Team | Review hardening: added endpoint owner binding; added network-targeting/SSRF controls, port and protocol constraints, and DNS re-validation; prohibited public SQL in V1; added the binding TLS trust rule; added secret reference format, namespace containment, IAM scoping and owner binding; specified runtime/migration credential permission sets and the `CustomerDba` inference; expanded outage behaviour to platform-only surfaces and session protection; raised drift to a mandatory Ready criterion; declared Azure SQL out of scope; added customer-data deletion, audit field set, and the storage permission-family follow-up |
