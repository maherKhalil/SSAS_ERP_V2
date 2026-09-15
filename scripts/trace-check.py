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
import subprocess
import sys
from collections import defaultdict
from pathlib import Path, PurePosixPath

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
    # Declared from what the package CONTAINS, not from what it will contain: zero BRULE-
    # identifiers and an FP-014-shaped skeleton. It is two files deep -- README and open
    # decisions -- and `DEC-L-061` failed it as UNDECLARED within an hour of its creation,
    # which is the rule working on the newest package in the repository.
    "FP-015-self-service": "modern",
}

# BRULE must precede BR in the alternation. `BR-` cannot match inside `BRULE-` because the
# hyphen is required, but ordering longest-first removes the question rather than answering
# it -- and the next space added may not be so safely distinguishable.
HOME_FILES["BRULE"] = ("business-rules.md",)
SPACES = ("REQ", "BRULE", "BR", "AC", "TS", "DEC", "OD")

# What may LEAD a chain row. The modern convention leads with REQ-; the legacy one
# leads with DEC- or BR-, and FP-001 carries no REQ- identifier at all.
CHAIN_ANCHORS = ("REQ", "BR", "BRULE", "DEC")


# --------------------------------------------------------------------------------------
# WHO OWNS AN IDENTIFIER SPACE (T-064)
#
# DECLARED, NEVER COUNTED. This replaced a majority heuristic: a module was "owned" by a
# package if that package held at least a quarter of the identifiers carrying its token.
# `DOC` therefore belonged to FP-009 because FP-009 has 96 DOC identifiers and FP-010 has 25
# -- so FP-010's own scenarios read as somebody else's, and FP-009's citation of them read as
# an internal orphan. **A majority count deciding who owns a namespace is an answer over a
# domain nobody established**, which is what `DEC-L-061` removed for conventions.
#
# `DOC` HAS TWO OWNERS, BY A RECORDED AGREEMENT. FP-009's matrix carries a handover table
# above the sentence "Every one kept its number. Neither package will reallocate an
# identifier the other used." The register says so; the checker now says so too.
#
# MINIMAL BY CONSTRUCTION. This answers one question -- WHO MAY DEFINE AN IDENTIFIER IN THIS
# SPACE -- and nothing else. It is not an ontology, and a space in no entry FAILS rather than
# falling back to a guess.
SPACE_OWNERS: dict[str, tuple[str, ...]] = {
    "IAM": ("FP-001-identity-access",),
    "AUTH": ("FP-002-authentication-token-lifecycle",),
    "TEN": ("FP-003-tenant-lifecycle",),
    "LOC": ("FP-004-localization",),
    "CMP": ("FP-005-company-legal-entity",),
    "EMP": ("FP-006-hr-employee",),
    "DEP": ("FP-007-hr-department",),
    "POS": ("FP-008-hr-position",),
    # The one shared space, and the reason this table exists rather than a count.
    "DOC": ("FP-009-hr-employee-import-export", "FP-010-hr-employee-documents"),
    "GL": ("FP-011-gl-foundation",),
    "PAY": ("FP-012-payroll",),
    "ATT": ("FP-013-attendance",),
    "SUB": ("FP-014-subscription",),
    "SS": ("FP-015-self-service",),
}

# Extra home files a SINGLE package declares, never added globally. FP-010 defines its test
# scenarios in `carried-analysis.md` -- legitimately, it is a carried-forward analysis package
# with no `test-scenarios.md` -- and no other package should inherit that.
HOME_OVERRIDES: dict[str, dict[str, tuple[str, ...]]] = {
    "FP-010-hr-employee-documents": {"TS": ("test-scenarios.md", "carried-analysis.md")},
}


# --------------------------------------------------------------------------------------
# HOW A PACKAGE ALLOCATES ITS NUMBERS (T-066)
#
# `sequential` -- 0001 upward with no gaps. `block` -- themed blocks opening at round
# boundaries, whose tails are never filled.
#
# ESTABLISHED, NOT ASSUMED. `git log -S` over fourteen identifiers across the six packages
# that failed contiguity found **zero commits** that ever added or removed one: they were
# never deleted and never lost, they were **never allocated**. And 115 of 115 gaps end at a
# round-number boundary while every sequential package has none.
#
#     FP-001   Domain 0001-0015 | Tenant selection 0020-0024 | API 0030-0036 | Persistence 0040-0048
#
# THIS IS NOT A BLANKET DOWNGRADE OF CONTIGUITY. A sequential package that grows a gap must
# still fail -- dropping the assertion everywhere would trade a false positive for a real
# hole. Contiguity is ASSERTED where the declaration says sequential and REPORTED where it
# says block, and **a package with no entry FAILS**, exactly as an undeclared convention and
# an undeclared space do.
ALLOCATION: dict[str, str] = {
    "FP-001-identity-access": "block",
    "FP-002-authentication-token-lifecycle": "block",
    "FP-003-tenant-lifecycle": "block",
    "FP-004-localization": "block",
    "FP-005-company-legal-entity": "block",
    "FP-006-hr-employee": "block",
    "FP-007-hr-department": "block",
    "FP-008-hr-position": "block",
    "FP-009-hr-employee-import-export": "block",
    "FP-010-hr-employee-documents": "block",
    "FP-011-gl-foundation": "sequential",
    "FP-012-payroll": "sequential",
    "FP-013-attendance": "sequential",
    "FP-014-subscription": "sequential",
    "FP-015-self-service": "sequential",
}


def allocation_of(package: str) -> str | None:
    return ALLOCATION.get(package)


def owners_of(module: str) -> tuple[str, ...]:
    return SPACE_OWNERS.get(module, ())


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
    homes = dict(CONVENTIONS[name])
    homes.update(HOME_OVERRIDES.get(package, {}))
    return homes

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
        if len(cells) < 3:
            continue
        # ANCHORED BY ANY CHAIN-LEADING SPACE, NOT BY REQ- ALONE. T-062, piece 3.
        # Under the legacy convention the chain is led by DEC- or BR-; FP-001's matrix has
        # no REQ- identifier anywhere. Measured in T-061: eight of fourteen packages had NO
        # parseable chain, so coverage rules 3 and 4 had never run on them.
        anchor = [i for i in scan_ids(cells[0]) if i.space in CHAIN_ANCHORS]
        # A PROSE CAPABILITY NAME IS A LEGITIMATE ANCHOR. T-064, from T-063's measurement.
        # FP-011's coverage rows read `| Administer grants no functional permission |
        # ADR-025 d8 | AC-GL-0017 | TS-GL-0022 |` -- criteria and scenario present, first
        # cell a capability name carrying no identifier. FP-002, FP-005 and FP-006 lead their
        # chain tables the same way. Five criteria read as untraced while being traced on the
        # row that names them, and the five were CONSECUTIVE, which looked systematic and was:
        # **the systematic thing was the authoring order of the criteria file, not coverage.**
        # Measured: 33 failures to 25, nothing worse.
        criteria_probe = next((c for c in cells[1:] if cell_ids(c, "AC")), "")
        tests_probe = next((c for c in cells[1:] if cell_ids(c, "TS")), "")
        if not anchor and not (criteria_probe and tests_probe):
            continue
        # CRITERIA AND TESTS ARE FOUND BY CONTENT, NOT BY COLUMN OFFSET. The shipped parser
        # read criteria from cell 2 and tests from cell 3; the real tables put them in
        # columns 3 through 6 -- FP-003's chain table has seven columns. Reading by offset
        # is why a table could match the anchor and still yield nothing.
        criteria_cell = next((c for c in cells[1:] if cell_ids(c, "AC")), "")
        tests_cell = next((c for c in cells[1:] if cell_ids(c, "TS")), "")
        # A ROW THAT CARRIES NEITHER IS NOT A CHAIN ROW. Every matrix file here holds
        # several tables, and some map requirements to CODE SYMBOLS and test class names --
        # a legitimate different mapping. Falling back to fixed column offsets for those
        # rows makes rule 3 fire on every one of them: it took 29 failures to 76 instead of
        # the 35 the T-061 dry run measured, because the dry run had no such fallback and
        # the shipped version did. **The measured behaviour is the specification.**
        # ...UNLESS IT DECLARES A GAP. `REQ-SUB-0026`'s row carries an em-dash in both the
        # criteria and the tests cell -- a DECLARED absence, which the matrices insist on
        # and which this check reports rather than fails. Dropping such a row made the
        # requirement "absent from the chain table" and turned FP-014 red, which is a
        # package saying honestly that it has a gap being failed for saying so.
        # NO POSITIONAL FALLBACK AND NO SKIPPING. A row that names neither a criterion nor a
        # scenario yields EMPTY cells, and `is_declared_gap("")` is true -- so it is reported
        # as a declared gap rather than failed, which is how the matrices already treat an
        # em-dash. That is exactly what the T-061 dry run did, and reproducing the measured
        # behaviour is the point: three attempts to be cleverer than it (a positional
        # fallback, then a row skip, then a narrowed fallback) gave 76, 34 and 56 failures
        # against its 35, and each looked reasonable while I wrote it.
        rules_cell = next((c for c in cells[1:] if cell_ids(c, "BR") or cell_ids(c, "BRULE")),
                          cells[1])
        rows.append(
            {
                # An unanchored row still needs a label for the coverage messages; the
                # first cell is what a reader would call it.
                "requirements": sorted(i.key for i in anchor) or [f"(row: {cells[0][:40]})"],
                "rules": rules_cell,
                "criteria": criteria_cell,
                "tests": tests_cell,
                "decisions": cells[4] if len(cells) > 4 else "",
                "line": raw.rstrip(),
            }
        )
    return rows


def cell_ids(cell: str, space: str) -> list[str]:
    return sorted({i.key for i in scan_ids(cell) if i.space == space})


# T-062, piece 2. Both range spellings the corpus actually uses:
#   AC-IAM-0013-0015           bare upper bound
#   AC-IAM-0013-AC-IAM-0015    fully qualified upper bound   <- FP-001 writes this one
SPAN_RE = re.compile(
    r"\b(" + "|".join(SPACES) + r")-([A-Z]{2,4})-(\d{4})`?\s*[–—-]\s*"
    r"`?(?:(?:" + "|".join(SPACES) + r")-[A-Z]{2,4}-)?(\d{4})\b"
)


def cell_ids_expanded(cell: str, space: str) -> list[str]:
    """`cell_ids` plus the identifiers a RANGE implies but never spells.

    `AC-IAM-0013`-`AC-IAM-0015` on a row with two scenarios covers three criteria; only
    two of them appear as literal text, so the middle one read as untraced. Check 5
    already objects to ranges standing in for citations -- this stops rule 4 producing a
    SECOND, different complaint about the same notation.
    """
    keys = set(cell_ids(cell, space))
    for sp, module, lo, hi in SPAN_RE.findall(cell):
        if sp != space:
            continue
        a, b = int(lo), int(hi)
        # A malformed or reversed span expands to nothing rather than to a guess, and an
        # absurd one is refused: a typo must not silently mark two hundred criteria covered.
        if a <= b and (b - a) <= 200:
            keys.update(f"{sp}-{module}-{n:04d}" for n in range(a, b + 1))
    return sorted(keys)


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


def declared_modules(package: str) -> set[str]:
    """The identifier spaces this package is DECLARED to own. No counting, no fallback."""
    return {module for module, owners in SPACE_OWNERS.items() if package in owners}


def master_register_modules(repo_root: Path) -> set[str]:
    """Module tokens the MASTER registers define -- `PLT`, `HR` and the like.

    Kept separate from the global index on purpose: the global index absorbs every package's
    own home files, so testing a module against it answers "did anyone write this down",
    which is true of a planted identifier the moment it is planted. The first version of the
    undeclared-space check did exactly that and never fired.
    """
    catalog = repo_root / "docs" / "00-Master-Product-Specification"
    if not catalog.is_dir():
        return set()
    modules: set[str] = set()
    for f in catalog.rglob("*.md"):
        modules.update(i.module for i in scan_ids(f.read_text(encoding="utf-8", errors="replace")))
    return modules


def check_package(pkg_dir: Path, global_index: dict[str, set[str]] | None = None,
                  master_modules: set[str] | None = None) -> dict:
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
    result["allocation"] = allocation_of(pkg_dir.name)
    if result["allocation"] is None:
        result["failures"].append(
            f"UNDECLARED ALLOCATION — {pkg_dir.name} appears in no ALLOCATION entry. "
            f"Contiguity cannot be judged without knowing whether numbers run sequentially "
            f"or open in blocks; unknown must not mean tolerated."
        )
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

    mine = declared_modules(pkg_dir.name)
    result["own_modules"] = sorted(mine)

    # A SPACE THIS PACKAGE DEFINES BUT NOBODY DECLARED IS A FAILURE, not a fallback. The
    # same rule as an undeclared convention: unknown must not mean tolerated.
    defined_modules = {
        ident.module
        for fname, text in texts.items()
        if fname in {h for homes in homes_map.values() for h in homes}
        for ident in scan_ids(text)
        if ident.space in ("REQ", "AC", "TS", "BR", "BRULE")
    }
    for module in sorted(defined_modules - mine):
        # A CITATION IS NOT A DEFINITION. `BR-PLT-0001` and `REQ-HR-0100` live in the master
        # registers under docs/00-Master-Product-Specification and are cited by packages in
        # their own home files; the first version of this check called seventeen of those
        # undeclared spaces. If the global index resolves the space, somebody owns it and it
        # is not this package's to declare.
        if not owners_of(module) and module not in (master_modules or set()):
            result["failures"].append(
                f"UNDECLARED SPACE — {module}- identifiers are defined in this package's "
                f"home files and {module} appears in no SPACE_OWNERS entry"
            )
    if not mine:
        result["warnings"].append(
            "this package is declared to own no identifier space — numbering and orphan "
            "rules are skipped for it; coverage still checked"
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
            # A SHARED SPACE RESOLVES AGAINST ITS CO-OWNERS. `DOC` belongs to FP-009 AND
            # FP-010 by a recorded agreement, so FP-009 citing a scenario FP-010 defines is
            # an external reference, not an orphan -- even though the space is "mine".
            co_owned = len(owners_of(key.split("-")[1])) > 1
            if co_owned and key in global_index.get(space, set()):
                result["external_refs"].append(f"{key} (cited in {where}, co-owned space)")
                continue
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
            # CONTIGUITY IS NOT ASSERTABLE PER PACKAGE FOR A CO-OWNED SPACE. FP-009 holds
            # the lower `DOC` numbers and FP-010 the upper ones, by the same recorded
            # agreement that neither reallocates the other's -- so each package alone looks
            # full of holes and the two together are contiguous. Asserting it per package
            # would fail both for honouring the agreement. Same reasoning as BRULE- above:
            # a rule whose domain was never established is not asserted, it is reported.
            co_owned_space = len(owners_of(module)) > 1
            # ASSERTED WHERE THE DECLARATION SAYS SEQUENTIAL, REPORTED WHERE IT SAYS BLOCK.
            # A block-allocated package's unused tails are not gaps; a sequential package
            # that grows one still fails, which is why this is a per-package declaration
            # rather than switching the rule off.
            block_allocated = result.get("allocation") == "block"
            sink = (result["warnings"]
                    if space == "BRULE" or co_owned_space or block_allocated
                    else result["failures"])
            span_note = ("  (co-owned space; contiguity spans its owners, not this package)"
                         if co_owned_space else "")
            if lo != 1:
                sink.append(
                    f"CONTIGUITY {space}-{module} starts at {lo:04d}, not 0001"
                    + ("  (BRULE- numbering is unestablished; reported, not failed)"
                       if space == "BRULE" else span_note)
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
        # CORRECTED T-062. This said the MATRIX maps to code symbols rather than to AC-/TS-
        # identifiers. It described the rows the parser MATCHED, not the file: FP-007's
        # matrix holds four chain-bearing tables and 24 rows pairing an AC- with a TS-, and
        # the claim was true of its first table only.
        #
        # **A declaration that is wrong about the corpus is harder to doubt than a check**,
        # because it reads as something a person established. This one was also buying
        # FP-007's green until rule 4 learned to read `test-scenarios.md`; it no longer buys
        # anything, and it is corrected because the next person to widen the parser would
        # have read it and believed it.
        result["warnings"].append(
            f"the {len(chain)} chain row(s) the parser matched in {matrix_name} carry no "
            f"AC-/TS- identifiers — they map to code symbols and test class names, so "
            f"coverage rules 3 and 4 are not applied to THOSE ROWS. The file may pair "
            f"criteria with scenarios in tables this parser does not match; orphan and "
            f"contiguity rules still applied"
        )
        chain = []

    for row in chain:
        req_label = ", ".join(row["requirements"])
        req_in_chain.update(row["requirements"])
        criteria = cell_ids_expanded(row["criteria"], "AC")
        tests = cell_ids_expanded(row["tests"], "TS")

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

    # ---- RULE 4 LOOKS WHERE COVERAGE IS ACTUALLY RECORDED. T-062, piece 1.
    #
    # Rule 4 was WRONG ABOUT A FACT: it assumed `AC -> TS` is recorded only in the matrix.
    # FP-007 and FP-008 record it in `test-scenarios.md`, where every scenario row names
    # the criterion it covers:
    #
    #     | TS-DEP-0037 | S | Clearing a manager leaves ... | AC-DEP-0022 |
    #
    # Measured before this changed (T-061): widening the chain parser alone would have
    # failed **55 criteria that are fully covered**, across two correctly-specified
    # packages, against 13 real gaps. **A checker that cries wolf at correct work gets
    # switched off, and then the 13 are invisible for a different reason.**
    #
    # This is a RULE fix, not a parser fix, and the distinction is `DEC-L-061`: the rule
    # was wrong about where coverage lives, so the rule changes.
    #
    # THE PAIRING MUST BE ON ONE ROW. A file-wide "this AC and some TS both appear
    # somewhere" test would make rule 4 unfalsifiable -- every criterion co-occurs with
    # some scenario in a file that lists both.
    ts_name = "test-scenarios.md"
    ac_via_scenarios: set[str] = set()
    if ts_name in texts:
        for line in texts[ts_name].split("\n"):
            if not line.lstrip().startswith("|"):
                continue
            acs = set(cell_ids_expanded(line, "AC"))
            if acs and cell_ids_expanded(line, "TS"):
                ac_via_scenarios.update(acs)
        ac_with_test.update(ac_via_scenarios)
    result["ac_via_scenarios"] = len(ac_via_scenarios)

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
        alloc = r.get("allocation")
        if alloc == "block":
            print("  Allocation: BLOCK — themed blocks at round boundaries; unused tails are "
                  "not gaps, so contiguity is REPORTED, not asserted")
        elif alloc == "sequential":
            print("  Allocation: SEQUENTIAL — 0001 upward with no gaps; contiguity is ASSERTED")
        else:
            print("  Allocation: UNDECLARED — this package is in no ALLOCATION entry")

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


# --------------------------------------------------------------------------------------
# THE BASELINE GATE (T-065)
#
# NOT A HARD ZERO, AND THAT IS THE WHOLE DESIGN. Eleven failures stand today and every one
# is work a package has already declared pending. **A gate permanently red on declared work
# is a gate switched off by the second week** -- which is exactly how nine red packages went
# unremarked for weeks before anyone looked.
#
# So: red when a package's count RISES above its committed baseline. Silent when it holds.
# **Improvement ratchets** -- a package that improves lowers its own baseline on the next
# green run, or the first person to fix something hands the next person room to break it.
#
# The instrument writes the file and the coder commits it, for the reasons condition 4's
# baseline already established: it cannot drift while runs happen, and the delta lands in
# the diff where review sees it.
NL = chr(10)
BASELINE_HEADER = (
    "# Written by scripts/trace-check.py --baseline --update-baseline on a clean run." + NL
    + "# package|failures" + NL
)


def read_baseline(path: Path) -> dict[str, int]:
    if not path.is_file():
        return {}
    out: dict[str, int] = {}
    for line in path.read_text(encoding="utf-8", errors="replace").split(NL):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        name, _, count = line.partition("|")
        if count.strip().isdigit():
            out[name.strip()] = int(count.strip())
    return out


def report_baseline(results: list[dict], path: Path, update: bool) -> int:
    """Prints the comparison and returns the number of packages that REGRESSED."""
    base = read_baseline(path)
    current = {r["package"]: len(r["failures"]) for r in results}
    regressed, improved, new = [], [], []
    for pkg in sorted(current):
        now = current[pkg]
        was = base.get(pkg)
        if was is None:
            new.append((pkg, now))
        elif now > was:
            regressed.append((pkg, was, now))
        elif now < was:
            improved.append((pkg, was, now))

    print()
    print("=" * 86)
    print("TRACE BASELINE — red on a RISE, never on the standing count")
    print("=" * 86)
    if not base:
        print(f"  no baseline at {path} — it is written by the first non-regressing run")
    for pkg, was, now in regressed:
        print(f"  !!! REGRESSION: {pkg} {was} -> {now}")
    for pkg, was, now in improved:
        print(f"  improved: {pkg} {was} -> {now}  (baseline lowers; it does not rise again)")
    for pkg, now in new:
        print(f"  new package: {pkg} at {now}")
    held = len(current) - len(regressed) - len(improved) - len(new)
    print(f"  {held} package(s) unchanged, {sum(current.values())} failure(s) standing")

    if regressed:
        print("  baseline NOT updated: a regression must not become the new normal.")
        return len(regressed)
    if update:
        # RATCHET: never write a number higher than the one already committed. A package
        # that got worse is caught above; a package that got better lowers its own bar.
        merged = dict(base)
        for pkg, now in current.items():
            merged[pkg] = min(now, base.get(pkg, now))
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(BASELINE_HEADER + "".join(
            f"{k}|{v}" + NL for k, v in sorted(merged.items())),
            encoding="utf-8", newline="")
        print(f"  baseline updated at {path} — COMMIT IT WITH YOUR WORK.")
    return 0


# --------------------------------------------------------------------------------------
# THE `DEC-L-082` ADVISORY (T-106)
# --------------------------------------------------------------------------------------
#
# `DEC-L-082`: **a citation resolves; it does not validate.** When a cited identifier's MEANING
# changes, every citation of it becomes a claim nobody re-checked -- and nothing notices, because
# the reference still resolves.
#
# T-102 changed `AC-ATT-0032` from an absence criterion to an exact inventory. Eight files cited
# it. Three of them were still reading it as an absence, and finding those three took T-103,
# T-104 and T-105. **Run against T-102's own commit, this advisory lists all eight.**
#
# ---- WHAT IT KNOWS, AND THE LIMIT IS THE DESIGN RATHER THAN A SHORTFALL.
#
# **The definition/citation discrimination in this script is by FILE, not by line.** `check_package`
# marks an identifier defined when it appears ANYWHERE in a home file; there is no line-level
# notion of a definition here, and no reuse creates one.
#
# So this reports what it actually saw -- *a line in a home file changed and names this
# identifier* -- and NEVER *"the definition changed"*. **A prose CITATION sitting inside a home
# file is indistinguishable from a definition** (`FP-013/acceptance-criteria.md:120` cites
# `AC-ATT-0032` inside `AC`'s own home file), so this over-reports. For something that reports and
# never fails, over-reporting is the correct direction to be wrong -- but the WORDING is what
# keeps it honest, which is why the output states the reason rather than leaving it to be deduced.
#
# **`DEC-L-074`, one layer down: the sweep cannot tell a claim from a quotation of a claim.**
#
# ---- WHY IT KEYS ON HOME FILES, AND NOT ON EVERY MENTION.
#
# An advisory that fired on every identifier anywhere would fire on every commit and be switched
# off in a week. **A change in a home file is the only signal available that an identifier's
# meaning may have moved**; a change anywhere else is a citer being edited, which is the reader's
# own business.

FLOOR_LINE = ("this advisory keys on IDENTIFIERS -- it sees a fact duplicated ACROSS a citation "
              "and is blind to a fact duplicated WITHOUT one")


def _git_stdout(argv: list[str], cwd: Path) -> list[str] | None:
    """`None` on any failure. An advisory that crashes a gate is worse than one that is quiet."""
    try:
        done = subprocess.run(["git", *argv], cwd=str(cwd), capture_output=True, text=True,
                              encoding="utf-8", errors="replace", timeout=120)
    except (OSError, subprocess.SubprocessError):
        return None
    return done.stdout.splitlines() if done.returncode == 0 else None


def changed_home_identifiers(base: str, features: Path,
                             repo_root: Path) -> list[tuple[str, str, str]] | None:
    """`(package, filename, identifier)` for ADDED lines in a home file naming an OWNED identifier.

    `git diff <base> -- <path>` with no second commit compares base to the WORKING TREE, which is
    deliberate and is what makes this an advisory rather than a post-mortem: it fires while the
    amendment is still being written, not after it is committed. `gate.sh` uses the same source
    for condition 4 and says so at its line 190.
    """
    try:
        rel = features.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError:
        return None

    diff = _git_stdout(["diff", "--unified=0", base, "--", rel], repo_root)
    if diff is None:
        return None

    hits: set[tuple[str, str, str]] = set()
    package = filename = None

    for line in diff:
        if line.startswith("+++ "):
            path = line[4:].strip()
            if path == "/dev/null":
                package = filename = None
                continue
            parts = PurePosixPath(path[2:] if path.startswith("b/") else path).parts
            package, filename = (parts[-2], parts[-1]) if len(parts) >= 2 else (None, None)
            continue
        if not line.startswith("+") or line.startswith("+++") or package is None:
            continue

        homes = homes_for(package)
        owned = declared_modules(package)
        for ident in scan_ids(line[1:]):
            # BOTH conditions, and the second is not decoration. A package's home file may cite a
            # NEIGHBOUR's identifier -- FP-013's acceptance-criteria.md names `AC-SS-0005` -- and
            # that is a citation however it is spelled, because FP-013 cannot define an `SS` one.
            if filename in homes.get(ident.space, ()) and ident.module in owned:
                hits.add((package, filename, ident.key))

    return sorted(hits)


def citer_index(repo_root: Path) -> dict[str, set[str]]:
    """Every identifier to every file naming it, in ONE walk.

    One pass, not one grep per identifier: a full walk is ~1s against a ~90s gate, while grepping
    per identifier is what makes an advisory somebody switches off.

    `CITATION_ROOTS` is reused rather than reinvented, and it earns something for free here:
    `.claude/` is outside it, so handoff result files -- dated records, `DEC-L-071` -- drop off
    every list without a special case.
    """
    index: dict[str, set[str]] = defaultdict(set)
    for root_name in CITATION_ROOTS:
        base = repo_root / root_name
        if not base.is_dir():
            continue
        for path in base.rglob("*"):
            if path.suffix not in CITATION_SUFFIXES or not path.is_file():
                continue
            if SKIP_PARTS.intersection(path.parts):
                continue
            for ident in scan_ids(path.read_text(encoding="utf-8", errors="replace")):
                index[ident.key].add(path.relative_to(repo_root).as_posix())
    return index


def report_citers(base: str, features: Path, repo_root: Path) -> int:
    """Always 0. This reports and never fails -- see the header note and `DEC-L-082`."""
    hits = changed_home_identifiers(base, features, repo_root)

    if hits is None:
        print(f"--- DEC-L-082 advisory: NOT RUN -- no diff against '{base}'")
        print(f"    ({FLOOR_LINE})")
        return 0

    # AN EXPLICIT LINE, NEVER SILENCE. Absence must not read as not-applicable, which is the
    # failure this loop has recorded more than any other.
    if not hits:
        print("--- DEC-L-082 advisory: no identifier-bearing line changed in any home file")
        print(f"    ({FLOOR_LINE})")
        return 0

    index = citer_index(repo_root)
    try:
        features_rel = features.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError:
        features_rel = features.as_posix()

    print(f"--- DEC-L-082 advisory: {len(hits)} identifier(s) named on a changed line in a home file")
    print("    A LINE CHANGED AND NAMES THESE. Whether the MEANING moved is yours to judge.")

    for package, filename, key in hits:
        home = f"{features_rel}/{package}/{filename}"
        citers = sorted(index.get(key, set()) - {home})
        print(f"    {key} -- changed in {filename} ({package}) -- {len(citers)} citer(s)")
        for citer in citers:
            print(f"      {citer}")

    print("    WHAT THIS DOES NOT KNOW:")
    print("      * The discrimination is by FILE, not by line -- an identifier counts as defined")
    print("        when it appears anywhere in a home file. A prose CITATION inside a home file is")
    print("        indistinguishable from a DEFINITION, so this OVER-REPORTS. DEC-L-074, one layer")
    print("        down, in the instrument.")
    print(f"      * THIS {FLOOR_LINE[5:]}.")
    print("        A document and a comment holding one fact with no identifier between them are")
    print("        invisible here and always will be -- that is the boundary, not a gap to close.")
    print("      * It reports. It never fails the gate.")
    return 0


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
    ap.add_argument("--baseline", metavar="FILE",
                    help="compare per-package failure counts against FILE and fail on a RISE")
    ap.add_argument("--update-baseline", action="store_true",
                    help="with --baseline: rewrite it when nothing regressed (ratchets down only)")
    ap.add_argument("--edges-only", action="store_true",
                    help="run only checks 9 and 10 (ADR edges, decision mechanisms)")
    # THE `DEC-L-082` ADVISORY (T-106). Reports and never fails, so it takes the earliest
    # return in main and cannot reach any exit code but 0.
    ap.add_argument("--citers", metavar="BASE",
                    help="advisory: list citers of every identifier named on a changed line in a "
                         "home file, diffed against BASE (a commit-ish). Reports, never fails.")
    args = ap.parse_args()

    root = Path(__file__).resolve().parent.parent
    features = (root / args.features_dir) if not Path(args.features_dir).is_absolute() \
        else Path(args.features_dir)

    adr_dir = (root / args.adr_dir) if not Path(args.adr_dir).is_absolute() \
        else Path(args.adr_dir)
    board = (root / args.board) if not Path(args.board).is_absolute() \
        else Path(args.board)

    # FIRST, and deliberately: this answers a different question from every check below and
    # must never inherit their exit codes. See `DEC-L-082` and the section above `report_citers`.
    if args.citers:
        return report_citers(args.citers, features, root)

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
    master_modules = master_register_modules(root)
    results = [check_package(d, global_index, master_modules) for d in dirs]

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
    # `--baseline` ASKS A DIFFERENT QUESTION AND GETS A DIFFERENT ANSWER: 6 on a rise, 0
    # otherwise, deliberately overriding the 1/3/4/5 codes below.
    #
    # Without the override the gate would go red on the STANDING count -- eleven
    # declared-pending items today -- which is the hard zero the ruling rejects and the exact
    # shape that gets a gate switched off by the second week. A caller that wants the
    # standing count runs WITHOUT `--baseline` and reads 1; a caller that wants "did anything
    # get worse" passes it and reads 6.
    if args.baseline:
        base_path = (root / args.baseline) if not Path(args.baseline).is_absolute() else Path(args.baseline)
        return 6 if report_baseline(results, base_path, args.update_baseline) else 0

    if package_red:
        return 1
    if master_findings:
        return 3
    if register_findings:
        return 4
    return 5 if edge_findings else 0


if __name__ == "__main__":
    sys.exit(main())
