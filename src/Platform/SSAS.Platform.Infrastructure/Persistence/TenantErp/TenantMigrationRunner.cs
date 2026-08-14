using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// Single-database tenant migration tooling (ADR-018, deliberately limited scope).
//
// This is the minimum needed by developers and tests to work with the tenant stream against ONE resolved
// physical database: inspect applied migrations, inspect pending migrations, apply them.
//
// IT IS NOT THE MIGRATION ORCHESTRATOR. There is no fleet loop, no health-state tracking, no maintenance
// window, no compatibility status and no rolling upgrade — those are ADR-018's orchestrator and require
// the health model that does not exist yet. Every method here takes ONE explicit tenant, so nothing can
// accidentally sweep the estate.
public interface ITenantMigrationRunner
{
  Task<Result<IReadOnlyCollection<string>>> GetAppliedMigrationsAsync(
    Guid tenantId, CancellationToken cancellationToken = default);

  Task<Result<IReadOnlyCollection<string>>> GetPendingMigrationsAsync(
    Guid tenantId, CancellationToken cancellationToken = default);

  Task<Result<IReadOnlyCollection<string>>> MigrateAsync(
    Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class TenantMigrationRunner(ITenantDbContextFactory contextFactory) : ITenantMigrationRunner
{
  public Task<Result<IReadOnlyCollection<string>>> GetAppliedMigrationsAsync(
    Guid tenantId, CancellationToken cancellationToken = default) =>
    ExecuteAsync(tenantId, async context =>
      (IReadOnlyCollection<string>)(await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray());

  public Task<Result<IReadOnlyCollection<string>>> GetPendingMigrationsAsync(
    Guid tenantId, CancellationToken cancellationToken = default) =>
    ExecuteAsync(tenantId, async context =>
      (IReadOnlyCollection<string>)(await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray());

  public Task<Result<IReadOnlyCollection<string>>> MigrateAsync(
    Guid tenantId, CancellationToken cancellationToken = default) =>
    ExecuteAsync(tenantId, async context =>
    {
      await context.Database.MigrateAsync(cancellationToken);

      // Re-read the ACTUAL history after migrating rather than reporting what was requested. ADR-018 is
      // explicit that a successful call is not evidence: observed __EFMigrationsHistory is.
      return (IReadOnlyCollection<string>)(await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
    });

  // Routing is resolved for the given tenant exactly as it is for a request, so migration tooling can
  // never reach a database the runtime would not route to — no bypass, no fallback.
  private async Task<Result<IReadOnlyCollection<string>>> ExecuteAsync(
    Guid tenantId,
    Func<TenantDbContext, Task<IReadOnlyCollection<string>>> operation)
  {
    var created = await contextFactory.CreateAsync(tenantId);
    if (created.IsFailure)
    {
      return Result.Failure<IReadOnlyCollection<string>>(created.Error);
    }

    await using var context = created.Value;
    return Result.Success(await operation(context));
  }
}
