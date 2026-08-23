---
package: FP-011
title: General Ledger — Traceability Matrix
status: APPROVED — anchored, now that Requirement-Catalog/GL.md exists (2026-08-23)
version: 1.0
date: 2026-08-23
---

# FP-011 — Traceability Matrix

> **COMPLETED 2026-08-23.** `OD-GL-0001` ratified the drafted lines into
> `Requirement-Catalog/GL.md`, so the REQ column now anchors to real catalog entries rather than to
> proposals. The "Blocked by" column below is kept as the record of what each row was waiting on and which
> ruling released it.
>
> What follows is the finding as it stood, preserved because the gap it describes is the reason this package
> exists in the form it does.
>
> **This matrix could not be completed, and the reason was the package's top-line finding.**
>
> Traceability in this product is REQ-anchored: `Requirement-Catalog/Traceability-Matrix.md` maps
> `Requirement → BR → Feature → Screen → API → Table → Permission → Test`. GL has **no requirement catalog
> entry to anchor to**. Every `REQ-GL-` identifier below is a *proposal* from [requirements.md](requirements.md),
> and until `OD-GL-0001` is answered every row here is provisional.
>
> It is published incomplete rather than withheld, because the shape of what is missing is itself the
> deliverable. FP-009 ratified the standard that applies: **an inventory with a footnote saying it is
> incomplete is not an inventory** — so the incompleteness is stated in the status line and in every affected
> column, not in a footnote.

---

# Proposed requirement → rule → criterion → scenario

| REQ (proposed) | Business rule | Acceptance | Scenarios | Blocked by |
|---|---|---|---|---|
| `REQ-GL-0001` Record a balanced journal | `BR-GL-0001`, `BR-GL-0004` | `AC-GL-0001`–`0003` | `TS-GL-0001`, `TS-GL-0004`, `TS-GL-0005` | `OD-GL-0002`, `OD-GL-0005`, `OD-GL-0007` |
| `REQ-GL-0002` Refuse unbalanced | `BR-GL-0001` | `AC-GL-0004` | `TS-GL-0002`, `TS-GL-0003` | — |
| `REQ-GL-0003` Posted journals immutable | `BR-GL-0002` | `AC-GL-0005` | `TS-GL-0006`–`TS-GL-0008` | **`OD-GL-0007`** |
| `REQ-GL-0004` Reverse a posted journal | `BR-GL-0002` (by implication) | `AC-GL-0006` | `TS-GL-0009` | `OD-GL-0006` |
| `REQ-GL-0005` Create an account | Glossary: Account | `AC-GL-0007` | — | **`OD-GL-0003`** |
| `REQ-GL-0006` Update an account | — | `AC-GL-0008` | `TS-GL-0033` | code immutability unstated |
| `REQ-GL-0007` Deactivate an account | `BR-GL-0004` | `AC-GL-0009` | `TS-GL-0012`, `TS-GL-0013` | — |
| `REQ-GL-0008` View the chart | `BR-RPT-0002` | `AC-GL-0010` | `TS-GL-0025` | `OD-GL-0003` |
| `REQ-GL-0009` Define year and periods | Glossary: Fiscal Year/Period | `AC-GL-0011` | — | **`OD-GL-0004`** |
| `REQ-GL-0010` Close a period | `BR-GL-0003` | `AC-GL-0012` | `TS-GL-0010`, `TS-GL-0011` | `OD-GL-0004`; reopening unstated |
| `REQ-GL-0011` Unique journal number | `BR-GL-0005` | `AC-GL-0013` | `TS-GL-0034` | `OD-GL-0004`; **gapless unstated** |
| `REQ-GL-0012` Search journals | `BR-RPT-0001/0002` | `AC-GL-0014` | `TS-GL-0021`, `TS-GL-0029` | `OD-GL-0005` |
| `REQ-GL-0013` Balance enquiry | `BR-RPT-0001/0002` | `AC-GL-0015` | — | `OD-GL-0008` |
| `REQ-GL-0014` Trial balance | `BR-GL-0001`, `BR-RPT-0002` | `AC-GL-0016` | `TS-GL-0031`, `TS-GL-0032` | `OD-GL-0002` |

# Business rules — coverage, and what each leaves open

| Rule | Covered by | Fully settled by existing authority? |
|---|---|---|
| `BR-GL-0001` balance | `REQ-GL-0001`, `REQ-GL-0002`, `REQ-GL-0014` | **No** — "balanced in which currency" is `OD-GL-0002` |
| `BR-GL-0002` posted journals immutable | `REQ-GL-0003`, `REQ-GL-0004` | **No** — names no correction mechanism (`OD-GL-0006`), and says *posted* (`OD-GL-0007`) |
| `BR-GL-0003` closed periods prohibit posting | `REQ-GL-0010` | **No** — whose calendar (`OD-GL-0004`); reopening unstated |
| `BR-GL-0004` inactive accounts | `REQ-GL-0007` | **Yes** |
| `BR-GL-0005` journal number unique in year | `REQ-GL-0011` | **No** — unique within whose year, and unique ≠ gapless |

**One of five is fully settled.** That ratio is the argument for raising nine owner decisions rather than
proceeding on inference.

# Cross-cutting obligations

| Obligation | Source | Criterion | Scenario |
|---|---|---|---|
| Adopt `decimal(19,4)` **by recorded decision** | `ADR-027` deferred obligations | `AC-GL-0003` | `TS-GL-0004` |
| `nvarchar` everywhere | standing constraint | `AC-GL-0019` | `TS-GL-0017`, `TS-GL-0019` |
| No cross-database FK | standing constraint | `AC-GL-0020` | `TS-GL-0018` |
| E3 manifest + ten-site inventory | `DEC-POS-0022` | `AC-GL-0021` | `TS-GL-0015`, `TS-GL-0016` |
| Cutover must not mutate append-only rows | new — no prior coverage | — | **`TS-GL-0014`** |
| Permission defined, not merely named | FP-006P | `AC-GL-0018` | `TS-GL-0024` |
| Administer grants no functional permission | `ADR-025` d8 | `AC-GL-0017` | `TS-GL-0022` |
| Scope unforgeable, empty set refuses | `ADR-025` d10, `ADR-023` d22 | `AC-GL-0014` | `TS-GL-0020`, `TS-GL-0023` |

# Identifiers reserved but not yet used

`Requirement-Numbering.md` reserves a full GL identifier space. This package proposes only `REQ-GL-*`; the
rest are listed so the next package knows they exist and does not invent parallel names.

| Reserved | Used here | Note |
|---|---|---|
| `REQ-GL-0001` | **Proposed** | Aligned to the `# Example Mapping` row's implied meaning — journal posting |
| `FR-GL-0001` | No | Feature identifiers not drafted |
| `SCR-GL-0001` | No | No screen design in scope |
| `API-GL-0001` | No | Routes sketched but not identifier-tagged |
| `TBL-GL-JournalEntry` | **Adopted as the naming pattern** | The one reservation this package follows directly |
| `RPT-GL-0001` | No | Trial balance would be the first |
| `WF-GL-0001` | No | Posting workflow depends on `OD-GL-0007` |
| `TC-GL-0001` | No | `TS-GL-*` used pending the catalog decision |

> **`TS-GL-*` versus `TC-GL-*`.** `Requirement-Numbering.md` reserves `TC-GL-0001` for test cases; the feature
> packages use `TS-` for scenarios. This package uses `TS-GL-*` to match its siblings and records the
> discrepancy rather than resolving it — reconciling the product's test-identifier namespace is not FP-011's
> to do, and doing it in passing is exactly the kind of drive-by change `ADR-027` decision 4 warns against.

# What is NOT traced, and why

| Gap | Reason |
|---|---|
| Every REQ column | `OD-GL-0001` — no `GL.md` exists, so nothing is anchored |
| Year-end close | `OD-GL-0008` — not proposed, so not traced |
| Inbound postings from other modules | `OD-GL-0009` — no contract proposed |
| Import/export | No GL requirement exists for it anywhere |
| Screens and reports beyond trial balance | Out of scope for a foundation package |


---

# AS-BUILT — what the build found, changed, or had to decide (2026-08-23)

Recorded here rather than in a new document, following FP-009's precedent: an as-built section that travels
with the traceability is read by the person checking whether the package still describes the code.

## Defects the build found

**Every GL write route was unusable, and only a transport test could have found it.** The request records
carried no `[property: JsonPropertyName(...)]`. `StrictRequestReader` deserializes with
`JsonSerializerOptions.Default`, which is **case-sensitive**, so `{"code":"4100"}` never bound to `Code`;
the reader returned `null` and every route correctly answered `400 request.invalid`. Creating an account,
defining a fiscal year, creating or updating a draft, and reversing a journal all refused valid bodies. The
routes, handlers, domain and error mapper were all correct — the wire contract simply never deserialized.
**No review would have caught it: the records read correctly and the fault is an absence.** HR does not have
the bug because its records carry the attributes; GL now follows the same convention.

**The exit gate would have skipped an entire test project.** `gate.sh` enumerates test projects by name and
did not list `SSAS.Finance.Tests`, which this package creates — so 46 GL domain tests would never have run
in the gate. This is FP-008's `H9` incident in a new shape: the 2026-08-21 ruling redefined the gate as *"the
full Integration suite plus every other test project in full"* precisely to prevent it, and the script still
did not implement that for projects added later. **A gate that names its projects silently omits every
project added after it was written.**

**A near-miss on the migration.** Running `dotnet ef migrations add` against `SSAS.Platform.Infrastructure` —
the shape `ADR-018` documents for `database update` — scaffolded a migration that **dropped all eleven HR
tables**, because Platform's design-time factory passes `modelContributors: null` while the snapshot holds
them. The correct mechanism, `tools/SSAS.Tenant.MigrationTool`, already existed and its own comment
predicted the failure verbatim. Recorded as a standing rule: **before any `dotnet ef` command, sweep for the
repo's own tooling first.**

## Divergences from the package, and why

| What the package said | What was built | Why |
|---|---|---|
| Create four GL projects | **Used the five that already existed** under `src/Modules/Finance/` | They were registered in the solution and `Program.cs` already called `AddGlModule()`. A parallel tree would have been two assemblies named `SSAS.GL.Domain` |
| — | **`SSAS.GL.Contracts` removed**, with its five references | `OD-GL-0009`: nothing posts to GL in V1. It existed empty and was already referenced by three source projects and two test projects, which is closer to the coupling the ruling excluded than an unreferenced stub. Returns when Payroll consumes it, shaped by its consumer |
| A `GL.Tests` project | **`tests/Finance.Tests`** | The canonical home already existed and was referenced. The prompt's name was a guess made before the repo was consulted |
| `data-model.md` left the amount shape open | **Two columns, debit and credit** | Accounting-native; makes `BR-GL-0001` a comparison of two sums rather than a test that a signed total is zero |
| `REQ-GL-0006` left code immutability open | **Immutable from creation** | "Immutable once used" requires the aggregate to know whether any line references it — a cross-aggregate query at write time, and the derived-state-that-drifts shape this codebase refuses |
| `authorization-model.md` questioned `GL.Reports.View` | **It exists** | `DEC-DOC-0015` declined an additive permission because export read exactly what search read — no boundary. A trial balance **aggregates**, so it reveals a total for accounts an individual enquiry surfaces one at a time. Same rule, different fact |

## Things added that no requirement asked for, and why

**`Account.NormalizedName`.** `DEC-POS-0030` records that a value-converted property translates in a
projection but not a predicate, and that HR shipped a department search that threw for every search term.
`REQ-GL-0008` searches the chart, so GL would have reproduced it exactly. Written up front rather than after
the same failure.

**Separation of duties between `GL.Drafts.Manage` and `GL.Journals.Post`.** Nobody ordered it; it became
expressible for free once `OD-GL-0007` made the draft a distinct aggregate, and a user who prepares work for
someone else to post is a real organisational need.

## Deliberate duplications, each with its reason

`ADR-027` decision 4 sanctions promotion into `SSAS.BuildingBlocks` but does not mandate it, and is explicit
that a promotion is a reviewed change to shared foundations rather than a side effect of a feature package
needing a type. Three small duplications were taken knowingly:

* **`GlPersistenceConstants`** — the schema name and collation name. Two string literals fixed by the
  database, unchanged since Sprint-00. **Trigger: a third module.**
* **`GlReadScope`'s company set** — not HR's `AuthorizedCompanyScope`. A list behind a guarded constructor is
  too little type to spend a foundations review on. **Trigger: a third consumer**, written where the type
  lives, because drift in a scope type is a security defect rather than an inconvenience.
* **`GlCompanyContextEndpointFilter`** — fifteen lines bound to GL's own error mapper and resource key. The
  genuinely shared part, `ICompanyContextEstablisher`, already lives in BuildingBlocks.

## Known limits, recorded rather than papered over

**Fiscal-year overlap has no database backstop.** Overlap is a range predicate across rows, not an equality
on a key, so no unique index expresses it. Two concurrent definitions of overlapping years could both pass.
The exposure is small — defining a fiscal year is rare and deliberate — and the alternative is a lock held
across a human-scale operation. **This is the only invariant in the module with no second line of defence.**

**`NextJournalNumberAsync` reads the year's numbers rather than using SQL `MAX`.** The numbers are stored as
text and a lexical maximum over `"9"` and `"10"` answers `"9"`. At realistic per-year volumes this is one
indexed range read; if it stops being cheap, the fix is a numeric column beside the text one, not a lexical
shortcut.

**Journal numbers are unique, not gapless.** `BR-GL-0005` asks for unique and V1 delivers unique.
`AC-GL-0013` deliberately does not assert the absence of gaps, and a ledger that ships unique-but-gapped
cannot be retrofitted for history that already exists — so this stays a live owner question.

**`InternalsVisibleTo` was granted twice.** `SSAS.GL.Application` needs `JournalEntry.Post`, and
`SSAS.Integration.Tests` needs it to seed a posted journal the way posting does. Making the factory public
would have been the easy fix and the wrong one: the boundary `OD-GL-0007` established is *nothing outside
the GL module fabricates a posted journal*, and public opens it to the API layer, the Host, and every future
module.

## The cutover guard fired, and it was right to

The exit gate's only failures were five assertions in `TenantCutoverCopySqlServerTests`, and every one was
the guard doing its job. Its own comment states the contract:

> AN EXACT LIST, DELIBERATELY. The derivation guarantees the engine cannot MISS a table; this guarantees a
> human SEES a new one, because a new tenant-owned entity may need ordering, identity or column decisions
> that "it compiles" does not settle.

Three exact entity-name lists and two counts had to acknowledge GL's seven entities by name. **The
interesting part is why the pre-gate sweep missed them.** FP-009's lesson was *derive the inventory, do not
trust your memory of it* — and the inventory was derived. But the sweep searched for one **shape** of
expectation, `Assert.Equal(13, ...)`, and the other four expectations were written differently: as literal
name arrays, as `composed.Value.Count - 11`, and as `Assert.Equal(7, retried.Value.TablesCopied)`. A
correct derivation applied to an incomplete search is still an incomplete answer.

Two of the fixes are worth recording for their content rather than their arithmetic:

* `composed.Value.Count - 11` became `- 18` rather than the literal `2` it evaluates to. Written against the
  composed count, the two halves cannot drift: a module that adds a table and forgets this test moves the
  left side and fails.
* The retry's `TablesCopied` moved 7 → 14 while `TablesAlreadyComplete` stayed at **6**. GL's tables are
  empty in that fixture, and an empty destination table is indistinguishable from one never copied — so the
  retry re-copies all seven. The asymmetry is the retry's safety claim working, not a regression.
