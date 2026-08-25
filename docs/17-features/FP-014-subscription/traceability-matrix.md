# FP-014 — Traceability matrix

Requirement → business rule → acceptance criterion → test scenario → governing decision.

**Every count in this document was derived by running `scripts/trace-check.py` over the package's own
files, not typed from memory.** FP-012 stated its entity count wrong four times — once inside the
section warning about miscounts — which is why counting is mechanical here.

**Run it with every other package present.** Isolating FP-014 in a scratch directory manufactures
warnings that are not real: `DEC-ATT-0008` lives in FP-013 and the `BR-PAY` rules quoted in
[`business-rules.md`](business-rules.md) live in FP-012, so a checker that cannot see those packages
reports resolvable citations as unresolved. That is a fault in the invocation, not in this package.

---

## Inventory, derived

| Space | Count | Numbering | Orphans |
|---|---|---|---|
| `REQ-SUB` | **28** | 0001 to 0028, contiguous | none |
| `BR-SUB` | **21** | 0001 to 0021, contiguous | none |
| `AC-SUB` | **51** | 0001 to 0051, contiguous | none |
| `TS-SUB` | **50** | 0001 to 0050, contiguous | none |
| `DEC-SUB` | **12** | 0001 to 0012, contiguous | none |
| `OD-SUB` | **17** | 0001 to 0017, contiguous | none |

"Orphan" means an identifier cited somewhere in the package that is not defined in its home file.

**`OD-SUB` reports 17 defined and 17 unruled, and that is correct.** The rulings exist — they are in
the architect's note of 2026-08-25 and this whole slice is written from them — but
`decisions-ratified.md` is not this task's to write. Ratification is a separate act with the owner,
so `BUILD BLOCKED` is the honest state of the package until it happens.

---

## The chain

| Requirement | Rules | Criteria | Tests | Decisions |
|---|---|---|---|---|
| `REQ-SUB-0001` one subscription in force, append-only | `BR-SUB-0001`, `BR-SUB-0002`, `BR-SUB-0003` | `AC-SUB-0001`, `AC-SUB-0002`, `AC-SUB-0003`, `AC-SUB-0004`, `AC-SUB-0044` | `TS-SUB-0002`, `TS-SUB-0003`, `TS-SUB-0033`, `TS-SUB-0034`, `TS-SUB-0035` | `OD-SUB-0008` |
| `REQ-SUB-0002` a plan is a reusable definition | — | `AC-SUB-0005` | `TS-SUB-0010`, `TS-SUB-0017` | `OD-SUB-0015` |
| `REQ-SUB-0003` plans are Platform-global | — | `AC-SUB-0006` | `TS-SUB-0040` | `DEC-SUB-0003` |
| `REQ-SUB-0004` no tenant-plane amendment | `BR-SUB-0004` | `AC-SUB-0007`, `AC-SUB-0008`, `AC-SUB-0045` | `TS-SUB-0023`, `TS-SUB-0042`, `TS-SUB-0043` | `OD-SUB-0013` |
| `REQ-SUB-0005` survives a tenant-database outage | `BR-SUB-0008` | `AC-SUB-0009` | `TS-SUB-0036` | `DEC-SUB-0004` |
| `REQ-SUB-0006` attributable and dated | — | `AC-SUB-0010` | `TS-SUB-0032` | `DEC-SUB-0007` |
| `REQ-SUB-0007` answers enabled per module | `BR-SUB-0015` | `AC-SUB-0011`, `AC-SUB-0012`, `AC-SUB-0046` | `TS-SUB-0004`, `TS-SUB-0007`, `TS-SUB-0038` | `OD-SUB-0005` |
| `REQ-SUB-0008` resolved server-side, never in the token | `BR-SUB-0011` | `AC-SUB-0013` | `TS-SUB-0026` | `DEC-SUB-0005` |
| `REQ-SUB-0009` takes effect without re-issuing a token | `BR-SUB-0012` | `AC-SUB-0014`, `AC-SUB-0015` | `TS-SUB-0027`, `TS-SUB-0028` | `OD-SUB-0004` |
| `REQ-SUB-0010` additive grants only | `BR-SUB-0005` | `AC-SUB-0016`, `AC-SUB-0017` | `TS-SUB-0005`, `TS-SUB-0006` | `OD-SUB-0011` |
| `REQ-SUB-0011` unentitled route refused | `BR-SUB-0007` | `AC-SUB-0018` | `TS-SUB-0018` | `OD-SUB-0006` |
| `REQ-SUB-0011` release condition — gate held back from the migration's release | — | `AC-SUB-0047` | — | `OD-SUB-0003` |
| `REQ-SUB-0012` one mechanism, every module | `BR-SUB-0007` | `AC-SUB-0019`, `AC-SUB-0020` | `TS-SUB-0019`, `TS-SUB-0044` | `DEC-SUB-0006` |
| `REQ-SUB-0013` platform plane never gated | `BR-SUB-0008` | `AC-SUB-0021` | `TS-SUB-0020` | `DEC-SUB-0010` |
| `REQ-SUB-0014` the enabled-module set | `BR-SUB-0019` | `AC-SUB-0022`, `AC-SUB-0023` | `TS-SUB-0021`, `TS-SUB-0022` | `OD-SUB-0007` |
| `REQ-SUB-0015` permissions of a disabled module | `BR-SUB-0009` | `AC-SUB-0024`, `AC-SUB-0025` | `TS-SUB-0030`, `TS-SUB-0031` | `OD-SUB-0012` |
| `REQ-SUB-0016` data retained untouched | `BR-SUB-0010` | `AC-SUB-0026` | `TS-SUB-0037` | `OD-SUB-0012` |
| `REQ-SUB-0017` a dated term | `BR-SUB-0001` | `AC-SUB-0027`, `AC-SUB-0028` | `TS-SUB-0001`, `TS-SUB-0008` | `OD-SUB-0009` |
| `REQ-SUB-0018` expiry blocks login | `BR-SUB-0013` | `AC-SUB-0029`, `AC-SUB-0032` | `TS-SUB-0009`, `TS-SUB-0029` | `OD-SUB-0009` |
| `REQ-SUB-0019` orthogonal to `TenantStatus` | `BR-SUB-0014` | `AC-SUB-0030`, `AC-SUB-0031` | `TS-SUB-0041`, `TS-SUB-0045` | `OD-SUB-0010` |
| `REQ-SUB-0020` no trial concept | — | `AC-SUB-0033` | `TS-SUB-0046` | `OD-SUB-0014` |
| `REQ-SUB-0021` tenant reads modules, not terms | `BR-SUB-0019` | `AC-SUB-0022`, `AC-SUB-0035` | `TS-SUB-0021`, `TS-SUB-0024` | `DEC-SUB-0002` |
| `REQ-SUB-0022` platform reads across tenants | — | `AC-SUB-0034` | `TS-SUB-0025` | `DEC-SUB-0010` |
| `REQ-SUB-0023` price per currency | — | `AC-SUB-0036` | `TS-SUB-0015` | `OD-SUB-0015` |
| `REQ-SUB-0024` money is `decimal(19,4)` | — | `AC-SUB-0037` | `TS-SUB-0039` | `DEC-SUB-0008` |
| `REQ-SUB-0025` the invoice | `BR-SUB-0017`, `BR-SUB-0018`, `BR-SUB-0020` | `AC-SUB-0038`, `AC-SUB-0039`, `AC-SUB-0048` | `TS-SUB-0011`, `TS-SUB-0012`, `TS-SUB-0047` | `OD-SUB-0016` |
| `REQ-SUB-0026` payment capture — **owned by `T-010`**, see below | `BR-SUB-0020` | — | — | `OD-SUB-0016` |
| `REQ-SUB-0027` metering and caps | `BR-SUB-0006` | `AC-SUB-0040`, `AC-SUB-0041`, `AC-SUB-0043` | `TS-SUB-0013`, `TS-SUB-0014`, `TS-SUB-0016` | `OD-SUB-0017` |
| `REQ-SUB-0027` seat admission — cap enforced at the grant, never at login | `BR-SUB-0021` | `AC-SUB-0049`, `AC-SUB-0050`, `AC-SUB-0051` | `TS-SUB-0048`, `TS-SUB-0049`, `TS-SUB-0050` | `OD-SUB-0017` |
| `REQ-SUB-0028` proration | `BR-SUB-0016` | `AC-SUB-0042` | `TS-SUB-0012` | `OD-SUB-0015` |

Twenty-eight requirements over thirty rows. `REQ-SUB-0011` carries a second row because one of its
criteria is a declared gap and the other is not — putting both on one row would let the covered half
vouch for the uncovered one. `REQ-SUB-0027` carries a second row because metering and seat admission
are different enforcement surfaces answering to the same requirement, and separating them keeps the
`DEC-L-009` ruling legible against the metering rows it deliberately does **not** behave like.

### The three build obligations, and where they now live

Each was found by reading code during T-006 or T-007. None is visible from inside this package's
documents alone, and each is the kind of finding that survives as an acceptance criterion and
evaporates as prose.

| Obligation | Criterion | Scenario | Found in |
|---|---|---|---|
| `PlatformDbContext` has no `PreventAppendOnlyMutation`; `OD-SUB-0008` rests on a guard that exists only on `TenantDbContext` | `AC-SUB-0044` | `TS-SUB-0033` | T-006, reading `TenantDbContext.cs:484` against `PlatformDbContext.cs:107` |
| The `Platform.` prefix does not distinguish the two authorization planes, so the no-tenant-plane-amendment requirement needs a guard rather than careful reading | `AC-SUB-0045` | `TS-SUB-0043` | T-007, writing the permission set |
| No backfill and no default plan, and the gate held out of the migration's release | `AC-SUB-0046`, `AC-SUB-0047` | `TS-SUB-0038` | T-006, reading the migration path |

**`TS-SUB-0033` fails today**, and that is the point: it is the executable form of the first
obligation, and it stays red until `PlatformDbContext` gains the guard. No FP-014 entity may carry
`IAppendOnlyEntity` before then — the interface without the guard is the appearance of immutability
with none of it.

---

## The two declared gaps, and why each is a gap rather than an omission

**A third gap was declared in this document's first draft and has since been closed.** The
consequence of exceeding a seat cap was unruled when the matrix was first written;
**`DEC-L-009`** ruled it on 2026-08-25 — enforced at admission, never at login — and it now has three
criteria and three scenarios on `REQ-SUB-0027`'s second row. It is named here rather than quietly
removed, so a reader comparing drafts sees a gap closed rather than a gap dropped.

### `REQ-SUB-0026` — payment capture belongs to `T-010`

**Chain: `REQ-SUB-0026 → BR-SUB-0020 → T-010`.** No criterion, no scenario.

`OD-SUB-0016` ruled that the product issues invoices **and captures payment itself**. That puts
cardholder data in PCI-DSS scope, and the standard containment answer — isolating the
cardholder-data environment — sits in tension with `ADR-001` Modular Monolith. **That is an
architectural decision and `T-010` owns it.**

What this package asserts is the **boundary**: `BR-SUB-0020` and `AC-SUB-0048` require that no
request body, no response body and no log statement here carries a primary account number, card
verification value, cardholder name or expiry date, and `TS-SUB-0047` tests it by reflection over the
contract types.

**An acceptance criterion invented for `REQ-SUB-0026` would claim coverage this package does not
have**, and would read to a future implementer as though capture had been specified. The row says
`T-010` owns it, which is worth more.

### `AC-SUB-0047` — a release condition no suite can observe

The enablement gate must not be active in the release that introduces the migration, because
`AC-SUB-0046` requires the migration to seed nothing and every existing tenant is therefore
unentitled the moment it runs.

**No automated suite can assert a release boundary.** A test asserting "the gate is off" passes
trivially before the gate exists and fails permanently once it is switched on — it would assert the
opposite of the intent. The criterion is real and is verified by whoever schedules the release; the
tests cell is a declared gap rather than a scenario pretending to cover it.

---

## Coverage

- **Every one of the 28 requirements appears in the chain.** Twenty-seven carry criteria; one —
  `REQ-SUB-0026` — is a declared gap owned by `T-010`.
- **Every one of the 51 criteria appears in the chain.** Fifty reach a test scenario; one —
  `AC-SUB-0047` — is a declared gap because no suite can observe a release boundary.
  `trace-check` reports it as *"1 criterion carried by prose with no owning requirement"*. **It does
  have an owning requirement** — `REQ-SUB-0011`, second row — and the wording is the script
  describing the section it found the criterion discussed in. The substance is right and is the
  substance that matters: `AC-SUB-0047` is the one criterion no scenario covers.
- **Every one of the 50 scenarios is reached from a criterion.** None is orphaned, and none exists
  only to make a count look complete.
- **No en-dash ranges.** Every identifier in every cell is written out. An abbreviated span reads as
  several identifiers to a human and as one to a string search, and ten identifiers hid behind such
  spans in FP-012 — which is why `trace-check`'s RANGE rule treats one in a matrix as a failure
  rather than a warning. **This document's first draft failed that rule**, by writing the FP-012
  example out literally in this very bullet; the check caught it, which is the argument for the rule.

## What the package still does not have

- **`decisions-ratified.md`.** The rulings are complete and live in the architect's note; ratifying
  them into the package is a separate act with the owner. `trace-check` reports `BUILD BLOCKED` on 17
  unruled decisions and that is the correct reading of the package as it stands.
- **`REQ-SUB` in `Requirement-Numbering.md`**, and **`BR-SUB` in the master `Business-Rules.md`.**
  Both are added at ratification, per `OD-SUB-0002` and FP-013's `REQ-ATT` precedent. Until then
  `trace-check` check 7 reports `BR-SUB` as `UNPROMOTED` alongside `BR-PAY` and `BR-ATT` — verified,
  and **that is the check working rather than a defect in this package.**

  **`DEC-L-012` sharpens what happens next**, and it changes the obligation rather than the current
  state: the seventy-percent orphan class is closed **forwards**, so a package promotes its `BR-`
  rules at ratification and **an `UNPROMOTED` row that survives ratification is a ratification
  defect, not an accepted cost.** This package's twenty-one rules are therefore promotion work that
  ratification must do, not a backlog item it may inherit. The existing `BR-PAY` and `BR-ATT` rows
  are paid per module when those modules are next touched — not here, and not in one sweep.
