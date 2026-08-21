namespace SSAS.BuildingBlocks.Tenancy.Companies;

// THE ONE FACT A MODULE MAY LEARN ABOUT A COMPANY'S CURRENCY (FP-008 Phase 4, DEC-POS-0015).
//
// ==================================================================================================
// WHY THIS EXISTS AT ALL, AND WHY IT IS THIS NARROW.
// ==================================================================================================
//
// `DEC-POS-0015` ruled that `tenant.SalaryGrades` carries NO currency column: amounts are denominated in the
// owning Company's `BaseCurrencyCode`, which `DEC-CMP-0009` makes required at creation and immutable. The
// read representation still has to say which currency an amount is in, because an amount without one is
// unreadable — so the API has to obtain a fact that lives on a Platform-owned aggregate.
//
// `BaseCurrencyCode` is a value object in `SSAS.Platform.Domain`. `SSAS.HR.*` references only
// `SSAS.BuildingBlocks.*`, compiler-enforced under `ADR-012` and asserted by an architecture guard, so HR
// cannot reach it. This interface is the module-facing seam that closes that gap, and it lives here for the
// same reason `ITenantCompanyAccessResolver` does: a MODULE calls it and PLATFORM implements it, and
// `SSAS.Platform.*` is itself a module.
//
// ---- IT CARRIES AN OPAQUE STRING, AND THAT IS THE POINT (ruled 2026-08-21).
//
// The value object does NOT move. Validation, the ISO-4217 list, the `char(3)` column, the check constraint
// and the immutability rule all stay Platform-side; what crosses is three characters a caller may render.
//
// The alternative considered and refused was promoting `BaseCurrencyCode` into `SSAS.BuildingBlocks.Domain`.
// That is the right answer for a different problem — a company needing grade ladders in more than one
// currency — and `DEC-POS-0015` named it as an ADR-level change to be made deliberately rather than as a
// side effect of needing one display field. **That revisit condition is unaffected by this interface**: this
// seam reads a company's single base currency and would be useless for a multi-currency ladder, so it cannot
// quietly become the answer to the question the ADR reserved.
//
// ---- WHY NOT `CompanyAccessSummary`.
//
// That record is an AUTHORIZATION-shaped DTO — which companies a caller may act within — consumed by scope
// resolvers across the product. Widening it with a display field would couple two concerns and would make
// every authorization path carry data it has no use for.
public interface ITenantCompanyCurrencyLookup
{
  // The company's ISO-4217 base currency code, or NULL when no such company exists in this tenant.
  //
  // ---- NULL MEANS "NO SUCH COMPANY IN THIS TENANT", AND NOTHING ELSE.
  //
  // It is not an authorization answer: this lookup decides nothing about what the caller may see, and every
  // caller reaching it has already passed the scope check that produced the identifier. A null for a company
  // the caller could already read is therefore a genuine inconsistency — a dangling reference — and the
  // caller should treat it as a server fault rather than translating it into the not-found answer that
  // scoped absence produces. The two are kept distinct deliberately: collapsing them would turn a data
  // integrity problem into a silent 404 that looks like ordinary authorization.
  Task<string?> FindBaseCurrencyCodeAsync(
    Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
}
