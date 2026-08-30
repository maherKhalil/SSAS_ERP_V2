# item 161 — FP-014's 54 criteria against the tree

**Measurement only. Nothing built, nothing edited.** The README, dated 2026-08-25, says *"No code and no
schema. Nothing here is implemented."* `AddSubscriptionCommercialPlane` is dated **2026-08-26**.

## The split

| bucket | count |
|---|---|
| **1 — PINNED BY A TEST** | **20** |
| **2 — IMPLEMENTED BUT UNPINNED** | **11** |
| **3 — NOT IMPLEMENTED** | **19** |
| **4 — SUBJECT THE PRODUCT DOES NOT HAVE** | **4** |
| | **54** |

**Roughly the entitlement half is built and tested; the commercial half does not exist.** The line falls
almost exactly between *what a tenant may use* and *what a tenant is charged*.

The implementation is **9 domain types, 4 application, 1 API adapter, 3 infrastructure, 2 migrations**,
plus the tests named below.

## Bucket 1 — pinned, with the test named

| criteria | pinned by |
|---|---|
| `0001` `0003` `0044` append-only immutability, refusal from the guard | `PlatformAppendOnlyGuardTests` (6 — cites `AC-SUB-0044` by id, covers both bypass routes) and `A_seeded_subscription_cannot_be_corrected_in_place` |
| `0002` `0004` history and append ordering | `A_backdated_append_cannot_rewrite_what_was_in_force_at_a_past_instant`, `An_append_at_or_behind_the_current_maximum_is_refused`, `The_first_record_for_a_tenant_appends_with_no_current_maximum` |
| `0012` no record resolves to no modules | `A_tenant_with_no_subscription_is_refused_and_that_is_the_ruled_interim_state`, `A_tenant_the_seed_never_reached_is_still_refused_every_module` |
| `0016` `0017` grant against the plan cap | `A_limit_grant_at_or_below_the_plan_cap_is_refused`, `..._above_the_plan_cap_is_accepted`, `..._of_zero_against_a_plan_cap_of_zero_is_refused`, `..._when_the_plan_carries_no_such_limit` |
| `0018` 403 on an unentitled module | `A_route_of_a_module_the_tenant_does_not_have_is_refused_with_403` |
| `0021` no entitlement still authenticates | `An_expired_tenant_reaches_a_platform_plane_route_and_is_never_asked`, `An_ungated_route_does_not_consult_entitlement_at_all` |
| `0027` `0028` term invariants and expiry | four `SubscriptionInvariantTests` term cases, plus `A_fixed_term_expires_after_its_end`, `A_perpetual_term_never_expires`, `One_tick_after_the_term_no_module_is_reachable` |
| `0029` expired authenticates and is refused gated | `An_expired_tenant_is_refused_a_gated_route_with_403` (**item 160**) |
| `0032` the cache does not outlive the term | `A_request_before_expiry_caches_a_snapshot_that_still_refuses_after_it`, `The_final_instant_of_the_fourteenth_day_is_still_admitted`, `A_week_after_the_term_is_still_refused_and_no_grace_has_appeared` |
| `0033` no trial state anywhere | `No_type_in_the_subscription_model_carries_a_trial_flag` |
| `0048` no cardholder data | `CardholderDataArchitectureTests` (5, **including two anti-vacuity controls**) |
| `0052` `0053` `0054` seeding | `The_seeded_record_is_the_trial_plan_with_a_fixed_fourteen_day_term`, `An_archived_tenant_is_seeded_like_every_other`, `Running_the_seed_twice_issues_exactly_one_subscription_per_tenant`, `A_tenant_already_holding_a_subscription_is_left_untouched`, `The_plan_the_grants_the_price_and_the_catalog_are_written_exactly_once`, `Creating_a_tenant_issues_the_trial_plan_with_a_fourteen_day_term_and_commits_once` |
| `0020` gated versus exempt groups — **pinned by a different and stronger property**, below | `Every_module_owned_endpoint_is_gated`, `No_platform_plane_endpoint_is_gated`, `Every_module_owned_endpoint_declares_its_own_module_key`, with `The_endpoint_scan_finds_all_four_modules_which_is_what_stops_the_gating_test_below_being_vacuous` |

⚠ **`AC-SUB-0020` says "exactly the ten gated route groups … and the seven exempt ones". The host carries
20 `RequireModule` sites over FOUR module keys** — Attendance, GL, HR, Payroll. **The counts in the
criterion do not match the tree.** The test does not assert them: it asserts that *every* module-owned
endpoint is gated and *no* platform-plane endpoint is, which is count-free and stronger. **The criterion's
numbers are stale; the property is safe.**

## Bucket 2 — implemented, nothing asserts it (11)

`0005` plans shared with nothing copied into the subscription row · `0006` plan tables carry no `TenantId` ·
`0011` `plan ∪ grants` (**one path built, not the two the criterion compares**) · `0014` `0015` grant and
plan amendment take effect on the next request (the cache path exists; **there is no write route to
trigger it**) · `0019` one problem type on every gated route (structurally true — a single resolver) ·
`0030` expiry writes nothing to `Tenant` (`HasExpiredAt` is a pure read, and the domain comments the
ruling) · `0031` `TenantStatusChangeReason` carries no commercial member (**true of the enum today, but
nothing asserts it over the enum, which is what the criterion asks for**) · `0037` `decimal(19,4)`
(`PlanPrice.Amount` — **the only monetary column that exists**) · `0043` the cap resolves in the same call ·
`0046` the commercial-plane migration inserts no subscription row.

## Bucket 3 — not implemented (19), and how each was established

**`0007` `0009` `0010` `0034` `0035` `0036` — no routes.** Established from the route inventory in
`item-152-route-table.md`, where every FP-014 route is NOT BUILT, and corroborated independently: **no
`Platform.Subscriptions.*`, `Platform.Plans.*`, `Platform.Grants.*` or `Platform.Invoices.*` permission
exists.** I enumerated all 28 platform permission names to check.

⚠ **`AC-SUB-0008` IS SATISFIED FOR THE WRONG REASON, AND `AC-SUB-0045` IS NOT SATISFIABLE.** `0008`
requires that **no tenant-plane** subscription permission exist — true, **because the package defines no
permissions on EITHER plane.** `0045` speaks of *"this package's six"*; **those six do not exist.** A
criterion met by universal absence is not evidence that the distinction it protects was implemented.

**`0022` `0023` — no enabled-module read.** No `modules/enabled` route and no `EnabledModules` symbol in
`src/`.

**`0013` `0024` `0025` `0026` — I did not find an entitlement-to-permission coupling.** There is no
entitlement reference in the identity-access application layer. ⚠ **Stated as "I did not find it": such a
coupling could be composed at a seam I did not search, and `0026`'s "counts before and after" needs an
entitlement-lapse path I could not exercise.**

**`0038` `0039` `0041` `0042` `0047` — the commercial half is absent outright.** **Zero declarations of
`Invoice`, `PaymentAttempt`, `Overage`, `Proration` or `SeatUsage` anywhere in `src/`.** `0047` is a
release condition whose intent the seed migration serves, but nothing asserts it as a release gate.

## ⚠ Bucket 4 — the criterion's subject does not exist in the product (4)

**`0040` `0049` `0050` `0051` all rest on a SEAT, and no seat is defined.** `DEC-L-009` says "seats" and
does not define one. `AC-SUB-0049` names `TenantUser` as the seat-bearing entity **because that is the only
reading available, not because it was ruled** — flagged at ratification in `T-008`, again in `T-013`, both
times as an open concern, and still open. **`REQ-SUB-0027` carries two rows with different enforcement
semantics**, also open at ratification. Nothing in `src/` enforces a seat cap; the only `seat` symbols are
HR approver-directory locals, unrelated.

**These are not "not implemented".** A criterion whose subject is undefined cannot be implemented, and
filing them in bucket 3 would present a decision nobody made as engineering work not yet done.

## The 28 requirements

**Every one of the 28 is cited by at least one acceptance criterion** — the set difference is empty, so
there are no orphan requirements. **Requirement status therefore follows its criteria, and I did not map
requirements independently.** `REQ-SUB-0001` (append-only) is pinned; `REQ-SUB-0024` to `0028` (invoicing,
proration, seats) are buckets 3 and 4 throughout; the entitlement requirements `0007` to `0019` are pinned
or implemented, apart from the routed reads.

## ⚠ What this population excludes

- **Criteria are judged against the code and the test NAMES, not by executing each criterion.** A test
  whose name matches a criterion may assert something narrower. I read the names, and the bodies where the
  mapping was load-bearing — not all of them.
- **"Not implemented" is an absence claim.** Where it rests on a whole-tree symbol search, I say so; where
  it rests on my failing to find a seam, I say *that* instead.
- **Bucket 1 does not mean a criterion is fully covered.** `0020` is the worked example: pinned by a
  stronger property while its own stated counts are stale.
- **The gate was not run for this item.** It changes no `src/` or `tests/` file. The tests named here were
  observed; those exercised in items 156, 157 and 160 were run.
