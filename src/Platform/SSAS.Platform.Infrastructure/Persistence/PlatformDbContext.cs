using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Localization;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantStorage;
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

  public DbSet<PlatformSupportPrincipal> PlatformSupportPrincipals => Set<PlatformSupportPrincipal>();

  public DbSet<PlatformPermissionAssignment> PlatformPermissionAssignments => Set<PlatformPermissionAssignment>();

  public DbSet<AuthenticationAccount> AuthenticationAccounts => Set<AuthenticationAccount>();

  public DbSet<AccountActionToken> AccountActionTokens => Set<AccountActionToken>();

  public DbSet<Tenant> Tenants => Set<Tenant>();

  public DbSet<AuthenticationSession> AuthenticationSessions => Set<AuthenticationSession>();

  public DbSet<RefreshTokenRecord> RefreshTokenRecords => Set<RefreshTokenRecord>();

  public DbSet<PlatformAuthenticationSession> PlatformAuthenticationSessions => Set<PlatformAuthenticationSession>();

  public DbSet<PlatformRefreshTokenRecord> PlatformRefreshTokenRecords => Set<PlatformRefreshTokenRecord>();

  public DbSet<TenantSelectionTransaction> TenantSelectionTransactions => Set<TenantSelectionTransaction>();

  public DbSet<LocalizationCatalogState> LocalizationCatalogStates => Set<LocalizationCatalogState>();

  public DbSet<TenantLocalizationSettings> TenantLocalizationSettings => Set<TenantLocalizationSettings>();

  public DbSet<TenantLocalizationOverride> TenantLocalizationOverrides => Set<TenantLocalizationOverride>();

  public DbSet<TenantLocalizationOverrideVersion> TenantLocalizationOverrideVersions => Set<TenantLocalizationOverrideVersion>();

  // Tenant-storage registry (ADR-017). Platform operational metadata: neither type is ITenantOwnedEntity,
  // so routing and bootstrap read them without an ambient tenant filter.
  public DbSet<TenantDatabase> TenantDatabases => Set<TenantDatabase>();

  public DbSet<TenantDatabaseAssignment> TenantDatabaseAssignments => Set<TenantDatabaseAssignment>();

  // Backup and recovery metadata (ADR-022). Platform-plane operational metadata, deliberately here and NOT
  // in a tenant ERP database: it describes the physical database rather than living inside it, and a
  // database that cannot be reached is exactly when its protection state must still be readable.
  public DbSet<TenantDatabaseBackupPolicy> TenantDatabaseBackupPolicies => Set<TenantDatabaseBackupPolicy>();

  public DbSet<TenantDatabaseBackupRun> TenantDatabaseBackupRuns => Set<TenantDatabaseBackupRun>();

  // Restore-verification operations (ADR-022 §17, TS-Backup Phase D). A separate history from backup runs
  // because a verification creates a disposable database that can outlive the process that created it, and
  // safe automated cleanup depends on a durable record of which operation created which database.
  public DbSet<TenantDatabaseRestoreVerificationRun> TenantDatabaseRestoreVerificationRuns =>
    Set<TenantDatabaseRestoreVerificationRun>();

  // Shared → Dedicated cutover operations (ADR-020). Durable because the freeze must survive the process
  // that established it, and because ADR-020 requires the flip transaction to cover this record alongside
  // the assignment change and the version increment.
  public DbSet<TenantCutoverOperation> TenantCutoverOperations => Set<TenantCutoverOperation>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    // Only PLATFORM configurations. Company moved to TenantDbContext (ADR-017) and its configuration now
    // lives in the tenant configuration namespace; an unfiltered scan would silently pull it — and every
    // future tenant ERP entity — back into the platform model, recreating the boundary this slice removes.
    modelBuilder.ApplyConfigurationsFromAssembly(
      typeof(PlatformDbContext).Assembly,
      type => type.Namespace != TenantErp.TenantPersistenceConstants.ConfigurationNamespace);
    base.OnModelCreating(modelBuilder);
  }

  public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    PreventTenantDeletion();
    PreventPlatformSupportPrincipalDeletion();
    PreventPlatformPermissionAssignmentDeletion();
    PreventAuthenticationHistoryDeletion();
    PreventLocalizationHistoryMutation();
    PreventIdentityOwnershipChanges();
    PreventTenantStorageRegistryDeletion();
    PromoteAssignmentOwners();
    return base.SaveChangesAsync(cancellationToken);
  }

  // Routing history is retained operational state: an assignment is superseded by setting EndedUtc, never
  // by deletion, and a physical database record outlives the tenants that used it so past routing stays
  // reconstructable. Mirrors the existing authority/authentication history guards.
  private void PreventTenantStorageRegistryDeletion()
  {
    if (ChangeTracker.Entries().Any(entry =>
      entry.State == EntityState.Deleted && entry.Entity is TenantDatabase or TenantDatabaseAssignment))
    {
      throw new InvalidOperationException(
        "Tenant storage registry rows are retained routing history and cannot be physically deleted; end the assignment instead.");
    }
  }

  private void PreventLocalizationHistoryMutation()
  {
    if (ChangeTracker.Entries<TenantLocalizationOverrideVersion>()
      .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
    {
      throw new InvalidOperationException("Tenant localization override versions are immutable and cannot be updated or deleted.");
    }

    if (ChangeTracker.Entries().Any(entry => entry.State == EntityState.Deleted &&
      entry.Entity is LocalizationCatalogState or
        SSAS.Platform.Domain.Localization.TenantLocalizationSettings or
        TenantLocalizationOverride))
    {
      throw new InvalidOperationException("Localization state is retained and cannot be physically deleted.");
    }
  }

  private void PreventAuthenticationHistoryDeletion()
  {
    if (ChangeTracker.Entries().Any(entry =>
      entry.State == EntityState.Deleted && entry.Entity is AuthenticationSession or RefreshTokenRecord or TenantSelectionTransaction))
    {
      throw new InvalidOperationException("Authentication session, refresh-token, and tenant-selection history cannot be physically deleted.");
    }

    // Platform-plane session/refresh history is retained security state (DEC-TEN-0022), consistent with the
    // tenant guard above: revoke/compromise via status updates only, never physical delete.
    if (ChangeTracker.Entries().Any(entry =>
      entry.State == EntityState.Deleted && entry.Entity is PlatformAuthenticationSession or PlatformRefreshTokenRecord))
    {
      throw new InvalidOperationException("Platform authentication session and refresh-token history cannot be physically deleted.");
    }
  }

  private void PreventTenantDeletion()
  {
    if (ChangeTracker.Entries<Tenant>().Any(entry => entry.State == EntityState.Deleted))
    {
      throw new InvalidOperationException("Tenant rows cannot be physically deleted; use the Archive lifecycle transition.");
    }
  }

  private void PreventPlatformSupportPrincipalDeletion()
  {
    if (ChangeTracker.Entries<PlatformSupportPrincipal>().Any(entry => entry.State == EntityState.Deleted))
    {
      throw new InvalidOperationException("Platform-support principals are retained authority records and cannot be physically deleted.");
    }
  }

  private void PreventPlatformPermissionAssignmentDeletion()
  {
    if (ChangeTracker.Entries<PlatformPermissionAssignment>().Any(entry => entry.State == EntityState.Deleted))
    {
      throw new InvalidOperationException("Platform permission assignments are retained authority history and cannot be physically deleted; use revoke instead.");
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
