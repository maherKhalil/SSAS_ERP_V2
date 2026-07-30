---
title: "ADR-012: Runtime Module Composition"
document_type: "Architecture Decision Record"
status: "Accepted"
version: "1.0"
date: "2026-07-30"
decision_owners:
"Architecture Team"
applies_to:
"SSAS.Host.API"
"SSAS.Platform"
"SSAS.HR"
"SSAS.GL"
related_adrs:
"ADR-001 Modular Monolith"
"ADR-003 Clean Architecture"
"ADR-004 CQRS"
"ADR-005 Multi-Tenancy"
"ADR-006 JWT Authentication"
"ADR-008 Entity Framework Core"
"ADR-009 Domain Events"
"ADR-010 Repository Pattern"
"ADR-011 Unit of Work"
---
ADR-012: Runtime Module Composition
Status
Accepted
Context
SSAS ERP V2 is implemented as a multi-tenant Modular Monolith using Clean Architecture and independently bounded Platform, Human Resources, and General Ledger modules.
Each business module is divided into the following assemblies:
Domain
Application
Infrastructure
Contracts
API
The current solution structure establishes `SSAS.Host.API` as the executable application and composition root. The Host references the module API projects and is responsible for application startup, configuration, dependency injection, middleware, authentication, authorization, health checks, logging, OpenAPI, and module registration.
The architecture also requires infrastructure concerns to remain in each module's Infrastructure assembly. These concerns include:
Entity Framework Core `DbContext` implementations
Repository implementations
Unit of Work implementations
Database providers and migrations
External service clients
Email, SMS, notification, and file-storage adapters
Current-user and current-tenant adapters
Event dispatching infrastructure
Other technical implementations of Application-layer abstractions
Under the original project-reference graph, the Host referenced only module API projects, while module API projects were prohibited from referencing module Infrastructure projects. This preserved layer purity but created a runtime-composition gap:
Module API projects could not register Infrastructure implementations.
The Host could not register Infrastructure implementations because it did not reference them.
Runtime discovery through reflection or plugins was not defined or approved.
Persistence, repositories, Unit of Work, and external adapters could not be composed without introducing an undocumented dependency exception.
A clear composition rule is therefore required before EF Core, repositories, Unit of Work, authentication adapters, or external integrations are implemented.
Decision
`SSAS.Host.API` is the single application composition root and may reference each module's API and Infrastructure assemblies.
The approved production dependency shape is:
```text
SSAS.Host.API
├── SSAS.Platform.API
├── SSAS.Platform.Infrastructure
├── SSAS.HR.API
├── SSAS.HR.Infrastructure
├── SSAS.GL.API
└── SSAS.GL.Infrastructure
```
This is an explicit outer-layer composition dependency and does not violate Clean Architecture. The Host is the outermost executable layer and must know the concrete implementations required to assemble the running application.
The Host shall coordinate registrations only. It shall not contain module business rules, use-case implementations, domain behavior, persistence logic, or repository implementations.
Registration Responsibilities
Application assemblies
Each module Application assembly owns registration for application-level services, including:
CQRS handlers
Validators
Application services
Mapping profiles
Domain-event handlers
Pipeline behaviors
Application abstractions
Example naming:
```csharp
services.AddHrApplication();
services.AddGlApplication();
services.AddPlatformApplication();
```
Infrastructure assemblies
Each module Infrastructure assembly owns registration for technical implementations, including:
Module `DbContext`
Repository implementations
Unit of Work implementation
Database provider configuration
Persistence interceptors
Event dispatcher implementation
External API clients
Email, SMS, file-storage, and notification adapters
Options binding for infrastructure providers
Application-interface-to-infrastructure-implementation mappings
Example naming:
```csharp
services.AddHrInfrastructure(configuration);
services.AddGlInfrastructure(configuration);
services.AddPlatformInfrastructure(configuration);
```
API assemblies
Each module API assembly owns HTTP delivery concerns, including:
Endpoint mapping
Controllers or minimal API route groups
API-specific filters
Request and response handling
OpenAPI metadata specific to the module
Module-specific middleware only when explicitly approved
Example naming:
```csharp
app.MapHrEndpoints();
app.MapGlEndpoints();
app.MapPlatformEndpoints();
```
Host assembly
`SSAS.Host.API` coordinates the application startup sequence:
```csharp
builder.Services
    .AddPlatformApplication()
    .AddPlatformInfrastructure(builder.Configuration)
    .AddHrApplication()
    .AddHrInfrastructure(builder.Configuration)
    .AddGlApplication()
    .AddGlInfrastructure(builder.Configuration);

app.MapPlatformEndpoints();
app.MapHrEndpoints();
app.MapGlEndpoints();
```
The Host also owns global concerns such as:
Configuration
Dependency injection composition
Authentication middleware
Authorization middleware
Global exception handling
Problem Details
Logging
Correlation
Health checks
OpenAPI/Swagger
Host-level middleware ordering
Application startup and shutdown
Dependency Rules
The following dependencies are permitted:
```text
Host -> Module API
Host -> Module Infrastructure
Module API -> its Application and Contracts
Module Infrastructure -> its Application, Domain, Contracts, and approved BuildingBlocks
Module Application -> its Domain, Contracts, and approved BuildingBlocks
Module Domain -> approved Domain BuildingBlocks and SharedKernel
```
The following dependencies are forbidden:
```text
Module Domain -> Application
Module Domain -> Infrastructure
Module Domain -> API
Module Domain -> Host

Module Application -> Infrastructure
Module Application -> API
Module Application -> Host

Module API -> Infrastructure

Module Infrastructure -> Host
Module Infrastructure -> another module's Infrastructure
Module Infrastructure -> another module's internal Application or Domain

Platform -> HR internals
Platform -> GL internals
HR -> Platform internals
HR -> GL internals
GL -> Platform internals
GL -> HR internals
```
Tests may reference production assemblies as required for verification, but test dependencies do not alter the production dependency graph.
Cross-Module Communication
This ADR does not authorize direct business coupling between modules.
Platform, HR, and GL shall communicate only through approved integration mechanisms, including:
Integration events
Public contracts
Explicit module-facing abstractions
Host-provided cross-cutting abstractions
Future transport-backed messaging when modules are extracted
A module must not reference another module's Domain, Application, API, or Infrastructure internals merely because both modules run in the same process.
Where synchronous communication is required, the dependency must target an explicitly approved public contract and must not expose internal domain models or infrastructure types.
Where eventual consistency is acceptable, integration events are preferred.
Domain Events and Unit of Work
Domain-event handlers belong in Application assemblies.
Infrastructure provides the dispatching mechanism.
Domain events shall be dispatched only according to ADR-009 and the approved Unit of Work transaction boundary. Events that represent committed business outcomes must not be published before the successful transaction boundary defined by ADR-011.
Cross-module business notifications must use integration events or approved contracts rather than direct invocation of another module's internals.
Multi-Tenancy
All runtime registrations must preserve tenant isolation.
Infrastructure implementations that access tenant-owned data must obtain tenant context through approved abstractions and enforce tenant filtering according to ADR-005.
The Host may register current-user, current-tenant, claims, and correlation adapters, but it must not implement tenant-specific business rules.
Runtime Discovery
Reflection-based plugin discovery is not used in Version 1.
The application uses explicit compile-time module registration from the Host. This provides:
Clear dependencies
Predictable startup behavior
Compile-time validation
Easier debugging
Safer deployment
Simpler architecture tests
A dynamic plugin or module-manifest mechanism may be introduced later only through a separate ADR.
Dedicated Bootstrapper Projects
Dedicated per-module bootstrapper projects are not introduced at this stage.
They may be considered later if:
The Host becomes excessively coupled to registration details.
Modules require independent hosting.
Deployment packaging requires a module facade.
Runtime plugin loading becomes a product requirement.
A separate ADR approves the additional assemblies and dependency rules.
Until then, explicit Host references to module API and Infrastructure projects are the approved composition approach.
Architecture Enforcement
Architecture tests must permit:
```text
SSAS.Host.API -> SSAS.Platform.API
SSAS.Host.API -> SSAS.Platform.Infrastructure
SSAS.Host.API -> SSAS.HR.API
SSAS.Host.API -> SSAS.HR.Infrastructure
SSAS.Host.API -> SSAS.GL.API
SSAS.Host.API -> SSAS.GL.Infrastructure
```
Architecture tests must reject:
Module API references to Infrastructure
Domain references to Application, Infrastructure, API, or Host
Application references to Infrastructure, API, or Host
Infrastructure references to Host
Direct cross-module references to internal assemblies
Circular dependencies
Cross-module Infrastructure dependencies
Business logic implemented in the Host
Source-level architecture tests should be expanded as implementation grows to detect:
EF Core usage outside Infrastructure
Repository implementations outside Infrastructure
Controllers containing business logic
Direct cross-module handler invocation
Tenant-owned data access without tenant context
Infrastructure types exposed through public contracts
Consequences
Positive consequences
The runtime-composition gap is resolved.
Each module keeps its API and Infrastructure responsibilities separate.
Clean Architecture dependency direction remains protected.
The Host can register concrete implementations explicitly.
EF Core, repositories, Unit of Work, and external adapters can be introduced without hidden dependencies.
Module boundaries remain suitable for future microservice extraction.
Startup behavior remains visible and testable.
Reflection-based discovery complexity is avoided.
Architecture tests can enforce the approved exception precisely.
Negative consequences
The Host references more module assemblies.
Adding a module requires an explicit Host project reference and startup registration.
The Host must be kept free of business logic despite knowing concrete module Infrastructure assemblies.
Architecture tests require a specific composition-root exception.
Independent module deployment will require a new Host or bootstrapper when a module is extracted.
Risks
Developers may incorrectly interpret Host-to-Infrastructure references as permission for unrestricted layering violations.
Registration code may grow excessively inside `Program.cs`.
Cross-module behavior may be implemented in the Host instead of through contracts or events.
Shared Infrastructure may become a hidden coupling point.
Mitigations
Keep registration logic inside module extension methods.
Keep `Program.cs` limited to orchestration.
Enforce dependency rules through architecture tests.
Review all cross-module dependencies.
Use integration events and stable contracts.
Add a new ADR before introducing runtime plugin discovery or bootstrapper projects.
Continuously verify tenant isolation.
Alternatives Considered
Module API references Infrastructure
Rejected.
This would allow `AddModule()` to register all module components from the API assembly, but it would make the delivery adapter depend on persistence and technical implementations. That reverses the documented layer direction and weakens API purity.
Dedicated module bootstrapper projects
Deferred.
This is architecturally valid and can provide a single module facade that references API, Application, and Infrastructure. It was not selected because it adds one project per module and additional abstraction before the need is demonstrated.
Reflection-based runtime discovery
Rejected for Version 1.
This would avoid explicit Host references but introduce hidden runtime dependencies, deployment complexity, startup failure modes, and more difficult debugging.
Put all registrations directly in the Host
Rejected as an implementation style.
The Host may coordinate registration, but module-specific service mappings must remain encapsulated in module Application and Infrastructure extension methods. The Host must not become a container for module implementation details.
Implementation Guidance
When the relevant Sprint-00 milestones begin:
Add Host references to each module Infrastructure project.
Update architecture tests to permit only the documented Host-to-Infrastructure dependencies.
Introduce module Application registration extensions.
Introduce module Infrastructure registration extensions.
Keep API endpoint mapping in module API projects.
Keep `Program.cs` focused on composition and middleware ordering.
Do not add business logic to the Host.
Validate the complete dependency graph after every project-reference change.
Build and run architecture tests before completing the milestone.
Validation Criteria
This decision is correctly implemented when:
The Host references the API and Infrastructure project for every enabled module.
Module APIs do not reference Infrastructure.
Application and Domain projects do not reference Host or Infrastructure.
Infrastructure implementations register through module-owned extensions.
API endpoints register through module-owned mapping extensions.
No direct cross-module internal references exist.
Architecture tests pass.
The solution builds without circular references.
Tenant isolation remains enforced.
The Host contains composition code only.
Decision Review Triggers
Review this ADR if any of the following occurs:
A module is extracted into an independently deployed service.
Dynamic module installation becomes a requirement.
Runtime plugin discovery is proposed.
The Host becomes difficult to maintain because of registration volume.
Dedicated module bootstrapper projects are proposed.
Modules require separate deployment packaging.
Cross-module synchronous contracts become extensive.
A shared Infrastructure assembly begins creating module coupling.
Final Decision Statement
`SSAS.Host.API` is the explicit runtime composition root for SSAS ERP V2. It may reference each enabled module's API and Infrastructure assemblies solely to compose the running application.
This exception applies only to the outermost Host layer. It does not weaken Domain, Application, API, Infrastructure, module-boundary, multi-tenancy, or cross-module communication rules.