using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.Tenants;
using PlatformIdentity = SSAS.Platform.Domain.Identities.Identity;

namespace SSAS.Platform.Infrastructure.Persistence;

public sealed class PlatformDbContext(
  DbContextOptions<PlatformDbContext> options,
  ICurrentUser currentUser,
  ICurrentTenant currentTenant,
  IDateTimeProvider dateTimeProvider) : PersistenceDbContext(options, currentUser, currentTenant, dateTimeProvider)
{
  public DbSet<PlatformIdentity> Identities => Set<PlatformIdentity>();

  public DbSet<TenantUser> TenantUsers => Set<TenantUser>();

  public DbSet<Role> Roles => Set<Role>();

  public DbSet<TenantUserRoleAssignment> TenantUserRoleAssignments => Set<TenantUserRoleAssignment>();

  public DbSet<RolePermissionAssignment> RolePermissionAssignments => Set<RolePermissionAssignment>();

  public DbSet<AuthenticationAccount> AuthenticationAccounts => Set<AuthenticationAccount>();

  public DbSet<AccountActionToken> AccountActionTokens => Set<AccountActionToken>();

  public DbSet<Tenant> Tenants => Set<Tenant>();

  public DbSet<AuthenticationSession> AuthenticationSessions => Set<AuthenticationSession>();

  public DbSet<RefreshTokenRecord> RefreshTokenRecords => Set<RefreshTokenRecord>();

  public DbSet<TenantSelectionTransaction> TenantSelectionTransactions => Set<TenantSelectionTransaction>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
    base.OnModelCreating(modelBuilder);
  }

  public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    PreventTenantDeletion();
    PreventAuthenticationHistoryDeletion();
    PreventIdentityOwnershipChanges();
    PromoteAssignmentOwners();
    return base.SaveChangesAsync(cancellationToken);
  }

  private void PreventAuthenticationHistoryDeletion()
  {
    if (ChangeTracker.Entries().Any(entry =>
      entry.State == EntityState.Deleted && entry.Entity is AuthenticationSession or RefreshTokenRecord or TenantSelectionTransaction))
    {
      throw new InvalidOperationException("Authentication session, refresh-token, and tenant-selection history cannot be physically deleted.");
    }
  }

  private void PreventTenantDeletion()
  {
    if (ChangeTracker.Entries<Tenant>().Any(entry => entry.State == EntityState.Deleted))
    {
      throw new InvalidOperationException("Tenant rows cannot be physically deleted; use the Archive lifecycle transition.");
    }
  }

  private void PreventIdentityOwnershipChanges()
  {
    foreach (var entry in ChangeTracker.Entries<TenantUser>().Where(entry => entry.State == EntityState.Modified))
    {
      if (entry.Property(user => user.IdentityId).IsModified)
      {
        throw new InvalidOperationException("Identity ownership cannot be changed after a tenant membership is created.");
      }
    }
  }

  private void PromoteAssignmentOwners()
  {
    var changedUserIds = ChangeTracker.Entries<TenantUserRoleAssignment>()
      .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
      .Select(entry => entry.Entity.TenantUserId)
      .ToHashSet();
    foreach (var entry in ChangeTracker.Entries<TenantUser>().Where(entry => entry.State == EntityState.Unchanged))
    {
      if (changedUserIds.Contains(entry.Entity.Id))
      {
        entry.Property(user => user.ModifiedUtc).IsModified = true;
      }
    }

    var changedRoleIds = ChangeTracker.Entries<RolePermissionAssignment>()
      .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
      .Select(entry => entry.Entity.RoleId)
      .Concat(ChangeTracker.Entries<TenantUserRoleAssignment>()
        .Where(entry => entry.State == EntityState.Added)
        .Select(entry => entry.Entity.RoleId))
      .ToHashSet();
    foreach (var entry in ChangeTracker.Entries<Role>().Where(entry => entry.State == EntityState.Unchanged))
    {
      if (changedRoleIds.Contains(entry.Entity.Id))
      {
        entry.Property(role => role.ModifiedUtc).IsModified = true;
      }
    }
  }
}
