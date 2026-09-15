# B18 pass 16 — `DEP` opened: 4 of 52, and the sweep's own progress metric was counting mentions

**TASK gate: see foot. `AC-DEP` 0 → 4 cited.**

## ⚠⚠ THE FINDING IS NOT A CITATION — THE PROGRESS NUMBERS WERE LOOSE

**Every `AC-…` count this sweep has reported came from `grep -rhoE "AC-XXX-[0-9]{4}" tests/`.** ⚠ **That
counts a criterion NAMED IN A COMMENT exactly as it counts one CITED IN A `[Trait]`.**

**Recounted strictly — `[Trait("Criterion"|"Acceptance"|"Decision"|"AcceptanceCriteria", "AC-…")]` only:**

| package | STRICT | LOOSE | inflated by |
|---|---|---|---|
| `AC-PAY` | 22 | 22 | 0 |
| `AC-EMP` | 46 | 46 | 0 |
| `AC-SS` | 14 | 14 | 0 |
| `AC-GL` | 7 | 7 | 0 |
| `AC-CMP` | 11 | 11 | 0 |
| `AC-DEP` | **4** | 5 | 1 |
| ⚠ **`AC-ATT`** | **8** | 14 | **6** |
| ⚠⚠ **`AC-POS`** | **0** | 3 | **3** |

⚠⚠ **`AC-POS` reads as three cited under a text grep and is actually ZERO. `AC-ATT` is 8, not 14.**
**The five packages that have been swept are clean; the inflation is entirely in packages nobody has
swept, where every appearance is a passing mention.**

⚠ **This is the mention-versus-assertion defect — diagnosed twice today in someone else's censuses —
found in the sweep's OWN progress metric.** **And I created one instance of it in this very pass: my
comment on `Every_department_read_takes_a_scope` names `AC-DEP-0044` to distinguish it from `0045`, and
a text grep counts that as a citation.**

**The comment stays.** ⚠ **The fix is to COUNT STRICTLY, not to stop writing informative comments** — the
mention is doing real work for the next reader, and it is the instrument that was wrong.

**`AC-PAY` is 22, not the 21 I last reported** — pass 15 added five traits, not four, because
`AC-PAY-0021` gained its transport half as well.

## ⚠ THE GROUPING, DONE BEFORE SEARCHING (B18)

**52 criteria in 10 documented sections, regrouped by the MECHANISM that would pin them:**

| group | criteria | mechanism |
|---|---|---|
| **A — structural / architecture** | 0002, 0032, 0034, 0043, 0044, 0045, 0050, 0051, 0052 | reflection and composed-model assertions, no database |
| B — create and identity | 0001, 0003, 0004, 0005 | domain factories + normalized-code uniqueness in SQL |
| C — company isolation | 0006–0009 | `DepartmentScopeResolverTests` + cross-company SQL |
| D — hierarchy | 0010–0017 | re-parent and cycle tests |
| E — manager | 0018–0024 | assign/clear manager |
| F — lifecycle | 0025–0031 | deactivate / reactivate |
| G — employee membership | 0033, 0035–0039 | employee department-change surface |
| H — authorization | 0040–0042 | permission tests |
| I — reads | 0046, 0047 | read service and branch scope |
| J — concurrency and cutover | 0048, 0049 | rowversion; cutover chain |

**Group A searched. B–J grouped and NOT searched — the grouping is the durable artefact.**

## Cited: four, all from group A

- **`AC-DEP-0051`** — `Department_is_tenant_and_company_owned_but_never_branch_owned`. **Verbatim**, and
  ⚠ its positive `Contains` assertions are its own anti-vacuity control.
- **`AC-DEP-0052`** — `No_department_table_has_a_branch_column`. ⚠ **Verbatim INCLUDING ITS INSTRUMENT**:
  the criterion says *"from the composed EF model rather than from a migration file"* and the test reads
  `ComposedTenantModel().FindEntityType(...)`. **The sibling that asserts the CLASS is the half the
  criterion explicitly does not ask for.**
- **`AC-DEP-0045`** — **both clauses, two tests**: construction refused, and every read's FIRST parameter
  is the scope.
- **`AC-DEP-0032`** — ⚠⚠ **three tests in three files.**

## ⚠⚠ AND `AC-DEP-0032` IS WHY THE MECHANISM SEARCH EXISTS

**The criterion bans a physical delete by *API route, command, handler or repository method*.**

| clause | test | file |
|---|---|---|
| command, handler | `No_department_delete_command_or_handler_exists` | `DepartmentApplicationArchitectureTests` |
| ⚠ repository method | `The_department_repository_offers_no_delete` | **`DepartmentArchitectureTests`** |
| ⚠ API route | `The_hr_surface_exposes_no_delete_verb` | **`HrRouteInventoryTests`** |

⚠⚠ **I enumerated the two files named `Department*ArchitectureTests`, found only the command/handler
clause, and was about to record PARTLY PINNED.** **Searching the MECHANISM — any test banning a delete
verb or a delete method, tree-wide — found a THIRD department architecture file I had not listed and a
route inventory in a different suite.**

**Fully pinned, and the route test is a SUPERSET for the second time — it already carries `AC-EMP-0017`.**

*Same lesson as `AC-PAY-0002` and `AC-EMP-0035`: search the mechanism before recording unresolved.*
