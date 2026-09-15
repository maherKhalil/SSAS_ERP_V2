"""Item 40, step 3 — the sharpest tier: tests where THE STUB IS THE ORACLE.

Tier 2 returned 229 of 652, which is a list rather than a ranking, and most of it is
legitimate: a test named for a repository that asserts an endpoint's status code is
correct as it stands. What distinguishes a genuinely blind test is narrower and
mechanical:

    THE TEST SETS THE ANSWER ON A DOUBLE, THEN ASSERTS THE CONSEQUENCE OF THAT ANSWER.

That is the T-188 shape exactly. The payroll test assigned the posting window onto the
ledger stub and then asserted the refusal, so planting the real emitter could not redden
it -- the emitter was never on the path. The test was not wrong, but it proved the
CONSUMER while its name spanned consumer and emitter both.

Such a test is not automatically a defect. It is where the name has to be read against
what the stub decides, and it is the only tier small enough to be worth a plant each.
"""

import collections
import glob
import io
import re
import sys

PLUMBING = {
    "IAsyncLifetime", "IClassFixture", "ICollectionFixture", "IDisposable",
    "IAsyncDisposable", "IEnumerable", "IEquatable", "IComparable",
}

TEST = re.compile(r"public\s+(?:async\s+)?(?:Task|void)\s+([A-Za-z_]\w*)\s*\([^)]*\)")
# An assignment onto something reached through a fixture or stub handle: `host.Ledger.Window =`,
# `stub.Result =`. Deliberately NOT matching plain locals (`var x = ...`).
SETS = re.compile(r"\b([a-z]\w*)\.((?:\w+\.)*\w+)\s*=\s*(?!=)")
ASSERTS = re.compile(r"\bAssert\.\w+")

MINIMUM_TESTS = 400


def norm(p):
    return p.replace("\\", "/")


def body_of(text, start):
    """The braces of one method, from its signature."""
    i = text.find("{", start)
    if i < 0:
        return ""
    depth, j = 0, i
    while j < len(text):
        if text[j] == "{":
            depth += 1
        elif text[j] == "}":
            depth -= 1
            if depth == 0:
                return text[i:j + 1]
        j += 1
    return text[i:]


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else "tests/API.Tests"
    files = [f for f in glob.glob(root + "/**/*.cs", recursive=True)
             if "/bin/" not in norm(f) and "/obj/" not in norm(f)]

    # every class in the tree that stands in for an interface
    doubles = set()
    for f in files:
        text = io.open(f, encoding="utf-8", errors="replace").read()
        for m in re.finditer(r"class\s+(\w+)\s*(?:\([^)]*\))?\s*:\s*([^\r\n{]+)", text):
            for b in re.split(r",\s*(?![^<>]*>)", m.group(2)):
                b = b.strip().split("<")[0].split(".")[-1].strip()
                if len(b) > 1 and b[0] == "I" and b[1].isupper() and b not in PLUMBING:
                    doubles.add(m.group(1))

    scanned = 0
    oracle = []
    for f in files:
        text = io.open(f, encoding="utf-8", errors="replace").read()
        for m in TEST.finditer(text):
            scanned += 1
            body = body_of(text, m.end())
            if not ASSERTS.search(body):
                continue
            sets = SETS.findall(body)
            if not sets:
                continue
            # the assignment must come BEFORE the first assertion, or it is arrange-after-act
            first_assert = ASSERTS.search(body).start()
            early = [s for s in SETS.finditer(body) if s.start() < first_assert]
            if early:
                oracle.append((norm(f), m.group(1),
                               sorted({s.group(1) + "." + s.group(2) for s in early})))

    if scanned < MINIMUM_TESTS:
        raise SystemExit("REFUSED: only %d tests scanned -- the method scan degraded" % scanned)

    print("TIER 3 -- THE STUB IS THE ORACLE")
    print("  tests scanned                   %d" % scanned)
    print("  double classes in tree          %d" % len(doubles))
    print("  tests that SET state on a handle")
    print("  before asserting                %d" % len(oracle))
    print()
    by_file = collections.Counter(f for f, _, _ in oracle)
    print("  by file, densest first:")
    for f, n in by_file.most_common(20):
        print("    %-58s %d" % (f.replace("tests/API.Tests/", ""), n))
    print()
    print("  the tests, by file:")
    current = None
    for f, name, sets in sorted(oracle):
        if f != current:
            print("    %s" % f.replace("tests/API.Tests/", ""))
            current = f
        print("      %-62s <- %s" % (name[:62], ", ".join(sets)[:46]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
