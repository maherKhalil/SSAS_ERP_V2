# Architect's finding — the product has no commercial plane

**Date:** 2026-08-25 · **Raised by:** architect window, during T-001 · **Status:** awaiting owner ruling

---

## The finding in one line

`CON-0001` says this product **shall** operate as a subscription-based SaaS platform. Nothing in
the repository implements a subscription, and no version of the roadmap plans to.

## The evidence

Three authored, binding statements at the top of the authority chain:

| Where | What it says |
|---|---|
| `Requirement-Catalog/Constraints.md:21` — **`CON-0001`** | "The application shall operate as a subscription-based Software-as-a-Service (SaaS) platform." The file's own preamble: constraints "are mandatory and shall not be violated without an approved ADR." |
| `Business-Rules.md:161` — **`BR-PLT-0008` Feature Enablement** | "Modules may be enabled or disabled per subscription plan. **Disabled modules shall not appear in menus or APIs.**" |
| `Glossary.md:281` — **Subscription** | "A commercial agreement determining which modules and features are available to a Tenant." |

Against that, the repository:

- **`Tenant` carries no commercial state at all.** `src/Platform/SSAS.Platform.Domain/Tenants/Tenant.cs`
  holds code, name, status, lifecycle transitions and audit columns. No plan, no edition, no seat
  count, no entitlement, no billing anchor, no trial expiry.
- **Zero implementation of any commercial concept.** Sweeping `src/` for Subscription, Plan (as a
  commercial noun), Billing, Invoice, Metering, Quota, Dunning, Entitlement and Trial returns nothing.
  The apparent hits are false friends: GL's *trial balance* and Attendance's *leave entitlement*.
- **No feature package.** FP-001…FP-013 cover identity, tenancy, localization, company, HR, GL,
  Payroll and Attendance. None covers the commercial model.
- **Not on the roadmap — in any version.** `Product-Roadmap.md` runs to Version 5 and never names
  Subscription, Billing, Plans, Metering or Signup. This is not deferred work; it is absent work.
- **`CON-0001` and `BR-PLT-0008` are traceability orphans.** The *identifier* `CON-0001` appears exactly once in the
  entire `docs/` tree — in its own definition. `BR-PLT-0008` appears twice: its definition, and one
  bare mention in `Tenant-Management.md:35`. Neither has a `REQ`, an `AC`, a test, or a line of code.

## Why this gets more expensive every sprint, not less

`BR-PLT-0008` is not a data-model rule. It is an **enforcement** rule with a hard clause: disabled
modules must not appear in **menus or APIs**.

`src/Host/SSAS.Host.API/Program.cs` mounts every module unconditionally — **seventeen** `Map*Endpoints()`
calls across Platform, HR, GL, Payroll and Attendance, plus the `Add*Module()` registrations.
There is no gate. Every tenant reaches every route of every built module.

So the current state is not "the feature is not built yet". It is that **every route shipped so far
was shipped in violation of `BR-PLT-0008`**, and the retrofit cost grows with each module. Recruitment,
Performance and Self Service will each add routes to that retrofit. The cheapest moment to install
the gate is before the next module, and it is never cheaper again.

## The second-order problem

`scripts/trace-check.py` exists **untracked in the working tree** and is real tooling — it hunts
orphaned identifiers, numbering gaps, and `REQ→AC→TS` coverage holes, written because "FP-012 stated
its entity count wrong four times."

It checks *inside* `docs/17-features/`. It cannot see `CON-0001` or `BR-PLT-0008`, because those live
in the master specification above the packages. The orphan class the script was built to catch is
exactly the class that swallowed the product's defining constraint — one level up, where nothing looks.

## What I recommend

1. **Rule on `CON-0001` explicitly.** Either the product is subscription-based SaaS and the commercial
   plane gets a place on the roadmap, or `CON-0001` is amended by ADR. What must not continue is the
   third state: a mandatory constraint that everyone reads and nobody implements.
2. **Install the module-enablement gate before the next module ships**, not after. Scope it to the
   enforcement point (Host composition and route mounting) plus a per-tenant enablement record. This
   is `BR-PLT-0008` and it is buildable without settling pricing, invoicing or metering.
3. **Do not build billing or metering yet.** They need owner rulings this package cannot invent —
   pricing model, invoicing authority, proration, currency, tax. Enablement does not.
4. **Extend `trace-check.py` upward to `CON-*` and `BR-*`, and commit it.** A checker that only sees
   feature packages will keep missing the layer that outranks them.
5. **Record the roadmap-vs-reality drift.** Version 1 lists Dashboard, Reporting, Notifications and
   Audit. Audit is substantially built; Notifications and Dashboard have no code at all. The team is
   building Version 2 while Version 1 carries unbuilt entries, and the roadmap does not say so.

## What I deliberately did not do

I did not edit `docs/` to fix any of this. T-001 holds `docs/START-HERE.md` right now, and changing
the roadmap or the constraint register is a product ruling, not an architect's tidy-up. These become
coder tasks once the owner rules on item 1.

---

## Corrections — applied 2026-08-25 after T-002 verified this note against the repository

This note was right on every conclusion. Three details were wrong or incomplete, and two of them
change what a builder must do. Recorded rather than silently edited, because the note was cited by
the T-002 spec and by `DEC-L-004`.

1. **Seventeen route groups, not twelve.** `Program.cs:126`–`:152` mounts seventeen `Map*Endpoints()`
   calls, not twelve. Corrected inline above. The retrofit is larger than this note first recorded,
   which strengthens rather than weakens the argument for gating before the next module.

2. **`CON-0001` the identifier is an orphan; "subscription" the concept is not.** This note said the
   constraint appears once in `docs/`, which is true of the identifier and misleading about the
   subject. `Tenant-Management.md` carries seven mentions **including a `TBL-PLT-Subscription` table,
   a "Subscription Plan" entity and a "Multiple Subscription Plans" section**; `Authentication.md`
   states an expiry-blocks-login rule; `ADR-005:248`, `ADR-021:207` and `ADR-017` (twice) all reference
   subscription. **None of it is authority** — `Tenant-Management.md:13` disclaims its own subscription
   material as "deferred and non-authoritative until covered by an approved feature package", and
   `Authentication.md` is `Draft`. But a builder grepping the *concept* rather than the identifier
   finds a substantial apparent design and could easily mistake it for a specification. FP-014 records
   this as `DEC-SUB-0012`.

3. **`FP-002` already forbids the obvious implementation, and nobody had written that down.**
   `authentication-model.md:16` and `decisions-approved.md:642` exclude "subscription or billing
   information" from tokens by name, under **exact claim cardinality** where duplicate claims are
   invalid. The intuitive way to gate modules by plan — put entitlements in the JWT — is therefore
   already prohibited by an approved package. This is the constraint most likely to be violated by
   accident, and it is now `DEC-SUB-0005` and `REQ-SUB-0008`.

**Additional finding, wider than the ADR-026/027 one already on the board:** `ADR-017` carries
`status: Proposed` (v1.6). The tenant storage topology, the Shared→Dedicated cutover, and now
FP-014's plan-residency inheritance all rest on it. `ADR-015`, which FP-014 also leans on, is `Accepted`.
