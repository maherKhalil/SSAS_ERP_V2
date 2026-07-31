using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;

namespace SSAS.BuildingBlocks.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
  public static IServiceCollection AddPersistenceFoundation(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

    return services;
  }
}
