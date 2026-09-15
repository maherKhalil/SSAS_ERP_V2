using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;

namespace SSAS.Platform.Infrastructure.Persistence;

public sealed class PlatformDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
  public PlatformDbContext CreateDbContext(string[] args)
  {
    var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Platform");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
      throw new InvalidOperationException("ConnectionStrings__Platform is required for design-time migrations.");
    }

    var options = new DbContextOptionsBuilder<PlatformDbContext>()
      .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(
        PlatformPersistenceConstants.MigrationHistoryTable,
        PlatformPersistenceConstants.Schema))
      .Options;
    return new PlatformDbContext(options, new DesignCurrentUser(), new DesignCurrentTenant(), new DesignClock());
  }

  private sealed class DesignCurrentUser : ICurrentUser
  {
    public string? UserId => "migration";
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class DesignCurrentTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class DesignClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}
