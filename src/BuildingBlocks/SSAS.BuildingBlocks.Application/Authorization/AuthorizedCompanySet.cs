using System.Collections.ObjectModel;

namespace SSAS.BuildingBlocks.Application.Authorization;

// ================================================================================================
// THE DATA SHAPE THREE MODULES WERE DUPLICATING — AND NOTHING MORE THAN THAT.
// ================================================================================================
//
// **PROMOTED 2026-08-24 under `ADR-027` decision 4.** `GlReadScope` wrote the trigger where it would be
// found:
//
//   > **THE TRIGGER, WRITTEN WHERE IT WILL BE FOUND: a THIRD consumer.** Two modules each carrying a guarded
//   > identifier list is duplication nobody trips over. Three is where the shapes start to drift, and drift
//   > in a scope type is a SECURITY DEFECT rather than an inconvenience.
//
// HR's `AuthorizedCompanyScope`, `GlReadScope`, and FP-012's `PayrollReadScope` were the three.
//
// ================================================================================================
// WHAT WAS PROMOTED, AND — MORE IMPORTANTLY — WHAT WAS NOT.
// ================================================================================================
//
// **PROMOTED: the value.** A materialized, never-empty, never-writeable set of authorized company
// identifiers. That is the shape all three had independently written, and the thing that would have drifted.
//
// **NOT PROMOTED: the scope types themselves.** `EmployeeReadScope`, `GlReadScope` and `PayrollReadScope`
// each remain a sealed, per-module type with a PRIVATE constructor, an INTERNAL factory, and exactly ONE
// caller — that module's own resolver. They now WRAP this set instead of re-declaring it.
//
// ---- WHY THAT BOUNDARY IS EXACTLY WHERE IT IS.
//
// **The unforgeability is the security property, and it must stay per-module.** What makes a scope
// trustworthy is not the list it holds; it is that holding one is PROOF that a specific module's resolver
// ran, checked that module's functional permission, and resolved that module's company access against live
// state — and that no other code path can manufacture one. A shared scope type would mean any module able to
// build one could hand it to any other module's read service, and the proof would become a shrug.
//
// So this type has a PUBLIC factory, deliberately. It is not a credential and does not pretend to be: it is
// a validated list. The credential is the wrapper, and the wrapper stays home.
//
// A reader tempted to "finish the job" by promoting the scope types too should read that paragraph twice.
// Consolidating them would remove the duplication and remove the security property with it.
//
// ---- WHAT THE TYPE STILL GUARANTEES.
//
// **Never empty.** An empty authorized set must REFUSE a read, not return an empty page — an empty page
// claims something about the data, a refusal claims something about the caller, and only the second is true.
// Enforcing it at construction makes `WHERE CompanyId IN ()` unrepresentable rather than merely guarded
// against, so the factory returns null and the resolver turns that into a refusal.
//
// **Never writeable.** An `IReadOnlyList<Guid>` handed a `Guid[]` casts straight back to `Guid[]`, so the
// read-only would be a suggestion: code holding a scope could add a company AFTER the authorization that
// produced it had passed. The `ReadOnlyCollection` copy is what makes it a fact.
public sealed class AuthorizedCompanySet
{
  private AuthorizedCompanySet(IReadOnlyList<Guid> companyIds) => CompanyIds = companyIds;

  public IReadOnlyList<Guid> CompanyIds { get; }

  public int Count => CompanyIds.Count;

  public bool Contains(Guid companyId) => CompanyIds.Contains(companyId);

  // Returns null for an empty or absent set rather than throwing, because "this caller may see nothing" is
  // an ordinary authorization answer that each resolver turns into its own module's refusal — not an
  // exceptional condition.
  public static AuthorizedCompanySet? Create(IReadOnlyList<Guid>? companyIds)
  {
    if (companyIds is null || companyIds.Count == 0)
    {
      return null;
    }

    return new AuthorizedCompanySet(new ReadOnlyCollection<Guid>([.. companyIds]));
  }
}
