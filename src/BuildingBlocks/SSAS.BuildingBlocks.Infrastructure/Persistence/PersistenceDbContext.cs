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

    // ==================================================================================================
    // NO REFERENCE BETWEEN AGGREGATES CASCADES A DELETE. OWNERSHIP IS EXEMPT, DELIBERATELY.
    // ==================================================================================================
    //
    // ---- WHAT THE LOOP IS FOR.
    //
    // EF's convention makes a required reference `Cascade`, so `Employee` -> `Department`, `Journal` ->
    // `Account` and every other cross-aggregate reference would delete its dependents on the way past.
    // **This is an ERP of record**: a row disappearing because something upstream was removed is the
    // failure mode the whole append-only and archive-rather-than-delete posture exists to prevent. The
    // loop makes the default REFUSAL rather than propagation, once, instead of relying on ~200
    // configurations each remembering `.OnDelete(Restrict)`.
    //
    // ---- WHAT IT DELIBERATELY NO LONGER TOUCHES, AND WHY THAT IS NOT A WEAKENING.
    //
    // **Ownership foreign keys are skipped.** An owned entity has no independent existence — EF deletes
    // owned rows with their owner as a matter of definition, not of configuration — so `Restrict` here
    // does not protect an aggregate from a careless reference. **It is also UNEXPRESSIBLE**: the
    // migrations snapshot format serialises no delete behaviour for an owned relationship, so
    // rehydration implies `Cascade` while the model said `Restrict`, and the differ is right to report a
    // change. That disagreement is permanent and self-regenerating — **every migration scaffolded in
    // this repository carried six spurious foreign-key operations because of it**, and it cost two wrong
    // diagnoses (T-041, T-043) before the cause was found.
    //
    // ---- WHAT THE EXEMPTION GIVES UP, STATED RATHER THAN GLOSSED.
    //
    // It is not nothing. With `Restrict` on the ownership key, a **raw** `DELETE FROM SubscriptionPlans`
    // against a plan no subscription references would fail; with `Cascade` it succeeds and takes the
    // plan's module grants, limits and prices with it. That window is narrow — a plan any tenant is on is
    // still protected by `TenantSubscriptions`' own reference key, which this loop still restricts, and
    // `SubscriptionPlan`'s lifecycle has no removal at all — and it was never a protection anyone chose.
    // `SubscriptionPlanOwnershipCascadeSqlServerTests` asserts the behaviour we now have, so the trade is
    // recorded as a test rather than as a claim.
    foreach (var foreignKey in modelBuilder.Model.GetEntityTypes()
      .SelectMany(entityType => entityType.GetForeignKeys())
      .Where(foreignKey => !foreignKey.IsOwnership))
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
