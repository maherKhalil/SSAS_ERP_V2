---
id: ADR-001
title: Adopt Modular Monolith Architecture
status: Accepted
date: YYYY-MM-DD
---

# Context

SSAS ERP V2 is a greenfield SaaS ERP platform expected to grow into dozens of business modules while being developed by a small engineering team with extensive AI-assisted development.

The architecture must support rapid delivery, maintainability, and a future transition to microservices if business or scaling requirements justify it.

---

# Decision

The system shall be implemented as a **Modular Monolith**.

Each module owns:

- Domain
- Application
- Infrastructure
- API
- Contracts

Modules communicate through contracts and domain events.

No module may directly reference another module's Infrastructure layer.

---

# Alternatives Considered

## Microservices

Pros

- Independent deployment
- Independent scaling

Cons

- Operational complexity
- Distributed transactions
- Higher infrastructure cost
- Increased AI implementation complexity

---

## Traditional Layered Monolith

Pros

- Simple

Cons

- Poor module isolation
- Difficult future migration

---

# Consequences

Benefits

- Faster development
- Easier debugging
- Lower infrastructure cost
- Easy migration path

Tradeoffs

- Entire application deploys together
- Shared process memory

---

# Implementation Notes

Module boundaries must remain strict to simplify future extraction into microservices.

---

# References

- Solution Architecture
- Modular Monolith
- Sprint 00

# Depends On

None

This is the foundational architectural decision for SSAS ERP V2.

---

# Related ADRs

| ADR | Relationship |
|------|--------------|
| ADR-002 | Defines the database platform used by the modular architecture |
| ADR-003 | Defines the internal structure of each module |
| ADR-004 | Defines how application use cases are implemented |
| ADR-005 | Defines tenant isolation across all modules |
| ADR-006 | Defines authentication and authorization within the modular architecture |
| ADR-007 | Defines the frontend architecture consuming the modular APIs |
| ADR-008 | Defines the ORM implementation |
| ADR-009 | Defines communication between modules using domain events |
| ADR-010 | Defines persistence abstraction |