using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.Enums;

namespace SSAS.API.Tests.Companies;

// Company self-hosting endpoint test classes share one non-parallel collection so their in-memory
// hosts (JWT signing-key + DataProtection startup) do not start concurrently, which was observed to
// flake under contention. Test-only; no production behavior change.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CompanyApiEndpointGroup
{
  public const string Name = "Company API endpoints";
}

public static class CompanyEndpointTestServices
{
  // Registers every Company endpoint handler so any test host that calls the shared
  // MapPlatformCompanyEndpoints (which maps the full seven-route family) can build under the
  // Development-mode service-provider validation. Default no-op terminal stubs are added with TryAdd
  // so a test class that registers its own controllable stub first keeps it; unused handlers still
  // validate. Call this AFTER registering the class-specific stubs.
  public static IServiceCollection AddCompanyEndpointHandlers(this IServiceCollection services)
  {
    services.TryAddSingleton<ICompanyRepository, NoOpCompanyRepository>();
    services.TryAddSingleton<ICompanyReadService, NoOpCompanyReadService>();
    services.TryAddSingleton<IPlatformUnitOfWork, NoOpUnitOfWork>();
    services.TryAddSingleton<IDateTimeProvider, NoOpClock>();

    services.AddScoped<CreateCompanyCommandHandler>();
    services.AddScoped<ListCompaniesQueryHandler>();
    services.AddScoped<GetCompanyByIdQueryHandler>();
    services.AddScoped<UpdateCompanyProfileCommandHandler>();
    services.AddScoped<ActivateCompanyCommandHandler>();
    services.AddScoped<DeactivateCompanyCommandHandler>();
    services.AddScoped<ArchiveCompanyCommandHandler>();
    return services;
  }

  private sealed class NoOpCompanyRepository : ICompanyRepository
  {
    public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult<Company?>(null);

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCompanyCode, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task AddAsync(Company company, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class NoOpCompanyReadService : ICompanyReadService
  {
    public Task<CompanyDto?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult<CompanyDto?>(null);

    public Task<PagedResult<CompanyDto>> ListAsync(CompanyStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
      Task.FromResult(new PagedResult<CompanyDto>([], pageNumber < 1 ? 1 : pageNumber, pageSize < 1 ? 1 : pageSize, 0));
  }

  private sealed class NoOpUnitOfWork : IPlatformUnitOfWork
  {
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success(0));

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class NoOpClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => new(2026, 8, 10, 11, 0, 0, TimeSpan.Zero);
  }
}
