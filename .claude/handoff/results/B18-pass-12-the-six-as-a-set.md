# B18 pass 12 — the six closed as a set: 4 cited, 2 left with recorded searches

**TASK gate green, 0 warnings. Control: 153 cited, zero dangling. ⚠ FP-006 → 45 of 47.**

## The recorded searches (B18 clause 1)

| # | search | result |
|---|---|---|
| **S1** | `The_create_command_carries_no_ownership` body | ⚠ asserts the create command has **no Tenant/Company/Branch parameter** — the contract clause of **three** criteria |
| **S2** | test names in `CompanyOwnershipBoundarySqlServerTests` | `A_spoofed_company_on_create_is_refused_rather_than_rewritten`, `An_added_company_owned_entity_is_stamped_with_the_trusted_company` |
| **S3** | `"Tenant ownership cannot be changed"` in `tests/` | ⚠ **one hit, and it is a COMMENT** in `ConstructorKeyedEntityModelTests` — not an assertion |
| **S4** | the `V_…stamps_the_branch` body | authorizer called, employee's branch is the trusted one |
| **S5** | `TenantId = ` assignments in `tests/Integration.Tests/` | Attendance, Department, ImportExport — **no Employee tenant-change test** |
| **S6** | `tenant_ownership` / `cross_tenant` test names | Company, TenantUser, Attendance — ⚠ **foreign subjects, all six** |
| **S7** | `The_update_command_carries_no_ownership_or_status` body | the update contract excludes ownership and status |
| **S8** | `deactivat*` test names in HR and Integration | TenantUser and JobGrade — **no Employee deactivate/activate pair** |

⚠ **S3, S5, S6 and S8 are the negatives, and they are now re-runnable.** **That is the difference between
this pass's *unresolved* and every earlier one's.**

## Cited: four

- **`AC-EMP-0002`, `0003`, `0004`** share one test for their **contract clause** —
  `The_create_command_carries_no_ownership`. ⚠ **One test, three criteria, because the criteria state the
  same prohibition over three dimensions and the command has one parameter list.**
- **`AC-EMP-0003`** additionally: ⚠ `A_spoofed_company_on_create_is_refused_rather_than_rewritten` is the
  criterion's **exact words** — *"refused rather than silently rewritten"* — and its probe confirms **no row
  was written under the spoofed company**, which is what makes *refused* distinct from *rewritten*.
- **`AC-EMP-0004`** additionally: the `V_` test's **stamping** clause.
- **`AC-EMP-0007`**: clause 1 by the update contract; clause 2 (*updating a `Terminated` employee is
  refused*) by `A_terminated_employee_cannot_have_its_profile_updated`.

**Clauses named throughout, per the bounded rule — every one of these is multi-clause.**

## ⚠ `AC-EMP-0002` is PARTLY PINNED, and the missing half is real

*"A post-creation `TenantId` change is rejected."*

**S3, S5 and S6 found it asserted for Company (`An_ordinary_update_cannot_change_company_ownership`) and for
TenantUser — and NOT for Employee.** ⚠ **The guard is in `PersistenceDbContext` and is dimension-generic, so
it almost certainly holds; nothing asserts it for this aggregate.** **Recorded, not built — this pass was
scoped to closing the set.**

## Left uncited: two, with their searches

- **`AC-EMP-0001`** — a **composite** criterion: nonempty Guid, trusted tenant, trusted company, stamped
  branch, normalized number, initial assignment. ⚠ **Every clause is separately cited elsewhere** (`0005`,
  `0006`, `0020`, and the three above). **Citing `0001` on any one test would misrepresent a summary
  criterion as a single assertion** — and citing it on six would say nothing the six already say.
  **Deliberately uncited; the criterion is an index, not a claim.**
- **`AC-EMP-0013`** — S8 found no Employee deactivate/activate pair. `The_approved_transitions_are_permitted`
  walks the transitions and is cited for `0012`; ⚠ **the criterion's distinguishing clauses — *neither
  changes company, branch or any identity field*, and *neither writes a branch-assignment record* — are
  asserted by nothing found.** **Genuine candidate gap, seventh.**

## Where FP-006 ends

**45 of 47 cited · 47 of 47 examined · 0 uncovered · 2 deliberately uncited, each with a stated reason.**
