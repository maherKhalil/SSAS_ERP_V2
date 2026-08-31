# item 188 — what Release analysis reports that Debug does not

**Measured on `38d3237` from the PHASE run's own build logs, both configurations, `--no-incremental`.**
**Report only. No severity change proposed.**

## ⚠ THE ANSWER: THE DELTA IS EMPTY, AND THE DIFFERENCE IS **INHERITED**

| | Debug | Release |
|---|---|---|
| `Build succeeded` | yes | yes |
| **Warning(s)** | **0** | **0** |
| Error(s) | 0 | 0 |
| `: warning XNNNN` lines in the build log | **0** | **0** |

**There are no diagnostic ids and no sites to list, because Release reports nothing Debug does not.**
The item asked for ids and sites rather than a count; the honest answer is that the set is empty, and
below is why that is a structural fact rather than luck on one tree.

⚠ **The standing `CA1826` anecdote does not reproduce on today's tree.** A single anecdote had been
standing in for a whole configuration, and on this tree it stands for nothing.

## The mechanism set, enumerated before searching

A Release build can only report a diagnostic Debug does not through one of these:

| | mechanism | sites in `src/` + `tools/` |
|---|---|---|
| **M-a** | MSBuild property conditioned on `$(Configuration)` that changes analysis | **0** |
| **M-b** | `#if DEBUG` / `#if !DEBUG` / `#if RELEASE` — source analysed in one configuration only | **0** |
| **M-c** | `[Conditional("DEBUG")]` — calls elided in Release, changing usage counts | **0** |
| **M-d** | `Debug.Assert` / `Trace.Assert` — elided in Release, so nullable flow is no longer narrowed | **0** |
| **M-e** | explicit `DefineConstants` / `Optimize` / `DebugType` | **0** |
| **M-f** | `.editorconfig` severity varying by configuration | **0 — impossible by construction** |

**`.editorconfig` cannot be configuration-conditional at all.** There is exactly one file, at the root,
and it sets exactly one severity in the whole repository: `dotnet_diagnostic.CA1707.severity = none`.
**That applies to both configurations equally.**

`Directory.Build.props`: `Nullable=enable`, `Deterministic=true`, `TreatWarningsAsErrors=false`,
`AnalysisLevel=latest-recommended` — **no configuration-conditional property group.**

## CHOSEN or INHERITED — inherited, and nothing was chosen

**Nothing in this repository chooses any configuration-specific analysis behaviour.** What differs between
the two builds is only the SDK's defaults: Debug defines `DEBUG;TRACE` with `Optimize=false`, Release
defines `TRACE` with `Optimize=true`. **Since no source is gated on `DEBUG` and no property reacts to
`$(Configuration)`, those defaults have nothing to act on.**

So it is **a gap nobody made, not a decision anyone can defend** — but the gap is currently empty of
consequences for analysis. **The analyzer rule set is identical in both configurations.**

## ⚠ The prediction was recorded before the Release log existed

I wrote *"the expected delta is EMPTY; if Release reports anything Debug does not, one of the six
mechanisms is wrong or incomplete"* before the Release build ran, so it could have been falsified. It was
not.

## ⚠ A NAME SEARCH MATCHED NAMES — CAUGHT AND CORRECTED

My first M-a search, `Condition.*Configuration`, returned hits that were all the word *Configuration*
inside **package names** — `Microsoft.Extensions.Configuration.Binder`,
`EnableConfigurationBindingGenerator` — **not the `$(Configuration)` property.** Re-run against the
mechanism, `\$(Configuration)`, our build files hold **one** hit and it is not an analysis setting.

## ⚠ THE REAL DEBUG/RELEASE DIFFERENCE IS NOT A DIAGNOSTIC ONE

The configurations do differ, and the difference is **behavioural**: a test that passes in Debug fails in
Release under load (item 187). **An item scoped to "what does Release analysis report" would have missed
it entirely**, because the build logs are identical and the divergence is at run time.

**One config-conditional value does exist and is worth naming:**
`tests/Integration.Tests/SSAS.Integration.Tests.csproj:56` points `VerificationProcessHostPath` at
`...\bin\$(Configuration)\net8.0\...`. **It is a host binary location, not an analysis setting** — it did
not fire in this run, and a Release failure there would be a build-output fact rather than a product one.

## Scope
- **`tests/` was not swept for M-b/M-c/M-d**; the counts above are `src/` + `tools/`. Test-only
  configuration-gated code would not change the product analysis this item asked about.
- Measured on **one tree**. An empty delta today is not a guarantee about a tree with `#if DEBUG` in it —
  and with `TreatWarningsAsErrors=false`, nothing would stop one appearing.
