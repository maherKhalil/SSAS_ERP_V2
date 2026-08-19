using Microsoft.AspNetCore.Http;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Domain.Companies;

namespace SSAS.Platform.Infrastructure.RequestContext;

// THE `X-Company-Id` REQUEST HEADER, AND NOTHING ELSE (FP-006C1, ADR-025 decisions 2 and 4).
//
// THIS IS THE ONLY PLACE THE HEADER NAME APPEARS below the web layer. Everything downstream consumes
// ICompanySelection, so replacing this with a durable session selection (ADR-025 decision 11) touches no
// resolver, no write boundary and no handler.
//
// IT CARRIES INTENT, NEVER AUTHORITY. The value here has not been checked to exist, to belong to the trusted
// tenant, to be Active, or to be reachable by the caller. ICompanyContextResolver does all four against live
// state, and refuses identically for every one of those failures.
//
// A MALFORMED HEADER IS A SYNTAX FAILURE, and is reported as one. That is safe to distinguish because it
// says nothing about any company: the caller sent something that is not an identifier at all. Every
// authorization outcome collapses into one generic refusal further down, so a well-formed identifier reveals
// nothing about whether it names a real company.
//
// MORE THAN ONE HEADER VALUE IS ALSO MALFORMED. Taking the first would let a caller smuggle a second
// selection past anything that logged or inspected only one of them.
public sealed class RequestedCompanySelection(IHttpContextAccessor httpContextAccessor) : ICompanySelection
{
  public const string HeaderName = "X-Company-Id";

  public Result<Guid?> Requested
  {
    get
    {
      var context = httpContextAccessor.HttpContext;
      if (context is null || !context.Request.Headers.TryGetValue(HeaderName, out var values))
      {
        // No company requested. Legitimate: tenant-global and branch-only work needs none, and the write
        // boundary is what turns it into a refusal for company-owned data.
        return Result.Success<Guid?>(null);
      }

      if (values.Count != 1)
      {
        return Result.Failure<Guid?>(CompanyAccessErrors.InvalidSelectionFormat);
      }

      var raw = values[0];
      if (string.IsNullOrWhiteSpace(raw))
      {
        return Result.Failure<Guid?>(CompanyAccessErrors.InvalidSelectionFormat);
      }

      // Exact-format parsing only. Guid.TryParse would accept braces, parentheses and hyphenless forms, so
      // one company would have several accepted spellings — and anything comparing the raw header text
      // (a log, a cache key, a future audit record) would see them as different selections.
      return Guid.TryParseExact(raw.Trim(), "D", out var companyId) && companyId != Guid.Empty
        ? Result.Success<Guid?>(companyId)
        : Result.Failure<Guid?>(CompanyAccessErrors.InvalidSelectionFormat);
    }
  }
}
