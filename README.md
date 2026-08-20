# SSAS ERP V2

SSAS ERP V2 is a multi-tenant, cloud-ready enterprise ERP platform. It is
being built as a modular monolith that can evolve into independently deployed
services when business or operational needs justify extraction.

## Architecture

The solution follows Clean Architecture. Each module owns its Domain,
Application, Infrastructure, API, and Contracts layers. Dependencies point
inward, modules do not access another module's implementation or database, and
cross-module business communication uses approved contracts or events.

`SSAS.Host.API` is the composition root. It coordinates approved module
registration and endpoint mapping without containing business logic.

## Technology Stack

- .NET 8 and ASP.NET Core
- C#
- Microsoft SQL Server and Entity Framework Core (when persistence is added)
- Angular for the web client
- Serilog, Swagger/OpenAPI, health checks, and xUnit

## Repository Structure

```
src/
  BuildingBlocks/     Shared architectural primitives
  Host/               ASP.NET Core composition root
  Platform/           Platform module layers
  Modules/HR/         Human Resources module layers
  Modules/Finance/    General Ledger module layers
tests/                Unit, architecture, API, integration, UI, and performance tests
docs/                 Product, architecture, engineering, and sprint documentation
```

## Documentation

Start with [docs/START-HERE.md](docs/START-HERE.md). It defines the mandatory
reading order, documentation authority, architecture rules, and current sprint
constraints.

## Current Sprint

Sprint-00 Foundation is in progress. Milestones 1 and 2 are implemented and
awaiting review. Later Sprint-00 milestones remain out of scope until approved.

## Build And Test

```powershell
dotnet build SSAS.ERP.sln
dotnet test SSAS.ERP.sln
dotnet test tests/Architecture.Tests/SSAS.Architecture.Tests.csproj
```

## Continuous Integration

`.github/workflows/ci.yml` runs on every pull request into `main`, on every push
to `main`, and on demand. It is the same sequence you can run locally:

```powershell
dotnet build SSAS.ERP.sln --no-incremental
dotnet test tests/Architecture.Tests/SSAS.Architecture.Tests.csproj --no-build
dotnet test tests/Platform.Tests/SSAS.Platform.Tests.csproj --no-build
dotnet test tests/HR.Tests/SSAS.HR.Tests.csproj --no-build
dotnet test tests/API.Tests/SSAS.API.Tests.csproj --no-build
```

Architecture, Platform and HR tests need no database. **API tests do**: two
platform-support end-to-end classes create and migrate a real database, and they
fail rather than skip when no server is reachable. CI supplies an ephemeral SQL
Server service container; locally they use `Server=localhost` with Windows
authentication unless `SSAS_TEST_SQLSERVER` overrides it:

```powershell
$env:SSAS_TEST_SQLSERVER = "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False"
```

**Real-SQL integration tests run nightly, not per pull request.**
`tests/Integration.Tests` is 495 tests and about 79 minutes locally, most of it
serialized cutover and backup suites that create and drop databases one at a
time. Running it on every pull request would produce a gate people wait out
rather than trust, so `.github/workflows/integration-tests.yml` is triggered by:

| Trigger | When |
|---|---|
| `schedule` | 03:00 UTC daily |
| `workflow_dispatch` | on demand, still available |

Hosted runs have taken **roughly 14 to 18 minutes** end to end across the runs
observed so far — a measurement, not a guarantee, and one that will grow as the
suite does. Two runs never execute at once, and a run in progress is never
cancelled by a later one.

Run it yourself before a release, or when a change reaches cutover, storage or
routing behaviour:

```powershell
dotnet test tests/Integration.Tests/SSAS.Integration.Tests.csproj
```

## Operational Endpoints

| Endpoint | Purpose | Expected response |
|----------|---------|-------------------|
| `/` | Application information | `200 OK` |
| `/swagger/index.html` | OpenAPI user interface | `200 OK` |
| `/health/live` | Process liveness | `200 OK` |
| `/health/ready` | Configured infrastructure readiness | `200 OK` |

## Security Configuration

JWT configuration uses the `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey`, and
`Jwt:ClockSkewSeconds` keys. The committed development signing key is an
explicit non-secret placeholder and is rejected outside Development. Supply a
real signing key through environment variables or the deployment secret store.

## Contribution Workflow

Read the required documentation before changing code. Work on a focused branch,
keep changes scoped to an approved sprint or feature package, run the build and
relevant tests, and request review through a pull request. Do not push directly
to `main`.
