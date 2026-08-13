using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Infrastructure.Identity;

namespace SSAS.Platform.Infrastructure.PlatformSupport;

// Genesis/recovery bootstrap orchestrator (ADR-016 / DEC-TEN-0019/0020/0021, refined by DEC-TEN-0026).
//
// Convergence: when the plane lacks usable authority OR lacks usable ADMINISTRATIVE authority, establish
// exactly one new usable principal from the first eligible configured subject (deterministic ordinal
// selection), granting the configured PlatformSupport grant set (which must include Administer).
//
// Recovery is always new-principal-only. It never elevates the surviving non-admin principal, so losing the
// last Administer grant is recovered by establishing a separate configured recovery principal, not by
// silently handing administrative authority to whichever principal happens to remain.
// Guarantees:
//  * Inert unless opted in (no configured subjects -> zero persistence access).
//  * Never acts while usable authority already exists (live persistence-backed check, not a cached flag).
//  * Recovery is new-principal-only: it never re-enables a Disabled principal and never reuses an identity
//    that already owns a principal, so no implicit Disabled -> Active reactivation can occur here.
//  * Principal + full initial grant set are one atomic write (EF relationship fixup, single SaveChanges),
//    so a principal without its Platform.Support.Administer grant is structurally impossible.
//  * Concurrent bootstrap converges on exactly one principal. Convergence is provided by the recovery
//    serialization (IPlatformSupportRecoverySerializer): competing hosts contend on one resource common to
//    every candidate, then re-evaluate authority live while holding it, so a host that waited behind a peer
//    observes the peer's committed authority and stops instead of skipping that candidate and seeding a
//    second recovery principal. IdentityId uniqueness remains as defense-in-depth for a same-identity race,
//    and its loser path still requires BOTH general and administrative authority before reporting
//    convergence — it is no longer the primary multi-subject convergence mechanism.
public sealed class PlatformSupportBootstrapService(
  IOptions<PlatformSupportBootstrapOptions> optionsAccessor,
  IIdentityRepository identityRepository,
  IAuthenticationAccountRepository accountRepository,
  IPlatformSupportPrincipalRepository principalRepository,
  IPlatformSupportAuthorityStateReadService authorityState,
  IPlatformSupportRecoverySerializer recoverySerializer,
  IPermissionCatalog permissionCatalog,
  IPlatformUnitOfWork unitOfWork,
  IDateTimeProvider clock) : IPlatformSupportBootstrapService
{
  public async Task<PlatformSupportBootstrapOutcome> RunAsync(CancellationToken cancellationToken = default)
  {
    var options = optionsAccessor.Value;
    var subjects = options.Subjects ?? [];
    if (subjects.Length == 0)
    {
      // Opt-in: perform no persistence access at all when bootstrap is unconfigured.
      return PlatformSupportBootstrapOutcome.NoCandidatesConfigured;
    }

    // DEC-TEN-0026 recovery predicate. Two DISTINCT live states are evaluated, never collapsed into one flag:
    //
    //   general | administrative | outcome
    //   --------+----------------+---------------------------------------------
    //   false   | false          | recovery eligible (genesis: nobody can use the plane)
    //   true    | false          | recovery eligible (admin loss: usable plane, but nobody can administer it)
    //   true    | true           | inert
    //   false   | true           | unreachable — administrative authority implies general authority
    //
    // Only the fully-authorised state is inert. Checking general authority alone would leave the admin-loss
    // state permanently unrecoverable, because a surviving Platform.Tenants.View principal keeps general
    // authority true while no principal can Register/Grant/Revoke/Disable/Re-enable ever again.
    // Fast path, unserialized: a fully-authorised platform never takes the recovery lock, so an ordinary host
    // start costs two existential reads and nothing else. This check is advisory only — the binding decision is
    // re-made below under serialization, because a stale pre-check cannot be trusted against a concurrent peer.
    var hasUsableAuthority = await authorityState.HasUsablePlatformAuthorityAsync(cancellationToken);
    var hasUsableAdministrativeAuthority =
      await authorityState.HasUsablePlatformAdministrativeAuthorityAsync(cancellationToken);

    if (hasUsableAuthority && hasUsableAdministrativeAuthority)
    {
      return PlatformSupportBootstrapOutcome.AuthorityAlreadyUsable;
    }

    // Everything from here to the commit runs inside one transaction that holds the recovery serialization,
    // so competing hosts cannot interleave their authority decision with another host's recovery write.
    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    if (!await recoverySerializer.TryAcquireAsync(cancellationToken))
    {
      // Fail closed: never recover unserialized, because that is precisely how two configured subjects both
      // become Administer-bearing recovery principals.
      return PlatformSupportBootstrapOutcome.NoEligibleCandidate;
    }

    // Binding decision, serialized and live. A worker that waited here while a peer established authority now
    // observes it and stops, instead of skipping the peer's candidate and seeding a second recovery principal.
    hasUsableAuthority = await authorityState.HasUsablePlatformAuthorityAsync(cancellationToken);
    hasUsableAdministrativeAuthority =
      await authorityState.HasUsablePlatformAdministrativeAuthorityAsync(cancellationToken);

    if (hasUsableAuthority && hasUsableAdministrativeAuthority)
    {
      return PlatformSupportBootstrapOutcome.AuthorityAlreadyUsable;
    }

    // Resolve every configured subject against live persistence and keep only candidates that can seed
    // usable authority now: the identity exists, does not already own a principal (covers a Disabled or
    // otherwise-unusable existing principal -> new-principal-only recovery), and its account is eligible.
    var eligibleCandidates = new List<(string Subject, long IdentityId)>();
    foreach (var subject in subjects)
    {
      var identity = await identityRepository.GetBySubjectAsync(subject, cancellationToken);
      if (identity is null)
      {
        continue;
      }

      if (await principalRepository.ExistsForIdentityAsync(identity.Id, cancellationToken))
      {
        continue;
      }

      var account = await accountRepository.GetByIdentityIdAsync(identity.Id, cancellationToken);
      if (account is not { IsAuthenticationEligible: true })
      {
        continue;
      }

      eligibleCandidates.Add((subject, identity.Id));
    }

    if (eligibleCandidates.Count == 0)
    {
      // Fail closed: no eligible configured subject can establish authority (e.g. only a Disabled-A exists).
      return PlatformSupportBootstrapOutcome.NoEligibleCandidate;
    }

    // Deterministic ordinal first-eligible selection (DEC-TEN-0019): independent of configuration order
    // and identical across concurrent hosts, so every host that reaches the write races for the same subject.
    var selected = eligibleCandidates
      .OrderBy(candidate => candidate.Subject, StringComparer.Ordinal)
      .First();

    var registration = PlatformSupportPrincipal.Register(selected.IdentityId);
    if (registration.IsFailure)
    {
      return PlatformSupportBootstrapOutcome.NoEligibleCandidate;
    }

    var principal = registration.Value;
    var actor = $"platform-bootstrap:{selected.Subject}";

    // Build the full grant set on the aggregate before persisting. Scope is re-resolved from the catalog
    // even though options validation already enforced it, so no non-PlatformSupport name can slip through.
    foreach (var permissionName in (options.InitialPermissions ?? []).Distinct(StringComparer.Ordinal))
    {
      if (!permissionCatalog.TryGet(permissionName, out var definition) ||
        definition.Scope != PermissionScope.PlatformSupport)
      {
        return PlatformSupportBootstrapOutcome.NoEligibleCandidate;
      }

      var grant = principal.GrantPermission(definition, actor, clock.UtcNow);
      if (grant.IsFailure)
      {
        return PlatformSupportBootstrapOutcome.NoEligibleCandidate;
      }
    }

    await principalRepository.AddAsync(principal, cancellationToken);

    // Single atomic write: root principal + every assignment insert together, or nothing does.
    var save = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (save.IsFailure)
    {
      // A unique-constraint violation means a concurrent host won the race for the same deterministically
      // selected subject. Re-read live authority and converge on that single principal rather than falling
      // through to a second candidate (which would broaden authority).
      //
      // The recheck must include the ADMINISTRATIVE predicate (DEC-TEN-0026): in the admin-loss state general
      // authority is already true because of the surviving non-admin principal, so testing it alone would
      // report convergence even if the winner had not actually established administrative authority.
      if (save.Error == IdentityAccessErrors.UniqueConstraintViolation &&
        await authorityState.HasUsablePlatformAuthorityAsync(cancellationToken) &&
        await authorityState.HasUsablePlatformAdministrativeAuthorityAsync(cancellationToken))
      {
        return PlatformSupportBootstrapOutcome.AuthorityAlreadyUsable;
      }

      return PlatformSupportBootstrapOutcome.NoEligibleCandidate;
    }

    // Commit publishes the new principal AND its Administer grant together and only then releases the
    // recovery serialization, so a waiting peer can never observe a half-established recovery.
    await transaction.CommitAsync(cancellationToken);
    return PlatformSupportBootstrapOutcome.GenesisEstablished;
  }
}
