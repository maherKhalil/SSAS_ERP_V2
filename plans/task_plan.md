# SSAS_ERP_V2 Macro-Roadmap & Task Plan

## Phase 1: ERP API Completion
- [x] **Task 1: FP-001 & FP-003 Unrouted Endpoints**

## Phase 2: ERP FrontEnd (Angular + Tailwind)
- [x] **Task 2.1: Initialize Angular Workspace & Tailwind**
- [x] **Task 2.2: Global Auth & Permission Architecture**
- [x] **Task 2.3: Module Selector (Card Layout)**
- [x] **Task 2.4: Module Dashboards & Dynamic Menus**
- [x] **Task 2.5: API Client Generation & Integration**

## Phase 3: HIS API
- [x] **Task 3.1: HIS Core Registration API**
- [x] **Task 3.2: HIS Application Setup API**

## Phase 4: HIS Frontend
- [x] **Task 4.1: HIS Patient Registration UI**
  Create the Angular components for Patient Registration inside `ui/src/app/features/his/registration/`. Wire up the routing for `/his/patients` and integrate the UI into the `ShellComponent` layout.
- [x] **Task 4.2: HIS Module Integration**
  Update the `ModuleSelectorComponent` to include a "Health Information System" card (secured by `HIS.Registration.Manage`). Update the `SidebarMenuService` to display HIS-specific links when the HIS dashboard is active.

## Phase 5: Complete Data Migration Script
*(Tasks to be defined once Phase 4 is fully complete. Will combine both ERP and HIS data migrations into the final schema mappings.)*

