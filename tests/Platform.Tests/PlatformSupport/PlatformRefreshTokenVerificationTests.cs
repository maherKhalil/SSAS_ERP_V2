using System.Reflection;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Infrastructure.Identity;

namespace SSAS.Platform.Tests.PlatformSupport;

// Phase 3C-4: the platform refresh-token verification mirrors the tenant crypto and is bound to the platform
// session id (ADR-016 / DEC-TEN-0022).
public sealed class PlatformRefreshTokenVerificationTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
  private static readonly AuthenticationClientId Client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;

  [Fact]
  public void A_generated_platform_refresh_token_verifies_and_a_tampered_one_does_not()
  {
    var service = new AuthenticationTokenService();
    var session = ActiveSession(sessionId: 100, tokenFamilyId: Guid.NewGuid());
    var generated = service.GenerateRefreshToken(session.Id, session.TokenFamilyId, Client);
    var record = session.CreateInitialRefreshToken(generated.PublicId, generated.SecretHash, Now);

    var valid = new SensitiveAuthenticationTokenInput(generated.SensitiveToken.RevealOnce().Value);
    Assert.True(service.VerifyRefreshToken(session, record, valid));

    // A token whose secret does not match the stored hash is rejected (constant-time compare).
    var tampered = new SensitiveAuthenticationTokenInput($"{generated.PublicId:N}.{new string('A', 43)}");
    Assert.False(service.VerifyRefreshToken(session, record, tampered));
  }

  [Fact]
  public void A_refresh_token_bound_to_a_different_session_id_does_not_verify()
  {
    var service = new AuthenticationTokenService();
    var family = Guid.NewGuid();
    // Token generated for session 200, but presented against a session with a different id → hash mismatch.
    var generated = service.GenerateRefreshToken(200, family, Client);
    var otherSession = ActiveSession(sessionId: 201, tokenFamilyId: family);
    var record = otherSession.CreateInitialRefreshToken(generated.PublicId, generated.SecretHash, Now);

    var input = new SensitiveAuthenticationTokenInput(generated.SensitiveToken.RevealOnce().Value);
    Assert.False(service.VerifyRefreshToken(otherSession, record, input));
  }

  private static PlatformAuthenticationSession ActiveSession(long sessionId, Guid tokenFamilyId)
  {
    var session = PlatformAuthenticationSession.Create(
      42, 7, Client.Value, tokenFamilyId, securityVersionAtCreation: 1, Now, Now.AddMinutes(30), Now.AddHours(8));
    var field = typeof(Entity<long>).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(field);
    field!.SetValue(session, sessionId);
    return session;
  }
}
