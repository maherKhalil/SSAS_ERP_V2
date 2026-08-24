# FP-012 — Test Scenarios (PROPOSED)

`TS-PAY-####`, mapped to the acceptance criteria they exercise and to the layer each belongs in.

**Layer legend:** *Domain* — `tests/Payroll.Tests` (name to be confirmed against the solution, not
guessed). *API* — `tests/API.Tests`. *Arch* — `tests/Architecture.Tests`. *Integration* —
`tests/Integration.Tests`, real SQL Server.

---

## Domain

| ID | Scenario | AC | Layer |
|---|---|---|---|
| `TS-PAY-0001` | A second compensation record for the same employee leaves the first intact and becomes in force from its own date. | `AC-PAY-0001` | Domain |
| `TS-PAY-0002` | Resolving compensation for a date between two records returns the earlier one. | `AC-PAY-0002` | Domain |
| `TS-PAY-0003` | Resolving for a date before the first record returns nothing rather than the first. | `AC-PAY-0002` | Domain |
| `TS-PAY-0004` | An element code cannot be changed once created. | `AC-PAY-0007` | Domain |
| `TS-PAY-0005` | Elements evaluate in ascending calculation order, and a later element sees an earlier one's result. | `AC-PAY-0009` | Domain |
| `TS-PAY-0006` | A run refuses to move Draft → Approved without passing through Calculated. | `AC-PAY-0015` | Domain |
| `TS-PAY-0007` | A posted run refuses recalculation, edit and re-approval. | `AC-PAY-0017` | Domain |
| `TS-PAY-0008` | An employee terminated mid-period is included; one terminated before it begins is not. | `AC-PAY-0011`, `AC-PAY-0012` | Domain |
| `TS-PAY-0009` | Line amounts sum exactly to the run's stated total. | `AC-PAY-0026` | Domain |
| `TS-PAY-0010` | Proration of a mid-period joiner matches the ruled convention exactly at both boundaries (first day, last day). | `AC-PAY-0013` | Domain |

## Architecture

| ID | Scenario | AC | Layer |
|---|---|---|---|
| `TS-PAY-0011` | No Payroll assembly references a GL assembly, and no GL assembly references a Payroll assembly. | `AC-PAY-0025` | Arch |
| `TS-PAY-0012` | No Payroll API assembly references a Platform assembly (`ADR-012`). | — | Arch |
| `TS-PAY-0013` | Every payroll entity that is tenant-owned implements `ITenantOwnedEntity`. | `AC-PAY-0029` | Arch |
| `TS-PAY-0014` | Every payroll permission name follows `<Plane>.<Resource>.<Action>` and appears in the catalog contributor. | — | Arch |
| `TS-PAY-0015` | No payroll domain type exposes a public setter for a monetary property. | — | Arch |

## API

| ID | Scenario | AC | Layer |
|---|---|---|---|
| `TS-PAY-0016` | **Every payroll write route binds a correctly-cased JSON body and returns success, not `400 request.invalid`.** | — | API |
| `TS-PAY-0017` | The route inventory is exactly the documented surface, by name — not by count. | — | API |
| `TS-PAY-0018` | Every payroll route carries a permission policy; none is empty. | `AC-PAY-0027` | API |
| `TS-PAY-0019` | No payroll route responds to `DELETE`. | — | API |
| `TS-PAY-0020` | Approval is refused to a caller holding every payroll permission except `Payroll.Runs.Approve`. | `AC-PAY-0016` | API |
| `TS-PAY-0021` | A caller with every HR permission and no payroll permission reads no compensation and no payslip. | `AC-PAY-0027` | API |
| `TS-PAY-0022` | A request supplying a currency code is refused as an unknown property. | — | API |
| `TS-PAY-0023` | A request supplying its own read scope is refused. | `AC-PAY-0028` | API |

> **`TS-PAY-0016` is not routine.** It is written first and named explicitly because its absence in FP-011
> meant **every GL write route returned `400 request.invalid`** while the routes, handlers, domain and error
> mapper were all correct. The defect was a *missing attribute* — an absence, which no review of the code
> reveals. This scenario exists so Payroll cannot repeat it.

## Integration (real SQL Server)

| ID | Scenario | AC | Layer |
|---|---|---|---|
| `TS-PAY-0024` | Every payroll string column is `nvarchar`; every monetary column is `decimal(19,4)`. | `AC-PAY-0030` | Integration |
| `TS-PAY-0025` | Amounts round-trip at four decimal places without loss. | `AC-PAY-0030` | Integration |
| `TS-PAY-0026` | No foreign key crosses from a payroll table to the Platform database. | `AC-PAY-0031` | Integration |
| `TS-PAY-0027` | A posted run's lines cannot be updated or deleted through the context. | `AC-PAY-0017` | Integration |
| `TS-PAY-0028` | The five payroll tables appear in the E3 manifest and a real cutover carries payroll data for the moved tenant only. | `AC-PAY-0029` | Integration |
| `TS-PAY-0029` | Posting produces exactly one balanced journal in the fiscal period containing the pay date. | `AC-PAY-0019`, `AC-PAY-0020` | Integration |
| `TS-PAY-0030` | A run whose pay date falls in a closed period is refused at approval, naming the period. | `AC-PAY-0022` | Integration |
| `TS-PAY-0031` | Closing a period cannot be undone by any payroll operation. | `AC-PAY-0023` | Integration |
| `TS-PAY-0032` | A correction produces a reversing journal and a second run, leaving the originals unchanged. | `AC-PAY-0024` | Integration |
| `TS-PAY-0033` | Compensation in one company is unreadable from another under a real authorizer. | `AC-PAY-0005` | Integration |

---

## Notes the build prompt will need

**The cutover suite will go red when payroll tables are added, and that is correct.**
`TenantCutoverCopySqlServerTests` pins the tenant-owned entity inventory in **several different shapes** —
literal name arrays, arithmetic against a derived count, and a `TablesCopied` literal. FP-011's sweep found
one shape and missed the rest, and the gate found the remainder. **Derive the inventory and search for
every shape.** Count moves 20 → 25.

**Collection membership.** No payroll test class should join `TenantBackupSerialSuites` unless it holds a
resource shared **across databases** — the collection states its own admission rule, and "it is heavy" and
"it needs real SQL" are explicitly not reasons.

**The test project name must be confirmed against the solution before it is written into any
`InternalsVisibleTo`.** FP-011 wrote `SSAS.GL.Tests`, which did not exist; the canonical home was
`tests/Finance.Tests`.

**No scenario asserts a tax figure, an attendance-derived value, or a rounding mode** — the first two have
no authority and the third awaits `OD-PAY-0008`.
