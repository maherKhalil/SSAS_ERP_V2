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

    Codes 0-2 keep the meaning they have always had. A caller testing `== 1` is unaffected
    by check 7; a caller testing `!= 0` sees it. That is the intended split — the master
    register is a different question from package health and deserves a different answer.
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

SPACES = tuple(HOME_FILES)

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
            for space, homes in HOME_FILES.items():
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
            if fname in HOME_FILES[ident.space]:
                defined[ident.space].add(ident.key)

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
        homes = HOME_FILES[space]
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
            if lo != 1:
                result["failures"].append(
                    f"CONTIGUITY {space}-{module} starts at {lo:04d}, not 0001"
                )
            if missing:
                result["failures"].append(
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
    args = ap.parse_args()

    root = Path(__file__).resolve().parent.parent
    features = (root / args.features_dir) if not Path(args.features_dir).is_absolute() \
        else Path(args.features_dir)

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

    package_red = any(r["failures"] for r in results)
    master_findings = bool(master and master.get("total"))

    if args.json:
        payload: dict = {"packages": results}
        if master is not None:
            payload["master_register"] = master
        print(json.dumps(payload, indent=2))
    else:
        if master is not None:
            report_master(master)
        report(results)

    # 1 keeps its original meaning — a package failed — so a caller testing `== 1` is
    # untouched by check 7. 3 is the new answer to a different question.
    if package_red:
        return 1
    return 3 if master_findings else 0


if __name__ == "__main__":
    sys.exit(main())
