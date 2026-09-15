"""The ERP/clinical seam, measured from the catalogue.

PLANNING INSTRUMENT. Consumes catalogue.json and emits the seam report. The partition is declared
ONCE, here, and every count in the report derives from it - so a schema moved between sides changes
every number at once rather than leaving a stale one behind.

Run:
    python scripts/his-catalogue/analyse_seam.py scripts/his-catalogue/catalogue/catalogue.json
"""

import argparse
import collections
import io
import json
import sys

# ---- THE PARTITION, DECLARED ONCE.
#
# These are the ERP schemas. Everything else in the database is clinical or shared-clinical. The
# list is the single source for the partition: no count below re-derives it, and moving a schema
# between sides is a one-line change with consistent consequences.
ERP_SCHEMAS = [
    "Finance", "HR", "GeneralStores", "Purchasing", "Accounting", "Banking",
    "Assets", "GL", "PayRoll", "Budgeting", "Receivable", "Contracts", "ZaKat",
]

# ⚠ ANTI-VACUITY. If a rename makes every ERP schema stop matching, every crossing count goes to
# zero and the report reads as "there is no seam" - the most reassuring possible lie.
MINIMUM_ERP_TABLES = 600
MINIMUM_CROSSINGS = 100


def schema_of(key):
    """The schema half of a bracketed key. Split on '].[', never on '.', because a table NAME may
    contain a dot: [dbo].[Pharmacy.Borrowing] is not schema Pharmacy."""
    inner = key[1:-1]
    return inner.split("].[", 1)[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("catalogue")
    args = parser.parse_args()

    with io.open(args.catalogue, encoding="utf-8") as handle:
        catalogue = json.load(handle)

    erp = set(ERP_SCHEMAS)
    tables = catalogue["tables"]
    side = {key: ("erp" if schema_of(key) in erp else "clinical") for key in tables}

    erp_tables = [k for k, v in side.items() if v == "erp"]
    if len(erp_tables) < MINIMUM_ERP_TABLES:
        raise SystemExit("REFUSED: only %d ERP tables matched - the partition has stopped matching"
                         % len(erp_tables))

    internal_erp = internal_clinical = 0
    erp_to_clinical = clinical_to_erp = 0
    joints = collections.Counter()
    for fk in catalogue["foreign_keys"]:
        child, parent = side.get(fk["child"]), side.get(fk["parent"])
        if child is None or parent is None:
            continue
        if child == parent == "erp":
            internal_erp += 1
        elif child == parent == "clinical":
            internal_clinical += 1
        else:
            pair = tuple(sorted([schema_of(fk["child"]), schema_of(fk["parent"])]))
            joints[pair] += 1
            if child == "erp":
                erp_to_clinical += 1
            else:
                clinical_to_erp += 1

    crossings = erp_to_clinical + clinical_to_erp
    if crossings < MINIMUM_CROSSINGS:
        raise SystemExit("REFUSED: only %d crossings - the partition or the FK parse has degraded"
                         % crossings)

    print("PARTITION")
    print("  ERP schemas declared        %d" % len(ERP_SCHEMAS))
    print("  ERP tables                  %d" % len(erp_tables))
    print("  clinical tables             %d" % (len(tables) - len(erp_tables)))
    print()
    print("  per ERP schema:")
    counts = collections.Counter(schema_of(k) for k in erp_tables)
    for schema, count in counts.most_common():
        print("    %-16s %d" % (schema, count))
    print()
    print("FOREIGN KEYS")
    print("  ERP internal                %d" % internal_erp)
    print("  clinical internal           %d" % internal_clinical)
    print("  CROSSING                    %d   (erp->clinical %d, clinical->erp %d)"
          % (crossings, erp_to_clinical, clinical_to_erp))
    print()
    print("  the joints, densest first:")
    for (a, b), count in joints.most_common(12):
        print("    %-34s %d" % ("%s <-> %s" % (a, b), count))
    print()

    # ---- COMPANY ATTRIBUTION, WHICH IS ITEM 32's WHOLE PROBLEM.
    with_company = [k for k, cols in tables.items()
                    if any(c["name"].lower() == "companyid" for c in cols)]
    declarations = [c for cols in tables.values() for c in cols if c["name"].lower() == "companyid"]
    not_null = [c for c in declarations if not c["nullable"]]
    print("COMPANY ATTRIBUTION")
    print("  tables with a CompanyID     %d of %d" % (len(with_company), len(tables)))
    print("  tables with NONE            %d" % (len(tables) - len(with_company)))
    print("  CompanyID declarations      %d" % len(declarations))
    print("  of those NOT NULL           %d" % len(not_null))
    for column in not_null:
        print("      %s %s" % (column["name"], column["type"]))

    # ⚠ A SUBSTRING MATCH ON "tenant" FINDS `bedlieutenant` AND REPORTS A TENANT COLUMN THAT IS NOT
    # ONE. The first version did exactly that, and a single false hit is worse here than a hundred
    # elsewhere: the whole tenancy design turns on this count being ZERO.
    tenant = sorted({c["name"] for cols in tables.values() for c in cols
                     if c["name"].lower() in ("tenantid", "tenant_id", "tenant")})
    print("  columns naming a tenant     %d %s" % (len(tenant), tenant))
    print()

    print("TRUST ON THE EDGES")
    nocheck = [f for f in catalogue["foreign_keys"] if f["with_check"] == "NOCHECK"]
    print("  foreign keys WITH NOCHECK   %d of %d" % (len(nocheck), len(catalogue["foreign_keys"])))
    print("  (these are where orphans may already exist; they surface as MIGRATION failures,")
    print("   not as source errors, because the source never checked them)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
