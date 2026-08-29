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
---

## 5. What this plan does not cover

- **No transfer engine.** Ruled out for now; the ERP finishes first.
- **No reference data exists to inherit.** The 103 `INSERT`s are into **temp tables inside procedure
  bodies**. There is no chart of accounts, no service catalogue, no drug list — "empty" is literal.
- **The script does not stand up alone.** Procedures reference `JCIT_MembershipDB.dbo` (101 times —
  identity, users and roles live in a database **not in this script**), `JCIT_DB_Finance.dbo` (21), and
  hardcoded `TEBAS_GATS_ONCE.Finance.main_acc` / `GATS_AMRI.Finance.main_acc` — **other installations'
  databases left in production code**.
- **Type hazards need a per-column decision, not a per-type rule.** `float` appears 1490 times beside
  `money` 232 and `decimal` 530. **Float cannot represent 0.1**, and this system carries both currency
  and drug dosages. 38 date-named columns are `varchar`; `CreatedBy` is `nvarchar(max)` on 137 tables.
- **Unverified here:** the cross-database reference counts, the 39 disabled constraints, and the
  duplicate `FK_Doctors_SpecialityGroupDetails1/_2` both keyed on `[SpecialityID2]`. Reproduce before
  relying on them.
