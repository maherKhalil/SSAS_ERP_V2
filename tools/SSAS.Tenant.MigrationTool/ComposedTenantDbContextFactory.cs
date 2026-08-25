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
// correct for it and would silently produce an EMPTY migration for a module's tables. A migration is
// generated from a model, so the model has to be the composed one — and only a project that may reference
// every module can compose it.
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
