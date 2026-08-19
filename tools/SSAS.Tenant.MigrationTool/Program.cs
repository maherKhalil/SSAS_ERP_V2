// THE TENANT MIGRATION TOOL (FP-006C3, ADR-012, ADR-017, ADR-018).
//
// It exists to be a `--startup-project`, not to be run. `dotnet ef` needs a project that can build the
// COMPLETE tenant model — Platform's entities plus every module's contribution — and no production project
// may do that: Platform must not reference HR, HR must not reference Platform, and the Host is a runtime
// composition root that should not carry design-time tooling packages.
//
// This is the one place permitted to see both, exactly as the Host is the one place permitted to see every
// module's Infrastructure at runtime. It lives under `tools/` beside the localization catalog tool, outside
// `src/`, so the production dependency rules are neither relaxed nor circumvented.
//
// Usage (ADR-018 operational procedure, tenant stream):
//
//   SSAS_TENANT_MIGRATION_SQLSERVER=<connection string> \
//   dotnet ef migrations add <Name> \
//     --project        src/Platform/SSAS.Platform.Infrastructure \
//     --startup-project tools/SSAS.Tenant.MigrationTool \
//     --context        TenantDbContext \
//     --output-dir     Persistence/TenantErp/Migrations
Console.Error.WriteLine(
  "SSAS.Tenant.MigrationTool is a design-time host for `dotnet ef`. It is not intended to be executed.");
return 1;
