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

  public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    ApplyPersistenceRules();
    return base.SaveChangesAsync(cancellationToken);
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
