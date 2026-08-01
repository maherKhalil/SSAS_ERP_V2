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
using SSAS.Platform.Application.Identities;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Roles;
using SSAS.Platform.Application.TenantUsers;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Identity;

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
    services.AddScoped<ITenantRepository, TenantRepository>();
    services.AddScoped<ITenantUserReadService, TenantUserReadService>();
    services.AddScoped<IRoleReadService, RoleReadService>();
    services.AddScoped<ITenantReadService, TenantReadService>();
    services.AddScoped<ITenantAuthenticationEligibilityReadService, TenantAuthenticationEligibilityReadService>();
    services.AddScoped<IPlatformUnitOfWork, PlatformUnitOfWork>();
    services.AddSingleton<IPermissionCatalog, PlatformPermissionCatalog>();

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
        options.PasswordResetLifetime);
    });

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
    services.AddScoped<CreateTenantCommandHandler>();
    services.AddScoped<ActivateTenantCommandHandler>();
    services.AddScoped<SuspendTenantCommandHandler>();
    services.AddScoped<ReactivateTenantCommandHandler>();
    services.AddScoped<ArchiveTenantCommandHandler>();
    services.AddScoped<GetTenantQueryHandler>();
    services.AddScoped<ListTenantsQueryHandler>();
    services.AddScoped<GetTenantAuthenticationEligibilityQueryHandler>();

    return services;
  }
}
