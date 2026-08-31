# item 199 — what `Performance.Tests` and `UI.Tests` were for

**Measurement only. NEITHER DELETED** — the ruling is right that an empty project may be a placeholder
whose intention lives outside the repository, and this one's intention lives *inside* it.

## They were never an experiment — they are the unbuilt half of a declared taxonomy

**Both were created in `28fcc62`, "milstone 01", 2026-07-30 — the initial scaffolding commit — ALONGSIDE
SIX SIBLINGS, all eight test projects at once:**

`API.Tests`, `Architecture.Tests`, `Finance.Tests`, `HR.Tests`, `Integration.Tests`,
**`Performance.Tests`**, `Platform.Tests`, **`UI.Tests`**.

**Six were populated. These two never were.** ⚠ **Each has exactly ONE commit touching it — its creation.**
Neither has been edited since 2026-07-30.

**Their `.csproj` is boilerplate**, byte-identical in structure to the populated siblings: `net8.0`,
`IsTestProject`, xunit, coverlet. **Nothing in either file records what it was meant to hold** — no
`ProjectReference`, no package hinting at a benchmark or browser-automation harness. The intent is not in
the project; it is in the docs.

## ⚠ AND THE INTENT IS STILL DOCUMENTED AND STILL STANDING

**Both are named in the architecture as part of the intended layout** —
`docs/03-Architecture/Solution-Structure.md` lists `UI.Tests` and `Performance.Tests` in the solution tree
beside `Architecture.Tests`, which *is* built and green.

**And `docs/08-Development/Development-Standards.md` names them as test CATEGORIES**, under the standing
requirement: *"Every critical business workflow shall be covered by automated tests."*

**So they are not abandoned scratch. They are a plan that was written down, scaffolded, and never
executed** — and the plan has never been withdrawn. **That is the answer to "what were they for": UI
testing and performance testing, as declared standards, with no owner and no date.**

## They are in the solution, and that has a cost

Both are `Project(...)` entries in `SSAS.ERP.sln`, so **every `dotnet build SSAS.ERP.sln` builds two empty
assemblies — in both configurations on a PHASE run.** Small, and not zero.

⚠ **They are also why `test-baseline.txt` tracks eight suites and not ten**, and the gate header calls their
absence correct — which it is, *as a statement about counting*. It is not a statement about whether they
should exist.

## The disposal, which is not mine

Three coherent options, and the difference between them is a **decision about the standards document, not
about two files**:

1. **Keep and build them** — the standards stay true and the debt stays visible.
2. **Delete both, and amend `Solution-Structure.md` and `Development-Standards.md`** — honest, but it
   **retracts a testing standard**, which is exactly why it is not a coder's call.
3. **Keep and give them an owner and a date** — the only option that ends with the plan executed.

⚠ **Deleting the projects while leaving the standards in place would be the worst outcome**: the
requirement would survive with nothing named to satisfy it, and the next reader would find a standard with
no home rather than a home with no content. **An empty project is at least a visible claim.**

## Scope
- **History read to the creation commit; I did not search for an intention outside the repository** — a
  design doc, ticket or conversation predating 2026-07-30 could name a specific harness, and the ruling's
  caution about intent living elsewhere is not excluded by anything here.
- `BACKLOG.md` records `B17` on this subject, **downgraded 2026-08-31**, and `OWNER-DECISIONS.md` carries a
  related entry; I read both as context and neither states a disposal.
