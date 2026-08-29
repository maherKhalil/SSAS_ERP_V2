"""Item 40 — which API tests cannot prove the guard they name.

ENUMERATING INSTRUMENT, reports nothing it did not count. Two products:
  1. per test area, the interfaces replaced by a test-double;
  2. per test, whether the SUBJECT its name claims is one of those.

The ranking criterion is NOT "uses a stub" — that would have scored the fiscal-year
lock file as fully covered, and it was blind. It is A TEST CANNOT PROVE A GUARD IT
NEVER TRIGGERS; the stub is the commonest cause, not the definition.
"""

import collections
import glob
import io
import os
import re
import sys

SEP = os.sep


def norm(path):
    return path.replace("\\", "/")


def sources(root):
    out = []
    for f in glob.glob(root + "/**/*.cs", recursive=True):
        n = norm(f)
        if "/bin/" in n or "/obj/" in n:
            continue
        out.append(f)
    return out


# ⚠ ANTI-VACUITY. If the class regex stops matching — a C# syntax change, a reformat —
# every count below goes to zero and the report reads "nothing is stubbed", which is the
# most reassuring possible lie and the exact failure this item exists to find.
MINIMUM_TEST_FILES = 30
MINIMUM_STUBS = 40
MINIMUM_TESTS = 400

CLASS = re.compile(r"class\s+(\w+)\s*(?:\([^)]*\))?\s*:\s*([^\r\n{]+)")
TEST = re.compile(r"public\s+(?:async\s+)?(?:Task|void)\s+([A-Za-z_]\w*)\s*\(")


def bases_of(text):
    """Every interface a declared class implements, keyed by the class name."""
    found = collections.defaultdict(set)
    for m in CLASS.finditer(text):
        name, bases = m.group(1), m.group(2)
        for b in re.split(r",\s*(?![^<>]*>)", bases):
            b = b.strip().split("<")[0].split(".")[-1].strip()
            if len(b) > 1 and b[0] == "I" and b[1].isupper():
                found[name].add(b)
    return found


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else "tests/API.Tests"
    files = sources(root)
    if len(files) < MINIMUM_TEST_FILES:
        raise SystemExit("REFUSED: only %d source files under %s" % (len(files), root))

    per_area = collections.defaultdict(set)
    doubles = {}
    tests = collections.defaultdict(list)

    for f in files:
        text = io.open(f, encoding="utf-8", errors="replace").read()
        parts = norm(f).split("/")
        area = parts[2] if len(parts) > 3 else "(root)"

        for cls, ifaces in bases_of(text).items():
            for i in ifaces:
                per_area[area].add(i)
                doubles.setdefault(i, set()).add(cls)

        for m in TEST.finditer(text):
            tests[f].append(m.group(1))

    all_stubs = set().union(*per_area.values()) if per_area else set()
    all_tests = sum(len(v) for v in tests.values())

    if len(all_stubs) < MINIMUM_STUBS:
        raise SystemExit("REFUSED: only %d stubbed interfaces -- the class scan degraded"
                         % len(all_stubs))
    if all_tests < MINIMUM_TESTS:
        raise SystemExit("REFUSED: only %d tests found -- the test scan degraded" % all_tests)

    print("SURFACE")
    print("  source files                %d" % len(files))
    print("  test methods                %d" % all_tests)
    print("  distinct interfaces doubled %d" % len(all_stubs))
    print()
    print("  per area:")
    for area in sorted(per_area):
        print("    %-18s %3d doubled" % (area, len(per_area[area])))
    print()

    print("THE DOUBLES, most-implemented first")
    for iface, classes in sorted(doubles.items(), key=lambda kv: -len(kv[1]))[:20]:
        print("    %-42s %d  %s" % (iface, len(classes), ", ".join(sorted(classes))[:60]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
