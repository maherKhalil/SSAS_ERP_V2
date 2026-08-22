namespace SSAS.HR.Domain.Positions;

// THE LIFECYCLE STATES OF FP-008's THREE AGGREGATES (DEC-POS-0011, FP-008 lifecycle-model).
//
// TWO STATES EACH, AND `Inactive` IS REVERSIBLE. Employee has a third — `Terminated` — which is genuinely
// terminal. Retiring a job or closing a pay band is an organizational decision organizations reverse, so
// these are not modelled alike merely because they share a word with a state that is.
//
// THERE IS NO `Deleted` ANYWHERE. Nothing here is ever physically removed (`BR-PLT-0003`,
// `BRULE-POS-0012`), so a lifecycle state standing for deletion would describe something that cannot happen.
//
// ---- THREE ENUMS RATHER THAN ONE SHARED ONE, AND THAT IS DELIBERATE.
//
// The three values are identical today. They are kept as separate types because each is PERSISTED AS A
// STRING IN ITS OWN TABLE UNDER ITS OWN CHECK CONSTRAINT, and because the aggregates are independently
// evolvable: if a salary grade ever gains a `Draft` state for a band awaiting approval, a shared enum would
// hand that state to Position and JobGrade as well, where it would mean nothing. Sharing the type would
// couple three lifecycles that the package deliberately keeps separate.
//
// They live in one file because they are one decision, and splitting them across three would suggest three.

public enum PositionStatus
{
  // The state of every created position. A position created inactive would be a job nobody can fill and
  // nobody asked for.
  Active,

  // Retained, readable, and still the position of every employee already holding it — `OD-POS-005` ruled
  // "one ACTIVE position" to qualify the ASSIGNMENT, so incumbents keep it and `BR-HR-0006` stays satisfied
  // for them. What an inactive position cannot do is receive a NEW assignment (`BRULE-POS-0013`).
  Inactive
}

public enum JobGradeStatus
{
  Active,

  // A grade with `Active` dependents cannot reach this state at all: `DEC-POS-0013` refuses the transition
  // while positions still reference it, because an active position pointing at an inactive grade is an
  // incoherent tree. That refusal needs a repository lookup and belongs to Phase 2; the ERROR for it exists
  // now (`PositionErrors.GradeHasActiveDependents`) so the rule is named where it is enforced, not invented
  // later.
  Inactive
}

public enum SalaryGradeStatus
{
  Active,

  // As `JobGradeStatus.Inactive`, with job grades as the dependents rather than positions.
  Inactive
}
