# Task Plan

## Phase 3: Test-Driven Delegation Loop

- [x] **Task 1: Architecture Tests T-191 Fix**
  Update `tests/Architecture.Tests/*.cs` (`AuthenticationMilestoneArchitectureTests.cs`, `DeclaredDependencies.cs`, `PositionApplicationArchitectureTests.cs`, `SubscriptionResidencyArchitectureTests.cs`) to explicitly exclude `bin` and `obj` directories from `Directory.GetFiles` and `Directory.EnumerateFiles` walks instead of relying on directory containment or file extension patterns.

- [x] **Task 2: Fix Failing Integration Tests**
  - Fix `CatalogLeakGuardTests.No_test_catalog_survived_a_previous_run` by dropping leaked test databases.
  - Fix `PayrollSchemaSqlServerTests.The_same_element_code_is_free_in_a_second_company` which is currently failing (SQL exception / Uniqueness).

- [x] **Task 3: T-191 API Test Renaming**
  Rename the tests in `tests/API.Tests` (and others) that claim a lock/race but only test a 409 mapping, as listed in `.claude/handoff/results/T-191.md`. Specifically:
  - `A_busy_fiscal_calendar_is_409_and_names_a_retryable_condition`
  - The five `_race_is_409_rather_than_500` tests in Gl and Payroll.
  - The three constraint-named department tests (`D23_...`, `D24_...`, `D6_...`).

- [ ] **Task 4: Implement IDepartmentHierarchyLock Integration Test**
  As detailed in `T-191.md`, implement a real integration test for `IDepartmentHierarchyLock` with a second connection to prove lock contention. This is the missing behavioral evidence for the hierarchy lock.

- [ ] **Task 5: Implement Subscription Billing (FP-014)**
  Begin implementation of the missing billing half of FP-014 (Subscription).
  - Define `Invoice`, `PaymentAttempt`, `Overage`, `Proration`, and `SeatUsage` models.
  - Write corresponding unit tests.
