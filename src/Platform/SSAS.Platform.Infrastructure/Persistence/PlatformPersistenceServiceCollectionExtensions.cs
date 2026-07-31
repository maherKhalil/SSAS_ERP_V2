using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Identities;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Roles;
using SSAS.Platform.Application.TenantUsers;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence;

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
    services.AddScoped<ITenantUserReadService, TenantUserReadService>();
    services.AddScoped<IRoleReadService, RoleReadService>();
    services.AddScoped<IPlatformUnitOfWork, PlatformUnitOfWork>();
    services.AddSingleton<IPermissionCatalog, PlatformPermissionCatalog>();

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

    return services;
  }
}
