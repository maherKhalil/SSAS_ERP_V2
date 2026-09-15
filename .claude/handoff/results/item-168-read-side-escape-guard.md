# item 168 — the read-side escape guard

**Gated work.** `tests/Architecture.Tests/ReadSideEscapeArchitectureTests.cs`, **5 tests**,
`GATE_SCOPE=TASK` **green**. No `src/` change.

## ⚠ The independent line contradicted item 167, and that is the finding

Item 167 closed on **"no read service is injected into any command handler."** **That was false.**

The search behind it looked for **three interface names** in files named `*CommandHandler*.cs`. Handlers
live in files named for their aggregate — `LeaveCommandHandlers.cs`, plural — so it enumerated a subset
and reported it as the whole. **EIGHT command handlers take a read-side service**, across four services:

| service | handlers |
|---|---|
| `IIdentityTenantMembershipReadService` | `BeginTenantAccess`, `RefreshAuthenticationSession`, `SelectTenant` |
| `ITenantAuthenticationEligibilityReadService` | `CreateTenantLocalizationOverride`, `RestoreTenantLocalizationDefault`, `UndoTenantLocalizationOverride`, `UpdateTenantLocalizationOverride` |
| `IPlatformSupportPermissionReadService` | `RefreshPlatformAuthenticationSession` |

**The hazard is still unreached, but by a different guarantee than 167 claimed.** Not that read services
never reach a command handler — they do — but that **no read service returns an aggregate**. They hand
over DTOs. The conclusion survives; the reason for it does not.

⚠ **This is the second time in this thread that a correct enumeration of the wrong set read exactly like a
complete one.** The first was `DequeueDomainEvents`. Here the population was files matching a name pattern,
and the pattern encoded an assumption about file naming that the codebase does not follow.

## What was built

**The guard:** `No_read_side_service_returns_an_event_raising_aggregate` — every method on every
read-side service, with its return type unwrapped through `Task<>`, `Result<>` and collections at any
depth, checked against `IHasDomainEvents`.

**Guarding the escape, not the query.** A ban on `AsNoTracking` would refuse 203 sites across 65 files to
prevent a hazard occurring at none of them, and a guard whose false positives outnumber its true ones by
two orders of magnitude gets deleted rather than fixed.

## ⚠ Three controls, because the guard is an absence assertion

| control | what it stops |
|---|---|
| `The_read_side_population_is_not_empty_and_contains_what_the_census_found` | a suffix matcher that matches **nothing** — floored at 20, **and cross-checked against five read-side classes item 167's `AsNoTracking` census named while looking for something else.** Not the matcher's own output |
| `The_aggregate_side_of_the_guard_recognises_known_aggregates` | `IsEventRaising` recognising nothing, which would make the guard green over any return type at all. Asserts both directions — `Employee` true, `string` false |
| `Command_handlers_taking_a_read_side_service_are_exactly_the_known_inventory` | a ninth injection appearing unnoticed. Keys on **constructor parameters**, classifying no return type, so it fails independently of the guard |

**The inventory is pinned rather than banned.** Taking a read service for a DTO is legitimate, and a ban
would fire on eight correct handlers. What is worth noticing is a **ninth** — because a new injection is
exactly where an aggregate-returning read would first arrive.

## ⚠ The plants, and the second one proved the control necessary

| plant | result |
|---|---|
| added an `Employee`-returning method to `EmployeeReadService` | the guard **FAILED** |
| replaced the suffix list with one matching nothing | ⚠ **the guard PASSED — vacuously green over an empty set** — and the population control **FAILED**, along with the inventory |
| removed one entry from the pinned inventory | the inventory **FAILED** |

**The second plant is the whole argument for having controls at all.** The guard did not notice its own
population disappearing; only the control did. A first attempt at plant one inserted the probe method into
a nested scope and produced `CS0106` — **that plant was void, not passing**, and was redone at class level.

All reverted, 5 green.

## What this guard does not cover

- **The suffix convention is a convention.** A read-side service that does not carry `ReadService`,
  `DirectoryService` or `RosterService` is invisible to the matcher, and the census cross-check raises the
  floor without closing that gap.
- **Only the five infrastructure and five application assemblies named in the file.** A new module's
  assembly must be added by hand; nothing detects its absence.
- **Return types only.** An aggregate escaping through an `out` parameter, a property, or a field is not
  checked.
- The unsearchable forms from item 167 — detached mutation without `AsNoTracking`, and types raising
  through a helper — remain **B16** and are untouched here.
