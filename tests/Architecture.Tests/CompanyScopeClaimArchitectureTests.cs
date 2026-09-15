using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// COMPANY SCOPE IS NEVER A TOKEN CLAIM, AND NEVER A PROPERTY OF THE CALLER (ADR-025 decision 4).
// ==================================================================================================
//
// `ICompanySelection` states the rule where the legitimate mechanism lives:
//
//   WHAT MUST NEVER IMPLEMENT THIS: a JWT `company_id` claim or `ICurrentUser.CompanyId`. A claim would be
//   a client-presentable assertion of scope that survives revocation until the token expired; `ADR-025`
//   decision 4 makes that prohibition binding rather than advisory.
//
// Until item 164 that prohibition was held by prose alone. `ICurrentUser.CompanyId` existed, read the
// forbidden claim, and was consumed by nothing -- so it was prevented from doing harm by nobody having
// written the code, which is not a guard.
//
// ---- ⚠ THE CONTROL IS THE POINT OF THE SECOND TEST.
//
// "A member is absent" passes just as well when the reflection is pointed at the wrong type, when the
// assembly fails to load the member list, or when the name is misspelt. **`ICurrentCompany.CompanyId` is
// the LEGITIMATE member and must come back PRESENT**, from the same reflection, in the same run. Without
// it this file would assert an absence it could not distinguish from looking in the wrong place.
//
// The two read almost identically at a grep -- same member name, same type, different interface and
// different namespace -- which is exactly why the deletion needed a scoped edit and why this needs a
// control.
public sealed class CompanyScopeClaimArchitectureTests
{
  [Fact]
  public void ICurrentUser_declares_no_company_scope_member()
  {
    Assert.DoesNotContain(
      typeof(ICurrentUser).GetProperties(),
      property => property.Name.Contains("Company", StringComparison.Ordinal));
  }

  // ---- THE CONTROL: the legitimate member, found by the same means.
  [Fact]
  public void ICurrentCompany_still_declares_the_legitimate_company_member()
  {
    Assert.Contains(
      typeof(ICurrentCompany).GetProperties(),
      property => property.Name == nameof(ICurrentCompany.CompanyId));
  }
}
