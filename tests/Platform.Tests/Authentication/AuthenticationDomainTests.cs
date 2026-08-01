using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Events;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.Authentication;

public sealed class AuthenticationDomainTests
{
  private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("BusinessRequirement", "BR-AUTH-0001")]
  [Trait("BusinessRule", "BRULE-AUTH-0013")]
  [Trait("Decision", "DEC-AUTH-0023")]
  [Trait("Requirement", "SEC-AUTH-0206")]
  [Trait("Scenario", "TS-AUTH-0006")]
  public void Local_subject_is_exact_immutable_and_uses_guid_n_format()
  {
    var id = Guid.Parse("72e4872e-f9c9-47e9-b433-9c27e9478869");

    var subject = AuthenticationSubject.CreateLocal(id);

    Assert.True(subject.IsSuccess);
    Assert.Equal("local:72e4872ef9c947e9b4339c27e9478869", subject.Value.Value);
    Assert.Null(typeof(AuthenticationSubject).GetProperty(nameof(AuthenticationSubject.Value))?.SetMethod);
    Assert.True(AuthenticationSubject.CreateLocal(Guid.Empty).IsFailure);
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0024")]
  [Trait("Requirement", "BR-AUTH-0001")]
  [Trait("Scenario", "TS-AUTH-0008")]
  public void Login_email_trims_preserves_display_case_and_applies_no_provider_alias_rules()
  {
    var email = LoginEmail.Create("  Case.User+alias@Gmail.com  ");

    Assert.True(email.IsSuccess);
    Assert.Equal("Case.User+alias@Gmail.com", email.Value.Value);
    Assert.Equal("CASE.USER+ALIAS@GMAIL.COM", email.Value.NormalizedValue);
    Assert.NotEqual(
      LoginEmail.Create("caseuser@gmail.com").Value.NormalizedValue,
      email.Value.NormalizedValue);
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0023")]
  [Trait("Scenario", "TS-AUTH-0007")]
  public void Login_email_is_independent_from_the_stable_local_identity_subject()
  {
    var subject = AuthenticationSubject.CreateLocal(Guid.Parse("ca78252a-2e60-4f49-904c-6482191838e7")).Value;
    var firstEmail = LoginEmail.Create("first@example.com").Value;
    var laterEmail = LoginEmail.Create("later@example.com").Value;

    Assert.Equal("local:ca78252a2e604f49904c6482191838e7", subject.Value);
    Assert.NotEqual(firstEmail.NormalizedValue, laterEmail.NormalizedValue);
    Assert.Equal("local:ca78252a2e604f49904c6482191838e7", subject.Value);
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0027")]
  [Trait("Requirement", "FR-AUTH-0101")]
  [Trait("Scenario", "TS-AUTH-0001")]
  public void Pending_account_setup_verifies_email_and_activates_without_advancing_security_version()
  {
    var account = CreatePendingAccount();

    var result = account.CompleteInitialSetup("hash-v1", Guid.NewGuid(), Now);

    Assert.True(result.IsSuccess);
    Assert.Equal(AuthenticationAccountStatus.Active, account.Status);
    Assert.True(account.HasPassword);
    Assert.Equal(Now, account.EmailVerifiedUtc);
    Assert.Equal(Now, account.PasswordChangedUtc);
    Assert.Equal(1, account.SecurityVersion);
    Assert.Contains(account.DomainEvents, item => item is AuthenticationAccountSetupCompleted);
    Assert.True(account.CompleteInitialSetup("hash-v2", Guid.NewGuid(), Now).IsFailure);
  }

  [Fact]
  [Trait("BusinessRequirement", "BR-AUTH-0007")]
  [Trait("BusinessRule", "BRULE-AUTH-0011")]
  [Trait("NonFunctional", "NFR-AUTH-0305")]
  [Trait("Decision", "DEC-AUTH-0027")]
  [Trait("Requirement", "FR-AUTH-0118")]
  [Trait("Acceptance", "AC-AUTH-0016")]
  [Trait("Scenario", "TS-AUTH-0012")]
  public void Five_consecutive_failures_lock_the_account_for_fifteen_minutes_and_expiry_restores_eligibility()
  {
    var account = CreateActiveAccount();

    for (var count = 1; count <= 5; count++)
    {
      Assert.True(account.RecordFailedAttempt(5, TimeSpan.FromMinutes(15), Guid.NewGuid(), Now).IsSuccess);
      Assert.Equal(count, account.FailedAttemptCount);
    }

    Assert.True(account.IsLocked(Now.AddMinutes(14)));
    Assert.False(account.CanVerifyPassword(Now.AddMinutes(14)));
    Assert.False(account.IsLocked(Now.AddMinutes(15)));
    Assert.True(account.CanVerifyPassword(Now.AddMinutes(15)));
    Assert.Contains(account.DomainEvents, item => item is AuthenticationAccountLocked);
  }

  [Fact]
  [Trait("NonFunctional", "NFR-AUTH-0305")]
  [Trait("Requirement", "SEC-AUTH-0201")]
  [Trait("Scenario", "TS-AUTH-0011")]
  public void Successful_rehash_changes_only_the_hash_representation()
  {
    var account = CreateActiveAccount();
    var securityVersion = account.SecurityVersion;
    var passwordChangedUtc = account.PasswordChangedUtc;

    var result = account.ReplaceHashAfterSuccessfulVerification("hash-v2", Guid.NewGuid(), Now.AddHours(1));

    Assert.True(result.IsSuccess);
    Assert.Equal(securityVersion, account.SecurityVersion);
    Assert.Equal(passwordChangedUtc, account.PasswordChangedUtc);
    Assert.Contains(account.DomainEvents, item => item is AuthenticationPasswordRehashed);
    Assert.Single(account.DomainEvents);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-AUTH-0010")]
  [Trait("Requirement", "FR-AUTH-0117")]
  [Trait("Requirement", "FR-AUTH-0124")]
  [Trait("Scenario", "TS-AUTH-0017")]
  public void Password_reset_advances_security_version_and_clears_failed_attempt_state()
  {
    var account = CreateActiveAccount();
    Assert.True(account.RecordFailedAttempt(5, TimeSpan.FromMinutes(15), Guid.NewGuid(), Now).IsSuccess);
    var priorVersion = account.SecurityVersion;

    var result = account.ResetPassword("hash-reset", Guid.NewGuid(), Now.AddMinutes(1));

    Assert.True(result.IsSuccess);
    Assert.Equal(priorVersion + 1, account.SecurityVersion);
    Assert.Equal(0, account.FailedAttemptCount);
    Assert.Null(account.LockoutEndUtc);
    Assert.Equal(Now.AddMinutes(1), account.PasswordChangedUtc);
    Assert.Contains(account.DomainEvents, item => item is AuthenticationPasswordReset);
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0027")]
  public void Disabling_an_active_account_advances_security_version_without_changing_password_time()
  {
    var account = CreateActiveAccount();
    var priorVersion = account.SecurityVersion;
    var passwordChangedUtc = account.PasswordChangedUtc;

    var result = account.Disable(Guid.NewGuid(), Now.AddMinutes(1));

    Assert.True(result.IsSuccess);
    Assert.Equal(AuthenticationAccountStatus.Disabled, account.Status);
    Assert.Equal(priorVersion + 1, account.SecurityVersion);
    Assert.Equal(passwordChangedUtc, account.PasswordChangedUtc);
    Assert.Contains(account.DomainEvents, item => item is AuthenticationAccountDisabled);
  }

  [Fact]
  [Trait("BusinessRequirement", "BR-AUTH-0006")]
  [Trait("BusinessRule", "BRULE-AUTH-0008")]
  [Trait("BusinessRule", "BRULE-AUTH-0012")]
  [Trait("Decision", "DEC-AUTH-0029")]
  [Trait("Requirement", "SEC-AUTH-0205")]
  [Trait("Acceptance", "AC-AUTH-0014")]
  [Trait("Scenario", "TS-AUTH-0018")]
  public void Action_tokens_are_purpose_bound_single_use_and_retain_lifecycle_state()
  {
    var token = AccountActionToken.CreateInvitation(
      Guid.NewGuid(),
      Enumerable.Repeat((byte)7, 32).ToArray(),
      10,
      20,
      Guid.NewGuid(),
      30,
      Now,
      Now.AddHours(24),
      Guid.NewGuid());

    Assert.True(token.ValidateForUse(AccountActionTokenPurpose.Invitation, Now).IsSuccess);
    Assert.True(token.ValidateForUse(AccountActionTokenPurpose.PasswordReset, Now).IsFailure);
    Assert.True(token.Consume(Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
    Assert.True(token.Consume(Guid.NewGuid(), Now.AddMinutes(2)).IsFailure);
    Assert.NotNull(token.ConsumedUtc);
    Assert.Contains(token.DomainEvents, item => item is AccountActionTokenIssued);
    Assert.Contains(token.DomainEvents, item => item is AccountActionTokenConsumed);

    var revoked = AccountActionToken.CreatePasswordReset(
      Guid.NewGuid(), Enumerable.Repeat((byte)8, 32).ToArray(), 10, 20,
      Now, Now.AddMinutes(30), Guid.NewGuid());
    Assert.True(revoked.Revoke(null, "Replaced", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
    Assert.True(revoked.ValidateForUse(AccountActionTokenPurpose.PasswordReset, Now.AddMinutes(2)).IsFailure);

    var expired = AccountActionToken.CreatePasswordReset(
      Guid.NewGuid(), Enumerable.Repeat((byte)9, 32).ToArray(), 10, 20,
      Now, Now.AddMinutes(30), Guid.NewGuid());
    Assert.True(expired.ValidateForUse(AccountActionTokenPurpose.PasswordReset, Now.AddMinutes(30)).IsFailure);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-AUTH-0013")]
  [Trait("Decision", "DEC-AUTH-0025")]
  [Trait("Requirement", "FR-AUTH-0101")]
  [Trait("Scenario", "TS-AUTH-0005")]
  public void Pending_membership_activates_without_role_assignment()
  {
    var membership = TenantUser.CreatePending(
      10,
      Guid.NewGuid(),
      EmailAddress.Create("tenant@example.com").Value,
      UserDisplayName.Create("Pending User").Value,
      Guid.NewGuid(),
      Now);

    Assert.Equal(TenantUserStatus.Pending, membership.Status);
    Assert.Empty(membership.RoleAssignments);
    Assert.True(membership.Activate(Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
    Assert.Equal(TenantUserStatus.Active, membership.Status);
    Assert.Empty(membership.RoleAssignments);
  }

  [Fact]
  [Trait("Requirement", "SEC-AUTH-0203")]
  [Trait("Scenario", "TS-AUTH-0056")]
  public void Credential_and_action_token_hashes_are_not_public_aggregate_properties()
  {
    Assert.Null(typeof(AuthenticationAccount).GetProperty("PasswordHash"));
    Assert.Null(typeof(AccountActionToken).GetProperty("SecretHash"));
  }

  private static AuthenticationAccount CreatePendingAccount() =>
    AuthenticationAccount.CreatePending(10, LoginEmail.Create("User@example.com").Value);

  private static AuthenticationAccount CreateActiveAccount()
  {
    var account = CreatePendingAccount();
    Assert.True(account.CompleteInitialSetup("hash-v1", Guid.NewGuid(), Now).IsSuccess);
    account.ClearDomainEvents();
    return account;
  }
}
