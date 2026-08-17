using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Infrastructure.Persistence;

public abstract class PersistenceDbContext(
  DbContextOptions options,
  ICurrentUser currentUser,
  ICurrentTenant currentTenant,
  IDateTimeProvider dateTimeProvider) : DbContext(options)
{
  private static readonly MethodInfo ConfigureTenantFilterMethod = typeof(PersistenceDbContext)
    .GetMethod(nameof(ConfigureTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

  protected Guid? CurrentTenantId => currentTenant.TenantId;

  protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
  {
    configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.Ignore<DomainEvent>();
    base.OnModelCreating(modelBuilder);

    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
      if (entityType.ClrType is null || !typeof(ITenantOwnedEntity).IsAssignableFrom(entityType.ClrType))
      {
        continue;
      }

      ConfigureTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
    }

    foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetForeignKeys()))
    {
      foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
    }
  }

  // AUDITING AND THE TENANT GUARD HOOK THE INNERMOST OVERLOADS, and deliberately not the convenience ones.
  //
  // EF Core's own chain is `SaveChanges()` -> `SaveChanges(bool)` and `SaveChangesAsync(ct)` ->
  // `SaveChangesAsync(bool, ct)`, both by virtual dispatch. Overriding only the convenience overloads — as
  // this type previously did for the async pair — leaves `SaveChanges()`, `SaveChanges(bool)` and
  // `SaveChangesAsync(bool, ct)` writing without audit stamps or the tenant-ownership guard. Hooking the
  // two innermost overloads covers all four entry points and applies the rules exactly once, because the
  // convenience overloads reach the database only through these.
  public override Task<int> SaveChangesAsync(
    bool acceptAllChangesOnSuccess,
    CancellationToken cancellationToken = default)
  {
    ApplyPersistenceRules();
    return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
  }

  public override int SaveChanges(bool acceptAllChangesOnSuccess)
  {
    ApplyPersistenceRules();
    return base.SaveChanges(acceptAllChangesOnSuccess);
  }

  private void ConfigureTenantFilter<TEntity>(ModelBuilder modelBuilder)
    where TEntity : class, ITenantOwnedEntity
  {
    modelBuilder.Entity<TEntity>().HasQueryFilter(entity =>
      CurrentTenantId.HasValue && entity.TenantId == CurrentTenantId.Value);
  }

  private void ApplyPersistenceRules()
  {
    var now = dateTimeProvider.UtcNow.ToUniversalTime();
    var userId = currentUser.UserId;

    foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
    {
      if (entry.State == EntityState.Added)
      {
        entry.Entity.CreatedUtc = now;
        entry.Entity.CreatedBy = userId;
      }

      if (entry.State is EntityState.Added or EntityState.Modified)
      {
        entry.Entity.ModifiedUtc = now;
        entry.Entity.ModifiedBy = userId;
      }
    }

    foreach (var entry in ChangeTracker.Entries<ITenantOwnedEntity>())
    {
      if (entry.State == EntityState.Added)
      {
        AssignTenant(entry.Entity);
      }
      else if (entry.State == EntityState.Modified && entry.Property(nameof(ITenantOwnedEntity.TenantId)).IsModified)
      {
        throw new InvalidOperationException("Tenant ownership cannot be changed after an entity is created.");
      }
    }
  }

  private void AssignTenant(ITenantOwnedEntity entity)
  {
    if (CurrentTenantId is not { } tenantId)
    {
      throw new InvalidOperationException("A trusted tenant context is required to save tenant-owned entities.");
    }

    if (entity.TenantId == Guid.Empty)
    {
      entity.TenantId = tenantId;
      return;
    }

    if (entity.TenantId != tenantId)
    {
      throw new InvalidOperationException("Tenant ownership must match the trusted tenant context.");
    }
  }

  private sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
  {
    public UtcDateTimeOffsetConverter()
      : base(value => value.ToUniversalTime(), value => value.ToUniversalTime())
    {
    }
  }
}
