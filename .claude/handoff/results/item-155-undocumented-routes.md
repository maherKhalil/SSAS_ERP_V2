# item 155 — the mirror: mapped routes that appear in no `api-contracts.md`

**Measurement only. Nothing built, nothing edited.**

Every earlier sweep ran **documented → built**. This runs **built → documented**. Of **155 routes mapped in
`src/`, 26 appear in no `api-contracts.md`.**

## ⚠ The control had to be two-sided

A defect in the route parser (`/me/` wrongly treated as an absolute template) would have made
`me/records` — which FP-013 documents — appear undocumented. **The defect would have manufactured the very
finding this item exists to measure.** So both directions were asserted:

- `GET /api/payroll/me/payslips` must come back **undocumented** (the known positive), and
- `GET /api/attendance/me/records` must come back **documented** (the anti-vacuity control).

**A one-sided control passes either way.**

## The 26, in five classes

**A — documented, in a form no route-extractor can see (6). Not oversights.**
- `POST /api/hr/departments/{}/move-to-root` — FP-007 names it after a `+` in an as-built annotation, so
  the method is not adjacent to it. Also in `decisions-approved.md` and `traceability-matrix.md`.
- The five `/api/hr/salary-grades` routes — FP-008 documents the whole family **by an invariant**:
  *"`/api/hr/salary-grades` **mirrors this exactly** under `HR.SalaryGrades.*`. Both families exist
  (`OD-POS-002`)."* ⚠ **One sentence deliberately standing in for five rows** — and the extractor scored it
  as five undocumented routes. **Discipline compresses; shape-matchers count.**

**B — documented in the package, absent from its own `api-contracts.md` (1).**
`GET /api/payroll/me/payslips` — present in FP-012's `authorization-model.md` and `traceability-matrix.md`.
The permission and the trace exist; the contract row does not. **The one genuine contract gap.**

**C — documented only in an ADR (2).** `POST /api/platform/tenant-users/{}/employee-link` and
`/employee-link/remove` — `ADR-030-Identity-To-Employee-Mapping.md`.

**D — infrastructure, deliberate (1).** `GET /` — the Host root returning application info. No feature
package owns it and none should.

**E — no contract anywhere (16).**
- Platform support **authority** (9): `GET /support/principals`, `/{id}`, `/{id}/assignments`,
  `/{id}/permissions`; `POST /support/principals`, `/{id}/disable`, `/{id}/grant`, `/{id}/reenable`,
  `/{id}/revoke`
- Platform support **authentication** (3): `POST /support/auth/login`, `/logout`, `/refresh`
- **Payroll** (4): `GET` and `POST /api/payroll/periods`,
  `POST /api/payroll/employees/{}/one-off-payments`, `POST /api/payroll/runs/{}/reversals`

## ⚠ The 16 are NOT untracked work

A first draft of this said "16 routes nobody documented." Widening the search from `docs/` to every
markdown in the repository, **every one is traceable in `.claude/handoff/results/`** — support authority
T-073, support auth T-072 and T-077, one-off payments T-110/124/125, periods T-112/124/175, reversals
T-041/098/110. **The build record exists and the CONTRACT does not.** Different defect, different owner,
different fix — and it turned on searching one directory versus the repository.

**Nor are the support routes unpinned.** Their inventory is fixed in a TEST
(`PlatformSupportAuthorityRouteInventoryTests`), which for drift purposes is stronger than a markdown row:
a new or re-gated route **fails the build**, which no document can do. See
`item-156-support-surface-verification.md`.

## Scope — what this excludes

Routes registered by a mechanism the parser does not read (reflection, conventions, minimal-API
registration outside `Map*` with a literal or single-valued constant template). The 155 is what this
parser resolves, not a proof of the whole surface.

## One question, flagged as a question

FP-014's **unbuilt** `POST /api/platform/tenants/{}/grants/revoke` and the support plane's **built**
`POST /support/principals/{}/revoke` are different subsystems. An earlier noun-matcher paired them; that
pairing was noise. **Whether the support principals cover any part of FP-014's unbuilt grant capability
cannot be determined from route names, and is the owner's to answer.**
