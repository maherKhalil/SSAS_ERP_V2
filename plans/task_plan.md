# SSAS_ERP_V2 Macro-Roadmap & Task Plan

## Phase 1: ERP API Completion
- [ ] **Task 1: FP-001 & FP-003 Unrouted Endpoints**
  Wire up the 17 missing HTTP Minimal API endpoints for Custom Role Management, User Role Assignments, and Support-scoped Tenant User administration. These routes are marked [NOT ROUTED] in the API contracts but their MediatR handlers already exist. Write corresponding integration tests in 	ests/API.Tests to verify.

## Phase 2: ERP FrontEnd
*(Tasks to be defined once Phase 1 is fully complete)*

## Phase 3: HIS API
*(Tasks to be defined once Phase 2 is fully complete. Includes finishing the HIS EF Core integrations and API surface.)*

## Phase 4: HIS Frontend
*(Tasks to be defined once Phase 3 is fully complete. Will integrate seamlessly into the ERP frontend architecture.)*

## Phase 5: Complete Data Migration Script
*(Tasks to be defined once Phase 4 is fully complete. Will combine both ERP and HIS data migrations into the final schema mappings.)*
