# SSAS.BuildingBlocks.Tenancy

**The module-facing tenant-plane contracts** (`ADR-012`, `ADR-023`, `ADR-024`, `ADR-025`).

## Why this project exists

`ADR-012` forbids one module from referencing another module's internal Domain, Application, API or
Infrastructure assemblies, and permits cross-module consumption only through *approved public contracts* or
*explicitly authorized module-facing abstractions*. `SSAS.Platform.*` is a module under that rule, so a
business module such as HR or GL cannot reference Platform to reach the tenant execution plane.

But every business module needs that plane. An Employee, a journal or a stock movement has to save through
the tenant unit of work, be authorized against branch scope, and — where it is transferable — open the
sanctioned branch-transfer channel.

This project is the **explicitly authorized module-facing abstraction set** `ADR-012` anticipated. Platform
implements these contracts; business modules consume them. Neither references the other.

```
SSAS.BuildingBlocks.Tenancy          (contracts — this project)
        ▲                    ▲
        │                    │
SSAS.Platform.*         SSAS.HR.* / SSAS.GL.*
   (implements)              (consumes)
```

## What belongs here

A contract belongs here when **a business module must call it** and **Platform must implement it**. That is a
deliberately narrow test: it is not a general dumping ground for shared types.

| Contract | Purpose |
|---|---|
| `ITenantUnitOfWork` | Commit tenant ERP work; distinct from the platform unit of work because the two planes have no shared transaction (`ADR-017`) |
| `ITenantBranchAccessResolver` | The single place branch scope is decided (`ADR-023`) |
| `IBranchTransferScope`, `IBranchTransferAuthorizer`, `BranchTransferDeclaration` | The sanctioned branch-transfer channel (`ADR-024` decision 3) |
| `BranchTransferErrors` | The error vocabulary those contracts return |

## What does NOT belong here

- **Implementations.** They stay in `SSAS.Platform.Infrastructure`.
- **Platform-internal contracts.** `IBranchWriteAuthorizer`, `ICompanyWriteAuthorizer` and
  `ICompanyContextResolver` are consulted only by `TenantDbContext`; no module calls them, so they stay in
  Platform where they can change without a cross-module ripple.
- **EF Core types.** This project has no persistence dependency, so an Application-layer module can
  reference it without pulling EF in. The one EF-shaped contract modules need —
  `ITenantModelContributor` — lives in `SSAS.BuildingBlocks.Infrastructure`, which already owns EF.
- **Anything only Platform uses.** Moving a contract here widens its blast radius permanently.
