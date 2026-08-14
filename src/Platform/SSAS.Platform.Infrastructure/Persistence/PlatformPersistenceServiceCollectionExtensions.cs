using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Application.Identities;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Application.Roles;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Application.TenantUsers;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Infrastructure.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Identity;
using SSAS.Platform.Infrastructure.PlatformSupport;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.BuildingBlocks.Localization.Generated;
using SSAS.Platform.Application.Localization;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Infrastructure.Localization;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;

namespace SSAS.Platform.Infrastructure;

public static class PlatformInfrastructureServiceCollectionExtensions
{
  public static IServiceCollection AddPlatformInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configuration);

    services.AddPersistenceFoundation();
    services.AddDbContext<PlatformDbContext>(options =>
    {
      var connectionString = configuration.GetConnectionString(PlatformPersistenceConstants.ConnectionStringName);
      if (string.IsNullOrWhiteSpace(connectionString))
      {
        throw new InvalidOperationException(
          $"ConnectionStrings:{PlatformPersistenceConstants.ConnectionStringName} is required to use Platform persistence.");
      }

      options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(
        PlatformPersistenceConstants.MigrationHistoryTable,
        PlatformPersistenceConstants.Schema));
    });

    services.AddScoped<IIdentityRepository, IdentityRepository>();
    services.AddScoped<ITenantUserRepository, TenantUserRepository>();
    services.AddScoped<IRoleRepository, RoleRepository>();
    services.AddScoped<IAuthenticationAccountRepository, AuthenticationAccountRepository>();
    services.AddScoped<IAccountActionTokenRepository, AccountActionTokenRepository>();
    services.AddScoped<IAuthenticationSessionRepository, AuthenticationSessionRepository>();
    services.AddScoped<IPlatformAuthenticationSessionRepository, PlatformAuthenticationSessionRepository>();
    services.AddScoped<ITenantSelectionTransactionRepository, TenantSelectionTransactionRepository>();
    services.AddScoped<ITenantRepository, TenantRepository>();
    services.AddScoped<ICompanyRepository, CompanyRepository>();
    services.AddScoped<ITenantLocalizationSettingsRepository, TenantLocalizationSettingsRepository>();
    services.AddScoped<ITenantLocalizationOverrideRepository, TenantLocalizationOverrideRepository>();
    services.AddScoped<ITenantUserReadService, TenantUserReadService>();
    services.AddScoped<IRoleReadService, RoleReadService>();
    services.AddScoped<ITenantReadService, TenantReadService>();
    services.AddScoped<ICompanyReadService, CompanyReadService>();
    services.AddScoped<ITenantAuthenticationEligibilityReadService, TenantAuthenticationEligibilityReadService>();
    services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();
    services.AddScoped<ITenantLocalizationHistoryReadService, TenantLocalizationHistoryReadService>();
    services.AddScoped<ITenantLocalizationOverrideReadService, TenantLocalizationOverrideReadService>();
    services.AddScoped<ITenantLocalizationAdministrationReadService, TenantLocalizationAdministrationReadService>();
    services.AddScoped<ITenantLocalizationVersionReader, TenantLocalizationVersionReader>();
    services.AddScoped<IIdentityTenantMembershipReadService, IdentityTenantMembershipReadService>();
    services.AddScoped<IAccessTokenClaimsProvider, AccessTokenClaimsProvider>();
    services.AddScoped<IPlatformAccessTokenClaimsProvider, PlatformAccessTokenClaimsProvider>();
    services.AddScoped<IPlatformSupportPrincipalRepository, PlatformSupportPrincipalRepository>();
    services.AddScoped<IPlatformSupportPermissionReadService, PlatformSupportPermissionReadService>();
    services.AddScoped<IPlatformSupportAuthorityReadService, PlatformSupportAuthorityReadService>();
    services.AddScoped<IPlatformSupportAuthorityStateReadService, PlatformSupportAuthorityStateReadService>();
    services.AddScoped<IPlatformSupportRecoverySerializer, PlatformSupportRecoverySerializer>();
    services.AddScoped<IPlatformSupportBootstrapService, PlatformSupportBootstrapService>();
    // Tenant-storage registry baseline (ADR-017, TS-1B).
    services.AddScoped<ITenantStorageBootstrapService, TenantStorageBootstrapService>();

    // Tenant-storage routing foundation (ADR-017, TS-1C). Resolver and connection factory only: nothing
    // routes application persistence through these yet, and no TenantDbContext, routing cache, health
    // gating or customer-managed path exists.
    services.AddScoped<ITenantDatabaseRegistryReadRepository, TenantDatabaseRegistryReadRepository>();

    // Routed tenant ERP persistence. The provider is scoped so one unit of work resolves routing once and
    // every repository in that request shares the resulting context — reads and writes in a single request
    // cannot straddle two databases. Deliberately NOT AddDbContext/AddDbContextPool: the connection is
    // chosen per creation from trusted routing, and pooled state could carry connection identity across
    // tenants (ADR-017 binding lifetime rules 2 and 4).
    services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();
    services.AddScoped<TenantDbContextProvider>();
    services.AddScoped<ITenantDbContextProvider>(provider => provider.GetRequiredService<TenantDbContextProvider>());
    services.AddScoped<ITenantUnitOfWork, TenantUnitOfWork>();
    services.AddScoped<ITenantDatabaseResolver, TenantDatabaseResolver>();
    services.AddScoped<CurrentTenantDatabaseRouteProvider>();
    services.AddSingleton<ITenantDatabaseConnectionFactory, TenantDatabaseConnectionFactory>();
    services.AddScoped<IPlatformUnitOfWork, PlatformUnitOfWork>();
    services.AddSingleton<IPermissionCatalog, PlatformPermissionCatalog>();
    services.AddSingleton<ILocalizationCatalog>(GeneratedLocalizationCatalog.Instance);
    services.AddSingleton<ILocalizationTenantCache, LocalizationMemoryCache>();
    services.AddSingleton<ILocalizationDiagnostics, LocalizationDiagnostics>();
    services.AddSingleton<IDomainEventConsumer, LocalizationCacheDomainEventConsumer>();
    services.AddScoped<ILocalizationTextResolver, LocalizationTextResolver>();
    services.AddScoped<ILocalizationManagementAuditReadiness, LocalizationManagementAuditReadiness>();
    services.AddScoped<ILocalizationCatalogActivationService, LocalizationCatalogActivationService>();
    services.AddHostedService<LocalizationCatalogActivationHostedService>();

    services.AddOptions<LocalizationManagementAuditReadinessOptions>()
      .Bind(configuration.GetSection(LocalizationManagementAuditReadinessOptions.SectionName));

    services.AddOptions<AuthenticationPolicyOptions>()
      .Bind(configuration.GetSection(AuthenticationPolicyOptions.SectionName))
      .Validate(options => options.MinimumPasswordLength >= 12, "Minimum password length must be at least 12.")
      .Validate(options => options.MaximumPasswordLength >= 64 && options.MaximumPasswordLength >= options.MinimumPasswordLength,
        "Maximum password length must support at least 64 characters and cannot be below the minimum.")
      .Validate(options => options.FailedAttemptThreshold >= 1, "Failed-attempt threshold must be positive.")
      .Validate(options => options.LockoutDuration > TimeSpan.Zero, "Lockout duration must be positive.")
      .Validate(options => options.FailedAttemptConcurrencyRetries is >= 0 and <= 3,
        "Failed-attempt concurrency retries must be between zero and three.")
      .Validate(options => options.InvitationLifetime > TimeSpan.Zero && options.PasswordResetLifetime > TimeSpan.Zero,
        "Action-token lifetimes must be positive.")
      .Validate(options => options.SessionIdleLifetime > TimeSpan.Zero &&
        options.SessionAbsoluteLifetime > TimeSpan.Zero &&
        options.SessionIdleLifetime <= options.SessionAbsoluteLifetime,
        "Session lifetimes must be positive and idle lifetime cannot exceed absolute lifetime.")
      .Validate(options => options.TenantSelectionLifetime > TimeSpan.Zero,
        "Tenant-selection lifetime must be positive.")
      .Validate(options => options.MaximumActiveSessions is >= 1 and <= AuthenticationPolicy.DefaultMaximumActiveSessions,
        "Maximum active sessions must be between one and the approved maximum.")
      .ValidateOnStart();
    services.AddSingleton(provider =>
    {
      var options = provider.GetRequiredService<IOptions<AuthenticationPolicyOptions>>().Value;
      return new AuthenticationPolicy(
        options.MinimumPasswordLength,
        options.MaximumPasswordLength,
        options.FailedAttemptThreshold,
        options.LockoutDuration,
        options.FailedAttemptConcurrencyRetries,
        options.InvitationLifetime,
        options.PasswordResetLifetime,
        options.SessionIdleLifetime,
        options.SessionAbsoluteLifetime,
        options.TenantSelectionLifetime,
        options.MaximumActiveSessions);
    });

    services.AddOptions<AuthenticationClientOptions>()
      .Bind(configuration.GetSection(AuthenticationClientOptions.SectionName))
      .Validate(options => options.AllowedClientIds is { Length: > 0 }, "Authentication client allowlist cannot be empty.")
      .Validate(options => options.AllowedClientIds is { Length: > 0 } && options.AllowedClientIds.All(clientId =>
          !string.IsNullOrWhiteSpace(clientId) &&
          clientId.Length <= AuthenticationClientId.MaximumLength &&
          string.Equals(clientId, clientId.Trim(), StringComparison.Ordinal)),
        "Authentication client identifiers must be exact, nonblank, and within the approved length.")
      .Validate(options => options.AllowedClientIds is { Length: > 0 } &&
        options.AllowedClientIds.Contains(AuthenticationClientId.V1Web, StringComparer.Ordinal),
        "The V1 production client ssas-erp-web must be allowlisted.")
      .ValidateOnStart();
    services.AddSingleton<IAuthenticationClientRegistry, AuthenticationClientRegistry>();
    services.AddSingleton<IAuthenticationTokenService, AuthenticationTokenService>();

    services.AddOptions<PasswordHasherOptions>()
      .Bind(configuration.GetSection("Authentication:PasswordHasher"))
      .Validate(options => options.IterationCount >= 100_000, "Password hashing iteration count must be at least 100000.")
      .ValidateOnStart();
    services.AddSingleton<IPasswordHasher<object>, PasswordHasher<object>>();
    services.AddSingleton<IPasswordHashingService, AspNetPasswordHashingService>();
    services.AddSingleton<IActionTokenService, ActionTokenService>();
    services.AddSingleton<IAuthenticationDiagnostics, AuthenticationDiagnostics>();

    services.AddOptions<CompromisedPasswordOptions>()
      .Bind(configuration.GetSection(CompromisedPasswordOptions.SectionName))
      .ValidateOnStart();
    services.AddSingleton<IValidateOptions<CompromisedPasswordOptions>, CompromisedPasswordOptionsValidator>();
    services.AddSingleton<ICompromisedPasswordChecker, OfflineCompromisedPasswordChecker>();
    services.AddSingleton<IPasswordPolicyValidator, PasswordPolicyValidator>();

    // Platform-support genesis/recovery bootstrap (ADR-016 / DEC-TEN-0019). Options are fail-closed
    // validated at startup; the hosted service runs one convergence pass after the app is built.
    services.AddOptions<PlatformSupportBootstrapOptions>()
      .Bind(configuration.GetSection(PlatformSupportBootstrapOptions.SectionName))
      .ValidateOnStart();
    services.AddSingleton<IValidateOptions<PlatformSupportBootstrapOptions>, PlatformSupportBootstrapOptionsValidator>();
    services.AddHostedService<PlatformSupportBootstrapHostedService>();

    // ServerKey is a trusted configuration lookup key; no address or credential is persisted in the
    // Platform database. `Servers` holds the per-server connection material that the connection factory
    // resolves keys against.
    //
    // ValidateOnStart is required now that TenantDbContext routes through this configuration: an
    // unconfigured or malformed server map used to be inert, and would now surface as a per-request
    // failure on the first tenant that tried to reach its database. Failing at startup turns that into a
    // deployment error instead of a production incident.
    services.AddOptions<TenantStorageOptions>()
      .Bind(configuration.GetSection(TenantStorageOptions.SectionName))
      .ValidateOnStart();
    services.AddSingleton<IValidateOptions<TenantStorageOptions>, TenantStorageOptionsValidator>();
    services.AddHostedService<TenantStorageBootstrapHostedService>();

    services.AddScoped<RegisterIdentityCommandHandler>();
    services.AddScoped<CreateTenantUserMembershipCommandHandler>();
    services.AddScoped<UpdateTenantUserProfileCommandHandler>();
    services.AddScoped<DeactivateTenantUserCommandHandler>();
    services.AddScoped<ReactivateTenantUserCommandHandler>();
    services.AddScoped<AssignRoleToTenantUserCommandHandler>();
    services.AddScoped<RemoveRoleFromTenantUserCommandHandler>();
    services.AddScoped<GetTenantUserByIdQueryHandler>();
    services.AddScoped<ListTenantUsersQueryHandler>();
    services.AddScoped<CreateCustomRoleCommandHandler>();
    services.AddScoped<UpdateCustomRoleCommandHandler>();
    services.AddScoped<RequestRoleRetirementCommandHandler>();
    services.AddScoped<RetireRoleCommandHandler>();
    services.AddScoped<AssignPermissionToRoleCommandHandler>();
    services.AddScoped<RemovePermissionFromRoleCommandHandler>();
    services.AddScoped<GetRoleByIdQueryHandler>();
    services.AddScoped<ListRolesQueryHandler>();
    services.AddScoped<ListPermissionCatalogQueryHandler>();
    services.AddScoped<ResolveEffectivePermissionsQueryHandler>();
    services.AddScoped<IssueTenantUserInvitationCommandHandler>();
    services.AddScoped<CompleteInvitationCommandHandler>();
    services.AddScoped<VerifyPasswordCredentialsCommandHandler>();
    services.AddScoped<IssuePasswordResetCommandHandler>();
    services.AddScoped<CompletePasswordResetCommandHandler>();
    services.AddScoped<AuthenticationSessionCreator>();
    services.AddScoped<BeginTenantAccessCommandHandler>();
    services.AddScoped<SelectTenantCommandHandler>();
    services.AddScoped<RefreshAuthenticationSessionCommandHandler>();
    services.AddScoped<PlatformAuthenticationSessionCreator>();
    services.AddScoped<RefreshPlatformAuthenticationSessionCommandHandler>();
    services.AddScoped<RevokeCurrentPlatformAuthenticationSessionCommandHandler>();
    services.AddScoped<RevokeCurrentAuthenticationSessionCommandHandler>();
    services.AddScoped<CreateTenantCommandHandler>();
    services.AddScoped<ActivateTenantCommandHandler>();
    services.AddScoped<SuspendTenantCommandHandler>();
    services.AddScoped<ReactivateTenantCommandHandler>();
    services.AddScoped<ArchiveTenantCommandHandler>();
    services.AddScoped<GetTenantQueryHandler>();
    services.AddScoped<ListTenantsQueryHandler>();
    // Platform-support authority read/query surface (DEC-TEN-0025), exposed over HTTP in Phase 4D behind
    // RequirePlatformPermission(Platform.Support.Administer).
    services.AddScoped<ListPlatformSupportPrincipalsQueryHandler>();
    services.AddScoped<GetPlatformSupportPrincipalQueryHandler>();
    services.AddScoped<ListPlatformPermissionAssignmentsQueryHandler>();
    services.AddScoped<GetActivePlatformSupportPermissionsQueryHandler>();
    // Platform-support authority mutations (DEC-TEN-0020/0021), exposed over HTTP in Phase 4D behind the same
    // Administer permission. The handlers already own domain authorization, actor derivation and lifecycle rules.
    services.AddScoped<RegisterPlatformSupportPrincipalCommandHandler>();
    services.AddScoped<GrantPlatformPermissionCommandHandler>();
    services.AddScoped<RevokePlatformPermissionCommandHandler>();
    services.AddScoped<DisablePlatformSupportPrincipalCommandHandler>();
    services.AddScoped<ReenablePlatformSupportPrincipalCommandHandler>();
    services.AddScoped<CreateCompanyCommandHandler>();
    services.AddScoped<UpdateCompanyProfileCommandHandler>();
    services.AddScoped<ActivateCompanyCommandHandler>();
    services.AddScoped<DeactivateCompanyCommandHandler>();
    services.AddScoped<ArchiveCompanyCommandHandler>();
    services.AddScoped<GetCompanyByIdQueryHandler>();
    services.AddScoped<ListCompaniesQueryHandler>();
    services.AddScoped<GetTenantAuthenticationEligibilityQueryHandler>();
    services.AddScoped<CreateTenantLocalizationOverrideCommandHandler>();
    services.AddScoped<UpdateTenantLocalizationOverrideCommandHandler>();
    services.AddScoped<UndoTenantLocalizationOverrideCommandHandler>();
    services.AddScoped<RestoreTenantLocalizationDefaultCommandHandler>();
    services.AddScoped<GetTenantLocalizationHistoryQueryHandler>();
    services.AddScoped<ListTenantLocalizationResourcesQueryHandler>();
    services.AddScoped<GetTenantLocalizationResourceQueryHandler>();
    services.AddScoped<PreviewTenantLocalizationOverrideCommandHandler>();

    return services;
  }
}
