# Engineering

This section holds the durable engineering guidance for SSAS ERP V2: the architecture principles that every implementation must follow, and the Architecture Decision Records (ADRs) that capture binding technical decisions.

## Contents

- [Architecture-Principles.md](Architecture-Principles.md) — the mandatory engineering principles (module boundaries, Clean Architecture flow, CQRS, multi-tenancy, security, and the composition-root exception).
- [ADR/](ADR/) — the Architecture Decision Records directory.
  - [ADR/README.md](ADR/README.md) — the index of all ADRs and their statuses.
  - [ADR/ADR-Template.md](ADR/ADR-Template.md) — the template every new ADR follows.

## How this section is used

- Architecture principles and accepted ADRs are authoritative: no implementation may violate an accepted ADR.
- A new binding architectural decision is recorded as a new ADR using the template, reviewed, and then set to `Accepted`.
- This README is an index only; it introduces no architectural decisions of its own.
