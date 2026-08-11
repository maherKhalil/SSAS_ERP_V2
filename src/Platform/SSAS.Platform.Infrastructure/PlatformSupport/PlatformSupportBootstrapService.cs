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

// Genesis/recovery bootstrap orchestrator (ADR-016 / DEC-TEN-0019/0020/0021).
//
// Convergence: no usable authority -> establish exactly one new usable principal from the first eligible
// configured subject (deterministic ordinal selection), granting the configured PlatformSupport grant set.
// Guarantees:
//  * Inert unless opted in (no configured subjects -> zero persistence access).
//  * Never acts while usable authority already exists (live persistence-backed check, not a cached flag).
//  * Recovery is new-principal-only: it never re-enables a Disabled principal and never reuses an identity
//    that already owns a principal, so no implicit Disabled -> Active reactivation can occur here.
//  * Principal + full initial grant set are one atomic write (EF relationship fixup, single SaveChanges),
//    so a principal without its Platform.Support.Administer grant is structurally impossible.
//  * Concurrent bootstrap converges on exactly one principal: the DB uniqueness on IdentityId rejects the
//    loser's insert, which then re-reads live authority instead of falling through to a second candidate,
//    preventing double-genesis.
public sealed class PlatformSupportBootstrapService(
  IOptions<PlatformSupportBootstrapOptions> optionsAccessor,
  IIdentityRepository identityRepository,
  IAuthenticationAccountRepository accountRepository,
  IPlatformSupportPrincipalRepository principalRepository,
  IPlatformSupportAuthorityStateReadService authorityState,
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

    if (await authorityState.HasUsablePlatformAuthorityAsync(cancellationToken))
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
      // A unique-constraint violation means a concurrent host won the genesis race. Re-read live authority:
      // if it is now usable, converge on that single principal rather than trying another candidate.
      if (save.Error == IdentityAccessErrors.UniqueConstraintViolation &&
        await authorityState.HasUsablePlatformAuthorityAsync(cancellationToken))
      {
        return PlatformSupportBootstrapOutcome.AuthorityAlreadyUsable;
      }

      return PlatformSupportBootstrapOutcome.NoEligibleCandidate;
    }

    return PlatformSupportBootstrapOutcome.GenesisEstablished;
  }
}
