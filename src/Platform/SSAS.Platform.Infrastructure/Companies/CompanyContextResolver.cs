using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Domain.Companies;

namespace SSAS.Platform.Infrastructure.Companies;

// TURNS CALLER INTENT INTO A TRUSTED COMPANY, OR REFUSES (FP-006C1, ADR-025 decision 3).
//
// THE FIVE STEPS RUN IN ORDER AND FAIL CLOSED. Nothing is cached: not from login, and not from an earlier
// call within the same request. Company access is revocable and a company is deactivatable inside a
// request's lifetime, so "it was valid a moment ago" is not an answer this is allowed to give.
//
// THE SELECTION AND THE SESSION ARE BOTH OPTIONAL, AND THEIR ABSENCE IS A REFUSAL. Background, maintenance
// and migration compositions have neither a request header nor a signed-in user. Registering them as
// required would make the persistence container un-buildable outside a request — and defaulting to "allow"
// when they are missing would be far worse, so absence simply means no company context and every
// company-owned write is refused.
internal sealed class CompanyContextResolver(
  ICurrentTenant currentTenant,
  ITenantCompanyAccessResolver accessResolver,
  ICompanySelection? selection = null,
  ICurrentAuthenticationSession? currentSession = null) : ICompanyContextResolver
{
  public async Task<Result<Guid>> ResolveTrustedCompanyAsync(CancellationToken cancellationToken = default)
  {
    // ---- 1. A TRUSTED TENANT MUST EXIST. A company is only meaningful inside one, and the tenant is never
    // inferred from the company: that direction would let a caller pick its own tenant by picking a company.
    if (currentTenant.TenantId is not { } tenantId || tenantId == Guid.Empty)
    {
      return Result.Failure<Guid>(CompanyAccessErrors.ContextRequired);
    }

    // The acting user comes from the durable session, never from the request. Without one there is nobody
    // to authorize, so there is no company context.
    if (currentSession?.Value is not { } session || session.TenantId != tenantId || session.TenantUserId <= 0)
    {
      return Result.Failure<Guid>(CompanyAccessErrors.ContextRequired);
    }

    if (selection is null)
    {
      return Result.Failure<Guid>(CompanyAccessErrors.ContextRequired);
    }

    // ---- 2. THE REQUESTED IDENTIFIER MUST BE AN IDENTIFIER. A syntax failure is distinguishable because
    // it discloses nothing about any company; every authorization outcome below collapses into one generic
    // refusal.
    var requested = selection.Requested;
    if (requested.IsFailure)
    {
      return Result.Failure<Guid>(requested.Error);
    }

    if (requested.Value is not { } companyId || companyId == Guid.Empty)
    {
      return Result.Failure<Guid>(CompanyAccessErrors.SelectionRequired);
    }

    // ---- 3, 4 AND 5. EXISTS, BELONGS TO THIS TENANT, IS ACTIVE, AND IS REACHABLE BY THIS USER — all live,
    // all through the one resolver, and all answered with the SAME error so the identifier cannot be probed
    // for existence.
    var authorized = await accessResolver.AuthorizeCompanyAsync(
      tenantId, session.TenantUserId, companyId, cancellationToken);

    return authorized.IsFailure
      ? Result.Failure<Guid>(authorized.Error)
      : Result.Success(companyId);
  }
}
