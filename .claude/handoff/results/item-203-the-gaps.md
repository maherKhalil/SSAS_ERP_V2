# item 203 — the gaps are ISSUED items whose result files were never committed

**Report only. Nothing reconstructed.** ⚠ **The ruling offered "never issued" as a complete answer that
would end the item. IT IS NOT THE ANSWER — every gap examined was issued.**

## ⚠⚠ FIRST, A CORRECTION TO ITEM 201's OWN NUMBERS

Item 201 said **"139 files across T-001…T-201, with 149 numbers missing"**. **Both figures are wrong.**

**137 distinct `T-nnn.md` files; 64 numbers missing in [1..201]** — and 201 − 137 = 64 confirms it.

**The cause:** I compared a zero-padded list (`001`) against `seq`'s unpadded output (`1`), so almost every
number read as absent. **A join on two different formats of the same key**, which produces a plausible
number rather than an error. It did not change 201's conclusion — five branch-held files are still a small
fraction — but the fraction was five of 64, not five of 149, and **the trail is far less sparse than I
reported.**

## The 64 are two different things, and only one is a gap

| | numbers | what they are |
|---|---|---|
| covered by an `item-N` file | **37** | ⚠ **A RENAMING, NOT A GAP.** The series continues as `item-nnn-*.md` from 152 on |
| absent from both series | **27** | the real candidates |

The two series overlap on **8** numbers (156, 157, 158, 179, 180, 191, 194, 201), so the handover was not a
clean switch — but 37 of the 64 "missing" numbers have a result file under the newer name.

## ⚠⚠ ALL 27 WERE ISSUED. NONE IS AN UNUSED NUMBER

| evidence | count |
|---|---|
| named in `BOARD.md` / `BACKLOG.md` / `OWNER-DECISIONS.md` | **21** |
| the other 6 — by branch or commit | **6** |

The six with no handoff-document trace still exist: `T-052`, `T-053`, `T-054` are named in commit
`81de8a0`; **`T-154` and `T-178` have MERGED PULL REQUESTS — #267 and #286** — and `T-181` has a branch.

⚠ **A board-only check would have called those six "never issued" and been wrong about two MERGED items.**
The trace has to include branches and commits, which is why the sample was widened rather than reported.

## The result files were never committed

**26 of the 27 have NO history anywhere in the repository** — `git log --all` over
`.claude/handoff/results/T-nnn.md` returns nothing, branches included. **The 27th is `T-181`, whose file
exists only on `agent/T-181-fiscal-overlap-establish`** — the case item 201 already found.

**And the result file is not optional:** `CODER.md:83` — *"**Write the result file**, then send `RESULT`,
then end your turn"* — and `:371` names the exact path. **So these are 27 omissions of a required step, not
27 items that legitimately produced nothing.**

## ⚠ WHAT I CANNOT DISTINGUISH, AND IT IS THE INTERESTING HALF

**"Written but never committed" and "never written" look identical to git.** An uncommitted file that no
longer exists leaves no trace of either kind.

**But at least two were written.** `BOARD.md` records `T-056.md` and `T-057.md` being **"copied to
scratchpad before removal, not deleted"**, with a byte-level `diff` argument about line endings — you
cannot diff a file that was never written. **T-057 is one of the 27**, so for that one the answer is
*written, never committed, deliberately removed.*

⚠ **So the ruling's framing was right to suspect the day's pattern at scale**: three uncommitted result
files surfaced in one day, and the historical gaps are the same failure — **but I can only prove the
"never committed" half.** How many of the 26 were ever written is not recoverable from this repository.

## Scope
- **The sample was 15, chosen as every 4th missing number to spread across [1..201] — deterministic, not
  cherry-picked.** ⚠ **Then the classification was run over ALL 27**, because the per-number check was cheap
  and a sample would have left the count itself uncertain.
- ⚠ **The range [1..201] is bounded by the last SURVIVING file, not the last issued number.** `BOARD.md`
  says *"We are at T-229"*, so **T-202…T-229 were issued and have no `T-nnn.md` either** — they belong to
  the `item-N` era and were not examined here.
- No file was reconstructed, recovered, or created.
