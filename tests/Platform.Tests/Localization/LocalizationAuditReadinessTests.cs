using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Generated;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Localization;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Localization;

namespace SSAS.Platform.Tests.Localization;

public sealed class LocalizationAuditReadinessTests
{
  private static readonly Guid TenantId = Guid.Parse("4b878586-566a-4b86-b3c5-c58d3d3ad962");

  // ⚠ CITES THE *"NO SQL STATE CHANGE"* CLAUSE OF `AC-LOC-0064`, ACROSS ALL FOUR MUTATION HANDLERS. Every
  // repository call count is 0, `SaveChanges` is 0, `Commit` is 0 and `Rollback` is 1 — so the transaction
  // is not merely uncommitted but actively unwound. `LocalizationAuditReadinessApiTests` carries the HTTP
  // half (503 and the code); this is the handler half, and the four rows are the four handlers.
  //
  // ⚠⚠ THE *"Production"* QUALIFIER IS NOT EXERCISED HERE AND MUST NOT BE READ INTO THIS. The criterion
  // says *"an otherwise authorized PRODUCTION localization mutation"*, and the environment logic lives
  // ENTIRELY inside the real `LocalizationManagementAuditReadiness`, which this fixture replaces with a stub
  // that returns whatever it is given. **The real class returns `Unavailable` unless an explicit
  // development/test bypass is configured — so the environment condition is a separate subject with a
  // separate test, and a dropped mode qualifier here would read as though this file covered it.**
  //
  // ⚠⚠⚠ AND THREE SIDE-EFFECT KINDS THE SENTENCE NAMES ARE NOT ASSERTED: *Domain event*, *cache eviction*
  // and *submitted-text logging*. Nothing in this fixture can observe them — there is no event collector,
  // no cache and no logger. **The clause covered is one of four in that list**, and the count is stated
  // because *no side effects* is exactly the phrase a reader compresses into *all of them*.
  [Theory]
  [InlineData("create")]
  [InlineData("update")]
  [InlineData("undo")]
  [InlineData("restore")]
  [Trait("Criterion", "AC-LOC-0064")]
  public async Task Direct_mutation_handler_fails_without_side_effects_when_audit_is_not_ready(string operation)
  {
    var fixture = new Fixture(LocalizationManagementAuditReadinessResult.Unavailable);

    var result = await fixture.InvokeAsync(operation);

    Assert.Equal(LocalizationManagementErrors.AuditReadinessUnavailable, result.Error);
    Assert.Equal(1, fixture.Eligibility.LockedCalls);
    Assert.Equal(0, fixture.Eligibility.UnlockedCalls);
    Assert.Equal(1, fixture.Readiness.Calls);
    Assert.Equal(0, fixture.Settings.Calls);
    Assert.Equal(0, fixture.Overrides.Calls);
    Assert.Equal(0, fixture.UnitOfWork.SaveCalls);
    Assert.Equal(0, fixture.UnitOfWork.Transaction.CommitCalls);
    Assert.Equal(1, fixture.UnitOfWork.Transaction.RollbackCalls);
  }

  // ⚠ CITES THE *"no INTERNAL-CAUSE DISCLOSURE"* CLAUSE OF `AC-LOC-0064`, and it is the one clause in that
  // sentence with a *positive* observable rather than a count of zero: the provider throws carrying
  // `provider-secret-reason`, and that string is absent from the error the caller receives.
  //
  // ⚠⚠ IT ALSO CARRIES *FAILS CLOSED*, WHICH IS THE HARDER HALF AND IS EASY TO LOSE. The stub is
  // constructed `Ready` and then given an exception — **so the readiness result that WOULD have permitted
  // the mutation is present, and the throw is what stops it.** A handler that swallowed the exception and
  // proceeded on the `Ready` value would satisfy the disclosure assertion and violate the criterion; the
  // `Settings.Calls`/`SaveCalls` zeroes are what exclude it.
  [Fact]
  [Trait("Criterion", "AC-LOC-0064")]
  public async Task Operational_readiness_exception_fails_closed_without_disclosing_the_reason()
  {
    var fixture = new Fixture(LocalizationManagementAuditReadinessResult.Ready);
    fixture.Readiness.Exception = new InvalidOperationException("provider-secret-reason");

    var result = await fixture.InvokeAsync("create");

    Assert.Equal("localization.audit_readiness_unavailable", result.Error.Code);
    Assert.DoesNotContain("provider-secret-reason", result.Error.Message, StringComparison.Ordinal);
    Assert.Equal(0, fixture.Settings.Calls);
    Assert.Equal(0, fixture.UnitOfWork.SaveCalls);
  }

  // ⚠ CITES THE *"an OTHERWISE AUTHORIZED … mutation"* QUALIFIER OF `AC-LOC-0064`. A suspended tenant is
  // refused with `TenantIneligible` and `Readiness.Calls` is **0** — the audit gate is never consulted, so
  // the criterion's subject really is a mutation that has already passed authorization. **Without this, the
  // sentence would be satisfiable by a handler that ran the audit check first and reported audit failure for
  // an unauthorized caller — leaking that audit is unavailable to someone with no right to know.**
  //
  // ⚠⚠ AND IT PINS *WHICH* ELIGIBILITY READ THE HANDLERS USE, WHICH IS A SAFETY PROPERTY NO CRITERION
  // COVERS. `LockedCalls` is 1 and `UnlockedCalls` is 0 — the handlers take `GetEligibilityForUpdateAsync`,
  // the locked read. That is the positive counterpart of the guard in `RequestTenantEligibilityTests`, whose
  // `GetEligibilityForUpdateAsync` THROWS *"Request eligibility must never replace the locked mutation
  // check."* **One test proves the request-scoped cache refuses to serve the locked path; this one proves
  // the mutation handlers actually ask for it. Neither alone establishes the pair.**
  [Fact]
  [Trait("Criterion", "AC-LOC-0064")]
  public async Task Locked_live_tenant_denial_precedes_the_audit_gate()
  {
    var fixture = new Fixture(LocalizationManagementAuditReadinessResult.Unavailable, TenantStatus.Suspended);

    var result = await fixture.InvokeAsync("create");

    Assert.Equal(SSAS.Platform.Domain.Localization.LocalizationErrors.TenantIneligible, result.Error);
    Assert.Equal(1, fixture.Eligibility.LockedCalls);
    Assert.Equal(0, fixture.Readiness.Calls);
  }

  // ⚠⚠⚠ THIS IS THE ANTI-VACUITY CONTROL FOR EVERY ZERO ABOVE, AND IT IS THE LOAD-BEARING TEST IN THE FILE.
  // *No SQL state change when audit is unavailable* is a property ONLY IF THE MUTATION OTHERWISE HAPPENS.
  // Were the handlers broken — a repository never called, a transaction never opened — every count above
  // would still be 0 and every assertion would still pass. **This test is what makes those zeroes mean
  // *refused* rather than *inert*:** `Overrides.Added` is non-null, `SaveCalls` is 1, `CommitCalls` is 1.
  //
  // ⚠ THE SAME SHAPE AS THE RAW-TEMPLATE PAIR AND THE 422/400 PAIR — a positive over a contrasting
  // producer, carrying a property that no single assertion can hold. Here it is not a citation but a
  // control, and it is cited to no criterion deliberately: **the criterion is about what does NOT happen,
  // and this test exists to give that a denominator.**
  [Fact]
  public async Task Ready_create_continues_through_domain_save_and_commit()
  {
    var fixture = new Fixture(LocalizationManagementAuditReadinessResult.Ready);

    var result = await fixture.InvokeAsync("create");

    Assert.True(result.IsSuccess);
    Assert.NotNull(fixture.Overrides.Added);
    Assert.Single(fixture.Overrides.Added!.Versions);
    Assert.Equal(2, result.Value.TenantLocalizationVersion);
    Assert.Equal(1, fixture.UnitOfWork.SaveCalls);
    Assert.Equal(1, fixture.UnitOfWork.Transaction.CommitCalls);
  }

  private sealed class Fixture
  {
    private readonly CurrentTenant currentTenant = new(TenantId);
    private readonly CurrentUser currentUser = new();
    private readonly Clock clock = new();

    public Fixture(LocalizationManagementAuditReadinessResult readiness, TenantStatus status = TenantStatus.Active)
    {
      Readiness = new AuditReadiness(readiness);
      Eligibility = new Eligibility(status);
    }

    public SettingsRepository Settings { get; } = new();
    public OverrideRepository Overrides { get; } = new();
    public Eligibility Eligibility { get; }
    public AuditReadiness Readiness { get; }
    public UnitOfWork UnitOfWork { get; } = new();

    public Task<Result<LocalizationMutationResult>> InvokeAsync(string operation) => operation switch
    {
      "create" => new CreateTenantLocalizationOverrideCommandHandler(
        Settings, Overrides, Eligibility, Readiness, UnitOfWork, GeneratedLocalizationCatalog.Instance,
        currentTenant, currentUser, clock).HandleAsync(new("platform.common.actions.save", "en", "Store")),
      "update" => new UpdateTenantLocalizationOverrideCommandHandler(
        Settings, Overrides, Eligibility, Readiness, UnitOfWork, GeneratedLocalizationCatalog.Instance,
        currentTenant, currentUser, clock).HandleAsync(new("platform.common.actions.save", "en", "Store", [1])),
      "undo" => new UndoTenantLocalizationOverrideCommandHandler(
        Settings, Overrides, Eligibility, Readiness, UnitOfWork, GeneratedLocalizationCatalog.Instance,
        currentTenant, currentUser, clock).HandleAsync(new("platform.common.actions.save", "en", 1, [1])),
      "restore" => new RestoreTenantLocalizationDefaultCommandHandler(
        Settings, Overrides, Eligibility, Readiness, UnitOfWork, GeneratedLocalizationCatalog.Instance,
        currentTenant, currentUser, clock).HandleAsync(new("platform.common.actions.save", "en", [1])),
      _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };
  }

  private sealed class AuditReadiness(LocalizationManagementAuditReadinessResult result)
    : ILocalizationManagementAuditReadiness
  {
    public int Calls { get; private set; }
    public Exception? Exception { get; set; }

    public Task<LocalizationManagementAuditReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
    {
      Calls++;
      return Exception is null
        ? Task.FromResult(result)
        : Task.FromException<LocalizationManagementAuditReadinessResult>(Exception);
    }
  }

  private sealed class Eligibility(TenantStatus status) : ITenantAuthenticationEligibilityReadService
  {
    public int UnlockedCalls { get; private set; }
    public int LockedCalls { get; private set; }

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default)
    {
      UnlockedCalls++;
      return Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, status));
    }

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default)
    {
      LockedCalls++;
      return Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, status));
    }
  }

  private sealed class SettingsRepository : ITenantLocalizationSettingsRepository
  {
    public int Calls { get; private set; }
    private readonly TenantLocalizationSettings settings = TenantLocalizationSettings.Create(TenantId, LocalizationCulture.English);

    public Task<TenantLocalizationSettings?> GetForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
      Calls++;
      return Task.FromResult<TenantLocalizationSettings?>(settings);
    }

    public Task<TenantLocalizationSettings> GetOrCreateForUpdateAsync(
      Guid tenantId,
      LocalizationCulture defaultCulture,
      CancellationToken cancellationToken = default)
    {
      Calls++;
      return Task.FromResult(settings);
    }
  }

  private sealed class OverrideRepository : ITenantLocalizationOverrideRepository
  {
    public int Calls { get; private set; }
    public TenantLocalizationOverride? Added { get; private set; }

    public Task<TenantLocalizationOverride?> GetForUpdateAsync(
      Guid tenantId,
      ResourceKey resourceKey,
      LocalizationCulture culture,
      CancellationToken cancellationToken = default)
    {
      Calls++;
      return Task.FromResult<TenantLocalizationOverride?>(null);
    }

    public Task<LocalizationVersionSnapshot?> GetVersionSnapshotAsync(
      Guid overrideId,
      TenantOverrideVersion versionNumber,
      CancellationToken cancellationToken = default)
    {
      Calls++;
      return Task.FromResult<LocalizationVersionSnapshot?>(null);
    }

    public Task AddAsync(TenantLocalizationOverride localizationOverride, CancellationToken cancellationToken = default)
    {
      Calls++;
      Added = localizationOverride;
      return Task.CompletedTask;
    }
  }

  private sealed class UnitOfWork : IPlatformUnitOfWork
  {
    public int SaveCalls { get; private set; }
    public Transaction Transaction { get; } = new();

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveCalls++;
      return Task.FromResult(Result.Success(1));
    }

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<ITransaction>(Transaction);
  }

  private sealed class Transaction : ITransaction
  {
    private bool completed;
    public int CommitCalls { get; private set; }
    public int RollbackCalls { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
      CommitCalls++;
      completed = true;
      return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
      RollbackCalls++;
      completed = true;
      return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
      if (!completed)
      {
        await RollbackAsync();
      }
    }
  }

  private sealed class CurrentTenant(Guid tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class CurrentUser : ICurrentUser
  {
    public string? UserId => "audit-test-actor";
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class Clock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow { get; } = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
  }
}
