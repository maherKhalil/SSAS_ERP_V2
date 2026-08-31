# Item 231 — the second walk, and it found that four of the seven were never the guard's business

**TASK gate green, 0 warnings. Architecture 622 → 623. All 42 declaring types are now judged.**

## ⚠⚠ THE MEASURED PARTITION, WHICH IS THE FINDING

| model | types | guarded by `ApplyPersistenceRules` | ⚠ key-immutable |
|---|---|---|---|
| composed tenant | **35** | **35** | 0 |
| platform | **7** | **3** | ⚠ **4** |

**42 total, and the split is now stated on every run rather than inferred.**

⚠⚠ **The four are `Role`, `TenantUser`, `TenantLocalizationOverride` and `TenantLocalizationSettings`.
They carry `TenantId` IN A KEY, so the row cannot be re-tenanted at all** — EF refuses to mark a key
property modified, **before `SaveChanges` is reached and before the guard exists as a question.** **That
is a STRONGER guarantee than the guard, not a weaker one.**

⚠ **So two of the four rows named as highest-consequence when this was queued — `Role` and `TenantUser` —
turn out not to need the guard.** **The three that do — `RolePermissionAssignment`,
`TenantUserRoleAssignment`, `TenantLocalizationOverrideVersion` — are asserted here for the first time**,
and one of them was on that list too.

## Feasibility: yes, and the model built the same way

`PlatformDbContext` needs no contributors — it configures itself from its own assembly excluding the
tenant namespace — so the second walk is **the same loop against a second model-only shell.** ⚠ **It is
sealed and runs eight write rules before `base.SaveChangesAsync`, so the shell derives from
`PersistenceDbContext` for the same reason the tenant one does: enter below the other guards.**

## ⚠ Three obstacles, each cleared WITHOUT per-type knowledge

| obstacle | fix |
|---|---|
| `Role.TenantId` is part of a key; EF refuses to mark it modified | ⚠ **classify by the OBSERVED refusal, not by a metadata prediction** |
| `TenantLocalizationOverride` cannot be tracked — alternate key `ResourceKey` is null | fill key participants from `GetKeys()` |
| `ResourceKey` has no parameterless constructor | `GetUninitializedObject` for reference types, as for the entity itself |

⚠⚠ **The first is the one worth keeping.** My first version asked `FindPrimaryKey()` — **and still hit
the throw, because *part of a key* covers alternate and identifying foreign keys too.** **A predicate
over the PRIMARY key missed one and the test failed on a property it had already decided was ordinary.**
**Observing the refusal cannot miss a kind of key nobody thought of.** *The same lesson as asserting the
refusal's identity: ask what happened, not what should have.*

**And nothing added is per-type**: the properties to fill come from the model's own key metadata, so a
new key on any entity is filled the day it is declared.

## Controls

- **Floors**: 30 for the tenant model (35 measured), 5 for the platform model (7 measured) — ⚠ low enough
  to survive one type moving plane, high enough to fail if a model stops building.
- ⚠ **A partition assertion**: `guarded + keyed == types.Length`, **so a model whose types all drifted
  into the key-immutable branch cannot pass while asserting nothing about the guard.** That branch is
  reachable — 4 of 7 are in it today — which is exactly why the count is stated.
- ⚠ **Plant on a GUARDED platform type**: `GetType().Name != "TenantUserRoleAssignment"`. **The platform
  test reddens and the tenant test stays green.** **Planting a key-immutable type instead would have
  proved nothing, because the guard never sees one.**
