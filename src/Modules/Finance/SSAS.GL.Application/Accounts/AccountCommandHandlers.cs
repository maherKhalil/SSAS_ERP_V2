using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.GL.Application.Abstractions;
using SSAS.GL.Application.Permissions;
using SSAS.GL.Application.Reads;
using SSAS.GL.Domain.Accounts;

namespace SSAS.GL.Application.Accounts;

// THE CHART OF ACCOUNTS' WRITE PATH (REQ-GL-0005..0007, OD-GL-0003).
//
// ================================================================================================
// NO COMMAND HERE CARRIES A CompanyId, AND THAT IS THE RULING RATHER THAN AN OVERSIGHT.
// ================================================================================================
//
// `OD-GL-0003` ruled the chart TENANT-level, so `Account` is `ITenantOwnedEntity` and not
// `ICompanyOwnedEntity`. Three consequences follow, and all three are visible in this file:
//
//   1. the commands take no company;
//   2. authorization is `RequirePermission` alone — there is no company dimension to check;
//   3. the write boundary runs no `AuthorizeCurrentCompanyAsync`, because nothing in the saved graph is
//      company-owned.
//
// Contrast `CreateDepartmentCommand`, which carries a `CompanyId` and calls `AuthorizeAsync`. The
// difference between these two files IS the difference between the two rulings.

public sealed record CreateAccountCommand(string Code, string Name);

public sealed class CreateAccountCommandHandler(
  IAccountRepository accounts,
  IGlScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public async Task<Result<Guid>> HandleAsync(
    CreateAccountCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (currentTenant.TenantId is not { } || string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure<Guid>(GlScopeErrors.InvalidActor);
    }

    var permitted = scope.RequirePermission(GlPermissionNames.CreateAccounts);
    if (permitted.IsFailure)
    {
      return Result.Failure<Guid>(permitted.Error);
    }

    var account = Account.Create(command.Code, command.Name);
    if (account.IsFailure)
    {
      return Result.Failure<Guid>(account.Error);
    }

    // ---- CHECKED HERE AND ALSO ENFORCED BY A UNIQUE INDEX, DELIBERATELY BOTH.
    //
    // This check gives the caller a named error instead of a constraint violation. The index is what makes
    // the rule TRUE under concurrency, because two requests can both read "no such code" before either
    // writes. Neither alone is sufficient: the check without the index is a race, and the index without the
    // check is a stack trace where an error message belongs.
    if (await accounts.CodeExistsAsync(account.Value.Code.NormalizedValue, cancellationToken))
    {
      return Result.Failure<Guid>(AccountErrors.DuplicateCode);
    }

    await accounts.AddAsync(account.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saved.IsFailure
      ? Result.Failure<Guid>(saved.Error)
      : Result.Success(account.Value.Id);
  }
}

// `REQ-GL-0006`. The name changes; the CODE never does, and there is no command that would change it —
// `Account` exposes no method for it either. `AccountErrors.CodeIsImmutable` exists for the transport layer
// to answer a caller who sends one anyway, not because any path here can reach it.
public sealed record RenameAccountCommand(Guid AccountId, string Name, byte[]? RowVersion);

public sealed class RenameAccountCommandHandler(
  IAccountRepository accounts,
  IGlScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentUser currentUser)
{
  public async Task<Result> HandleAsync(
    RenameAccountCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(GlScopeErrors.InvalidActor);
    }

    var permitted = scope.RequirePermission(GlPermissionNames.UpdateAccounts);
    if (permitted.IsFailure)
    {
      return permitted;
    }

    var account = await accounts.GetByIdAsync(command.AccountId, cancellationToken);
    if (account is null)
    {
      return Result.Failure(AccountErrors.NotFound);
    }

    var renamed = account.Rename(command.Name);
    if (renamed.IsFailure)
    {
      return renamed;
    }

    ApplyConcurrencyToken(account, command.RowVersion);

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }

  // ---- THE CALLER'S ROW VERSION IS THE ONE THE UPDATE IS CHECKED AGAINST.
  //
  // Setting the ORIGINAL value is what makes the concurrency token do its work: EF compares what the caller
  // last read against what is stored, so a caller holding a stale version loses. Assigning the current
  // value instead — the easy mistake — would compare the row against itself and never conflict, which is a
  // concurrency check that always passes and therefore is not one.
  internal static void ApplyConcurrencyToken(Account account, byte[]? rowVersion)
  {
    if (rowVersion is { Length: > 0 })
    {
      account.RowVersion = rowVersion;
    }
  }
}

// `REQ-GL-0007`. Deactivation and reactivation share a handler shape because they are one lifecycle read in
// two directions; `BR-GL-0004` gives the inactive state its only consequence, and reactivation was recorded
// in `lifecycle-model.md` as permitted without ceremony.
public sealed record SetAccountActivationCommand(Guid AccountId, bool IsActive, byte[]? RowVersion);

public sealed class SetAccountActivationCommandHandler(
  IAccountRepository accounts,
  IGlScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentUser currentUser)
{
  public async Task<Result> HandleAsync(
    SetAccountActivationCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(GlScopeErrors.InvalidActor);
    }

    var permitted = scope.RequirePermission(GlPermissionNames.DeactivateAccounts);
    if (permitted.IsFailure)
    {
      return permitted;
    }

    var account = await accounts.GetByIdAsync(command.AccountId, cancellationToken);
    if (account is null)
    {
      return Result.Failure(AccountErrors.NotFound);
    }

    if (command.IsActive)
    {
      account.Reactivate();
    }
    else
    {
      account.Deactivate();
    }

    RenameAccountCommandHandler.ApplyConcurrencyToken(account, command.RowVersion);

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}
