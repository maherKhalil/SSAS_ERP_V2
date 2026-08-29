"""Item 41, axis 2 — live routes that no test ever calls.

⚠ THIS IS A REACHABILITY PROXY AND SAYS SO. It asks whether any test source contains a string
that could address the route, not whether an assertion covers its behaviour. So a route it
reports as UNTOUCHED is certainly untested; a route it reports as touched may still be tested
only for its 401. The proxy is therefore SOUND ON THE GAP and generous on the coverage, which
is the correct direction for a completeness count: it under-reports the problem rather than
inventing one.
"""

import collections
import glob
import io
import re
import sys

MINIMUM_ROUTES = 100
MINIMUM_TEST_FILES = 50


def norm(p):
    return p.replace("\\", "/")


def module_of(path):
    parts = [p for p in path.split("/") if p]
    if len(parts) >= 2 and parts[0] == "api":
        return parts[1]
    return "(other)"


def main():
    routes_file, tests_root = sys.argv[1], sys.argv[2]
    rows = [l.rstrip("\n").split("\t") for l in io.open(routes_file, encoding="utf-8") if l.strip()]
    if len(rows) < MINIMUM_ROUTES:
        raise SystemExit("REFUSED: only %d routes -- the probe output looks truncated" % len(rows))

    files = [f for f in glob.glob(tests_root + "/**/*.cs", recursive=True)
             if "/bin/" not in norm(f) and "/obj/" not in norm(f)]
    if len(files) < MINIMUM_TEST_FILES:
        raise SystemExit("REFUSED: only %d test files found" % len(files))

    # ⚠ INLINE STRING CONSTANTS BEFORE MATCHING, BECAUSE THE FIRST VERSION OVER-REPORTED BADLY.
    #
    # `DepartmentEndpointTests` declares `const string Route = "/api/hr/departments"` and then builds
    # `$"{Route}/{id}/move"`, so the literal path NEVER APPEARS CONTIGUOUSLY and the route read as
    # untested when it is tested. Verified against that exact route before and after.
    #
    # This substitutes each file's own const strings into its own interpolations, which is what turns a
    # naive text search into one that can see the common idiom.
    CONST = re.compile(r'const\s+string\s+(\w+)\s*=\s*"([^"]*)"')
    parts = []
    for f in files:
        text = io.open(f, encoding="utf-8", errors="replace").read()
        for name, value in CONST.findall(text):
            text = text.replace("{" + name + "}", value)
        parts.append(text)
    corpus = "\n".join(parts)

    untouched = []
    touched = 0
    for row in rows:
        key = row[0]
        policy = row[1] if len(row) > 1 else "ANONYMOUS"
        method, path = key.split(" ", 1)
        # `{}` stands for any single segment; everything else must appear literally.
        pattern = re.escape(path).replace(r"\{\}", r"[^/\"]+")
        if re.search(pattern, corpus):
            touched += 1
        else:
            untouched.append((module_of(path), method, path, policy))

    print("AXIS 2 -- LIVE ROUTES NO TEST SOURCE EVER ADDRESSES")
    print("  live routes                     %d" % len(rows))
    print("  addressed somewhere in tests    %d" % touched)
    print("  NEVER ADDRESSED                 %d" % len(untouched))
    print()
    if not untouched:
        print("  (none -- printed rather than assumed, because an empty list is a claim)")
    per = collections.Counter(m for m, _, _, _ in untouched)
    for module, n in per.most_common():
        print("    %-22s %d" % (module, n))
    print()
    for module, method, path, policy in sorted(untouched):
        print("    %-7s %-58s %s" % (method, path, policy))
    return 0


if __name__ == "__main__":
    sys.exit(main())
