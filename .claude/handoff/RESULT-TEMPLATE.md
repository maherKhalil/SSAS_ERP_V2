# T-### result — <STATUS>

- **Status:** <DONE | PARTIAL | BLOCKED | NEEDS-DECISION>
- **Branch:** `agent/T-###-<slug>` — pushed: <yes/no>
- **Commits:** <sha short — subject>
- **Reported:** <YYYY-MM-DD>

## Acceptance criteria

- [x] <criterion> — <the evidence: test name, file:line, or observed behaviour>
- [ ] <unmet criterion> — <why>

## What changed

<A few sentences of narrative, then the notable design choices made inside the given constraints.>

`git diff main...agent/T-###-<slug> --stat`:

```
<paste>
```

## Gate

| Suite               | Result |
| ------------------- | ------ |
| build (0 warnings)  |        |
| Architecture.Tests  |        |
| Platform.Tests      |        |
| HR.Tests            |        |
| API.Tests           |        |
| <module suite>      |        |

```
<paste the real tail of each run — counts, and full failure text if any>
```

## For the architect

- **Blocked / needs a decision:** <what, and the options as you see them — or "none">
- **Touched outside scope:** <what and why — or "nothing">
- **Follow-ups noticed but not done:** <deliberately left; not silently fixed>
