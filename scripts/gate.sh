#!/usr/bin/env bash
set -u
# ==================================================================================================
# THE GATE. TWO SCOPES: `TASK` (default, minutes) AND `PHASE` (the full run, ~69 minutes).
# ==================================================================================================
#
# **`GATE_SCOPE=PHASE` is the phase-exit gate and is unchanged**: the full Integration suite PLUS EVERY
# OTHER TEST PROJECT IN FULL, in Debug AND Release (2026-08-21 ruling). Debug-clean is not evidence of
# Release-clean: the analyzer sets differ, and the first Release run exposed CA1826 warnings and an
# allocation assertion that had never worked.
#
# **`GATE_SCOPE=TASK` is the default** and answers a narrower question -- see note 7y. It exists because
# `DEC-L-008` condition 3 has ALWAYS scoped Integration to tasks that touch persistence, while this
# script offered no way to honour that without also buying every other suite twice. The rule was already
# tiered; the instrument was not.
#
# --------------------------------------------------------------------------------------------------
# EIGHT BEHAVIOURS HERE ARE NOT PREFERENCES. EACH WAS PAID FOR BY AN INCIDENT.
# --------------------------------------------------------------------------------------------------
#
#  1. TRX PER SUITE PER CONFIGURATION.
#     A duration-by-class and failure-identity analysis once had to scavenge month-old artifacts
#     because no run left any. Every invocation now leaves a first-class one.
#
#  2. UNFILTERED LOG TO FILE; THE GREP IS A VIEW, NEVER THE RECORD.
#     2026-08-22: a Debug Integration run failed 1 of 729 and the failing test was UNRECOVERABLE. The
#     script piped `dotnet test` straight into `grep -E "Passed!|Failed!|error"`, and xUnit prints
#     failures as `[xUnit.net ...] Class.Method [FAIL]` / `Error Message:` -- neither matches, because
#     `[FAIL]` contains no "Passed!"/"Failed!" and "Error" is capitalised. A four-hour run produced a
#     red result nobody could act on.
#
#  3. PER-SUITE EXIT CODES CAPTURED, PLUS AN EXPLICIT [GATE RED] / [GATE GREEN].
#     With the old pipe, `$?` was GREP's status, so the gate exited 0 while BOTH configurations had a
#     red Integration suite. A gate that reports success on a failing run is worse than one that
#     reports nothing, because nobody goes looking.
#
#     ---- EVERY ABORT PATH EXITS NON-ZERO, AND IT MUST STAY THAT WAY. Do not "tidy" one back to 0.
#
#     The four preconditions in `reap_to_zero` exit with DISTINCT codes so a caller can tell them apart:
#
#       2  a testhost of ours is already running -- a sibling suite is live
#       3  a catalog matches the test prefix but does not look like a test catalog
#       4  the reap left catalogs behind; CatalogLeakGuardTests would fail on them
#       5  the box is below the memory floor for this mode
#
#     And two more, outside `reap_to_zero`, which abort before any leg starts:
#
#       6  GATE_SCOPE or GATE_INTEGRATION holds a value this script does not recognise (note 7y)
#       7  another gate holds the instance lock, or its state could not be read at all (note 7x)
#
#     6 AND 7 REFUSE RATHER THAN DEFAULT, for the same reason: both would otherwise run a DIFFERENT
#     gate from the one the caller asked for and report it under the requested name.
#
#     A merge rule was built on this file's exit code (`DEC-L-007`: a green gate is merge authority for
#     code), so a precondition abort that returned 0 would let untested code merge. It does not, and the
#     reason it does not is that `exit` inside `reap_to_zero` terminates the script rather than the
#     function -- the function is called plainly from the `for CFG` loop, never in a subshell and never
#     behind a pipe. **Wrapping that call in a pipeline or `$( )` would silently break all four.**
#
#     ---- DO NOT PIPE THIS SCRIPT. `$?` BECOMES THE PIPE'S STATUS, NOT THE GATE'S. Paid for 2026-08-25.
#
#     `bash scripts/gate.sh | tail -80` reports exit 0 for an abort that exited 5, because a shell
#     pipeline yields the status of its LAST command and `tail` succeeded. It also BUFFERS: a 69-minute
#     run produced no output at all until it finished, so a live gate was indistinguishable from a task
#     that had produced nothing. Both effects were diagnosed as defects in this script; neither was.
#
#     Run it bare and let it stream. If output must be captured, use a redirect -- `bash scripts/gate.sh
#     2>&1 | tee log` still loses `$?` unless `set -o pipefail` is set in the CALLING shell, so prefer
#     `bash scripts/gate.sh > log 2>&1` and read the exit code directly.
#
#  4. REAP TO ZERO, WITH VERIFIED PRECONDITIONS.
#     `CatalogLeakGuardTests` asserts no SSAS_ catalog predates the test process -- correct and
#     deliberately unweakenable. A Phase 1 gate failed it in BOTH configurations because filtered runs
#     during the wait had left orphans and the operator was supposed to hand-reap and did not. Reaping
#     BLIND, however, is how a live sibling suite's catalogs get dropped mid-run, so every precondition
#     is checked and a surprise ABORTS rather than destroying what it cannot identify.
#
#  5. --blame-crash ON INTEGRATION.
#     2026-08-23: the test host DIED twice and vstest could not name the test in flight. It reported
#     `Test host process crashed : WARNING: Using a process-local ephemeral RSA JWT signing
#     certificate` -- the certificate line being merely the last thing the dying process wrote to
#     stderr, so the stated "Reason" was a red herring. Recovering even the CLASS of the loss took a
#     TRX diff against the other configuration.
#
#  6. "Test Run Aborted" DETECTION.
#     The same run reported `Failed: 3, Passed: 747, Total: 750` -- which reads as an ordinary red
#     while FIFTEEN tests had silently vanished from the total. A summary line whose total quietly
#     shrank is the most dangerous shape a gate can print. A red that under-reports is one accident
#     away from a green that under-reports.
#
#  7s. THE TRACEABILITY CHECK, WIRED AT LAST, AND NOT TO A HARD ZERO. T-065.
#
#     `scripts/trace-check.py` existed for weeks and was in NEITHER this script NOR
#     `.github/workflows/ci.yml`. **Nine packages were red and nobody remarked on it, because
#     nothing ran it.** That is the last instance of the shape this file spent a day removing:
#     an instrument whose output nobody sees is indistinguishable from an instrument that
#     passes.
#
#     ---- RED ON A RISE. NEVER ON THE STANDING COUNT.
#
#     Eleven failures stand today and **every one is work a package has already declared
#     pending** -- FP-003's `AC-TEN-0078`..`0093`, marked "implementation pending" in its own
#     acceptance-criteria file. A gate permanently red on declared work **is a gate people
#     switch off**, and switching it off is how the nine went unnoticed in the first place.
#
#     So the comparison is per package against a committed baseline, and only a RISE is red.
#     **Improvement ratchets**: a package that gets better lowers its own baseline on the next
#     clean run and cannot raise it again, so the first person to fix something does not hand
#     the next person room to break it.
#
#     ---- THE COST IS MEASURED, NOT ASSUMED.
#
#     730 ms over three runs, against a 72-second TASK gate: **one per cent.** It therefore
#     runs in BOTH scopes. `DEC-L-051` bought back 68 minutes and this does not spend it --
#     had the number been material it would have belonged in PHASE only, and that is the
#     answer I was prepared to give.
#
#     ---- AND IT SAYS WHEN IT DID NOT RUN.
#
#     No `py`, or no script, prints `NOT RUN` rather than nothing. The memory sampler needed
#     that rule and did not have it for a day.
#
#  7t. CONDITION 4, PARTIALLY MECHANISED -- AND THE PARTIAL IS THE POINT. T-059.
#
#     `DEC-L-008` condition 4 was the last of the four held by nothing. The gate printed per-suite
#     counts and compared them to NOTHING; there was no baseline anywhere in this file. It was the most
#     dangerous of the four precisely BECAUSE the other three are now enforced -- someone who has
#     watched the gate go red on a warning and on a failed build reasonably assumes the count line is
#     checked too.
#
#     ---- WHAT IS CHECKED, AND WHAT CANNOT BE. DO NOT BLUR THESE.
#
#       CHECKED : did any suite total move, when non-comment lines under src/ changed?
#       NOT     : do the tests the task required exist? do the tests that moved cover what was written?
#
#     **The gate cannot know what a task required.** Wording that implies it does would be worse than
#     the silence this replaces, because it converts an honest convention into a false enforcement --
#     and this repository has now recorded four instruments that reported on a domain narrower than
#     their wording implied. Every line this prints is worded to claim only the first.
#
#     ---- MEASURE IT PER MERGED PR. PER COMMIT YOU WILL CONCLUDE IT IS UNSHIPPABLE.
#
#       per merged PR (200):  45 touched src/ -- 44 added a new [Fact]/[Theory], 1 did not
#       per commit    (300): 126 touched src/ -- 19 added no test
#
#     The gate runs once per task, and a task is several commits: intermediate ones legitimately land
#     code before their tests. **The granularity is the finding**, and the next person to evaluate this
#     will reach for commits first.
#
#     And the ONE spurious fire in 200 merges was `#66 T-018`: nineteen added lines under src/, all of
#     them a comment block, zero non-comment lines. **That is why the diff filters comments** -- with
#     the filter that false positive disappears, and it is exactly the "a docs task, a rename" case.
#
#     ---- THE DIFF INCLUDES THE WORKING TREE, AND AN UNREADABLE ONE SAYS SO.
#
#     `git diff <merge-base> -- src/` with no second commit compares against the WORKING TREE, so
#     uncommitted work counts. A merge-base-to-HEAD diff would see nothing before the first commit and
#     skip the check IN SILENCE -- permissive, which is the `DEC-L-051` hole rather than a smaller
#     version of it. Every path that cannot compare prints WHY it could not, rather than nothing.
#
#     ---- IT WARNS. IT NEVER FAILS THE GATE. AND THE REASON TRAVELS WITH THE LABEL.
#
#     Not because it is noisy -- it is not; zero spurious fires in 200 merges. **That number is
#     evidence about this repository's past, not about the check**, and "it has never happened" is the
#     same reasoning that made condition 1 look enforced when nothing enforced it.
#
#     The reason is the REMEDY ASYMMETRY. Conditions 0, 1 and 2 go red with an unambiguous fix: fix the
#     warning, fix the build, fix the test. **This one's wrong-fire remedies are to write a test you do
#     not believe in, or to route around the gate.** A red answerable by manufacturing a test produces
#     exactly the tests that make a suite worthless, and a legitimate refactor covered by existing
#     tests is CORRECT WORK the gate would be asking to fake something for. That reason will still be
#     true when the 200-merge number is stale.
#
#     **A tier without its reason is a ranking; with it, it is a distinction.** Condition 4 is a
#     different KIND of signal, not a less important one, and no assertion variable exists to argue
#     with it -- the honest reason would be "I refactored and existing tests cover it", which is
#     precisely what a warning already permits without teaching anyone the bar is negotiable.
#
#  7u. AM I THE MERGED GATE? WARN, NAME THE DISTANCE, PROCEED. T-058.
#
#     THE TREE IS STALE BY DEFAULT, AND THE MERGE THAT MAKES IT STALE IS THE ONE THAT JUST SUCCEEDED.
#     This happened THREE TIMES on 2026-08-27, in one shared tree, to both windows -- the third to the
#     window that had just recorded the second as a High finding. Nothing makes a working tree notice
#     that its integration branch moved, and `scripts/gate.sh` is one of the files that goes stale.
#     **A gate that is not the merged gate presents identically to a normal green run.**
#
#     It is live elsewhere too: `SSAS_ERP_V2-chain-test` carries a `gate.sh` from 2026-08-25 with ZERO
#     occurrences of `GATE_SCOPE`. That tree would run the pre-scope gate and report exactly what it
#     has always reported.
#
#     ---- WHY THIS WARNS WHERE NOTE 7x REFUSES. The two are deliberately opposite.
#
#     **Fail-closed requires a signal you can trust, and this signal is self-admittedly stale.** The
#     lock's evidence is authoritative -- `sys.dm_tran_locks` IS the truth -- and proceeding wrongly
#     destroys a 69-minute run. Here the evidence is a remote-tracking ref that is only as good as the
#     last fetch, and the failure is a WRONG GREEN rather than a destroyed run. A warning surfaces a
#     wrong green. A refusal on a possibly-stale signal lets a stale ref block every run on the box --
#     which is note 7x's own strand-the-instance failure, relocated.
#
#     ---- WHY IT DOES NOT FETCH. Measured on this box:
#
#       git fetch origin ClaudeBranch     1338 ms
#       clean offline (DNS fails)          140 ms
#       HALF-OPEN NETWORK               22 201 ms   <- thirty per cent of a 72-second gate
#
#     And a credential prompt would hang a gate forever with no output. `DEC-L-051` bought back 68
#     minutes; spending 22 seconds per run policing it would be self-defeating. **Offline is a normal
#     condition. A colliding gate is not.** The cost of not fetching is stated in the message itself --
#     "as of your last fetch, <when>" -- rather than left for the reader to remember.
#
#     ---- ANCESTRY, NOT A HASH, AND THAT IS THE HALF THAT MAKES IT USABLE.
#
#     A hash comparison says "different" and conflates two states with opposite meanings: a tree that
#     is BEHIND, and a tree that is DEVELOPING this file. Every task that has ever edited the gate
#     looks like the second. **A check that cries wolf at the developer gets disabled long before it
#     ever catches the stale tree.** So: is the last commit touching `scripts/gate.sh` on the
#     integration ref an ancestor of HEAD? Three states, and the distance reported in commits.
#
#     NO SUPPRESSION FLAG. If it becomes noisy the fix is a sharper check, not a way to silence it.
#
#  7v. THE BUILD IS A CONDITION, NOT A PREAMBLE. T-058.
#
#     ---- A FAILING BUILD USED TO REPORT GREEN. Demonstrated 2026-08-27, not argued:
#
#       Build FAILED.  1 Error(s)   (a deliberate CS0029)
#       Passed! 509 · 1032 · 326 · 724 · 46 · 56 · 59      <- the PREVIOUS build's assemblies
#       [GATE GREEN -- TASK scope]    exit 0
#
#     `dotnet build`'s status was computed and discarded: `grep` ran next, so `$?` was grep's, and
#     `GATE_FAILED=1` was set only inside the suite loop. The suites then ran `--no-build` against
#     whatever was last built successfully. **`DEC-L-007` makes this exit code merge authority, so the
#     gate was prepared to merge code that does not compile.** That is note 3's defect exactly, moved
#     from the verdict to the build -- found the day after the same shape was found in the sampler.
#
#     A failed build now SKIPS THE SUITES for that configuration and sets the flag. Running them would
#     produce a green that describes a build nobody performed.
#
#     ---- AND `--no-incremental` IS WHY THE WARNING BAR CAN BE ENFORCED AT ALL.
#
#     MSBuild skips up-to-date projects, so the compiler never re-runs and NEVER RE-EMITS THEIR
#     WARNINGS. Measured:
#
#       plant one CS0219, build   ->  1 Warning(s)
#       build again, no changes   ->  0 Warning(s)      <- the warning is still in the code
#       --no-incremental          ->  1 Warning(s)      16 s against 3 s
#
#     So a warning check over an incremental build reports on what it happened to recompile rather
#     than on the code, and **anyone who builds in an IDE first makes the gate's build a no-op.** That
#     is a coincidence with a log line, in the same sense that omitting `-d` in note 7x was a
#     coincidence with a login setting. Baseline when this landed: 0 warnings, Debug AND Release, so
#     enforcement cost nothing to adopt.
#
#     `DEC-L-008` condition 1 -- "the build succeeds at zero warnings" -- is now the gate's condition
#     rather than the coder's habit. Of the four merge conditions, 2 and 3 are suite runs and 4 is the
#     count comparison; **1 was held entirely by the person the rule applies to, under a rule that
#     merges on green without review.** It had never been violated, which is precisely what made it
#     read as a property of the instrument.
#
#  7x. ONE GATE AT A TIME, ENFORCED ON THE INSTANCE. T-056.
#
#     `reap_to_zero` drops every SSAS[_]% catalog on the box, under EVERY scope. Until T-056 the only
#     guard was the sibling-testhost check, which matches on `basename "$ROOT"` -- so two worktrees with
#     DIFFERENT DIRECTORY NAMES each concluded the other's testhost belonged to someone unrelated and
#     proceeded. A 72-second TASK gate would reap the catalogs a 69-minute PHASE run was using, mid-leg,
#     and the PHASE run would then fail in a way that looks exactly like a test failure.
#
#     `DEC-L-051` made this materially more likely: something that costs 72 seconds does not feel like
#     an action that needs checking first.
#
#     THE SHARED RESOURCE IS THE SQL SERVER INSTANCE, NOT THE REPOSITORY. `git rev-parse
#     --git-common-dir` would close the two-worktree case and nothing else -- a second clone, or a
#     colleague's checkout, reaps the same catalogs while repo identity correctly reports them
#     unrelated. So the lock lives where the damage lands.
#
#     ---- FOUR THINGS ESTABLISHED BY TEST, EACH OF WHICH RULES OUT AN OBVIOUS DESIGN.
#
#     A ONE-SHOT LOCK IS NOT A LOCK. `sp_getapplock @LockOwner='Session'` taken by `sqlcmd -Q` is
#     released the instant sqlcmd exits, because the lock is scoped to the session and a one-shot
#     sqlcmd IS the session. Two consecutive acquires both returned 0 with nothing held in between.
#     The take AND the release both succeed, so that gate announces an exclusive lock and guards
#     nothing -- the same shape as the defect this note exists to prevent.
#
#     THE NAMESPACE IS PER-DATABASE. A holder in `master` and a challenger in `tempdb` took the same
#     lock name without seeing each other. A gate that omits `-d` inherits the login's default
#     database and therefore works BY ACCIDENT OF A LOGIN SETTING. `-d` is not optional here.
#
#     A KILLED GATE DOES NOT RELEASE -- IT ORPHANS. `kill -9` on the gate left the holder sqlcmd alive
#     and still holding five seconds later; only killing the sqlcmd itself released it, within four.
#
#     AND `trap` DOES NOT FIX THAT. Tested twice, both negative: bash defers a trap while a foreground
#     child runs -- a gate is inside `dotnet test` for up to 33 minutes at a stretch -- and sqlcmd is a
#     native Windows process MSYS signals do not reach. The EXIT trap here is real and useful for
#     normal exit and for this script's own aborts; NOTHING DEPENDS ON IT.
#
#     ---- WHY LIVENESS AND NEVER AGE.
#
#     A legitimate PHASE run is 69 minutes and a single Integration leg is 33 minutes of one process.
#     NO THRESHOLD SEPARATES "hung" FROM "working correctly" HERE. Any age would either strand real
#     runs or license stealing them, and it would be a number nobody could defend. So the question
#     asked is never "how old" but "is that gate's process still alive":
#
#       lock held, holder's gate pid alive        -> REFUSE, naming root, pid, scope and start time
#       lock held, holder's gate pid provably dead -> RECLAIM, and say so loudly in the log
#       liveness undeterminable                    -> REFUSE. Fail closed.
#
#     PID ALONE IS INSUFFICIENT: a reused pid reads as alive. The pid must still be held by a process
#     whose START TIME matches the one recorded in the label. Get this wrong and the failure direction
#     is REFUSING WHEN WE COULD HAVE PROCEEDED -- the safe side, and it is not traded for convenience.
#
#     ---- THE LABEL IS NOT THE LOCK, AND THAT SEPARATION IS DELIBERATE.
#
#     The applock is the truth: it cannot be forgotten and it dies with its connection. But it carries
#     no payload, and `sys.dm_exec_sessions` gives login_time and host_name while never giving the
#     WORKTREE PATH -- and its host_process_id is the holder sqlcmd, which is alive in precisely the
#     orphan case. So a one-row label carries root path, gate pid, start time and scope, purely so the
#     refusal can name a holder instead of refusing bare. A guard that refuses without naming who holds
#     it gets disabled by the next person. A stale label is harmless BECAUSE THE LOCK IS THE TRUTH.
#
#     It lives in `tempdb` and not in any `SSAS[_]%` catalog, which `reap_to_zero` would drop -- the
#     guard's own state must not be reapable by the thing it guards.
#
#  7y. TWO SCOPES, ORTHOGONAL TO THE TWO MODES. `DEC-L-051`, implemented by T-055.
#
#     GATE_SCOPE=TASK (DEFAULT) -- seven suites, Debug only, NO Integration unless asked.
#     GATE_SCOPE=PHASE          -- all eight suites, Debug and Release. The gate as it was.
#     GATE_INTEGRATION=1        -- under TASK, adds the Integration leg. Ignored under PHASE.
#
#     **MODE is about what this box can afford; SCOPE is about what question is being asked.** Folding
#     them together would make the cheap answer unavailable on a build box and the thorough one
#     unavailable here, which is backwards for both.
#
#     WHY THIS EXISTS, IN ONE MEASUREMENT. Note 7z records 32 m 21 s and 32 m 35 s for the two
#     Integration legs of a 69-minute run: **about 65 of the 69 minutes are those two legs**, and the
#     remaining seven suites in both configurations cost roughly four. A per-task change that touches no
#     persistence was buying an hour to be told what four minutes already said.
#
#     MEASURED ON THIS BOX UNDER LEAN, 2026-08-27. BOTH FIGURES ARE FROM RUNS OF THIS BUILD -- neither
#     is inherited, because note 7z exists precisely because someone estimated one shape from another
#     and was forty minutes wrong:
#       TASK, no Integration, incremental build:      72 SECONDS   (build 18 s; 2752 tests, seven suites)
#       PHASE, both configurations:                 4095 SECONDS   (68 m 15 s; Integration 32 m 48 s
#                                                                  Debug and 32 m 55 s Release)
#
#     **Seventy-two seconds against sixty-eight minutes -- a factor of fifty-seven** -- and that ratio is
#     the whole argument for the scope split.
#
#     **PHASE cost nothing.** 68 m 15 s measured here against the 69 minutes note 7z recorded before
#     GATE_SCOPE existed: a difference of forty-five seconds, which is inside the run-to-run spread of
#     the two Integration legs themselves (they differ by seven seconds in this very run). The scope
#     machinery is two variables read once, and the measurement says so.
#
#     The build is the largest single component of a TASK run, so that figure is for an INCREMENTAL
#     build -- the normal per-task case. A cold build costs more and is not what this measures.
#
#     Use the measured TASK figure rather than deriving one from the FULL or PHASE shapes. Note 7z
#     records what deriving from the wrong shape cost: a 90-110 minute projection revised down mid-run.
#
#     ---- THE RED PATH IS VERIFIED BY RUN, NOT BY READING. 2026-08-27, T-055.
#
#     A gate that reports success on a failing run is note 3's subject and T-016's finding, and the
#     specific shape is `GATE_FAILED=$?` per suite instead of a latch -- under which a red suite followed
#     by a green one reports green. Verified deliberately, in a throwaway worktree, with the failure in
#     the FIRST suite so it had to survive six subsequent passes:
#
#       Architecture 1 failed of 510  ->  six suites green  ->  [GATE RED -- TASK scope]  ->  exit 1
#
#     A red LAST suite would have proved almost nothing. The structural half is `GATE_FAILED=0` appearing
#     exactly once, 181 lines above the configuration loop, which makes a per-configuration reset
#     unrepresentable without adding a line.
#
#     ---- INTEGRATION IS OPT-IN UNDER `TASK`, AND IS NEVER INFERRED FROM THE DIFF.
#
#     The coder knows whether `DEC-L-008` condition 3 applies -- persistence, a migration, an EF
#     configuration, the cutover inventory. A script reading the diff would be guessing, and **a guess
#     that is wrong in the permissive direction is a silent hole in the merge bar**: the suite that
#     should have run simply does not, and the log is green either way. This repository has recorded
#     that shape of defect four times.
#
#     ---- WHAT SCOPE DOES NOT CHANGE.
#
#     All four preconditions, both memory floors, the sibling-testhost refusal and the SSAS_% reaping run
#     for BOTH scopes. A fast gate that skipped the floor would reproduce 2026-08-24 exactly: both legs
#     dead with no TRX at 14 MB and 92 MB free. The terminal `exit $GATE_FAILED` and its distinct codes
#     are likewise shared -- a narrow gate that reports success on a red run is worse than the wide one
#     it replaced, which is note 3's whole subject.
#
#     ---- AND THE VERDICT SAYS WHICH SCOPE RAN.
#
#     `[GATE GREEN]` was unambiguous while there was one gate. With two it is not, and the cheap scope is
#     the one that will be read as the thorough one, because it is the one that gets run.
#
#  7z. TWO MODES, AND THE PAIRING IS THE POINT.
#
#     GATE_MODE=LEAN (DEFAULT) -- xUnit.MaxParallelThreads=4, memory floor 2048 MB. For THIS box.
#     GATE_MODE=FULL           -- no parallelism ceiling, memory floor 4096 MB. For a build box.
#
#     **This development machine runs LEAN**, because it hosts resident agent sessions alongside the suite
#     and cannot supply 4 GB with them running. A CI or build machine sets FULL explicitly.
#
#     LEAN BECAME THE DEFAULT ON 2026-08-25, and the reason is worth keeping. This note already said the
#     machine runs LEAN while the code defaulted to FULL, so the correct invocation was the one nobody
#     types -- a plain `bash scripts/gate.sh` aborted on a floor this box cannot meet. Documentation that
#     contradicts its own default is a trap with a paper trail.
#
#     MEASURED ON THIS BOX UNDER LEAN, 2026-08-25 -- use these to estimate, not the FULL figures:
#       Integration Debug    32 m 21 s      Integration Release  32 m 35 s
#       whole run, both configurations, seven other suites included:  69 minutes
#     The two Integration legs landed within FOURTEEN SECONDS of each other, so one measured leg is a
#     sound basis for estimating the second. The 45-55 minute figure quoted in note 6 is the FULL shape;
#     estimating a LEAN run from it produced a 90-110 minute projection that had to be revised down
#     mid-run.
#
#     **A floor without its matching ceiling is either theatre or a wall.** Memory scales with the number of
#     concurrent fixtures, so each floor is calibrated to what the suite actually needs UNDER its ceiling.
#     Raising the ceiling without raising the floor re-creates the 2026-08-24 starvation; lowering the floor
#     without lowering the ceiling just moves the wall.
#
#     Four threads is the lean shape rather than an arbitrary number: it is the sum/N arithmetic that made
#     the gate-economics work pay off, applied in the other direction. Expect roughly 45-55 minutes per
#     Integration leg -- still far under the 113-minute serial era that work replaced.
#
#     ---- NEVER EDIT THIS FILE WHILE IT IS RUNNING. Paid for 2026-08-25.
#
#     bash reads a script INCREMENTALLY as it executes, so inserting lines into a running gate shifts the
#     code underneath the interpreter. A LEAN run completed every leg green and then died with
#     "syntax error near unexpected token" on its own trailing lines, purely because this header grew by
#     nine lines mid-run. The results were valid and had to be recovered from the TRX files. Edit between
#     runs, or copy the script first.
#
#     TROUBLESHOOTING, PAID FOR ON 2026-08-24: before concluding a box cannot meet its floor, CHECK FOR
#     STALE BUILD NODES. Three `dotnet` MSBuild processes survived `MSBUILDDISABLENODEREUSE=1` after aborted
#     runs and held 372 MB between them; killing them moved the box from 1820 MB (below the LEAN floor) to
#     2390 MB (above it). Node reuse is disabled for the builds this script starts, not for whatever ran
#     before it.
#
#  7a. A MEMORY FLOOR IN THE PRECONDITIONS (4096 MB in FULL, 2048 MB in LEAN, before EACH leg).
#     2026-08-24: BOTH Integration legs exited 127 with NO TRX AT ALL -- not a partial one, no blame
#     sequence, no dump. The sampler showed the box down to 14 MB free during Debug and 92 MB during
#     Release. The instant-of-exit sample looked healthy (229 MB working set, 1362 MB free), which is
#     exactly what a memory kill looks like from outside: the sampler's last reading is taken after the
#     kill has already released the memory.
#     A busy box is a PRECONDITION FAILURE, not a flaky suite -- the same principle already applied to
#     foreign testhosts and orphan catalogs, now applied to memory. Checked before EACH leg rather than
#     once, because Debug's footprint bleeding into Release's start is what the two death timestamps show.
#
#     ---- THE FLOOR IS A PRE-LEG PRECONDITION, NOT A RUNNING MINIMUM. Read this before acting on a dip.
#
#     A HEALTHY LEAN INTEGRATION LEG ON THIS BOX HAS BEEN OBSERVED DOWN TO ~550 MB FREE MID-LEG, far
#     below the 2048 MB floor, with every leg green. FOUR MEASURED LEGS, both runs finishing clean:
#
#       2026-08-25 run 1  Debug     min_free 1510 MB   peak testhost ws 586 MB   free at leg start 3550 MB
#       2026-08-25 run 1  Release   min_free 1232 MB   peak testhost ws 641 MB   free at leg start 2985 MB
#       2026-08-25 run 2  Debug     min_free  551 MB   peak testhost ws 564 MB
#       2026-08-25 run 2  Release   min_free  580 MB   peak testhost ws 573 MB
#
#     ---- THIS IS AN OBSERVED RANGE, NOT A THRESHOLD. Do not read it as one.
#
#     The first two legs alone were once written up here as a 1,200-1,500 MB "healthy band". The next run
#     went to less than half that and finished green twice, so the band was never a property of the suite
#     -- it was two samples generalised into a rule. Treat 550 MB as the LOWEST SEEN SO FAR, not the floor
#     of safe operation, and expect a future run to go lower without that meaning anything.
#
#     The floor asks one question -- "is there room to START this leg" -- and the sampler answers a
#     different one. A mid-leg reading below the floor is the suite using the memory it was given.
#
#     ---- THE NUMBER IS NOT THE SIGNAL. THE SIGNATURE IS.
#
#     A memory death does not look like a low reading. It looks like this, from 2026-08-24: 14 MB and
#     92 MB free, and NO TRX AT ALL from either leg -- not a partial one, no blame sequence, no dump.
#     **If the TRX was written and the sampler ran to the end, memory was not the problem**, whatever
#     number the sampler bottomed out at. That is the check to make, and it is the only one.
#
#     TWO WRONG REACTIONS, BOTH ALREADY ATTEMPTED. Raising the floor because a running leg dipped under
#     it would refuse runs that would have passed. Aborting a healthy leg on a low reading would throw
#     away a completed configuration. On 2026-08-25 a mid-leg reading of 1946 MB was reported as a risk
#     to be acted on -- and both legs of that same run went lower still and finished green. The reading
#     that triggered the alarm was nearly four times the lowest healthy figure since measured.
#
#  7b. SQL SERVER IS CAPPED AT 4096 MB ON THIS INSTANCE.
#     Applied and persisted 2026-08-24 (`sp_configure 'max server memory (MB)', 4096`). It was UNBOUNDED
#     (2147483647) -- entitled to all 15 GB. It sat at ~600 MB at rest, which is irrelevant: the
#     ENTITLEMENT is what kills under load, and a dev-box instance entitled to all physical RAM is a
#     landmine every future suite steps on. This is an instance setting and survives restarts; it is
#     recorded here because nothing in the repository would otherwise say it had been done.
#
#  7. MSBUILDDISABLENODEREUSE=1.
#     This box is memory-bound (~15 GB, one local SQL Server) and the gate builds TWICE before running
#     two ~30-minute Integration legs, so Debug's worker nodes would sit resident through the whole
#     Release leg. Not a fix for anything -- the cheapest recoverable margin, for the price of a cold
#     build.
#
#  8. THE 120s SETUP COMMAND TIMEOUT (asserted by the suite, documented here).
#     Every integration connection string resolves through `IntegrationSqlEnvironment`, whose
#     `SetupCommandTimeoutSeconds = 120` was PROVEN honoured on both the raw ADO and EF paths by a
#     `Command Timeout=1` vs `WAITFOR 2s` probe with a control. If setup timeouts recur AT 120s the
#     next step is a stated `xunit.runner.json` parallelism ceiling, NOT a bigger number.
#
# --------------------------------------------------------------------------------------------------
# AND THE PROJECT LIST IS EXHAUSTIVE ON PURPOSE.
# --------------------------------------------------------------------------------------------------
#
# A gate that enumerates projects by name silently omits every project added after it was written.
# That is exactly how FP-008's `H9` site sat outside a nine-site inventory, and how `SSAS.Finance.Tests`
# -- 46 GL domain tests -- was invisible to the gate on the day it was created. WHEN YOU ADD A TEST
# PROJECT, ADD IT HERE.
#
# Usage:  bash scripts/gate.sh              # both configurations
#         GATE_LOGS=/some/dir bash scripts/gate.sh

# Repo root is derived from this script's own location, so the gate runs from anywhere.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

export MSBUILDDISABLENODEREUSE=1

# TestResults/ is already gitignored, so the gate cannot pollute the working tree.
LOGS="${GATE_LOGS:-$ROOT/TestResults/gate}"
mkdir -p "$LOGS"
GATE_FAILED=0

# ---- CONDITION 4's BASELINE. See note 7t. T-059.
#
# TRACKED, ON PURPOSE, AND THIS IS THE ONE TRACKED FILE THE GATE WRITES. Everything else it produces
# goes to gitignored TestResults. A baseline a person maintains is a baseline nobody maintains, so the
# instrument writes it and the coder commits it with the work -- which also puts the delta in the diff,
# where review sees a count change as a reviewable line rather than as a number in a log.
#
# **It can be hand-edited, and the defence is visibility rather than prevention.** Anyone can change a
# number in it; the change lands in the diff next to the code it excuses. That is the design, not a
# hole in it.
GATE_BASELINE_FILE="${GATE_BASELINE:-$ROOT/.claude/handoff/test-baseline.txt}"
rm -f "$LOGS/counts.txt"

reap_count () {
  sqlcmd -S localhost -E -C -h -1 -W -Q \
    "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name LIKE 'SSAS[_]%'" 2>/dev/null | head -1 | tr -d '[:space:]'
}

# ---- MODE. See note 7z in the header.
#
# LEAN IS THE DEFAULT BECAUSE THIS REPOSITORY LIVES ON THIS BOX. Note 7z has said "this development
# machine runs LEAN" since it was written, while the code defaulted to FULL -- so the documented-correct
# invocation was the one nobody types, and getting it wrong aborts on a 4096 MB floor this machine
# cannot meet with agent sessions resident. That happened on 2026-08-25 and cost a run.
#
# Defaulting to the safe mode rather than refusing to run without one: a gate that demands ceremony
# before it will start is a gate people stop running. A build box sets GATE_MODE=FULL once, in CI
# configuration, where an explicit value belongs.
GATE_MODE=${GATE_MODE:-LEAN}

# ---- SCOPE. See note 7y in the header. ORTHOGONAL TO MODE, AND THAT SEPARATION IS THE POINT.
#
# `GATE_MODE` answers "what can this box afford". `GATE_SCOPE` answers "what question is being asked".
# Folding them together would make the cheap answer unavailable on a build box and the thorough one
# unavailable here, which is the opposite of what each is for.
GATE_SCOPE=${GATE_SCOPE:-TASK}

# Recorded BEFORE the default is applied, because afterwards there is no way to tell "the caller asked
# for 0" from "nobody said". PHASE needs that distinction to warn about an instruction it will not obey.
GATE_INTEGRATION_SET=${GATE_INTEGRATION+set}
GATE_INTEGRATION=${GATE_INTEGRATION:-0}

# AN UNRECOGNISED VALUE ABORTS RATHER THAN FALLING THROUGH TO THE DEFAULT.
#
# `DEC-L-045`: a misspelled `GATE_SCOPE=PAHSE` that quietly ran the TASK scope would report a per-task
# result under the name of a phase-exit one, and the log would look right. A shell variable nobody reads
# is ignored in silence -- so both of these are read, and a value neither branch understands stops the
# run before it can be mistaken for the other.
case "$GATE_SCOPE" in
  TASK|PHASE) ;;
  *) echo "!!! ABORT: GATE_SCOPE='$GATE_SCOPE' is neither TASK nor PHASE."
     echo "!!! Not defaulting: a scope that fell through would report one gate under the other's name."
     exit 6;;
esac

case "$GATE_INTEGRATION" in
  0|1) ;;
  *) echo "!!! ABORT: GATE_INTEGRATION='$GATE_INTEGRATION' is neither 0 nor 1."
     echo "!!! Not defaulting: a typo here silently drops the one suite it exists to add."
     exit 6;;
esac

if [ "$GATE_SCOPE" = "PHASE" ]; then
  GATE_CONFIGS="Debug Release"
  GATE_SUITES="Architecture Platform HR API Finance Payroll Attendance Integration"
  SCOPE_NOTE="all eight suites, Debug and Release"
  # Stated rather than silently overridden: someone reaching for GATE_INTEGRATION=0 to shorten a phase
  # exit is asking for a thing this scope does not offer, and should be told so rather than obeyed.
  if [ "$GATE_INTEGRATION_SET" = "set" ]; then
    echo "--- note: GATE_SCOPE=PHASE always runs Integration; GATE_INTEGRATION is ignored here."
  fi
else
  # DEBUG ONLY, AND THE ASYMMETRY WITH PHASE IS DELIBERATE. Note 7 argues the Debug/Release pairing for
  # a phase exit and that argument is untouched; what this scope changes is HOW OFTEN the pair is bought,
  # never what the pair contains.
  GATE_CONFIGS="Debug"

  # ALL SEVEN NON-INTEGRATION SUITES, NOT A SUBSET CHOSEN FROM THE DIFF.
  #
  # T-055 specified "Architecture, Platform, HR, API, plus the changed module's suite". The three module
  # suites it would have selected between cost 23, 24 and 25 MILLISECONDS -- measured, in this file's own
  # note 7z lineage -- against a build that costs minutes. Selecting among them would buy back under a
  # tenth of a second in exchange for an inference over the diff, and an inference that is wrong in the
  # permissive direction is a suite nobody runs that looks exactly like a suite that passed.
  #
  # That is the same trade the task refuses for Integration, at a hundredth of the saving. Running all
  # seven costs nothing measurable and removes the inference entirely.
  GATE_SUITES="Architecture Platform HR API Finance Payroll Attendance"

  if [ "$GATE_INTEGRATION" = "1" ]; then
    GATE_SUITES="$GATE_SUITES Integration"
    SCOPE_NOTE="seven suites plus Integration (requested), Debug only"
  else
    SCOPE_NOTE="seven suites, NO Integration, Debug only"
  fi
fi

if [ "$GATE_MODE" = "LEAN" ]; then
  # The ceiling travels as a RunSettings argument rather than an xunit.runner.json, so the repository holds
  # no file asserting a parallelism policy that is true of only one machine.
  RUNSETTINGS_ARGS="-- xUnit.MaxParallelThreads=4"
  MEMORY_FLOOR_MB=${GATE_MEMORY_FLOOR_MB:-2048}
else
  RUNSETTINGS_ARGS=""
  MEMORY_FLOOR_MB=${GATE_MEMORY_FLOOR_MB:-4096}
fi

echo "########## GATE MODE: $GATE_MODE (floor ${MEMORY_FLOOR_MB} MB, ceiling: ${RUNSETTINGS_ARGS:-none})"
echo "########## GATE SCOPE: $GATE_SCOPE -- $SCOPE_NOTE"
echo "########## suites: $GATE_SUITES"
echo "########## configurations: $GATE_CONFIGS"

# ---- THE CONCURRENCY GUARD. See note 7x in the header. T-056.
#
# Two gates on this box destroy each other: `reap_to_zero` drops every SSAS[_]% catalog under every
# scope, and the only previous guard matched on `basename "$ROOT"`, so two differently-named worktrees
# each concluded the other's testhost belonged to someone unrelated.
#
# THE SHARED RESOURCE IS THE SQL SERVER INSTANCE, NOT THE REPOSITORY, so the lock lives on the instance.
GATE_LOCK_DB=tempdb
GATE_LOCK_RESOURCE=SSAS_GATE_EXCLUSIVE
GATE_HOLDER_PID=""
GATE_LOCK_STATE=""
GATE_ROOT_SQL=${ROOT//\'/\'\'}

# -d IS NOT OPTIONAL AND IS NOT COSMETIC. Application locks are DATABASE-SCOPED: a holder in master and
# a challenger in tempdb take the same lock name without seeing each other (measured, T-056). Omitting
# -d inherits the login's default database, so the guard would work by accident of one login's settings
# and silently stop working for another. tempdb rather than master: no persistent object in a system
# database, and an instance restart clears any stale label for free.
sqlq () { sqlcmd -S localhost -E -C -d "$GATE_LOCK_DB" -h -1 -W -Q "$1" 2>/dev/null; }

# The gate's own WINDOWS pid. `$$` is the MSYS pid and means nothing to Get-Process; column 4 of
# `ps -W` is the Windows one. Both are needed: the label records the Windows pid so any later
# challenger -- a different shell, a different worktree -- can ask the OS whether we are still alive.
GATE_WINPID=$(ps -W -p $$ 2>/dev/null | awk 'NR>1{print $4}' | head -1)

# ALIVE <start> | DEAD | UNKNOWN. Anything unparsed is UNKNOWN, never DEAD: mistaking a live gate for a
# dead one is the failure that reaps a running gate's catalogs.
gate_probe_pid () {
  local P="$1" OUT
  case "$P" in ''|*[!0-9]*) echo UNKNOWN; return;; esac
  OUT=$(powershell.exe -NoProfile -Command '$p = Get-Process -Id '"$P"' -ErrorAction SilentlyContinue; if ($null -eq $p) { "DEAD" } elseif ($null -eq $p.StartTime) { "UNKNOWN" } else { "ALIVE " + $p.StartTime.ToString("yyyy-MM-dd HH:mm:ss") }' 2>/dev/null | tr -d '\r' | head -1)
  case "$OUT" in
    DEAD|ALIVE\ ????-??-??\ ??:??:??) echo "$OUT";;
    *) echo UNKNOWN;;
  esac
}

GATE_STARTED=$(gate_probe_pid "$GATE_WINPID"); GATE_STARTED=${GATE_STARTED#ALIVE }

# THE LABEL IS NOT THE LOCK. It carries what the lock cannot: which worktree, which pid, started when.
# `sys.dm_exec_sessions` gives login_time and host_name for free but never the path, and its
# host_process_id is the HOLDER sqlcmd -- which is alive in precisely the orphan case, so it cannot
# answer the only question that matters. A stale label is harmless because the lock is the truth.
#
# IT IS WRITTEN BY THE HOLDER ITSELF, in the same batch that takes the lock, and that is not a
# stylistic choice. Writing it from the gate afterwards leaves a window in which the lock is held and
# the label still names the PREVIOUS holder -- and a challenger arriving in that window would probe a
# dead pid and reclaim a lock that a live gate had just legitimately taken. Same session, same batch,
# no window.
gate_label_read () {
  sqlq "SET NOCOUNT ON; IF OBJECT_ID('tempdb.dbo.ssas_gate_holder') IS NOT NULL
    SELECT CONCAT(gate_winpid,'|',gate_started,'|',gate_scope,'|',
      CONVERT(varchar(19), written_at, 120),'|',root_path) FROM dbo.ssas_gate_holder WHERE pin=1;" \
    | head -1 | sed 's/[[:space:]]*$//'
}

gate_label_pid () {
  sqlq "SET NOCOUNT ON; IF OBJECT_ID('tempdb.dbo.ssas_gate_holder') IS NOT NULL
    SELECT gate_winpid FROM dbo.ssas_gate_holder WHERE pin=1;" | head -1 | tr -d '[:space:]'
}

# A HOLDER MUST BE A LIVE PROCESS, NOT A CALL. A session-scoped applock taken by a one-shot `sqlcmd -Q`
# is released the instant sqlcmd exits -- and BOTH the take and the release return 0, so a gate built
# that way would announce an exclusive lock, guard nothing, and look correct at every step (measured,
# T-056). WAITFOR caps the holder's life at just under 24 h so a catastrophically orphaned holder is
# bounded rather than eternal; the takeover path below is what actually recovers one.
#
# IT SETS A GLOBAL AND ECHOES NOTHING, AND IT MUST STAY THAT WAY. Called as `$(gate_lock_acquire)` the
# whole thing runs in a subshell: the holder is started there, `GATE_HOLDER_PID` never reaches the
# parent, and the EXIT trap below then has nothing to kill -- so every abort path leaks a holder that
# blocks the next run. That is not hypothetical; it is what the first version of this function did, and
# it is the same hazard note 3 records for `reap_to_zero`. **Never wrap this call in `$( )` or a pipe.**
#
# ACQUISITION IS DETECTED FROM THE LABEL TABLE, NOT FROM THE HOLDER'S OUTPUT. sqlcmd's stdout is
# block-buffered when redirected to a file, so a sentinel printed before a 24-hour WAITFOR never
# reaches the file while the holder lives -- the first version polled for it and timed out on a lock it
# had successfully taken. The table is written by the same batch and is visible immediately.
gate_lock_acquire () {
  local i
  GATE_LOCK_STATE=UNKNOWN
  sqlcmd -S localhost -E -C -d "$GATE_LOCK_DB" -h -1 -W -Q \
    "SET NOCOUNT ON; DECLARE @r int;
     EXEC @r = sp_getapplock @Resource=N'$GATE_LOCK_RESOURCE', @LockMode=N'Exclusive',
       @LockOwner=N'Session', @LockTimeout=0;
     IF @r < 0 RETURN;
     IF OBJECT_ID('tempdb.dbo.ssas_gate_holder') IS NULL
       CREATE TABLE dbo.ssas_gate_holder (pin int NOT NULL PRIMARY KEY, root_path nvarchar(400) NOT NULL,
         gate_winpid int NOT NULL, gate_started nvarchar(19) NOT NULL, gate_scope nvarchar(10) NOT NULL,
         written_at datetime2(0) NOT NULL);
     DELETE FROM dbo.ssas_gate_holder;
     INSERT INTO dbo.ssas_gate_holder VALUES (1, N'$GATE_ROOT_SQL', $GATE_WINPID,
       N'$GATE_STARTED', N'$GATE_SCOPE', SYSDATETIME());
     WAITFOR DELAY '23:59:00';" > "$LOGS/gate-lock.out" 2>&1 &
  GATE_HOLDER_PID=$!
  # Bounded wait, paced by the server rather than by `sleep`, so the pause costs a round trip we are
  # already able to make and proves the instance is answering while we wait for it.
  for i in 1 2 3 4 5 6 7 8 9 10; do
    if [ "$(gate_label_pid)" = "$GATE_WINPID" ]; then GATE_LOCK_STATE=HELD; return; fi
    # The holder exits immediately when denied. A dead holder plus no label of ours is a refusal.
    if ! kill -0 "$GATE_HOLDER_PID" 2>/dev/null; then GATE_LOCK_STATE=DENIED; return; fi
    sqlq "WAITFOR DELAY '00:00:01';" >/dev/null 2>&1
  done
}

# Kill the session holding OUR resource and nothing else. Reached only after the holder's gate has been
# shown to be dead.
#
# THE STALE LABEL IS DELETED IN THE SAME BATCH. If it were left, a third gate arriving between this
# reclaim and the next acquisition would read the dead holder's row, probe the same dead pid, and
# reclaim the lock a moment after we legitimately took it. With the row gone that gate reads nothing,
# which it treats as undeterminable and refuses -- the safe direction.
gate_lock_steal () {
  sqlq "SET NOCOUNT ON; DECLARE @spid int, @s nvarchar(50);
    SELECT TOP 1 @spid = request_session_id FROM sys.dm_tran_locks
      WHERE resource_type='APPLICATION' AND resource_database_id = DB_ID()
        AND resource_description LIKE '%$GATE_LOCK_RESOURCE%';
    IF OBJECT_ID('tempdb.dbo.ssas_gate_holder') IS NOT NULL DELETE FROM dbo.ssas_gate_holder;
    IF @spid IS NOT NULL AND @spid <> @@SPID
    BEGIN SET @s = N'KILL ' + CONVERT(nvarchar(10), @spid); EXEC sp_executesql @s; END" >/dev/null 2>&1
}

# Covers normal exit AND every `exit` in this script -- including the precondition aborts. It does NOT
# cover an external kill: a trap does not fire while bash is inside a foreground child, and a gate is
# inside `dotnet test` for up to 33 minutes at a stretch (measured, T-056; both the TERM and the
# process-group INT case were tested and neither released). That is why the takeover path exists and
# why nothing here depends on this trap running.
gate_lock_release () {
  [ -n "$GATE_HOLDER_PID" ] || return 0
  kill "$GATE_HOLDER_PID" 2>/dev/null
  kill -9 "$GATE_HOLDER_PID" 2>/dev/null
  wait "$GATE_HOLDER_PID" 2>/dev/null
  sqlq "SET NOCOUNT ON; IF OBJECT_ID('tempdb.dbo.ssas_gate_holder') IS NOT NULL
    DELETE FROM dbo.ssas_gate_holder WHERE gate_winpid = ${GATE_WINPID:-0};" >/dev/null 2>&1
}
trap 'gate_lock_release' EXIT

# A gate that cannot establish its own identity must not take a lock, because every later challenger
# would read UNKNOWN and refuse -- stranding the instance behind a holder nobody can name.
if [ -z "$GATE_WINPID" ] || [ "$GATE_STARTED" = "UNKNOWN" ] || [ "$GATE_STARTED" = "DEAD" ]; then
  echo "!!! ABORT: cannot establish this gate's own Windows pid and start time."
  echo "!!! Not proceeding: a lock held by a gate that cannot be identified is one no later run can clear."
  exit 7
fi

gate_lock_acquire                       # sets GATE_LOCK_STATE; NEVER call this inside $( ) -- see above
case "$GATE_LOCK_STATE" in
  HELD) ;;
  DENIED)
    GATE_HOLDER_LABEL=$(gate_label_read)
    IFS='|' read -r H_PID H_START H_SCOPE H_WRITTEN H_ROOT <<< "$GATE_HOLDER_LABEL"
    H_LIVE=$(gate_probe_pid "$H_PID")
    echo "--- another gate holds the instance lock."
    echo "---   root       : ${H_ROOT:-<no label row>}"
    echo "---   gate pid   : ${H_PID:-?}   scope: ${H_SCOPE:-?}"
    echo "---   started    : ${H_START:-?}   (label written ${H_WRITTEN:-?})"
    # PID ALONE IS INSUFFICIENT: a reused pid reads as alive. The pid must still be occupied by a
    # process that started at the recorded time. The failure direction of getting this wrong is
    # REFUSING WHEN WE COULD HAVE PROCEEDED, which is the safe side, and it is not traded away.
    if [ "$H_LIVE" = "ALIVE $H_START" ]; then
      echo "!!! ABORT: that gate is RUNNING. Wait for it, or run in the tree that owns it."
      echo "!!! Not proceeding: reaping now would drop the catalogs it is using, mid-leg, silently."
      exit 7
    elif [ "$H_LIVE" = "DEAD" ] || [ "${H_LIVE%% *}" = "ALIVE" ]; then
      if [ "$H_LIVE" = "DEAD" ]; then
        echo "--- RECLAIMING: pid ${H_PID} is gone. The holder is an ORPHAN -- its sqlcmd outlived the gate."
      else
        echo "--- RECLAIMING: pid ${H_PID} is alive but started ${H_LIVE#ALIVE }, not ${H_START}."
        echo "---             The pid was REUSED; the gate that took this lock is dead."
      fi
      gate_lock_steal
      gate_lock_acquire
      if [ "$GATE_LOCK_STATE" != "HELD" ]; then
        echo "!!! ABORT: reclaim did not free the lock (state: $GATE_LOCK_STATE)."
        exit 7
      fi
      echo "--- RECLAIMED. Proceeding."
    else
      echo "!!! ABORT: cannot determine whether that gate is alive."
      echo "!!! Not proceeding: an unreadable holder is treated as a live one. Fail closed."
      exit 7
    fi;;
  *)
    echo "!!! ABORT: could not determine whether another gate is running."
    echo "!!! Not proceeding: a lock that is skipped when it cannot be read is the guard that was"
    echo "!!! already here -- and that guard is what T-056 exists to replace."
    exit 7;;
esac
# The label was written by the holder itself, in the acquiring batch. Nothing to write here.
echo "########## instance lock: HELD by pid $GATE_WINPID ($ROOT)"

# ---- AM I THE MERGED GATE? See note 7u in the header. T-058.
#
# WARNS AND PROCEEDS. IT NEVER REFUSES, AND THAT IS THE OPPOSITE OF THE LOCK ABOVE ON PURPOSE:
# fail-closed requires a signal you can trust, and this signal is SELF-ADMITTEDLY STALE -- a
# remote-tracking ref is only as good as the last fetch. Refusing on it would let a stale ref block
# every run on the box, which is the strand-the-instance failure the lock exists to avoid, relocated.
# The lock's evidence is authoritative (the DMV is the truth) and proceeding wrongly destroys a
# 69-minute run; here the failure is a WRONG GREEN, and a warning surfaces a wrong green.
#
# IT DOES NOT FETCH. Measured: 1338 ms online, 140 ms to fail on clean DNS -- but 22 SECONDS on a
# half-open network, which is thirty per cent of a 72-second gate, and a credential prompt would hang
# a gate forever with no output at all. `DEC-L-051` bought back 68 minutes; spending 22 s per run
# policing it would be self-defeating. Offline is a NORMAL condition. A colliding gate is not.
#
# ANCESTRY, NOT A HASH. A hash says "different" and conflates two states with opposite meanings: a
# tree that is BEHIND, and a tree that is DEVELOPING this file -- which is what every task that has
# ever edited the gate looks like. A check that cries wolf at the second gets disabled before it ever
# catches the first.
GATE_STALE_NOTE=""
gate_check_staleness () {
  local REF=${GATE_INTEGRATION_REF:-origin/ClaudeBranch} C BEHIND FETCH_FILE LAST_FETCH
  command -v git >/dev/null 2>&1 || { GATE_STALE_NOTE="unchecked: no git on PATH"; return; }
  git rev-parse --verify -q "$REF" >/dev/null 2>&1 || {
    GATE_STALE_NOTE="unchecked: '$REF' does not resolve in this repository"; return; }
  C=$(git rev-list -1 "$REF" -- scripts/gate.sh 2>/dev/null)
  [ -n "$C" ] || { GATE_STALE_NOTE="unchecked: no history for scripts/gate.sh on $REF"; return; }

  FETCH_FILE="$(git rev-parse --git-common-dir 2>/dev/null)/FETCH_HEAD"
  if [ -f "$FETCH_FILE" ]; then LAST_FETCH=$(date -r "$FETCH_FILE" '+%Y-%m-%d %H:%M' 2>/dev/null); fi
  LAST_FETCH=${LAST_FETCH:-never}

  if git merge-base --is-ancestor "$C" HEAD 2>/dev/null; then
    # Contains the merged commit. A local edit here is deliberate -- someone is working ON the gate.
    if [ "$(git hash-object -- scripts/gate.sh 2>/dev/null)" \
         != "$(git ls-tree HEAD -- scripts/gate.sh 2>/dev/null | awk '{print $3}')" ]; then
      GATE_STALE_NOTE="MODIFIED locally (not stale) -- this tree contains $REF's gate and edits it"
    fi
    return
  fi

  BEHIND=$(git rev-list --count "$C" --not HEAD -- scripts/gate.sh 2>/dev/null)
  GATE_STALE_NOTE="STALE -- ${BEHIND:-?} commit(s) behind $REF on scripts/gate.sh, as of your last fetch ($LAST_FETCH)"
}

gate_check_staleness
if [ -n "$GATE_STALE_NOTE" ]; then
  echo "########## !!! GATE SCRIPT: $GATE_STALE_NOTE"
fi

reap_to_zero () {
  local CFG="$1"

# 0. THE BOX MUST HAVE ROOM. Measured at the start of EVERY leg, not once for the run.
#
#    Aborts LOUDLY and distinctly: this is a precondition failure, and reporting it as a suite failure
#    would send someone hunting for a defect in the tests. See note 7a in the header for the incident.
  local FREE_MB
  FREE_MB=$(powershell.exe -NoProfile -Command     "[math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1KB,0)"     2>/dev/null | tr -d '[:space:]')
  FREE_MB=${FREE_MB:-0}

  echo "--- free physical memory before $CFG: ${FREE_MB} MB (floor ${MEMORY_FLOOR_MB} MB)"

  if [ "$FREE_MB" -lt "$MEMORY_FLOOR_MB" ]; then
    echo "!!! ABORT ($CFG): PRECONDITION FAILURE -- only ${FREE_MB} MB free, floor is ${MEMORY_FLOOR_MB} MB."
    echo "!!! This is NOT a suite failure. Quiet the box (editors, browsers) and run again."
    echo "!!! On 2026-08-24 both Integration legs died with no TRX at 14 MB and 92 MB free."
    exit 5
  fi

  # 1. NO TESTHOST OF OURS MAY BE RUNNING. One means a sibling suite is live and its catalogs are not
  #    orphans. Matched on the repo path: counting every testhost.exe on the machine over-reaches, and
  #    once aborted this gate because an UNRELATED repository had a suite running -- which cannot
  #    possibly hold an SSAS_ catalog. Killing another project's tests to satisfy our gate would be the
  #    wrong resolution; waiting on a process we do not own is not a resolution at all.
  local HOSTS
  HOSTS=$(powershell.exe -NoProfile -Command \
    "(Get-CimInstance Win32_Process | Where-Object { \$_.Name -eq 'testhost.exe' -and \$_.CommandLine -like '*$(basename "$ROOT")*' } | Measure-Object).Count" \
    2>/dev/null | tr -d '[:space:]')
  HOSTS=${HOSTS:-0}
  if [ "$HOSTS" -ne 0 ]; then
    echo "!!! ABORT ($CFG): $HOSTS testhost process(es) running -- a sibling suite is live."
    echo "!!! Reaping now would drop catalogs that are in use. Serialise the runs."
    exit 2
  fi

  # 2. THE MATCH IS SHOWN BEFORE ANYTHING IS DROPPED, so the log records what was destroyed and a
  #    protected database appearing here would be visible rather than silent.
  echo "--- catalogs present before $CFG:"
  sqlcmd -S localhost -E -C -h -1 -W -Q \
    "SET NOCOUNT ON; SELECT name FROM sys.databases WHERE name LIKE 'SSAS[_]%' ORDER BY name" 2>/dev/null

  # 3. PROTECTED NAMES MUST NOT MATCH. A production or platform catalog caught by the reserved test
  #    prefix is a naming defect, and the gate must not paper over it by dropping the row.
  local PROTECTED
  PROTECTED=$(sqlcmd -S localhost -E -C -h -1 -W -Q \
    "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name LIKE 'SSAS[_]%' AND (name NOT LIKE 'SSAS[_]%[_]%' OR name LIKE '%PROD%' OR name LIKE '%LIVE%')" \
    2>/dev/null | head -1 | tr -d '[:space:]')
  if [ "${PROTECTED:-0}" != "0" ]; then
    echo "!!! ABORT ($CFG): $PROTECTED catalog(s) match the test prefix but do not look like test catalogs."
    exit 3
  fi

  # 4. DROP. Per-database TRY/CATCH so one locked catalog cannot stop the rest; repeated because a
  #    single-user transition can lose a race with a connection that is still closing.
  local i
  for i in 1 2 3; do
    sqlcmd -S localhost -E -C -Q "SET NOCOUNT ON; DECLARE @s nvarchar(max)=N''; \
      SELECT @s = @s + N'BEGIN TRY ALTER DATABASE [' + name + N'] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; \
      DROP DATABASE [' + name + N']; END TRY BEGIN CATCH END CATCH;' \
      FROM sys.databases WHERE name LIKE 'SSAS[_]%'; EXEC sp_executesql @s;" >/dev/null 2>&1
  done

  # 5. ZERO IS VERIFIED, not hoped for. A non-zero count means something holds a catalog the guard will
  #    fail on anyway -- better to stop now than an hour into Integration.
  local LEFT
  LEFT=$(reap_count)
  echo "=== catalogs before $CFG (after reap): ${LEFT:-?}"
  if [ "${LEFT:-1}" != "0" ]; then
    echo "!!! ABORT ($CFG): reap left ${LEFT} catalog(s); CatalogLeakGuardTests would fail on them."
    exit 4
  fi
}

# THE TWO LISTS ARE THE ONLY THING SCOPE CHANGES. Everything below -- preconditions, reaping, build,
# blame collection, sampling, status capture, the greps and the verdict -- runs identically for both
# scopes, which is what makes PHASE the gate that existed before GATE_SCOPE was added rather than a
# reimplementation of it.
#
# Unquoted deliberately: these are space-separated word lists and must split.
for CFG in $GATE_CONFIGS; do
  echo "########## $CFG ##########"
  reap_to_zero "$CFG"

  # ---- THE BUILD IS A CONDITION, NOT A PREAMBLE. See note 7v. T-058.
  #
  # `--no-incremental` IS LOAD-BEARING AND IS NOT A PERFORMANCE CHOICE. MSBuild skips up-to-date
  # projects, so the compiler never re-runs and NEVER RE-EMITS THEIR WARNINGS: a planted CS0219
  # reported `1 Warning(s)` on the build that introduced it and `0 Warning(s)` on the very next build
  # with nothing changed (measured, T-058). Anyone who builds in an IDE before running the gate makes
  # the gate's build a no-op, and the gate then prints a clean bill over code that is not clean.
  # Measured cost of honesty: 16 s against 3 s, on a 72-second TASK gate.
  echo "=== BUILD ($CFG) ==="
  dotnet build SSAS.ERP.sln -c "$CFG" --nologo -v m --no-incremental > "$LOGS/build-$CFG.log" 2>&1
  BUILD_STATUS=$?
  grep -E "Warning\(s\)|Error\(s\)|Build succeeded|Build FAILED|error" "$LOGS/build-$CFG.log" | head -20

  # A FAILING BUILD MUST REACH THE VERDICT. Until T-058 this status was computed and discarded -- the
  # `grep` above ran next, so `$?` was grep's, and `GATE_FAILED=1` was set only inside the suite loop.
  # DEMONSTRATED 2026-08-27: a deliberate CS0029 produced `Build FAILED`, `1 Error(s)`, then 2752
  # PASSING TESTS against the previous build's assemblies, and `[GATE GREEN]` with exit 0. `DEC-L-007`
  # makes that exit code merge authority, so the gate was prepared to merge code that does not compile.
  # This is note 3's defect exactly, moved from the verdict to the build.
  if [ $BUILD_STATUS -ne 0 ]; then
    echo "!!! BUILD FAILED ($CFG) -- dotnet build exited $BUILD_STATUS."
    echo "!!! Suites SKIPPED for this configuration: --no-build would run the PREVIOUS build's"
    echo "!!! assemblies and, if those were green, report green for code that does not compile."
    GATE_FAILED=1
    continue
  fi

  # ---- `DEC-L-008` CONDITION 1, ENFORCED RATHER THAN PRINTED. T-058.
  #
  # "The build succeeds at zero warnings; a warning you introduced is a failure." Nothing enforced it:
  # `Directory.Build.props` sets TreatWarningsAsErrors false, and this script grepped the count and
  # printed it. Of the four merge conditions, 2 and 3 are suite runs and 4 is the count comparison --
  # condition 1 was held entirely by the coder choosing to honour it under a rule that merges on green
  # without review. It had never been violated, which is what made it read as a property of the
  # instrument rather than of a person.
  #
  # The suites still run: a warning does not invalidate a test result, and stopping here would trade
  # one true report for another. It is RED, and the run still says everything it knows.
  BUILD_WARNINGS=$(grep -m1 -oE '[0-9]+ Warning\(s\)' "$LOGS/build-$CFG.log" | awk '{print $1}')
  if [ "${BUILD_WARNINGS:-0}" != "0" ]; then
    echo "!!! WARNINGS ($CFG): $BUILD_WARNINGS -- DEC-L-008 condition 1 is zero. This gate is RED."
    grep -E ": warning [A-Z]+[0-9]+" "$LOGS/build-$CFG.log" | sort -u | head -20
    GATE_FAILED=1
  fi

  for P in $GATE_SUITES; do
    case $P in
      Architecture) F=tests/Architecture.Tests/SSAS.Architecture.Tests.csproj;;
      Platform)     F=tests/Platform.Tests/SSAS.Platform.Tests.csproj;;
      HR)           F=tests/HR.Tests/SSAS.HR.Tests.csproj;;
      API)          F=tests/API.Tests/SSAS.API.Tests.csproj;;
      Finance)      F=tests/Finance.Tests/SSAS.Finance.Tests.csproj;;
      Payroll)      F=tests/Payroll.Tests/SSAS.Payroll.Tests.csproj;;
      # FP-013. Added IN THE CREATING COMMIT rather than after a gate ran without it -- a suite the gate does
      # not name is a suite nobody runs, and its absence looks exactly like green.
      Attendance)   F=tests/Attendance.Tests/SSAS.Attendance.Tests.csproj;;
      Integration)  F=tests/Integration.Tests/SSAS.Integration.Tests.csproj;;
    esac

    BLAME=""
    SAMPLER=""
    if [ "$P" = "Integration" ]; then
      BLAME="--blame-crash"

      # ---- WORKING-SET SAMPLING, REPORTED AND NEVER ASSERTED.
      #
      # An allocation BUDGET on this suite was removed on 2026-08-21 for failing at 287MB under parallel
      # load without being able to discriminate a regression from a busy box. That was right about the
      # ASSERTION and it also discarded the OBSERVATION -- which is why the 2026-08-23 host deaths
      # arrived with no memory history to reason from. This can never fail a gate; it just means the
      # next death comes with a curve. Baselines from the 2026-08-24 measurement: the two heaviest
      # cutover classes peak at 213MB and 239MB alone and 261MB together, and the FULL suite under
      # sixteen parallel collections peaks at 509MB Debug / 555MB Release.
      if [ -f "$ROOT/scripts/sample-mem.ps1" ]; then
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$ROOT/scripts/sample-mem.ps1" \
          -OutFile "$LOGS/mem-Integration-$CFG.csv" -Tag "Integration-$CFG" -Match "$(basename "$ROOT")" &
        SAMPLER=$!
      fi
    fi

    echo "=== $P.Tests ($CFG) ==="

    # THE COMPLETE OUTPUT, UNFILTERED, ALWAYS. The greps below are a view over this file, never a
    # replacement for it.
    dotnet test "$F" -c "$CFG" --nologo -v q --no-build $BLAME \
      --logger "trx;LogFileName=$P-$CFG.trx" \
      --results-directory "$LOGS" \
      $RUNSETTINGS_ARGS \
      > "$LOGS/$P-$CFG.log" 2>&1
    STATUS=$?

    # A DEAD INSTRUMENT MUST NOT READ AS A NOT-APPLICABLE ONE. Until T-056 this block printed a
    # `--- memory:` line when the sampler worked and NOTHING when it died, so a leg with no memory line
    # was indistinguishable from a leg that never sampled -- and on 2026-08-27 both PHASE legs printed
    # two errors, set no failure flag, and reported green with nobody the wiser. Sampling still cannot
    # fail a gate; that argument is about a sampler that RUNS AND REPORTS, and this line is what makes
    # the difference visible. It costs nothing and asserts nothing.
    if [ "$P" = "Integration" ]; then
      if [ -n "$SAMPLER" ]; then
        kill "$SAMPLER" 2>/dev/null
        wait "$SAMPLER" 2>/dev/null
      fi
      if [ -f "$LOGS/mem-Integration-$CFG.csv" ]; then
        awk -F, 'NR>1 && $3 ~ /^[0-9]+$/ { if ($3+0 > pk) pk = $3+0; if (mn == 0 || $5+0 < mn) mn = $5+0; n++ }
                 END { if (n == 0) print "--- memory: SAMPLER PRODUCED NO SAMPLES -- the file exists and is empty of data."
                       else printf "--- memory: samples=%d peak_testhost_ws=%d MB min_free=%d MB\n", n, pk, mn }' \
          "$LOGS/mem-Integration-$CFG.csv"
      elif [ -n "$SAMPLER" ]; then
        echo "--- memory: SAMPLER DID NOT RUN -- launched but wrote no file. See DEC-L-056:"
        echo "---         MSYS_NO_PATHCONV=1 in the launching shell breaks powershell.exe -File."
      else
        echo "--- memory: SAMPLER NOT PRESENT -- scripts/sample-mem.ps1 is missing; no curve for this leg."
      fi
    fi

    if [ $STATUS -ne 0 ]; then
      echo "!!! $P.Tests ($CFG) EXITED $STATUS"
      GATE_FAILED=1
    fi

    grep -E "Passed!|Failed!|Test Run Aborted|host process crashed" "$LOGS/$P-$CFG.log" | head -6
    grep -E "\[FAIL\]|Error Message|Assert\." "$LOGS/$P-$CFG.log" | head -40
    grep -A 4 "The test running when the crash occurred" "$LOGS/$P-$CFG.log" | head -8

    # `Total:` rather than `Passed:` -- a skipped test still EXISTS, and condition 4 asks whether the
    # tests moved, not whether they ran. A suite the build failure skipped writes nothing here, which
    # is why the comparison below reports what it compared rather than assuming it saw everything.
    SUITE_TOTAL=$(grep -m1 -oE 'Total:[[:space:]]+[0-9]+' "$LOGS/$P-$CFG.log" 2>/dev/null | awk '{print $2}')
    echo "$P|$CFG|${SUITE_TOTAL:-?}" >> "$LOGS/counts.txt"
  done

  echo "=== catalogs after $CFG: $(reap_count)"
done

# ---- THE VERDICT NAMES ITS SCOPE. `DEC-L-045`.
#
# `[GATE GREEN]` alone was unambiguous while there was one gate. With two, a log that does not say what
# it covered is read a week later as if it covered everything -- and the cheap scope is the one that
# will be read that way, because it is the one that gets run.
# ---- CONDITION 4, PARTIALLY MECHANISED AND SAYING SO. See note 7t in the header. T-059.
#
# WHAT IS COMPARED: the per-suite totals this run produced, against the baseline this gate wrote on
# its last green run. WHAT IS NOT: whether the tests a task required exist, or whether the tests that
# moved cover what was written. **The gate cannot know what a task required.** Everything printed here
# is worded to claim only the first.
GATE_C4_NOTE=""
gate_condition_4 () {
  local REF=${GATE_INTEGRATION_REF:-origin/ClaudeBranch} BASE CHANGED MOVED=0 COMPARED=0 KEY OLD NEW
  [ -s "$LOGS/counts.txt" ] || { GATE_C4_NOTE="not compared: no suite totals were captured"; return; }
  command -v git >/dev/null 2>&1 || { GATE_C4_NOTE="not compared: no git on PATH"; return; }
  BASE=$(git merge-base HEAD "$REF" 2>/dev/null) || true
  [ -n "$BASE" ] || { GATE_C4_NOTE="not compared: no merge-base with '$REF'"; return; }

  # THE WORKING TREE IS INCLUDED, DELIBERATELY. `git diff <base> -- src/` with no second commit
  # compares base to the WORKING TREE, so uncommitted work counts. A merge-base-to-HEAD diff would see
  # nothing before the first commit and skip the check IN SILENCE -- permissive, which is the
  # `DEC-L-051` hole rather than a smaller version of it.
  #
  # NON-COMMENT LINES ONLY, AND THAT IS WHAT MAKES THIS SHIPPABLE. Measured over 200 merges: 45
  # touched src/, 44 added a new [Fact]/[Theory], and the ONE that did not was 19 added lines that
  # were entirely a comment block. With this filter that false positive disappears.
  CHANGED=$(git diff "$BASE" -- src/ 2>/dev/null \
    | grep -E '^[+-]' | grep -vE '^[+-]{3}' \
    | grep -vE '^[+-][[:space:]]*(//|\*|/\*|$)' | wc -l | tr -d '[:space:]')
  CHANGED=${CHANGED:-0}

  # UNTRACKED FILES ARE INVISIBLE TO `git diff`, AND A NEW SOURCE FILE IS THE COMMONEST NEW CODE.
  # This was found by planting one: the check reported "no non-comment change under src/" over a new
  # .cs file full of executable code. That is the permissive, silent-skip failure this check exists to
  # avoid, inside the check itself -- and reading the code would not have found it, because the line
  # that was wrong is the line that looks right.
  local UNTRACKED
  UNTRACKED=$(git ls-files --others --exclude-standard -- src/ 2>/dev/null \
    | while IFS= read -r f; do
        [ -f "$f" ] && grep -vE '^[[:space:]]*(//|\*|/\*|$)' "$f" 2>/dev/null
      done | wc -l | tr -d '[:space:]')
  CHANGED=$(( CHANGED + ${UNTRACKED:-0} ))

  if [ ! -f "$GATE_BASELINE_FILE" ]; then
    GATE_C4_NOTE="not compared: no baseline yet at ${GATE_BASELINE_FILE#$ROOT/} (it is written on the first green run)"
    return
  fi

  while IFS='|' read -r P C N; do
    [ -n "$P" ] || continue
    KEY="$P|$C"
    OLD=$(grep -m1 "^$KEY|" "$GATE_BASELINE_FILE" 2>/dev/null | cut -d'|' -f3)
    [ -n "$OLD" ] || continue          # a suite with no baseline row cannot be compared, only recorded
    COMPARED=$((COMPARED+1))
    NEW="$N"
    [ "$OLD" = "$NEW" ] || MOVED=$((MOVED+1))
  done < "$LOGS/counts.txt"

  if [ "$COMPARED" = "0" ]; then
    GATE_C4_NOTE="not compared: no suite in this run has a baseline row yet"
  elif [ "$CHANGED" = "0" ]; then
    GATE_C4_NOTE="ok: no non-comment change under src/; $COMPARED suite total(s) checked"
  elif [ "$MOVED" -gt 0 ]; then
    GATE_C4_NOTE="ok: $MOVED of $COMPARED suite total(s) moved, with $CHANGED non-comment line(s) changed under src/"
  else
    GATE_C4_NOTE="ATTENTION: totals unchanged in all $COMPARED suite(s) while $CHANGED non-comment line(s) under src/ changed -- condition 4 is yours to judge"
  fi
}
gate_condition_4
echo "--- condition 4: $GATE_C4_NOTE"

# ---- THE TRACEABILITY CHECK, AGAINST A BASELINE. See note 7s in the header. T-065.
#
# RED ON A RISE, NEVER ON THE STANDING COUNT. Eleven failures stand today and every one is
# work a package has already declared pending. **A gate permanently red on declared work is
# a gate switched off by the second week** -- which is how nine red packages went unremarked
# for weeks while `trace-check.py` was in neither this script nor `ci.yml`.
#
# IT RUNS IN BOTH SCOPES, and the reason is measured rather than assumed: **730 ms** over
# three runs, against a 72-second TASK gate. That is one per cent, and `DEC-L-051` bought
# back 68 minutes -- spending three-quarters of a second to close the last unwired instrument
# is not the trade that undoes it. Had it been material it would belong in PHASE only.
#
# The interpreter is `py`, not `python`: on this box `python` resolves through a per-user
# app-execution alias that can be switched off, and `py` is the launcher.
GATE_TRACE_BASELINE="${GATE_TRACE_BASELINE:-$ROOT/.claude/handoff/trace-baseline.txt}"
if command -v py >/dev/null 2>&1 && [ -f "$ROOT/scripts/trace-check.py" ]; then
  # `--baseline` returns 6 on a RISE and 0 otherwise. It deliberately does not return the
  # standing-count code, which is the whole point: see the note above `--baseline` in
  # trace-check.py.
  py "$ROOT/scripts/trace-check.py" --baseline "$GATE_TRACE_BASELINE" --update-baseline \
    > "$LOGS/trace-check.log" 2>&1
  TRACE_STATUS=$?
  sed -n '/TRACE BASELINE/,$p' "$LOGS/trace-check.log" | head -12
  if [ "$TRACE_STATUS" = "6" ]; then
    echo "!!! TRACEABILITY REGRESSION -- a package has MORE failures than its committed baseline."
    echo "!!! This gate is RED. Full report: $LOGS/trace-check.log"
    GATE_FAILED=1
  elif [ "$TRACE_STATUS" != "0" ]; then
    # Any other code means the checker did not answer the question. Reported, not failed:
    # a documentation checker that cannot start must not block code that compiles and passes.
    echo "--- trace-check: did not complete (exit $TRACE_STATUS) -- see $LOGS/trace-check.log"
  fi
else
  # ABSENCE IS STATED. A missing interpreter or script must not read as a clean run, which
  # is the same rule the memory sampler needed and did not have.
  echo "--- trace-check: NOT RUN -- no 'py' on PATH or scripts/trace-check.py missing."
fi

if [ $GATE_FAILED -ne 0 ]; then
  echo "[GATE RED -- $GATE_SCOPE scope: $SCOPE_NOTE]"
else
  echo "[GATE GREEN -- $GATE_SCOPE scope: $SCOPE_NOTE]"
fi
# IT WARNS AND NEVER FAILS THE GATE, AND THE REASON TRAVELS WITH THE LABEL. See note 7t: the only
# remedy for a wrong fire is to write a test you do not believe in, and a red answerable that way
# manufactures exactly the tests that make a suite worthless. A tier without its reason is a ranking.
case "$GATE_C4_NOTE" in
  ATTENTION:*) echo "[CONDITION 4: $GATE_C4_NOTE]";;
  "not compared:"*|not\ compared*) echo "[CONDITION 4: $GATE_C4_NOTE]";;
esac
# REPEATED AT THE VERDICT, NOT ONLY AT THE START. A warning printed at second 3 of a 4095-second run
# is not a warning anyone reads; the verdict is the one line guaranteed to be looked at. That is the
# argument that put the scope into `[GATE GREEN -- <scope>]`, and it applies here unchanged.
if [ -n "$GATE_STALE_NOTE" ]; then
  echo "[GATE SCRIPT: $GATE_STALE_NOTE]"
fi
# ---- THE BASELINE IS WRITTEN BY THE INSTRUMENT, ONCE, HERE. See note 7t.
#
# ONCE, AFTER THE LAST CONFIGURATION, AFTER THE VERDICT IS COMPUTED -- not "after the suites". Under
# PHASE that phrase is ambiguous: Debug's suites finish and then Release runs, and a write between
# them dirties the tree MID-RUN while Release is reading it. That is `DEC-L-013` with the gate doing
# it to itself.
#
# A RED RUN WRITES NOTHING, AND SAYS SO. A red run that silently leaves the baseline alone is
# indistinguishable from one that updated it -- the same absence problem as the sampler, one layer
# down. Nothing partial either: a half-written baseline is a wrong baseline that looks maintained.
if [ $GATE_FAILED -ne 0 ]; then
  echo "--- baseline: NOT updated (gate is red). ${GATE_BASELINE_FILE#$ROOT/} still holds the last green run's totals."
elif [ ! -s "$LOGS/counts.txt" ]; then
  echo "--- baseline: NOT updated -- no suite totals were captured this run."
else
  mkdir -p "$(dirname "$GATE_BASELINE_FILE")"
  # Rows this run did not produce are CARRIED FORWARD, not dropped. A TASK run covers seven suites in
  # Debug; dropping the rest would report Integration as having vanished on the next PHASE.
  {
    echo "# Written by scripts/gate.sh on a green run. Do not hand-edit -- see note 7t."
    echo "# suite|configuration|total"
    {
      if [ -f "$GATE_BASELINE_FILE" ]; then
        grep -v '^#' "$GATE_BASELINE_FILE" 2>/dev/null | while IFS='|' read -r P C N; do
          [ -n "$P" ] || continue
          grep -q "^$P|$C|" "$LOGS/counts.txt" || echo "$P|$C|$N"
        done
      fi
      cat "$LOGS/counts.txt"
    } | sort -u
  } > "$GATE_BASELINE_FILE.tmp" && mv "$GATE_BASELINE_FILE.tmp" "$GATE_BASELINE_FILE"
  echo "--- baseline: updated ${GATE_BASELINE_FILE#$ROOT/} from this green run ($(grep -vc '^#' "$GATE_BASELINE_FILE") row(s)). COMMIT IT WITH YOUR WORK."
fi

echo "[GATE COMPLETE -- $GATE_SCOPE scope: $SCOPE_NOTE -- full logs and TRX in $LOGS]"

# ---- AND THE VERDICT REACHES `$?`. Do not remove this line. Paid for 2026-08-25.
#
# WITHOUT IT A RED GATE EXITED 0. `GATE_FAILED=1` was set, `[GATE RED]` was printed, and then the script
# fell off its own end -- and a bash script that ends without `exit` returns the status of its last
# command, which was the `echo` above. Success.
#
# That is the failure this file's note 3 describes, arrived at from the other direction: not a pipe
# swallowing the status, but no status ever being set. And it landed on the one path that matters most,
# because `DEC-L-007` makes a green gate the merge authority for code -- so a gate whose SUITES FAILED
# was reporting success to anything reading `$?`.
#
# 1 is deliberate and distinct from the precondition codes in note 3: 2, 3, 4 and 5 mean the run never
# started, 1 means it ran and something failed. A caller can tell "the box was not ready" from "the code
# is broken", which is the distinction the whole precondition apparatus exists to preserve.
exit $GATE_FAILED
