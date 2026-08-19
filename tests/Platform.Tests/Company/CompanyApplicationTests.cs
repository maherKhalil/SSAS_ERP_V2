using SSAS.BuildingBlocks.Tenancy.Persistence;
using System.Reflection;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.Companies;

public sealed class CompanyApplicationTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);
  private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

  // ---------- Create ----------

  [Fact]
  [Trait("Requirement", "FR-CMP-0101")]
  [Trait("Acceptance", "AC-CMP-0001")]
  public async Task Create_uses_trusted_tenant_and_commits_once_returning_company_id()
  {
    var repository = new FakeCompanyRepository();
    var unitOfWork = new FakeUnitOfWork();
    var handler = new CreateCompanyCommandHandler(repository, unitOfWork, Tenant(), User("user-1"), Clock());

    var result = await handler.HandleAsync(new CreateCompanyCommand("  acme-eg  ", "  Acme Egypt  ", "egp"));

    Assert.True(result.IsSuccess);
    Assert.NotEqual(Guid.Empty, result.Value);
    Assert.Equal(result.Value, repository.Added?.CompanyId);
    Assert.Equal(TenantId, repository.Added?.TenantId);
    Assert.Equal("ACME-EG", repository.Added?.NormalizedCompanyCode);
    Assert.Equal("EGP", repository.Added?.BaseCurrencyCode.Value);
    Assert.Equal(CompanyStatus.Inactive, repository.Added?.Status);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  [Trait("Security", "SEC-CMP-0201")]
  public void Create_command_cannot_provide_a_tenant_id()
  {
    Assert.DoesNotContain(typeof(CreateCompanyCommand).GetProperties(), property =>
      property.Name.Contains("Tenant", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  [Trait("Security", "SEC-CMP-0201")]
  public async Task Create_requires_a_trusted_tenant_context()
  {
    var repository = new FakeCompanyRepository();
    var handler = new CreateCompanyCommandHandler(repository, new FakeUnitOfWork(), NoTenant(), User("user-1"), Clock());

    var result = await handler.HandleAsync(new CreateCompanyCommand("ACME-EG", "Acme Egypt", "EGP"));

    Assert.Equal(IdentityAccessErrors.Unauthorized, result.Error);
    Assert.Null(repository.Added);
  }

  [Theory]
  [InlineData("", "Acme", "EGP", "Company.InvalidCode")]
  [InlineData("AB\tC", "Acme", "EGP", "Company.InvalidCode")]
  [InlineData("ACME-EG", "", "EGP", "Company.InvalidName")]
  [InlineData("ACME-EG", "Acme", "ZZZ", "Company.InvalidBaseCurrency")]
  [InlineData("ACME-EG", "Acme", "US", "Company.InvalidBaseCurrency")]
  [Trait("Requirement", "FR-CMP-0101")]
  public async Task Create_rejects_invalid_value_objects(string code, string name, string currency, string expectedCode)
  {
    var repository = new FakeCompanyRepository();
    var unitOfWork = new FakeUnitOfWork();
    var handler = new CreateCompanyCommandHandler(repository, unitOfWork, Tenant(), User("user-1"), Clock());

    var result = await handler.HandleAsync(new CreateCompanyCommand(code, name, currency));

    Assert.True(result.IsFailure);
    Assert.Equal(expectedCode, result.Error.Code);
    Assert.Null(repository.Added);
    Assert.Equal(0, unitOfWork.SaveCount);
  }

  [Fact]
  [Trait("Acceptance", "AC-CMP-0002")]
  [Trait("Scenario", "TS-CMP-0020")]
  public async Task Create_pre_check_rejects_duplicate_normalized_code_without_saving()
  {
    var repository = new FakeCompanyRepository { CodeExists = true };
    var unitOfWork = new FakeUnitOfWork();
    var handler = new CreateCompanyCommandHandler(repository, unitOfWork, Tenant(), User("user-1"), Clock());

    var result = await handler.HandleAsync(new CreateCompanyCommand("ACME-EG", "Acme Egypt", "EGP"));

    Assert.Equal(CompanyErrors.CodeConflict, result.Error);
    Assert.Null(repository.Added);
    Assert.Equal(0, unitOfWork.SaveCount);
  }

  [Fact]
  [Trait("Acceptance", "AC-CMP-0002")]
  [Trait("Scenario", "TS-CMP-0028")]
  public async Task Create_maps_the_database_unique_violation_to_code_conflict()
  {
    var repository = new FakeCompanyRepository();
    var unitOfWork = new FakeUnitOfWork { Failure = IdentityAccessErrors.UniqueConstraintViolation };
    var handler = new CreateCompanyCommandHandler(repository, unitOfWork, Tenant(), User("user-1"), Clock());

    var result = await handler.HandleAsync(new CreateCompanyCommand("ACME-EG", "Acme Egypt", "EGP"));

    Assert.Equal(CompanyErrors.CodeConflict, result.Error);
    Assert.NotNull(repository.Added);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task Create_propagates_a_generic_write_failure()
  {
    var unitOfWork = new FakeUnitOfWork { Failure = IdentityAccessErrors.WriteFailure };
    var handler = new CreateCompanyCommandHandler(new FakeCompanyRepository(), unitOfWork, Tenant(), User("user-1"), Clock());

    var result = await handler.HandleAsync(new CreateCompanyCommand("ACME-EG", "Acme Egypt", "EGP"));

    Assert.Equal(IdentityAccessErrors.WriteFailure, result.Error);
  }

  [Fact]
  public async Task Create_propagates_cancellation()
  {
    var handler = new CreateCompanyCommandHandler(new FakeCompanyRepository(), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock());
    using var source = new CancellationTokenSource();
    await source.CancelAsync();

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
      handler.HandleAsync(new CreateCompanyCommand("ACME-EG", "Acme Egypt", "EGP"), source.Token));
  }

  // ---------- Update profile ----------

  [Fact]
  [Trait("Requirement", "FR-CMP-0104")]
  public async Task Update_profile_succeeds_and_commits()
  {
    var company = CompanyInStatus(CompanyStatus.Inactive, [1]);
    var repository = new FakeCompanyRepository(company);
    var unitOfWork = new FakeUnitOfWork();
    var handler = new UpdateCompanyProfileCommandHandler(repository, unitOfWork, Tenant(), User("user-1"), Clock());

    var result = await handler.HandleAsync(new UpdateCompanyProfileCommand(company.CompanyId, "Acme Egypt LLC", [1]));

    Assert.True(result.IsSuccess);
    Assert.Equal("Acme Egypt LLC", company.CompanyName.Value);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task Update_profile_returns_not_found_for_missing_company()
  {
    var handler = new UpdateCompanyProfileCommandHandler(new FakeCompanyRepository(), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock());

    var result = await handler.HandleAsync(new UpdateCompanyProfileCommand(Guid.NewGuid(), "New Name", [1]));

    Assert.Equal(CompanyErrors.NotFound, result.Error);
  }

  [Fact]
  [Trait("Acceptance", "AC-CMP-0014")]
  public async Task Update_profile_rejects_a_stale_rowversion_without_saving()
  {
    var company = CompanyInStatus(CompanyStatus.Inactive, [1]);
    var unitOfWork = new FakeUnitOfWork();
    var handler = new UpdateCompanyProfileCommandHandler(new FakeCompanyRepository(company), unitOfWork, Tenant(), User("user-1"), Clock());

    var result = await handler.HandleAsync(new UpdateCompanyProfileCommand(company.CompanyId, "New Name", [9]));

    Assert.Equal(IdentityAccessErrors.ConcurrencyConflict, result.Error);
    Assert.Equal(0, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task Update_profile_rejects_an_invalid_name()
  {
    var company = CompanyInStatus(CompanyStatus.Inactive, [1]);
    var handler = new UpdateCompanyProfileCommandHandler(new FakeCompanyRepository(company), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock());

    var result = await handler.HandleAsync(new UpdateCompanyProfileCommand(company.CompanyId, "   ", [1]));

    Assert.Equal("Company.InvalidName", result.Error.Code);
  }

  [Fact]
  public async Task Update_profile_propagates_the_domain_archived_rejection()
  {
    var company = CompanyInStatus(CompanyStatus.Archived, [1]);
    var handler = new UpdateCompanyProfileCommandHandler(new FakeCompanyRepository(company), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock());

    var result = await handler.HandleAsync(new UpdateCompanyProfileCommand(company.CompanyId, "New Name", [1]));

    Assert.Equal(CompanyErrors.InvalidTransition, result.Error);
  }

  [Fact]
  [Trait("Security", "SEC-CMP-0208")]
  public void Update_profile_command_exposes_only_the_mutable_name_and_rowversion()
  {
    var properties = typeof(UpdateCompanyProfileCommand).GetProperties().Select(property => property.Name).ToArray();

    Assert.Equal(3, properties.Length);
    Assert.Contains("CompanyId", properties);
    Assert.Contains("CompanyName", properties);
    Assert.Contains("ExpectedRowVersion", properties);
    Assert.DoesNotContain(properties, name =>
      name.Contains("Code", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Currency", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Status", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Tenant", StringComparison.OrdinalIgnoreCase));
  }

  // ---------- Activate / Deactivate / Archive ----------

  [Fact]
  [Trait("Requirement", "FR-CMP-0105")]
  [Trait("Requirement", "FR-CMP-0106")]
  [Trait("Acceptance", "AC-CMP-0006")]
  public async Task Activate_then_deactivate_succeed_with_matching_rowversion()
  {
    var company = CompanyInStatus(CompanyStatus.Inactive, [1]);
    var repository = new FakeCompanyRepository(company);
    var unitOfWork = new FakeUnitOfWork();

    var activate = await new ActivateCompanyCommandHandler(repository, unitOfWork, Tenant(), User("user-1"), Clock())
      .HandleAsync(new ActivateCompanyCommand(company.CompanyId, CompanyStatusChangeReason.Administrative, [1]));
    Assert.True(activate.IsSuccess);
    Assert.Equal(CompanyStatus.Active, company.Status);

    var deactivate = await new DeactivateCompanyCommandHandler(repository, unitOfWork, Tenant(), User("user-1"), Clock())
      .HandleAsync(new DeactivateCompanyCommand(company.CompanyId, CompanyStatusChangeReason.Operational, [1]));
    Assert.True(deactivate.IsSuccess);
    Assert.Equal(CompanyStatus.Inactive, company.Status);
    Assert.Equal(2, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task Activate_returns_not_found_for_missing_company()
  {
    var result = await new ActivateCompanyCommandHandler(new FakeCompanyRepository(), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock())
      .HandleAsync(new ActivateCompanyCommand(Guid.NewGuid(), CompanyStatusChangeReason.Administrative, [1]));

    Assert.Equal(CompanyErrors.NotFound, result.Error);
  }

  [Fact]
  public async Task Activate_rejects_a_stale_rowversion()
  {
    var company = CompanyInStatus(CompanyStatus.Inactive, [1]);
    var unitOfWork = new FakeUnitOfWork();
    var result = await new ActivateCompanyCommandHandler(new FakeCompanyRepository(company), unitOfWork, Tenant(), User("user-1"), Clock())
      .HandleAsync(new ActivateCompanyCommand(company.CompanyId, CompanyStatusChangeReason.Administrative, [7]));

    Assert.Equal(IdentityAccessErrors.ConcurrencyConflict, result.Error);
    Assert.Equal(0, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task Activate_rejects_a_created_reason_and_an_invalid_transition()
  {
    var inactive = CompanyInStatus(CompanyStatus.Inactive, [1]);
    var createdReason = await new ActivateCompanyCommandHandler(new FakeCompanyRepository(inactive), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock())
      .HandleAsync(new ActivateCompanyCommand(inactive.CompanyId, CompanyStatusChangeReason.Created, [1]));
    Assert.Equal(CompanyErrors.InvalidTransitionReason, createdReason.Error);

    var active = CompanyInStatus(CompanyStatus.Active, [1]);
    var invalidTransition = await new ActivateCompanyCommandHandler(new FakeCompanyRepository(active), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock())
      .HandleAsync(new ActivateCompanyCommand(active.CompanyId, CompanyStatusChangeReason.Administrative, [1]));
    Assert.Equal(CompanyErrors.InvalidTransition, invalidTransition.Error);
  }

  [Fact]
  public async Task Deactivate_rejects_not_found_stale_reason_and_invalid_transition()
  {
    var missing = await new DeactivateCompanyCommandHandler(new FakeCompanyRepository(), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock())
      .HandleAsync(new DeactivateCompanyCommand(Guid.NewGuid(), CompanyStatusChangeReason.Administrative, [1]));
    Assert.Equal(CompanyErrors.NotFound, missing.Error);

    var active = CompanyInStatus(CompanyStatus.Active, [1]);
    var stale = await new DeactivateCompanyCommandHandler(new FakeCompanyRepository(active), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock())
      .HandleAsync(new DeactivateCompanyCommand(active.CompanyId, CompanyStatusChangeReason.Administrative, [2]));
    Assert.Equal(IdentityAccessErrors.ConcurrencyConflict, stale.Error);

    var createdReason = await new DeactivateCompanyCommandHandler(new FakeCompanyRepository(active), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock())
      .HandleAsync(new DeactivateCompanyCommand(active.CompanyId, CompanyStatusChangeReason.Created, [1]));
    Assert.Equal(CompanyErrors.InvalidTransitionReason, createdReason.Error);

    var inactive = CompanyInStatus(CompanyStatus.Inactive, [1]);
    var invalidTransition = await new DeactivateCompanyCommandHandler(new FakeCompanyRepository(inactive), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock())
      .HandleAsync(new DeactivateCompanyCommand(inactive.CompanyId, CompanyStatusChangeReason.Administrative, [1]));
    Assert.Equal(CompanyErrors.InvalidTransition, invalidTransition.Error);
  }

  [Theory]
  [InlineData(CompanyStatus.Inactive)]
  [InlineData(CompanyStatus.Active)]
  [Trait("Requirement", "FR-CMP-0107")]
  [Trait("Acceptance", "AC-CMP-0008")]
  public async Task Archive_succeeds_from_inactive_and_active(CompanyStatus status)
  {
    var company = CompanyInStatus(status, [1]);
    var unitOfWork = new FakeUnitOfWork();
    var result = await new ArchiveCompanyCommandHandler(new FakeCompanyRepository(company), unitOfWork, Tenant(), User("user-1"), Clock())
      .HandleAsync(new ArchiveCompanyCommand(company.CompanyId, CompanyStatusChangeReason.CustomerRequest, [1]));

    Assert.True(result.IsSuccess);
    Assert.Equal(CompanyStatus.Archived, company.Status);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task Archive_rejects_not_found_stale_reason_and_terminal_state()
  {
    var missing = await new ArchiveCompanyCommandHandler(new FakeCompanyRepository(), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock())
      .HandleAsync(new ArchiveCompanyCommand(Guid.NewGuid(), CompanyStatusChangeReason.Administrative, [1]));
    Assert.Equal(CompanyErrors.NotFound, missing.Error);

    var inactive = CompanyInStatus(CompanyStatus.Inactive, [1]);
    var stale = await new ArchiveCompanyCommandHandler(new FakeCompanyRepository(inactive), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock())
      .HandleAsync(new ArchiveCompanyCommand(inactive.CompanyId, CompanyStatusChangeReason.Administrative, [5]));
    Assert.Equal(IdentityAccessErrors.ConcurrencyConflict, stale.Error);

    var createdReason = await new ArchiveCompanyCommandHandler(new FakeCompanyRepository(inactive), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock())
      .HandleAsync(new ArchiveCompanyCommand(inactive.CompanyId, CompanyStatusChangeReason.Created, [1]));
    Assert.Equal(CompanyErrors.InvalidTransitionReason, createdReason.Error);

    var archived = CompanyInStatus(CompanyStatus.Archived, [1]);
    var terminal = await new ArchiveCompanyCommandHandler(new FakeCompanyRepository(archived), new FakeUnitOfWork(), Tenant(), User("user-1"), Clock())
      .HandleAsync(new ArchiveCompanyCommand(archived.CompanyId, CompanyStatusChangeReason.Administrative, [1]));
    Assert.Equal(CompanyErrors.InvalidTransition, terminal.Error);
  }

  [Fact]
  public async Task Lifecycle_handlers_require_a_trusted_tenant_context()
  {
    var company = CompanyInStatus(CompanyStatus.Inactive, [1]);
    var handler = new ActivateCompanyCommandHandler(new FakeCompanyRepository(company), new FakeUnitOfWork(), NoTenant(), User("user-1"), Clock());

    var result = await handler.HandleAsync(new ActivateCompanyCommand(company.CompanyId, CompanyStatusChangeReason.Administrative, [1]));

    Assert.Equal(IdentityAccessErrors.Unauthorized, result.Error);
  }

  // ---------- Queries ----------

  [Fact]
  [Trait("Requirement", "FR-CMP-0102")]
  [Trait("Acceptance", "AC-CMP-0010")]
  public async Task Get_company_returns_the_projection_or_not_found()
  {
    var company = CompanyInStatus(CompanyStatus.Inactive, [1]);
    var dto = MapDto(company);
    var readService = new FakeCompanyReadService(dto);

    var found = await new GetCompanyByIdQueryHandler(readService, Tenant(), User("user-1")).HandleAsync(new GetCompanyByIdQuery(company.CompanyId));
    var missing = await new GetCompanyByIdQueryHandler(readService, Tenant(), User("user-1")).HandleAsync(new GetCompanyByIdQuery(Guid.NewGuid()));

    Assert.Equal(dto, found.Value);
    Assert.Equal(CompanyErrors.NotFound, missing.Error);
  }

  [Fact]
  [Trait("Requirement", "FR-CMP-0103")]
  [Trait("Decision", "DEC-CMP-0022")]
  public async Task List_applies_defaults_and_status_filter()
  {
    var company = CompanyInStatus(CompanyStatus.Active, [1]);
    var readService = new FakeCompanyReadService(MapDto(company));

    var defaults = await new ListCompaniesQueryHandler(readService, Tenant(), User("user-1")).HandleAsync(new ListCompaniesQuery());
    Assert.True(defaults.IsSuccess);
    Assert.Equal(1, readService.RequestedPageNumber);
    Assert.Equal(50, readService.RequestedPageSize);

    var filtered = await new ListCompaniesQueryHandler(readService, Tenant(), User("user-1")).HandleAsync(new ListCompaniesQuery(CompanyStatus.Active, 2, 25));
    Assert.True(filtered.IsSuccess);
    Assert.Equal(CompanyStatus.Active, readService.RequestedStatus);
    Assert.Single(filtered.Value.Items);
  }

  [Theory]
  [InlineData(0, 50, null)]
  [InlineData(1, 0, null)]
  [InlineData(1, 201, null)]
  [InlineData(1, 50, 999)]
  [Trait("Decision", "DEC-CMP-0022")]
  public async Task List_rejects_out_of_range_paging_and_undefined_status(int pageNumber, int pageSize, int? status)
  {
    var handler = new ListCompaniesQueryHandler(new FakeCompanyReadService(), Tenant(), User("user-1"));

    var result = await handler.HandleAsync(new ListCompaniesQuery((CompanyStatus?)status, pageNumber, pageSize));

    Assert.True(result.IsFailure);
    Assert.Equal("Company.ListFilterInvalid", result.Error.Code);
  }

  [Fact]
  public async Task Get_company_propagates_cancellation()
  {
    var handler = new GetCompanyByIdQueryHandler(new FakeCompanyReadService(), Tenant(), User("user-1"));
    using var source = new CancellationTokenSource();
    await source.CancelAsync();

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
      handler.HandleAsync(new GetCompanyByIdQuery(Guid.NewGuid()), source.Token));
  }

  // ---------- Permissions ----------

  [Fact]
  [Trait("Decision", "DEC-CMP-0021")]
  public void Company_permission_names_are_exactly_the_approved_three()
  {
    Assert.Equal("Platform.Companies.View", PlatformPermissionNames.ViewCompanies);
    Assert.Equal("Platform.Companies.Manage", PlatformPermissionNames.ManageCompanies);
    Assert.Equal("Platform.Companies.Lifecycle", PlatformPermissionNames.CompanyLifecycle);
  }

  [Fact]
  [Trait("Decision", "DEC-CMP-0021")]
  public void Company_permission_catalog_contains_exactly_the_three_tenant_scoped_permissions()
  {
    var catalog = new PlatformPermissionCatalog();

    foreach (var name in new[] { PlatformPermissionNames.ViewCompanies, PlatformPermissionNames.ManageCompanies, PlatformPermissionNames.CompanyLifecycle })
    {
      Assert.True(catalog.TryGet(name, out var permission));
      Assert.Equal(PermissionScope.Tenant, permission.Scope);
    }

    var companyPermissions = catalog.All
      .Select(permission => permission.Name.Value)
      .Where(name => name.StartsWith("Platform.Companies.", StringComparison.Ordinal))
      .ToArray();
    Assert.Equal(3, companyPermissions.Length);
    Assert.DoesNotContain(companyPermissions, name =>
      name.Contains("Create", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Archive", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
  }

  // ---------- Helpers and fakes ----------

  private static Company CompanyInStatus(CompanyStatus status, byte[] rowVersion)
  {
    var company = Company.Create(
      TenantId,
      CompanyCode.Create("ACME-EG").Value,
      CompanyName.Create("Acme Egypt").Value,
      BaseCurrencyCode.Create("EGP").Value,
      "seed-actor",
      Guid.NewGuid(),
      Now).Value;

    if (status is CompanyStatus.Active)
    {
      Assert.True(company.Activate(CompanyStatusChangeReason.Administrative, "seed-actor", Guid.NewGuid(), Now).IsSuccess);
    }

    if (status is CompanyStatus.Archived)
    {
      Assert.True(company.Archive(CompanyStatusChangeReason.Administrative, "seed-actor", Guid.NewGuid(), Now).IsSuccess);
    }

    SetRowVersion(company, rowVersion);
    company.ClearDomainEvents();
    return company;
  }

  private static CompanyDto MapDto(Company company) => new(
    company.CompanyId,
    company.TenantId,
    company.CompanyCode.Value,
    company.CompanyName.Value,
    company.BaseCurrencyCode.Value,
    company.Status,
    company.CreatedUtc,
    company.CreatedBy,
    company.ModifiedUtc,
    company.ModifiedBy,
    company.StatusChangedUtc,
    company.StatusChangedBy,
    company.StatusChangeReasonCode,
    company.RowVersion);

  private static void SetRowVersion(Company company, byte[] value)
  {
    var field = typeof(Company).GetField(
      "<RowVersion>k__BackingField",
      BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(field);
    field.SetValue(company, value);
  }

  private static TestCurrentTenant Tenant() => new(TenantId);

  private static TestCurrentTenant NoTenant() => new(null);

  private static TestCurrentUser User(string? userId) => new(userId);

  private static TestClock Clock() => new();

  private sealed class FakeCompanyRepository(params Company[] companies) : ICompanyRepository
  {
    private readonly List<Company> values = [.. companies];

    public bool CodeExists { get; set; }

    public Company? Added { get; private set; }

    public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return Task.FromResult(values.SingleOrDefault(company => company.CompanyId == companyId));
    }

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCompanyCode, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return Task.FromResult(CodeExists || values.Any(company => company.NormalizedCompanyCode == normalizedCompanyCode));
    }

    public Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      Added = company;
      values.Add(company);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeUnitOfWork : ITenantUnitOfWork
  {
    public int SaveCount { get; private set; }

    public Error? Failure { get; init; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      SaveCount++;
      return Task.FromResult(Failure is null ? Result.Success(1) : Result.Failure<int>(Failure));
    }

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class FakeCompanyReadService(params CompanyDto[] companies) : ICompanyReadService
  {
    private readonly CompanyDto[] values = companies;

    public CompanyStatus? RequestedStatus { get; private set; }

    public int RequestedPageNumber { get; private set; }

    public int RequestedPageSize { get; private set; }

    public Task<CompanyDto?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return Task.FromResult(values.SingleOrDefault(company => company.CompanyId == companyId));
    }

    public Task<PagedResult<CompanyDto>> ListAsync(
      CompanyStatus? status,
      int pageNumber,
      int pageSize,
      CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      RequestedStatus = status;
      RequestedPageNumber = pageNumber;
      RequestedPageSize = pageSize;
      var filtered = values.Where(company => !status.HasValue || company.Status == status.Value).ToArray();
      return Task.FromResult(new PagedResult<CompanyDto>(filtered, pageNumber, pageSize, filtered.Length));
    }
  }

  private sealed class TestCurrentTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class TestCurrentUser(string? userId) : ICurrentUser
  {
    public string? UserId { get; } = userId;
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }
}
