# How to read an item citation in this repository

**Written 2026-08-31 (T-204). Three numbering series have been used here, and one of them resolves to
nothing.** This file exists so that a reader chasing a number stops hunting when hunting is futile.

## The three series

| citation you will see | resolves to | status |
|---|---|---|
| **`T-157`**, `T-201`, … | `T-157.md`, `T-201.md` | ✅ **resolves** — 137 distinct numbers across 139 files |
| **`item 157`**, `item 194`, … **where N ≥ 152** | `item-157-anonymous-door.md` | ✅ **resolves** — 47 numbers, the series begins at 152 |
| ⚠ **`item N` where N < 152** | **nothing** | ⚠⚠ **DOES NOT RESOLVE — see below** |

⚠ **THE TWO FILE SERIES DO NOT COLLIDE.** Eight numbers appear in both — **156, 157, 158, 179, 180, 191,
194, 201** — and in every case they are **different items with different subjects**. **The prefix
distinguishes them**: `T-157` and `item 157` are both unambiguous, and a bare `157` was never a citation
form this loop wrote. **Do not renumber anything on account of these eight; it was checked in T-204.**

## ⚠⚠ The dead end: `item N` below 152

**In the `T-` era, `item N` meant a DIFFERENT COUNTER, recorded only inside some `T-` files' own headers.**
Four such labels exist, and they are the only mappings anybody has:

| label found in the file | means |
|---|---|
| `# T-191 (item 40)` | **item 40 = T-191** |
| `# T-194 (item 41)` | **item 41 = T-194** |
| `# T-201 (item 48)` | **item 48 = T-201** |
| `T-181`'s status line, `ESTABLISH ONLY, item 26` | **item 26 = T-181** |

⚠⚠ **AND THE FOUR LABELS ARE NOT ALL IN THE SAME PLACE. Three are in the file's FIRST-LINE HEADER; the
fourth is in `T-181`'s STATUS LINE.** **A reader grepping for the header form finds three and concludes
that is all there is.** **If you go looking for more, search the file bodies, not the headings.**

⚠ **`item N` is cited for 25 distinct numbers below 152, and only three of them (40, 41, 48) can be
resolved by the labels above.** The other twenty-two — **1, 4, 5, 8, 9, 10, 16, 24, 25, 47, 56, 64, 90,
91, 95, 100, 116, 120, 123, 125, 133, 136** — **have no file and no recorded mapping.**

**They are listed here so the hunt can stop.** ⚠ **A reader chasing `item 40` today finds nothing, and no
rule anywhere told them to look inside `T-191`'s header.** **The failure is resolving to NOTHING rather
than to the WRONG thing, which is the better of the two and still a failure.**

## Two other things a reader will notice and should not re-investigate

- **Two numbers carry two files each, and they are VARIANTS of one item, not distinct items:**
  `T-056.md` + `T-056-design.md`, and `T-098.md` + `T-098-part1.md`. **That is why 139 files carry 137
  numbers.**
- ⚠ **Twenty-seven result files that `CODER.md` required were never committed** (T-204, T-203). **Twenty-six
  have no history on any branch and are unrecoverable.** **The twenty-seventh, `T-181.md`, was recovered
  from `agent/T-181-fiscal-overlap-establish` and carries a note saying so** — it must not be mistaken for
  a file that was always here.
- ⚠ **`BOARD.md` records `T-056.md` and `T-057.md` both being *copied to scratchpad before removal*. Only
  `T-057` stayed removed; `T-056.md` is present.** **The board entry describes an operation only half of
  which persisted** — which is also the only hard evidence that any of the twenty-seven were WRITTEN rather
  than never produced, since you cannot diff a file that was never written.

## Why nothing was renumbered

**Renumbering would break every citation already written, in this repository and in the handoff files.**
⚠ **And it would not touch the real problem at all: the twenty-two unresolvable numbers HAVE NO FILES TO
RENAME.** **Only a mapping table helps, and this is it — partial, and honest about which part is missing.**
