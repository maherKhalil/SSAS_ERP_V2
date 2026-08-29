"""Item 40, step 2 — rank tests by how much their NAME claims beyond what they can reach.

Reads the same surface as stub_survey.py and joins two facts per test:
  - the interfaces its own area replaces with a double;
  - the subject words in its own method name.

A test scores when its name claims a MECHANISM whose only implementation in that area is a
double. That is the fiscal-year lock's shape exactly: the name says the guard holds, the
composition cannot make the guard fire.
"""

import collections
import glob
import io
import re
import sys

# xUnit plumbing, not doubles of anything under test. Named rather than pattern-matched so a
# new one has to be added deliberately.
PLUMBING = {
    "IAsyncLifetime", "IClassFixture", "ICollectionFixture", "IDisposable",
    "IAsyncDisposable", "IEnumerable", "IEquatable", "IComparable",
}

# ⚠ WORDS THAT NAME A MECHANISM, NOT AN OUTCOME. A test called "..._is_refused" claims a
# result; one called "..._lock_..." or "..._concurrent_..." claims the machinery produced it,
# and only the second kind can be wrong in the way this item is looking for.
MECHANISM = {
    "lock", "locked", "concurrent", "concurrently", "race", "simultaneous", "unique",
    "overlap", "overlapping", "transaction", "rollback", "committed", "atomic",
    "isolation", "retry", "idempotent", "serialized", "contended", "deadlock",
}

CLASS = re.compile(r"class\s+(\w+)\s*(?:\([^)]*\))?\s*:\s*([^\r\n{]+)")
TEST = re.compile(r"public\s+(?:async\s+)?(?:Task|void)\s+([A-Za-z_]\w*)\s*\(")

MINIMUM_AREAS = 5


def norm(p):
    return p.replace("\\", "/")


def camel(name):
    """IDepartmentHierarchyLock -> {department, hierarchy, lock}"""
    body = name[1:] if len(name) > 1 and name[0] == "I" and name[1].isupper() else name
    return {w.lower() for w in re.findall(r"[A-Z][a-z0-9]*", body) if len(w) > 2}


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else "tests/API.Tests"
    files = [f for f in glob.glob(root + "/**/*.cs", recursive=True)
             if "/bin/" not in norm(f) and "/obj/" not in norm(f)]

    doubled = collections.defaultdict(set)     # area -> {interface}
    impls = collections.defaultdict(set)       # (area, interface) -> {class}
    tests = collections.defaultdict(list)      # area -> [(file, name)]

    for f in files:
        text = io.open(f, encoding="utf-8", errors="replace").read()
        parts = norm(f).split("/")
        area = parts[2] if len(parts) > 3 else "(root)"
        for m in CLASS.finditer(text):
            cls, bases = m.group(1), m.group(2)
            for b in re.split(r",\s*(?![^<>]*>)", bases):
                b = b.strip().split("<")[0].split(".")[-1].strip()
                if len(b) > 1 and b[0] == "I" and b[1].isupper() and b not in PLUMBING:
                    doubled[area].add(b)
                    impls[(area, b)].add(cls)
        for m in TEST.finditer(text):
            tests[area].append((norm(f), m.group(1)))

    if len(doubled) < MINIMUM_AREAS:
        raise SystemExit("REFUSED: only %d areas found -- the scan degraded" % len(doubled))

    hits = []
    wider = []
    for area, entries in tests.items():
        vocab = {}
        for iface in doubled.get(area, ()):
            for word in camel(iface):
                vocab.setdefault(word, set()).add(iface)
        for path, name in entries:
            words = {w.lower() for w in name.split("_") if len(w) > 2}
            mech = words & MECHANISM
            subj = {i for w in words & set(vocab) for i in vocab[w]}
            if mech and subj:
                hits.append((area, path, name, sorted(mech), sorted(subj)))
            elif subj:
                wider.append((area, path, name, sorted(subj)))

    total = sum(len(v) for v in tests.values())
    print("COUNT FIRST")
    print("  test methods scanned            %d" % total)
    print("  areas                           %d" % len(doubled))
    print("  interfaces doubled (net xUnit)  %d" % len(set().union(*doubled.values())))
    print()
    print("  TIER 1 -- names a MECHANISM whose implementation is a double")
    print("                                  %d" % len(hits))
    print("  TIER 2 -- names a SUBJECT that is a double, no mechanism word")
    print("                                  %d" % len(wider))
    print("  (tier 2 is the T-187/T-188 shape: the name claims a subject the")
    print("   composition reaches only through a stand-in)")
    print()
    if not hits:
        print("  (none -- and an empty list here is a CLAIM, so it is printed rather than assumed)")
    for area, path, name, mech, subj in sorted(hits):
        print("  %-12s %s" % (area, name))
        print("      claims: %-28s doubled: %s" % (",".join(mech), ", ".join(subj)))
        print("      %s" % path)
        for i in subj:
            print("        %s <- %s" % (i, ", ".join(sorted(impls[(area, i)]))))
    return 0


if __name__ == "__main__":
    sys.exit(main())
