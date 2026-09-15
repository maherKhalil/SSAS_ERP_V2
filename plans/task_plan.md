# SSAS_ERP_V2 Macro-Roadmap & Task Plan

## Phase 1: ERP API Completion
- [x] **Task 1: FP-001 & FP-003 Unrouted Endpoints**
  Wire up the 17 missing HTTP Minimal API endpoints for Custom Role Management, User Role Assignments, and Support-scoped Tenant User administration.

## Phase 2: ERP FrontEnd (Angular + Tailwind)
- [x] **Task 2.1: Initialize Angular Workspace & Tailwind**
- [x] **Task 2.2: Global Auth & Permission Architecture**
- [x] **Task 2.3: Module Selector (Card Layout)**
- [x] **Task 2.4: Module Dashboards & Dynamic Menus**
- [x] **Task 2.5: API Client Generation & Integration**

## Phase 3: HIS API
- [ ] **Task 3.1: HIS Core Registration API**
  Build the MediatR Handlers and Minimal API REST controllers for Patient Registration (Registration schema). This includes endpoints to register patients, manage patient demographics, and list patients. Ensure TenantId isolation is properly enforced.
- [ ] **Task 3.2: HIS Application Setup API**
  Build foundational lookup endpoints for HIS Application Setup (e.g., Doctors, Clinics, Specialties). These endpoints provide dropdown data for the clinical frontends.

## Phase 4: HIS Frontend
*(Tasks to be defined once Phase 3 is fully complete. Will integrate seamlessly into the ERP frontend architecture.)*

## Phase 5: Complete Data Migration Script
*(Tasks to be defined once Phase 4 is fully complete. Will combine both ERP and HIS data migrations into the final schema mappings.)*
