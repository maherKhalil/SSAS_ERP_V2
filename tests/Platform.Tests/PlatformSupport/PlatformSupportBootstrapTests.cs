using System.Reflection;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Identity;
using SSAS.Platform.Infrastructure.PlatformSupport;

namespace SSAS.Platform.Tests.PlatformSupport;

// Phase 3B platform-support bootstrap (ADR-016 / DEC-TEN-0019/0021): the authority-administration
// permission, fail-closed options validation, and the deterministic genesis/recovery orchestrator.
public sealed class PlatformSupportBootstrapTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

  private static readonly PlatformPermissionCatalog Catalog = new();

  // ---- Permission (ADR-016 / DEC-TEN-0021) ----

  [Fact]
  public void Administer_platform_support_is_a_platform_support_scoped_catalog_permission()
  {
    Assert.True(Catalog.TryGet(PlatformPermissionNames.AdministerPlatformSupport, out var definition));
    Assert.Equal("Platform.Support.Administer", PlatformPermissionNames.AdministerPlatformSupport);
    Assert.Equal(SSAS.Platform.Domain.Enums.PermissionScope.PlatformSupport, definition.Scope);
  }

  [Fact]
  public void Administer_platform_support_can_be_granted_to_a_principal()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;
    Assert.True(Catalog.TryGet(PlatformPermissionNames.AdministerPlatformSupport, out var definition));

    Assert.True(principal.GrantPermission(definition, "actor", Now).IsSuccess);
    Assert.Contains(principal.ActivePermissions, permission => permission.Value == PlatformPermissionNames.AdministerPlatformSupport);
  }

  // ---- Options validation (ADR-016 / DEC-TEN-0021) ----

  private static ValidateOptionsResult Validate(PlatformSupportBootstrapOptions options) =>
    new PlatformSupportBootstrapOptionsValidator(Catalog).Validate(null, options);

  [Fact]
  public void Default_options_are_valid_and_inert()
  {
    // Unconfigured bootstrap (no subjects, default Administer-only grant set) must pass startup validation.
    var result = Validate(new PlatformSupportBootstrapOptions());

    Assert.True(result.Succeeded);
  }

  [Fact]
  public void A_well_formed_multi_subject_configuration_is_valid()
  {
    var result = Validate(new PlatformSupportBootstrapOptions
    {
      Subjects = ["local:alice", "local:bob"],
      InitialPermissions = [PlatformPermissionNames.AdministerPlatformSupport, PlatformPermissionNames.ViewTenants]
    });

    Assert.True(result.Succeeded);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData(" untrimmed")]
  [InlineData("untrimmed ")]
  public void Malformed_subjects_fail_validation(string subject)
  {
    var result = Validate(new PlatformSupportBootstrapOptions { Subjects = [subject] });

    Assert.False(result.Succeeded);
  }

  [Fact]
  public void An_over_length_subject_fails_validation()
  {
    var result = Validate(new PlatformSupportBootstrapOptions { Subjects = [new string('a', 257)] });

    Assert.False(result.Succeeded);
  }

  [Fact]
  public void Duplicate_subjects_fail_validation()
  {
    var result = Validate(new PlatformSupportBootstrapOptions { Subjects = ["local:alice", "local:alice"] });

    Assert.False(result.Succeeded);
  }

  [Fact]
  public void An_empty_initial_permission_set_fails_validation()
  {
    var result = Validate(new PlatformSupportBootstrapOptions { InitialPermissions = [] });

    Assert.False(result.Succeeded);
  }

  [Fact]
  public void A_grant_set_missing_administer_fails_validation()
  {
    var result = Validate(new PlatformSupportBootstrapOptions { InitialPermissions = [PlatformPermissionNames.ViewTenants] });

    Assert.False(result.Succeeded);
  }

  [Fact]
  public void A_tenant_scoped_initial_permission_fails_validation()
  {
    var result = Validate(new PlatformSupportBootstrapOptions
    {
      InitialPermissions = [PlatformPermissionNames.AdministerPlatformSupport, PlatformPermissionNames.ViewCompanies]
    });

    Assert.False(result.Succeeded);
  }

  [Fact]
  public void An_unknown_initial_permission_fails_validation()
  {
    var result = Validate(new PlatformSupportBootstrapOptions
    {
      InitialPermissions = [PlatformPermissionNames.AdministerPlatformSupport, "Platform.Unknown.Thing"]
    });

    Assert.False(result.Succeeded);
  }

  [Fact]
  public void Duplicate_initial_permissions_fail_validation()
  {
    var result = Validate(new PlatformSupportBootstrapOptions
    {
      InitialPermissions = [PlatformPermissionNames.AdministerPlatformSupport, PlatformPermissionNames.AdministerPlatformSupport]
    });

    Assert.False(result.Succeeded);
  }

  // ---- Orchestrator (ADR-016 / DEC-TEN-0019/0020/0021) ----

  [Fact]
  public async Task No_configured_subjects_short_circuits_without_any_persistence_access()
  {
    var authority = new FakeAuthorityState();
    var identities = new FakeIdentityRepository();
    var service = Build(new PlatformSupportBootstrapOptions(), authority, identities, new FakeAccountRepository(), new FakePrincipalRepository(), new FakeUnitOfWork());

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.NoCandidatesConfigured, outcome);
    Assert.Equal(0, authority.CallCount);
    Assert.Equal(0, identities.CallCount);
  }

  [Fact]
  public async Task Existing_usable_authority_makes_bootstrap_inert()
  {
    var authority = new FakeAuthorityState(true);
    var principals = new FakePrincipalRepository();
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice"] },
      authority, EligibleWorld("local:alice"), EligibleAccounts("local:alice"), principals, new FakeUnitOfWork());

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.AuthorityAlreadyUsable, outcome);
    Assert.Null(principals.Added);
  }

  [Fact]
  public async Task Selection_is_deterministic_ordinal_first_eligible_regardless_of_configuration_order()
  {
    // Both eligible; configured in reverse order. The ordinal-least subject (local:alice) must be chosen.
    var principals = new FakePrincipalRepository();
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:bob", "local:alice"] },
      new FakeAuthorityState(false), EligibleWorld("local:alice", "local:bob"), EligibleAccounts("local:alice", "local:bob"), principals, new FakeUnitOfWork());

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, outcome);
    Assert.NotNull(principals.Added);
    Assert.Equal(IdFor("local:alice"), principals.Added!.IdentityId);
    Assert.All(principals.Added.PermissionAssignments, assignment => Assert.Equal("platform-bootstrap:local:alice", assignment.AssignedBy));
  }

  [Fact]
  public async Task A_missing_first_candidate_is_skipped_for_the_next_eligible_one()
  {
    var identities = new FakeIdentityRepository();
    identities.Add("local:bob", IdFor("local:bob")); // local:alice is deliberately absent.
    var principals = new FakePrincipalRepository();
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice", "local:bob"] },
      new FakeAuthorityState(false), identities, EligibleAccounts("local:bob"), principals, new FakeUnitOfWork());

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, outcome);
    Assert.Equal(IdFor("local:bob"), principals.Added!.IdentityId);
  }

  [Fact]
  public async Task An_ineligible_account_and_an_already_owning_identity_are_both_skipped()
  {
    // local:alice's account is not authentication-eligible; local:bob already owns a principal;
    // only local:carol can seed authority.
    var accounts = new FakeAccountRepository();
    accounts.AddIneligible("local:alice", IdFor("local:alice"));
    accounts.AddEligible("local:bob", IdFor("local:bob"));
    accounts.AddEligible("local:carol", IdFor("local:carol"));
    var principals = new FakePrincipalRepository();
    principals.MarkExisting(IdFor("local:bob"));
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice", "local:bob", "local:carol"] },
      new FakeAuthorityState(false), EligibleWorld("local:alice", "local:bob", "local:carol"), accounts, principals, new FakeUnitOfWork());

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, outcome);
    Assert.Equal(IdFor("local:carol"), principals.Added!.IdentityId);
  }

  [Fact]
  public async Task No_eligible_candidate_fails_closed_without_establishing_a_principal()
  {
    // Only a Disabled/existing principal case: the single configured identity already owns a principal,
    // so recovery is new-principal-only and finds nothing eligible.
    var principals = new FakePrincipalRepository();
    principals.MarkExisting(IdFor("local:alice"));
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice"] },
      new FakeAuthorityState(false), EligibleWorld("local:alice"), EligibleAccounts("local:alice"), principals, new FakeUnitOfWork());

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.NoEligibleCandidate, outcome);
    Assert.Null(principals.Added);
  }

  [Fact]
  public async Task Genesis_grants_the_full_configured_set_which_always_includes_administer()
  {
    var principals = new FakePrincipalRepository();
    var unitOfWork = new FakeUnitOfWork();
    var service = Build(
      new PlatformSupportBootstrapOptions
      {
        Subjects = ["local:alice"],
        InitialPermissions = [PlatformPermissionNames.AdministerPlatformSupport, PlatformPermissionNames.ViewTenants]
      },
      new FakeAuthorityState(false), EligibleWorld("local:alice"), EligibleAccounts("local:alice"), principals, unitOfWork);

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, outcome);
    Assert.Equal(1, unitOfWork.SaveCount);
    var granted = principals.Added!.ActivePermissions.Select(permission => permission.Value).ToArray();
    Assert.Contains(PlatformPermissionNames.AdministerPlatformSupport, granted);
    Assert.Contains(PlatformPermissionNames.ViewTenants, granted);
    Assert.Equal(2, granted.Length);
  }

  [Fact]
  public async Task A_write_race_that_loses_the_unique_constraint_reconverges_on_the_winner()
  {
    // Pre-check sees no authority; the atomic insert loses the IdentityId uniqueness race; the live
    // re-read then observes the winner's authority, so this host converges rather than double-genesis.
    var authority = new FakeAuthorityState(false, true);
    var principals = new FakePrincipalRepository();
    var unitOfWork = new FakeUnitOfWork(IdentityAccessErrors.UniqueConstraintViolation);
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice"] },
      authority, EligibleWorld("local:alice"), EligibleAccounts("local:alice"), principals, unitOfWork);

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.AuthorityAlreadyUsable, outcome);
    Assert.Equal(2, authority.CallCount); // pre-check + post-failure re-read
  }

  [Fact]
  public async Task A_write_race_that_loses_but_still_sees_no_authority_fails_closed()
  {
    var authority = new FakeAuthorityState(false, false);
    var unitOfWork = new FakeUnitOfWork(IdentityAccessErrors.UniqueConstraintViolation);
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice"] },
      authority, EligibleWorld("local:alice"), EligibleAccounts("local:alice"), new FakePrincipalRepository(), unitOfWork);

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.NoEligibleCandidate, outcome);
  }

  // ---- Fakes and builders ----

  private static PlatformSupportBootstrapService Build(
    PlatformSupportBootstrapOptions options,
    FakeAuthorityState authority,
    FakeIdentityRepository identities,
    FakeAccountRepository accounts,
    FakePrincipalRepository principals,
    FakeUnitOfWork unitOfWork) =>
    new(Options.Create(options), identities, accounts, principals, authority, Catalog, unitOfWork, new StubClock());

  private static long IdFor(string subject) => Math.Abs((long)subject.GetHashCode(StringComparison.Ordinal)) + 1;

  private static FakeIdentityRepository EligibleWorld(params string[] subjects)
  {
    var repository = new FakeIdentityRepository();
    foreach (var subject in subjects)
    {
      repository.Add(subject, IdFor(subject));
    }

    return repository;
  }

  private static FakeAccountRepository EligibleAccounts(params string[] subjects)
  {
    var repository = new FakeAccountRepository();
    foreach (var subject in subjects)
    {
      repository.AddEligible(subject, IdFor(subject));
    }

    return repository;
  }

  private static Identity IdentityWith(string subject, long id)
  {
    var identity = Identity.Create(AuthenticationSubject.Create(subject).Value);
    SetId(identity, id);
    return identity;
  }

  private static AuthenticationAccount EligibleAccount(string subject, long identityId)
  {
    var account = AuthenticationAccount.CreatePending(identityId, LoginEmail.Create($"{Sanitize(subject)}@example.com").Value);
    Assert.True(account.CompleteInitialSetup("hash", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(account.IsAuthenticationEligible);
    return account;
  }

  private static AuthenticationAccount IneligibleAccount(long identityId, string subject)
  {
    // PendingSetup: no password and unverified email, so not authentication-eligible.
    var account = AuthenticationAccount.CreatePending(identityId, LoginEmail.Create($"{Sanitize(subject)}@example.com").Value);
    Assert.False(account.IsAuthenticationEligible);
    return account;
  }

  private static string Sanitize(string subject) => subject.Replace(":", "-", StringComparison.Ordinal);

  private static void SetId(object entity, long id)
  {
    var field = typeof(Entity<long>).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(field);
    field!.SetValue(entity, id);
  }

  private sealed class FakeAuthorityState(params bool[] results) : IPlatformSupportAuthorityStateReadService
  {
    private readonly bool[] results = results.Length == 0 ? [false] : results;

    public int CallCount { get; private set; }

    public Task<bool> HasUsablePlatformAuthorityAsync(CancellationToken cancellationToken = default)
    {
      var index = Math.Min(CallCount, results.Length - 1);
      CallCount++;
      return Task.FromResult(results[index]);
    }
  }

  private sealed class FakeIdentityRepository : IIdentityRepository
  {
    private readonly Dictionary<string, Identity> bySubject = new(StringComparer.Ordinal);

    public int CallCount { get; private set; }

    public void Add(string subject, long id) => bySubject[subject] = IdentityWith(subject, id);

    public Task<Identity?> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default)
    {
      CallCount++;
      return Task.FromResult(bySubject.GetValueOrDefault(subject));
    }

    public Task<Identity?> GetByIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<bool> SubjectExistsAsync(string subject, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task AddAsync(Identity identity, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class FakeAccountRepository : IAuthenticationAccountRepository
  {
    private readonly Dictionary<long, AuthenticationAccount> byIdentityId = [];

    public void AddEligible(string subject, long identityId) => byIdentityId[identityId] = EligibleAccount(subject, identityId);

    public void AddIneligible(string subject, long identityId) => byIdentityId[identityId] = IneligibleAccount(identityId, subject);

    public Task<AuthenticationAccount?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(byIdentityId.GetValueOrDefault(identityId));

    public Task<AuthenticationAccount?> GetByIdAsync(long authenticationAccountId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<AuthenticationAccount?> GetByIdForUpdateAsync(long authenticationAccountId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<AuthenticationAccount?> GetByIdentityIdForUpdateAsync(long identityId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<AuthenticationAccount?> GetByNormalizedLoginEmailAsync(string normalizedLoginEmail, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task ReloadAsync(AuthenticationAccount account, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task AddAsync(AuthenticationAccount account, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class FakePrincipalRepository : IPlatformSupportPrincipalRepository
  {
    private readonly HashSet<long> existingIdentityIds = [];

    public PlatformSupportPrincipal? Added { get; private set; }

    public void MarkExisting(long identityId) => existingIdentityIds.Add(identityId);

    public Task<bool> ExistsForIdentityAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(existingIdentityIds.Contains(identityId));

    public Task AddAsync(PlatformSupportPrincipal principal, CancellationToken cancellationToken = default)
    {
      Added = principal;
      return Task.CompletedTask;
    }

    public Task<PlatformSupportPrincipal?> GetByIdAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<PlatformSupportPrincipal?> GetByIdForUpdateAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<PlatformSupportPrincipal?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<PlatformSupportPrincipal?> GetByIdentityIdForUpdateAsync(long identityId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class FakeUnitOfWork(Error? failWith = null) : IPlatformUnitOfWork
  {
    public int SaveCount { get; private set; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveCount++;
      return Task.FromResult(failWith is null ? Result.Success(1) : Result.Failure<int>(failWith));
    }

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class StubClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }
}
