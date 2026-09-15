---
package: FP-011
title: General Ledger — Test Scenarios
status: APPROVED — scenarios settled by the 2026-08-23 rulings
version: 1.0
date: 2026-08-23
---

# FP-011 — Test Scenarios

> **DECISIONS CLOSED, 2026-08-23.** All nine owner decisions are ruled; conditional wording below is kept as
> the record of what was weighed, with the ruling stated where it changes the answer.
>
> | | | | |
> |---|---|---|---|
> | `0001` catalog: ratified into `GL.md` | `0002` **single currency** | `0003` **tenant-level chart** | `0004` **company calendar** |
> | `0005` **no branch dimension** | `0006` **reversal + `ReversesJournalId`** | `0007` **two aggregates** | `0008` **period close only** |
> | `0009` **manual entry only** | | | |

> Proposed scenarios, with the **layer each must run at** stated explicitly. That column is the point of this
> document: several of the guarantees GL depends on are enforced by code a handler test never executes, and a
> scenario placed at the wrong layer can pass while proving nothing.
>
> FP-009 produced the standing example — an API-level test asserted a rollback-and-still-record property that
> its own harness could not exercise, because `StubUnitOfWork.NoOpTransaction` makes the transaction a no-op.
> The property was real; the test was at the wrong layer. That correction is recorded in FP-009 and its lesson
> is applied here in advance.

| Layer | What it can prove |
|---|---|
| **Domain** | Aggregate invariants, in memory, fast |
| **Application** | Handler orchestration against stubs |
| **API** | Transport: strict reading, status codes, error names, size limits |
| **Integration (real SQL)** | Anything enforced by `TenantDbContext`, any constraint, any migration, anything about transactions |

---

## Journal posting

| ID | Scenario | Layer |
|---|---|---|
| `TS-GL-0001` | A balanced two-line journal posts and both lines persist | Integration |
| `TS-GL-0002` | An unbalanced journal is refused and **no row is persisted** — verified by counting rows, not by trusting the return | Integration |
| `TS-GL-0003` | A one-line journal is refused | Domain |
| `TS-GL-0004` | Amounts round-trip `decimal(19,4)` without precision loss, including a value with four decimal places | Integration |
| `TS-GL-0005` | The fiscal period is resolved from the entry date, not accepted from the caller — a request supplying one is refused as an unknown property | API |

## Immutability — the scenarios that justify the interface

| ID | Scenario | Layer |
|---|---|---|
| `TS-GL-0006` | Updating a posted journal through the repository is refused | Integration |
| `TS-GL-0007` | **Updating a posted journal by attaching it directly to the context and calling `SaveChangesAsync` is refused** | Integration |
| `TS-GL-0008` | Deleting a posted journal's line is refused | Integration |
| `TS-GL-0009` | A reversing journal posts, and the original is byte-for-byte unchanged | Integration |

> **`TS-GL-0007` is the one that matters.** `TS-GL-0006` would pass even if the guarantee were only "there is
> no repository method for it", which is exactly the protection `IAppendOnlyEntity`'s own comment says is
> insufficient. Only the direct-attach path proves the write boundary is doing the work.
>
> Under `OD-GL-0007` option 2, **`TS-GL-0007` cannot pass** — the type must be mutable to be posted at all.
> That is the clearest statement of what option 2 costs, and it is why it is written here as a scenario rather
> than as prose.

## Fiscal period and account state

| ID | Scenario | Layer |
|---|---|---|
| `TS-GL-0010` | Posting into a closed period is refused with `Gl.FiscalPeriodClosed` | Integration |
| `TS-GL-0011` | A period closed **between** validation and posting still refuses — the check reads live state | Integration |
| `TS-GL-0012` | Posting to an inactive account is refused and the error names the account | Integration |
| `TS-GL-0013` | Journals posted before deactivation remain readable afterwards | Integration |

## Platform obligations — the ones GL is most likely to under-test

| ID | Scenario | Layer |
|---|---|---|
| `TS-GL-0014` | **The E3 cutover copies GL journals between databases, and does so by INSERT — no path attempts an UPDATE on an append-only table** | Integration |
| `TS-GL-0015` | Every GL `ITenantOwnedEntity` appears in the manifest built by `TenantCutoverCopyPlan.Build`, asserted by derivation rather than by a hard-coded list | Integration |
| `TS-GL-0016` | The `DEC-POS-0022` ten-site inventory is updated for GL's entities | Integration |
| `TS-GL-0017` | Every GL string column is `nvarchar`; asserted from `sys.columns`, not from the model | Integration |
| `TS-GL-0018` | No FK exists from any GL table to any Platform-database table | Integration |
| `TS-GL-0019` | Arabic text in a description round-trips unchanged | Integration |

> **`TS-GL-0014` does not exist for any current append-only table.** No such table has yet been central enough
> for anyone to ask whether the cutover mutates one. GL makes the question load-bearing, and the scenario is
> listed so it is written before the volume arrives rather than after a cutover fails.
>
> **`TS-GL-0017` reads `sys.columns` deliberately.** Asserting `nvarchar` from the EF model tests the model's
> opinion of the database. FP-009 established the pattern of asserting against the catalog views instead, and
> it is the only version that would catch a hand-written migration.

## Authorization

| ID | Scenario | Layer |
|---|---|---|
| `TS-GL-0020` | A caller with `GL.Journals.Post` but no authorized company is refused **at the write boundary** | Integration |
| `TS-GL-0021` | A scope resolved for company A cannot read company B's journals | Integration |
| `TS-GL-0022` | `Platform.Tenant.Administer` alone posts nothing and reads nothing | Integration |
| `TS-GL-0023` | A `JournalReadScope` cannot be constructed with an empty company set | Domain |
| `TS-GL-0024` | A GL permission constant that no catalog contributor defines authorizes nothing | Integration |
| `TS-GL-0025` | An out-of-scope account is reported as not found rather than forbidden | API |

> `TS-GL-0023` is a *domain* test and it is the cheapest high-value test in this list: it proves the
> "empty means everything" bug is unrepresentable rather than merely absent.
>
> ⚠⚠ **CORRECTED 2026-09-01 — `TS-GL-0023` RETURNS ZERO FILES IN `tests/`, AND THE PROPERTY IS ASSERTED
> ANYWAY:** `GlArchitectureTests.An_empty_company_set_cannot_produce_a_scope`
> (`tests/Architecture.Tests/GlArchitectureTests.cs:136`), **whose own comment carries this paragraph's
> sentence almost verbatim.** ⚠ **The test was clearly written FROM this text and the scenario id was never
> carried into it — which is the whole shape of this defect class: the work was done and only the pointer
> is dead.**

## Transport

| ID | Scenario | Layer |
|---|---|---|
| `TS-GL-0026` | An unknown property in the request body is refused, not ignored | API |
| `TS-GL-0027` | A request supplying a currency is refused as an unknown property | API |
| `TS-GL-0028` | A body exceeding the configured limit is refused with the size error, not a parse error | API |
| `TS-GL-0029` | An unrecognized query-string filter is refused | API |
| `TS-GL-0030` | Invalid UTF-8 in any text field is refused, never substituted | API |

## Reporting

| ID | Scenario | Layer |
|---|---|---|
| `TS-GL-0031` | A trial balance over a period of balanced journals itself balances | Integration |
| `TS-GL-0032` | A trial balance computed for a scope-limited caller still balances — **the scope predicate is applied to both sides of the ledger** | Integration |

> `TS-GL-0032` is the scenario most likely to catch a real defect, because a filter applied to debits and not
> to credits produces a plausible-looking report that is silently wrong.

## Concurrency

| ID | Scenario | Layer |
|---|---|---|
| `TS-GL-0033` | Two concurrent account updates: the loser is refused via `RowVersion` | Integration |
| `TS-GL-0034` | Two concurrent journals in the same fiscal year cannot take the same number | Integration |

---

## A note on the gate

GL's integration scenarios will be numerous and will each create a disposable catalog. Before adding any of
them to a serial collection, read `TenantBackupSerialSuites`' admission rule: a class belongs there **only if
it uses a resource shared across databases** that a per-test catalog cannot isolate. "It is heavy", "it needs
real SQL", and "the class next to it is a member" are explicitly not reasons — that collection reached fifteen
members by arrival convention and had to be cut back to eight.

GL is large enough to undo that work by itself if the rule is not read first.
