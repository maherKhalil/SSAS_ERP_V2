using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.API.Transport;

namespace SSAS.Platform.API.IdentityAccess;

// ==================================================================================================
// THE IDENTITY/ACCESS SITE — AND THE FIVE CODES IT ANSWERED WRONGLY UNTIL T-093.
// ==================================================================================================
//
// It answers for the roles read AND, since T-091, for the two tenant-user lifecycle routes.
//
// ---- WHAT WAS WRONG, AND HOW IT SURVIVED.
//
// Three arms, and a default of `request.invalid`. **So a tenant user that does not exist answered
// `400 request.invalid`** — the caller told to fix a request that was fine — and an invalid lifecycle
// transition answered 400 where every module in the product answers 409.
//
// **Nothing went red because `ModuleErrorMappingArchitectureTests` registered seven sites, all of them
// module mappers.** T-078 and T-079 built a per-site inventory and stopped at the module boundary; T-091
// then mounted routes onto the one mapper outside it. The register covers Platform as of T-093.
//
// ---- THE DEFAULT IS NOW A SERVER ERROR, WHICH IS THE CONVENTION EVERY OTHER SITE STATES.
//
// It was `RequestInvalid`. `EmployeeApiErrorMapper` says why that is backwards, in its own header:
// *"an unmapped code means this table is out of date. Answering 400 would blame the caller for the gap
// and hide it; a 500 is visible and gets fixed."*
//
// **The flip came AFTER the real codes were mapped, deliberately** — flipping first would have turned
// every still-unmapped code into a 500, and some of those are legitimate caller errors that were
// accidentally right under the old default.
public static class IdentityAccessApiErrorMapper
{
  // ---- 404, AND THE CODE IS DELIBERATELY NOT RESOURCE-SPECIFIC.
  //
  // `Company.NotFound` becomes `company.not_found` because that mapper answers for one resource. **This
  // one answers for roles AND tenant users**, and `Common.NotFound` is the single code both raise — so a
  // `tenant_user.not_found` here would be a lie on the roles route and vice versa.
  public static readonly ApiError NotFound = new(404, "platform.not_found");

  // 409, matching `company.transition_invalid` and `employee.transition_invalid` — the shape every module
  // already answers 409 for.
  public static readonly ApiError TransitionInvalid = new(409, "platform.transition_invalid");

  // 409. `Persistence.UniqueConstraint` reaches here when a uniqueness rule the database owns refuses the
  // write. Company folds it into `company.code_conflict` because it has exactly one such rule; this site
  // has several, so the code names the condition rather than guessing which rule fired.
  public static readonly ApiError UniqueConstraint = new(409, "platform.unique_conflict");

  // ---- T-092. ONE CODE FOR BOTH COLLISION DIRECTIONS, AND THE MESSAGE CARRIES WHICH.
  //
  // The two `Error` values stay separate so the DESCRIPTION names the repair; the wire code is shared
  // because a client branching on "which side collided" would be making a decision only a human can act
  // on. Same reasoning `company.code_conflict` uses for folding a unique violation into itself.
  public static readonly ApiError LinkConflict = new(409, "platform.employee_link_conflict");

  public static readonly ApiError EmploymentEnded = new(409, "platform.employment_ended");

  // ⚠ THE DOMAIN MESSAGE IS ATTACHED HERE BECAUSE THIS IS THE LAST PLACE IT EXISTS (T-261).
  //
  // Ninety-six call sites hand an already-mapped `ApiError` straight to `ApiProblems.Problem` and never
  // see the original `Error`. Attaching the message to the result is one edit per mapper; passing it
  // alongside would have been ninety-six.
  //
  // `ApiError.ShowsDetail` decides whether it reaches the caller: an authorization refusal (401/403)
  // drops it unless that code opted in, because `branch.scope_denied` has nine different messages behind
  // it and showing them would separate a branch that does not exist from one that is forbidden.
  public static ApiError Map(Error error) =>
    MapCore(error).Explaining(error.Message);

  private static ApiError MapCore(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);
    return error.Code switch
    {
      "Pagination.Invalid" => ProblemResults.RequestInvalid,

      // ---- 404. THE DEFECT T-091 SHIPPED: this fell through and answered 400.
      "Common.NotFound" => NotFound,

      // ---- 409. A lifecycle transition refused from the current status — deactivating an already
      // deactivated user, reactivating an active one. Not caller input: the request was well formed and
      // the state was not what it assumed.
      "TenantUser.InvalidTransition" => TransitionInvalid,

      // ---- 409.
      "Persistence.UniqueConstraint" => UniqueConstraint,

      // ---- T-092's LINK COLLISIONS. BOTH 409, AND BOTH DISTINGUISHABLE FROM EACH OTHER.
      //
      // `ADR-030` Decision 3 allows one live link each way and the unique indexes enforce it. These say
      // WHICH way a collision went — the user is spoken for, or the employee is — because the two need
      // different repairs and a single conflict code would make an administrator guess.
      //
      // **Distinguishing them is safe here for the same reason the handler distinguishes `Unknown` from
      // `Ended`:** the caller is a tenant administrator acting on a user and an employee they named and can
      // already read, so neither answer discloses anything they do not have.
      "UserEmployeeLink.TenantUserAlreadyLinked" => LinkConflict,
      "UserEmployeeLink.EmployeeAlreadyLinked" => LinkConflict,

      // 409 rather than 400: the request is well formed and the STATE refuses it. A terminated employee is
      // a fact about the subject, not a mistake in what was sent.
      "UserEmployeeLink.EmploymentEnded" => EmploymentEnded,

      // 404. Nothing to remove — and deliberately not a silent success, so a typo in the tenant user id
      // cannot look like a completed correction.
      "UserEmployeeLink.NotFound" => NotFound,

      // The factory's own refusal: a link needs a tenant, a user and an employee. Caller input.
      "UserEmployeeLink.Invalid" => ProblemResults.RequestInvalid,
      "Persistence.ConcurrencyConflict" => ProblemResults.ConcurrencyConflict,

      // ---- 403, BOTH OF THEM.
      //
      // `Tenant.Unauthorized` comes from `ApplicationExecutionContext.GetTenantActor`, which EVERY
      // tenant-plane handler funnels through — it was unmapped at all four Platform sites, which is the
      // finding T-093 actually turned up. `Authorization.Unauthorized` is its neighbour.
      //
      // `ProblemResults.Forbidden` is this site's spelling and is an ALIAS of `ApiErrors.Forbidden`, not
      // a second definition — Platform and HR cannot drift to different codes for the same condition.
      "Authorization.Unauthorized" => ProblemResults.Forbidden,
      "Tenant.Unauthorized" => ProblemResults.Forbidden,

      // ---- EXPLICIT, THOUGH IT NOW MATCHES THE DEFAULT (T-080's precedent).
      //
      // An arm that agrees with the default is a decision; its absence is an accident, and the two are
      // indistinguishable from the wire. Do not delete this as redundant.
      "Persistence.WriteFailure" => ProblemResults.WriteFailure,

      // ---- THE DEFAULT IS A SERVER ERROR, AND THAT IS THE POINT.
      //
      // It was `RequestInvalid` until T-093. An unmapped code means this table is out of date: a 400 would
      // blame the caller for the gap and hide it; a 500 is visible and gets fixed. Nothing is guessed from
      // the code's shape.
      //
      // **Flipped AFTER the real codes were mapped, deliberately.** Flipping first would have turned every
      // still-unmapped code into a 500, including ones that are legitimate caller errors and were
      // accidentally right under the old default.
      _ => ProblemResults.WriteFailure
    };
  }
}
