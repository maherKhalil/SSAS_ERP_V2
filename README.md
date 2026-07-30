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
