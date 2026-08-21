using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Positions;

// NOTHING HERE NAMES DATABASE TOPOLOGY OR ANOTHER TENANT'S DATA (ADR-023, ADR-025).
//
// Scope refusals — a company the caller may not reach — are answered by the Platform boundaries with their
// own generic errors and are never restated here, so the HR surface cannot be used to probe for the
// existence of identifiers it is not allowed to see. This mirrors `EmployeeErrors` and `DepartmentErrors`
// exactly.
//
// ---- THE FILE IS IN TWO HALVES, AND THE SPLIT IS MEANINGFUL.
//
// Above the Phase 2 banner are the refusals an AGGREGATE can decide alone — an invalid code, a band that is
// half-priced or out of order, a lifecycle transition that does not exist. Below it are the ones that need a
// repository lookup or application orchestration to reach. Phase 1 carries only ONE of those, and says why.
public static class PositionErrors
{
  // ---- POSITION IDENTITY.
  public static readonly Error InvalidCode =
    new("Position.InvalidCode", "The position code is invalid.");

  public static readonly Error InvalidTitle =
    new("Position.InvalidTitle", "The position title is invalid.");

  public static readonly Error InvalidActor =
    new("Position.InvalidActor", "A trusted lifecycle actor is required.");

  // ---- GRADE IDENTITY.
  //
  // SEPARATE CONSTANTS PER LADDER, not one shared `InvalidGradeCode`. `DEC-POS-0005` made the two grades
  // separate aggregates, and a refusal that cannot say WHICH ladder rejected the value would be answered
  // identically for two different mistakes.
  public static readonly Error InvalidJobGradeCode =
    new("Position.InvalidJobGradeCode", "The job grade code is invalid.");

  public static readonly Error InvalidJobGradeName =
    new("Position.InvalidJobGradeName", "The job grade name is invalid.");

  public static readonly Error InvalidSalaryGradeCode =
    new("Position.InvalidSalaryGradeCode", "The salary grade code is invalid.");

  public static readonly Error InvalidSalaryGradeName =
    new("Position.InvalidSalaryGradeName", "The salary grade name is invalid.");

  // ---- RANK ORDER (DEC-POS-0006).
  //
  // Authoritative data, not derived from the code. Rank UNIQUENESS is a database concern — a unique index
  // per company and ladder — so its refusal arrives with the handler that translates index violations, in
  // Phase 2, rather than as a constant here that nothing can raise.
  public static readonly Error InvalidRankOrder =
    new("Position.InvalidRankOrder", "The grade rank order must be a positive number.");

  // ---- THE SALARY BAND (DEC-POS-0016, DEC-POS-0027).
  //
  // THREE DISTINCT REFUSALS, because they are three distinct mistakes and a caller can act on each
  // differently. A half-filled band is a form the user did not finish; a negative amount is a typo; an
  // out-of-order band is a misunderstanding of which field is which. Collapsing them into one
  // `InvalidSalaryBand` would leave the API unable to say which.
  public static readonly Error SalaryBandIncomplete =
    new("Position.SalaryBandIncomplete",
      "A salary band requires all three amounts, or none. A partially specified band is not accepted.");

  public static readonly Error SalaryBandNegative =
    new("Position.SalaryBandNegative", "A salary band amount cannot be negative.");

  public static readonly Error SalaryBandOutOfOrder =
    new("Position.SalaryBandOutOfOrder",
      "A salary band requires minimum, midpoint and maximum in non-decreasing order.");

  // ---- GRADE REFERENCE.
  public static readonly Error InvalidGradeReference =
    new("Position.InvalidGradeReference", "The grade reference is invalid.");

  // ---- LIFECYCLE.
  public static readonly Error InvalidTransition =
    new("Position.InvalidTransition", "The lifecycle transition is invalid.");

  // ---- POSITION HISTORY.
  //
  // The append-only assignment log is never edited. A correction is another position change, never a
  // rewrite — exactly as for the branch and department histories.
  public static readonly Error InvalidPositionAssignment =
    new("Position.InvalidPositionAssignment", "The employee position assignment is invalid.");

  // ================================================================================================
  // THE ONE PHASE 1 REFUSAL THAT NEEDS A REPOSITORY TO REACH — AND WHY IT IS HERE ANYWAY
  // ================================================================================================
  //
  // `DEC-POS-0013` refuses deactivating a grade while `Active` dependents reference it. The check is a
  // repository lookup and the OPERATION ships in Phase 2, so nothing in Phase 1 can raise this.
  //
  // It is declared now because the ruling that binds this phase named it, and because the alternative —
  // inventing the constant in Phase 2 alongside the handler — is how a rule ends up worded by whoever
  // happens to implement it rather than by the decision that created it. The asymmetry with
  // `DepartmentErrors`, where Phase 1 deliberately carried NO Phase 2 constants, is the deliberate part:
  // FP-007 had no ruling pointing forward, and this phase does.
  public static readonly Error GradeHasActiveDependents =
    new("Position.GradeHasActiveDependents",
      "The grade cannot be deactivated while active positions or grades reference it.");
}
