# T-056 — preconditions established, and the shape they rule out

- **Status:** DESIGN + EVIDENCE. **No mechanism implemented**, per the instruction to check back at the
  design step. Nothing under `scripts/` is touched by this branch.
- **Branch:** `agent/T-056-preconditions`, from `ClaudeBranch` at `47ee511`
- **Everything below was run on this box.** Where a claim is not tested, it says so.

## The headline: the obvious mechanism does not work, and fails silently

**A `sp_getapplock` taken by a one-shot `sqlcmd -Q` is gone the instant `sqlcmd` exits.**

```
acquire, connection closes   -> 0   (acquired)
acquire again, seconds later -> 0   (acquired AGAIN -- nothing was holding it)
sys.dm_tran_locks            -> no rows
```

The lock is scoped to the **session**, and a one-shot `sqlcmd` *is* the session. So the natural
reading of the task — the gate takes a lock, runs, releases it — **acquires nothing, guards nothing,
and reports success at every step.** Both the take and the release return 0. A gate built that way
would announce that it holds an exclusive lock and would then collide exactly as it does today.

That is the same defect class as the thing T-056 exists to prevent: **a guard whose failure mode is
indistinguishable from working.**

## Precondition 1 — what actually holds a lock

A **persistent connection** does. A `sqlcmd` that acquires and then stays open blocks a challenger:

```
holder: sp_getapplock ... ; WAITFOR DELAY '00:00:40'      (connection stays open)
challenger, 2 s later:  acquire returned -1               (blocked)
holder killed:          acquire returned 0                (released, within 4 s)
```

So the holder must be a **live process for the whole run**, not a call. Everything below follows from
that one fact.

### What a challenger can learn about the holder — and what it cannot

```
resource   : 0:[SSAS_GATE_PROBE]:(a32bba16)
session    : 54
host_name  : DEV-MAHER-MOHAM
program    : SQLCMD
login      : DEV-MAHER-MOHAM\Maher Mohamed
login_time : 2026-08-27 12:03:19
host_pid   : 316
```

**`login_time` gives "started when" for free.** `host_name` gives the machine. **Nothing gives the
worktree path** — and the task requires *"another gate is running in `<path>`"*. `host_pid` is the
**`sqlcmd`'s** pid, not the gate's, which matters below.

### A trap that would have been missed: the lock namespace is per-database

```
holder in master, challenger in master  -> -1   blocked
holder in master, challenger in tempdb  ->  0   ACQUIRED THE SAME NAME
```

Application locks are database-scoped. A gate that omits `-d` inherits the login's default database —
`master` here, today, for this login. It would work **by accident of a login setting** and stop
working for any login configured differently, with no error anywhere. **`-d master` must be
explicit**, and that is a one-word detail that silently voids the entire guard.

## Precondition 2 — what happens when the holding gate is killed

**The answer is not "it releases". It orphans.**

```
wrapper spawns holder as a child, exactly as a gate script would
kill -9 <wrapper>            -> holder STILL ALIVE, session 63 STILL HOLDING 5 s later
kill -9 <the sqlcmd itself>  -> released within 4 s
```

The lock outlives the gate, held by a process nobody is watching, for as long as that process lives.

### And `trap` does not fix it — tested twice, both negative

```
trap 'kill "$HOLDER"' EXIT INT TERM

kill -TERM <gate>                 -> trap did NOT fire; lock held 5 s later; 2 sqlcmd alive
kill -INT  -<gate process group>  -> trap did NOT fire; lock held 6 s later; 2 sqlcmd alive
```

Two reasons, each sufficient on its own:

1. **Bash defers a trap while a foreground child runs.** A gate is inside `dotnet test` for
   essentially its whole life — up to 33 minutes for a single Integration leg. The signal is queued
   until that returns, which is far too late to be called cleanup.
2. **`sqlcmd` is a native Windows process.** MSYS signal delivery does not reach it the way it reaches
   a shell, so the holder survives the signal that was supposed to end it.

**The release path therefore cannot depend on the gate cleaning up after itself.** Any design that
says *"and on abort we kill the holder"* is asserting something that does not happen on this box. I
would have assumed it worked. It does not, and finding out cost two minutes.

*One test in this sequence was **void, not negative**: an attempt to signal a new process group failed
with `setsid: command not found`, so no lock was ever taken and the probe printed
`NONE -- RELEASED`. It is recorded because **a void test that prints the same string as a pass is
worth more as a warning than as a result.***

## The shape I think is right

**Three parts, deliberately separable, because only the middle one is a judgement.**

**1. Detection — an applock in `master`, held by a dedicated long-lived process.** Presence of the
lock is the fact. It cannot be forgotten and it releases within ~4 s whenever the holding process
actually dies. **Fails closed:** a challenger that cannot reach the instance cannot conclude the coast
is clear, and must abort — with a **new exit code 7**, distinct from 1 and from the 2–6 precondition
codes.

**2. Label — one row written before acquiring: root path, gate pid, start time, scope.** Not
authoritative. It exists so the refusal can say *"another gate is running in `<path>`, started
`<when>`"* rather than refusing bare, which is the requirement the applock alone cannot meet. A stale
row is harmless because **the lock, not the row, is the truth.** It must not live in an `SSAS[_]%`
catalog, or `reap_to_zero` will drop the guard's own state.

**3. Liveness — the challenger checks whether the recorded *gate* pid is still alive.** This is what
turns an orphan from invisible into nameable. The DMV's `host_process_id` is the `sqlcmd`, which is
alive in exactly the orphan case, so it cannot answer this question; the gate's own pid can.

### The one decision that is yours, not mine

**Detection can say "the lock is held and the gate that took it is dead." Policy says what to do about
that.** Three defensible answers, and I am not picking one:

- **Refuse and print the recovery command.** Safest; a human decides. Costs a manual step at 2 a.m.
- **Take over automatically.** Frictionless; risks stealing from a live gate whose pid was reused, and
  pid reuse is not rare on a long-lived Windows box.
- **Refuse, then take over after a stated age.** Splits the difference and introduces a number nobody
  can justify from evidence.

**The reason to separate them is that detection is testable and policy is arguable.** Welding them
together means the arguable half cannot be revisited later without retesting the half that was fine.

### What I am not proposing, and why

`basename "$ROOT"` → `git rev-parse --git-common-dir` closes the case that prompted the task and
nothing else. **The shared resource is the SQL Server instance, not the repository** — repo identity
would correctly tell a second clone that it is unrelated, which is true and irrelevant, because it
reaps the same catalogs. The task already argues this; what the tests above add is that the
instance-level alternative is actually buildable, with the caveats recorded.

## The third question, established and NOT answered

**"Am I even running the merged script?"** Cheap to detect, and the detection is already in hand:

```
git hash-object -- scripts/gate.sh                 -> 571225f...  (this tree)
git ls-tree origin/ClaudeBranch -- scripts/gate.sh -> 571225f...  SAME
```

Blob against blob, so none of the working-file/CRLF confusion that has cost three separate mistakes
today. **And it is not hypothetical** — the same probe against the other tree on this box that carries
a gate:

```
SSAS_ERP_V2-chain-test/scripts/gate.sh -> 81e342e...  DIFFERENT
  last touched 2026-08-25;  grep -c GATE_SCOPE -> 0
```

**That tree would run the pre-`GATE_SCOPE` gate today**, and report exactly what it has always
reported.

Two properties to settle before anyone builds on this, neither of which is settled:

- `origin/ClaudeBranch` is **only as fresh as the last fetch**, so the probe compares against whatever
  was last pulled. Making it authoritative means a network call in the gate's precondition path.
- **A gate that refuses when it cannot reach the remote stops all work offline.** Fail-closed is right
  for the concurrency guard, where the failure is destructive. It is not obviously right here, where
  the failure is staleness. **That is a different trade and must not inherit the concurrency answer.**

## Two corrections to the task file

**The worktree table is out of date, and `../SSAS_arch` does not exist.** Verified with `git worktree
list` and `ls`, not inherited:

```
SSAS_ERP_V2                  ClaudeBranch                          gate.sh PRESENT
SSAS_ERP_V2-chain-test       codex/chain-test          2026-08-25  gate.sh PRESENT  <- and STALE
SSAS_ERP_V2-admin-transport  codex/platform-admin-...  2026-08-10  no scripts/
SSAS_ERP_V2_TENANT_ARCH      docs/tenant-storage-adrs  2026-08-13  no gate
```

**Four trees, one live collision pair — unchanged from the task file's count.** The architect's
`../SSAS_arch` is neither registered nor on disk; their docs tree is `SSAS_ERP_V2_TENANT_ARCH`, which
the table already listed. So the count did not change and the name in the message was wrong.

**And the second tree is no longer merely latent.** The task says the older trees *"acquire the hazard
the moment anyone pulls"*. `chain-test` carries **both** hazards already: it can reap a live run's
catalogs, and it would do so while running a gate that predates `GATE_SCOPE`.

## Folded in, not yet done

The **sampler-absence line** — one line saying the sampler did not run, so absence stops reading as
not-applicable. Same file, same kind of change, no separate gate run. It belongs with the
implementation rather than with this note.

## For the architect

- **Blocked / needs a decision:** the detection-versus-policy choice above. That is the whole of it.
- **Touched outside scope:** nothing. One new file under `.claude/handoff/results/`.
- **Not implemented on purpose:** every mechanism described here.
