# SunCity_Clinics → SSAS: the measured plan

**Planning only.** Nothing here is built. The owner has ruled: **one product — HIS with the ERP
included**; the source is **on-premises**, so `TenantID` was never needed and must be **added**; **our
ERP finishes first**; and a full-size database can be obtained later, so this plans the part that works
without one.

Every number below is emitted by the two scripts beside this file. **Re-run them before trusting any of
it** — that is the point of committing them rather than the prose alone.

```
python scripts/his-catalogue/parse_his_schema.py <script.sql> --out scripts/his-catalogue/catalogue
python scripts/his-catalogue/analyse_seam.py scripts/his-catalogue/catalogue/catalogue.json
```

---

## 1. The source, measured

```
tables                 1754        procedures        1347        views      177
schemas                  38        indexes             47        functions   12
foreign keys           1988        IDENTITY cols     1541        uniqueidentifier  100
```

**Read the object manifest, never the CREATE statements.** SSMS emits one
`/****** Object: Table [x].[y] ******/` header per scripted object. That **enumerates**; a
`CREATE …` grep **matches**, and the two disagree badly:

| | manifest | `CREATE` grep | first census |
|---|---|---|---|
| tables | 1754 | 1758 (4 are `#MyStored` temp tables inside procedures) | 1754 |
| **procedures** | **1347** | 538 `CREATE PROCEDURE` + 728 `CREATE PROC` | **518** |
| views | 177 | 173 distinct | 172 |

**The procedure count was wrong by 2.6×** because T-SQL has two spellings and the first pattern knew
one. `DEC-L-086`: the instrument that enumerates wins.

---

## 2. ⚠ Three source properties that will break a naive tool

### The encoding returns a confident zero

The script is **UTF-16LE**. A line-anchored grep over the raw bytes matches **nothing at all** — not an
error, a finding-shaped nothing, for every pattern at once. `parse_his_schema.py` **refuses** a file
without a UTF-16 BOM rather than emitting an empty catalogue.

### Two tables collide when a name is normalised

```
[dbo].[Pharmacy.Borrowing]      a table whose NAME contains a dot
[Pharmacy].[Borrowing]          a genuinely different table
```

**Both exist.** Any tool that splits the first on `.` maps it onto the second, and one is dropped or
overwritten — **silently, with no parse error, because both names are valid.**
`[Registration].[Registration.Companies]` is the same shape. **The catalogue's key is therefore the
bracketed, unparsed name**, and the collision is a self-test fixture.

### An identifier filter renames rather than drops

`[A-Za-z0-9_]` does not discard `RepEmployees'Vacations` — it yields `RepEmployees`, **a perfectly
believable procedure name**. Nine procedures and ten tables carry apostrophes, hyphens, `$`, or
leading/trailing spaces (` ConsumptionRateAnalysis`, `PermissionRequestsReport `).

**The row survives and the KEY is corrupted.** A count cannot catch it and both sides of a diff look
plausible. The parser **refuses** an identifier it cannot round-trip.

**Four `$`-suffixed tables are Excel-import residue** — `Users$`, `icd102019_chapters$`, `_codes$`,
`_groups$`. **The ICD-10 2019 code set is an unmanaged spreadsheet import**, in a system that bills
against diagnosis codes.

---

## 3. The seam (item 31)

The partition is declared **once**, in `analyse_seam.py`. Moving a schema between sides changes every
number below at once.

```
ERP        658 tables   Finance 271, HR 123, GeneralStores 113, Purchasing 26, Accounting 21,
                        Assets 20, Banking 20, GL 18, PayRoll 15, Budgeting 13, Receivable 12,
                        Contracts 4, ZaKat 2
clinical  1096 tables

foreign keys   ERP-internal 495   clinical-internal 1334   CROSSING 159  (82 erp→clinical, 77 back)
```

**The 159 are not scattered — they are four joints:**

| joint | edges | what it means |
|---|---|---|
| **HR ↔ Nursing** | 39 | Nursing owns the org structure HR points at. The employee master lives in `Nursing`, not `HR`. |
| **GeneralStores as hub** | 15 Marketing, 10 Purchasing, 8 ApplicationSetup, 7 InPatient, 4 each Nursing/Maintenance | Inventory is a **shared service**, not an ERP module. That is correct design and must survive. |
| **Assets ↔ Maintenance** | 8 | the asset lifecycle crosses into clinical operations |
| **ApplicationSetup** | 8 + 3 Banking | shared master data, referenced from both sides |

**Each of the 159 needs a disposition — comes along, becomes a reference into HIS, or is dropped. That
decision list IS the migration scope.**

### ⚠ But it is not 159 decisions. It is three, and then 29. (T-216, counts as at 2026-08-30)

An earlier reading of this section — including the sentence directly above — treated the 159 as a decision
list. **It is an edge list. Most of the edges are CONSEQUENCES of a small number of placement decisions
nobody has taken yet**, and enumerating consequences as decisions hides the decisions.

**All figures below were recomputed from `catalogue/catalogue.json` and reproduce this document's own
published totals exactly — crossing 159, erp→clinical 82, clinical→erp 77** — so the ERP/clinical schema
split used here is the one this plan was written from. A misclassification would have silently changed every
number, which is why the control was run before anything was built on it.

| # | decision | edges it settles | recommendation |
|---|---|---|---|
| **D1** | **Is `GeneralStores` a shared service or an ERP module?** | **59** (37%) | **Shared.** This document already argues it: inventory is referenced by Marketing, InPatient, Nursing, Maintenance, CSSD, Emergency and Laboratory. That is correct design and must survive. |
| **D2** | **Is `ApplicationSetup` shared master data?** | **8** | **Shared.** Cities, governorates, countries, banks — referenced from both sides and owned by neither. |
| **D3** | **Who owns the organisational structure of the hospital — HR, or the clinical modules?** | **63** (40%) | **Genuinely open — this is the real architectural question, and it is answerable without reading a schema.** `Nursing.Employee` is one person master with two role extensions (`Doctors`, `NurseMaster`), while `HR.Department` / `HR.SubDepartment` are pointed at from InfectionControl, CSSD, Billing and InPatient. **Referenced in BOTH directions, so it cannot simply follow ERP.** |
| | **settled by the three** | **130 of 159 (82%)** | |
| | **remainder needing per-edge disposition** | **29** | listed below |

**D1 and D2 are close to settled by the owner's own framing — *one product, HIS with ERP included*. They
need RATIFYING, not deriving.** D3 is the one that deserves argument, and it is worth noting that the
answer decides 40% of the seam on its own.

**⚠ D3 IS ONE DECISION ONLY IF ALL THREE PARTS LAND TOGETHER. THEY ARE COUPLED BY THE EDGES, NOT
IDENTICAL** — and a plan that offers two options where there are four invites the fourth to be proposed in
the meeting instead of here. Each part dissolves its own subset independently:

| part of D3 | edges it dissolves | what it is |
|---|---|---|
| **person master** | **38** | `Nursing.Employee` with its `Doctors` / `NurseMaster` role extensions |
| **org structure** | **14** | `HR.Department`, `HR.SubDepartment` — the ward and department tree |
| **employment reference data** | **11** | `HR.Banks`, `SocialStatus`, `Jobs`, `Unions`, `TypeofContract`, `TaxDistinctionMaster`, `EmployeeTerminationCase` |

**Splitting them is legitimate** — the person master could sit with HR while the ward tree stays clinical —
**and the cost of each split is the edge count above, which stays crossing and joins the reference-or-drop
list.** So D3 is *one* decision at 63 edges if the parts move together, or up to *three* decisions with a
stated price for separating them.

**⚠ And "comes along" is not available to a crossing foreign key.** By definition its endpoints are on
opposite sides, so that outcome exists ONLY as a consequence of a placement that stops the edge crossing —
which is exactly what D1–D3 do for 130 of them. **For the 29 below the choice really is binary: become a
reference, or be dropped.**

#### The 29 that survive the three decisions

| child | parent | direction | trust |
|---|---|---|---|
| `Assets.Asset_Transactions` | `Billing.CashierBox` | ERP→HIS | CHECK |
| `Billing.CustomerMaster` | `GL.accounts` | HIS→ERP | CHECK |
| `CSSD.Machine` | `Assets.AssetsMaster` | HIS→ERP | CHECK |
| `HR.ComprehensiveSocialResearchRequest` | `InPatient.AdmitPatients` | ERP→HIS | CHECK |
| `HR.ComprehensiveSocialResearchRequest` | `Registration.Patients` | ERP→HIS | CHECK |
| `HR.MaterialGrades` | `OutPatient.SpecialityGroupMaster` | ERP→HIS | CHECK |
| `HR.SpecialMemo` | `Registration.Patients` | ERP→HIS | CHECK |
| `Maintenance.AsstesWarenty` | `Assets.AssetsMaster` | HIS→ERP | CHECK |
| `Maintenance.AsstesWarenty` | `Assets.FX_Location` | HIS→ERP | CHECK |
| `Maintenance.MaintenanceRecorde` | `Assets.AssetsMaster` | HIS→ERP | CHECK |
| `Maintenance.MaintenanceRecorde` | `Assets.FX_Location` | HIS→ERP | CHECK |
| `Maintenance.OrginalMaintenanceRequest` | `Assets.AssetsMaster` | HIS→ERP | CHECK |
| `Maintenance.OrginalMaintenanceRequest` | `Assets.FX_Location` | HIS→ERP | CHECK |
| `Maintenance.WarentyAlarmRequest` | `Assets.AssetsMaster` | HIS→ERP | CHECK |
| `Maintenance.WarentyAlarmRequest` | `Assets.FX_Location` | HIS→ERP | CHECK |
| `Pharmacy.LocalPurchaseOrderHeader` | `Purchasing.PurchaseRequest` | HIS→ERP | **NOCHECK** |
| `Purchasing.PurchaseRequestDetail` | `Pharmacy.Drugs` | ERP→HIS | CHECK |
| `Purchasing.PurchaseRequestDetail` | `Pharmacy.GenericNames` | ERP→HIS | CHECK |
| `Purchasing.PurchaseTenderDeliveryDrugBatch` | `Pharmacy.Substores` | ERP→HIS | CHECK |
| `Purchasing.PurchaseTenderDrug` | `Pharmacy.Drugs` | ERP→HIS | CHECK |
| `Purchasing.PurchaseTenderDrug` | `Pharmacy.GenericNames` | ERP→HIS | CHECK |
| `Purchasing.PurchaseTenderDrug` | `Pharmacy.UnitConversionFactor` | ERP→HIS | CHECK |
| `Purchasing.PurchaseTenderSupplierGivingDrug` | `Pharmacy.Drugs` | ERP→HIS | CHECK |
| `Purchasing.PurchaseTenderSupplierGivingDrug` | `Pharmacy.GenericNames` | ERP→HIS | CHECK |
| `Purchasing.PurchaseTenderSupplierGivingDrug` | `Pharmacy.UnitConversionFactor` | ERP→HIS | CHECK |
| `Receivable.InsuranceClaims` | `Billing.InsuranceCompanies` | ERP→HIS | CHECK |
| `Receivable.InsuranceClaimsMaster` | `Billing.InsuranceCompanies` | ERP→HIS | CHECK |
| `Receivable.InsuranceDeposite` | `Billing.InsuranceCompanies` | ERP→HIS | CHECK |
| `Receivable.PatientPackagePaymentMaster` | `Registration.Patients` | ERP→HIS | CHECK |

**They are four themes, not twenty-nine unrelated choices:** drug procurement (`Purchasing ↔ Pharmacy`, 10),
the asset lifecycle (`Maintenance`/`CSSD → Assets`, 9), insurance and patient billing
(`Receivable`/`Assets → Billing`/`Registration`, 6), and social-work case records (`HR → InPatient` /
`Registration` / `OutPatient`, 4).

**⚠ `Pharmacy.LocalPurchaseOrderHeader → Purchasing.PurchaseRequest` is one of only TWO `NOCHECK` foreign
keys among all 159** (the other being `FK_NurseMaster_Employee`, discussed below). Orphans may already
exist on both.

**That edge is BLOCKED ON THE PRODUCTION COPY, not decided.** A reference across the boundary is a promise
about integrity, and this relationship has never been checked by the database — so whether it can become a
reference at all depends on whether orphans exist, which is not knowable from the catalogue. **It is the
one item in the 29 whose disposition cannot be argued, only measured.**

⚠ **One earlier reading was wrong and is corrected here.** `Registration.Patients` (160 inbound FKs),
`Nursing.Employee` (108) and `Registration.Doctors` (80) are **not** three unreconciled staff masters.
`Doctors.EmployeeId → Nursing.Employee.Id` and `NurseMaster.EmployeeId → Nursing.Employee.Id`: **one
person master with two role extensions.** Good design, oddly placed.

**What is actually wrong there:** `FK_NurseMaster_Employee` is `WITH NOCHECK` while
`FK_Doctors_Employee` is `WITH CHECK` — **asymmetric trust on the same relationship**, so orphan nurses
may already exist.

**And that asymmetry is genuinely exceptional rather than typical of the seam: exactly 2 of the 159
crossing foreign keys are `NOCHECK` (as at 2026-08-30, T-216)** — this one and
`Pharmacy.LocalPurchaseOrderHeader → Purchasing.PurchaseRequest`. **The number is stated because
"exceptional" without it reads as "typical" to the next person**, which would invert the point.

**99 of 1988 foreign keys were added `WITH NOCHECK`.** Those are where the orphans are, and **they
surface as migration failures rather than source errors**, because the source never checked them.

---

## 4. The crosswalk contract (item 32) — design, not code

### Identity

Source PKs are **`int IDENTITY`, 1541 of them**; ours are `Guid`. So a crosswalk is **mandatory**, and
it must be a table, not a computed mapping: the same source row must resolve to the same `Guid` across
re-runs, or a partial migration cannot be resumed.

**Key:** `(source_table_bracketed, source_id)` → `target_guid`. **The bracketed name, for §2's reason** —
a crosswalk keyed on `schema.table` merges the two `Borrowing` tables and silently maps one table's row
1 onto the other's.

**Uniqueness:** unique on `(source_table, source_id)` and unique on `target_guid`. Both directions, or a
re-run can mint a second `Guid` for a row already migrated.

### Tenancy — injected, never discovered

**`TenantID` occurs zero times in 1754 tables** (a substring search finds `bedlieutenant`; an exact
match finds nothing). The source is on-premises and never needed one.

**So a migration run without an explicit tenant must not start.** Not defaulted, not inferred from the
database name — **refused**, the way `sp_getapplock` with `Transaction` ownership fails when there is no
open transaction. **The failure is the feature**: a run that silently picks a tenant produces data that
looks correct and belongs to nobody.

### Company — ⚠ RESOLVED BY THE OWNER, AND THIS RETIRES THE PLAN'S LARGEST RISK

**`ApplicationSetup.Company` contains exactly ONE row, and that row is the tenant.** Confirmed by the
owner from production, 2026-08-29.

#### What that retires, with the numbers it retires

```
tables carrying a CompanyID    1199 of 1754
tables carrying NONE            555
CompanyID declarations         1199   (one per table)
of those NOT NULL                  3
```

The plan previously required attribution to be **derived** where an FK path existed and **asserted**
where none did, recording which — because a derived company and an asserted one are different evidential
claims. **That reasoning was correct on a premise that is now false.** With one company there is nothing
to derive: every row belongs to the only company there is.

So **assertion is total and correct**, and the `company_source ∈ {declared, derived, asserted}` column is
no longer needed. Struck rather than deleted, because the argument for it returns intact the day a second
company exists.

⚠ **Two of the three NOT NULL columns are spelled `CompanyId`, not `CompanyID`.** A case-sensitive
pattern finds **one of three** — a 67% miss **concentrated in the rarest and most load-bearing class**,
while the bulk count is off by under 1%. Kept, because it is a fact about reading this schema and it will
be true of the next pattern anyone writes against it.

#### ⚠ AND THE CONFIRMATION MUST NOT BE OVER-APPLIED: ONE TENANT, ONE COMPANY, **TWO COLUMNS**

"CompanyID is the tenant id" is true of the DATA and must not become true of the TARGET SCHEMA. Our ERP
has `TenantId` and `CompanyId` as separate required columns, and the single source value populates
**both**.

**Follow the schema, not the count.** `ApplicationSetup.Company` carries `HoldingCompany` and
`ParentCompanyId` — **the source schema is built for a GROUP and currently holds one member.** A row count
of one is a fact about today; the parent and holding columns are a statement of intent, and they disagree.
The day a second clinic or a second legal entity arrives, a migration that mapped company onto tenant has
nowhere to put it and the answer is a re-migration.

#### ⚠ A PRE-MIGRATION GATE: EVERY `CompanyID` MUST NAME THE ONE COMPANY

This is now a DATA-QUALITY question rather than a tenancy one, and the confirmation does not answer it.

**There are 1,199 `CompanyID` columns and THREE foreign keys among them.** So nothing in the database has
ever prevented a row from carrying a `CompanyID` that matches no `Company` row at all — a 2, a 7, a value
left by a previous install.

For every `CompanyID`-bearing table, `SELECT DISTINCT CompanyID` must yield **only NULL and the single**
**`Company.Id`**. **Any other value is a row whose company attribution is a lie**, and it is far cheaper to
find in a gate than halfway through a transfer.

⚠ **The gate must PRINT THE VALUES IT FOUND, not report a pass.** A gate that says "ok" is
indistinguishable from a gate whose query matched nothing, and an empty result must be SEEN rather than
inferred — the same reason the tenant-column count above is printed with its (empty) list beside it.

Cheap to write now; runs the day the copy lands.

#### THE SECOND ITEM ON THAT LIST: `sys.dm_exec_procedure_stats` (T-219)

The procedure triage below establishes 897 reporting and 450 logic, **but it cannot compute dead code at
all** — the schema script holds no application calls, so "referenced by nothing" measures the corpus.

`SELECT * FROM sys.dm_exec_procedure_stats` on the production instance carries execution counts and last
execution times per procedure, which turns that into *"not executed in N days"*.

⚠ **The limit travels with the query: the DMV resets on restart and evicts under cache pressure, so it
yields a LIVE set and never a DEAD one.** Capture the instance's uptime alongside it — a procedure absent
from the DMV on a box restarted yesterday is evidence of nothing.

---

## 5. What this plan does not cover

- **No transfer engine.** Ruled out for now; the ERP finishes first.
- **No reference data exists to inherit.** The 103 `INSERT`s are into **temp tables inside procedure
  bodies**. There is no chart of accounts, no service catalogue, no drug list — "empty" is literal.
- **The script does not stand up alone.** Procedures reference `JCIT_MembershipDB.dbo` (101 times —
  identity, users and roles live in a database **not in this script**), `JCIT_DB_Finance.dbo` (21), and
  hardcoded `TEBAS_GATS_ONCE.Finance.main_acc` / `GATS_AMRI.Finance.main_acc` — **other installations'
  databases left in production code**.
- **Type hazards need a per-column decision, not a per-type rule** — **now resolved: see the `float`
  section above. 1,486 float COLUMNS, of which 544 in real tables hold money or measurements and a further 51 hold identifiers.** `float`
  appears 1490 times in the SCRIPT beside
  `money` 232 and `decimal` 530. **Float cannot represent 0.1**, and this system carries both currency
  and drug dosages. 38 date-named columns are `varchar`; `CreatedBy` is `nvarchar(max)` on 137 tables.
- **Unverified here:** the cross-database reference counts, the 39 disabled constraints, and the
  duplicate `FK_Doctors_SpecialityGroupDetails1/_2` both keyed on `[SpecialityID2]`. Reproduce before
  relying on them.

---

## The 1,347 stored procedures: two-thirds are reporting (T-219, counted 2026-08-30)

**This is the largest line item in the migration and it lands on the favourable side.**

| | count | share |
|---|---|---|
| **REPORTING** — no write anywhere on its path | **897** | **67%** |
| **LOGIC** — writes, directly or through a writer it calls | **450** | **33%** |

**So the work is not "port a thousand procedures". It is rebuild 450 as domain code and re-express 897
as queries** — and a query in the new system is a different kind of artefact from a procedure, written
against the new model rather than translated from the old one.

### The classification was controlled, not trusted

**264 procedures declare their intent in their own names** (`Rep*`, `*Report*`). The body test never
looks at names. **260 of the 264 — 98% — agree.**

That is two independent signals, human intent and mechanical read/write analysis, and **agreement between
independent measurements is worth more than confidence from one.** The four disagreements are real rather
than defects: `[Billing].[E_Claims_Report]` and its siblings genuinely write, which is a report that
stages. **A control that agreed perfectly would suggest the two signals were never independent.**

### ⚠ The third axis — dead code — is NOT COMPUTABLE from this catalogue

A mechanical "referenced by nothing in the script" test returns **1,048 of 1,347 (78%)**. **That number is
not published as a finding, because it is a measurement of the corpus rather than of the estate.** The
script contains schema only; **every call from the application is invisible to it**, so "referenced by
nothing" mostly means "called from code we do not have". Reporting it would read as licence to delete
two-thirds of the estate.

**THE ANSWERING INSTRUMENT IS `sys.dm_exec_procedure_stats`**, which carries execution counts and
last-execution times per procedure. On a production instance that has been up for a reasonable window it
turns *"referenced by nothing in the schema"* into *"not executed in N days"*, which is the real question.

**⚠ Its limit must travel with it: the DMV resets on restart and evicts under cache pressure, so it yields
a LIVE set and never a DEAD one.** A procedure absent from it is **unproven, not unused**. → **Added to
the production-copy gate list**, beside the `SELECT DISTINCT CompanyID` check.

### What the catalogue does support: a bound, not an estimate

**At least 299 procedures are definitely live** — something inside the schema calls them (another
procedure, or a view). Of those, **250 are reporting and 49 are logic.** When a population is partially
invisible, **a bound is honest and an estimate is not**, and which one you have should be stated.

### Limits of the mechanical test

- **42 procedures (3%) use dynamic SQL**, where the read/write test cannot see inside `EXEC(@sql)`.
  **Five of those were classed REPORTING, which the mechanical test could not justify.**

  **⚠ ALL FIVE HAVE NOW BEEN READ (T-220) AND ALL FIVE ARE GENUINELY REPORTING. The 897/450 split is
  unchanged.** Every one builds a `SELECT` — three of them a `PIVOT` — and executes it; **no write verb
  appears anywhere in any of the five bodies, inside the dynamic strings or outside them.**

  | procedure | what it builds |
  |---|---|
  | `[dbo].[DynamicPivotTableInSql]` | `SELECT … PIVOT` over emergency arrivals |
  | `[HR].[SP_EmployeeAssessment]` | `SELECT … PIVOT` of five years of assessment grades |
  | `[HotelServices].[RepWorkListOrder]` | `SELECT` with optional filters appended |
  | `[GeneralStores].[GetInvoicePayment]` | `SELECT` across supplier dues and payments |
  | `[Pharmacy].[GetInvoicePayment]` | the same shape, in the pharmacy schema |

  **This is the one hole the triage stated in its own number, and it is now closed rather than carried.**

- ⚠ **AND READING THEM FOUND SOMETHING THE TRIAGE WAS NOT LOOKING FOR: FOUR OF THE FIVE CONCATENATE
  PARAMETERS DIRECTLY INTO THE SQL STRING.** `[GeneralStores].[GetInvoicePayment]` and its pharmacy twin
  take `@where nvarchar(4000)` and `@inv nvarchar(4000)` and splice them in unquoted — **a caller-supplied
  SQL fragment executed verbatim.** `RepWorkListOrder` concatenates too, but converts typed `int` and
  `datetime` parameters, so its exposure is far narrower.

  **These become queries in the new system, and the injection surface must not be carried across with
  them.** Recorded here because it is a property of the code rather than of the data, so it needs no
  production copy — and because a triage that only asked "does it write" would never have seen it.
- A reporting procedure that `EXEC`s a writer **is** logic; resolved transitively to a fixed point rather
  than one hop.
- Comments are stripped before testing, or a commented-out `INSERT` would make a report look like logic.
- **String literals are NOT stripped**, so a literal containing `INSERT` over-classifies a procedure as
  logic. **That is the safe direction** — it moves work from the cheap bucket to the expensive one.

---

## `float` where 0.1 matters: 425 columns in 214 real tables (T-221, counted 2026-08-30)

`float` cannot represent 0.1. This system carries **both currency and clinical measurements**, so every
float column holding either is a fidelity decision at migration time: **convert to `decimal` and accept the
rounding already baked into the stored values, or carry the drift forward.** This section establishes how
many such columns there are, because twelve would be a footnote and this is not twelve.

### ⚠ First, a correction to this document's own figure

This plan said `float` appears **1490** times. **The schema has 1,486 float COLUMNS.** The other four are
inside a dynamic-SQL string literal in a procedure —

```
set @sqlCommand ='alter TABLE '+@ToDataBAse+'.[Finance].INV_LINE_B_TMP(
	[INV_PAX] [float] NULL, [INV_RATE] [float] NULL, …
```

— so they are **text, not columns.** 1490 was a raw grep; **1,486 is a measurement of the schema.** Money
(232) and decimal (530) reproduce exactly, which is what localises the discrepancy to `float`.

### The count, after reading all 400 distinct residual names

| bucket | total | in `TMP_` staging | **in REAL tables** |
|---|---|---|---|
| money-shaped | 977 | 639 | **338** |
| dose / measurement | 261 | 55 | **206** |
| **where 0.1 matters** | **1,238 (83%)** | 694 | **544, across 239 tables** |
| identifier / code / date | 158 | 107 | 51 |
| unclassified | 90 | 39 | 51 |

The real-table hits are where they would hurt: `[Billing].[CashierBox]`,
`[Pharmacy].[LocalPurchaseOrderDetails]`, `[Assets].[AssetsMaster]`, `[InPatient].[MedicalObservation]`.

### ⚠ The finding is not "some floats hold money". It is that ALMOST NO FLOAT HERE IS APPROPRIATE.

**83% hold money or measurements** and need a `decimal` decision. **A further 158 hold identifiers, codes,
dates and print positions** — `TKT_NR`, `INVNO`, `LIN_NO`, `EntryCodes`, `CHK_SER`, month names — **which is
a second and different defect: an identifier in a floating-point type.** 51 of those are in real tables
across 32 tables.

**Only 90 columns (6%) resist classification**, and they are 59 distinct legacy abbreviations in the
Finance/airline module (`AIR_OR`, `AIR_COMT`, `ENT_21KM`, `INV_REP0`–`INV_REPO`).

**`float` is not being used as a numeric type here — it is being used as the default type**, and 232
columns already use `money`, so the correct type was available and known throughout.

### ⚠ AND AT 83% THE REMEDY INVERTS: ONE RULE AND NINETY EXCEPTIONS, NOT 544 JUDGEMENTS

**The number tripled and the work shrank by an order of magnitude**, which is the opposite of what a bigger
count usually means.

  *"544 real-table columns need review"*   → a per-column exercise, 544 decisions, weeks
  ***"83% of floats are wrong"***          → **a TYPE-MAPPING RULE with an exception list**

At 83% the default flips. **You no longer justify each conversion — you justify each NON-conversion**, so
the migration reviews **the 90 unclassified columns rather than the 1,238 classified ones.** `float` becomes
`decimal` unless something proves otherwise.

**One rule and ninety exceptions, not five hundred and forty-four judgements.**

### ⚠ How this number moved, because the progression is the finding

| pass | where 0.1 matters | what changed |
|---|---|---|
| 1 — obvious English words | 558 | — |
| 2 — after reading the top of the residual | 886 | `ENT_CR`, `ENT_DR`, `ENT_AMT` are credit, debit, amount |
| 3 — after reading **all 400** distinct residual names | **1,238** | `_VAL`, `BAS`, `GOZE`, `M1_C`…`M12_D`, `0_DAY`…`120_DAY`, `STRBAL`, `CST_*` |

**Each pass of reading the discard pile moved the answer by more than any deliberate refinement**, and the
first two passes left it a floor. Reading all four hundred is what turned it into a count — **twenty
minutes, because 600 occurrences are only 400 distinct names.**

**A RESIDUAL IS DEFINED BY THE INSTRUMENT, NOT BY THE WORLD.** *"The things that are not money"* and *"the
things a regex could not see as money"* are different sets, and the gap between them here was **680
columns — 46% of every float in the schema.**


### What this does not need

**No production copy.** This is a property of the schema, so the count is available now — and the decision
(convert versus preserve) can be taken before any data moves. What the production copy would add is the
*magnitude* of the drift already stored, which is a different question and only worth asking if the answer
is "convert".

---

## Two properties of the source system that are not migration questions

**These are facts about the running system rather than about moving it. Both were found as by-products of
analyses aimed at something else, and neither will be looked for under the heading it was found in.**

### 1. ⚠ An injection interface in the invoice-payment procedures (found T-220)

`[GeneralStores].[GetInvoicePayment]` and `[Pharmacy].[GetInvoicePayment]` take

```
@where nvarchar(4000),
@inv   nvarchar(4000)
```

and splice both into the statement unquoted before executing it. **A parameter named `@where`, four
thousand characters wide, concatenated into SQL is not an injection RISK — it is an injection INTERFACE**,
and it is a documented part of the procedure's contract.

`[HotelServices].[RepWorkListOrder]` also concatenates, but converts typed `int` and `datetime` parameters
first, **so its exposure is far narrower — that distinction is the severity axis.**

**Whether these are reachable from an authenticated user's input is not knowable from the schema**, and
that is the question to answer before deciding urgency. **What is knowable: these become queries in the new
system, and the interface must not be carried across with them.**

**Found by hand-reading five procedures that a mechanical triage had flagged as unsafe to classify.** A
classifier asking *"does it write"* cannot notice an injection surface, because nobody told it to weigh one.

#### The full picture across all 42 dynamic-SQL procedures (T-224, 2026-08-30)

**17 of the 42 splice an unconverted string parameter into the statement.** They fall into two tiers, and
the tier is decidable: **a parameter spliced INSIDE quotes is a value; one spliced BARE is structure.**

**TIER 1 — structure spliced bare, 7 procedures.** The caller supplies a table, a database, a column list,
a `WHERE` clause, or a procedure name. **This is not injection through a value; it is an interface for
arbitrary SQL.**

| procedure | what the caller controls |
|---|---|
| `[dbo].[SPSearch]`, `[Finance].[SPSearch]` | `@Table`, `@Fields`, `@Conditions`, and `@Description` — **executed as a procedure name via `exec('exec ' + @Description + …)`.** A generic SQL execution engine exposed as a stored procedure. |
| `[Finance].[SPTransfearData]` | `@ToDataBAse`, spliced into **sixteen `DELETE FROM` statements** — deletes Finance tables in a caller-named database |
| `[GeneralStores].[GetInvoicePayment]`, `[Pharmacy].[GetInvoicePayment]` | `@where`, `@inv` — a whole `WHERE` clause |
| `[dbo].[DynamicPivotTableInSql]` | `@ColumnToPivot`, `@ListToPivot` |
| `[Finance].[usp_ENTRY_TP_Header_DetailInsert]` | `@jrn_typ` |

**TIER 2 — a value inside quotes, 10 procedures**, and they are **one recurring pattern rather than ten
findings**: `USR_C = '''+ @USR_C +'''` in the Finance reporting procedures (`SPACCO*`, `SPF1Get*`,
`xACCO3128`). Breaking out requires a quote in the value — the classic case, and the narrow one.

⚠ **HOW THE TIER-1 COUNT MOVED, BECAUSE IT MOVED FOR THE USUAL REASON.** A first structural regex returned
**4**. Hand-reading `SPSearch` — which that regex had placed in tier 2 — showed it splicing `@Table` and
`@Conditions` bare, **because the regex recognised `' + @x + '` and not a splice at the end of a string.**
Reclassifying per occurrence returned **7**. The first pass under-counted the worst tier by 43%, and it was
found by a hand-read contradicting the machine rather than by improving the machine.

**The parameters listed above are indicative rather than complete** — the same head-parsing limit that hid
`@Table` will have hidden others.

### 2. ⚠ 158 identifiers stored in `float` — a correctness question, not a fidelity one (found T-222)

`TKT_NR`, `INVNO`, `LIN_NO`, `EntryCodes`, `CHK_SER`, `REQ_NO`, and the twelve month names are `float`
columns. **51 of them are in real tables, across 32 tables.**

**Money in `float` drifts. An identifier in `float` is a different failure:** it can compare unequal to
itself after a round trip, sort wrongly, and format with exponent notation. **If any of these participates
in a join or a lookup, that is a live defect in the running system rather than a migration concern.**

⚠ **This is NOT an assertion that it bites today**, and half of it is now checked rather than presumed.

**FOREIGN KEYS: zero of the 51 participate in one.** Every declared referential relationship in the schema
was enumerated and none is keyed on a float identifier — **so the worst case, a float on both sides of an
enforced join, does not occur.**

**JOINS AND `WHERE` EQUALITIES ARE UNVERIFIED AND ARE NOT GOING TO BE VERIFIED**, which is a decision
rather than an omission. They live in the 1,347 procedure bodies and the 177 views, so they are greppable —
**but a count of ad-hoc joins on float identifiers is a risk surface, not a defect list.** Small integer
values in `float` compare fine, which is why this has run for years, **so the scan would produce a number
that neither proves a defect nor excludes one.**

**AND THE MIGRATION CONVERTS THESE COLUMNS REGARDLESS.** Float identifiers become proper types on the way
across, which removes the class entirely rather than measuring it. **A question whose answer cannot change
the remedy is not worth closing.**

**The complete honest position: no enforced join is keyed on a float identifier; ad-hoc equality in
procedure bodies is unverified; and the conversion removes the question either way.**

---

## The 177 views: 110 carry across, 63 encode rules (T-225, counted 2026-08-30)

The procedure triage asked whether logic re-expresses as queries. **Views are already queries**, so the
parallel question is the opposite one: **do they carry across as reporting artefacts, or do they encode
business rules that must become code?**

| | views | share |
|---|---|---|
| **plain projection / join** | **110** | 62% |
| `CASE` used only to coalesce across joins | 4 | 2% |
| **encodes a rule** — comparison, aggregate or arithmetic | **63** | **36%** |

**The 110 carry across.** They are `SELECT`s over tables with joins and no computation, and they become
views or queries against the new model with no decision to take.

**The 63 are the work**, and they are concentrated where you would expect: `[Billing].[V_PatientBill]`
resolves **discount precedence** (`CtService.Discount` falling back to `Ser.Discount`), **sponsor-based
pricing** (`SponserCategoryId > 0`), `IsDiscountByPercentage` semantics and customer-category resolution —
**pricing rules living in a view.** `V_Coverage Approvals Follow-up`, `V_Insurance_Claims` and
`V_PackagePayment` are the same shape.

**⚠ A rule in a view is worse than a rule in a procedure for the migration**, because a view is read by
things that do not know they are invoking logic. The procedure triage could at least say *"450 procedures
contain logic"*; **a view presents itself as data.**

### The coalescing distinction, and why it needed a hand-read

A first pass counted every `CASE` as computation and returned **70**. Reading two of them showed
`[InPatient].[V_AdmitData]` uses `CASE WHEN adReq.[Id] IS NOT NULL THEN adReq.[PatientID]` — **null
coalescing across an outer join, which is structure rather than a rule** — while `V_PatientBill` in the same
bucket encodes pricing. **Separating `CASE`s that test only `IS NOT NULL` from those containing a comparison,
a literal or arithmetic moved the count to 63 and isolated 4 as structural.**

### ⚠ Two instrument defects were fixed before these numbers were believed

1. **View bodies ran to the next `CREATE` of any kind.** A view followed by the 1,988 `ALTER TABLE … ADD
   CONSTRAINT` statements swallowed all of them — `[Registration].[VUPatientInsuranceR]` measured **865,132
   characters**. Terminating at `GO` gives a maximum of 18,560 and a median of 1,144.
2. **The arithmetic test matched `SELECT * FROM`.** `[\w\)]\s*[*]\s*[\w\(]` matches `T * F`, so every
   `SELECT *` counted as a computation.

**Both were found by disbelieving an outlier rather than by review**, and the corrected numbers are close to
the uncorrected ones — 110/67 against 107/70 — **which is the uncomfortable part: two real defects that
roughly cancelled would have left a plausible answer standing.**

---

## The type rule, completed: what the exceptions actually are (T-226, 2026-08-30)

The float finding gave a rule and an exception list. **The exception list was not yet a list.** Resolving
it collapses the exercise further, for a reason that was not obvious until the names were read.

### ⚠ The money-versus-measurement distinction does not change the mapping

**`money` and `measurement` both become `decimal`.** So the entire 1,238-column classification — the one
that took three passes and moved from 558 to 1,238 — **does not affect a single mapping decision.** It was
worth doing to establish that `float` is the default type rather than a considered choice, **but the only
distinction that changes what a column becomes is IDENTIFIER versus NUMERIC.**

### The rule, and its three real exception classes

| population | becomes | count |
|---|---|---|
| **default — every float column** | **`decimal`** | 1,486 |
| **identifiers, codes, dates, print positions** | **`int` / `varchar` / `date`** | **158** (51 in real tables) |
| named nothing useful — needs a domain answer | **unresolved** | **5 names** |

**The 158 are the exception that matters**, and they do not become `decimal` — a ticket number, an invoice
number, a line number, a print position and the twelve month names are not quantities. **They were counted
in the float total and must be routed out of the default rule, not converted with it.**

### The 90 unclassified, resolved

59 distinct names, and reading them dissolves most of the uncertainty **because the target type is the same
either way**:

- **12 names → `decimal`, money-shaped on reading**: `AIR_OR`, `AIR_ORT`, `AIR_COMT`, `AIR_C`, `AIR_P`,
  `AIR_RF` (airline fare, commission and refund components), `PARKING_TCK` and `ROAD_TCK` (toll and parking
  charges), `RC_I` / `RC_R` (the issue/refund suffix pair used across the module), `INV_PP`, `Deblor`.
- **5 names → `decimal`, measurements**: `ENT_21KM` (kilometres), `INV_NTS` (nights), `Min`, `TimeBound`,
  `PackageChange`.
- **11 names → `int`/`varchar`, identifiers**: `JRN_N`, `Chapter No#`, `SYS_GRP`, `CAL_DATA`, `REC_FM`,
  `REC_TO`, `CARR`, `Chk`, `BCC`, `AF`, `CF`.
- **27 columns `INV_REP0`–`INV_REPO`** — invoice report slots, one occurrence each, all in Finance. A
  lettered/numbered series of generic slots; **they take the default and nothing is lost either way.**
- **5 names genuinely resist**: `MISSING`, `Cleaving`, `Rael`, `Others`, `Modified`. **These need someone
  who knows the Finance module, and they are the whole residue of a 1,486-column analysis.**

**So "one rule and ninety exceptions" is really ONE RULE, ONE ROUTED-OUT CLASS OF 158, AND FIVE NAMES
NOBODY CAN RESOLVE FROM THE SCHEMA.** That is ratifiable in a sitting.

---

# THE PRODUCTION-COPY CHECKLIST (T-227, assembled 2026-08-30)

**This is the page to open on the day the copy lands.** Every question below was raised somewhere else in
this document and could not be answered from the schema. **They were five sentences in five sections; this
is the single list.**

**Run them in this order — the cheap refusals first, so an hour is not spent on a copy that fails the first
check.**

| # | check | answers | cost | if it fails |
|---|---|---|---|---|
| **1** | **`SELECT DISTINCT CompanyID` on every one of the 1,199 `CompanyID`-bearing tables** | is every row's company attribution real? There are 1,199 such columns and **three foreign keys among them**, so nothing has ever prevented an orphan value | minutes, scriptable | rows whose company attribution is a lie — **find before the transfer, not during** |
| **2** | **`sys.dm_exec_procedure_stats`, captured with the instance's uptime** | which of the 1,347 procedures are actually executed. **The schema cannot answer this** — "referenced by nothing" measures the corpus, because every application call is invisible to it | one query | nothing; it only ever ADDS a live set |
| **3** | **Row counts and value ranges on the 425 real-table money floats** | how much drift is already stored. **Only worth running if the decision is "convert"** | minutes | informs the conversion, does not block it |
| **4** | **Orphan check on the two `NOCHECK` foreign keys** — `FK_NurseMaster_Employee` and `Pharmacy.LocalPurchaseOrderHeader → Purchasing.PurchaseRequest` | whether orphans already exist. **The engine has never validated either** | two queries | a reference across the boundary is a promise about integrity; **an orphan means the promise cannot be made** |
| **5** | **Reachability of the 7 Tier-1 dynamic-SQL procedures** from application code | whether `SPSearch`, `SPTransfearData` and the rest are callable by an authenticated user. **Needs the application source, not the database** | depends on access | decides urgency, not remedy — they are rewritten regardless |

## ⚠ Two properties of these checks

**Every one of them PRINTS what it found rather than reporting a pass.** A check that says "ok" is
indistinguishable from a check whose query matched nothing — **the same reason the tenant-column count in
this document is printed with its (empty) list beside it.**

**And check 2 has a limit that must travel with it: `sys.dm_exec_procedure_stats` resets on restart and
evicts under cache pressure.** It yields a **LIVE set and never a DEAD one.** A procedure absent from it on
a box restarted yesterday is evidence of nothing, which is why the instance uptime is captured alongside.

## What is NOT on this list, and why

**The type-mapping decision (`float` → `decimal`) does not need the copy.** It is a property of the schema
and can be ratified now. **The 130-of-159 foreign-key dispositions do not need the copy either** — they
follow from three placement decisions. **Nothing on this list blocks planning; it all blocks EXECUTION**,
which is the distinction that decides what can proceed tonight and what waits.
