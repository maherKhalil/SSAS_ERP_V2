using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.Payroll.Application.Abstractions;
using SSAS.Payroll.Application.Permissions;
using SSAS.Payroll.Application.Reads;
using SSAS.Payroll.Domain.Compensation;

namespace SSAS.Payroll.Application.Compensation;

// ==================================================================================================
// RECORDING A ONE-OFF PAY INSTRUCTION (T-110).
// ==================================================================================================
//
// ---- ONE COMMAND, AND IT APPENDS — THE SAME SHAPE AS COMPENSATION AND FOR A DIFFERENT REASON.
//
// Compensation has no update because `BR-PAY-0002` makes a change a new dated record. **A one-off has no
// update because it is an EVENT**: correcting the amount of a payment somebody is owed means withdrawing the
// instruction and writing another, and once a run has paid it there is nothing left to correct here — the
// correction is a payroll matter, not an edit to history.
//
// **There is deliberately no delete either.** An unconsumed instruction that should not be paid is a real
// need, and it is a withdrawal with a reason rather than a row disappearing — which is a separate command
// nobody has asked for yet. **Naming its absence so the next person adds it deliberately.**
//
// ---- IT TAKES `ManageCompensation`, NOT A NEW PERMISSION.
//
// Deciding that someone is paid an amount is the same authority whether it recurs or happens once. A second
// permission would let the two be granted apart, which is a distinction nobody has ruled and which
// `AC-SS-0005`'s reasoning warns against inventing.
public sealed record RecordOneOffPaymentCommand(
  Guid CompanyId,
  Guid EmployeeId,
  Guid PayrollPeriodId,
  Guid PayElementId,
  decimal Amount,
  string? Reason);

public sealed class RecordOneOffPaymentCommandHandler(
  IOneOffPaymentRepository oneOffPayments,
  IPayElementRepository elements,
  IPayrollScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result<Guid>> HandleAsync(
    RecordOneOffPaymentCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var authorized = await scope.AuthorizeAsync(
      PayrollPermissionNames.ManageCompensation, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    // ---- THE ELEMENT IS CHECKED HERE, WHEN THE INSTRUCTION IS WRITTEN.
    //
    // It supplies the line's KIND and its GL ACCOUNT, so an instruction naming an element that does not
    // exist could never produce a line. The calculator refuses such a run, but discovering it there means
    // finding out on the day payroll is calculated rather than on the day somebody typed it.
    var element = await elements.GetByIdAsync(command.PayElementId, cancellationToken);
    if (element is null || element.CompanyId != command.CompanyId)
    {
      return Result.Failure<Guid>(OneOffPaymentErrors.PayElementNotFound);
    }

    var payment = OneOffPayment.Create(
      command.CompanyId, command.EmployeeId, command.PayrollPeriodId, command.PayElementId,
      command.Amount, command.Reason);
    if (payment.IsFailure)
    {
      return Result.Failure<Guid>(payment.Error);
    }

    await oneOffPayments.AddAsync(payment.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saved.IsFailure ? Result.Failure<Guid>(saved.Error) : Result.Success(payment.Value.Id);
  }
}
