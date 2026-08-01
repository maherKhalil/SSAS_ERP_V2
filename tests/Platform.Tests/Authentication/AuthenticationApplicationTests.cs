using System.Reflection;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Identity;

namespace SSAS.Platform.Tests.Authentication;

public sealed class AuthenticationApplicationTests
{
  private static readonly Guid TenantId = Guid.Parse("4c0aef16-87ee-4c0f-9c49-e40948d6f622");
  private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("BusinessRequirement", "BR-AUTH-0006")]
  [Trait("BusinessRule", "BRULE-AUTH-0013")]
  [Trait("Decision", "DEC-AUTH-0025")]
  [Trait("Requirement", "FR-AUTH-0101")]
  [Trait("Acceptance", "AC-AUTH-0015")]
  [Trait("Scenario", "TS-AUTH-0001")]
  public async Task New_account_invitation_creates_pending_global_account_and_membership_then_completes_setup()
  {
    var scope = new TestScope();
    var issue = scope.CreateInvitationHandler();

    var issued = await issue.HandleAsync(new IssueTenantUserInvitationCommand(" New.User@example.com ", "New User"));

    Assert.True(issued.IsSuccess);
    var identity = Assert.Single(scope.Identities.Values);
    var account = Assert.Single(scope.Accounts.Values);
    var membership = Assert.Single(scope.TenantUsers.Values);
    Assert.StartsWith("local:", identity.Subject.Value, StringComparison.Ordinal);
    Assert.Equal("New.User@example.com", account.LoginEmail.Value);
    Assert.Equal("NEW.USER@EXAMPLE.COM", account.NormalizedLoginEmail);
    Assert.Equal(AuthenticationAccountStatus.PendingSetup, account.Status);
    Assert.Equal(TenantUserStatus.Pending, membership.Status);
    Assert.Empty(membership.RoleAssignments);

    var rawToken = issued.Value.RevealOnce();
    Assert.True(rawToken.IsSuccess);
    var completion = await scope.CreateInvitationCompletionHandler().HandleAsync(
      new CompleteInvitationCommand(rawToken.Value, "Long password with spaces 123"));

    Assert.True(completion.IsSuccess);
    Assert.Equal(AuthenticationAccountStatus.Active, account.Status);
    Assert.Equal(TenantUserStatus.Active, membership.Status);
    Assert.True(account.EmailVerifiedUtc.HasValue);
    Assert.Equal(1, account.SecurityVersion);
    Assert.NotNull(Assert.Single(scope.ActionTokens.Values).ConsumedUtc);
    Assert.Equal(1, scope.PasswordHasher.HashCount);
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0026")]
  [Trait("Requirement", "FR-AUTH-0101")]
  [Trait("Scenario", "TS-AUTH-0002")]
  public async Task Existing_verified_account_invitation_activates_only_new_membership_without_password_or_security_change()
  {
    var scope = new TestScope();
    var identity = scope.AddIdentity("local:72e4872ef9c947e9b4339c27e9478869");
    var account = scope.AddActiveAccount(identity.Id, "existing@example.com");
    var version = account.SecurityVersion;
    var passwordChanged = account.PasswordChangedUtc;

    var issued = await scope.CreateInvitationHandler().HandleAsync(
      new IssueTenantUserInvitationCommand("existing@example.com", "Tenant Profile"));
    var rawToken = issued.Value.RevealOnce().Value;
    var result = await scope.CreateInvitationCompletionHandler().HandleAsync(
      new CompleteInvitationCommand(rawToken, null));

    Assert.True(result.IsSuccess);
    Assert.Equal(TenantUserStatus.Active, Assert.Single(scope.TenantUsers.Values).Status);
    Assert.Equal(version, account.SecurityVersion);
    Assert.Equal(passwordChanged, account.PasswordChangedUtc);
    Assert.Equal(0, scope.PasswordHasher.HashCount);
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0026")]
  [Trait("Requirement", "FR-AUTH-0101")]
  [Trait("Scenario", "TS-AUTH-0003")]
  public async Task Existing_pending_setup_account_reuses_identity_and_requires_initial_password()
  {
    var scope = new TestScope();
    var identity = scope.AddIdentity("local:bba5fb5b8988412bbffde6f44690c466");
    var account = AuthenticationAccount.CreatePending(identity.Id, LoginEmail.Create("pending@example.com").Value);
    await scope.Accounts.AddAsync(account);

    var issued = await scope.CreateInvitationHandler().HandleAsync(
      new IssueTenantUserInvitationCommand("pending@example.com", "Pending Account"));
    var rawToken = issued.Value.RevealOnce().Value;
    var missingPassword = await scope.CreateInvitationCompletionHandler().HandleAsync(
      new CompleteInvitationCommand(rawToken, null));
    var completed = await scope.CreateInvitationCompletionHandler().HandleAsync(
      new CompleteInvitationCommand(rawToken, "Long initial password 123"));

    Assert.Equal("AuthenticationAccount.PasswordRequired", missingPassword.Error.Code);
    Assert.True(completed.IsSuccess);
    Assert.Single(scope.Identities.Values);
    Assert.Same(identity, scope.Identities.Values.Single());
    Assert.Equal(AuthenticationAccountStatus.Active, account.Status);
    Assert.Equal(TenantUserStatus.Active, Assert.Single(scope.TenantUsers.Values).Status);
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0027")]
  [Trait("Requirement", "FR-AUTH-0101")]
  [Trait("Scenario", "TS-AUTH-0005")]
  public async Task Reissued_invitation_reuses_identity_account_and_membership_and_retains_revoked_history()
  {
    var scope = new TestScope();
    var handler = scope.CreateInvitationHandler();

    var first = await handler.HandleAsync(new IssueTenantUserInvitationCommand("retry@example.com", "Retry User"));
    scope.Clock.UtcNow = Now.AddMinutes(1);
    var second = await handler.HandleAsync(new IssueTenantUserInvitationCommand("RETRY@example.com", "Retry User"));

    Assert.True(first.IsSuccess);
    Assert.True(second.IsSuccess);
    Assert.Single(scope.Identities.Values);
    Assert.Single(scope.Accounts.Values);
    Assert.Single(scope.TenantUsers.Values);
    Assert.Empty(scope.TenantUsers.Values.Single().RoleAssignments);
    Assert.Equal(2, scope.ActionTokens.Values.Count);
    Assert.NotNull(scope.ActionTokens.Values[0].RevokedUtc);
    Assert.Null(scope.ActionTokens.Values[0].ConsumedUtc);
    Assert.Null(scope.ActionTokens.Values[1].RevokedUtc);
    Assert.Null(scope.ActionTokens.Values[1].ConsumedUtc);
  }

  [Theory]
  [InlineData(TenantUserStatus.Active, "Invitation.ActiveMembership")]
  [InlineData(TenantUserStatus.Deactivated, "Invitation.DeactivatedMembership")]
  [Trait("Requirement", "FR-AUTH-0101")]
  [Trait("Scenario", "TS-AUTH-0004")]
  public async Task Invitation_rejects_active_and_deactivated_membership_misuse(
    TenantUserStatus status,
    string expectedError)
  {
    var scope = new TestScope();
    var identity = scope.AddIdentity("local:0e41fa6233934d14a140646d579104b6");
    scope.AddActiveAccount(identity.Id, "member@example.com");
    var membership = scope.AddActiveTenantUser(identity.Id, "member@example.com");
    if (status == TenantUserStatus.Deactivated)
    {
      Assert.True(membership.Deactivate(Guid.NewGuid(), Now).IsSuccess);
    }

    var result = await scope.CreateInvitationHandler().HandleAsync(
      new IssueTenantUserInvitationCommand("member@example.com", "Member"));

    Assert.True(result.IsFailure);
    Assert.Equal(expectedError, result.Error.Code);
    Assert.Empty(scope.ActionTokens.Values);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-AUTH-0001")]
  [Trait("Requirement", "FR-AUTH-0102")]
  [Trait("Scenario", "TS-AUTH-0009")]
  [Trait("Scenario", "TS-AUTH-0011")]
  public async Task Successful_rehash_verification_returns_only_identity_and_preserves_security_state()
  {
    var scope = new TestScope();
    var identity = scope.AddIdentity("local:452818a428444301bc9093046514117a");
    var account = scope.AddActiveAccount(identity.Id, "login@example.com");
    var securityVersion = account.SecurityVersion;
    var passwordChangedUtc = account.PasswordChangedUtc;
    scope.PasswordHasher.VerificationOutcome = PasswordVerificationOutcome.SuccessRehashNeeded;

    var result = await scope.CreateCredentialHandler().HandleAsync(
      new VerifyPasswordCredentialsCommand(" login@example.com ", "Long password with spaces 123"));

    Assert.True(result.IsSuccess);
    Assert.Equal(identity.Id, result.Value.VerifiedIdentity.IdentityId);
    Assert.Equal(account.SecurityVersion, result.Value.VerifiedIdentity.SecurityVersion);
    Assert.Equal(securityVersion, account.SecurityVersion);
    Assert.Equal(passwordChangedUtc, account.PasswordChangedUtc);
    Assert.Equal(1, scope.PasswordHasher.HashCount);
  }

  [Fact]
  [Trait("BusinessRequirement", "BR-AUTH-0008")]
  [Trait("BusinessRule", "BRULE-AUTH-0011")]
  [Trait("Requirement", "SEC-AUTH-0204")]
  [Trait("Requirement", "FR-AUTH-0118")]
  [Trait("Acceptance", "AC-AUTH-0001")]
  [Trait("Acceptance", "AC-AUTH-0017")]
  [Trait("Scenario", "TS-AUTH-0010")]
  [Trait("Scenario", "TS-AUTH-0050")]
  public async Task Credential_failures_are_generic_and_five_wrong_passwords_lock_the_account()
  {
    var scope = new TestScope();
    var identity = scope.AddIdentity("local:a29e761d2be14592b90276b74578dacb");
    var account = scope.AddActiveAccount(identity.Id, "locked@example.com");
    scope.PasswordHasher.VerificationOutcome = PasswordVerificationOutcome.Failed;
    var handler = scope.CreateCredentialHandler();

    for (var count = 1; count <= 5; count++)
    {
      var result = await handler.HandleAsync(
        new VerifyPasswordCredentialsCommand("locked@example.com", "incorrect-password"));
      Assert.True(result.IsFailure);
      Assert.Equal("Authentication.Failed", result.Error.Code);
    }

    Assert.Equal(5, account.FailedAttemptCount);
    Assert.Equal(Now.AddMinutes(15), account.LockoutEndUtc);

    scope.PasswordHasher.VerificationOutcome = PasswordVerificationOutcome.Success;
    var locked = await handler.HandleAsync(
      new VerifyPasswordCredentialsCommand("locked@example.com", "correct-password"));
    Assert.True(account.Disable(Guid.NewGuid(), Now).IsSuccess);
    var disabled = await handler.HandleAsync(
      new VerifyPasswordCredentialsCommand("locked@example.com", "correct-password"));
    var unknown = await handler.HandleAsync(
      new VerifyPasswordCredentialsCommand("unknown@example.com", "incorrect-password"));
    Assert.Equal("Authentication.Failed", locked.Error.Code);
    Assert.Equal("Authentication.Failed", disabled.Error.Code);
    Assert.Equal("Authentication.Failed", unknown.Error.Code);
    Assert.Equal(1, scope.PasswordHasher.DummyVerificationCount);
    Assert.Equal(2, scope.PasswordHasher.IneligibleVerificationCount);
  }

  [Fact]
  [Trait("Requirement", "FR-AUTH-0102")]
  [Trait("Acceptance", "AC-AUTH-0016")]
  [Trait("Scenario", "TS-AUTH-0013")]
  public async Task Successful_verification_after_lockout_expiry_clears_failed_attempt_state()
  {
    var scope = new TestScope();
    var identity = scope.AddIdentity("local:5bb6471f76924348859cd7f2e66a413f");
    var account = scope.AddActiveAccount(identity.Id, "expiry@example.com");
    for (var count = 0; count < 5; count++)
    {
      Assert.True(account.RecordFailedAttempt(5, TimeSpan.FromMinutes(15), Guid.NewGuid(), Now).IsSuccess);
    }
    scope.Clock.UtcNow = Now.AddMinutes(15);

    var result = await scope.CreateCredentialHandler().HandleAsync(
      new VerifyPasswordCredentialsCommand("expiry@example.com", "Long correct password 123"));

    Assert.True(result.IsSuccess);
    Assert.Equal(0, account.FailedAttemptCount);
    Assert.Null(account.LockoutEndUtc);
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0031")]
  [Trait("NonFunctional", "NFR-AUTH-0304")]
  [Trait("Requirement", "FR-AUTH-0118")]
  [Trait("Scenario", "TS-AUTH-0014")]
  public async Task Failed_attempt_concurrency_retries_three_times_then_returns_generic_failure()
  {
    var scope = new TestScope();
    var identity = scope.AddIdentity("local:70a8ad3614d94d87a4a73bc21d451737");
    scope.AddActiveAccount(identity.Id, "race@example.com");
    scope.PasswordHasher.VerificationOutcome = PasswordVerificationOutcome.Failed;
    scope.UnitOfWork.SaveResults.Enqueue(Result.Failure<int>(IdentityAccessErrors.ConcurrencyConflict));
    scope.UnitOfWork.SaveResults.Enqueue(Result.Failure<int>(IdentityAccessErrors.ConcurrencyConflict));
    scope.UnitOfWork.SaveResults.Enqueue(Result.Failure<int>(IdentityAccessErrors.ConcurrencyConflict));
    scope.UnitOfWork.SaveResults.Enqueue(Result.Failure<int>(IdentityAccessErrors.ConcurrencyConflict));

    var result = await scope.CreateCredentialHandler().HandleAsync(
      new VerifyPasswordCredentialsCommand("race@example.com", "incorrect-password"));

    Assert.True(result.IsFailure);
    Assert.Equal("Authentication.Failed", result.Error.Code);
    Assert.Equal(4, scope.UnitOfWork.SaveCount);
    Assert.Equal(3, scope.Accounts.ReloadCount);
    Assert.Equal([1, 2, 3], scope.Diagnostics.RetryNumbers);
    Assert.Equal(1, scope.Diagnostics.ExhaustedCount);
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0031")]
  [Trait("Requirement", "FR-AUTH-0118")]
  [Trait("Scenario", "TS-AUTH-0014")]
  public async Task Failed_attempt_concurrency_retry_reapplies_one_logical_attempt_to_reloaded_state()
  {
    var scope = new TestScope();
    var identity = scope.AddIdentity("local:d26d627cdd2149ada00f2b9319934dca");
    var account = scope.AddActiveAccount(identity.Id, "retry-race@example.com");
    scope.PasswordHasher.VerificationOutcome = PasswordVerificationOutcome.Failed;
    scope.UnitOfWork.SaveResults.Enqueue(Result.Failure<int>(IdentityAccessErrors.ConcurrencyConflict));
    scope.Accounts.ReloadAction = reloaded =>
    {
      SetProperty(reloaded, nameof(AuthenticationAccount.FailedAttemptCount), 0);
      SetProperty<DateTimeOffset?>(reloaded, nameof(AuthenticationAccount.LockoutEndUtc), null);
    };

    var result = await scope.CreateCredentialHandler().HandleAsync(
      new VerifyPasswordCredentialsCommand("retry-race@example.com", "incorrect-password"));

    Assert.True(result.IsFailure);
    Assert.Equal("Authentication.Failed", result.Error.Code);
    Assert.Equal(2, scope.UnitOfWork.SaveCount);
    Assert.Equal(1, scope.Accounts.ReloadCount);
    Assert.Equal(1, account.FailedAttemptCount);
    Assert.Single(account.DomainEvents);
  }

  [Fact]
  [Trait("BusinessRequirement", "BR-AUTH-0008")]
  [Trait("BusinessRule", "BRULE-AUTH-0010")]
  [Trait("BusinessRule", "BRULE-AUTH-0011")]
  [Trait("Requirement", "FR-AUTH-0116")]
  [Trait("Requirement", "FR-AUTH-0117")]
  [Trait("Acceptance", "AC-AUTH-0013")]
  [Trait("Scenario", "TS-AUTH-0016")]
  [Trait("Scenario", "TS-AUTH-0017")]
  [Trait("Scenario", "TS-AUTH-0051")]
  public async Task Password_reset_is_non_enumerating_and_completion_advances_security_state_once()
  {
    var scope = new TestScope();
    var unknown = await scope.CreateResetIssuanceHandler().HandleAsync(
      new IssuePasswordResetCommand("unknown@example.com"));
    Assert.True(unknown.IsSuccess);
    Assert.Null(unknown.Value.SensitiveToken);

    var identity = scope.AddIdentity("local:7e41439677f44164b2efc7bf5af09e86");
    var account = scope.AddActiveAccount(identity.Id, "reset@example.com");
    Assert.True(account.RecordFailedAttempt(5, TimeSpan.FromMinutes(15), Guid.NewGuid(), Now).IsSuccess);
    var securityVersion = account.SecurityVersion;
    var issued = await scope.CreateResetIssuanceHandler().HandleAsync(
      new IssuePasswordResetCommand("reset@example.com"));
    var rawToken = issued.Value.SensitiveToken!.RevealOnce().Value;

    var result = await scope.CreateResetCompletionHandler().HandleAsync(
      new CompletePasswordResetCommand(rawToken, "Replacement password 123"));
    var replay = await scope.CreateResetCompletionHandler().HandleAsync(
      new CompletePasswordResetCommand(rawToken, "Another replacement 123"));

    Assert.True(result.IsSuccess);
    Assert.True(replay.IsFailure);
    Assert.Equal(securityVersion + 1, account.SecurityVersion);
    Assert.Equal(0, account.FailedAttemptCount);
    Assert.Null(account.LockoutEndUtc);

    Assert.True(account.Disable(Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
    var disabledIssuance = await scope.CreateResetIssuanceHandler().HandleAsync(
      new IssuePasswordResetCommand("reset@example.com"));
    Assert.True(disabledIssuance.IsSuccess);
    Assert.Null(disabledIssuance.Value.SensitiveToken);
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0027")]
  [Trait("Requirement", "FR-AUTH-0116")]
  [Trait("Scenario", "TS-AUTH-0016")]
  public async Task Reissued_password_reset_revokes_prior_token_and_retains_account_state()
  {
    var scope = new TestScope();
    var identity = scope.AddIdentity("local:b98739e9cc864e308a5b64085c03ca46");
    var account = scope.AddActiveAccount(identity.Id, "reset-retry@example.com");
    var securityVersion = account.SecurityVersion;
    var passwordChangedUtc = account.PasswordChangedUtc;
    var handler = scope.CreateResetIssuanceHandler();

    var first = await handler.HandleAsync(new IssuePasswordResetCommand("reset-retry@example.com"));
    scope.Clock.UtcNow = Now.AddMinutes(1);
    var second = await handler.HandleAsync(new IssuePasswordResetCommand("RESET-RETRY@example.com"));

    Assert.NotNull(first.Value.SensitiveToken);
    Assert.NotNull(second.Value.SensitiveToken);
    Assert.Equal(2, scope.ActionTokens.Values.Count);
    Assert.NotNull(scope.ActionTokens.Values[0].RevokedUtc);
    Assert.Null(scope.ActionTokens.Values[0].ConsumedUtc);
    Assert.Null(scope.ActionTokens.Values[1].RevokedUtc);
    Assert.Null(scope.ActionTokens.Values[1].ConsumedUtc);
    Assert.Equal(securityVersion, account.SecurityVersion);
    Assert.Equal(passwordChangedUtc, account.PasswordChangedUtc);
  }

  [Theory]
  [InlineData("short", CompromisedPasswordCheckOutcome.Safe, "Authentication.InvalidPassword")]
  [InlineData("Long compromised password 123", CompromisedPasswordCheckOutcome.Compromised, "Authentication.CompromisedPassword")]
  [InlineData("Long unavailable password 123", CompromisedPasswordCheckOutcome.Unavailable, "Authentication.PasswordCheckUnavailable")]
  [Trait("Requirement", "SEC-AUTH-0210")]
  [Trait("Scenario", "TS-AUTH-0015")]
  public async Task Password_policy_rejects_short_compromised_and_unavailable_checks(
    string password,
    CompromisedPasswordCheckOutcome checkOutcome,
    string expectedError)
  {
    var checker = new FakeCompromisedPasswordChecker { Outcome = checkOutcome };
    var validator = new PasswordPolicyValidator(new AuthenticationPolicy(), checker);

    var result = await validator.ValidateAsync(password);

    Assert.True(result.IsFailure);
    Assert.Equal(expectedError, result.Error.Code);
  }

  [Fact]
  [Trait("Requirement", "NFR-AUTH-0305")]
  [Trait("Scenario", "TS-AUTH-0015")]
  public async Task Password_policy_accepts_spaces_unicode_and_the_documented_128_character_maximum()
  {
    var validator = new PasswordPolicyValidator(
      new AuthenticationPolicy(),
      new FakeCompromisedPasswordChecker());

    Assert.True((await validator.ValidateAsync("كلمة مرور طويلة مع مسافات")).IsSuccess);
    Assert.True((await validator.ValidateAsync(new string('س', 128))).IsSuccess);
    Assert.True((await validator.ValidateAsync(new string('س', 129))).IsFailure);
  }

  private sealed class TestScope
  {
    public TestScope()
    {
      Identities = new FakeIdentityRepository();
      TenantUsers = new FakeTenantUserRepository();
      Accounts = new FakeAuthenticationAccountRepository();
      ActionTokens = new FakeActionTokenRepository();
    }

    public FakeIdentityRepository Identities { get; }

    public FakeTenantUserRepository TenantUsers { get; }

    public FakeAuthenticationAccountRepository Accounts { get; }

    public FakeActionTokenRepository ActionTokens { get; }

    public FakeAuthenticationSessionRepository Sessions { get; } = new();

    public FakeUnitOfWork UnitOfWork { get; } = new();

    public FakePasswordHasher PasswordHasher { get; } = new();

    public FakeAuthenticationDiagnostics Diagnostics { get; } = new();

    public TestClock Clock { get; } = new(Now);

    public AuthenticationPolicy Policy { get; } = new();

    public ActionTokenService ActionTokenService { get; } = new();

    public PasswordPolicyValidator PasswordPolicyValidator => new(Policy, new FakeCompromisedPasswordChecker());

    public Identity AddIdentity(string subject)
    {
      var identity = Identity.Create(AuthenticationSubject.Create(subject).Value);
      Identities.AddAsync(identity).GetAwaiter().GetResult();
      return identity;
    }

    public AuthenticationAccount AddActiveAccount(long identityId, string email)
    {
      var account = AuthenticationAccount.CreatePending(identityId, LoginEmail.Create(email).Value);
      Accounts.AddAsync(account).GetAwaiter().GetResult();
      Assert.True(account.CompleteInitialSetup("existing-hash", Guid.NewGuid(), Now).IsSuccess);
      account.ClearDomainEvents();
      return account;
    }

    public TenantUser AddActiveTenantUser(long identityId, string email)
    {
      var user = TenantUser.CreateActive(
        identityId,
        TenantId,
        EmailAddress.Create(email).Value,
        UserDisplayName.Create("Existing Member").Value,
        Guid.NewGuid(),
        Now);
      TenantUsers.AddAsync(user).GetAwaiter().GetResult();
      return user;
    }

    public IssueTenantUserInvitationCommandHandler CreateInvitationHandler() => new(
      Identities,
      TenantUsers,
      Accounts,
      ActionTokens,
      UnitOfWork,
      ActionTokenService,
      Policy,
      new TestCurrentTenant(TenantId),
      new TestCurrentUser("tenant-admin"),
      Clock);

    public CompleteInvitationCommandHandler CreateInvitationCompletionHandler() => new(
      Accounts,
      ActionTokens,
      TenantUsers,
      UnitOfWork,
      ActionTokenService,
      PasswordHasher,
      PasswordPolicyValidator,
      Clock);

    public VerifyPasswordCredentialsCommandHandler CreateCredentialHandler() => new(
      Accounts,
      UnitOfWork,
      PasswordHasher,
      Diagnostics,
      Policy,
      Clock);

    public IssuePasswordResetCommandHandler CreateResetIssuanceHandler() => new(
      Accounts,
      ActionTokens,
      UnitOfWork,
      ActionTokenService,
      Policy,
      Clock);

    public CompletePasswordResetCommandHandler CreateResetCompletionHandler() => new(
      Accounts,
      ActionTokens,
      Sessions,
      UnitOfWork,
      ActionTokenService,
      PasswordHasher,
      PasswordPolicyValidator,
      Clock);
  }

  private sealed class FakeIdentityRepository : IIdentityRepository
  {
    public List<Identity> Values { get; } = [];

    public Task<Identity?> GetByIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(item => item.Id == identityId));

    public Task<Identity?> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(item => item.Subject.Value == subject));

    public Task<bool> SubjectExistsAsync(string subject, CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.Any(item => item.Subject.Value == subject));

    public Task AddAsync(Identity identity, CancellationToken cancellationToken = default)
    {
      SetId(identity, Values.Count + 1);
      Values.Add(identity);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeTenantUserRepository : ITenantUserRepository
  {
    public List<TenantUser> Values { get; } = [];

    public Task<TenantUser?> GetByIdAsync(long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(item => item.Id == tenantUserId));

    public Task<TenantUser?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(item => item.TenantId == TenantId && item.IdentityId == identityId));

    public Task<TenantUser?> GetByTrustedInvitationBindingAsync(
      Guid tenantId,
      long tenantUserId,
      CancellationToken cancellationToken = default) => Task.FromResult(
        Values.SingleOrDefault(item => item.TenantId == tenantId && item.Id == tenantUserId));

    public Task<bool> EmailExistsAsync(
      string normalizedEmail,
      long? excludingTenantUserId = null,
      CancellationToken cancellationToken = default) => Task.FromResult(Values.Any(item =>
        item.TenantId == TenantId && item.NormalizedEmail == normalizedEmail && item.Id != excludingTenantUserId));

    public Task<bool> MembershipExistsAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.Any(item => item.TenantId == TenantId && item.IdentityId == identityId));

    public Task<bool> HasActiveAssignmentToRoleAsync(long roleId, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task AddAsync(TenantUser tenantUser, CancellationToken cancellationToken = default)
    {
      SetId(tenantUser, Values.Count + 1);
      Values.Add(tenantUser);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeAuthenticationAccountRepository : IAuthenticationAccountRepository
  {
    public List<AuthenticationAccount> Values { get; } = [];

    public int ReloadCount { get; private set; }

    public Action<AuthenticationAccount>? ReloadAction { get; set; }

    public Task<AuthenticationAccount?> GetByIdAsync(
      long authenticationAccountId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(item => item.Id == authenticationAccountId));

    public Task<AuthenticationAccount?> GetByIdentityIdAsync(
      long identityId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(item => item.IdentityId == identityId));

    public Task<AuthenticationAccount?> GetByIdForUpdateAsync(
      long authenticationAccountId,
      CancellationToken cancellationToken = default) => GetByIdAsync(authenticationAccountId, cancellationToken);

    public Task<AuthenticationAccount?> GetByIdentityIdForUpdateAsync(
      long identityId,
      CancellationToken cancellationToken = default) => GetByIdentityIdAsync(identityId, cancellationToken);

    public Task<AuthenticationAccount?> GetByNormalizedLoginEmailAsync(
      string normalizedLoginEmail,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(item => item.NormalizedLoginEmail == normalizedLoginEmail));

    public Task ReloadAsync(AuthenticationAccount account, CancellationToken cancellationToken = default)
    {
      ReloadCount++;
      ReloadAction?.Invoke(account);
      return Task.CompletedTask;
    }

    public Task AddAsync(AuthenticationAccount account, CancellationToken cancellationToken = default)
    {
      SetId(account, Values.Count + 1);
      Values.Add(account);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeAuthenticationSessionRepository : IAuthenticationSessionRepository
  {
    public List<AuthenticationSession> Values { get; } = [];

    public Task<RefreshTokenSessionLocator?> GetRefreshTokenLocatorAsync(
      Guid refreshTokenPublicId,
      CancellationToken cancellationToken = default) => Task.FromResult<RefreshTokenSessionLocator?>(null);

    public Task<AuthenticationSession?> GetByRefreshTokenForUpdateAsync(
      long authenticationSessionId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(session => session.Id == authenticationSessionId));

    public Task<IReadOnlyList<AuthenticationSession>> ListActiveUnexpiredByIdentityForUpdateAsync(
      long identityId,
      DateTimeOffset utcNow,
      CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AuthenticationSession>>(
        Values.Where(session => session.IdentityId == identityId && session.IsUsable(utcNow)).ToArray());

    public Task<IReadOnlyList<AuthenticationSession>> ListActiveByIdentityForUpdateAsync(
      long identityId,
      CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AuthenticationSession>>(
        Values.Where(session => session.IdentityId == identityId && session.Status == AuthenticationSessionStatus.Active).ToArray());

    public Task AddAsync(AuthenticationSession session, CancellationToken cancellationToken = default)
    {
      Values.Add(session);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeActionTokenRepository : IAccountActionTokenRepository
  {
    public List<AccountActionToken> Values { get; } = [];

    public Task<AccountActionToken?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(item => item.PublicId == publicId));

    public Task<AccountActionToken?> GetActiveInvitationAsync(
      Guid tenantId,
      long tenantUserId,
      DateTimeOffset utcNow,
      CancellationToken cancellationToken = default) => Task.FromResult(Values.SingleOrDefault(item =>
        item.Purpose == AccountActionTokenPurpose.Invitation && item.TenantId == tenantId &&
        item.TenantUserId == tenantUserId && item.ConsumedUtc is null && item.RevokedUtc is null));

    public Task<AccountActionToken?> GetActivePasswordResetAsync(
      long authenticationAccountId,
      DateTimeOffset utcNow,
      CancellationToken cancellationToken = default) => Task.FromResult(Values.SingleOrDefault(item =>
        item.Purpose == AccountActionTokenPurpose.PasswordReset &&
        item.AuthenticationAccountId == authenticationAccountId && item.ConsumedUtc is null && item.RevokedUtc is null));

    public Task AddAsync(AccountActionToken actionToken, CancellationToken cancellationToken = default)
    {
      SetId(actionToken, Values.Count + 1);
      Values.Add(actionToken);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeUnitOfWork : IPlatformUnitOfWork
  {
    public Queue<Result<int>> SaveResults { get; } = new();

    public int SaveCount { get; private set; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      SaveCount++;
      return Task.FromResult(SaveResults.Count == 0 ? Result.Success(1) : SaveResults.Dequeue());
    }

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<ITransaction>(new FakeTransaction());
  }

  private sealed class FakeTransaction : ITransaction
  {
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }

  private sealed class FakePasswordHasher : IPasswordHashingService
  {
    public PasswordVerificationOutcome VerificationOutcome { get; set; } = PasswordVerificationOutcome.Success;

    public int HashCount { get; private set; }

    public int DummyVerificationCount { get; private set; }

    public int IneligibleVerificationCount { get; private set; }

    public string HashPassword(string password)
    {
      HashCount++;
      return $"test-hash-{HashCount}";
    }

    public PasswordVerificationOutcome VerifyPassword(string passwordHash, string providedPassword)
    {
      if (VerificationOutcome == PasswordVerificationOutcome.Success && providedPassword == "correct-password")
      {
        IneligibleVerificationCount++;
      }

      return VerificationOutcome;
    }

    public void PerformDummyVerification(string providedPassword) => DummyVerificationCount++;
  }

  private sealed class FakeCompromisedPasswordChecker : ICompromisedPasswordChecker
  {
    public CompromisedPasswordCheckOutcome Outcome { get; init; } = CompromisedPasswordCheckOutcome.Safe;

    public Task<CompromisedPasswordCheckOutcome> CheckAsync(
      string password,
      CancellationToken cancellationToken = default) => Task.FromResult(Outcome);
  }

  private sealed class FakeAuthenticationDiagnostics : IAuthenticationDiagnostics
  {
    public List<int> RetryNumbers { get; } = [];

    public int ExhaustedCount { get; private set; }

    public void FailedAttemptConcurrencyRetry(int retryNumber) => RetryNumbers.Add(retryNumber);

    public void FailedAttemptConcurrencyRetriesExhausted() => ExhaustedCount++;
  }

  private sealed class TestClock(DateTimeOffset utcNow) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow { get; set; } = utcNow;
  }

  private sealed class TestCurrentTenant(Guid tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class TestCurrentUser(string userId) : ICurrentUser
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

  private static void SetId(object entity, long id)
  {
    var field = typeof(Entity<long>).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(field);
    field.SetValue(entity, id);
  }

  private static void SetProperty<T>(object entity, string propertyName, T value)
  {
    var field = entity.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(field);
    field.SetValue(entity, value);
  }
}
