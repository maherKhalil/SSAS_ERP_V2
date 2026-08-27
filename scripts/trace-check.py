#!/usr/bin/env python3
"""
trace-check — the mechanical orphan / contiguity / coverage check over a feature package.

WHY THIS EXISTS
---------------
Every traceability matrix in docs/17-features/ claims its counts were "derived by a script
over the package's own files, not typed from memory". No such script was in the repository.
FP-012 stated its entity count wrong four times, once inside the very section warning about
miscounts. This is that script.

It implements the four rules the matrices describe, plus two traps that have already cost
this project real time:

  1. ORPHANS      — an identifier cited somewhere in the package but never defined in its
                    home file.
  2. CONTIGUITY   — each identifier space runs from 0001 with no gaps.
  3. REQ -> AC    — every requirement carries at least one acceptance criterion.
  4. AC  -> TS    — every acceptance criterion carries at least one test scenario.
  5. RANGES       — `AC-PAY-0006`-`0008` reads as three identifiers to a human and one to a
                    string search. Ten identifiers hid behind en-dash ranges in FP-012.
                    A traceability matrix has to be machine-checkable, not merely legible.
  6. UNRULED OD   — OD- identifiers with no ruling on record. Nothing in the OD register has
                    a default, and the build prompt must not be written while one is open.

An em-dash in a matrix cell is a DECLARED gap, not a defect: the matrices are explicit that
inventing a parent to make the table tidier would make it mean less. Declared gaps are
reported separately from failures and never turn the check red.

CHECK 7 — THE MASTER REGISTER, ONE LEVEL ABOVE THE PACKAGES
-----------------------------------------------------------
Checks 1-6 look INSIDE `docs/17-features/`. That is where the identifiers a package owns
live, and for two years it was the only place anyone looked.

It is not where the expensive failures happened. Both instances found so far are one level
UP, in the master specification that outranks every package:

  * `CON-0001` — "The application shall operate as a subscription-based SaaS platform."
    A mandatory constraint, in a file whose preamble says constraints "shall not be violated
    without an approved ADR". It appears exactly once in the whole repository: in its own
    definition. No requirement, no acceptance criterion, no test, no line of code. The
    product's defining commercial constraint was invisible to a checker that only reads
    packages, because a package that never mentions it looks complete.

  * `BR-ATT-0001`…`0012` and `BR-PAY-0001`…`0013` — drafted inside FP-013 and FP-012 and
    NEVER PROMOTED to the master `Business-Rules.md`, which still lists Attendance and
    Payroll under "will be added in future releases" while both modules ship. Checks 1-6
    pass both packages cleanly, and correctly: the rules ARE defined in their home files.
    The defect is that the master register does not know they exist.

  7. MASTER REGISTER — three findings, reported ABOVE the package line and never mixed into
                    a package's result:

       ORPHAN     a `CON-####` or `BR-###-####` defined in the master specification and
                  cited NOWHERE else in the repository.
       UNTRACED   cited somewhere, but never in the same block as a `REQ-` identifier. It is
                  mentioned; nothing implements it. `BR-PLT-0008` is the type specimen — it
                  is named once in `Tenant-Management.md` and has no requirement anywhere.
       UNPROMOTED a `BR-<MODULE>` space defined inside a feature package where the master
                  `Business-Rules.md` carries no rule for that module at all.

Co-occurrence within a block is the tracing signal, the same signal checks 3 and 4 use for
the chain table. Where the check cannot decide, it reports: a checker that under-reports is
worse than one that occasionally asks a human to look, because it is trusted at exactly the
moment it is wrong.

**A finding here is not a broken package**, which is why it is printed in its own section
with its own verdict line and is excluded from the `[TRACE RED] n of m package(s)` tally.

USAGE
    python3 scripts/trace-check.py                          # every package
    python3 scripts/trace-check.py FP-013-attendance         # one package
    python3 scripts/trace-check.py --json                    # machine-readable
    python3 scripts/trace-check.py --features-dir docs/17-features
    python3 scripts/trace-check.py --no-master               # checks 1-6 only

EXIT CODES
    0  all checked packages clean
    1  at least one package has a failure
    2  usage / no packages found
    3  every package clean, but the MASTER REGISTER has findings (check 7)
    4  packages and master register clean, but the two DECISION REGISTERS disagree (check 8)

    Codes 0-2 keep the meaning they have always had. A caller testing `== 1` is unaffected
    by checks 7 and 8; a caller testing `!= 0` sees them. That is the intended split — the
    master register and the decision registers are different questions from package health
    and deserve different answers.

    They are ORDERED, not combined, because an exit code carries one number: 1 outranks 3
    outranks 4. So a run returning 1 or 3 may ALSO have check 8 findings that the exit code
    does not show. Read the section, or `--json`, if that is the question being asked.

CHECK 8 — THE TWO DECISION REGISTERS AGREE
-------------------------------------------
Check 6 asks whether every `OD-` carries a ruling and reads the answer from
`decisions-ratified.md` / `decisions-approved.md`. **Nothing verified that the other register
agreed.** FP-014 was ratified — check 6 green, `0 unruled` — while `decisions-open.md` still
introduced all seventeen decisions as open questions and still said each one blocks the build
prompt. The tool was green because it consulted the file that knew; the file a person opens
was the one that did not.

  8. REGISTER      an `OD-` ruled in the ruling register and still presented as an open
                   question, by its own heading, in `decisions-open.md`.

Reported in its own section with its own verdict, like check 7, and excluded from the
`[TRACE RED]` tally: a divergence is not a broken package. Both files are internally coherent
and the defect is that they disagree with each other.

**A GREEN here means the two registers agree — NOT that every decision is recorded
somewhere.** A ruling that lives only in a note or a message is invisible to this check and to
check 6 alike, which is exactly what `DEC-L-034` was until T-036 moved it.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

# --------------------------------------------------------------------------------------
# Identifier model
# --------------------------------------------------------------------------------------

# Where each space is DEFINED. An identifier found anywhere else is a citation.
# DEC and OD share the decisions register, whose filename is not consistent across
# packages: decisions-approved.md (FP-001..009, 011, 012), decisions-open.md (FP-010, 013),
# decisions-ratified.md (FP-013). All three are accepted as home files.
HOME_FILES: dict[str, tuple[str, ...]] = {
    "REQ": ("requirements.md",),
    "BR": ("business-rules.md",),
    "AC": ("acceptance-criteria.md",),
    "TS": ("test-scenarios.md",),
    "DEC": ("decisions-approved.md", "decisions-open.md", "decisions-ratified.md"),
    "OD": ("decisions-approved.md", "decisions-open.md", "decisions-ratified.md"),
}

# --------------------------------------------------------------------------------------
# PER-PACKAGE CONVENTIONS (T-060)
#
# THIS REPOSITORY HAS TWO IDENTIFIER CONVENTIONS AND BOTH ARE LIVE. `FP-001..010` define
# their business rules as `BRULE-XX-NNNN` in `business-rules.md` and carry a SEPARATE
# `BR-XX-NNNN` space in `requirements.md`. `FP-011..014` define `BR-XX-NNNN` in
# `business-rules.md` and have no `BRULE-` at all.
#
# ---- THEY ARE DIFFERENT RULES, NOT A SPELLING CHANGE. ESTABLISHED BY COMPARING TITLES:
#
#     BRULE-IAM-0001  Tenant ownership is immutable      business-rules.md
#     BR-IAM-0001     Tenant isolation                   requirements.md
#
# Nine for nine in FP-001, twelve for twelve in FP-006. **A `BRULE- -> BR-` rename would
# have collapsed nine rules onto nine others and turned this checker GREEN by destroying
# content** -- and `BRULE-` is cited in 86 files under `src/` and `tests/`, so it is a
# live, load-bearing space rather than legacy residue.
#
# ---- WHY THE INSTRUMENT CHANGED AND NOT THE CORPUS, WHICH IS THE OPPOSITE OF THE GATE.
#
# `scripts/gate.sh` was WRONG ABOUT A FACT -- a failing build is a failing build under any
# convention -- so the instrument had to change to match reality. Here **the corpus is
# coherent and the checker did not know one of its conventions.** Those get opposite
# treatment, and arguing both is not inconsistency.
#
# ---- THE DIVERGENCE IS DECLARED AND LOUD, NEVER ABSORBED.
#
# A checker that quietly accepted both would become a RECORD of the inconsistency rather
# than a force against it, and someone reading `BR-` in FP-001 and `BR-` in FP-014 would be
# reading two different things with no warning. So:
#
#   * the convention is DECLARED PER PACKAGE, in the table below -- never inferred, and
#     never a fallback that searches the other home when a lookup misses. **A fallback that
#     quietly finds the identifier elsewhere is the silent-permissive failure** removed from
#     four instruments on 2026-08-27.
#   * every run PRINTS each package's convention, whether or not anything fails.
#   * a package in NO table entry FAILS. Unknown must not mean tolerated.
#   * a package that does not CONFORM to what it declares FAILS.
CONVENTIONS: dict[str, dict[str, tuple[str, ...]]] = {
    # FP-011 onward. `BR-` is the business rule and lives in business-rules.md.
    "modern": {
        "REQ": ("requirements.md",),
        "BR": ("business-rules.md",),
        "BRULE": (),
        "AC": ("acceptance-criteria.md",),
        "TS": ("test-scenarios.md",),
        "DEC": ("decisions-approved.md", "decisions-open.md", "decisions-ratified.md"),
        "OD": ("decisions-approved.md", "decisions-open.md", "decisions-ratified.md"),
    },
    # FP-001..010. `BRULE-` is the business rule; `BR-` is a separate space in requirements.
    "legacy": {
        "REQ": ("requirements.md",),
        "BR": ("requirements.md",),
        "BRULE": ("business-rules.md",),
        "AC": ("acceptance-criteria.md",),
        "TS": ("test-scenarios.md",),
        "DEC": ("decisions-approved.md", "decisions-open.md", "decisions-ratified.md"),
        "OD": ("decisions-approved.md", "decisions-open.md", "decisions-ratified.md"),
    },
}

PACKAGE_CONVENTION: dict[str, str] = {
    "FP-001-identity-access": "legacy",
    "FP-002-authentication-token-lifecycle": "legacy",
    "FP-003-tenant-lifecycle": "legacy",
    "FP-004-localization": "legacy",
    "FP-005-company-legal-entity": "legacy",
    "FP-006-hr-employee": "legacy",
    "FP-007-hr-department": "legacy",
    "FP-008-hr-position": "legacy",
    "FP-009-hr-employee-import-export": "legacy",
    "FP-010-hr-employee-documents": "legacy",
    "FP-011-gl-foundation": "modern",
    "FP-012-payroll": "modern",
    "FP-013-attendance": "modern",
    "FP-014-subscription": "modern",
}

# BRULE must precede BR in the alternation. `BR-` cannot match inside `BRULE-` because the
# hyphen is required, but ordering longest-first removes the question rather than answering
# it -- and the next space added may not be so safely distinguishable.
HOME_FILES["BRULE"] = ("business-rules.md",)
SPACES = ("REQ", "BRULE", "BR", "AC", "TS", "DEC", "OD")


def convention_of(package: str) -> str | None:
    return PACKAGE_CONVENTION.get(package)


def homes_for(package: str) -> dict[str, tuple[str, ...]]:
    """The per-space home files this package's declared convention specifies."""
    name = convention_of(package)
    if name is None:
        # Undeclared packages FAIL rather than falling back. The caller raises that; this
        # returns the union so the run can still produce a useful inventory alongside it.
        return {s: tuple(sorted(set(CONVENTIONS["modern"].get(s, ()))
                                | set(CONVENTIONS["legacy"].get(s, ())))) for s in SPACES}
    return CONVENTIONS[name]

ID_RE = re.compile(r"\b(" + "|".join(SPACES) + r")-([A-Z]{2,4})-(\d{4})\b")

# `AC-PAY-0006`-`0008` in any dash flavour, with or without backticks.
RANGE_RE = re.compile(
    r"(" + "|".join(SPACES) + r")-([A-Z]{2,4})-(\d{4})`?\s*[–—-]\s*`?(\d{4})\b"
)

# Conditional passages that step 0 is required to drive to zero.
DEPENDENT_RE = re.compile(r"-dependent\b")

DASH_ONLY = re.compile(r"^[\s–—-]*$")

# ---- check 7: the master register -----------------------------------------------------
#
# `CON-####` carries NO module token, unlike every space in SPACES, so it needs its own
# pattern rather than a widened ID_RE. Widening ID_RE would change what checks 1-6 see.
CON_RE = re.compile(r"\bCON-(\d{4})\b")

# A definition in the master files is a heading. Both registers use `## <ID>` today; `###`
# is tolerated so a nesting change does not silently empty the check — the same reason
# parse_chain identifies chain rows by shape rather than by heading text.
MASTER_CON_DEF_RE = re.compile(r"^#{2,3}\s+(CON-\d{4})\s*$", re.M)
MASTER_BR_DEF_RE = re.compile(r"^#{2,3}\s+(BR-[A-Z]{2,4}-\d{4})\s*$", re.M)

# A requirement — any module. Used as the tracing signal for check 7.
REQ_ANY_RE = re.compile(r"\bREQ-[A-Z]{2,4}-\d{4}\b")

MASTER_SPEC_DIR = ("docs", "00-Master-Product-Specification")
MASTER_CON_FILE = MASTER_SPEC_DIR + ("Requirement-Catalog", "Constraints.md")
MASTER_BR_FILE = MASTER_SPEC_DIR + ("Business-Rules.md",)

# Where a citation may live. Deliberately wider than docs/: a constraint satisfied by a
# comment in a `.cs` file IS traced, and refusing to look there would manufacture orphans.
CITATION_SUFFIXES = frozenset(
    {".md", ".cs", ".yml", ".yaml", ".sql", ".json", ".csproj", ".props", ".targets"}
)
CITATION_ROOTS = ("docs", "src", "tests", "tools", "scripts", ".github")
SKIP_PARTS = frozenset({"obj", "bin", ".git", "node_modules", "TestResults"})


class Ident:
    __slots__ = ("space", "module", "number")

    def __init__(self, space: str, module: str, number: str):
        self.space, self.module, self.number = space, module, number

    @property
    def key(self) -> str:
        return f"{self.space}-{self.module}-{self.number}"

    def __hash__(self):
        return hash(self.key)

    def __eq__(self, other):
        return isinstance(other, Ident) and self.key == other.key


def scan_ids(text: str) -> set[Ident]:
    return {Ident(*m.groups()) for m in ID_RE.finditer(text)}


# --------------------------------------------------------------------------------------
# Matrix chain parsing
# --------------------------------------------------------------------------------------

def split_blocks(text: str) -> list[str]:
    """
    The matrix carries coverage in two shapes, and a checker that reads only one of them
    invents failures. The chain is a pipe table, one requirement per row; the
    'criteria carrying no requirement' section is PROSE listing criteria and the tests they
    map to. Both are legitimate coverage.

    A block is therefore either a single table row or a single blank-line-delimited
    paragraph. Co-occurrence within a block is the coverage signal.
    """
    blocks: list[str] = []
    buf: list[str] = []
    for raw in text.splitlines():
        line = raw.strip()
        if line.startswith("|"):
            if buf:
                blocks.append("\n".join(buf))
                buf = []
            blocks.append(line)
        elif not line:
            if buf:
                blocks.append("\n".join(buf))
                buf = []
        else:
            buf.append(line)
    if buf:
        blocks.append("\n".join(buf))
    return blocks


def parse_chain(matrix_text: str) -> list[dict]:
    """
    Rows of the 'chain' table: Requirement | Rules | Criteria | Tests | Decisions.

    Identified by shape rather than by heading, so a renamed section does not silently
    empty the check: any pipe-table row whose first cell carries a REQ- identifier is a
    chain row.
    """
    rows: list[dict] = []
    for raw in matrix_text.splitlines():
        line = raw.strip()
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if len(cells) < 4:
            continue
        req_in_first = [i for i in scan_ids(cells[0]) if i.space == "REQ"]
        if not req_in_first:
            continue
        rows.append(
            {
                "requirements": sorted(i.key for i in req_in_first),
                "rules": cells[1],
                "criteria": cells[2],
                "tests": cells[3],
                "decisions": cells[4] if len(cells) > 4 else "",
                "line": raw.rstrip(),
            }
        )
    return rows


def cell_ids(cell: str, space: str) -> list[str]:
    return sorted({i.key for i in scan_ids(cell) if i.space == space})


def is_declared_gap(cell: str) -> bool:
    """An em/en dash alone is a deliberate 'no link exists', not an omission."""
    return bool(DASH_ONLY.match(cell))


# --------------------------------------------------------------------------------------
# The check
# --------------------------------------------------------------------------------------

def build_global_index(features: Path, repo_root: Path) -> dict[str, set[str]]:
    """
    Identifiers defined ANYWHERE in the repository's registers.

    A package legitimately cites its neighbours: FP-013 refers to `DEC-PAY-0002` (FP-012),
    `BR-HR-0004` (FP-006) and `BR-PLT-0103` (the platform register). Those are not orphans
    of FP-013 and must not be judged against FP-013's numbering. They are only worth
    reporting when they resolve nowhere at all.
    """
    index: dict[str, set[str]] = defaultdict(set)

    def absorb(path: Path, spaces: tuple[str, ...]) -> None:
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            return
        for ident in scan_ids(text):
            if ident.space in spaces:
                index[ident.space].add(ident.key)

    if features.is_dir():
        for pkg in features.iterdir():
            if not pkg.is_dir():
                continue
            for space, homes in homes_for(pkg.name).items():
                for home in homes:
                    f = pkg / home
                    if f.is_file():
                        absorb(f, (space,))

    # The global registers: REQ-/BR- for every module live here too.
    catalog = repo_root / "docs" / "00-Master-Product-Specification"
    if catalog.is_dir():
        for f in catalog.rglob("*.md"):
            absorb(f, ("REQ", "BR"))

    return index


def own_modules(texts: dict[str, str]) -> set[str]:
    """
    The module token(s) this package OWNS, derived from its own requirements.md rather
    than from the directory name — FP-008-hr-position owns POS, not HR.
    """
    counts: dict[str, int] = defaultdict(int)
    for home in ("requirements.md", "acceptance-criteria.md", "test-scenarios.md"):
        for ident in scan_ids(texts.get(home, "")):
            if ident.space in ("REQ", "AC", "TS"):
                counts[ident.module] += 1
    if not counts:
        return set()
    top = max(counts.values())
    # A module is "owned" if it holds a substantial share, so a package spanning two
    # prefixes is handled without letting a single cross-reference qualify.
    return {m for m, n in counts.items() if n >= max(3, top * 0.25)}


def check_package(pkg_dir: Path, global_index: dict[str, set[str]] | None = None) -> dict:
    result: dict = {
        "package": pkg_dir.name,
        "failures": [],       # red
        "warnings": [],       # amber — worth a human look, does not fail the gate
        "declared_gaps": [],  # deliberate, reported for visibility only
        "external_refs": [],  # cited here, defined in a sibling package or global register
        "inventory": {},
        "files_scanned": [],
    }
    global_index = global_index or {}

    # The convention is DECLARED, never inferred. An undeclared package fails: a checker
    # that guessed would be back to absorbing the divergence it exists to surface.
    result["convention"] = convention_of(pkg_dir.name)
    homes_map = homes_for(pkg_dir.name)
    if result["convention"] is None:
        result["failures"].append(
            f"UNDECLARED CONVENTION — {pkg_dir.name} appears in no PACKAGE_CONVENTION "
            f"entry. Unknown must not mean tolerated; add it to the table."
        )

    md_files = sorted(p for p in pkg_dir.glob("*.md"))
    if not md_files:
        result["failures"].append("no markdown files in package directory")
        return result

    texts: dict[str, str] = {}
    for p in md_files:
        texts[p.name] = p.read_text(encoding="utf-8", errors="replace")
        result["files_scanned"].append(p.name)

    # ---- defined vs cited -------------------------------------------------------------
    defined: dict[str, set[str]] = defaultdict(set)
    cited: dict[str, set[str]] = defaultdict(set)
    cited_in: dict[str, set[str]] = defaultdict(set)

    for fname, text in texts.items():
        for ident in scan_ids(text):
            cited[ident.space].add(ident.key)
            cited_in[ident.key].add(fname)
            if fname in homes_map.get(ident.space, ()):
                defined[ident.space].add(ident.key)

    # ---- 0. THE PACKAGE MUST CONFORM TO WHAT IT DECLARES.
    # Declaring a convention and not following it is worse than declaring none, because the
    # declaration is what every other check now trusts. `legacy` must actually define BRULE-
    # rules; `modern` must contain no BRULE- identifier at all.
    if (result["convention"] == "legacy" and "business-rules.md" in texts
            and not defined.get("BRULE")):
        result["failures"].append(
            "CONVENTION MISMATCH — declared `legacy`, which homes BRULE- in "
            "business-rules.md, but no BRULE- identifier is defined there"
        )
    if result["convention"] == "modern" and (cited.get("BRULE") or defined.get("BRULE")):
        n = len(cited.get("BRULE", set()) | defined.get("BRULE", set()))
        result["failures"].append(
            f"CONVENTION MISMATCH — declared `modern`, which has no BRULE- space, but "
            f"{n} BRULE- identifier(s) appear in this package"
        )

    mine = own_modules(texts)
    result["own_modules"] = sorted(mine)
    if not mine:
        result["warnings"].append(
            "could not derive an owning module from requirements/criteria/scenarios — "
            "numbering and orphan rules skipped; coverage still checked"
        )

    def is_mine(key: str) -> bool:
        return key.split("-")[1] in mine

    # ---- 1. orphans -------------------------------------------------------------------
    # Only the package's OWN identifiers are judged against its home files. A citation of
    # a neighbour's identifier is resolved against the repository-wide index instead.
    for space in SPACES:
        homes = homes_map.get(space, ())
        present_homes = [h for h in homes if h in texts]
        for key in sorted(cited[space] - defined[space]):
            where = ", ".join(sorted(cited_in[key]))
            if not is_mine(key):
                if key in global_index.get(space, set()):
                    result["external_refs"].append(f"{key} (cited in {where})")
                else:
                    result["warnings"].append(
                        f"UNRESOLVED {key} — cited in {where} and defined in no package "
                        f"or global register"
                    )
            elif not present_homes:
                result["warnings"].append(
                    f"{key} cited in {where} but no home file for {space}- exists in this "
                    f"package (expected one of: {', '.join(homes)})"
                )
            else:
                result["failures"].append(
                    f"ORPHAN {key} — cited in {where} but not defined in "
                    f"{' / '.join(present_homes)}"
                )

    # ---- 2. contiguity + inventory ----------------------------------------------------
    # Contiguity is a property of a space this package owns. FP-013 citing DEC-PAY-0002
    # and DEC-PAY-0017 does not mean FP-013 has fourteen missing decisions.
    for space in SPACES:
        keys = {k for k in defined[space] if is_mine(k)}
        if not keys:
            continue
        by_module: dict[str, list[int]] = defaultdict(list)
        for key in keys:
            by_module[key.split("-")[1]].append(int(key.split("-")[2]))
        for module, numbers in sorted(by_module.items()):
            numbers.sort()
            lo, hi = numbers[0], numbers[-1]
            expected = set(range(1, hi + 1))
            missing = sorted(expected - set(numbers))
            dupes = len(numbers) - len(set(numbers))
            result["inventory"][f"{space}-{module}"] = {
                "count": len(set(numbers)),
                "range": f"{lo:04d}-{hi:04d}",
                "contiguous": not missing,
                "missing": [f"{n:04d}" for n in missing],
            }
            # BRULE- IS EXEMPT FROM CONTIGUITY, AND THIS IS NOT A WEAKENING.
            # Contiguity was never asserted for BRULE- because the space did not exist in
            # this checker until T-060 added it. Asserting it NOW would be inventing a rule
            # nobody wrote -- and FP-009 numbers its document rules in an 0600 BLOCK
            # (`BRULE-DOC-0601`, `0602`, `0604`, `0606`...), so the first run demanded 600
            # missing identifiers that were never meant to exist. Reported as a warning so
            # the numbering stays visible, and it fails nothing until someone establishes
            # what BRULE- numbering means.
            sink = result["warnings"] if space == "BRULE" else result["failures"]
            if lo != 1:
                sink.append(
                    f"CONTIGUITY {space}-{module} starts at {lo:04d}, not 0001"
                    + ("  (BRULE- numbering is unestablished; reported, not failed)"
                       if space == "BRULE" else "")
                )
            if missing:
                sink.append(
                    f"CONTIGUITY {space}-{module} missing "
                    + ", ".join(f"{space}-{module}-{n:04d}" for n in missing)
                )
            if dupes:
                result["warnings"].append(
                    f"{space}-{module} has {dupes} duplicate definition(s) in its home file"
                )

    # ---- 5. machine-unreadable ranges -------------------------------------------------
    # A range is only harmful when it is the ONLY place an identifier appears. FP-012's
    # damage was `AC-PAY-0006`-`0008` standing in for a link that no explicit citation
    # backed. A summary line reading `REQ-PAY-0001`-`0018` next to eighteen explicit
    # definitions hides nothing, and failing it would train people to ignore this check.
    all_cited: set[str] = set()
    for space in SPACES:
        all_cited |= cited[space]

    for fname, text in texts.items():
        for m in RANGE_RE.finditer(text):
            space, module, first, last = m.groups()
            if int(last) <= int(first):
                continue  # not a range; a date, a version, or a stray dash
            hidden = [
                f"{space}-{module}-{n:04d}"
                for n in range(int(first) + 1, int(last) + 1)
                if f"{space}-{module}-{n:04d}" not in all_cited
            ]
            if not hidden:
                continue
            span = hidden[0] + (f"..{hidden[-1]}" if len(hidden) > 1 else "")
            if fname == "traceability-matrix.md":
                # The matrix is the machine-readable artifact. A range here breaks the
                # linkage itself, which is the FP-012 defect verbatim.
                result["failures"].append(
                    f"RANGE {fname}: '{m.group(0)}' is the only citation of "
                    f"{len(hidden)} identifier(s) ({span}) — the matrix has to be "
                    f"machine-checkable, not merely legible; expand to explicit identifiers"
                )
            elif not all(k in global_index.get(space, set()) for k in hidden):
                # Outside the matrix a range is prose. It is only worth reporting when the
                # identifiers it covers are defined nowhere in the repository.
                result["warnings"].append(
                    f"RANGE {fname}: '{m.group(0)}' covers {len(hidden)} identifier(s) "
                    f"({span}) that are defined in no package or global register"
                )

    # ---- 3 & 4. coverage --------------------------------------------------------------
    matrix_name = "traceability-matrix.md"
    if matrix_name not in texts:
        result["warnings"].append(f"no {matrix_name} — coverage rules 3 and 4 not checked")
        chain = []
    else:
        chain = parse_chain(texts[matrix_name])
        if not chain:
            result["failures"].append(
                f"{matrix_name} has no parseable chain rows (no pipe-table row carries a "
                f"REQ- identifier in its first cell)"
            )

    ac_with_test: set[str] = set()
    req_in_chain: set[str] = set()
    ac_on_declared_gap_row: set[str] = set()

    # NOT EVERY PACKAGE USES THE IDENTIFIER CHAIN.
    # FP-001..FP-009 map requirements to code symbols and test CLASS names
    # (`DepartmentDomainTests`, `D22`-`D25`) rather than to AC-/TS- identifiers. Rules 3
    # and 4 are meaningless there, and emitting a failure per row would produce dozens of
    # false reds — which is how a check earns the right to be ignored. Detected by shape,
    # then declared, rather than assumed.
    identifier_chain = any(cell_ids(r["criteria"], "AC") or cell_ids(r["tests"], "TS")
                           for r in chain)
    if chain and not identifier_chain:
        result["warnings"].append(
            f"{matrix_name} maps requirements to code symbols and test class names rather "
            f"than to AC-/TS- identifiers ({len(chain)} rows) — coverage rules 3 and 4 are "
            f"not applicable to this package; orphan and contiguity rules still applied"
        )
        chain = []

    for row in chain:
        req_label = ", ".join(row["requirements"])
        req_in_chain.update(row["requirements"])
        criteria = cell_ids(row["criteria"], "AC")
        tests = cell_ids(row["tests"], "TS")

        if not criteria:
            if is_declared_gap(row["criteria"]):
                result["declared_gaps"].append(
                    f"{req_label} — criteria cell is a declared gap"
                )
            else:
                result["failures"].append(
                    f"COVERAGE {req_label} has no acceptance criterion "
                    f"(criteria cell: {row['criteria'][:60]!r})"
                )
        if not tests:
            if is_declared_gap(row["tests"]):
                result["declared_gaps"].append(
                    f"{req_label} — tests cell is a declared gap"
                )
                ac_on_declared_gap_row.update(criteria)
            else:
                result["failures"].append(
                    f"COVERAGE {req_label} has no test scenario "
                    f"(tests cell: {row['tests'][:60]!r})"
                )
        if criteria and tests:
            ac_with_test.update(criteria)

    # Criteria may also be carried by PROSE — the 'criteria carrying no requirement'
    # section, which the matrices keep deliberately parentless. FP-013 splits it across two
    # paragraphs: the criteria in one, the scenarios they map to in the next. So the scope
    # for prose coverage is the '##' section, not the paragraph.
    #
    # Section scope is applied ONLY to sections that carry no chain row. Inside the chain
    # table every criterion and every scenario co-occur, and using section scope there
    # would make rules 3 and 4 unfalsifiable.
    if matrix_name in texts:
        for section in re.split(r"^##\s", texts[matrix_name], flags=re.M):
            has_chain_row = any(
                line.strip().startswith("|")
                and any(i.space == "REQ" for i in scan_ids(line.split("|")[1] if "|" in line else ""))
                for line in section.splitlines()
            )
            if has_chain_row:
                continue
            ids = scan_ids(section)
            acs = {i.key for i in ids if i.space == "AC"}
            tss = {i.key for i in ids if i.space == "TS"}
            if not (acs and tss):
                continue
            # Report only what the chain did NOT already cover. A criterion mentioned in
            # passing by a prose section AND linked by a chain row is not parentless.
            newly = sorted(acs - ac_with_test)
            ac_with_test.update(acs)
            if newly:
                result["declared_gaps"].append(
                    f"{len(newly)} criteri{'on' if len(newly) == 1 else 'a'} carried by "
                    f"prose with no owning requirement: {', '.join(newly)}"
                )

    # every DEFINED requirement must appear in the chain at all
    for key in sorted(k for k in defined["REQ"] - req_in_chain if is_mine(k)):
        result["failures"].append(
            f"COVERAGE {key} is defined in requirements.md but absent from the chain table"
        )

    # every DEFINED criterion must reach a test somewhere in the matrix
    if chain:
        for key in sorted(k for k in defined["AC"] - ac_with_test if is_mine(k)):
            if key in ac_on_declared_gap_row:
                # Its row said '—' for tests. The absence is stated, not hidden — the same
                # honesty the matrices insist on for the Req column. Surfaced, not red.
                result["warnings"].append(
                    f"COVERAGE {key} has no test scenario, on a row that declares the gap "
                    f"— confirm the criterion is asserted inside another scenario"
                )
            else:
                result["failures"].append(
                    f"COVERAGE {key} is defined but nothing in {matrix_name} carries it "
                    f"together with a test scenario"
                )

    # ---- 6. unruled owner decisions ---------------------------------------------------
    od_defined = sorted(k for k in defined["OD"] if is_mine(k))
    ratified_text = texts.get("decisions-ratified.md", "")
    approved_text = texts.get("decisions-approved.md", "")
    ruling_text = ratified_text + "\n" + approved_text
    if od_defined:
        if ruling_text.strip():
            ruled = {i.key for i in scan_ids(ruling_text) if i.space == "OD"}
            unruled = [k for k in od_defined if k not in ruled]
            result["od_total"] = len(od_defined)
            result["od_unruled"] = unruled
            if unruled:
                result["warnings"].append(
                    f"BUILD BLOCKED: {len(unruled)} owner decision(s) carry no ruling "
                    f"({', '.join(unruled[:6])}{' …' if len(unruled) > 6 else ''}) — "
                    f"nothing in the OD register has a default"
                )
        else:
            result["od_total"] = len(od_defined)
            result["od_unruled"] = od_defined
            result["warnings"].append(
                f"BUILD BLOCKED: {len(od_defined)} owner decision(s) and no ratification "
                f"file in this package"
            )

    # ---- step-0 residue ---------------------------------------------------------------
    dependent_hits = {
        fname: len(DEPENDENT_RE.findall(text))
        for fname, text in texts.items()
        if DEPENDENT_RE.search(text)
    }
    outside_ratification = {
        f: n for f, n in dependent_hits.items() if f != "decisions-ratified.md"
    }
    if outside_ratification:
        result["warnings"].append(
            "'-dependent' markers remain outside the ratification file: "
            + ", ".join(f"{f} ({n})" for f, n in sorted(outside_ratification.items()))
        )

    return result


# --------------------------------------------------------------------------------------
# Check 7 — the master register, one level above the packages
# --------------------------------------------------------------------------------------

def _citation_corpus(root: Path, exclude: set[Path]) -> list[tuple[Path, str]]:
    """
    Every file a citation could plausibly live in, minus the two definition files.

    The definition files are excluded rather than filtered per-identifier because a
    constraint's own `## CON-0001` heading is not a citation of itself, and neither is the
    paragraph under it. Excluding the file is exact; excluding "the heading line" would
    quietly count the body text as evidence that something references the rule.
    """
    corpus: list[tuple[Path, str]] = []
    for name in CITATION_ROOTS:
        base = root / name
        if not base.is_dir():
            continue
        for path in base.rglob("*"):
            if not path.is_file():
                continue
            if path.suffix.lower() not in CITATION_SUFFIXES:
                continue
            if SKIP_PARTS & set(path.parts):
                continue
            if path in exclude:
                continue
            try:
                corpus.append((path, path.read_text(encoding="utf-8", errors="replace")))
            except OSError:
                continue
    return corpus


def check_master_register(root: Path, features: Path) -> dict:
    """
    Check 7. Reads the master specification, then asks two questions the package-level
    checks structurally cannot ask.

    Returns its own result dict. It is never merged into a package result: a rule that
    nobody implemented is a governance finding, not a defect in whichever package happened
    to be scanned at the time.
    """
    result: dict = {
        "orphans": [],       # defined in the master register, cited nowhere at all
        "untraced": [],      # cited, but never beside a REQ- identifier
        "unpromoted": [],    # BR-<MODULE> owned by a package, absent from the master file
        "defined": 0,
        "files_scanned": 0,
        "sources": [],
        "notes": [],
    }

    con_path = root.joinpath(*MASTER_CON_FILE)
    br_path = root.joinpath(*MASTER_BR_FILE)

    master: dict[str, Path] = {}
    if con_path.is_file():
        text = con_path.read_text(encoding="utf-8", errors="replace")
        for key in MASTER_CON_DEF_RE.findall(text):
            master[key] = con_path
        result["sources"].append(con_path.relative_to(root).as_posix())
    else:
        result["notes"].append(
            f"no {'/'.join(MASTER_CON_FILE)} — CON- constraints not checked"
        )

    master_br_modules: set[str] = set()
    if br_path.is_file():
        text = br_path.read_text(encoding="utf-8", errors="replace")
        for key in MASTER_BR_DEF_RE.findall(text):
            master[key] = br_path
            master_br_modules.add(key.split("-")[1])
        result["sources"].append(br_path.relative_to(root).as_posix())
    else:
        result["notes"].append(
            f"no {'/'.join(MASTER_BR_FILE)} — BR- rules and promotion not checked"
        )

    result["defined"] = len(master)
    result["master_br_modules"] = sorted(master_br_modules)

    # ---- 7a/7b. orphan and untraced ---------------------------------------------------
    #
    # ONE PASS OVER THE CORPUS, NOT ONE PASS PER IDENTIFIER. Fifty-one identifiers against
    # a few thousand files is a product this script should not be paying; inverting the
    # loop makes it linear in blocks and keeps the whole check under a second.
    if master:
        corpus = _citation_corpus(root, exclude={con_path, br_path})
        result["files_scanned"] = len(corpus)

        cited: set[str] = set()
        traced: set[str] = set()
        cited_in: dict[str, set[str]] = defaultdict(set)
        traced_in: dict[str, set[str]] = defaultdict(set)

        for path, text in corpus:
            # Cheap rejection first: most files mention neither register.
            if "CON-" not in text and "BR-" not in text:
                continue
            rel = path.relative_to(root).as_posix()
            for block in split_blocks(text):
                keys = {f"CON-{n}" for n in CON_RE.findall(block)}
                keys |= {i.key for i in scan_ids(block) if i.space == "BR"}
                keys &= master.keys()
                if not keys:
                    continue
                cited |= keys
                for key in keys:
                    cited_in[key].add(rel)
                if REQ_ANY_RE.search(block):
                    traced |= keys
                    for key in keys:
                        traced_in[key].add(rel)

        for key in sorted(master):
            if key not in cited:
                result["orphans"].append(
                    f"{key} — defined in "
                    f"{master[key].relative_to(root).as_posix()} and cited nowhere else in "
                    f"the repository"
                )
            elif key not in traced:
                where = sorted(cited_in[key])
                shown = ", ".join(where[:3]) + (" …" if len(where) > 3 else "")
                result["untraced"].append(
                    f"{key} — cited in {len(where)} file(s) ({shown}) but never alongside a "
                    f"REQ- identifier: mentioned, not implemented"
                )

    # ---- 7c. unpromoted package BR spaces ---------------------------------------------
    #
    # A package that defines BR-ATT in its own business-rules.md is COMPLETE by checks 1-6,
    # and correctly so. This is the only check that can see that the master register does
    # not know those rules exist.
    if br_path.is_file() and features.is_dir():
        for pkg in sorted(p for p in features.iterdir() if p.is_dir()):
            home = pkg / "business-rules.md"
            if not home.is_file():
                continue
            text = home.read_text(encoding="utf-8", errors="replace")
            by_module: dict[str, set[str]] = defaultdict(set)
            for ident in scan_ids(text):
                if ident.space == "BR":
                    by_module[ident.module].add(ident.key)
            for module, keys in sorted(by_module.items()):
                if module in master_br_modules:
                    continue
                # A package citing ONE rule of a foreign module is quoting a neighbour, not
                # owning a space. Three is the same floor own_modules() uses for the same
                # reason, so the two rules cannot disagree about what a package owns.
                if len(keys) < 3:
                    continue
                lo = min(keys)
                hi = max(keys)
                result["unpromoted"].append(
                    f"BR-{module} — {len(keys)} rule(s) ({lo}…{hi}) defined in "
                    f"{pkg.name}/business-rules.md, and the master Business-Rules.md "
                    f"carries no BR-{module} rule at all"
                )

    result["total"] = (
        len(result["orphans"]) + len(result["untraced"]) + len(result["unpromoted"])
    )
    return result


# --------------------------------------------------------------------------------------
# Reporting
# --------------------------------------------------------------------------------------

MASTER_LIST_CAP = 12


# --------------------------------------------------------------------------------------
# Check 8 — the two decision registers agree
# --------------------------------------------------------------------------------------
#
# WHAT THIS CATCHES, AND WHY CHECK 6 IS GREEN WHILE IT IS TRUE
# ------------------------------------------------------------
# Check 6 asks whether every `OD-` carries a ruling, and it reads the ruling from
# `decisions-ratified.md` / `decisions-approved.md` — correctly. It reports `0 unruled` and
# is satisfied.
#
# **Nothing verified that the OTHER register agreed.** FP-014 was ratified while
# `decisions-open.md` still introduced all seventeen of its decisions as open questions and
# still said "each one blocks the build prompt". The tool was green because it consulted the
# file that knew; the file a person opens was the one that did not.
#
# That is the same shape as `AC-SUB-0047` — one fact in two registers, only one aware it
# changed — and this is the buildable half of it.
#
# HOW A RULING IS RECOGNISED — SURVEYED, NOT INVENTED
# ----------------------------------------------------
# **FP-013 already had the convention, and it is the reason this check has something to
# match rather than something to impose.** Every one of its sixteen decisions carries a
# `**RULED: <the answer>**` line directly under its heading, written when the package was
# ratified. FP-014 carries none of it.
#
# So the marker vocabulary below is taken from the corpus. `RULED` is FP-013's word; the
# others are the obvious neighbours a later package might reach for, and a link to the ruling
# register is accepted because pointing at the answer is an acknowledgement too. **The check
# prints which marker it matched**, so the vocabulary stays auditable from the output rather
# than trusted.
#
# It accepts the marker in the heading OR in the lines just beneath it, because the two
# packages place it differently — FP-013 on the line below, FP-014's one marked decision in
# the heading itself. **Neither placement is adjudicated here.** Reporting the divergence is
# this check's job; choosing a house style is not.
#
# Word forms are matched UPPERCASE ONLY. Lower-case "ruled" is ordinary prose in these files
# ("`OD-SUB-0009` ruled that…") and matching it would make every cross-reference look like an
# acknowledgement.
#
# WHAT IT CANNOT SEE — stated because a GREEN here is narrower than it looks
# --------------------------------------------------------------------------
#   * A ruling recorded in NEITHER file — in an architect's note, or a message — is invisible
#     to both this check and check 6. `DEC-L-034` was exactly that until T-036 moved it.
#   * A ruling recorded ONLY in the open register is check 6's finding, not this one.
#   * It reads HEADINGS. A decision discussed in prose without a heading of its own is not
#     seen at all.
#   * It cannot tell a stale acknowledgement from a current one: a heading marked RULED whose
#     ruling was later reversed reads as agreement.
#   * A package with no `decisions-open.md` is SKIPPED, not passed. Eleven of the fourteen
#     packages keep a single `decisions-approved.md`, where the two registers are one file
#     and cannot disagree.
#
# Its bias is to over-report, which is this script's stated posture: a checker that
# under-reports is worse than one that occasionally asks a human to look.

OPEN_REGISTER = "decisions-open.md"
RULING_REGISTERS = ("decisions-ratified.md", "decisions-approved.md")

# A heading DEFINES the decision whose identifier comes first in it. FP-013 carries
# `### \`DEC-ATT-0014\` — … whichever way \`OD-ATT-0011\` rules`, which mentions an OD and
# defines a DEC; taking the first identifier rather than any identifier is what tells them
# apart.
HEADING_RE = re.compile(r"^(#{2,6})[ \t]+(.*\S)[ \t]*$", re.M)

# Uppercase only, and as whole words. See the note above.
ACK_TOKEN_RE = re.compile(r"\b(RULED|RATIFIED|SUPERSEDED|WITHDRAWN|DECIDED|CLOSED)\b")
ACK_LINK_RE = re.compile(r"decisions-(?:ratified|approved)\.md")

# How far past the heading an acknowledgement may sit before it stops being an announcement
# and becomes a remark buried in the discussion.
ACK_LEAD_LINES = 8


def _heading_blocks(text: str) -> list[tuple[int, str, str]]:
    """(1-based line number, heading text, block body) for every markdown heading."""
    matches = list(HEADING_RE.finditer(text))
    out: list[tuple[int, str, str]] = []
    for n, m in enumerate(matches):
        level = len(m.group(1))
        end = len(text)
        for later in matches[n + 1:]:
            if len(later.group(1)) <= level:
                end = later.start()
                break
        line_no = text.count("\n", 0, m.start()) + 1
        out.append((line_no, m.group(2), text[m.end():end]))
    return out


def check_decision_registers(pkg_dir: Path) -> dict:
    """One package's answer to: do the open and the ruling register tell the same story?"""
    result: dict = {
        "package": pkg_dir.name,
        "applicable": False,
        "reason": "",
        "headed": 0,
        "diverged": [],
        "acknowledged": [],
        "notes": [],
    }

    open_path = pkg_dir / OPEN_REGISTER
    ruling_paths = [pkg_dir / name for name in RULING_REGISTERS
                    if (pkg_dir / name).is_file()]

    if not open_path.is_file():
        result["reason"] = (
            f"no {OPEN_REGISTER} — the two registers are one file and cannot disagree")
        return result
    if not ruling_paths:
        result["reason"] = (
            f"{OPEN_REGISTER} with no ruling register — nothing claims these are ruled, "
            f"so check 6 owns this package, not check 8")
        return result

    result["applicable"] = True
    result["registers"] = [p.name for p in ruling_paths]

    ruling_text = "\n".join(
        p.read_text(encoding="utf-8", errors="replace") for p in ruling_paths)
    ruled = {i.key for i in scan_ids(ruling_text) if i.space == "OD"}

    open_text = open_path.read_text(encoding="utf-8", errors="replace")

    for line_no, heading, body in _heading_blocks(open_text):
        first = ID_RE.search(heading)
        if not first or first.group(1) != "OD":
            continue

        key = f"{first.group(1)}-{first.group(2)}-{first.group(3)}"
        result["headed"] += 1

        if key not in ruled:
            continue  # Genuinely open. Check 6 already reports it if it should not be.

        lead = heading + "\n" + "\n".join(body.splitlines()[:ACK_LEAD_LINES])
        token = ACK_TOKEN_RE.search(lead)
        link = ACK_LINK_RE.search(lead)

        if token or link:
            result["acknowledged"].append(
                f"{key} — {OPEN_REGISTER}:{line_no}, matched "
                f"{token.group(1) if token else link.group(0)}")
        else:
            result["diverged"].append(
                f"{key} — ruled in {', '.join(p.name for p in ruling_paths)}, still "
                f"presented as open at {OPEN_REGISTER}:{line_no}")

    return result


# --------------------------------------------------------------------------------------
# CHECK 9 — ADR DEPENDENCY EDGES, AND CHECK 10 — DECISION MECHANISMS (T-025)
#
# WHY THESE ARE ONE SECTION. Both answer the same question about two registers: what does
# this thing depend on, and is the thing it depends on still true? The ADR register carried
# `depends_on` for 28 of its 29 files and NOTHING read it. The decision register had no way
# to say so at all, and that cost three hours on 2026-08-27 when `DEC-L-054` prescribed
# `git rev-parse <ref>:<path>` in the morning and `DEC-L-056` restricted the environment
# variable that form silently requires at midday. Both windows had read both rules.
#
# WHAT CHECK 9 FAILS ON, and it is deliberately only two things:
#   * a `depends_on` target that does not exist and is not declared a reservation
#   * a `depends_on` target whose status is Superseded
#
# WHAT IT REPORTS AND NEVER FAILS ON:
#   * an Accepted ADR depending on one that is Proposed
#
# THAT THIRD CLASS IS NOT A DEFECT UNTIL SOMEONE DEFINES `depends_on`. Nobody has written
# down whether the edge asserts "requires this to be true" or "relates to this". Under the
# first reading those edges are broken; under the second they are an Accepted decision
# correctly pointing at future work. **Failing on them would enforce a semantic nobody
# wrote**, which is the exact failure this repository has recorded four times in other
# instruments. It is reported as its own named class so the definition can be written.
#
# RESERVATIONS. `ADR-028` is reserved for V5 by `OD-DOC-009` and does not exist as a file.
# Nothing depends_on it today, so this syntax has nothing to express yet -- it is INSURANCE,
# recorded as insurance: the first person to write that edge will be a stranger, and the
# false positive they get would be a mechanical reproduction of one this board carried for
# nine tasks. The syntax is a YAML comment on the edge itself:
#
#     depends_on:
#       - ADR-028  # reserved: OD-DOC-009
#
# A reservation whose target DOES exist is reported too: it means the reservation was filled
# and the annotation was never removed, which is a stale claim rather than a missing file.
#
# CHECK 10's REGISTER FORMAT IS FREE TEXT ON PURPOSE. Two optional keys, inline in the
# decision's own row, matched by one regular expression:
#
#     **prescribes:** `git ls-tree`
#     **restricts:**  `MSYS_NO_PATHCONV`
#
# A controlled vocabulary would need maintaining, and a rule whose mechanism is not in the
# list would get NO key rather than a new term. Free text spelled wrong fails to join in
# silence -- so the report prints the DISTINCT set of mechanism strings it saw, and a typo
# shows up as a near-duplicate to anyone reading it. That is one `sorted(set())` and needs
# no vocabulary.
#
# LIVENESS IS THE PRESENCE OF THE KEY. A decision whose mechanism has been replaced loses
# its `prescribes:` key -- that is how retirement is expressed, and it is why there is no
# supersedes graph here. `DEC-L-054` carries no key because `DEC-L-054b` replaced it.
# --------------------------------------------------------------------------------------

ADR_ID_RE = re.compile(r"^id:\s*(ADR-\d+)\s*$", re.M)
ADR_STATUS_RE = re.compile(r"^status:\s*(\S+)", re.M)
ADR_EDGE_RE = re.compile(r"^\s*-\s*(ADR-\d+)\s*(?:#\s*(.*))?$")
RESERVED_RE = re.compile(r"\breserved:\s*(\S+)", re.I)
MECHANISM_RE = re.compile(r"\*\*(prescribes|restricts):\*\*\s*`([^`]+)`", re.I)
DEC_ID_RE = re.compile(r"`(DEC-L-\d+[a-z]?)`")


def _adr_front_matter(text: str) -> tuple[str, str, list[tuple[str, str]]]:
    """(id, status, [(target, annotation)]) from an ADR's YAML front matter."""
    end = text.find("\n---", 3)
    fm = text[:end] if end > 0 else text[:2000]
    ident = ADR_ID_RE.search(fm)
    status = ADR_STATUS_RE.search(fm)
    edges: list[tuple[str, str]] = []
    in_block = False
    for line in fm.splitlines():
        if line.startswith("depends_on:"):
            in_block = True
            continue
        if in_block:
            m = ADR_EDGE_RE.match(line)
            if m:
                edges.append((m.group(1), (m.group(2) or "").strip()))
                continue
            if line.strip() and not line.startswith(" "):
                in_block = False
    return (ident.group(1) if ident else "",
            status.group(1) if status else "",
            edges)


def check_adr_edges(adr_dir: Path) -> dict:
    out: dict = {"applicable": False, "reason": "", "adrs": 0, "edges": 0,
                 "missing_key": [], "dangling": [], "superseded": [],
                 "reserved": [], "stale_reservation": [], "accepted_on_unratified": []}
    if not adr_dir.is_dir():
        out["reason"] = f"no ADR directory at {adr_dir}"
        return out
    files = sorted(p for p in adr_dir.glob("ADR-0*.md"))
    if not files:
        out["reason"] = f"no ADR-0*.md under {adr_dir}"
        return out

    out["applicable"] = True
    index: dict[str, str] = {}
    parsed: list[tuple[str, str, list[tuple[str, str]]]] = []
    for p in files:
        ident, status, edges = _adr_front_matter(
            p.read_text(encoding="utf-8", errors="replace"))
        if not ident:
            continue
        index[ident] = status
        parsed.append((ident, status, edges))
    out["adrs"] = len(parsed)

    for ident, status, edges in parsed:
        if not edges and not _has_depends_key(files, ident):
            out["missing_key"].append(ident)
        for target, note in edges:
            out["edges"] += 1
            reservation = RESERVED_RE.search(note)
            if target not in index:
                if reservation:
                    out["reserved"].append(
                        f"{ident} -> {target}  (reserved by {reservation.group(1)})")
                else:
                    out["dangling"].append(f"{ident} -> {target}  (no such ADR)")
                continue
            if reservation:
                out["stale_reservation"].append(
                    f"{ident} -> {target}  marked reserved, but {target} exists")
            tstatus = index[target]
            if tstatus.lower().startswith("supersed"):
                out["superseded"].append(f"{ident} -> {target}  (target is {tstatus})")
            elif status == "Accepted" and tstatus != "Accepted":
                out["accepted_on_unratified"].append(
                    f"{ident} (Accepted) -> {target} ({tstatus})")
    return out


def _has_depends_key(files: list[Path], ident: str) -> bool:
    for p in files:
        if p.name.startswith(ident + "-"):
            return "depends_on:" in p.read_text(encoding="utf-8", errors="replace")[:2000]
    return False


def check_decision_mechanisms(board: Path) -> dict:
    out: dict = {"applicable": False, "reason": "", "decisions": 0,
                 "prescribes": {}, "restricts": {}, "collisions": [], "mechanisms": []}
    if not board.is_file():
        out["reason"] = f"no decision register at {board}"
        return out
    out["applicable"] = True

    # errors="replace" ON PURPOSE: this file has carried literal NUL bytes, written where
    # someone meant the TEXT `\0` while describing git's blob header. `grep` then reports
    # "Binary file matches" and prints no lines at all -- so a register whose whole design
    # is "the grep must be a grep" was unreadable by grep. Reading it here must not care.
    # READ BYTES AND DECODE, NEVER `read_text()`. This register contains FIVE lone CRs,
    # written where the TEXT `\r` was meant while quoting a mangled Windows path
    # (`.claude\roles\CODER.md`). `read_text()` opens in text mode with UNIVERSAL NEWLINE
    # TRANSLATION, which turns a bare CR into a newline -- so the row for `DEC-L-054b` was
    # split in two before any splitting of ours, and its mechanism key was attributed to
    # `DEC-L-054`: the id appearing in the second fragment, and the one whose mechanism was
    # REPLACED. **The wrong answer was the plausible one**, which is why it survived a first
    # fix aimed at `str.splitlines()` -- also true about lone CRs, and not what was doing it.
    #
    # Same family as the NUL bytes: a control character written where its escape sequence was
    # meant. Found by running against the real register, and only by attributing an edge to
    # the wrong decision in a way a reader would have believed.
    text = board.read_bytes().decode("utf-8", errors="replace")
    for line in text.split("\n"):
        ids = DEC_ID_RE.findall(line)
        hits = MECHANISM_RE.findall(line)
        if not hits:
            continue
        owner = ids[0] if ids else "<unattributed>"
        out["decisions"] += 1
        for kind, mech in hits:
            bucket = out["prescribes"] if kind.lower() == "prescribes" else out["restricts"]
            bucket.setdefault(mech.strip(), []).append(owner)

    for mech, restricting in sorted(out["restricts"].items()):
        prescribing = out["prescribes"].get(mech)
        if prescribing:
            out["collisions"].append(
                f"`{mech}` — restricted by {', '.join(restricting)}; "
                f"still prescribed by {', '.join(prescribing)}")

    out["mechanisms"] = sorted(set(out["prescribes"]) | set(out["restricts"]))
    return out


def report_edges(adr: dict, dec: dict) -> int:
    """Its own section and its own verdict. Returns the number of FAILING findings."""
    print()
    print("=" * 86)
    print("CHECK 9/10 — ADR DEPENDENCY EDGES AND DECISION MECHANISMS")
    print("=" * 86)

    failures = 0
    if not adr["applicable"]:
        print(f"  check 9 not applicable — {adr['reason']}")
    else:
        print(f"  {adr['adrs']} ADR(s), {adr['edges']} dependency edge(s)")
        for label, key in (("DANGLING", "dangling"), ("SUPERSEDED TARGET", "superseded")):
            for row in adr[key]:
                print(f"    {label}: {row}")
                failures += 1
        for row in adr["missing_key"]:
            print(f"    NO depends_on KEY: {row} — an absent list and an empty one must "
                  f"not look alike")
            failures += 1
        for row in adr["stale_reservation"]:
            print(f"    STALE RESERVATION: {row}")
        for row in adr["reserved"]:
            print(f"    reserved (not a defect): {row}")
        # REPORTED, NEVER FAILED. See the section note: `depends_on` has no written meaning,
        # and an edge cannot be called invalid before the edge is defined.
        for row in adr["accepted_on_unratified"]:
            print(f"    ACCEPTED-ON-UNRATIFIED (reported, not failed): {row}")

    print()
    if not dec["applicable"]:
        print(f"  check 10 not applicable — {dec['reason']}")
    else:
        print(f"  {dec['decisions']} decision(s) declare a mechanism")
        if dec["mechanisms"]:
            # The architect's mitigation for free text: a typo cannot fail to join loudly,
            # but it CAN show up as a near-duplicate in a list somebody reads.
            print("    mechanism strings seen (a near-duplicate here is a typo):")
            for m in dec["mechanisms"]:
                who = []
                if m in dec["prescribes"]:
                    who.append("prescribed by " + ", ".join(dec["prescribes"][m]))
                if m in dec["restricts"]:
                    who.append("restricted by " + ", ".join(dec["restricts"][m]))
                print(f"      `{m}` — {'; '.join(who)}")
        for row in dec["collisions"]:
            print(f"    COLLISION: {row}")
            failures += 1

    print()
    print(f"  {'edges: FINDINGS' if failures else 'edges: clean'} "
          f"({failures} failing finding(s))")
    return failures


def report_registers(rows: list[dict]) -> int:
    """
    Its own section, its own verdict, and deliberately NOT the RED/GREEN words the package
    sections use — for check 7's reason. A divergence is not a broken package: both files
    are internally coherent and the defect is that they disagree with each other.

    Returns the number of diverged decisions.
    """
    applicable = [r for r in rows if r["applicable"]]
    diverged_rows = [r for r in applicable if r["diverged"]]
    total = sum(len(r["diverged"]) for r in applicable)

    verdict = f"{total} DIVERGENT in {len(diverged_rows)} package(s)" if total else "AGREED"
    print(f"\n{'=' * 86}")
    print(f"DECISION REGISTERS (check 8, open vs ruled)  [{verdict}]")
    print("=" * 86)

    print(f"\n  {len(applicable)} of {len(rows)} package(s) keep both registers and can "
          f"therefore disagree.")

    for r in rows:
        if r["applicable"]:
            continue
        print(f"    skipped  {r['package']:<38} {r['reason']}")

    for r in applicable:
        marks = f"{len(r['acknowledged'])} acknowledged" if r["acknowledged"] else ""
        print(f"\n  {r['package']}  —  {r['headed']} decision(s) headed in "
              f"{OPEN_REGISTER}"
              + (f", {marks}" if marks else ""))
        if not r["diverged"]:
            print("    both registers agree")
            continue
        print(f"    DIVERGENT ({len(r['diverged'])}) — ruled in the ruling register and "
              f"still open here:")
        for item in r["diverged"][:MASTER_LIST_CAP]:
            print(f"      - {item}")
        if len(r["diverged"]) > MASTER_LIST_CAP:
            print(f"      … and {len(r['diverged']) - MASTER_LIST_CAP} more")

    print()
    if total:
        print("  Check 6 is GREEN for these packages and is right to be: the rulings exist, "
              "in the file")
        print("  it reads. This is the register a PERSON opens still saying the question is "
              "open.")
    print("  What this cannot see: a ruling recorded in NEITHER register — in a note or a "
          "message — is")
    print("  invisible here and to check 6 alike. A GREEN means the two registers agree, "
          "NOT that every")
    print("  decision is recorded somewhere.")

    return total


def report_master(m: dict) -> None:
    """
    Printed in its own section, with its own verdict, ABOVE the package results.

    It deliberately does NOT use the RED/GREEN words the package sections use. A reader
    scanning for "RED" must not find one here and conclude a package is broken — the whole
    point of check 7 is that these findings belong to nobody's package.
    """
    findings = m.get("total", 0)
    verdict = f"{findings} FINDING(S)" if findings else "CLEAN"
    print(f"\n{'=' * 86}")
    print(f"MASTER REGISTER (check 7, above the package line)  [{verdict}]")
    print("=" * 86)

    if m.get("sources"):
        print(f"\n  Read: {', '.join(m['sources'])}")
    print(f"  {m.get('defined', 0)} identifier(s) defined; "
          f"{m.get('files_scanned', 0)} file(s) scanned for citations")
    if m.get("master_br_modules"):
        print(f"  Master BR modules: {', '.join(m['master_br_modules'])}")

    for label, items, gloss in (
        ("ORPHAN", m.get("orphans", []),
         "defined in the master register and cited nowhere else"),
        ("UNTRACED", m.get("untraced", []),
         "cited, but no requirement anywhere references it"),
        ("UNPROMOTED", m.get("unpromoted", []),
         "a package owns a BR space the master register does not carry"),
    ):
        if not items:
            continue
        print(f"\n  {label} ({len(items)}) — {gloss}:")
        for item in items[:MASTER_LIST_CAP]:
            print(f"    - {item}")
        if len(items) > MASTER_LIST_CAP:
            print(f"    … and {len(items) - MASTER_LIST_CAP} more")

    for note in m.get("notes", []):
        print(f"\n  NOTE: {note}")

    print()
    if findings:
        print("  A finding here is NOT a broken package. It is a rule the product wrote "
              "down and never")
        print("  implemented, or implemented and never registered. Fixing one is a "
              "product ruling, not a")
        print("  tidy-up — see the packages' own decision registers.")
    else:
        print("  Every master-register rule is cited and reaches a requirement.")


def report(results: list[dict]) -> int:
    red = 0
    for r in results:
        fails, warns, gaps = r["failures"], r["warnings"], r["declared_gaps"]
        status = "RED" if fails else "GREEN"
        if fails:
            red += 1
        print(f"\n{'=' * 86}")
        print(f"{r['package']}  [{status}]  "
              f"{len(fails)} failure(s), {len(warns)} warning(s), {len(gaps)} declared gap(s)")
        print("=" * 86)

        # PRINTED EVERY RUN, PASS OR FAIL. The divergence between the two conventions must
        # be visible rather than absorbed: a reader who sees `BR-` in FP-001 and `BR-` in
        # FP-014 is reading two different things, and nothing else in this output says so.
        conv = r.get("convention")
        if conv == "legacy":
            print("\n  Convention: LEGACY — BRULE- homed in business-rules.md, "
                  "BR- in requirements.md (a SEPARATE space, not a renaming)")
        elif conv == "modern":
            print("\n  Convention: MODERN — BR- homed in business-rules.md, no BRULE- space")
        else:
            print("\n  Convention: UNDECLARED — this package is in no PACKAGE_CONVENTION entry")

        if r.get("own_modules"):
            print(f"\n  Owns: {', '.join(r['own_modules'])}"
                  + (f"   External refs: {len(r['external_refs'])}"
                     if r.get("external_refs") else ""))

        if r["inventory"]:
            print("\n  Inventory, derived:")
            print(f"    {'space':<14} {'count':>6}  {'range':<12} contiguous")
            for space, inv in r["inventory"].items():
                mark = "yes" if inv["contiguous"] else f"NO (missing {', '.join(inv['missing'])})"
                print(f"    {space:<14} {inv['count']:>6}  {inv['range']:<12} {mark}")

        if "od_total" in r:
            unruled = r.get("od_unruled", [])
            print(f"\n  Owner decisions: {r['od_total']} defined, "
                  f"{len(unruled)} unruled -> "
                  f"{'BUILD BLOCKED' if unruled else 'ratified, build may proceed'}")

        for label, items in (("FAILURES", fails), ("WARNINGS", warns)):
            if items:
                print(f"\n  {label}:")
                for item in items:
                    print(f"    - {item}")

        if gaps:
            print(f"\n  Declared gaps ({len(gaps)}) — deliberate, not defects:")
            for item in gaps[:12]:
                print(f"    - {item}")
            if len(gaps) > 12:
                print(f"    … and {len(gaps) - 12} more")

    print(f"\n{'=' * 86}")
    if red:
        print(f"[TRACE RED] {red} of {len(results)} package(s) have failures")
    else:
        print(f"[TRACE GREEN] {len(results)} package(s) clean")
    print("=" * 86)
    return 1 if red else 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("packages", nargs="*",
                    help="package directory names (default: all under --features-dir)")
    ap.add_argument("--features-dir", default="docs/17-features")
    ap.add_argument("--json", action="store_true", help="machine-readable output")
    ap.add_argument("--no-master", action="store_true",
                    help="skip check 7 (the master register) and run checks 1-6 only")
    ap.add_argument("--adr-dir", default="docs/14-Engineering/ADR",
                    help="directory holding ADR-0*.md (check 9)")
    ap.add_argument("--board", default=".claude/handoff/BOARD.md",
                    help="decision register read by check 10")
    # WHY THIS FLAG EXISTS: checks 1-6 are RED on this repository today (9 of 14 packages),
    # and `package_red` wins the exit code. Without a way to run 9 and 10 alone, their
    # verdict is unobservable through `$?` -- which is the defect this script's own header
    # spends three notes on. This is the observable path, not a convenience.
    ap.add_argument("--edges-only", action="store_true",
                    help="run only checks 9 and 10 (ADR edges, decision mechanisms)")
    args = ap.parse_args()

    root = Path(__file__).resolve().parent.parent
    features = (root / args.features_dir) if not Path(args.features_dir).is_absolute() \
        else Path(args.features_dir)

    adr_dir = (root / args.adr_dir) if not Path(args.adr_dir).is_absolute() \
        else Path(args.adr_dir)
    board = (root / args.board) if not Path(args.board).is_absolute() \
        else Path(args.board)

    if args.edges_only:
        adr = check_adr_edges(adr_dir)
        dec = check_decision_mechanisms(board)
        if args.json:
            print(json.dumps({"adr_edges": adr, "decision_mechanisms": dec}, indent=2))
            edge_findings = (len(adr["dangling"]) + len(adr["superseded"])
                             + len(adr["missing_key"]) + len(dec["collisions"]))
        else:
            edge_findings = report_edges(adr, dec)
        return 5 if edge_findings else 0

    if not features.is_dir():
        print(f"trace-check: no such directory: {features}", file=sys.stderr)
        return 2

    if args.packages:
        dirs = [features / name for name in args.packages]
        for d in dirs:
            if not d.is_dir():
                print(f"trace-check: no such package: {d}", file=sys.stderr)
                return 2
    else:
        dirs = sorted(p for p in features.iterdir() if p.is_dir())

    if not dirs:
        print(f"trace-check: no packages found under {features}", file=sys.stderr)
        return 2

    global_index = build_global_index(features, root)
    results = [check_package(d, global_index) for d in dirs]

    # Check 7 reads the master specification, not the packages, so it is unaffected by
    # which packages were named on the command line and runs identically either way.
    master = None if args.no_master else check_master_register(root, features)

    # Check 8 reads the packages named, like checks 1-6.
    registers = [check_decision_registers(d) for d in dirs]

    # Checks 9 and 10 read two registers OUTSIDE docs/17-features and are unaffected by
    # which packages were named, exactly as check 7 is.
    adr = check_adr_edges(adr_dir)
    dec = check_decision_mechanisms(board)

    package_red = any(r["failures"] for r in results)
    master_findings = bool(master and master.get("total"))

    if args.json:
        payload: dict = {"packages": results}
        if master is not None:
            payload["master_register"] = master
        payload["decision_registers"] = registers
        payload["adr_edges"] = adr
        payload["decision_mechanisms"] = dec
        print(json.dumps(payload, indent=2))
        register_findings = sum(len(r["diverged"]) for r in registers)
        edge_findings = (len(adr["dangling"]) + len(adr["superseded"])
                         + len(adr["missing_key"]) + len(dec["collisions"]))
    else:
        if master is not None:
            report_master(master)
        register_findings = report_registers(registers)
        edge_findings = report_edges(adr, dec)
        report(results)

    # 1 keeps its original meaning — a package failed — so a caller testing `== 1` is
    # untouched by checks 7 and 8. 3 and 4 are answers to different questions, and they are
    # ordered rather than combined because an exit code carries one number.
    #
    # THE CONSEQUENCE, STATED SO IT IS NOT DISCOVERED: when this returns 1 or 3, check 8's
    # findings may ALSO be present and are not visible in the exit code. The section above is
    # where to look. A caller that cares specifically about register divergence should read
    # `--json`, not the exit status.
    # 5 is checks 9 and 10, added LAST so every existing meaning is untouched. It inherits
    # the same consequence the note above states, and worse: with checks 1-6 red on this
    # repository today, 1 always wins and 5 is never visible in a full run. `--edges-only`
    # exists so the new checks have an observable exit code of their own -- see the flag.
    if package_red:
        return 1
    if master_findings:
        return 3
    if register_findings:
        return 4
    return 5 if edge_findings else 0


if __name__ == "__main__":
    sys.exit(main())
