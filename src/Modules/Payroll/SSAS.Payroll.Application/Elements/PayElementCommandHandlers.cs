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
    return saved.IsFailure ? Result.Failure<Guid>(saved.Error) : Result.Success(element.Value.Id);
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
