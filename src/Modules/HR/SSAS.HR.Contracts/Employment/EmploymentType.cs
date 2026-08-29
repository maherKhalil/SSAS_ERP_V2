namespace SSAS.HR.Contracts.Employment;

// ==================================================================================================
// HOW AN EMPLOYEE IS ENGAGED (T-153, owner ruling 2026-08-29).
// ==================================================================================================
//
// **The owner's words: full time is a monthly salary, part time is per day or per hour, contract is a
// one-time payment.** Three engagements, each admitting a different set of payment shapes.
//
// ---- ⚠ THIS IS NOT A PAY FIELD, AND `DEC-POS-0023` IS NOT ENGAGED.
//
// `DEC-POS-0023` bars a salary, wage, rate or pay COLUMN from `Employee` — *"what an individual is paid is
// Payroll"*. **The test that settles it: does knowing an employee's type tell you what they are PAID?**
//
// **It does not. It tells you HOW they are paid, not how much.** A part-timer may be daily or hourly at any
// rate; two part-timers of the same type earn different amounts, and the type says nothing about either.
// **`DEC-POS-0023` bars holding a VALUE; this constrains which value-SHAPES are legal, and constraining a
// set is not holding a member of it.**
//
// It is HR's fact for the same reason `EmploymentDate` is: it describes the engagement, and HR owns
// engagements. **Recorded here because a future reader who finds this without the argument will assume
// `DEC-POS-0023` was missed** — it has been invoked correctly before.
//
// ---- AND IT RESOLVES T-107'S OPEN TENSION RATHER THAN REOPENING IT.
//
// T-107 established that CONTRACT is **not** a salary type: `EmployeeCompensation` has no `EffectiveToUtc`,
// so a one-time payment expressed as a salary type would recur every period forever. The owner was told
// they had ruled four things and the model could hold three.
//
// **They have now answered at the right level.** Contract is an EMPLOYMENT type whose payment mechanism is
// the one-off instruction — so the three-way `SalaryType` split was correct, and this names the fourth
// thing as what it always was. **A contract employee holds NO compensation record at all**, which is the
// payee T-110 fixed after they were silently dropped from every run.
// ---- IT LIVES IN CONTRACTS, NOT IN THE DOMAIN, AND THAT IS THE CROSSING DECISION.
//
// Payroll must know an employee's type to refuse an illegal pairing, and `DEC-PAY-0017` pins
// `EmploymentRecord` to four fields — **widening it would let every future payroll feature read the
// value with no call site changing for anyone to review.** So the crossing is a purpose-named read,
// and this enum is its return type.
//
// **Defined ONCE here rather than mirrored in the domain.** `SSAS.HR.Contracts` depends on nothing, so
// `SSAS.HR.Domain` referencing it creates no cycle, and the only guard on HR's domain references bars
// `SSAS.Platform` — this is intra-HR. **Two enums kept in step by hand is `DEC-L-080`; one is not.**
public enum EmploymentType
{
  // Monthly salary. `SalaryType.Monthly` is `default`, so this being `default` keeps every employee written
  // before T-153 on exactly the arrangement they already had — the same construction T-107 used when
  // `SalaryType` arrived.
  FullTime,

  // Paid per day or per hour. **Which of the two is a compensation decision, not an employment one** — the
  // engagement says the employee is part time; the rate shape says how that is priced.
  PartTime,

  // Paid by one-off instruction and holding no compensation record. **Not "unpaid" and not "no salary type"
  // — a distinct engagement whose payment mechanism is `OneOffPayment`.**
  Contract
}
