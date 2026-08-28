namespace SSAS.BuildingBlocks.SharedKernel;

// ==================================================================================================
// THE OVERTIME TIER VOCABULARY — **THE ONE PLACE THE TWO SIDES OF THIS CONTRACT AGREE** (T-131).
// ==================================================================================================
//
// An overtime tier is operator-entered text that is written in **Attendance** (on a record) and read in
// **Payroll** (on a pay element), and the two are matched to decide what somebody is paid. The modules
// cannot reference each other — `ADR-012` — so before T-131 each carried its own rule:
//
//   AttendanceRecord   Trim(t) => IsNullOrWhiteSpace(t) ? null : t.Trim()     written here
//   PayElement         IsNullOrWhiteSpace(t) ? null : t.Trim()                written again, identically
//   NEITHER SIDE case-folds
//   the match          quantities.TryGetValue(tier)                           ORDINAL, exact
//
// **The whitespace halves agree. The CASE halves do not exist at all**, and the match is ordinal — so a
// record tagged `"Night"` against an element tagged `"NIGHT"` does not match, **and the lookup returns
// `0m` on a miss with no error.** The employee is paid **no overtime for that tier, silently**, on a
// payslip that looks complete.
//
// **Both problems are the same problem: one rule, written twice** (`DEC-L-080`), at the point where money
// is computed. **That the two copies agree TODAY is not the reassurance it looks like** — they were written
// separately, so nothing was keeping them in step, and the half neither copy implemented is the half that
// costs somebody money.
//
// ---- THE RULE IS THE PRODUCT'S EXISTING ONE, NOT A NEW INVENTION.
//
// `LeaveTypeCode`, `WorkingCalendarName` and the login-identity partition all normalise operator-entered
// labels the same way: **trim, then `ToUpperInvariant`.** A tier is the same kind of thing — a short label
// two people type independently and expect to match — **so it follows the convention rather than growing a
// fourth one.**
//
// `ToUpperInvariant` rather than `ToUpper()`: the result is a MATCHING KEY, and a key that depends on the
// server's culture would match differently on different machines.
//
// ---- ⚠ WHAT THIS DOES NOT DO, STATED SO THE GUARANTEE IS NOT READ AS WIDER THAN IT IS.
//
// **It equates case and surrounding whitespace. It does not equate anything else.**
//
// The product is configured for **`ar` and `en`**. Arabic is caseless, so `ToUpperInvariant` is a no-op for
// an Arabic tier — **two Arabic labels that differ by alef form (`أ` `إ` `ا`), by tatweel, or by harakat
// remain DIFFERENT keys here.** So do two Latin labels differing by a typo, and so do composed versus
// decomposed forms of the same character.
//
// **That is deliberate and it is a floor, not an oversight.** Folding Arabic orthographic variants is a
// language decision with real semantic risk — it is not obviously right that `إجازة` and `اجازة` are the
// same tier — **and this task's remit was the half nobody chose, not the half somebody has to.**
//
// **What happens when a tier legitimately matches no element remains an open decision for the owner**
// (`OWNER-DECISIONS.md` §1). This type removes the accidental mismatches; it does not decide the policy for
// the real ones.
public static class OvertimeTierKey
{
  // ---- ONE LENGTH, CITED BY BOTH SIDES.
  //
  // `AttendanceRecord` and `PayElement` each declared their own copy with a comment on each saying the two
  // must agree. **A fact in two places goes stale in one of them**, and this is the label's own vocabulary,
  // so the length belongs with it.
  public const int MaximumLength = 32;

  // Returns null for absent-or-blank so callers get one representation of "no tier" rather than three
  // (`null`, `""`, `"   "`), which is the same collapse `PayElement` already did on its own.
  public static string? Normalize(string? tier) =>
    string.IsNullOrWhiteSpace(tier) ? null : tier.Trim().ToUpperInvariant();
}
