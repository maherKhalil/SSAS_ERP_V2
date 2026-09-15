# SSAS_ERP_V2 Macro-Roadmap & Task Plan

## Phase 1: ERP API Completion
- [x] **Task 1: FP-001 & FP-003 Unrouted Endpoints**
  Wire up the 17 missing HTTP Minimal API endpoints for Custom Role Management, User Role Assignments, and Support-scoped Tenant User administration.

## Phase 2: ERP FrontEnd (Angular + Tailwind)
- [x] **Task 2.1: Initialize Angular Workspace & Tailwind**
  Create a new `ui` directory in the repository root. Generate a new Angular application using the Angular CLI. Install and configure Tailwind CSS. Set up standard formatting and linting.
- [ ] **Task 2.2: Global Auth & Permission Architecture**
  Create an Authentication service, Auth Guards, and a Permission service. Ensure that pages (including dashboards) can be protected by specific permission keys (e.g., matching the backend's PlatformPermissionNames).
- [ ] **Task 2.3: Module Selector (Card Layout)**
  Implement the root landing page (/modules). It should display a grid of "Cards" representing every available ERP module (e.g., HR, Payroll, Finance, Subscription). These cards should only be visible if the user has permissions for the module.
- [ ] **Task 2.4: Module Dashboards & Dynamic Menus**
  Implement the layout shell (Sidebar + Header inspired by the DreamSERP template). When a user clicks a Module Card, they are navigated to that module's specific dashboard (e.g., /hr/dashboard). The sidebar menu must dynamically update to show *only* the links relevant to the selected module.
- [ ] **Task 2.5: API Client Generation & Integration**
  Generate TypeScript clients from the .NET backend OpenAPI/Swagger spec and wire up the environment configurations to communicate with the local API host.

## Phase 3: HIS API
*(Tasks to be defined once Phase 2 is fully complete. Includes finishing the HIS EF Core integrations and API surface.)*

## Phase 4: HIS Frontend
*(Tasks to be defined once Phase 3 is fully complete. Will integrate seamlessly into the ERP frontend architecture.)*

## Phase 5: Complete Data Migration Script
*(Tasks to be defined once Phase 4 is fully complete. Will combine both ERP and HIS data migrations into the final schema mappings.)*
