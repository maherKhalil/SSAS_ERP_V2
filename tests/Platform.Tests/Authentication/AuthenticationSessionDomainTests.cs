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

  // Hoisted because CA1861 is enforced as an error by the gate: an inline array argument in an assertion
  // trips it. Both lists are the SUBJECT of their assertions, not incidental data.
  private static readonly string[] ApprovedSessionStatuses = ["Active", "Compromised", "Revoked"];

  private static readonly string[] ApprovedSessionOperations =
  [
    "ClearBranch", "CreateInitialRefreshToken", "FindRefreshToken", "IsUsable",
    "MarkCompromised", "Revoke", "Rotate", "SelectBranch"
  ];

  [Fact]
  [Trait("Acceptance", "AC-AUTH-0026")]
  // ==================================================================================================
  // `AC-AUTH-0026` — *"A session is IMMUTABLY BOUND to one Identity, membership, Tenant, ClientId, and
  // token family and PERSISTS ONLY Active, Revoked, or Compromised status."* TWO claims, and the second is
  // a claim about a COMPLEMENT.
  // ==================================================================================================
  //
  // ⚠ *PERSISTS ONLY …* IS AN ENUM-VOCABULARY CLAIM AND NOTHING PINNED IT. Every test that touches
  // `AuthenticationSessionStatus` asserts a session HAS one of the three; **a fourth member added tomorrow
  // satisfies all of them and violates the criterion.** A presence assertion cannot carry an *only*; the
  // arity pin is the whole content of the word, and this is the same idiom
  // `Status_and_reason_vocabularies_are_exact` uses for tenants.
  //
  // ⚠⚠ AND THE *IMMUTABLY BOUND* HALF CANNOT BE A "NO PUBLIC SETTER" CHECK, WHICH IS THE OBVIOUS TEST AND
  // THE WRONG ONE. **Every property on this aggregate is `private set`, including `Status`, `RevokedUtc`
  // and `IdleExpiresUtc`, which the aggregate mutates on purpose** — so that assertion passes for all
  // twenty-one and discriminates none of them. *Immutable* here means NO OPERATION CHANGES THEM, so the
  // test drives the operations.
  //
  // ⚠⚠⚠ AND IT PINS THE MUTATOR SET RATHER THAN SAMPLING IT. Exercising four methods proves nothing about
  // a fifth added later, so the public/internal instance-method surface is asserted to be exactly this
  // list: **a new operation forces this test to be updated, which is the only way an enumeration of
  // behaviour stays complete.** Without it the test decays silently the first time the aggregate grows.
  public void Session_bindings_survive_every_operation_and_the_status_vocabulary_is_exact()
  {
    // ---- THE COMPLEMENT CLAIM.
    Assert.Equal(
      ApprovedSessionStatuses,
      Enum.GetNames<AuthenticationSessionStatus>().OrderBy(name => name, StringComparer.Ordinal).ToArray());

    // ---- THE MUTATOR SET, PINNED SO THE ENUMERATION BELOW CANNOT GO STALE.
    var mutators = typeof(AuthenticationSession)
      .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
      .Where(method => !method.IsPrivate && !method.IsSpecialName)
      .Select(method => method.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();
    Assert.Equal(ApprovedSessionOperations, mutators);

    var session = NewPersistedSession(100, 90);
    var bindings = (session.IdentityId, session.TenantUserId, session.TenantId, session.ClientId, session.TokenFamilyId);

    // ---- EVERY OPERATION THAT CHANGES STATE, IN AN ORDER THAT LETS EACH SUCCEED.
    Assert.True(session.SelectBranch(Guid.NewGuid()).IsSuccess);
    session.ClearBranch();
    var predecessor = session.CreateInitialRefreshToken(Guid.NewGuid(), new byte[32], Now, Guid.NewGuid());
    SetIdentity(predecessor, 200);
    var rotation = session.Rotate(
      predecessor, Guid.NewGuid(), Enumerable.Repeat((byte)7, 32).ToArray(), Now.AddDays(1), TimeSpan.FromDays(30), Guid.NewGuid());
    Assert.True(rotation.IsSuccess);
    SetIdentity(rotation.Value, 201);
    Assert.True(session.MarkCompromised(predecessor, Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(2)).IsSuccess);

    Assert.Equal(
      bindings,
      (session.IdentityId, session.TenantUserId, session.TenantId, session.ClientId, session.TokenFamilyId));

    // The control: the session DID change, so the equality above is a survival claim rather than a
    // statement that nothing happened. Without this, a no-op aggregate would satisfy every line.
    //
    // ⚠ PLANTS, AND THE FIRST ONE FOUND SOMETHING I WAS NOT LOOKING FOR. Rebinding `TenantUserId` in
    // `SelectBranch` reddens the tuple above, naming the field. **Rebinding `TokenFamilyId` in `Rotate`
    // reddens too — but by THROWING from `RefreshTokenRecord.LinkReplacement`, which independently
    // validates the family.** So that one binding is guarded twice and the aggregate refuses the change
    // before this test can observe it; the other four rest on this assertion alone. *Worth knowing which
    // of the five are load-bearing here, because a plant that reddens for the wrong reason still reads as
    // a passing plant.*
    Assert.Equal(AuthenticationSessionStatus.Compromised, session.Status);
  }

  [Fact]
  [Trait("Acceptance", "AC-AUTH-0030")]
  // ==================================================================================================
  // `AC-AUTH-0030` — *"Refresh atomically consumes one token, links exactly one replacement, UPDATES IDLE
  // EXPIRATION WITHOUT EXTENDING ABSOLUTE EXPIRATION, and rolls back all changes on failed persistence."*
  // ==================================================================================================
  //
  // The third clause is the one this test carries, and it is carried by `Assert.Equal(AbsoluteExpiresUtc,
  // IdleExpiresUtc)` after rotating at `Now + 70d` with a 30-day idle lifetime — the `Min` clamp binds.
  //
  // ⚠⚠ THAT ASSERTION SITS AFTER A `MarkCompromised` CALL, SO TWO MECHANISMS COULD PRODUCE IT. A compromise
  // that collapsed the idle window would give the same equality, and the green would say nothing about
  // which. ***WHEN AN ASSERTION SITS DOWNSTREAM OF MORE THAN ONE MECHANISM THAT COULD PRODUCE IT, THE
  // CITATION IS A CLAIM ABOUT ATTRIBUTION AND ONLY A PLANT SETTLES IT.*** Planted: `Min(utc.Add(idleLifetime),
  // AbsoluteExpiresUtc)` replaced by `utc.Add(idleLifetime)` — THIS test reddens. Attributed to the rotation.
  //
  // ⚠ NOT WITNESSED HERE: *links exactly one replacement* — the count of records is asserted, but not that
  // the predecessor POINTS at the successor; and *rolls back on failed persistence*, which needs a failing
  // unit of work (`Access_token_issuance_failure_rolls_back_...`, `AC-AUTH-0046`, is the nearest).
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
