using System.Reflection;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Platform.Tests.PlatformSupport;

// Phase 3C-3 platform authentication session domain invariants (ADR-016 / DEC-TEN-0022).
public sealed class PlatformAuthenticationSessionTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
  private const string Client = "ssas-erp-web";
  private const long IdentityId = 42;
  private const long PrincipalId = 7;

  private static PlatformAuthenticationSession CreateSession(long id = 0) =>
    WithId(PlatformAuthenticationSession.Create(
      IdentityId, PrincipalId, Client, Guid.NewGuid(), securityVersionAtCreation: 3,
      Now, Now.AddMinutes(30), Now.AddHours(8)), id);

  private static byte[] Hash() => new byte[32];

  [Fact]
  public void Create_rejects_invalid_values()
  {
    Assert.Throws<ArgumentException>(() => PlatformAuthenticationSession.Create(0, PrincipalId, Client, Guid.NewGuid(), 3, Now, Now.AddMinutes(30), Now.AddHours(8)));
    Assert.Throws<ArgumentException>(() => PlatformAuthenticationSession.Create(IdentityId, 0, Client, Guid.NewGuid(), 3, Now, Now.AddMinutes(30), Now.AddHours(8)));
    Assert.Throws<ArgumentException>(() => PlatformAuthenticationSession.Create(IdentityId, PrincipalId, " ", Guid.NewGuid(), 3, Now, Now.AddMinutes(30), Now.AddHours(8)));
    Assert.Throws<ArgumentException>(() => PlatformAuthenticationSession.Create(IdentityId, PrincipalId, Client, Guid.Empty, 3, Now, Now.AddMinutes(30), Now.AddHours(8)));
    Assert.Throws<ArgumentException>(() => PlatformAuthenticationSession.Create(IdentityId, PrincipalId, Client, Guid.NewGuid(), 0, Now, Now.AddMinutes(30), Now.AddHours(8)));
    // Idle must not exceed absolute; expiries must be after creation.
    Assert.Throws<ArgumentException>(() => PlatformAuthenticationSession.Create(IdentityId, PrincipalId, Client, Guid.NewGuid(), 3, Now, Now.AddHours(9), Now.AddHours(8)));
    Assert.Throws<ArgumentException>(() => PlatformAuthenticationSession.Create(IdentityId, PrincipalId, Client, Guid.NewGuid(), 3, Now, Now, Now.AddHours(8)));
  }

  [Fact]
  public void Create_starts_active_and_anchored_to_identity_and_principal()
  {
    var session = CreateSession();

    Assert.Equal(AuthenticationSessionStatus.Active, session.Status);
    Assert.Equal(IdentityId, session.IdentityId);
    Assert.Equal(PrincipalId, session.PlatformSupportPrincipalId);
    Assert.Equal(3, session.SecurityVersionAtCreation);
    Assert.True(session.IsUsable(Now.AddMinutes(1)));
    Assert.Null(session.RevocationReason);
  }

  [Fact]
  public void Session_carries_no_tenant_or_company_members()
  {
    var type = typeof(PlatformAuthenticationSession);
    foreach (var forbidden in new[] { "TenantId", "TenantUserId", "CompanyId" })
    {
      Assert.Null(type.GetProperty(forbidden));
    }

    Assert.DoesNotContain(typeof(ITenantOwnedEntity), type.GetInterfaces());
  }

  [Fact]
  public void Initial_refresh_token_requires_a_persisted_active_session()
  {
    var unsaved = CreateSession(); // Id == 0
    Assert.Throws<InvalidOperationException>(() => unsaved.CreateInitialRefreshToken(Guid.NewGuid(), Hash(), Now));

    var session = CreateSession(100);
    var token = session.CreateInitialRefreshToken(Guid.NewGuid(), Hash(), Now);
    Assert.True(token.IsActive(Now.AddMinutes(1)));
    Assert.Single(session.RefreshTokenRecords);

    // Only one initial token may be created.
    Assert.Throws<InvalidOperationException>(() => session.CreateInitialRefreshToken(Guid.NewGuid(), Hash(), Now));
  }

  [Fact]
  public void Rotate_consumes_predecessor_links_replacement_and_extends_idle()
  {
    var session = CreateSession(100);
    var first = session.CreateInitialRefreshToken(Guid.NewGuid(), Hash(), Now);

    var rotated = session.Rotate(first, Guid.NewGuid(), Hash(), Now.AddMinutes(5), TimeSpan.FromMinutes(30));

    Assert.True(rotated.IsSuccess);
    Assert.NotNull(first.ConsumedUtc);
    Assert.False(first.IsActive(Now.AddMinutes(6)));
    Assert.True(rotated.Value.IsActive(Now.AddMinutes(6)));
    Assert.Equal(2, session.RefreshTokenRecords.Count);
    Assert.Equal(Now.AddMinutes(5), session.LastRefreshedUtc);
  }

  [Fact]
  public void Revoke_transitions_active_to_revoked_and_revokes_tokens()
  {
    var session = CreateSession(100);
    var token = session.CreateInitialRefreshToken(Guid.NewGuid(), Hash(), Now);

    var result = session.Revoke(PlatformAuthenticationSessionRevocationReason.PlatformPrincipalIneligible, "platform-actor", Now.AddMinutes(1));

    Assert.True(result.IsSuccess);
    Assert.Equal(AuthenticationSessionStatus.Revoked, session.Status);
    Assert.Equal(PlatformAuthenticationSessionRevocationReason.PlatformPrincipalIneligible, session.RevocationReason);
    Assert.Equal("platform-actor", session.RevokedBy);
    Assert.NotNull(session.RevokedUtc);
    Assert.NotNull(token.RevokedUtc);
    Assert.False(session.IsUsable(Now.AddMinutes(2)));
  }

  [Fact]
  public void Revoke_is_rejected_when_not_active()
  {
    var session = CreateSession(100);
    Assert.True(session.Revoke(PlatformAuthenticationSessionRevocationReason.UserLogout, null, Now).IsSuccess);

    var again = session.Revoke(PlatformAuthenticationSessionRevocationReason.Administrative, null, Now.AddMinutes(1));

    Assert.True(again.IsFailure);
    Assert.Equal(PlatformAuthenticationSessionRevocationReason.UserLogout, session.RevocationReason);
  }

  [Fact]
  public void Mark_compromised_requires_a_consumed_triggering_token()
  {
    var session = CreateSession(100);
    var first = session.CreateInitialRefreshToken(Guid.NewGuid(), Hash(), Now);

    // Not consumed yet → cannot compromise on it.
    Assert.True(session.MarkCompromised(first, Now.AddMinutes(1)).IsFailure);

    var rotated = session.Rotate(first, Guid.NewGuid(), Hash(), Now.AddMinutes(1), TimeSpan.FromMinutes(30)).Value;
    // Persisted-id emulation for the consumed predecessor.
    WithEntityId(first, 500);
    WithEntityId(rotated, 501);

    var compromised = session.MarkCompromised(first, Now.AddMinutes(2));

    Assert.True(compromised.IsSuccess);
    Assert.Equal(AuthenticationSessionStatus.Compromised, session.Status);
    Assert.Equal(500, session.CompromisedByRefreshTokenRecordId);
    Assert.NotNull(session.CompromisedUtc);
  }

  [Fact]
  public void Refresh_record_rejects_invalid_construction()
  {
    var session = CreateSession(100);
    Assert.Throws<ArgumentException>(() => session.CreateInitialRefreshToken(Guid.Empty, Hash(), Now));            // empty public id
    Assert.Throws<ArgumentException>(() => session.CreateInitialRefreshToken(Guid.NewGuid(), new byte[16], Now)); // wrong hash length
  }

  private static PlatformAuthenticationSession WithId(PlatformAuthenticationSession session, long id)
  {
    if (id != 0)
    {
      WithEntityId(session, id);
    }

    return session;
  }

  private static void WithEntityId(object entity, long id)
  {
    var field = typeof(Entity<long>).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(field);
    field!.SetValue(entity, id);
  }
}
