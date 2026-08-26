using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.GL.Infrastructure.Persistence;
using SSAS.Attendance.Infrastructure.Persistence;
using SSAS.Payroll.Infrastructure.Persistence;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Tenant.MigrationTool;

// The COMPLETE tenant model, for `dotnet ef` (ADR-012, ADR-018).
//
// ---- WHY THIS EXISTS SEPARATELY FROM PLATFORM'S OWN DESIGN-TIME FACTORY.
//
// Platform's factory builds the model Platform can see: its own tenant entities and nothing else, which is
// correct for it. A migration is generated from a model, so the model has to be the composed one — and
// only a project that may reference every module can compose it.
//
// ---- WHAT SCAFFOLDING WITHOUT THIS TOOL ACTUALLY PRODUCES. CORRECTED 2026-08-26 (T-050).
//
// **This paragraph said Platform's factory "would silently produce an EMPTY migration for a module's
// tables". It does not. It produces a DESTRUCTIVE one** — an `Up` of 32 `DropTable` covering the whole of
// HR, Finance/GL, Payroll and Attendance, because those tables exist in the database and in no model,
// which is the definition of a table to drop.
//
// **The original claim was written before anyone had run it**, and it is dated rather than deleted
// (`DEC-L-039`) because the difference between the two is the whole point: **empty and destructive call
// for opposite reactions.** An empty migration is a no-op nobody commits, so a reader who remembers that
// sentence concludes the output is useless and STOPS CHECKING. That is worse than no comment at all.
//
// The evidence, from T-048: the same no-op scaffold with `--startup-project` on
// `SSAS.Platform.Infrastructure` emits 32 `DropTable`; with this tool it emits an empty migration.
// `ADR-018` § *Scaffolding a tenant migration* carries the procedure and the recognition test.
//
// ---- THE CONTRIBUTOR LIST IS EXPLICIT, AND THAT IS THE POINT.
//
// ADR-012 rejects reflection-based module discovery. A module that is not named here does not appear in a
// migration, which is a loud, reviewable omission rather than a silent one — and adding a module means
// adding one line, in the same spirit as registering it in the Host.
//
// The connection string is resolved by Platform's factory contract and has NO fallback, for the reason
// ADR-018 records: an operator who omits it would otherwise get an apparently successful `database update`
// against a stray local database while the intended tenant database stayed unmigrated.
public sealed class ComposedTenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
  // Every module that contributes tenant entities. One line per module, reviewed like any other code.
  private static readonly ITenantModelContributor[] Contributors =
    [
      new HrTenantModelContributor(),
      new GlTenantModelContributor(),
      new PayrollTenantModelContributor(),
      new AttendanceTenantModelContributor()
    ];

  public TenantDbContext CreateDbContext(string[] args)
  {
    var connectionString = TenantDbContextDesignTimeFactory.ResolveConnectionString(
      Environment.GetEnvironmentVariable(TenantDbContextDesignTimeFactory.ConnectionStringVariable));

    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(
        TenantPersistenceConstants.MigrationHistoryTable,
        TenantPersistenceConstants.MigrationHistorySchema))
      .Options;

    // A migration never saves an entity, so the user, tenant and clock are stubs. The tenant is deliberately
    // null rather than a placeholder, so an accidental entity query fails closed against the global filter
    // instead of returning another tenant's rows.
    return new TenantDbContext(
      options, DesignTimeUser.Instance, DesignTimeTenant.Instance, DesignTimeClock.Instance,
      writeFence: null,
      branchAuthorizer: null,
      companyAuthorizer: null,
      branchTransferAuthorizer: null,
      modelContributors: Contributors);
  }

  private sealed class DesignTimeUser : ICurrentUser
  {
    public static readonly DesignTimeUser Instance = new();

    public string? UserId => null;

    public string? UserName => null;

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class DesignTimeTenant : ICurrentTenant
  {
    public static readonly DesignTimeTenant Instance = new();

    public Guid? TenantId => null;
  }

  private sealed class DesignTimeClock : IDateTimeProvider
  {
    public static readonly DesignTimeClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}
