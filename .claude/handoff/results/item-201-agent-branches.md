# item 201 — the `agent/*` branches, by the corrected method

**Measurement only. Nothing pushed, nothing deleted.** ⚠ **The yield is near-empty, which the ruling
pre-authorised — and one branch is real.**

## The method, validated before it was trusted

**Content against `origin/ClaudeBranch`, by BASENAME** — not reachability from `main` (item 198 showed
`main` is ~925 commits behind), and not by path (item 198 showed a path check cannot tell *deleted* from
*moved*).

**Validated on `agent/T-072-spec-and-authorization-model` as instructed:** **20 commits** ahead by
reachability, **0 of 13 files missing** by basename. It reads exactly as predicted — *content present,
branch retained deliberately*. **The method survives the case whose answer was known.**

## The population

⚠ **151 `agent/*` branches, not ~200.** Compared against 1,862 distinct basenames on `ClaudeBranch`.

**Six branches hold any file whose basename is absent.** The other 145 hold nothing that is not live.

## Five of the six are result files, and they are NOT evidence of loss

`agent/T-163…`, `T-167…`, `T-169…`, `T-181…`, `T-185…` each carry exactly one absent file:
`.claude/handoff/results/T-nnn.md`.

⚠ **That looked like a systematic loss of the handoff record and is not.** ⚠ **CORRECTED BY ITEM 203 — THE
FIGURES BELOW WERE WRONG.** The live trail holds **137** `T-nnn.md` files spanning T-001…T-201, with **64**
numbers missing inside that range — not 139 and 149. I compared a zero-padded list against unpadded output,
so almost every number read as absent. **The conclusion is unchanged — five of 64 is still a small fraction
and closes nothing — but the trail is far less sparse than reported here.** Five branch-held
files are **five of 149 gaps**, and their immediate neighbours are absent too (T-162, T-164, T-166, T-168,
T-170, T-182, T-184, T-186 — none present).

**So the trail is sparse by nature, and these five are not a distinguishable subset of the sparseness.**
Recovering them would add five documents to a series already missing 149; **it would not close anything.**

## ⚠ THE ONE REAL BRANCH: `agent/T-155-tenant-transport`

**One commit, `2e2154d` (2026-08-29), and four absent source files** —
`TenantApiErrorMapper.cs`, `TenantEndpointRouteBuilderExtensions.cs`, `TenantTransportContracts.cs`,
`TenantRouteInventoryTests.cs`. ⚠ **`src/Platform/SSAS.Platform.API/Tenants/` DOES NOT EXIST on
`ClaudeBranch` at all** — every sibling module (`Companies`, `IdentityAccess`, `Localization`,
`Authentication`) has exactly this three-file shape, and `Tenants` is the hole.

**It is genuinely unlanded work. It is NOT lost work.** Its own commit subject says
**`BLOCKED on AC-TEN-0020`**, and that blocker is recorded in three independent places:

| where | what it says |
|---|---|
| `BOARD.md:759` | *"THE TENANT TRANSPORT IS BLOCKED BY A RECORDED SCOPE DECISION"* |
| `results/item-152-route-table.md:124–125` | `GET /api/platform/tenants` → **`DEFERRED - AC-TEN-0020`** |
| `AC-TEN-0020` (FP-003 first-milestone scope) | eleven deferred concerns |

**So: parked deliberately, with the reason written down before it was parked.** The branch is the work,
and the scope decision is why it is not merged.

### ⚠ But nothing mechanically prevents it landing, and that is deliberate

`Milestone_contains_no_deferred_tenant_endpoint_or_post_session_implementation` **no longer exists** — it
survives only as a comment at `TenantLifecycleArchitectureTests.cs:117–139` explaining that it was
**RETIRED under `DEC-L-030`**, because it *"checked four spellings of a rule that had already expired three
times over"* and went on passing only by looking for `CompanyProvision` rather than `Company`.

⚠ **I checked whether it was a live test rather than trusting the reference** — the line that names it is a
comment in the past tense. **Retiring it was right; the consequence is that the deferral now rests on the
scope document being READ, not on anything failing.** That is a statement about where the guarantee lives,
not a request to rebuild a bad guard.

## Scope
- **151 `agent/*` branches. `codex/*` was item 198.** ⚠ **This repository has 280 branches; 38 + 151 = 189
  are now measured, and the remaining ~91 — including `ClaudeBranch` itself, `main`, and any other prefix —
  were not.**
- **Absence was judged by basename anywhere in the tree.** A file present under a *different* basename with
  the same content still reads as absent; a file present under the same basename with *different* content
  reads as present. **The second is the weaker direction**, and it is why the six hits were each opened and
  read rather than counted.
- No branch content was compared line-by-line except the six.
