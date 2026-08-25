using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.Payroll.Application.Abstractions;
using SSAS.Payroll.Application.Permissions;
using SSAS.Payroll.Application.Reads;
using SSAS.Payroll.Domain.Compensation;

namespace SSAS.Payroll.Application.Compensation;

// RECORDING WHAT SOMEONE IS PAID (REQ-PAY-0001, OD-PAY-0003).
//
// There is one command and it APPENDS. There is no update command and no delete command, because
// `BR-PAY-0002` makes a change a new dated record — and the absence of the handler is the rule enforced by
// the shape of the module rather than by everyone remembering it.
public sealed record RecordCompensationCommand(
  Guid CompanyId,
  Guid EmployeeId,
  DateTimeOffset EffectiveFromUtc,
  decimal BaseAmount,
  IReadOnlyList<(Guid PayElementId, decimal? RateOrAmount)> Assignments,
  // ---- THE BAND OBSERVATION IS SUPPLIED, NOT FETCHED (OD-PAY-0004, DEC-PAY-0017).
  //
  // Payroll does not reach into HR for the employee's salary grade. The roster contract carries employment
  // dates and nothing else — deliberately — so a grade-band comparison is data the CALLER provides if it has
  // it, and its absence simply means no observation was recorded.
  //
  // This keeps `OD-PAY-0004`'s ruling honest in both directions: the band is informational, so a missing
  // band must not block recording compensation. A handler that fetched the band would have made it a
  // prerequisite, which is the "validated" reading the ruling refused.
  bool WasOutsideGradeBand,
  string? GradeBandObservation);

public sealed class RecordCompensationCommandHandler(
  IEmployeeCompensationRepository compensation,
  IPayrollScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result<Guid>> HandleAsync(
    RecordCompensationCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var authorized = await scope.AuthorizeAsync(
      PayrollPermissionNames.ManageCompensation, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var record = EmployeeCompensation.Create(
      command.CompanyId, command.EmployeeId, command.EffectiveFromUtc, command.BaseAmount, command.Assignments);
    if (record.IsFailure)
    {
      return Result.Failure<Guid>(record.Error);
    }

    // Recorded, never enforced. An out-of-band amount is a real business event — retention, acting-up, a
    // legacy arrangement — and `OD-PAY-0004` ruled it is warned about rather than refused.
    record.Value.RecordGradeBandObservation(command.WasOutsideGradeBand, command.GradeBandObservation);

    await compensation.AddAsync(record.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saved.IsFailure ? Result.Failure<Guid>(saved.Error) : Result.Success(record.Value.Id);
  }
}
