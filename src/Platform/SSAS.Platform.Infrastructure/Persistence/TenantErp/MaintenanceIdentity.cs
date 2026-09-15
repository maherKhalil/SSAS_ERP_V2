using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// THE IDENTITY MAINTENANCE AND METADATA CONTEXTS RUN AS (ADR-017, ADR-018).
//
// Schema health, migration orchestration and the cutover model source all construct a TenantDbContext that
// is never used to query a tenant-owned entity. They need the same three collaborators and the same
// deliberate choices, so those live here once rather than being re-declared beside each caller — two
// definitions could drift into two different notions of "no tenant", and one of them would be wrong.
//
// THE TENANT IS NULL ON PURPOSE. A placeholder identity would make an accidental entity query return
// somebody's rows; null makes it fail closed against the global filter instead.
internal static class MaintenanceIdentity
{
  public static readonly ICurrentUser User = new MaintenanceUser();

  public static readonly ICurrentTenant Tenant = new MaintenanceTenant();

  public static readonly IDateTimeProvider Clock = new MaintenanceClock();

  // A reserved NON-CUSTOMER probe identity, for the schema probe that needs the real query filters to
  // compile but must not inspect any customer tenant.
  public static readonly ICurrentTenant SchemaProbeTenant = new SchemaProbe();

  private sealed class MaintenanceUser : ICurrentUser
  {
    public string? UserId => "tenant-storage-maintenance";

    public string? UserName => null;

    public string? Email => null;


    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class MaintenanceTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class SchemaProbe : ICurrentTenant
  {
    public Guid? TenantId => Guid.Empty;
  }

  private sealed class MaintenanceClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}
