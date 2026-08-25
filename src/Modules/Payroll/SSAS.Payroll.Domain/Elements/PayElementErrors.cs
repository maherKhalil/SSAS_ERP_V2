using SSAS.BuildingBlocks.Domain;

namespace SSAS.Payroll.Domain.Elements;

// PAY ELEMENT REFUSALS. Named rather than numbered, per the transport rules this module inherits: a client
// branches on a stable name, and a message is for a human.
public static class PayElementErrors
{
  public static readonly Error CompanyRequired = new(
    "Payroll.PayElementCompanyRequired",
    "A pay element must belong to a company.");

  public static readonly Error InvalidCode = new(
    "Payroll.PayElementCodeInvalid",
    "A pay element code is required, must be at most 64 characters, and cannot contain control characters.");

  public static readonly Error InvalidName = new(
    "Payroll.PayElementNameInvalid",
    "A pay element name is required and must be at most 256 characters.");

  public static readonly Error DuplicateCode = new(
    "Payroll.PayElementCodeConflict",
    "A pay element with this code already exists in this company.");

  public static readonly Error NotFound = new(
    "Payroll.PayElementNotFound",
    "The pay element does not exist.");

  // Every amount in this module is positive; `Kind` decides whether it earns or deducts. A negative value is
  // a caller who has misunderstood the model, not a smaller number.
  public static readonly Error NegativeAmount = new(
    "Payroll.PayElementAmountNegative",
    "A pay element amount or rate cannot be negative; whether it earns or deducts is decided by its kind.");

  public static readonly Error InvalidCalculationOrder = new(
    "Payroll.PayElementCalculationOrderInvalid",
    "A pay element calculation order cannot be negative.");

  public static readonly Error AccountRequired = new(
    "Payroll.PayElementAccountRequired",
    "A ledger account is required to map a pay element.");

  public static readonly Error CodeIsImmutable = new(
    "Payroll.PayElementCodeImmutable",
    "A pay element's code cannot be changed after it is created.");

  // ---- WHY THIS NAMES THE ELEMENT (OD-PAY-0012).
  //
  // The ruling put the mapping check at APPROVAL, and a refusal that says only "a pay element is unmapped"
  // makes the user hunt through the whole element list. `AccountErrors.Inactive` set this standard for GL:
  // naming the thing is the difference between a user fixing something and a user filing a ticket.
  public static Error Unmapped(string elementCode) => new(
    "Payroll.PayElementUnmapped",
    $"Pay element '{elementCode}' has no ledger account mapping, so this run cannot be approved.");

  public static Error Inactive(string elementCode) => new(
    "Payroll.PayElementInactive",
    $"Pay element '{elementCode}' is inactive.");
}
