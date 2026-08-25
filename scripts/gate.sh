#!/usr/bin/env bash
set -u
# ==================================================================================================
# THE PHASE-EXIT GATE. FULL SUITE, BOTH CONFIGURATIONS.
# ==================================================================================================
#
# The gate is the full Integration suite PLUS EVERY OTHER TEST PROJECT IN FULL, in Debug AND Release
# (2026-08-21 ruling). Debug-clean is not evidence of Release-clean: the analyzer sets differ, and the
# first Release run exposed CA1826 warnings and an allocation assertion that had never worked.
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

for CFG in Debug Release; do
  echo "########## $CFG ##########"
  reap_to_zero "$CFG"

  echo "=== BUILD ($CFG) ==="
  dotnet build SSAS.ERP.sln -c "$CFG" --nologo -v m > "$LOGS/build-$CFG.log" 2>&1
  grep -E "Warning\(s\)|Error\(s\)|Build succeeded|Build FAILED|error" "$LOGS/build-$CFG.log" | head -20

  for P in Architecture Platform HR API Finance Payroll Attendance Integration; do
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

    if [ -n "$SAMPLER" ]; then
      kill "$SAMPLER" 2>/dev/null
      wait "$SAMPLER" 2>/dev/null
      awk -F, 'NR>1 && $3 ~ /^[0-9]+$/ { if ($3+0 > pk) pk = $3+0; if (mn == 0 || $5+0 < mn) mn = $5+0; n++ }
               END { printf "--- memory: samples=%d peak_testhost_ws=%d MB min_free=%d MB\n", n, pk, mn }' \
        "$LOGS/mem-Integration-$CFG.csv"
    fi

    if [ $STATUS -ne 0 ]; then
      echo "!!! $P.Tests ($CFG) EXITED $STATUS"
      GATE_FAILED=1
    fi

    grep -E "Passed!|Failed!|Test Run Aborted|host process crashed" "$LOGS/$P-$CFG.log" | head -6
    grep -E "\[FAIL\]|Error Message|Assert\." "$LOGS/$P-$CFG.log" | head -40
    grep -A 4 "The test running when the crash occurred" "$LOGS/$P-$CFG.log" | head -8
  done

  echo "=== catalogs after $CFG: $(reap_count)"
done

if [ $GATE_FAILED -ne 0 ]; then echo "[GATE RED]"; else echo "[GATE GREEN]"; fi
echo "[GATE COMPLETE -- full logs and TRX in $LOGS]"

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
