# T-### — <short imperative title>

- **Branch:** `agent/T-###-<slug>`
- **Module:** <HR | Payroll | Attendance | Finance | Platform | BuildingBlocks>
- **Issued:** <YYYY-MM-DD>
- **Depends on:** <T-### | none>

## Goal

<Two or three sentences. What this slice makes true that is not true today, and why it is next.>

## Acceptance criteria

Observable and checkable. The coder is done when every box can be ticked from evidence.

- [ ] <e.g. `LeaveRequest.Approve` rejects an approval by the requester, covered by a named test>
- [ ] <e.g. `GET /api/leave-requests` returns only the calling tenant's rows>
- [ ] <e.g. all four CI suites green, zero new warnings>

## Files in scope

Everything outside this list is off-limits.

- `src/Modules/<M>/...`
- `tests/<M>.Tests/...`

## Out of scope

- <the adjacent thing deliberately not being asked for>

## Design constraints

- <which layer owns what; which building block to reuse; what must not be referenced>
- <tenant scoping / permission contributor / localization obligations that apply>

## Tests required

- <suite and shape: unit / architecture guard / integration against real SQL>

## References

- `docs/...`
- Existing pattern to imitate: `src/...`
