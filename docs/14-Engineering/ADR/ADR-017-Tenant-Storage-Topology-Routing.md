---
id: ADR-017
title: Tenant Storage Topology and Routing
category: Architecture Decision Record
version: 1.6
status: Proposed
date: 2026-08-13
owner: Solution Architecture Team
tags:
  - architecture
  - multi-tenancy
  - persistence
  - routing
  - hosting
  - security
depends_on:
  - ADR-005
  - ADR-008
  - ADR-011
  - ADR-013
  - ADR-015
used_by:
  - Platform
  - FP-003
  - HR
  - GL
---

# ADR-017: Tenant Storage Topology and Routing

---

# Status

**Proposed**

Proposed as the storage-topology decision gate that must be resolved **before** substantial tenant-owned ERP persistence (HR, GL, Payroll, Sales, Purchasing, Inventory) is implemented. It does not supersede `ADR-015`/`ADR-016`: the two-plane security model, the platform token profile, and platform-support authority remain in force and are unaffected. It is the parent decision for `ADR-018` (schema health and migration orchestration), `ADR-019` (dynamic placement policy), `ADR-020` (shared-to-dedicated migration and cutover), and `ADR-021` (customer-managed tenant database connectivity and operations).

---

# Context

The solution currently runs on **one physical database**. A single concrete `PlatformDbContext` (deriving from the shared `PersistenceDbContext` in BuildingBlocks) is registered with `services.AddDbContext<PlatformDbContext>` against `ConnectionStrings:Platform`. There is no `TenantDbContext`, no `IDbContextFactory`, and no notion of a physical tenant database.

Tenant isolation is logical and automatic. Entities that implement the marker `ITenantOwnedEntity { Guid TenantId }` receive an EF Core global query filter applied by reflection in `PersistenceDbContext.OnModelCreating`:

```
CurrentTenantId.HasValue && entity.TenantId == CurrentTenantId.Value
```

`CurrentTenantId` comes from `ICurrentTenant`, implemented by `CurrentTenant`, which reads the `tenant_id` claim from the **validated** `ClaimsPrincipal` via `IHttpContextAccessor`. An unauthenticated request yields `null`, so the filter matches nothing — the mechanism is fail-closed by construction and is never influenced by caller-supplied input beyond the server-issued token. `PersistenceDbContext` additionally forces `DeleteBehavior.Restrict` on every foreign key.

The tenant/platform split as implemented today:

| Tenant-owned (`ITenantOwnedEntity`, 8) | Platform-global (no `TenantId`, 12) |
|---|---|
| `Company`, `Role`, `RolePermissionAssignment`, `TenantUser`, `TenantUserRoleAssignment`, `TenantLocalizationOverride`, `TenantLocalizationOverrideVersion`, `TenantLocalizationSettings` | `Tenant`, `Identity`, `AuthenticationAccount`, `AccountActionToken`, `AuthenticationSession`, `RefreshTokenRecord`, `TenantSelectionTransaction`, `LocalizationCatalogState`, `PlatformSupportPrincipal`, `PlatformPermissionAssignment`, `PlatformAuthenticationSession`, `PlatformRefreshTokenRecord` |

Six foreign keys currently cross that boundary, **in both directions**:

| Direction | Constraint |
|---|---|
| Platform → tenant-owned | `AuthenticationSession → TenantUser`; `AccountActionToken → TenantUser` |
| Tenant-owned → platform | `TenantUser → Identity`; `Company → Tenant`; `TenantLocalizationOverride → Tenant`; `TenantLocalizationSettings → Tenant` |

The localization design already demonstrates the global-catalog-plus-tenant-override pattern: `LocalizationCatalogState` is platform-global while `TenantLocalizationOverride`/`Settings` are tenant-owned.

Business context: the product must be able to serve enterprise and regulated customers that require physical data isolation, per-tenant restore, or data residency, while remaining operationally affordable at thousands of small tenants.

A further class of requirement exists: a customer may consume the SSAS ERP Web/API **hosted by us** while requiring that their ERP business database be physically hosted on **their own SQL Server / their own infrastructure**. This is driven by data-residency, regulatory, procurement, or internal-policy constraints rather than by workload. It means the physical database endpoint is not always one the platform provisions, names, or credentials from its own configuration.

---

# Problem Statement

A decision is required now because the cost of changing tenant storage topology scales with the number of tenant-owned entities. Today there are **8** such entities and **6** cross-boundary foreign keys — a contained refactor. After HR and GL there will be dozens; after the full ERP surface, hundreds, at which point conversion becomes a rewrite that teams realistically do not attempt.

Risks of not deciding:

- Enterprise/compliance deals requiring physical isolation, or requiring the database to live on customer infrastructure, cannot be served, and the constraint is discovered late.
- Hosting location and isolation degree get conflated into a single "storage mode" flag, which cannot express "dedicated, but on the customer's server" and forces a second, incompatible mechanism later.
- Per-tenant restore remains impossible; a single tenant's data-loss incident requires logical, row-level reconstruction from a shared backup.
- A noisy tenant degrades all tenants with no isolation lever.
- Every additional ERP entity and cross-boundary relationship increases future conversion cost.

Desired outcome: a topology that is affordable at V1, does not require a domain rewrite later, and can place an individual tenant on isolated storage when justified — without changing that tenant's entity model, identifiers, or application code.

---

# Decision

Adopt a **hybrid-ready tenant storage topology**:

1. A **Platform database** holding tenancy, identity, authentication/authorization, platform-support authority, and all tenant-storage routing and operational metadata.
2. **One or more shared Tenant ERP databases** holding tenant-owned business data for many tenants, discriminated by `TenantId`.
3. **Optional dedicated Tenant ERP databases** for individual tenants when justified by `ADR-019` policy or by business/compliance requirement.
4. **Optional customer-hosted Tenant ERP databases**, physically running on the customer's own SQL Server, reached over an approved secure network path.
5. **One logical tenant model regardless of physical placement or hosting** — the same entity model, migrations, repositories, queries, and indexes apply to every tenant database.

## Two independent placement dimensions

Where a tenant database is **hosted** and how much it is **shared** are two orthogonal properties and are modelled separately. Collapsing them into a single flag is prohibited.

**`HostingMode`** — who owns and operates the physical SQL Server:

- `PlatformManaged` — the server and database are provisioned and operated by SSAS ERP.
- `CustomerManaged` — the server is owned and operated by the customer; SSAS ERP connects to it.

**`StorageMode`** — how many tenants share the physical database:

- `Shared` — many tenants, discriminated by `TenantId`.
- `Dedicated` — one tenant.

Supported combinations:

| `HostingMode` | `StorageMode` | Supported | Note |
|---|---|---|---|
| `PlatformManaged` | `Shared` | Yes | Default placement |
| `PlatformManaged` | `Dedicated` | Yes | Promotion target (`ADR-019`, `ADR-020`) |
| `CustomerManaged` | `Dedicated` | Yes | Governed by `ADR-021` |
| `CustomerManaged` | `Shared` | **No** | Prohibited |

`CustomerManaged` **implies** `Dedicated`. Customer-hosted storage is dedicated to that customer by definition; placing another customer's tenant rows inside a customer-owned database is not an acceptable outcome under any configuration, and the combination must be rejected as invalid data rather than merely discouraged.

## Conceptual topology

```
                     SSAS ERP Web/API
                           |
                     Platform DB
                           |
                TenantDatabaseResolver
                     /           \
                    /             \
                   v               v
      Platform-Managed         Customer-Managed
      Tenant Storage           Tenant Storage
         /      \                    |
      Shared   Dedicated          Dedicated
         DB       DB                 DB
                                  Customer SQL
                                     Server
```

## Implementation scope of customer-managed hosting

This ADR makes the architecture **customer-managed-ready**; it does **not** place customer-managed databases in the V1 implementation scope. The routing model, physical database model, health model, and migration model must accommodate `CustomerManaged` from the outset so that supporting it later is a configuration and operations exercise rather than a redesign. Actual implementation is **deferred until a real customer requirement exists**, at which point `ADR-021` moves from Proposed to Accepted and its pending decisions are resolved.

The current product baseline runs on a single platform-operated database and no repository or product document records a committed customer-hosted deployment; this scope call should be revisited if such a commitment is made.

## Platform database boundary

The Platform database owns:

- `Tenant` registry.
- `Identity`, `AuthenticationAccount`, `AccountActionToken`.
- Authentication/session infrastructure: `AuthenticationSession`, `RefreshTokenRecord`, `PlatformAuthenticationSession`, `PlatformRefreshTokenRecord`, `TenantSelectionTransaction`.
- Platform-support authority: `PlatformSupportPrincipal`, `PlatformPermissionAssignment`.
- **Tenant membership and access control: `TenantUser`, `Role`, `RolePermissionAssignment`, `TenantUserRoleAssignment`.**
- Global catalogs and reference definitions, including `LocalizationCatalogState`.
- **Tenant localization: `TenantLocalizationOverride`, `TenantLocalizationOverrideVersion`, `TenantLocalizationSettings`.**
- Subscription/plan metadata when introduced.
- Tenant-storage metadata: `TenantDatabase`, `TenantDatabaseAssignment`, provisioning state, schema-health metadata, placement policy, placement recommendations, placement overrides, and storage operational metadata.

**Tenant identity/access membership remains Platform database data even though it is tenant-scoped.** This is a deliberate refinement of the naive rule "everything `ITenantOwnedEntity` moves to the tenant database". The reasons are:

- Login, tenant selection, and refresh read membership and role data. Keeping it in the Platform database means **authentication remains a single-database operation** and never depends on tenant-database routing or availability.
- It removes three of the six current cross-boundary foreign keys (`AuthenticationSession → TenantUser`, `AccountActionToken → TenantUser`, `TenantUser → Identity`) without any workaround.
- **Customer-managed hosting makes this decisive.** A customer's SQL Server may be unreachable for reasons entirely outside platform control — customer network maintenance, VPN failure, firewall change, customer-side outage. Login, token refresh, platform authority evaluation, and tenant membership resolution **must not** depend on that server being reachable. A user must still be able to authenticate and receive a clear, controlled message that their ERP data store is unavailable, rather than being unable to sign in at all.

These entities keep `TenantId` and keep the global query filter; they are tenant-scoped rows that happen to live in the Platform database.

**Tenant localization also remains Platform database data**, for reasons drawn from how it is actually used rather than from its marker interface:

- It is served exclusively by **platform endpoints** under `/api/platform/localization/…`, governed by platform permissions (`Platform.Localization.View`, `Platform.Localization.Manage`); no ERP module reads it.
- The effective-text endpoint requires only that the caller be authenticated. It is **login-adjacent**: the UI needs resolved strings to render at all, including immediately after login and before any ERP module is opened.
- Overrides resolve **against `LocalizationCatalogState`**, which is platform-global. Splitting the pair would make ordinary override resolution a cross-database join — the exact hazard prohibited below.
- During a tenant-database outage the UI must still render in the tenant's language, not least to display the storage-unavailable message itself.

Placement is therefore decided by usage and availability, **not** by whether an entity implements `ITenantOwnedEntity`. That marker governs isolation, not location.

**The boundary is now complete**: of the eight entities implementing `ITenantOwnedEntity` today, seven (`TenantUser`, `Role`, `RolePermissionAssignment`, `TenantUserRoleAssignment`, `TenantLocalizationOverride`, `TenantLocalizationOverrideVersion`, `TenantLocalizationSettings`) remain in the Platform database, and one (`Company`) moves to the Tenant ERP database. Every entity is assigned to exactly one side.

ERP business operations, by contrast, legitimately depend on the resolved tenant database. That asymmetry — authentication and platform surfaces always available, ERP gated on tenant-storage health — is intentional and is the behaviour `ADR-018` and `ADR-021` gate on.

## Tenant-isolation enforcement is existing, load-bearing behaviour

The `TenantId` guarantee is not aspirational; it is enforced today in the shared persistence base and **must not be weakened when `TenantDbContext` is introduced**:

- **Writes carry the trusted tenant.** On insert, a tenant-owned entity with no `TenantId` is assigned the trusted current tenant; an entity whose `TenantId` differs from the trusted context is **rejected**; and saving tenant-owned entities with no trusted tenant context at all is **rejected**.
- **Tenant ownership is immutable.** Modifying `TenantId` after creation is **rejected**.
- **Reads are filtered.** The global filter restricts every tenant-owned query to the trusted tenant, and matches nothing when no trusted tenant is present.

Together these give write-side fail-loud and read-side fail-closed behaviour. The tenant context implementation changes under this ADR — it must be resolved per tenant database rather than from a single context — but the guarantees themselves are preserved unchanged.

## Misrouting behaviour is asymmetric

This asymmetry is a property of the design and must be understood rather than assumed away:

| Direction | Behaviour on misroute |
|---|---|
| **Read** | The global filter normally matches nothing. The caller sees an **empty result**, not an error. |
| **Write** | The tenant guard **fails loudly**: a mismatched or absent tenant is rejected. |

Misrouting therefore does **not** automatically produce an explicit routing exception. A misrouted read looks like "this tenant has no data" — which is safe with respect to disclosure, but silent, and will not page anyone. This is one reason routing must fail closed before a query is ever issued (below), and why tenant-less access must fail loudly rather than resolve to an empty set.

## Tenant ERP database boundary

Tenant ERP databases own business/operational data, including: `Company`, Branch, HR (`Employee`, `EmployeeContract`, `Department`, `JobTitle`, `EmployeeType`, `LeaveType`), Payroll, GL (`Account`, `Journal`, `JournalEntry`, `CostCenter`, `FiscalPeriod`), Sales, Purchasing, Inventory, `Warehouse`, tenant-specific ERP configuration, and tenant-customizable business lookups.

## `TenantId` retention

`TenantId` **remains** on tenant-owned ERP entities in **every** tenant database — shared, platform-managed dedicated, and customer-managed dedicated alike. A dedicated database normally contains exactly one distinct `TenantId` value; the column is not removed.

Customer-managed hosting does **not** change this policy. The reasons are the same schema, the same migrations, the same repositories, defence in depth, and portability between hosting modes: a database that has dropped `TenantId` could not be moved back onto shared storage, and could not be validated by the same tooling.

## Global query filtering

The automatic global `TenantId` query filter is **retained for every tenant database regardless of hosting or storage mode**, with the existing fail-closed behaviour preserved. Physical isolation — whether a platform-managed dedicated database or a customer-owned server — is **additional** protection, never a replacement for logical `TenantId` enforcement.

## Physical database model

`TenantDatabase` represents **one physical tenant-storage database endpoint**, not a tenant. `TenantId` is deliberately **not** a field, because one shared database hosts many tenants.

| Field | Purpose |
|---|---|
| `Id` | Identity of the physical endpoint |
| `HostingMode` | `PlatformManaged` / `CustomerManaged` |
| `StorageMode` | `Shared` / `Dedicated` |
| `ServerKey` | Logical server identifier resolved against trusted configuration |
| `Endpoint` | Physical address material required only when `ServerKey` cannot resolve it — see below |
| `DatabaseName` | Database on that server |
| `AuthenticationMode` | How the application authenticates to this server |
| `CredentialSecretReference` | Opaque pointer into the secret store; never credential material |
| `MigrationManagementMode` | Who is permitted to apply DDL — see `ADR-018` |
| `ProvisioningStatus` | Lifecycle of the endpoint (registered, provisioning, onboarding, ready, disabled) |
| `ConnectivityStatus` | Can the application currently reach and authenticate to it — see `ADR-018` |
| `SchemaCompatibilityStatus` | Cached migration-compatibility result — see `ADR-018` |
| `MigrationExecutionStatus` | Outcome/progress of the last migration attempt — see `ADR-018` |
| `LastConnectivityCheckUtc` | Freshness of `ConnectivityStatus` |
| `LastSchemaCheckUtc` | Freshness of `SchemaCompatibilityStatus` |
| `LastAppliedMigration` | Cached observed migration (cache only — see `ADR-018`) |
| `LastMigrationUtc`, `LastMigrationError` | Timing and failure detail of the last migration attempt |
| `CreatedUtc` | Registration timestamp |

Deliberate normalisation choices:

- **`Host`/`Port`/`ServerInstanceName` are not separate top-level columns.** They are endpoint address material that is meaningless for `PlatformManaged` rows and would be null on the overwhelming majority of records. They are modelled as a single `Endpoint` value (owned type / value object) populated only for `CustomerManaged` rows, with SQL Server's alternative addressing forms (host + port, host + named instance) expressed inside it. *Implementation decision pending: whether `Endpoint` is an EF owned type, a small related table, or a constrained serialized value, and exactly which SQL Server addressing forms are supported.*
- **`ProvisioningStatus`, `ConnectivityStatus`, `SchemaCompatibilityStatus`, and `MigrationExecutionStatus` remain four orthogonal dimensions** and are never merged into one overloaded `Status`, per `ADR-018`. A database can be schema-current and unreachable, reachable and schema-incompatible, or compatible while a migration is in flight; a single column cannot express that truthfully.
- **`LastAppliedMigration` and `SchemaCompatibilityStatus` are cached metadata, not authority** — `ADR-018` retains `__EFMigrationsHistory` as the source of truth.
- **No `Status` field duplicates a derivable state.** `Ready` is a conclusion drawn from the four status dimensions plus routing and credential validity, and is **never independently writable** — a settable flag would drift from the dimensions it summarises and then be trusted in preference to them.

*Implementation decision pending: the physical database naming convention (for example `Shared_01` / a dedicated-database naming scheme) and the exact `ServerKey` vocabulary.*

## `ServerKey` versus physical endpoint address

For **`PlatformManaged`** databases, routing uses **`ServerKey` + `DatabaseName`**. `ServerKey` is a logical name (for example `PrimarySqlCluster`) resolved against trusted application/secret configuration that already holds the address and credential material for that server. The Platform database therefore does **not** store a hostname, username, or password for every platform-managed tenant database — one configuration entry serves many rows, and rotating a server or credential is a configuration change, not a data migration.

For **`CustomerManaged`** databases, `ServerKey` cannot be pre-provisioned in platform configuration, because each customer endpoint is unique and is onboarded per contract. Such rows carry `Endpoint` address material (host, and port or named instance as the supported topology requires) plus `DatabaseName`, and resolve credentials through `CredentialSecretReference`. `ServerKey` remains meaningful as a stable operator-facing alias for the customer endpoint and as the secret-configuration key where the deployment supports registering customer servers in configuration.

Both forms produce the same thing — trusted routing metadata — and neither is ever influenced by caller input.

## Customer-managed endpoint ownership

A `CustomerManaged` `TenantDatabase` **must** carry a trusted **authorized-owner binding** — conceptually `AuthorizedTenantId` / `CustomerOwnerId`, or the repository-conventional equivalent — identifying the customer and tenant(s) permitted to use that endpoint. *Implementation decision pending: exact field name and whether ownership is expressed as a single tenant or a customer identity spanning several tenants.*

Assignment creation for a `CustomerManaged` database **must validate** that `TenantDatabaseAssignment.TenantId` matches that binding. A customer-owned endpoint **may never** be assigned to a different tenant merely because both are `Dedicated`.

`CustomerManaged` + `Dedicated` therefore means **dedicated to the authorized customer**, not merely "only one assignment happens to exist today". Without the owning binding, `Dedicated` is a statement about cardinality; with it, it is a statement about *whose* database this is — which is what residency and confidentiality actually require.

This closes an otherwise open path in which operator error or a malicious administrator places one customer's tenant inside another customer's server. The global `TenantId` filter would keep such rows logically separated while the data sat physically inside a database the other customer administers and backs up — a breach that would not look like a failure. Transferring an endpoint between owners is deliberately **out of scope**; it would require an explicit, separately-designed ownership-transfer workflow.

## Assignment model

`TenantDatabaseAssignment` maps a tenant to a physical database: `TenantId`, `TenantDatabaseId`, `AssignedUtc`, plus the lifecycle state and concurrency controls below.

### Binding invariants

1. **At most one active assignment per `TenantId`.** A tenant resolves to exactly one authoritative database at any instant.
2. **Database-level uniqueness enforces it** — a filtered unique index over `TenantId` restricted to active assignments. The invariant must be enforced by the schema, not merely by application logic; two concurrent cutovers must not both be able to commit.
3. **`RowVersion` optimistic concurrency** on the assignment, consistent with the concurrency-token convention already used elsewhere in the repository.
4. **A monotonic `RoutingVersion`**, incremented on every assignment change. This is the correctness basis for resolver caching (`ADR-020`), not a diagnostic.
5. **Lifecycle with history, not destructive overwrite.** Superseded assignments are retained so that routing history is reconstructable and auditable.

Conceptual states: `Active`, `Superseded`, `CutoverInProgress`. *Implementation decision pending: exact state names and whether history lives in the same table or an adjacent one.*

Effective-date scheduling is **not** required for V1. State plus history is sufficient, and simpler.

### Assignment transaction

A routing change **must** occur inside **one Platform database transaction** that atomically:

- changes the authoritative `TenantDatabaseAssignment`;
- increments `RoutingVersion`;
- records/audits the migration or cutover transition.

These **must not** be separate, independently-succeeding writes. A partial outcome — routing changed but version not incremented, or routing changed with no audit record — produces stale caches that believe they are current, or a routing change nobody can explain.

The separation of `TenantDatabase` from `TenantDatabaseAssignment` is load-bearing: endpoint facts belong to `TenantDatabase`, and the tenant→endpoint mapping belongs to `TenantDatabaseAssignment`. Customer endpoint data **must not** be stored on `Tenant`. For example:

```
TenantDatabase
  Id           = 25
  HostingMode  = CustomerManaged
  StorageMode  = Dedicated
  Endpoint     = sql.customer.example
  DatabaseName = CustomerERP

TenantDatabaseAssignment
  TenantId         = X
  TenantDatabaseId = 25
```

The same model expresses many tenants → `Shared_01`, one tenant → a platform-managed dedicated database, and one tenant → a customer-managed database, without variation.

## Connection security

Complete connection strings containing credentials **must not** be stored in the Platform database. Neither may plaintext SQL passwords, certificates, or private keys, for platform-managed or customer-managed endpoints alike.

Only trusted routing metadata is persisted: `ServerKey`, `DatabaseName`, and — for customer-managed rows — `Endpoint` address material. Credential material for platform-managed servers comes from application configuration or a secret store keyed by `ServerKey`. Credential material for customer-managed servers is reached through an opaque **`CredentialSecretReference`**, conceptually of the form `tenant-db/customer-9001`, which the secret store resolves to whatever authentication material that endpoint requires (username/password, certificate, or other).

The reference is a pointer, not a secret: it is safe to store in the Platform database, safe to log, and carries no authority by itself. Repository/deployment standards govern the secret store; this ADR does not select one. *Implementation decision pending: secret-store selection, reference naming convention, and rotation procedure — see `ADR-021`.*

## Database authentication mode

`AuthenticationMode` records how the application authenticates to a physical endpoint. The architecture must be extensible across at least `PlatformCredential`, `SqlAuthentication`, `WindowsIntegrated`, `ManagedIdentity`, and `Certificate`, because customer environments vary and the correct mode is a per-endpoint fact.

**Architecture extensibility is not a promise of V1 support.** The minimal V1 recommendation is:

- `PlatformCredential` for `PlatformManaged` endpoints — the existing configuration-supplied credential path, which is what the product uses today.
- `SqlAuthentication` via `CredentialSecretReference` as the single initially supported `CustomerManaged` mode, because it is the one mode reachable across arbitrary customer network topologies without depending on a shared directory or cloud identity plane.

Other modes are architecturally accommodated and implemented only when a concrete customer requires them. `ADR-021` owns the supported-mode list and its evolution.

## Routing

A resolver abstraction (`ITenantDatabaseResolver`, or the repository-conventional equivalent) resolves a trusted tenant to trusted routing metadata. The binding trust chain is:

```
validated JWT
  -> trusted tenant_id claim
  -> ICurrentTenant
  -> TenantDatabaseAssignment
  -> TenantDatabase
  -> HostingMode
       |
       +-- PlatformManaged --> trusted ServerKey / DatabaseName
       |                       -> credentials from trusted configuration
       |
       +-- CustomerManaged --> trusted Endpoint / DatabaseName
                               -> CredentialSecretReference
                               -> secret store
  -> TenantDbContextFactory
  -> tenant SQL Server (platform-operated or customer-operated)
  -> tenant ERP database
  -> global TenantId query filter
```

Every element of that chain is trusted server-side state. The browser/client **must never** supply `Host`, `Port`, `DatabaseName`, connection string, `CredentialSecretReference`, `ServerKey`, `StorageMode`, `HostingMode`, `TenantDatabaseId`, shard, or any other routing hint. The only caller-influenced input is the `tenant_id` claim inside a token the platform itself issued and validated.

## Fail-closed routing

The resolver **fails closed**. It must refuse to produce routing — and the request must terminate in a controlled tenant-storage unavailability result rather than a raw data-access exception — when:

- no `TenantDatabaseAssignment` exists for the tenant;
- the resolved `TenantDatabase` is not Ready;
- the credential secret is unavailable or unresolvable;
- endpoint configuration is invalid or incomplete;
- `SchemaCompatibilityStatus` is explicitly incompatible (see `ADR-018`);
- the database is under migration or cutover.

## No automatic fallback

If a tenant's assigned database is unavailable, the request **must not** be served from any other database. Specifically, a `CustomerManaged` tenant **must never** be routed to platform-managed shared or dedicated storage, and no tenant may be silently relocated to an alternative endpoint at request time.

Database placement is an explicit security and data-residency boundary. A fallback would write or read customer ERP data in a location the customer did not agree to, which is a worse outcome than an outage. The correct behaviour is a controlled unavailability result.

## Administrative authorization of storage operations

All routing is **trusted server-side state**. Administrative storage operations — registering or provisioning a `TenantDatabase`, changing a `TenantDatabaseAssignment`, forcing a placement override, configuring an endpoint or credential reference, testing connectivity, disabling an endpoint, and triggering migration or cutover — must be **platform-admin authorized**, consistent with the platform-plane authority model of `ADR-015`/`ADR-016`. Tenant-plane users are **never** permitted to modify physical database placement, hosting mode, storage mode, endpoint configuration, or routing metadata for their own tenant or any other.

Customer-managed endpoint and credential configuration is **security-sensitive platform configuration** and is held to the same authority requirement; `ADR-021` details it.

*Requires follow-up decision: the permission family governing storage administration.* It **should not** permanently reuse `Platform.Support.Administer`, which governs platform-support authority itself. The expected direction is a dedicated family following the existing `Platform.{Resource}.{Action}` catalog convention, with **cutover approval separable** from ordinary storage metadata administration because its blast radius is data loss rather than misconfiguration. No permission catalog entries are created by this ADR.

### Audit of storage actions

High-impact storage actions — routing creation and change, secret reference change, hosting/storage mode change or override, placement policy change, manual recommendation override, migration execution, cutover approval, cutover, rollback, and customer connectivity configuration — are audited with: `ActorIdentityId`, `ActingPlatformSupportPrincipalId`, `SessionId`, before values, after values, approval reference, execution actor, timestamp, outcome, and failure reason.

Actor identity alone is not sufficient: for a platform-plane action, the authority under which it was taken is the meaningful record, and approver and executor may legitimately differ. **Audit records must never contain credential material.**

## Tenant DbContext

Future ERP persistence introduces a dedicated `TenantDbContext` with its **own** migration stream. `PlatformDbContext` remains for the Platform database. Connection selection for `TenantDbContext` is dynamic, derived only from trusted routing. The preferred construction mechanism is `IDbContextFactory<TenantDbContext>` or a custom equivalent, subject to implementation review.

### Binding lifetime rules

These are recorded now because each is easy to satisfy before implementation and expensive to detect afterwards:

1. **Routing resolves per context creation.** Trusted routing is resolved on each `TenantDbContext` creation, never once per process.
2. **Routing must not be captured at registration.** `IDbContextFactory<T>` is a singleton whose `DbContextOptions` are built once; a connection string captured while registering the factory would pin every tenant to whichever tenant happened to be first. Connection selection must happen at creation time, not at options-build time.
3. **No tenant-dependent `OnModelCreating`.** The EF model is cached per options; tenant-conditional model configuration would let one tenant's model serve another. The model **must remain tenant-invariant** — the same entities, keys, indexes, and filters for every tenant and every placement.
4. **No `AddDbContextPool` for the dynamically-routed `TenantDbContext`**, where pooled state could carry connection identity across tenants.
5. **Tenant-less access fails loudly** — see below.
6. **Background services must open an explicit trusted tenant execution scope** before touching tenant data. They may not rely on an ambient HTTP context, because they do not have one.
7. **Migration context and options are separate from runtime context and options**, and use the migration credential rather than the runtime credential (`ADR-021`).

### Tenant-less reads must fail loudly

The global filter is written as `CurrentTenantId.HasValue && entity.TenantId == CurrentTenantId.Value`, so **absence of a trusted tenant context yields zero rows rather than an error**. For request-path reads that is correct fail-closed behaviour. For anything else it is dangerous: a scheduled job, message consumer, webhook handler, or background workflow running without a tenant context would read an empty result set and conclude, without any error, that there is nothing to do.

Therefore: **`TenantDbContext` read and write operations must fail when no trusted tenant context is present.** "No tenant" must never be indistinguishable from "no data".

The single, narrow exception is **explicitly designed maintenance and migration tooling** — for example the cutover copy path in `ADR-020` — which operates outside the tenant context deliberately, under review, and never on the request path.

### Connection pooling consequence

ADO.NET pools connections **per connection string**. Many dedicated and customer-managed databases therefore mean many pools, each with its own minimum and maximum, held by every serving node. This is an operational limit that arrives with dedicated placement, not with the first dedicated tenant.

Connection-pool usage **must be bounded and observed before dedicated placement is scaled**. No numeric limits are set here; the requirement is that the ceiling be known and monitored rather than discovered during an incident.

## Unit of work

Once the Platform and Tenant ERP databases are physically separate, a request touching both **does not** have a single atomic EF/SQL transaction. Assuming distributed-transaction atomicity is **prohibited**. Cross-database workflows must use an ordered application workflow with idempotency, compensation, or another explicitly approved consistency mechanism. Distributed transactions are not the default and require a separate decision.

## Cross-plane commit ordering

Ordering is what makes the absence of a distributed transaction safe. It is binding in both directions:

**Provisioning / creation — routing goes live last.**

1. The tenant database, schema, and required seed data reach **Ready** first (`ADR-018`).
2. The `TenantDatabaseAssignment` becomes **active last**.

A tenant therefore becomes routable only once there is something correct to route it to. A failure at any earlier step leaves a tenant that is simply not yet routable — an incomplete provisioning job, not a broken tenant.

**Teardown / deletion — routing is revoked first.**

1. Routing is **revoked first**.
2. Destructive database or data steps occur **afterwards**.

No request can then reach a tenant whose data is being removed. The reverse order would expose partially-deleted state to live traffic.

This ordering applies to tenant creation, tenant deletion, storage movement, and any future workflow spanning the plane boundary. Membership creation is unaffected, being entirely Platform-side.

## Cross-database foreign keys

There are **no** SQL foreign keys across the Platform and Tenant ERP databases. When an entity moves across the boundary, its cross-boundary constraints are replaced with trusted identifiers plus application/domain validation. Foreign keys remain fully used **within** each physical database.

With membership *and* localization both remaining in the Platform database, **exactly one** of the six current cross-boundary foreign keys is affected: **`Company → Tenant`**. The other five are eliminated outright by the boundary rather than replaced by weaker validation — which is a stronger outcome than the original proposal and a direct consequence of placing entities by usage rather than by marker interface.

`Company → Tenant` is a tenant-existence reference. Its replacement is adequate because `TenantId` on `Company` is independently enforced by the global filter and the tenant guard described above, and because the commit ordering above guarantees a tenant is routable only while it exists. Validation occurs at creation; the identifier is referenced thereafter. No snapshot is required.

For **future HR/GL references to platform identity** (employee ↔ `TenantUser`, approver, owner, and similar), the binding pattern is: **reference the trusted identifier, snapshot the display data**. This avoids the hot-path join described below and gives better historical semantics.

## Cross-database joins

Normal ERP request paths **must not** depend on joins between a Tenant ERP database and the Platform database. Required reference data either stays truly global and is resolved outside hot ERP joins, or is seeded/copied into tenant ERP storage.

### Named hazard: actor display resolution

The most likely violation is not reference data but **audit actor display**. Every tenant-owned row carries `CreatedBy` and `ModifiedBy` — platform-side user identifiers — and virtually every ERP list screen and report wants to show a *name*, not an identifier. Resolving that naively means joining the Tenant ERP database to Platform identity data on the hottest paths in the product.

Tenant ERP reports and grids **must not** join the Platform database for actor display. Two acceptable approaches, neither mandated here:

- **Snapshot the actor display metadata** onto the business or audit record at write time. This is also better history semantics — it records who acted *as they were then*, which is what an audit trail should show.
- **Enrich in the application layer** from a separate Platform query or cache, merging after both reads.

*Implementation decision pending: which approach, and whether it differs between transactional records and audit history.*

Note that `CreatedBy`/`ModifiedBy` are already plain scalar identifiers with no foreign key today, so the split introduces **no integrity regression** here — only a resolution problem.

## Lookup classification

- **A — Platform global**: `Country`, `Currency`, `Language`, subscription plans, permission catalog, module definitions, global localization catalog. Stored in the Platform database. **Tenants cannot create global rows.**
- **B — Tenant system-seeded**: stored in the Tenant ERP database, written from versioned seed definitions, marked `IsSystem = true` (or the repository-conventional equivalent). Examples: document states, employment states, GL account categories.
- **C — Tenant customizable**: stored in the Tenant ERP database, `TenantId`-scoped, `IsSystem = false`. Examples: departments, job titles, employee types, leave types, cost centres, payment terms, warehouses, numbering series.
- **D — Closed domain value sets**: **not lookup tables at all.** Where a value set is a fixed lifecycle or processing state, the established repository convention is a **domain enum plus a database `CHECK` constraint** — as used today for company status and status-change reason codes — not a seeded table. Examples: entity lifecycle statuses, document processing states, approval states.

A value created by Tenant A **must not** be visible to Tenant B. For default rows in category B, the binding pattern is **seed per `TenantId`** rather than global rows with tenant overrides: it preserves a single query shape, keeps the global filter honest, and makes shared→dedicated movement a pure row copy.

Category D exists to prevent a specific misreading: **not every fixed set of values becomes a seeded tenant lookup table.** Something like an employment status or a document state is a closed domain concept the application reasons about in code; modelling it as a per-tenant table would imply tenants may extend it, add rows the domain cannot handle, and diverge from the enum the code switches on. Where the domain is genuinely open to tenant extension, it is category C; where it is closed, it is category D and no table exists.

### Tenant-scoped uniqueness is binding

Tenant-owned business uniqueness is expressed as **`(TenantId, NaturalKey)`** — for example `(TenantId, NormalizedCode)`, `(TenantId, NaturalNumber)` — **in every placement, including dedicated and customer-managed databases**. A bare `NaturalKey` unique constraint is prohibited on tenant-owned entities.

This is what keeps one schema portable: a bare-key constraint would function in a dedicated database and break the moment the same schema is applied to shared storage, or the tenant is moved. It is also the existing repository convention, which this rule makes binding rather than incidental.

`TenantId` should likewise lead tenant-owned indexes where appropriate, so the retained global filter is a cheap seek rather than a scan in dedicated databases.

## Default placement

Normal new tenants default to **Shared**, unless contract, compliance, regulation, data residency, or an explicit platform override requires Dedicated. Future customer size **must not** be predicted at onboarding.

## Future shards

Routing must support future `Shared_01`, `Shared_02`, `Shared_03` without domain or entity redesign; `ServerKey` plus `DatabaseName` on `TenantDatabase` is sufficient. Automatic shard rebalancing is **not** required in V1.

## Tenant deletion and storage

Deleting a tenant is **not** the same as destroying a database, and the two must never be wired together as a cascade. In every case, **routing is revoked before any destructive step** (per the commit ordering above).

**`CustomerManaged`** — tenant deletion **must not** `DROP`, `TRUNCATE`, or otherwise destroy the customer-owned database. The platform does not own that data and has no authority to destroy it. Deletion means: revoke the assignment, revoke or disable the credential reference, disable routing, and stop connecting. **The customer retains their data and its disposition**, unless a contract explicitly governs another process.

**`PlatformManaged` + `Dedicated`** — database destruction may be supported, but only as an **explicitly authorized, separately-confirmed, audited, retention-aware** action. It is **not** a step inside generic tenant deletion; a tenant-deletion workflow that silently drops a database is prohibited.

**`PlatformManaged` + `Shared`** — only `TenantId`-scoped data is removed or archived. The database itself is untouched, as it hosts other tenants.

## Reporting

Tenant operational reports query the one resolved Tenant ERP database. Cross-tenant analytics **must not** fan out arbitrary business queries across tenant databases in real time; a warehouse/ETL/event-aggregation approach is preferred and is decided separately.

## Backup and recovery is a per-physical-database concern

**Deferred capability — documented now so the boundary is not designed wrongly, implemented in `TS-Backup`.**

Backup and recovery policy attaches to a **physical `TenantDatabase`**, never to a tenant. This follows directly from the topology and is not a matter of preference:

| Placement | Backup chain |
|---|---|
| `PlatformManaged` + `Shared` | **One** physical backup chain covering the shared database, and therefore **every tenant in it**. A single tenant cannot be restored independently by restoring the database. |
| `PlatformManaged` + `Dedicated` | **Its own independent** backup chain per database, restorable without affecting any other tenant. |
| `CustomerManaged` + `Dedicated` | The customer's chain, on the customer's server — see `ADR-021`. |

The consequence worth stating plainly: **on shared storage, "restore this tenant to yesterday" is not a database restore.** It would roll back every other tenant in that database. Per-tenant point-in-time recovery on shared storage requires export/extract tooling, which is a separate decision and is not implied by having backups. Dedicated placement is what makes database-level restore a per-tenant operation, and that is one of its real advantages over the shared default.

A **Backup & Recovery Manager** (`TS-Backup`) is therefore a required capability of the platform-managed estate, covering: policy per physical database, scheduled execution, retention, backup history, failure monitoring, recovery-model validation, periodic **restore verification**, and recovery-readiness reporting. Its sequencing constraint is binding: it **must** exist before dedicated provisioning and cutover become production-capable (`ADR-020`), because a dedicated database with no verified backup chain is a data-loss position the shared database did not have.

**`ADR-022` now owns these decisions.** It defines backup policy as a per-physical-database entity, recovery readiness as an independent operational dimension with its own writer, a `BackupManagementMode` separate from migration authority, a backup credential separate from the ERP runtime identity, SQL Server chain semantics, orchestration and execution ownership, run history, retention responsibility, restore verification, and the binding pre-cutover readiness gate. This section remains the topology rationale; `ADR-022` is authoritative for the mechanism.

For SQL Server the expected default strategy is a **full baseline + differential + transaction-log** chain, with schedules and retention configurable per policy. **Transaction-log backups alone are not a backup strategy** — they are only restorable onto a full baseline, and they require an appropriate recovery model to exist at all. Indicative defaults, to be set by configuration rather than hard-coded: full weekly, differential daily, transaction log every 15 minutes, with higher log frequency where the recovery-point objective demands it.

## Database provider extensibility

**Deferred capability — documented now so provider-specific decisions are not mistaken for provider-neutral contracts. Implemented, if ever required, in `TS-Provider`.**

The tenant persistence architecture is intended to remain **database-provider extensible**. **SQL Server is the only supported runtime provider in V1**, and no other provider works today. Provider-specific connection construction, schema migrations, concurrency mechanisms, locking behaviour, indexing capabilities, and backup/recovery behaviour remain isolated within Infrastructure. Oracle, PostgreSQL, and other providers are **deferred until explicitly required**.

`DatabaseProvider` is a **third dimension, independent of `HostingMode` and `StorageMode`**. Future combinations may include `PlatformManaged + Shared + SqlServer`, `PlatformManaged + Dedicated + SqlServer`, `PlatformManaged + Dedicated + Oracle`, and `CustomerManaged + Dedicated + Oracle`. The provider **must not** be inferred from `ServerKey`, `HostingMode`, or `StorageMode` — a customer-managed endpoint is not necessarily Oracle, and a platform-managed one is not necessarily SQL Server forever. No schema change adding `DatabaseProvider` is required until a second provider is actually adopted.

The following are **provider-specific** and must not be generalised around SQL Server assumptions: connection types and connection-string builders; the EF provider; migrations; identity/sequence generation; `rowversion`/concurrency tokens; filtered, partial and function-based indexes; locking syntax; collation and case semantics; schema naming; backup mechanisms; recovery model; and transaction-log/archive-log behaviour.

Consequently, the current V1 implementation legitimately uses `SqlConnection`, `SqlConnectionStringBuilder`, SQL Server error numbers `2601`/`2627`, SQL Server migrations, and `rowversion` concurrency tokens. These are **acceptable provider-specific Infrastructure details**, and they **must not** be presented anywhere as database-independent contracts. Documentation and roadmap material **must not** claim Oracle or PostgreSQL compatibility that does not exist.

## Implementation sequence

The revised order, with each item's dependency reason:

| # | Slice | Status |
|---|---|---|
| 1 | Tenant-storage registry (`TenantDatabase`, `TenantDatabaseAssignment`, bootstrap) | Done |
| 2 | Routing resolver and trusted connection factory | Done |
| 3 | `TenantDbContext` + separate tenant migration stream + `Company` pilot | Done |
| 4 | Schema health and migration orchestration (`ADR-018`) | Next |
| 5 | `TS-Provider` — database provider abstraction | Deferred; may remain deferred beyond V1 while SQL Server-only is acceptable |
| 6 | `TS-Backup` — tenant database backup and recovery manager | Deferred; **required before** 7 and 8 become production-capable |
| 7 | Dedicated database provisioning | Requires 4 and 6 |
| 8 | Shared → Dedicated migration and cutover (`ADR-020`) | Requires 7 |
| 9 | Dynamic placement automation (`ADR-019`) | Requires 8 |

Customer-managed connectivity (`ADR-021`) remains separate and later throughout.

---

# Decision Drivers

- Security isolation and defence in depth.
- Enterprise/compliance and data-residency addressability.
- Per-tenant backup and restore capability.
- Operational cost: number of physical migration targets.
- Conversion cost, which grows with every tenant-owned entity added.
- Preservation of the existing, working `ITenantOwnedEntity` + global-filter design.
- Developer complexity and V1 delivery speed.

---

# Alternatives Considered

## Option 1 – Single shared database forever (`TenantId` discriminator only)

### Advantages

- Simplest possible operations: one database, one migration target.
- No routing, provisioning, or placement machinery.
- Fastest V1 delivery; lowest developer complexity.
- Cross-tenant analytics is a trivial query.

### Disadvantages

- No per-tenant restore; recovery of one tenant requires logical row-level reconstruction.
- No noisy-neighbour isolation lever.
- Cannot satisfy customers requiring physical isolation or data residency.
- All isolation rests on a single logical mechanism.
- The constraint becomes unfixable once hundreds of ERP entities exist.

## Option 2 – One database per tenant from day one

### Advantages

- Strongest isolation; physical boundary per customer.
- Trivial per-tenant backup, restore, export, and deletion.
- Natural data residency and noisy-neighbour isolation.

### Disadvantages

- Migration targets scale with tenant count: 10,000 tenants means 10,000 migrations per release.
- Provisioning becomes part of the signup path, with failure/retry semantics.
- Large numbers of near-empty databases; poor resource utilisation and cost.
- Cross-tenant analytics becomes fan-out.
- Disproportionate for a product with no current dedicated-tenant demand.

## Option 3 – Hybrid-ready storage with hosting and storage as one combined mode

### Advantages

- One enum, one column, one concept to explain.
- Slightly simpler model and UI than two dimensions.

### Disadvantages

- Cannot express "dedicated, hosted by the customer" without inventing a third value whose meaning overlaps the other two.
- Every rule that depends on isolation (`TenantId` retention, placement thresholds, cutover) and every rule that depends on ownership (credentials, migration authority, backup responsibility, connectivity) would have to be re-derived from a conflated value.
- Adding a hosting variant later would break the existing enum's meaning and every persisted row.

## Option 4 – Hybrid-ready storage with independent `HostingMode` and `StorageMode` (selected)

### Advantages

- Migration targets scale with **physical databases**, not tenants.
- Same entity model, migrations, and code for every placement and hosting mode.
- Dedicated isolation available on demand for enterprise/compliance customers.
- Customer-hosted storage becomes a configuration of the same routing model, not a parallel mechanism.
- Preserves the existing `ITenantOwnedEntity` and global-filter design almost unchanged.
- Per-tenant restore becomes available exactly for the tenants that need it.
- Rules attach to the dimension they actually depend on: isolation rules to `StorageMode`, ownership/operations rules to `HostingMode`.

### Disadvantages

- Requires new machinery: registry, resolver, `TenantDbContext`, schema health, connectivity health, migration orchestration.
- Loses single-transaction atomicity across the plane boundary.
- Requires replacing the cross-boundary foreign keys of moved entities.
- Two dimensions mean an invalid combination exists (`CustomerManaged` + `Shared`) that must be actively rejected.
- Higher operational and testing complexity than Option 1.

---

# Rationale

Option 4 is selected.

The repository is already shaped for it. `ITenantOwnedEntity` plus the reflection-driven global filter means `TenantId` is uniform and enforced by construction — exactly the property a hybrid topology needs so that shared and dedicated databases can share one model with no domain rewrite. The domain layer is essentially unaffected by this decision.

The cost is at its historical minimum. Eight tenant-owned entities and six cross-boundary foreign keys today; after HR and GL, dozens; after the full ERP surface, hundreds. Deciding now converts a contained refactor into a permanent capability.

Option 1 is rejected because it permanently forecloses per-tenant restore, noisy-neighbour isolation, and physically-isolated/regulated customers, and because the decision becomes irreversible in practice once ERP persistence lands.

Option 2 is rejected because its dominant cost — migration and provisioning fan-out proportional to tenant count — is paid immediately and forever, in exchange for isolation that only a minority of tenants will ever require. Hybrid delivers the same isolation to those tenants while keeping migration targets proportional to physical databases.

Option 3 is rejected because hosting and isolation genuinely are independent, and the moment a customer-hosted database appears the conflated enum has no honest value for it. Separating the dimensions costs one extra column now and avoids re-interpreting every persisted row later. It also makes the invalid case explicit rather than accidental: `CustomerManaged` + `Shared` is prohibited, and the model can say so.

Splitting hosting from storage also puts each downstream rule on the property it actually depends on. `TenantId` retention, placement thresholds, and cutover mechanics follow `StorageMode`; credentials, connectivity, migration authority, backup ownership, and support boundaries follow `HostingMode`. Under a conflated value none of those could be expressed without re-deriving intent.

The membership refinement (keeping `TenantUser`/`Role`/assignments in the Platform database) is chosen because it keeps authentication single-database and eliminates three of six cross-boundary foreign keys outright, which is materially simpler than any workaround that would be needed if membership moved.

---

# Consequences

## Positive

- Enterprise, regulated, and residency-constrained customers become addressable without a rewrite.
- Migration effort scales with physical database count, not tenant count.
- Per-tenant restore, export, and deletion become straightforward for dedicated tenants.
- A noisy tenant can be isolated as an operational action rather than a redesign.
- `TenantId` retention preserves defence in depth even under misrouting or contamination.
- Domain layer is effectively unchanged; the existing security mechanism is preserved.
- Customers requiring their ERP data to remain on their own infrastructure become addressable without any change to HR, GL, Payroll, or other business code — hosting is a routing fact, not a domain fact.
- Authentication remains available during a customer-side database outage, so users get a clear diagnosis instead of a failed login.

## Negative

- New infrastructure must be built and maintained: registry, resolver, `TenantDbContext`, schema health, migration orchestration, provisioning.
- Cross-plane atomic transactions are lost; workflows must be ordered, idempotent, or compensating.
- Cross-boundary foreign keys of moved entities are replaced by application-level validation, reducing database-enforced integrity at that seam.
- Test infrastructure must create and migrate two databases.
- Deployment gains a mandatory tenant-migration orchestration step.
- Customer-managed endpoints introduce availability, network, credential, and support-boundary concerns that are outside platform control, and an operational surface (`ADR-021`) that does not exist today.
- An invalid hosting/storage combination exists and must be actively rejected wherever `TenantDatabase` rows are created.

---

# Implementation Guidelines

- Introduce `TenantDatabase` and `TenantDatabaseAssignment` in the Platform database first; routing can be exercised while the tenant schema still physically resides in one database.
- Introduce `TenantDbContext` with its own migration stream, its own schema and its own migration-history table before moving any entity. Sharing the platform history table would make a tenant database indistinguishable from a platform database.
- Move `Company` as the pilot tenant-owned entity to prove the routing and migration path end to end before HR/GL.
- Resolve routing per `TenantDbContext` creation / unit of work. Cached routing is valid only for the `RoutingVersion` under which it was resolved: `RoutingVersion` is the correctness mechanism, while explicit invalidation and a bounded TTL are propagation aids. Cache invalidation alone is insufficient (`ADR-020`).
- Make `TenantId` the leading column of tenant-owned indexes so the retained global filter is a no-op seek in dedicated databases.
- Keep `IgnoreQueryFilters` usage confined to explicitly reviewed platform-side maintenance paths.
- Prefer `IDbContextFactory<TenantDbContext>` or a custom equivalent that takes the tenant explicitly and selects the connection at creation time rather than at options-build time; do not adopt context pooling for the tenant context without an explicit review of connection-identity safety.

---

# Compliance Rules

1. All tenant-owned ERP entities **must** implement `ITenantOwnedEntity` and carry `TenantId`, in shared and dedicated databases alike.
2. The global `TenantId` query filter **must** remain enabled for tenant-owned entities regardless of placement.
3. Database routing metadata **must** originate only from Platform database state derived from the validated `tenant_id` claim. No caller input may influence routing.
4. Complete connection strings with credentials **must not** be persisted in the Platform database.
5. No SQL foreign key may cross the Platform / Tenant ERP database boundary.
6. Normal ERP request paths **must not** join across the Platform and Tenant ERP databases.
7. A tenant **must not** be able to create or modify Platform-global reference data, nor promote a tenant-customizable lookup into global reference data.
8. Cross-database workflows **must not** assume distributed-transaction atomicity.
9. New tenants **must** default to `PlatformManaged` + `Shared` placement unless a business/compliance rule requires otherwise.
10. Administrative storage operations (database registration/provisioning, assignment change, placement override, endpoint/credential configuration, connectivity test, endpoint disable, migration, cutover) **must** be platform-admin authorized; tenant-plane users **must not** be able to modify physical placement, hosting mode, endpoint configuration, or routing metadata.
11. `HostingMode` and `StorageMode` **must** be modelled as independent values and **must not** be collapsed into one combined mode.
12. `CustomerManaged` + `Shared` **must** be rejected as an invalid `TenantDatabase` configuration.
13. Plaintext passwords, complete credentialed connection strings, certificates, and private keys **must not** be persisted in the Platform database for any hosting mode; customer credentials **must** be reached through `CredentialSecretReference`.
14. Routing **must** fail closed on missing assignment, not-Ready database, unavailable secret, invalid endpoint configuration, explicitly incompatible schema, or in-progress migration/cutover.
15. A tenant **must not** be automatically routed to any database other than its assigned one; a `CustomerManaged` tenant **must never** fall back to platform-managed storage.
16. A tenant **must** have at most one active `TenantDatabaseAssignment`, enforced by a database-level unique constraint, with `RowVersion` and a monotonic `RoutingVersion`.
17. A routing change **must** be a single Platform-database transaction covering the assignment change, the `RoutingVersion` increment, and the audit record.
18. A `CustomerManaged` `TenantDatabase` **must** carry an authorized-owner binding, and assignment **must** validate the tenant against it.
19. Tenant-owned business uniqueness **must** be `TenantId`-scoped in every placement.
20. `TenantDbContext` routing **must** be resolved per context creation and **must not** be captured at factory/options registration.
21. The tenant EF model **must** be tenant-invariant; tenant-dependent `OnModelCreating` is prohibited.
22. `TenantDbContext` access without a trusted tenant context **must** fail, except in explicitly designed maintenance/migration tooling.
23. Provisioning **must** make routing active last; teardown **must** revoke routing first.
24. Tenant deletion **must not** destroy a `CustomerManaged` database, and **must not** destroy a `PlatformManaged` dedicated database as an implicit cascade.
25. Tenant ERP request paths **must not** join the Platform database for actor display resolution.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Loss of cross-plane transactional atomicity produces partial state | Keep the plane boundary out of single business transactions; use ordered, idempotent workflows with compensation; pilot with `Company` before HR/GL |
| Replacing cross-boundary FKs weakens database-enforced integrity | Limit the boundary to the minimum by keeping membership in the Platform database; add explicit application/domain validation and integration tests at each replaced seam |
| Resolver returns stale routing after a cutover | Mandatory cache invalidation/versioning on assignment change (`ADR-020`) |
| Dynamic connection selection leaks across tenants via pooled context state | Prohibit `AddDbContextPool` for `TenantDbContext`; require factory-based construction and review |
| Test and deployment complexity slows delivery | Introduce routing before physical split; keep one database physically until the pilot proves the path |
| Team treats dedicated placement as a performance feature | Record explicitly that isolation and operations, not raw speed, justify promotion (`ADR-019`) |
| Customer SQL Server outage makes the product appear entirely down | Authentication stays Platform-database-only; ERP access fails with a controlled tenant-storage-unavailable result; connectivity health surfaces the cause to operators (`ADR-018`, `ADR-021`) |
| Availability incident is mis-triaged because the failing component is customer-owned | Explicit support/ownership boundary and connectivity diagnostics in `ADR-021`; `HostingMode` visible in operator tooling |
| Fallback to shared storage is added "for resilience" and silently breaches residency | Compliance Rule 15 prohibits it outright; no-automatic-fallback recorded as a security boundary, not a preference |
| Customer endpoint credentials leak through logs, UI, or metadata | Only opaque `CredentialSecretReference` is persisted; secret display prohibited in operator tooling (`ADR-021`) |
| `CustomerManaged` + `Shared` is created by mistake | Combination declared invalid and rejected at creation, not merely discouraged |
| One customer's tenant assigned to another customer's endpoint | Authorized-owner binding on `CustomerManaged` databases, validated at assignment; ownership transfer explicitly out of scope |
| Two concurrent cutovers both commit, producing two active assignments | Database-level filtered unique constraint plus `RowVersion`; flip and version increment in one transaction |
| Background job reads with no tenant context and sees "no data" | Tenant-less `TenantDbContext` access fails loudly; background services must open an explicit trusted tenant scope |
| Routing captured once at factory registration pins all tenants to one database | Binding rule that routing resolves per context creation, never at options-build time |
| Tenant-conditional model configuration leaks one tenant's model to another | EF model required to be tenant-invariant |
| Tenant deletion cascades into dropping a customer-owned database | Explicit deletion rules per hosting/storage mode; destruction never implicit |
| Connection-pool exhaustion as dedicated placement scales | Pool usage must be bounded and observed before scaling dedicated placement |
| Actor-display grids quietly reintroduce a cross-plane join | Hazard named explicitly with two approved remedies |

---

# Future Considerations

This ADR should be revisited if: multi-region residency becomes a contractual requirement; a distributed-transaction or outbox mechanism is adopted; automatic shard rebalancing becomes necessary; a dedicated read/analytics store replaces tenant-local reporting; or tenant counts or per-tenant data volumes diverge materially from current expectations.

---

# Related Documents

- `ADR-005` — Multi-Tenancy
- `ADR-008` — Entity Framework Core
- `ADR-011` — Unit of Work
- `ADR-013` — Primary Key & Identifier Strategy
- `ADR-015` — Platform-Plane Authentication and Authorization
- `ADR-016` — Platform-Support Bootstrap, Lifecycle, and Authority Administration
- `ADR-018` — Tenant Schema Health and Migration Orchestration
- `ADR-019` — Dynamic Tenant Placement Policy
- `ADR-020` — Shared-to-Dedicated Tenant Migration and Cutover
- `ADR-021` — Customer-Managed Tenant Database Connectivity and Operations
- FP-003 `decisions-approved.md`, `authorization-model.md`

---

# Review Criteria

This ADR should be reviewed if:

- The Platform / Tenant ERP boundary is proposed to change, particularly the placement of `TenantUser`/`Role`.
- A requirement emerges for a genuine cross-plane atomic transaction.
- Dedicated placement demand materially exceeds or falls short of expectations.
- Multi-region or residency requirements are contracted.
- A customer-managed deployment is actually contracted, moving `ADR-021` from architecture-ready to implementation scope.
- Movement between hosting modes (`PlatformManaged` ↔ `CustomerManaged`) becomes a requirement.

---

# Phase-4D and FP-003 Relationship

FP-003 Phase 4A (platform permission authorization), Phase 4B (platform authentication HTTP and session serialization), Phase 4C (authority read surface), Phase 4D-0 (administrative recovery predicate), and the current Phase-4D authority-management HTTP surface operate exclusively on Platform-database entities — `Identity`, `AuthenticationAccount`, `PlatformSupportPrincipal`, `PlatformPermissionAssignment`, `PlatformAuthenticationSession`. None of them reads or writes a tenant-owned ERP entity.

This ADR therefore **does not invalidate or require rework of any FP-003 Phase-4 slice**, and Phase-4D validation, review, and commit may resume independently of tenant-storage implementation.

Customer-managed tenant ERP database support does not change this. It alters nothing in platform-support authorization, platform authentication, platform-support sessions, or the Phase-4D authority-management routes — all of which are Platform-database-only and hosting-mode-agnostic.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | 2026-08-13 | Solution Architecture Team | Initial version — hybrid-ready tenant storage topology and routing |
| 1.1 | 2026-08-13 | Solution Architecture Team | Separated `HostingMode` from `StorageMode`; added customer-managed hosting, endpoint/credential model, authentication mode, fail-closed routing and no-automatic-fallback rules; added `ADR-021` |
| 1.2 | 2026-08-13 | Solution Architecture Team | Review hardening: completed the boundary (localization to Platform DB); documented tenant-guard enforcement and misrouting asymmetry; added assignment invariants and single-transaction flip; added customer-endpoint owner binding; added `TenantDbContext` lifetime rules and tenant-less fail-loud; added cross-plane commit ordering, tenant-scoped uniqueness, closed-domain lookup category, actor-display hazard, tenant-deletion rules; corrected cross-references |
| 1.3 | 2026-08-13 | Solution Architecture Team | Editorial: synchronised the status model with `ADR-018` (four orthogonal dimensions; `SchemaCompatibilityStatus`/`MigrationExecutionStatus`; `Ready` never independently writable); corrected a directional reference and lookup-list formatting. No decision changed |
| 1.4 | 2026-08-13 | Solution Architecture Team | Editorial: corrected the routing-cache implementation guidance to resolve per `TenantDbContext` creation / unit of work with `RoutingVersion` as the correctness mechanism. No decision changed |
| 1.5 | 2026-08-14 | Solution Architecture Team | Added per-physical-database backup and recovery policy with the shared-restore consequence and the `TS-Backup` sequencing constraint; added database-provider extensibility with SQL Server as the only V1 runtime provider and `DatabaseProvider` as a dimension independent of `HostingMode`/`StorageMode`; added the implementation sequence table; clarified the tenant migration-stream and context-factory guidelines |
| 1.6 | 2026-08-14 | Solution Architecture Team | Pointed backup and recovery mechanism decisions to the new `ADR-022`; this ADR retains the topology rationale only. No decision changed |
