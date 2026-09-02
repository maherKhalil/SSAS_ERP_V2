#!/bin/sh
# ==================================================================================================
# THE HANDOFF FINGERPRINT, DERIVED (274)
# ==================================================================================================
#
# Writes .claude/handoff/session/fingerprint.txt from git and from the gate's own baseline. Run by
# .git/hooks/post-commit, so it is current after every commit without anyone remembering.
#
# ---- WHY THIS EXISTS: A HAND-WRITTEN COPY OF A DERIVABLE FACT.
#
# The session handoff files carried "last gate: N passed" and "last commit: <sha>" as PROSE, typed
# by hand. Their whole purpose is identity -- a window proves it is the one that did the work by
# naming its last gate and commit -- so a stale line there is worse than an absent one: it is a
# confident wrong answer to the one question the file exists to settle.
#
# On 2026-09-02 the coder file claimed `267496f` and 3257 passed while HEAD was `b029706` and the
# gate had reported 3266. Both were nine commits and one gate out of date, and nothing could have
# noticed, because nothing compares prose to the tree.
#
# ---- WHAT IS DERIVED AND WHAT IS DELIBERATELY NOT.
#
# HEAD's sha, subject and date come from git. The gate line does NOT compute a total: the TASK scope
# is seven of the sixteen baseline rows, and picking those seven here would be a HAND-WRITTEN
# POPULATION inside the very script written to stop hand-writing derivable things -- the same defect
# one level down. So this reports the baseline file's own timestamp and points at it. The gate owns
# that number; this names where it lives.
#
# The working tree's clean/dirty state is deliberately absent. That observation decays in seconds and
# has no business in a durable artefact.
#
# ---- IT DESCRIBES THE BRANCH, NOT A SESSION.
#
# Both windows commit to this repository, so HEAD is whoever committed last. This file therefore
# answers "what is HEAD" -- a fact anyone can check -- and never claims which window produced it.
# Identity is established by a session NAMING these values in conversation, which a file cannot do
# on its behalf.
set -eu

root=$(git rev-parse --show-toplevel)
out="$root/.claude/handoff/session/fingerprint.txt"
baseline="$root/.claude/handoff/test-baseline.txt"

mkdir -p "$root/.claude/handoff/session"

{
  echo "# DERIVED by scripts/handoff-fingerprint.sh from .git/hooks/post-commit. Do not hand-edit:"
  echo "# the next commit overwrites it. Prose that is NOT derivable belongs in coder.txt."
  echo
  echo "generated       $(date -u '+%Y-%m-%dT%H:%M:%SZ')"
  echo "branch          $(git rev-parse --abbrev-ref HEAD)"
  echo "head            $(git log -1 --format='%h  %s')"
  echo "head authored   $(git log -1 --format='%aI  %an')"
  echo "ahead of origin $(git rev-list --count "@{upstream}..HEAD" 2>/dev/null || echo 'no upstream')"

  if [ -f "$baseline" ]; then
    echo "gate baseline   $baseline"
    echo "  last written  $(date -u -r "$baseline" '+%Y-%m-%dT%H:%M:%SZ')"
    echo "  suite rows    $(grep -c '^[A-Za-z]*|' "$baseline")"
    echo
    echo "# The per-suite totals are IN that file, written by the gate on a green run. They are not"
    echo "# copied here: a copy is the thing this script exists to stop."
  else
    echo "gate baseline   ABSENT -- no green gate has written one at $baseline"
  fi
} > "$out"
