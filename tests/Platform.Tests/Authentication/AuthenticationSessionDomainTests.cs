using System.Reflection;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Events;
using SSAS.Platform.Infrastructure.Identity;

namespace SSAS.Platform.Tests.Authentication;

[Trait("Scenario", "TS-AUTH-0081")]
[Trait("Scenario", "TS-AUTH-0082")]
[Trait("Scenario", "TS-AUTH-0084")]
[Trait("Scenario", "TS-AUTH-0089")]
[Trait("Acceptance", "AC-AUTH-0029")]
[Trait("Acceptance", "AC-AUTH-0031")]
public sealed class AuthenticationSessionDomainTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);
  private static readonly AuthenticationClientId Client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;

  [Fact]
  public void Refresh_token_is_exactly_formatted_reveal_once_and_redacted()
  {
    var service = new AuthenticationTokenService();
    var generated = service.GenerateRefreshToken(41, Guid.NewGuid(), Client);

    Assert.Equal("[REDACTED SENSITIVE REFRESH TOKEN]", generated.SensitiveToken.ToString());
    var reveal = generated.SensitiveToken.RevealOnce();
    Assert.True(reveal.IsSuccess);
    Assert.Equal(76, reveal.Value.Length);
    Assert.Equal('.', reveal.Value[32]);
    Assert.True(service.TryReadPublicId(new SensitiveAuthenticationTokenInput(reveal.Value), out var publicId));
    Assert.Equal(generated.PublicId, publicId);
    Assert.True(generated.SensitiveToken.RevealOnce().IsFailure);
  }

  [Theory]
  [InlineData("")]
  [InlineData("not-a-token")]
  [InlineData("00000000000000000000000000000000.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
  [InlineData("00000000000000000000000000000000.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.")]
  public void Token_parser_rejects_noncanonical_inputs_without_database_material(string presented)
  {
    var service = new AuthenticationTokenService();

    Assert.False(service.TryReadPublicId(new SensitiveAuthenticationTokenInput(presented), out var publicId));
    Assert.Equal(Guid.Empty, publicId);
    Assert.Equal("[REDACTED SENSITIVE AUTHENTICATION TOKEN INPUT]", new SensitiveAuthenticationTokenInput(presented).ToString());
  }

  [Fact]
  public void Selection_proof_is_bound_to_public_id_identity_security_version_and_client()
  {
    var service = new AuthenticationTokenService();
    var generated = service.GenerateTenantSelectionProof(17, 4, Client);
    var raw = generated.SensitiveProof.RevealOnce().Value;
    var transaction = TenantSelectionTransaction.Create(
      generated.PublicId,
      17,
      Client.Value,
      4,
      generated.SecretHash,
      Now,
      Now.AddMinutes(5),
      Guid.NewGuid());
    var otherClientTransaction = TenantSelectionTransaction.Create(
      generated.PublicId,
      17,
      "another-client",
      4,
      generated.SecretHash,
      Now,
      Now.AddMinutes(5),
      Guid.NewGuid());

    Assert.True(service.VerifyTenantSelection(transaction, new SensitiveAuthenticationTokenInput(raw)));
    Assert.False(service.VerifyTenantSelection(otherClientTransaction, new SensitiveAuthenticationTokenInput(raw)));
  }

  [Fact]
  public void Selection_transaction_is_single_use_and_emits_safe_selection_events()
  {
    var transaction = TenantSelectionTransaction.Create(
      Guid.NewGuid(), 17, Client.Value, 4, new byte[32], Now, Now.AddMinutes(5), Guid.NewGuid());

    var first = transaction.Consume(31, Guid.NewGuid(), 51, Guid.NewGuid(), Now.AddMinutes(1));
    var second = transaction.Consume(31, Guid.NewGuid(), 52, Guid.NewGuid(), Now.AddMinutes(2));

    Assert.True(first.IsSuccess);
    Assert.True(second.IsFailure);
    Assert.Contains(transaction.DomainEvents, domainEvent => domainEvent is SSAS.Platform.Domain.Events.TenantSelectionRequired);
    Assert.Contains(transaction.DomainEvents, domainEvent => domainEvent is TenantMembershipSelected);
  }

  [Fact]
  public void Rotation_consumes_predecessor_caps_idle_expiry_and_reuse_compromises_descendants()
  {
    var session = NewPersistedSession(100, 90);
    var predecessor = session.CreateInitialRefreshToken(Guid.NewGuid(), new byte[32], Now, Guid.NewGuid());
    SetIdentity(predecessor, 200);

    var rotation = session.Rotate(
      predecessor,
      Guid.NewGuid(),
      Enumerable.Repeat((byte)7, 32).ToArray(),
      Now.AddDays(70),
      TimeSpan.FromDays(30),
      Guid.NewGuid());
    Assert.True(rotation.IsSuccess);
    SetIdentity(rotation.Value, 201);

    var compromised = session.MarkCompromised(predecessor, Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(71));

    Assert.True(compromised.IsSuccess);
    Assert.Equal(AuthenticationSessionStatus.Compromised, session.Status);
    Assert.Equal(200, session.CompromisedByRefreshTokenRecordId);
    Assert.Equal(session.AbsoluteExpiresUtc, session.IdleExpiresUtc);
    Assert.NotNull(rotation.Value.RevokedUtc);
    Assert.Contains(session.DomainEvents, domainEvent => domainEvent is AuthenticationSessionRefreshed);
    Assert.Contains(session.DomainEvents, domainEvent => domainEvent is AuthenticationSessionCompromised);
    Assert.Contains(session.DomainEvents, domainEvent => domainEvent is RefreshTokenReuseDetected);
  }

  [Fact]
  public void Revocation_is_terminal_and_revokes_unconsumed_tokens()
  {
    var session = NewPersistedSession(101);
    var token = session.CreateInitialRefreshToken(Guid.NewGuid(), new byte[32], Now, Guid.NewGuid());

    var revoke = session.Revoke(
      AuthenticationSessionRevocationReason.PasswordReset,
      "password-reset",
      Guid.NewGuid(),
      Now.AddMinutes(1));

    Assert.True(revoke.IsSuccess);
    Assert.False(session.IsUsable(Now.AddMinutes(1)));
    Assert.NotNull(token.RevokedUtc);
    Assert.True(session.Revoke(AuthenticationSessionRevocationReason.Administrative, null, Guid.NewGuid(), Now.AddMinutes(2)).IsFailure);
  }

  private static AuthenticationSession NewPersistedSession(long id, int idleDays = 30)
  {
    var session = AuthenticationSession.Create(
      17,
      31,
      Guid.NewGuid(),
      Client.Value,
      Guid.NewGuid(),
      4,
      Now,
      Now.AddDays(idleDays),
      Now.AddDays(90));
    SetIdentity(session, id);
    return session;
  }

  private static void SetIdentity<T>(Entity<T> entity, T id) where T : notnull
  {
    var field = typeof(Entity<T>).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(field);
    field.SetValue(entity, id);
  }
}
