using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
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
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Infrastructure.Branches;
using SSAS.Platform.Infrastructure.Companies;
using SSAS.Platform.Infrastructure.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Identity;
using SSAS.Platform.Infrastructure.PlatformSupport;
using SSAS.Platform.Infrastructure.RequestContext;
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
    // ADR-018 schema health and migration orchestration. Registered as services only: nothing here runs
    // automatically at startup and nothing is reachable from a request path, because DDL authority does
    // not belong in the process that serves requests.
    services.AddSingleton(TenantDatabaseHealthFreshness.Default);
    services.AddSingleton<ITenantDatabaseTrafficGate, TenantDatabaseTrafficGate>();
    services.AddScoped<ITenantDatabaseHealthWriter, TenantDatabaseHealthWriter>();
    services.AddScoped<ITenantDatabaseConnectivityHealthService, TenantDatabaseConnectivityHealthService>();
    services.AddScoped<ITenantDatabaseSchemaHealthService, TenantDatabaseSchemaHealthService>();
    services.AddScoped<ITenantDatabaseMigrationOrchestrator, TenantDatabaseMigrationOrchestrator>();

    // ADR-022 backup and recovery foundation (TS-Backup Phase A). Domain state and reads ONLY: the
    // recovery-readiness writer is registered as a SEPARATE interface from the health writer, because
    // ADR-022 requires one writer per dimension and compile-time separation is what keeps a component
    // holding one from expressing a write to another.
    //
    // Nothing here can cause a backup. There is no provider, no scheduler, no destination resolution and no
    // restore — those are Phase B, C and D, and the Phase B session-loss exit gate blocks Phase C.
    services.AddScoped<ITenantDatabaseRecoveryReadinessWriter, TenantDatabaseRecoveryReadinessWriter>();
    services.AddScoped<ITenantDatabaseBackupReadRepository, TenantDatabaseBackupReadRepository>();

    // TS-Storage Phase E: the recovery-gated Dedicated activation boundary.
    //
    // READ AND DECIDE ONLY. The gate authorises; it performs no freeze, copy, validation, routing flip or
    // invalidation, and registering it makes nothing writable that was not writable before. A cutover
    // orchestration calls it between validation and the routing flip.
    services.AddScoped<ITenantDatabaseRecoveryActivationReadRepository,
      TenantDatabaseRecoveryActivationReadRepository>();
    services.AddScoped<ITenantDatabaseRecoveryActivationGate, TenantDatabaseRecoveryActivationGate>();

    // TS-Backup Phase B: single-database SQL Server backup execution.
    //
    // The backup connection factory is registered SEPARATELY from ITenantDatabaseConnectionFactory and the
    // two must never be substituted for one another: the request-serving credential must never hold backup
    // privileges (ADR-022 §11).
    services.AddSingleton(TenantDatabaseBackupOperationalOptions.Default);
    services.AddScoped<ITenantDatabaseBackupConnectionFactory, TenantDatabaseBackupConnectionFactory>();
    services.AddScoped<ITenantDatabaseBackupRunStore, TenantDatabaseBackupRunStore>();
    services.AddScoped<ITenantDatabaseBackupProvider, SqlServerTenantDatabaseBackupProvider>();
    services.AddScoped<ITenantDatabaseBackupExecutor, TenantDatabaseBackupExecutor>();

    // TS-Backup Phase C: fleet scheduling (ADR-022 §13).
    //
    // BOUND FROM CONFIGURATION and validated at startup, following the TenantStorageOptions precedent. The
    // scheduler still DEFAULTS OFF — enabling unattended fleet backups also requires BackupServers
    // credentials and BackupDestinations to exist — but a deployment can now turn it on and tune it without
    // a rebuild, and a misconfigured enabled scheduler fails at startup rather than at 03:00.
    //
    // Note what is NOT configurable anywhere in this graph: the provider's visibility and in-flight checks.
    // Scheduling is an operational preference; that guard is a correctness precondition (compliance rule 29).
    services.AddOptions<TenantDatabaseBackupSchedulerOptions>()
      .Bind(configuration.GetSection(TenantDatabaseBackupSchedulerOptions.SectionName))
      .ValidateOnStart();
    services.AddSingleton<IValidateOptions<TenantDatabaseBackupSchedulerOptions>,
      TenantDatabaseBackupSchedulerOptionsValidator>();
    services.AddScoped<ITenantDatabaseBackupFleetReadRepository, TenantDatabaseBackupFleetReadRepository>();
    services.AddScoped<ITenantDatabaseBackupScheduler, TenantDatabaseBackupScheduler>();
    services.AddHostedService<TenantDatabaseBackupSchedulerHostedService>();

    // TS-Backup Phase D: restore-verification foundation (ADR-022 §17, v1.2).
    //
    // DEFAULTS OFF, and separately from the backup scheduler. Enabling restore verification additionally
    // requires a dedicated verification server, a privileged verification identity that can create and drop
    // databases, and file roots the verification instance's service account can write to — so a deployment
    // that has not been prepared fails startup validation rather than attempting restores it cannot perform.
    //
    // NOTE the asymmetry with backup credentials: VerificationServers is a SEPARATE key namespace, not a
    // second identity on the same keys. Verification reaches a server that hosts no authoritative tenant
    // data, and there is deliberately no fallback from it to BackupServers or Servers.
    services.AddOptions<TenantDatabaseRestoreVerificationOptions>()
      .Bind(configuration.GetSection(TenantDatabaseRestoreVerificationOptions.SectionName))
      .ValidateOnStart();
    services.AddSingleton<IValidateOptions<TenantDatabaseRestoreVerificationOptions>,
      TenantDatabaseRestoreVerificationOptionsValidator>();
    services.AddScoped<ITenantDatabaseVerificationConnectionFactory,
      TenantDatabaseVerificationConnectionFactory>();
    services.AddScoped<TenantDatabaseRestoreVerificationRunStore>();
    services.AddScoped<ITenantDatabaseRestoreVerificationRunStore>(provider =>
      provider.GetRequiredService<TenantDatabaseRestoreVerificationRunStore>());
    services.AddScoped<ITenantDatabaseRestoreVerificationReconciliationStore>(provider =>
      provider.GetRequiredService<TenantDatabaseRestoreVerificationRunStore>());
    services.AddScoped<ITenantDatabaseRestoreVerificationServerObserver,
      SqlServerTenantDatabaseRestoreVerificationServerObserver>();
    services.AddScoped<ITenantDatabaseRestoreVerificationReconciler,
      TenantDatabaseRestoreVerificationReconciler>();
    services.AddScoped<ITenantDatabaseRecoveryReadinessRefresher,
      TenantDatabaseRecoveryReadinessRefresher>();
    services.AddScoped<ITenantDatabaseRestoreVerificationFleetReadRepository,
      TenantDatabaseRestoreVerificationFleetReadRepository>();

    // TS-Backup Phase D6: the isolated restore provider. Registered as a service only — nothing schedules
    // it, and it implements no cleanup, because the destructive-permission model is not yet proven against a
    // directly-connected least-privilege identity on this Windows-auth-only instance.
    services.AddScoped<ITenantDatabaseRestoreVerificationProvider,
      SqlServerTenantDatabaseRestoreVerificationProvider>();

    // TS-Backup Phase D7: the executor is the only production caller of the restore provider, and the probe
    // opens the restored catalog through VerificationServers.
    //
    // A VERIFICATION SCHEDULER AND HOSTED SERVICE ARE REGISTERED BELOW, so autonomous execution is reachable
    // — but it is CONFIGURATION GATED and off by default: `TenantStorage:BackupVerification:Enabled` defaults
    // to false and is checked independently by both the hosted service and the scheduler sweep.
    //
    // Enabling it in production remains blocked on the carried LOW-C gate: reconciliation can release an
    // abandoned run's admission slot, and that path has not yet been proven against real process loss.
    services.AddScoped<ITenantDatabaseRestoreVerificationProbe, SqlServerRestoreVerificationProbe>();
    services.AddScoped<ITenantDatabaseRestoreVerificationExecutor,
      TenantDatabaseRestoreVerificationExecutor>();
    services.AddScoped<ITenantDatabaseRestoreVerificationScheduler,
      TenantDatabaseRestoreVerificationScheduler>();
    services.AddHostedService<TenantDatabaseRestoreVerificationHostedService>();

    // The destination resolver becomes an injected dependency here. Phase B's backup provider constructs one
    // directly from options, which was self-contained enough at the time; the restore provider needs the same
    // trust boundary, and two components sharing it is the point at which it belongs in the container rather
    // than being new-ed up twice.
    services.AddScoped<ITenantDatabaseBackupDestinationResolver, TenantDatabaseBackupDestinationResolver>();

    // TS-Storage Phase E1: the durable cutover operation and the tenant write freeze (ADR-020).
    //
    // The fence is registered BEFORE the context factory that consumes it, and there is no configuration
    // switch that disables it: a freeze that could be turned off at the write boundary would not be a
    // freeze. Only the timeouts are configurable.
    services.AddOptions<TenantCutoverFreezeOptions>()
      .Bind(configuration.GetSection(TenantCutoverFreezeOptions.SectionName));
    services.AddScoped<ITenantCutoverOperationStore>(provider => new TenantCutoverOperationStore(
      provider.GetRequiredService<PlatformDbContext>(),
      provider.GetRequiredService<IDateTimeProvider>(),
      provider.GetRequiredService<IOptions<TenantCutoverCopyOptions>>().Value.ReleaseOwnershipTimeout));
    services.AddScoped<ITenantWriteFence, TenantCutoverWriteFence>();
    services.AddScoped<ITenantCutoverFreezeService, TenantCutoverFreezeService>();

    // TS-Storage Phase E3: the Shared → Dedicated copy primitive (ADR-020).
    //
    // A SERVICE ONLY. Nothing schedules it, nothing exposes it over HTTP, and it advances no cutover state:
    // it copies an already-frozen operation's tenant data, validates it exactly, and reports. The routing
    // flip, the RoutingVersion increment and cache invalidation are the next slice and are absent here.
    services.AddOptions<TenantCutoverCopyOptions>()
      .Bind(configuration.GetSection(TenantCutoverCopyOptions.SectionName));
    // The concrete types are registered as well as their interfaces so the orchestrator can enter their
    // UNDER-OWNERSHIP paths — which take a non-forgeable ownership token and therefore cannot sit on the
    // Application-layer interfaces. Both registrations resolve the SAME scoped instance.
    // ---- THE TENANT MODEL THE CUTOVER COPIES FROM (FP-006C6, ADR-020).
    //
    // SINGLETON, because the contributor set is a composition fact fixed for the process, and building an EF
    // model is expensive. It resolves the SAME IEnumerable<ITenantModelContributor> the runtime context
    // factory resolves, which is what makes it impossible for the copy manifest and the application's own
    // persistence to describe different sets of tables.
    services.AddSingleton<ITenantModelSource, ComposedTenantModelSource>();

    services.AddScoped<TenantCutoverCopyService>();
    services.AddScoped<ITenantCutoverCopyService>(provider =>
      provider.GetRequiredService<TenantCutoverCopyService>());

    // TS-Storage Phase E4: the authoritative routing flip (ADR-020).
    //
    // It receives the INVALIDATOR rather than the cache: a flip may evict after committing, and must not be
    // able to write cache entries. Invalidation is an optimisation — E2's version check is what makes every
    // other instance converge — so nothing here can undo a committed flip.
    services.AddScoped<TenantCutoverRoutingFlipService>();
    services.AddScoped<ITenantCutoverRoutingFlipService>(provider =>
      provider.GetRequiredService<TenantCutoverRoutingFlipService>());

    // TS-Storage Phase E5: the orchestrator that composes E1-E4 (ADR-020).
    //
    // A SERVICE ONLY. No HTTP route, no hosted service, no scheduler — activating a one-way operation on
    // customer data is a separate operational and security decision this slice does not take.
    services.AddScoped<ITenantCutoverOrchestrator, TenantCutoverOrchestrator>();

    // Branch foundation B0/B1. The resolver is the ONE place branch scope is decided, and the authority
    // predicate behind it is the one place tenant-administrator status is decided.
    services.AddScoped<ITenantAdministratorAuthority, TenantAdministratorAuthority>();

    // ---- THE BRANCH READ PATH USES A CONTEXT FACTORY WITHOUT THE WRITE AUTHORIZER, deliberately.
    //
    // Otherwise the graph is circular: the routed context factory needs the branch write authorizer, which
    // needs the access resolver, which needs a routed context to read the tenant's branches. Resolving that
    // with lazy injection would hide the layering rather than fix it.
    //
    // The cycle is not real, because the resolver only ever READS branches — it never saves branch-owned
    // entities, so it has no use for the authorizer. Giving it a factory that omits one states that fact
    // instead of concealing it, and a context built here cannot be used to write branch-owned data because
    // a null authorizer refuses.
    services.AddScoped<ITenantBranchAccessResolver>(provider => new TenantBranchAccessResolver(
      provider.GetRequiredService<PlatformDbContext>(),
      BranchReadContextFactory(provider),
      provider.GetRequiredService<ITenantAdministratorAuthority>()));
    services.AddScoped<ITenantBranchService, TenantBranchService>();

    // Branch foundation B1b. The guard delegates to the SAME per-tenant resource B1a's deactivation takes,
    // which is what closes the R1/R2 races between assignment edits and branch deactivation.
    services.AddScoped<IBranchTopologyGuard, BranchTopologyGuard>();
    // Same reasoning: validating that branches are assignable is a read.
    services.AddScoped<ITenantBranchValidator>(provider =>
      new TenantBranchValidator(BranchReadContextFactory(provider)));
    services.AddScoped<IUserBranchAccessRepository, UserBranchAccessRepository>();
    services.AddScoped<SetTenantUserBranchesCommandHandler>();

    // Branch foundation B1c. The write authorizer is what makes the session's branch non-authoritative on
    // its own: it re-reads the durable session AND re-asks the resolver on every branch-owned write.
    services.AddScoped<IBranchWriteAuthorizer, BranchWriteAuthorizer>();

    // ---- FP-006C2: THE SANCTIONED BRANCH-TRANSFER CHANNEL (ADR-024 decision 3).
    //
    // SCOPED, so a declaration is reachable only from the operation that opened it — never static, which
    // would share it across concurrent requests, and never AsyncLocal, which would flow it into background
    // work that outlives the operation.
    //
    // The authorizer reads companies-free tenant state, so it takes the read-only context factory for the
    // same reason the branch and company resolvers do: the routed factory needs the authorizers, and the
    // authorizers need a context to read branches. The cycle is not real because this only ever READS.
    services.AddScoped<IBranchTransferScope, BranchTransferScope>();

    // ---- FP-006C3: THE MODULE-FACING READS (ADR-012).
    //
    // Both delegate to the contracts Platform already owns rather than resolving anything themselves, so a
    // module and Platform cannot disagree about who is acting or which branch is current.
    services.AddScoped<SSAS.BuildingBlocks.Tenancy.ICurrentTenantUser, RequestContext.CurrentTenantUser>();
    services.AddScoped<ICurrentBranchResolver, CurrentBranchResolver>();
    services.AddScoped<BuildingBlocks.Infrastructure.Persistence.ITenantDbContextAccessor, TenantDbContextAccessor>();
    services.AddScoped<IBranchTransferAuthorizer>(provider => new BranchTransferAuthorizer(
      provider.GetRequiredService<IBranchTransferScope>(),
      provider.GetRequiredService<ITenantBranchAccessResolver>(),
      provider.GetRequiredService<ITenantAdministratorAuthority>(),
      BranchReadContextFactory(provider),
      provider.GetService<ICurrentAuthenticationSession>()));

    // ---- FP-006C1: THE COMPANY DIMENSION (ADR-025). Registered alongside branch and shaped identically,
    // because ADR-025 chose the branch pattern for the sibling dimension.
    //
    // THE COMPANY READ PATH USES A CONTEXT FACTORY WITHOUT THE WRITE AUTHORIZERS, for the same reason the
    // branch read path does: otherwise the graph is circular — the routed factory needs the company write
    // authorizer, which needs the context resolver, which needs the access resolver, which needs a routed
    // context to read the tenant's companies. The cycle is not real, because the resolver only ever READS
    // companies; giving it a factory that omits the authorizers states that fact instead of concealing it.
    services.AddScoped<ITenantCompanyAccessResolver>(provider => new TenantCompanyAccessResolver(
      provider.GetRequiredService<PlatformDbContext>(),
      BranchReadContextFactory(provider),
      provider.GetRequiredService<ITenantAdministratorAuthority>()));

    // The five-step validation, in one place, used by BOTH the request path and the write boundary. Two
    // copies of "is this company usable" is how a read path and a write path come to disagree.
    services.AddScoped<ICompanyContextResolver, CompanyContextResolver>();

    // The trusted company context is composed HERE, with the resolver it depends on, rather than alongside
    // the request accessors that cannot satisfy it (FP-006C5). Idempotent, so a host may also call it
    // directly without producing a second registration.
    services.AddPlatformCompanyContext();

    // The write authorizer re-asks that validation on EVERY company-owned save, which is what makes a
    // company established at the start of a request non-authoritative by the time the request writes.
    services.AddScoped<ICompanyWriteAuthorizer, CompanyWriteAuthorizer>();

    services.AddScoped<IUserCompanyAccessRepository, UserCompanyAccessRepository>();
    services.AddScoped<IBranchSessionService, BranchSessionService>();

    services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();
    services.AddScoped<TenantDbContextProvider>();
    services.AddScoped<ITenantDbContextProvider>(provider => provider.GetRequiredService<TenantDbContextProvider>());
    services.AddScoped<ITenantUnitOfWork, TenantUnitOfWork>();

    // TS-Storage Phase E2: version-aware routing (ADR-020 "Resolver cache").
    //
    // ONE RESOLVER REACHES CONSUMERS. `TenantDatabaseResolver` is registered as its CONCRETE type and is
    // deliberately NOT registered against ITenantDatabaseResolver: everything that resolves routing —
    // TenantDbContextFactory, the route provider, every future cutover component — receives the
    // version-aware decorator, because a second registration would be a second routing mechanism, and the
    // one that skipped the version check would be the one that wrote to the wrong database.
    //
    // THE CACHE IS ONE SINGLETON WITH TWO FACES. Registered once by concrete type and resolved through for
    // both interfaces (the Phase D run-store precedent), so an invalidation and a read cannot land on
    // different dictionaries. Registering the two interfaces independently would compile, start, pass a
    // smoke test, and silently never invalidate anything.
    //
    // The reader is SCOPED because it reads through the scoped PlatformDbContext; the cache is a SINGLETON
    // because a per-request cache would expire before it was ever read.
    services.AddOptions<TenantRoutingCacheOptions>()
      .Bind(configuration.GetSection(TenantRoutingCacheOptions.SectionName));
    services.AddSingleton<TenantRoutingMemoryCache>();
    services.AddSingleton<ITenantRoutingCache>(provider =>
      provider.GetRequiredService<TenantRoutingMemoryCache>());
    services.AddSingleton<ITenantRoutingCacheInvalidator>(provider =>
      provider.GetRequiredService<TenantRoutingMemoryCache>());
    services.AddScoped<ITenantRoutingVersionReader, TenantRoutingVersionReader>();
    services.AddScoped<TenantDatabaseResolver>();
    services.AddScoped<ITenantDatabaseResolver>(provider => new VersionAwareTenantDatabaseResolver(
      provider.GetRequiredService<TenantDatabaseResolver>(),
      provider.GetRequiredService<ITenantRoutingVersionReader>(),
      provider.GetRequiredService<ITenantRoutingCache>(),
      provider.GetRequiredService<IOptions<TenantRoutingCacheOptions>>().Value,
      provider.GetRequiredService<IDateTimeProvider>()));
    services.AddScoped<CurrentTenantDatabaseRouteProvider>();
    services.AddSingleton<ITenantDatabaseConnectionFactory, TenantDatabaseConnectionFactory>();
    services.AddScoped<IPlatformUnitOfWork, PlatformUnitOfWork>();
    // ---- THE PERMISSION CATALOG IS COMPOSED, NOT PLATFORM-ONLY (FP-006P, ADR-012 r1.2).
    //
    // Platform's built-in definitions plus every EXPLICITLY registered module contribution. Registering
    // PlatformPermissionCatalog directly here is what left HR's five permissions unassignable: a role may
    // only be granted a name the catalog defines, and no catalog defined them.
    //
    // The built-in set is still registered as its own concrete type, because it remains the authoritative
    // Platform-owned half and several call sites want exactly that half.
    services.AddSingleton<PlatformPermissionCatalog>();
    services.AddSingleton<IPermissionCatalog, ComposedPermissionCatalog>();
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

  // A routed tenant context factory with NO branch write authorizer — the read-only half of the branch
  // graph. See the resolver registration above for why the alternative is a dependency cycle.
  private static TenantDbContextFactory BranchReadContextFactory(IServiceProvider provider) =>
    new(
      provider.GetRequiredService<ITenantDatabaseResolver>(),
      provider.GetRequiredService<ITenantDatabaseConnectionFactory>(),
      provider.GetRequiredService<ITenantDatabaseTrafficGate>(),
      provider.GetRequiredService<ICurrentUser>(),
      provider.GetRequiredService<ICurrentTenant>(),
      provider.GetRequiredService<IDateTimeProvider>(),
      provider.GetRequiredService<ITenantWriteFence>());
}
