using SSAS.BuildingBlocks.SharedKernel;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.Payroll.Application.Abstractions;
using SSAS.Payroll.Application.Permissions;
using SSAS.Payroll.Application.Reads;
using SSAS.Payroll.Domain.Elements;

namespace SSAS.Payroll.Application.Elements;

public sealed record CreatePayElementCommand(
  Guid CompanyId,
  string? Code,
  string? Name,
  PayElementKind Kind,
  PayElementBehaviour Behaviour,
  decimal DefaultRateOrAmount,
  int CalculationOrder,
  Guid? GlAccountId);

public sealed record UpdatePayElementCommand(
  Guid PayElementId, string? Name, decimal DefaultRateOrAmount, int CalculationOrder, Guid? GlAccountId);

public sealed record SetPayElementActivationCommand(Guid PayElementId, bool IsActive);

public sealed class CreatePayElementCommandHandler(
  IPayElementRepository elements,
  IPayrollScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result<Guid>> HandleAsync(
    CreatePayElementCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var authorized = await scope.AuthorizeAsync(
      PayrollPermissionNames.ManageElements, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var element = PayElement.Create(
      command.CompanyId, command.Code, command.Name, command.Kind, command.Behaviour,
      command.DefaultRateOrAmount, command.CalculationOrder);
    if (element.IsFailure)
    {
      return Result.Failure<Guid>(element.Error);
    }

    // Company-scoped uniqueness (`OD-PAY-0005`). The database index is the authority; this is the courteous
    // answer, and the index is what makes a race lose rather than duplicate.
    if (await elements.CodeExistsAsync(command.CompanyId, element.Value.Code.NormalizedValue, cancellationToken))
    {
      return Result.Failure<Guid>(PayElementErrors.DuplicateCode);
    }

    if (command.GlAccountId is { } accountId)
    {
      var mapped = element.Value.MapToAccount(accountId);
      if (mapped.IsFailure)
      {
        return Result.Failure<Guid>(mapped.Error);
      }
    }

    await elements.AddAsync(element.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      // ---- THE PAY-ELEMENT CODE RACE (T-178).
      //
      // `IPayElementRepository.CodeExistsAsync` is a read, so two callers can pass it with the same value and both reach this
      // save. **The unique index on `(TenantId, CompanyId, NormalizedCode)` decides it at commit**, and the loser reached `PayrollApiErrorMapper` with
      // an unmapped `Persistence.UniqueConstraint` — answered 500 for a plain business conflict, while
      // `PayElementErrors.DuplicateCode` sat mapped to 409 and unreturned on this path.
      //
      // **The race and the pre-check produce an IDENTICAL caller-visible condition**, so one code serves
      // both honestly, and **retrying the identical request fails again** — the caller must change the
      // input rather than repeat it.
      //
      // ⚠ **EVERY INDEX THIS SAVE CAN REACH MEANS THE SAME THING TO THE CALLER, WHICH IS THE ACTUAL TEST.**
      // This writes a `PayElement` and nothing else; the assignment index belongs to compensation.
      if (saved.Error.Code == PersistenceErrorCodes.UniqueConstraint)
      {
        return Result.Failure<Guid>(PayElementErrors.DuplicateCode);
      }

      return Result.Failure<Guid>(saved.Error);
    }

    return Result.Success(element.Value.Id);
  }
}

public sealed class UpdatePayElementCommandHandler(
  IPayElementRepository elements,
  IPayrollScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result> HandleAsync(
    UpdatePayElementCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var element = await elements.GetByIdAsync(command.PayElementId, cancellationToken);
    if (element is null)
    {
      return Result.Failure(PayElementErrors.NotFound);
    }

    // Authorized against the element's OWN company, read from the entity rather than taken from the
    // request. A caller who could name the company could authorize themselves against one they hold and
    // then edit an element belonging to one they do not.
    var authorized = await scope.AuthorizeAsync(
      PayrollPermissionNames.ManageElements, element.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    var updated = element.Update(command.Name, command.DefaultRateOrAmount, command.CalculationOrder);
    if (updated.IsFailure)
    {
      return updated;
    }

    // Kind and Behaviour are absent from Update by design — changing either would redefine what past runs
    // computed while leaving their stored lines untouched, so the record and its explanation would disagree.
    if (command.GlAccountId is { } accountId)
    {
      var mapped = element.MapToAccount(accountId);
      if (mapped.IsFailure)
      {
        return mapped;
      }
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}

public sealed class SetPayElementActivationCommandHandler(
  IPayElementRepository elements,
  IPayrollScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result> HandleAsync(
    SetPayElementActivationCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var element = await elements.GetByIdAsync(command.PayElementId, cancellationToken);
    if (element is null)
    {
      return Result.Failure(PayElementErrors.NotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      PayrollPermissionNames.ManageElements, element.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    // Idempotent, following `Account`: deactivating an inactive element is the state the caller asked for.
    // Deactivation never removes an element, because past run lines reference it and history must stay
    // reconstructable — the calculator simply stops selecting it.
    if (command.IsActive)
    {
      element.Activate();
    }
    else
    {
      element.Deactivate();
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}
