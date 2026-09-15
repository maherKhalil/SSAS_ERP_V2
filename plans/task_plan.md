# SSAS_ERP_V2 Macro-Roadmap & Task Plan

## Phase 1: All ERP Backend
- [x] **Task 1: FP-001 & FP-003 Unrouted Endpoints** (Complete)

## Phase 2: All ERP FrontEnd (Angular + Tailwind)
- [x] **Tasks 2.1 - 2.5: UI Initialization, Auth, Layouts, API Generation** (Complete)

## Phase 3: All HIS Backend
*(Strict Rule: Must complete ALL clinical domains before proceeding to Phase 4)*
- [x] **Task 3.1: HIS Core Registration API**
- [x] **Task 3.2: HIS Application Setup API**
- [ ] **Task 3.3: Outpatient (OPD) & Inpatient (IPD) APIs**
  Build MediatR handlers and Minimal APIs for OutPatient and InPatient schemas.
- [ ] **Task 3.4: Pharmacy & ClinicalPharmacy APIs**
  Build MediatR handlers and Minimal APIs for medication dispensing and inventory.
- [ ] **Task 3.5: Laboratory & Radiology APIs**
  Build MediatR handlers and Minimal APIs for lab tests and imaging.
- [ ] **Task 3.6: Emergency & BloodBank APIs**
  Build MediatR handlers and Minimal APIs for trauma and blood stock.
- [ ] **Task 3.7: Billing & Insurance APIs**
  Build MediatR handlers and Minimal APIs for clinical billing.
- [ ] **Task 3.8: Remaining Ancillary Clinical APIs**
  Build MediatR handlers and Minimal APIs for CSSD, Nursing, Operations, Nutrition, Laundry, Maintenance, etc.

## Phase 4: All HIS Frontend
- [ ] *(Locked until Phase 3 is 100% complete)*

## Phase 5: Complete Data Migration Script
- [ ] *(Locked until Phase 4 is 100% complete)*
