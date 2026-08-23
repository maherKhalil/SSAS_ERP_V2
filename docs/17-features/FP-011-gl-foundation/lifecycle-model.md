---
package: FP-011
title: General Ledger — Lifecycle Model
status: APPROVED — OD-GL-0007 ruled two aggregates; period and account lifecycles settled
version: 1.0
date: 2026-08-23
---

# FP-011 — Lifecycle Model

> **DECISIONS CLOSED, 2026-08-23.** All nine owner decisions are ruled; conditional wording below is kept as
> the record of what was weighed, with the ruling stated where it changes the answer.
>
> | | | | |
> |---|---|---|---|
> | `0001` catalog: ratified into `GL.md` | `0002` **single currency** | `0003` **tenant-level chart** | `0004` **company calendar** |
> | `0005` **no branch dimension** | `0006` **reversal + `ReversesJournalId`** | `0007` **two aggregates** | `0008` **period close only** |
> | `0009` **manual entry only** | | | |

Three things have a lifecycle in GL: the **journal**, the **fiscal period**, and the **account**. Only one of
them is genuinely undecided, and it is the one that decides whether `BR-GL-0002` is enforced by the platform
or by convention.

---

# The journal — `OD-GL-0007`, **RULED: option 3, two aggregates**

The three options are kept below because the one chosen is only meaningful next to the ones refused — in
particular, option 2 is the only one that would have cost `BR-GL-0002` its structural enforcement, and that
is worth being able to re-read.

## Option 1 — no drafts (proposed default)

```
        post
  (none) ────> POSTED ────────────> POSTED, REVERSED BY J2
                 │                        ▲
                 └── reverse: post J2 ────┘   (J2 is itself POSTED)
```

A journal exists only once it is valid. There is no state in which it is persisted and unbalanced, so
`BR-GL-0001` is an invariant of existence rather than a check on a transition. `JournalEntry` carries
`IAppendOnlyEntity` from creation and the write boundary refuses every subsequent `Modified` or `Deleted`.

**"Reversed" is not a state on the original.** It is a fact derivable from the existence of a reversing
journal that points at it — under `OD-GL-0006` option 1. Storing it as a status on the original would require
**modifying an append-only row**, which the write boundary refuses. That is not a limitation to work around;
it is the guarantee working. Anyone who finds themselves wanting a `Status = Reversed` column on the header
has found the boundary, not a bug.

## Option 2 — one aggregate with a status

```
  DRAFT ──edit──> DRAFT ──post──> POSTED ──(cannot change)──> ...
```

The transition `DRAFT → POSTED` is an `UPDATE`. Therefore the type **cannot** be `IAppendOnlyEntity`, and
`BR-GL-0002` becomes an aggregate-level guard: correct code that a future path can bypass, since the interface
existed precisely because *"there is no repository method for it" protects only the callers who go through the
repository.*

This is the only one of the three options that gives something up. It may still be the right answer — drafts
are genuinely useful — but it should be chosen knowing what it costs.

## Option 3 — two aggregates

```
  JournalDraft (mutable) ──promote──> JournalEntry (append-only)
        │                                    │
      edit/delete freely            no modification, ever
```

Keeps the structural guarantee **and** allows drafts. Costs a second type, a mapping, and a rule about what
happens to the draft after promotion (deleted, or retained as history — itself a small decision).

---

# The fiscal period

```
  OPEN ──close──> CLOSED ──reopen?──> OPEN
                            ▲
                   NOT SPECIFIED by any rule
```

`BR-GL-0003` gives the closed state its only stated consequence: posting is prohibited. Everything else is
open:

* **Is reopening permitted at all?** No rule says. Many ledgers forbid it outright; many permit it under a
  separate permission and an audit record.
* **Must periods close in order?** Closing period 5 while 3 is open is either a bug or a feature, and nothing
  says which.
* **Does closing a year close its periods?** Depends on `OD-GL-0008`.

All three are recorded rather than invented. They belong with `OD-GL-0004` and `OD-GL-0008`.

**One thing that is decided:** period state is checked **at post time against live state**, not captured on
the journal. A journal that was valid when drafted and whose period closed before posting must be refused —
which is another reason option 2's draft state above is not free.

---

# The account

```
  ACTIVE ──deactivate──> INACTIVE ──reactivate?──> ACTIVE
```

`BR-GL-0004`: an inactive account **cannot receive transactions**. Note what it does not say — it does not say
the account disappears, and it does not say its history becomes invalid. Deactivation is a lifecycle state,
not a deletion, exactly as `PreventCompanyDeletion` makes Company archive rather than delete so *"history
stays reconstructable."*

**Reactivation** is unstated. It is a much smaller question than period reopening — an account is master data,
not a closed book — and this package suggests it be permitted without ceremony unless the owner says
otherwise. It is listed here so the suggestion is visible rather than silently implemented.

**What is decided:** the active check runs at post time against the account's current state (`DEC-GL-0009`),
and the refusal names the account. A journal posted before deactivation stays valid — that is the whole point
of the rule being about *receiving* transactions.

---

# What the lifecycles have in common

Every state check above happens **at post time against live state**, never against a value captured earlier.
That is a deliberate pattern and it matches how the platform resolves scope: `EmployeeReadScope` resolves its
authorized sets *"against live state"* at the moment of the read, rather than trusting anything the caller
carried in.

The cost is that a long-running client can be refused for a state change it never saw. That is correct
behaviour for a ledger, and the refusals should say which state changed rather than failing generically —
"period 2026-05 is closed" and "account 4100 is inactive" are the difference between a user fixing something
and a user filing a ticket.
