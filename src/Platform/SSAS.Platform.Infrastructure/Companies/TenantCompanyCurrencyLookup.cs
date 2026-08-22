using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Platform.Infrastructure.Companies;

// THE PLATFORM SIDE OF THE CURRENCY SEAM (FP-008 Phase 4, DEC-POS-0015).
//
// A module asks "what currency is this company's money in"; Platform answers, because Platform owns the
// company. `SSAS.HR.*` cannot reference `SSAS.Platform.Domain` under `ADR-012`, so the contract lives in
// `SSAS.BuildingBlocks.Tenancy.Companies` and this implements it — the same arrangement
// `TenantCompanyAccessResolver` has, for the same reason.
//
// ---- IT RETURNS THE STRING, NOT THE VALUE OBJECT.
//
// `BaseCurrencyCode` stays here. Its ISO-4217 set, its `char(3)` column, its check constraint and the
// `DEC-CMP-0009` immutability rule are all Platform's, and none of them crosses. What crosses is three
// characters for a caller to render beside an amount it already has.
//
// ---- IT AUTHORIZES NOTHING, AND MUST NOT BE MISTAKEN FOR A SCOPE CHECK.
//
// Every caller reaching this has already proven it may see the company — a salary grade read resolves its
// own scope before it composes a representation. This answers a question about a company the caller can
// already name. A `null` therefore means the company genuinely is not in this tenant, which for a caller
// holding a scoped identifier is a dangling reference rather than an authorization outcome; the interface
// records why the two must not be collapsed.
internal sealed class TenantCompanyCurrencyLookup(ITenantDbContextFactory tenantContextFactory)
  : ITenantCompanyCurrencyLookup
{
  public async Task<string?> FindBaseCurrencyCodeAsync(
    Guid tenantId, Guid companyId, CancellationToken cancellationToken = default)
  {
    if (tenantId == Guid.Empty || companyId == Guid.Empty)
    {
      return null;
    }

    var context = await tenantContextFactory.CreateAsync(tenantId, cancellationToken);
    if (context.IsFailure)
    {
      // Tenant storage being unavailable is NOT "no such company". Answering null here would render an
      // amount with no currency beside it, which is precisely the unreadable state `DEC-POS-0015` accepted
      // no currency column in order to avoid — so the failure is raised rather than flattened.
      throw new InvalidOperationException(
        $"The tenant database for {tenantId} could not be opened to resolve a base currency: " +
        context.Error.Code);
    }

    await using var tenant = context.Value;

    // The tenant global query filter already restricts this to the routed tenant; TenantId is compared
    // explicitly as well so the predicate states the invariant it depends on rather than inheriting it —
    // the convention every scoped read in this codebase follows.
    //
    // NO STATUS PREDICATE, deliberately. An archived company's salary grades remain readable, and their
    // amounts are still denominated in the currency that company was created with; filtering on Active here
    // would blank the currency on exactly the historical records that most need it.
    return await tenant.Companies
      .AsNoTracking()
      .Where(company => company.Id == companyId && company.TenantId == tenantId)
      .Select(company => company.BaseCurrencyCode.Value)
      .SingleOrDefaultAsync(cancellationToken);
  }
}
