---
package: FP-011
title: General Ledger — Proposed Requirements
status: DRAFT — every line below is OWNER-DECISION-REQUIRED
version: 0.1
date: 2026-08-23
---

# FP-011 — Proposed Requirements

> ## THE CATALOG GAP
>
> **`docs/00-Master-Product-Specification/Requirement-Catalog/` has no `GL.md`.** The domain is declared
> (`Requirement-Catalog/README.md` lists `REQ-GL | General Ledger`) and the identifier space is reserved
> (`Requirement-Numbering.md` reserves `REQ-GL-0001`, `FR-GL-0001`, `SCR-GL-0001`, `API-GL-0001`,
> `TBL-GL-JournalEntry`, `RPT-GL-0001`, `WF-GL-0001`, `TC-GL-0001`), but the requirement lines were never
> written.
>
> **Every line in this document is a PROPOSAL and is marked `OWNER-DECISION-REQUIRED`.** None of them is a
> requirement yet. See `OD-GL-0001` for the four ways the owner can close this, including the option where
> none of these lines survives.
>
> They are drafted rather than omitted because a GL package that reports "there are no requirements" and
> stops hands the owner the same blank page with none of the analysis attached. They are marked rather than
> merged because **a feature package does not get to author the product's requirement catalog by filing a
> pull request.**

## Where the numbering starts, and why it is not obvious

`Traceability-Matrix.md` contains, under the heading **`# Example Mapping`**:

| Requirement | BR | Feature | Screen | API | Table | Permission | Test |
|-------------|----|---------|--------|-----|-------|------------|------|
| REQ-GL-0001 | BR-GL-0001 | FR-GL-0001 | SCR-GL-0001 | API-GL-0001 | TBL-GL-JournalEntry | PER-GL-PostJournal | TC-GL-0001 |

It is an **example**, and it is reported as one — it carries no authority and this package does not treat it
as a live traceability row. But it is the only existing signal about what `REQ-GL-0001` *is*, and it maps that
identifier to `BR-GL-0001` (the balance rule), `TBL-GL-JournalEntry` and `PER-GL-PostJournal`.

**So the drafts below start with journal posting rather than with the chart of accounts**, even though the
chart is the natural first thing to build. Matching the only existing signal costs nothing; contradicting it
would create a second answer to "what is `REQ-GL-0001`" for no benefit.

**If the owner intends the catalog to open with the chart of accounts instead, every identifier below shifts**
— which is precisely the kind of renumbering that `Requirement-Catalog/README.md` forbids after the fact
(*"Requirement IDs are immutable and shall never be reused"*). That is another reason `OD-GL-0001` is worth
answering before anything is ratified rather than after.

## Which shape these should take

`Requirement-Catalog/README.md` mandates a **Requirement Template**: ID, Title, Description, Priority,
Category, Business Rule Reference, Dependencies, Acceptance Criteria. The existing `HR.md` uses none of it —
it is a flat list of identifiers and titles, sixteen entries long.

The drafts below carry the **full template**, because the template is the stated rule and the precedent is
the deviation. If the owner rules that `GL.md` should match `HR.md` instead, these collapse to two columns
without loss — but the reverse is not true, so drafting the richer shape is the recoverable choice.

---

# Journal Entry

## `REQ-GL-0001` — Record a balanced journal entry `OWNER-DECISION-REQUIRED`

| | |
|---|---|
| **Description** | A user with the posting permission records a journal entry consisting of two or more lines, each naming an account and a debit or credit amount, whose debit total equals its credit total |
| **Priority** | Must |
| **Category** | Functional — Core |
| **Business Rule** | `BR-GL-0001` (must balance), `BR-GL-0004` (inactive accounts reject transactions) |
| **Dependencies** | `REQ-GL-0005` (accounts must exist), `REQ-GL-0009` (an open period must exist) |
| **Acceptance** | `AC-GL-0001`, `AC-GL-0002`, `AC-GL-0003` |
| **Open** | `OD-GL-0002` (currency), `OD-GL-0005` (branch dimension), `OD-GL-0007` (drafts) |

## `REQ-GL-0002` — Refuse an unbalanced journal entry `OWNER-DECISION-REQUIRED`

| | |
|---|---|
| **Description** | A journal whose debit total differs from its credit total is refused with a named error, and no partial state is persisted |
| **Priority** | Must |
| **Category** | Functional — Core |
| **Business Rule** | `BR-GL-0001` |
| **Dependencies** | `REQ-GL-0001` |
| **Acceptance** | `AC-GL-0004` |
| **Open** | — |

## `REQ-GL-0003` — Refuse modification of a posted journal `OWNER-DECISION-REQUIRED`

| | |
|---|---|
| **Description** | Once posted, a journal entry and its lines cannot be edited or deleted by any path |
| **Priority** | Must |
| **Category** | Functional — Integrity |
| **Business Rule** | `BR-GL-0002` |
| **Dependencies** | `REQ-GL-0001` |
| **Acceptance** | `AC-GL-0005` |
| **Open** | `OD-GL-0007` — under option 2 this stops being structurally enforceable and becomes a convention |

## `REQ-GL-0004` — Reverse a posted journal `OWNER-DECISION-REQUIRED`

| | |
|---|---|
| **Description** | A posted journal is corrected by posting a reversing journal, never by editing the original |
| **Priority** | Must |
| **Category** | Functional — Core |
| **Business Rule** | `BR-GL-0002` (by implication — the rule forbids editing but does not name the alternative) |
| **Dependencies** | `REQ-GL-0001`, `REQ-GL-0003` |
| **Acceptance** | `AC-GL-0006` |
| **Open** | `OD-GL-0006` — whether the reversal links back to the original |

---

# Chart of Accounts

## `REQ-GL-0005` — Create an account `OWNER-DECISION-REQUIRED`

| | |
|---|---|
| **Description** | An authorized user creates an account with a code and a name, unique within its owning scope |
| **Priority** | Must |
| **Category** | Functional — Master Data |
| **Business Rule** | Glossary: *"A General Ledger account used to classify financial transactions"* |
| **Dependencies** | — |
| **Acceptance** | `AC-GL-0007` |
| **Open** | `OD-GL-0003` — tenant-owned or company-owned decides the uniqueness scope AND whether this is a company-scoped write |

## `REQ-GL-0006` — Update an account `OWNER-DECISION-REQUIRED`

| | |
|---|---|
| **Description** | An account's name and descriptive attributes may be changed; its code may not be changed once transactions reference it |
| **Priority** | Must |
| **Category** | Functional — Master Data |
| **Business Rule** | — |
| **Dependencies** | `REQ-GL-0005` |
| **Acceptance** | `AC-GL-0008` |
| **Open** | Whether the code is immutable from creation or only once used — not settled by any existing rule |

## `REQ-GL-0007` — Deactivate an account `OWNER-DECISION-REQUIRED`

| | |
|---|---|
| **Description** | An account may be marked inactive. An inactive account rejects new transactions but retains its history |
| **Priority** | Must |
| **Category** | Functional — Master Data |
| **Business Rule** | `BR-GL-0004` |
| **Dependencies** | `REQ-GL-0005` |
| **Acceptance** | `AC-GL-0009` |
| **Open** | — |

## `REQ-GL-0008` — View the chart of accounts `OWNER-DECISION-REQUIRED`

| | |
|---|---|
| **Description** | An authorized user lists and searches accounts within the scope they are authorized for |
| **Priority** | Must |
| **Category** | Functional — Read |
| **Business Rule** | `BR-RPT-0002` (reports respect tenant and company boundaries) |
| **Dependencies** | `REQ-GL-0005` |
| **Acceptance** | `AC-GL-0010` |
| **Open** | `OD-GL-0003` |

---

# Fiscal Calendar

## `REQ-GL-0009` — Define a fiscal year and its periods `OWNER-DECISION-REQUIRED`

| | |
|---|---|
| **Description** | An authorized user defines a fiscal year composed of contiguous, non-overlapping fiscal periods |
| **Priority** | Must |
| **Category** | Functional — Configuration |
| **Business Rule** | Glossary: Fiscal Period, Fiscal Year |
| **Dependencies** | — |
| **Acceptance** | `AC-GL-0011` |
| **Open** | `OD-GL-0004` — tenant-owned or company-owned |

## `REQ-GL-0010` — Close a fiscal period `OWNER-DECISION-REQUIRED`

| | |
|---|---|
| **Description** | An authorized user closes a fiscal period. A closed period rejects new postings |
| **Priority** | Must |
| **Category** | Functional — Configuration |
| **Business Rule** | `BR-GL-0003` |
| **Dependencies** | `REQ-GL-0009` |
| **Acceptance** | `AC-GL-0012` |
| **Open** | `OD-GL-0004`; whether reopening is permitted at all is unstated by any rule |

## `REQ-GL-0011` — Assign a unique journal number within the fiscal year `OWNER-DECISION-REQUIRED`

| | |
|---|---|
| **Description** | Every posted journal receives a number unique within its fiscal year |
| **Priority** | Must |
| **Category** | Functional — Integrity |
| **Business Rule** | `BR-GL-0005` |
| **Dependencies** | `REQ-GL-0001`, `REQ-GL-0009` |
| **Acceptance** | `AC-GL-0013` |
| **Open** | `OD-GL-0004` — the uniqueness scope is *(FiscalYear, JournalNumber)* or *(CompanyId, FiscalYear, JournalNumber)* depending on the answer. **Gapless versus merely unique is also unstated**, and they are very different obligations |

---

# Read and Reporting

## `REQ-GL-0012` — Search and view journal entries `OWNER-DECISION-REQUIRED`

| | |
|---|---|
| **Description** | An authorized user searches posted journals by date range, account, number and reference, seeing only what their scope authorizes |
| **Priority** | Must |
| **Category** | Functional — Read |
| **Business Rule** | `BR-RPT-0001`, `BR-RPT-0002` |
| **Dependencies** | `REQ-GL-0001` |
| **Acceptance** | `AC-GL-0014` |
| **Open** | `OD-GL-0005` |

## `REQ-GL-0013` — Account balance enquiry `OWNER-DECISION-REQUIRED`

| | |
|---|---|
| **Description** | An authorized user views an account's movements and balance for a period |
| **Priority** | Must |
| **Category** | Functional — Read |
| **Business Rule** | `BR-RPT-0001`, `BR-RPT-0002` |
| **Dependencies** | `REQ-GL-0001`, `REQ-GL-0005` |
| **Acceptance** | `AC-GL-0015` |
| **Open** | `OD-GL-0008` — whether an opening balance exists to add |

## `REQ-GL-0014` — Trial balance `OWNER-DECISION-REQUIRED`

| | |
|---|---|
| **Description** | An authorized user produces a trial balance for a period, whose debit and credit totals are equal |
| **Priority** | Must |
| **Category** | Reporting |
| **Business Rule** | `BR-GL-0001`, `BR-RPT-0002` |
| **Dependencies** | `REQ-GL-0013` |
| **Acceptance** | `AC-GL-0016` |
| **Open** | `OD-GL-0002` — a multi-currency ledger has a trial balance per currency and one in base |

---

# What is NOT proposed, and why

**No year-end close requirement.** `OD-GL-0008` asks whether it is in V1. Drafting a requirement for it would
answer the question by writing it down, which is the failure mode this whole document is trying to avoid.

**No requirement for posting from another module.** `OD-GL-0009`. If the answer is "manual entry only", GL
has no inbound integration surface in V1 and none should be specified.

**No import or export requirement.** HR has one because `REQ-HR-0005` and the FP-009 split created it. There
is no equivalent GL requirement anywhere, and inventing one would be scope this package has no authority for.

**No budgeting, no cost centres, no analytical dimensions beyond the possible branch.** `Business-Rules.md`
lists Budgeting among *Future Modules*, and nothing places cost centres in V1.
