"""Deterministic catalogue of the SunCity_Clinics schema script.

PLANNING INSTRUMENT, NOT PRODUCT CODE. Emits a machine-readable catalogue so the HIS/ERP
migration plan rests on measurements rather than on assertions. Nothing here belongs in src/.

Run:
    python scripts/his-catalogue/parse_his_schema.py <script.sql> --out scripts/his-catalogue/catalogue
    python scripts/his-catalogue/parse_his_schema.py --self-test

=== THREE DESIGN RULES, EACH PAID FOR ===

1. THE ENCODING IS PART OF THE PARSE.
   The source is UTF-16LE. A line-anchored grep over the raw bytes returns a clean, confident ZERO
   for every pattern at once - not an error, a finding-shaped nothing. So this REFUSES a file that
   is not UTF-16, loudly, rather than producing an empty catalogue.

2. THE KEY IS THE BRACKETED, UNPARSED NAME.
   [dbo].[Pharmacy.Borrowing] and [Pharmacy].[Borrowing] are two DIFFERENT tables and both exist.
   Any tool that normalises the dotted name into schema-plus-table maps the first onto the second,
   and one of them is silently dropped or overwritten. Both names are valid, so nothing errors.

3. AN IDENTIFIER THAT WILL NOT ROUND-TRIP IS REFUSED, NEVER NORMALISED.
   A character class of [A-Za-z0-9_] does not DROP the awkward names - it RENAMES them, and renames
   them to something that reads as correct. RepEmployees'Vacations becomes RepEmployees; a perfectly
   believable procedure name. The row survives and the KEY is corrupted, so a count cannot catch it
   and both sides of a diff look plausible. Nine procedures and ten tables here carry apostrophes,
   hyphens, dollar signs or leading/trailing spaces.
"""

import argparse
import io
import json
import os
import re
import sys

# A bracketed identifier: everything between [ and the next ], no interpretation whatsoever.
BRACKETED = r"\[([^\]]*)\]"
QUALIFIED = re.compile(r"\[([^\]]*)\]\.\[([^\]]*)\]")

# A manifest entry is qualified for most object kinds and BARE for a schema:
#   /****** Object:  Table [dbo].[Patients] ...
#   /****** Object:  Schema [Finance] ...
# The first version demanded the qualified form and reported ZERO schemas. The floor caught it,
# which is the whole argument for asserting counts rather than listing what was found.
MANIFEST = re.compile(r"/\*+ Object:\s+(\w+)\s+\[([^\]]*)\](?:\.\[([^\]]*)\])?")
# Lenient for the same reason, verified not to change the count (1754 either way): the laxity costs
# nothing and removes the class rather than the instance.
CREATE_TABLE = re.compile(r"^\s*CREATE\s+TABLE\s*\[([^\]]*)\]\.\[([^\]]*)\]",
                          re.MULTILINE | re.IGNORECASE)
# ⚠ THE SAME DEFECT, FOUND BY THE GUARD ADDED FOR PROCEDURES, ON ITS FIRST RUN (T-218).
# Six views were lost to `^CREATE VIEW \[` with its literal single space: five carry LEADING TABS
# and one a DOUBLE space. Views had no floor and no cross-check either, so 171 of 177 had been
# passing as complete for as long as this parser has existed.
CREATE_VIEW = re.compile(r"^\s*CREATE\s+VIEW\s*\[([^\]]*)\]\.\[([^\]]*)\]",
                         re.MULTILINE | re.IGNORECASE)
# ⚠ TWO DEFECTS LIVED IN THIS ONE PATTERN AND COST 52 OF 1,347 PROCEDURES (T-218).
#
# It was `^CREATE\s+PROC(?:EDURE)?\s+\[`, and the script writes procedures three ways:
#
#   `CREATE PROCEDURE [Billing].[X]`      the form the pattern expected
#   ` CREATE proc   [Finance].[SPACCO3301]`   A LEADING SPACE -- 50 of them, defeated by `^CREATE`
#   `CREATE PROCEDURE[GL].[Sp_totalPatientsBill]`   NO SPACE AT ALL -- 2, defeated by `\s+`
#
# **The lost 52 have entirely ordinary names.** The awkwardness is in the whitespace around the KEYWORD,
# not in the identifier -- the nine apostrophe names parsed correctly throughout. A guess aimed at the
# awkward names would have been refuted while the defect stayed in place; comparing the manifest set
# against the parsed set found it in one pass, which is why `floors()` now does that for every artefact.
CREATE_PROC = re.compile(r"^\s*CREATE\s+PROC(?:EDURE)?\s*\[([^\]]*)\]\.\[([^\]]*)\]",
                         re.MULTILINE | re.IGNORECASE)
CREATE_SCHEMA = re.compile(r"^CREATE SCHEMA \[([^\]]*)\]", re.MULTILINE)

COLUMN = re.compile(r"^\s*\[([^\]]*)\]\s+\[([A-Za-z0-9_ ]+)\]\s*(\(([^)]*)\))?(.*)$")
FK_LINE = re.compile(
    r"ALTER TABLE \[([^\]]*)\]\.\[([^\]]*)\]\s+WITH (CHECK|NOCHECK)\s+ADD\s+CONSTRAINT \[([^\]]*)\]"
    r"\s+FOREIGN KEY\(([^)]*)\)", re.IGNORECASE)
REFERENCES = re.compile(r"REFERENCES \[([^\]]*)\]\.\[([^\]]*)\]\s*\(([^)]*)\)", re.IGNORECASE)


class RefusedIdentifier(Exception):
    """An identifier that cannot be represented without changing it."""


def read_source(path):
    """UTF-16 only. A UTF-8 file is REFUSED rather than parsed into an empty catalogue."""
    with open(path, "rb") as handle:
        head = handle.read(2)
    if head not in (b"\xff\xfe", b"\xfe\xff"):
        raise SystemExit(
            "REFUSED: %s does not begin with a UTF-16 BOM (got %r).\n"
            "The source script is UTF-16LE. Parsing it as UTF-8 yields ZERO matches for every\n"
            "pattern - a clean number that reads as a finding about the schema. Convert it, or\n"
            "point this at the real file." % (path, head))
    encoding = "utf-16-le" if head == b"\xff\xfe" else "utf-16-be"
    with io.open(path, encoding=encoding) as handle:
        return handle.read().replace("\r\n", "\n").lstrip("﻿")


def key_of(schema, name):
    """The catalogue key: bracketed and unparsed, so a dot inside a name cannot become a schema."""
    for part in (schema, name):
        if "]" in part:
            raise RefusedIdentifier("identifier contains a closing bracket: %r" % part)
    return "[%s].[%s]" % (schema, name)


def parse(text):
    catalogue = {
        "schemas": sorted({m.group(1) for m in CREATE_SCHEMA.finditer(text)}),
        "tables": {},
        "views": [],
        "procedures": [],
        "foreign_keys": [],
        "manifest": {},
    }

    for match in MANIFEST.finditer(text):
        kind = match.group(1)
        name = (key_of(match.group(2), match.group(3)) if match.group(3)
                else "[%s]" % match.group(2))
        catalogue["manifest"].setdefault(kind, []).append(name)

    catalogue["views"] = [key_of(m.group(1), m.group(2)) for m in CREATE_VIEW.finditer(text)]
    catalogue["procedures"] = [key_of(m.group(1), m.group(2)) for m in CREATE_PROC.finditer(text)]

    # Tables, with their column block. The block runs to the line that closes the CREATE.
    for match in CREATE_TABLE.finditer(text):
        key = key_of(match.group(1), match.group(2))
        block = text[match.end():]
        end = block.find("\n) ON ")
        if end < 0:
            end = block.find("\nGO")
        columns = []
        for line in block[:max(end, 0)].split("\n"):
            column = COLUMN.match(line)
            if not column:
                continue
            tail = column.group(5) or ""
            columns.append({
                "name": column.group(1),
                "type": column.group(2).strip(),
                "length": (column.group(4) or "").strip(),
                "nullable": "NOT NULL" not in tail.upper(),
                "identity": "IDENTITY" in tail.upper(),
            })
        # A dotted name that already exists as schema.table is the collision this refuses to hide.
        if key in catalogue["tables"]:
            raise RefusedIdentifier("duplicate table key %s - the catalogue would lose one" % key)
        catalogue["tables"][key] = columns

    for match in FK_LINE.finditer(text):
        child = key_of(match.group(1), match.group(2))
        with_check = match.group(3).upper()
        constraint = match.group(4)
        child_columns = re.findall(BRACKETED, match.group(5))
        rest = text[match.end():match.end() + 400]
        reference = REFERENCES.search(rest)
        if not reference:
            continue
        catalogue["foreign_keys"].append({
            "constraint": constraint,
            "child": child,
            "child_columns": child_columns,
            "parent": key_of(reference.group(1), reference.group(2)),
            "parent_columns": re.findall(BRACKETED, reference.group(3)),
            "with_check": with_check,
        })

    return catalogue


def floors(catalogue, text):
    """Anti-vacuity as NUMBERS, tight enough that PARTIAL degradation reddens.

    Every instrument failure in this loop has been partial - a pattern stopped seeing one spelling,
    one encoding, one file convention. None returned nothing, which is why each was believed.
    """
    manifest = catalogue["manifest"]
    checks = [
        ("manifest tables", len(manifest.get("Table", [])), 1754),
        ("manifest procedures", len(manifest.get("StoredProcedure", [])), 1347),
        ("manifest views", len(manifest.get("View", [])), 177),
        ("manifest schemas", len(manifest.get("Schema", [])), 38),
        ("parsed tables", len(catalogue["tables"]), 1754),
        # Added T-218. Its absence is what let 1,295 of 1,347 pass as complete.
        ("parsed procedures", len(catalogue["procedures"]), 1347),
        ("parsed views", len(catalogue["views"]), 177),
        ("parsed foreign keys", len(catalogue["foreign_keys"]), 1988),
        ("REFERENCES lines", len(REFERENCES.findall(text)), 1988),
        ("identity columns", text.count("IDENTITY("), 1541),
    ]
    failures = ["%s: %d, floor %d" % (n, got, want) for n, got, want in checks if got < want]

    # The manifest ENUMERATES and the CREATE statements MATCH. Disagreement means one of them has
    # started lying, and which one is not knowable from either alone - so it is a failure, not a note.
    #
    # ---- ⚠ THIS RAN FOR TABLES ALONE UNTIL T-218, AND PROCEDURES WERE SILENTLY 96% COMPLETE.
    #
    # Tables had a count AND this set comparison, and tables were never wrong. Foreign keys had a count.
    # **Procedures had neither, and 1,295 of 1,347 passed every check this file performed.** The mechanism
    # was right and the COVERAGE was the gap, which is why the fix extends it rather than replacing it.
    for kind, key in (("Table", "tables"), ("StoredProcedure", "procedures"), ("View", "views")):
        in_manifest = set(manifest.get(kind, []))
        parsed = set(catalogue[key])
        if in_manifest != parsed:
            failures.append(
                "manifest and CREATE %s disagree: %d only in manifest %s, %d only parsed %s"
                % (kind.upper(), len(in_manifest - parsed), sorted(in_manifest - parsed)[:5],
                   len(parsed - in_manifest), sorted(parsed - in_manifest)[:5]))

    # ---- ⚠ AND A CONTROL OVER THE FLOORS THEMSELVES, BECAUSE THE FLOORS HAD NO FLOOR.
    #
    # Fixing procedures closes one instance. The CLASS is that this function guards whatever somebody
    # thought of, and **nothing asserted that every artefact type in the catalogue HAS a guard** — which is
    # exactly how procedures went unguarded while the comment above warned about partial failure.
    #
    # This makes adding an artefact type without deciding how to guard it a RED PARSE rather than a silent
    # omission. It is the enumerate-the-set rule pointed at our own instrument.
    guarded = {"tables", "procedures", "views", "foreign_keys", "schemas", "manifest"}
    unguarded = sorted(set(catalogue) - guarded)
    if unguarded:
        failures.append(
            "artefact type(s) %s are in the catalogue and named in no floor: add a count and a "
            "manifest cross-check, or add them to `guarded` with a reason" % unguarded)

    return checks, failures


def self_test():
    """Fixtures for the three failures that produced the design rules."""
    ok = True

    # 1. THE COLLISION. Both of these exist in the real script.
    text = ("CREATE TABLE [dbo].[Pharmacy.Borrowing](\n\t[Id] [int] NOT NULL\n) ON [PRIMARY]\nGO\n"
            "CREATE TABLE [Pharmacy].[Borrowing](\n\t[Id] [int] NOT NULL\n) ON [PRIMARY]\nGO\n")
    result = parse(text)
    if len(result["tables"]) != 2:
        print("FAIL: the two Borrowing tables collapsed into %d" % len(result["tables"]))
        ok = False
    elif "[dbo].[Pharmacy.Borrowing]" not in result["tables"]:
        print("FAIL: the dotted name was normalised away")
        ok = False
    else:
        print("pass: [dbo].[Pharmacy.Borrowing] and [Pharmacy].[Borrowing] stay distinct")

    # 2. IDENTIFIERS THAT A CHARACTER CLASS WOULD RENAME RATHER THAN DROP.
    awkward = ("CREATE TABLE [dbo].[Users$](\n\t[Id] [int] NOT NULL\n) ON [PRIMARY]\nGO\n"
               "CREATE PROCEDURE [hr].[RepEmployees'Vacations] AS BEGIN SELECT 1 END\nGO\n"
               "CREATE PROCEDURE [dbo].[ PermissionRequestsReport ] AS BEGIN SELECT 1 END\nGO\n")
    result = parse(awkward)
    kept = list(result["tables"]) + result["procedures"]
    expected = ["[dbo].[Users$]", "[hr].[RepEmployees'Vacations]", "[dbo].[ PermissionRequestsReport ]"]
    missing = [name for name in expected if name not in kept]
    if missing:
        print("FAIL: identifiers lost or renamed: %s" % missing)
        ok = False
    else:
        print("pass: apostrophe, dollar and surrounding-space names survive verbatim")

    # 3. A FOREIGN KEY KEEPS ITS TRUST LEVEL. WITH NOCHECK is where the orphans are.
    fk = ("ALTER TABLE [Nursing].[NurseMaster]  WITH NOCHECK ADD  CONSTRAINT [FK_NurseMaster_Employee] "
          "FOREIGN KEY([EmployeeId])\nREFERENCES [Nursing].[Employee] ([Id])\nGO\n")
    result = parse(fk)
    if len(result["foreign_keys"]) != 1 or result["foreign_keys"][0]["with_check"] != "NOCHECK":
        print("FAIL: WITH NOCHECK not captured: %s" % result["foreign_keys"])
        ok = False
    else:
        print("pass: WITH NOCHECK is recorded, not flattened into 'has a foreign key'")

    return 0 if ok else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", nargs="?", help="the UTF-16 schema script")
    parser.add_argument("--out", help="directory for the catalogue")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()

    if args.self_test:
        return self_test()

    if not args.source:
        parser.error("a source script is required unless --self-test")

    text = read_source(args.source)
    catalogue = parse(text)
    checks, failures = floors(catalogue, text)

    for name, got, want in checks:
        print("%-24s %7d   floor %d" % (name, got, want))

    if failures:
        print("\nFLOOR BREACHED - the catalogue is not trustworthy:")
        for failure in failures:
            print("  " + failure)
        return 1

    if args.out:
        os.makedirs(args.out, exist_ok=True)
        with io.open(os.path.join(args.out, "catalogue.json"), "w", encoding="utf-8") as handle:
            json.dump(catalogue, handle, indent=1, sort_keys=True, ensure_ascii=False)
        print("\nwritten to %s" % args.out)

    return 0


if __name__ == "__main__":
    sys.exit(main())
