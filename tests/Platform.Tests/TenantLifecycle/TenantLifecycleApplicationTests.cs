using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.TenantLifecycle;

public sealed class TenantLifecycleApplicationTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("Requirement", "FR-TEN-0101")]
  [Trait("Acceptance", "AC-TEN-0001")]
  [Trait("Decision", "DEC-TEN-0001")]
  public async Task Create_generates_authoritative_guid_enforces_code_uniqueness_and_commits_once()
  {
    var repository = new FakeTenantRepository();
    var unitOfWork = new FakeUnitOfWork();
    var handler = new CreateTenantCommandHandler(repository, unitOfWork, new TestCurrentUser("platform-actor"), new TestClock());

    var result = await handler.HandleAsync(new CreateTenantCommand("  Acme  ", "  Shared Name  "));

    Assert.True(result.IsSuccess);
    Assert.NotEqual(Guid.Empty, result.Value);
    Assert.Equal(result.Value, repository.Added?.TenantId);
    Assert.Equal("ACME", repository.Added?.NormalizedTenantCode);
    Assert.Equal(TenantStatus.Provisioning, repository.Added?.Status);
    Assert.Equal(TenantStatusChangeReason.Created, repository.Added?.StatusChangeReasonCode);
    Assert.Equal(1, unitOfWork.SaveCount);

    repository.CodeExists = true;
    var duplicate = await handler.HandleAsync(new CreateTenantCommand("acme", "Another Shared Name"));
    Assert.Equal("Tenant.CodeExists", duplicate.Error.Code);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0002")]
  [Trait("Scenario", "TS-TEN-0010")]
  public async Task Concurrent_unique_index_conflict_maps_to_the_same_tenant_code_error()
  {
    var repository = new FakeTenantRepository();
    var unitOfWork = new FakeUnitOfWork
    {
      Failure = IdentityAccessErrors.UniqueConstraintViolation
    };
    var handler = new CreateTenantCommandHandler(
      repository,
      unitOfWork,
      new TestCurrentUser("platform-actor"),
      new TestClock());

    var result = await handler.HandleAsync(new CreateTenantCommand("ACME", "Acme Trading"));

    Assert.Equal("Tenant.CodeExists", result.Error.Code);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  [Trait("Security", "SEC-TEN-0203")]
  public async Task Platform_actor_is_required_without_inferring_authority_from_tenant_roles()
  {
    var repository = new FakeTenantRepository();
    var unitOfWork = new FakeUnitOfWork();
    var anonymous = new CreateTenantCommandHandler(repository, unitOfWork, new TestCurrentUser(null, ["Administrator"]), new TestClock());

    var result = await anonymous.HandleAsync(new CreateTenantCommand("ACME", "Acme"));

    Assert.Equal("Tenant.Unauthorized", result.Error.Code);
    Assert.Null(repository.Added);
  }

  [Fact]
  [Trait("Scenario", "TS-TEN-0013")]
  [Trait("Acceptance", "AC-TEN-0014")]
  public async Task Lifecycle_handlers_enforce_rowversion_and_all_approved_transitions()
  {
    var tenant = CreateTenant();
    SetRowVersion(tenant, [1]);
    var repository = new FakeTenantRepository(tenant);
    var unitOfWork = new FakeUnitOfWork();
    var user = new TestCurrentUser("platform-actor");
    var clock = new TestClock();

    var stale = await new ActivateTenantCommandHandler(repository, unitOfWork, user, clock)
      .HandleAsync(new ActivateTenantCommand(tenant.TenantId, [2]));
    Assert.Equal("Persistence.ConcurrencyConflict", stale.Error.Code);

    Assert.True((await new ActivateTenantCommandHandler(repository, unitOfWork, user, clock)
      .HandleAsync(new ActivateTenantCommand(tenant.TenantId, [1]))).IsSuccess);
    Assert.True((await new SuspendTenantCommandHandler(repository, unitOfWork, user, clock)
      .HandleAsync(new SuspendTenantCommand(tenant.TenantId, TenantStatusChangeReason.Security, [1]))).IsSuccess);
    Assert.True((await new ReactivateTenantCommandHandler(repository, unitOfWork, user, clock)
      .HandleAsync(new ReactivateTenantCommand(tenant.TenantId, TenantStatusChangeReason.IssueResolved, [1]))).IsSuccess);
    Assert.True((await new ArchiveTenantCommandHandler(repository, unitOfWork, user, clock)
      .HandleAsync(new ArchiveTenantCommand(tenant.TenantId, TenantStatusChangeReason.CustomerClosure, [1]))).IsSuccess);
    Assert.Equal(TenantStatus.Archived, tenant.Status);
    Assert.Equal(4, unitOfWork.SaveCount);
  }

  [Theory]
  [InlineData(null, false, null, false, TenantAuthenticationIneligibilityReason.TenantNotFound)]
  [InlineData(TenantStatus.Provisioning, true, TenantStatus.Provisioning, false, TenantAuthenticationIneligibilityReason.Provisioning)]
  [InlineData(TenantStatus.Active, true, TenantStatus.Active, true, TenantAuthenticationIneligibilityReason.None)]
  [InlineData(TenantStatus.Suspended, true, TenantStatus.Suspended, false, TenantAuthenticationIneligibilityReason.Suspended)]
  [InlineData(TenantStatus.Archived, true, TenantStatus.Archived, false, TenantAuthenticationIneligibilityReason.Archived)]
  [Trait("Requirement", "FR-TEN-0108")]
  [Trait("Requirement", "REQ-PLT-0005")]
  [Trait("Decision", "DEC-IAM-0013")]
  [Trait("Requirement", "FR-AUTH-0120")]
  [Trait("Acceptance", "AC-AUTH-0018")]
  [Trait("Scenario", "TS-TEN-0012")]
  public void Eligibility_is_derived_exactly_and_has_no_name(
    TenantStatus? status,
    bool exists,
    TenantStatus? expectedStatus,
    bool eligible,
    TenantAuthenticationIneligibilityReason reason)
  {
    var tenantId = Guid.NewGuid();
    var result = TenantAuthenticationEligibilityResult.FromStatus(tenantId, status);

    Assert.Equal(tenantId, result.TenantId);
    Assert.Equal(exists, result.Exists);
    Assert.Equal(expectedStatus, result.TenantStatus);
    Assert.Equal(eligible, result.IsAuthenticationEligible);
    Assert.Equal(reason, result.TenantAuthenticationIneligibilityReason);
    Assert.DoesNotContain(result.GetType().GetProperties(), property => property.Name.Contains("Name", StringComparison.Ordinal));
  }

  [Fact]
  [Trait("Requirement", "FR-TEN-0102")]
  [Trait("Requirement", "FR-TEN-0103")]
  [Trait("Scenario", "TS-TEN-0011")]
  public async Task Get_and_list_return_bounded_safe_projections()
  {
    var tenant = CreateTenant();
    var dto = Map(tenant);
    var readService = new FakeTenantReadService(dto);
    var user = new TestCurrentUser("platform-actor");

    var get = await new GetTenantQueryHandler(readService, user).HandleAsync(new GetTenantQuery(tenant.TenantId));
    var list = await new ListTenantsQueryHandler(readService, user)
      .HandleAsync(new ListTenantsQuery(TenantStatus.Provisioning, 1, 50));
    var invalid = await new ListTenantsQueryHandler(readService, user)
      .HandleAsync(new ListTenantsQuery(null, 0, 101));

    Assert.Equal(dto, get.Value);
    Assert.Single(list.Value.Items);
    Assert.True(invalid.IsFailure);
    Assert.Equal(TenantStatus.Provisioning, readService.RequestedStatus);
  }

  [Fact]
  [Trait("NonFunctional", "NFR-TEN-0301")]
  [Trait("Scenario", "TS-TEN-0018")]
  public async Task Eligibility_query_delegates_and_propagates_cancellation()
  {
    var service = new FakeEligibilityReadService();
    var handler = new GetTenantAuthenticationEligibilityQueryHandler(service);
    using var source = new CancellationTokenSource();
    source.Cancel();

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
      handler.HandleAsync(new GetTenantAuthenticationEligibilityQuery(Guid.NewGuid()), source.Token));
  }

  private static Tenant CreateTenant()
  {
    return Tenant.Create(
      TenantCode.Create("ACME").Value,
      TenantName.Create("Acme Trading").Value,
      "platform-actor",
      Guid.NewGuid(),
      Now).Value;
  }

  private static TenantDto Map(Tenant tenant) => new(
    tenant.TenantId,
    tenant.TenantCode.Value,
    tenant.TenantName.Value,
    tenant.Status,
    tenant.CreatedUtc,
    tenant.CreatedBy,
    tenant.ModifiedUtc,
    tenant.ModifiedBy,
    tenant.StatusChangedUtc,
    tenant.StatusChangedBy,
    tenant.StatusChangeReasonCode,
    tenant.RowVersion);

  private static void SetRowVersion(Tenant tenant, byte[] value)
  {
    var field = typeof(Tenant).GetField(
      "<RowVersion>k__BackingField",
      System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    Assert.NotNull(field);
    field.SetValue(tenant, value);
  }

  private sealed class FakeTenantRepository(params Tenant[] tenants) : ITenantRepository
  {
    private readonly List<Tenant> values = [.. tenants];

    public bool CodeExists { get; set; }

    public Tenant? Added { get; private set; }

    public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(values.SingleOrDefault(tenant => tenant.TenantId == tenantId));

    public Task<Tenant?> GetByNormalizedCodeAsync(string normalizedTenantCode, CancellationToken cancellationToken = default) =>
      Task.FromResult(values.SingleOrDefault(tenant => tenant.NormalizedTenantCode == normalizedTenantCode));

    public Task<bool> NormalizedCodeExistsAsync(string normalizedTenantCode, CancellationToken cancellationToken = default) =>
      Task.FromResult(CodeExists || values.Any(tenant => tenant.NormalizedTenantCode == normalizedTenantCode));

    public Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      Added = tenant;
      values.Add(tenant);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeUnitOfWork : IPlatformUnitOfWork
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

  private sealed class FakeTenantReadService(params TenantDto[] tenants) : ITenantReadService
  {
    private readonly TenantDto[] values = tenants;

    public TenantStatus? RequestedStatus { get; private set; }

    public Task<TenantDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(values.SingleOrDefault(tenant => tenant.TenantId == tenantId));

    public Task<PagedResult<TenantDto>> ListAsync(
      TenantStatus? status,
      int pageNumber,
      int pageSize,
      CancellationToken cancellationToken = default)
    {
      RequestedStatus = status;
      var filtered = values.Where(tenant => !status.HasValue || tenant.Status == status.Value).ToArray();
      return Task.FromResult(new PagedResult<TenantDto>(filtered, pageNumber, pageSize, filtered.Length));
    }
  }

  private sealed class FakeEligibilityReadService : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));
    }

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default) => GetEligibilityAsync(tenantId, cancellationToken);
  }

  private sealed class TestCurrentUser(string? userId, IReadOnlyCollection<string>? roles = null) : ICurrentUser
  {
    public string? UserId { get; } = userId;
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles { get; } = roles ?? [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }
}
