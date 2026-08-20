using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Application.Departments;

// MOVE A DEPARTMENT, AND ITS WHOLE SUBTREE, BENEATH ANOTHER (REQ-HR-0101, BR-HR-0008).
//
// EXPLICIT, never a field on the ordinary update. Folding it in would make a subtree move — which must pass
// the acyclicity invariant — reachable by sending one extra field to a route whose name says "update".
public sealed record ChangeDepartmentParentCommand(
  Guid DepartmentId,
  Guid NewParentDepartmentId,
  byte[] RowVersion);

// MOVE A DEPARTMENT TO THE ROOT (REQ-HR-0101).
//
// A SEPARATE COMMAND rather than `ChangeDepartmentParent(null)`. A nullable field in a strict request is
// ambiguous between "make it a root" and "I did not send this", and the two must not be confused when the
// field controls where a subtree hangs.
public sealed record MoveDepartmentToRootCommand(
  Guid DepartmentId,
  byte[] RowVersion);

// ================================================================================================
// BOTH OPERATIONS RUN INSIDE ONE TRANSACTION, UNDER ONE LOCK.
// ================================================================================================
//
// The ancestry read, the mutation and the commit are a single unit, and the company hierarchy lock is held
// across all three. Anything less leaves the gap described on `IDepartmentHierarchyLock`: two individually
// legal moves combining into a cycle that neither transaction could have detected.
//
// MOVE-TO-ROOT TAKES THE SAME LOCK even though a root parent cannot create a cycle. It is not defending
// itself — it is refusing to be the OTHER half of someone else's race. If it committed outside the lock, a
// concurrent `ChangeDepartmentParent` could validate ancestry against a chain this operation was in the
// middle of rewriting. One consistent mutation model, or none.
public sealed class ChangeDepartmentParentCommandHandler(
  IDepartmentRepository departments,
  IDepartmentScopeResolver scope,
  IDepartmentHierarchyLock hierarchyLock,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    ChangeDepartmentParentCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    // ---- THE TRANSACTION OPENS FIRST, because the lock is owned by it.
    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    var loaded = await DepartmentWriteContext.LoadAsync(
      departments, scope, currentTenant, command.DepartmentId,
      HrPermissionNames.UpdateDepartments, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      await transaction.RollbackAsync(cancellationToken);
      return Result.Failure(loaded.Error);
    }

    var department = loaded.Value;

    // ---- THE LOCK, BEFORE THE ANCESTRY IS READ.
    //
    // Taken after the department is loaded because the COMPANY is not known until then, and the key is
    // per company. Everything that decides whether this move is legal happens after this line.
    var acquired = await hierarchyLock.AcquireAsync(
      department.TenantId, department.CompanyId, cancellationToken);
    if (acquired.IsFailure)
    {
      await transaction.RollbackAsync(cancellationToken);
      return Result.Failure(acquired.Error);
    }

    var validated = await ValidateParentAsync(
      departments, department, command.NewParentDepartmentId, cancellationToken);
    if (validated.IsFailure)
    {
      await transaction.RollbackAsync(cancellationToken);
      return Result.Failure(validated.Error);
    }

    var moved = department.ChangeParent(command.NewParentDepartmentId, Guid.NewGuid(), clock.UtcNow);
    if (moved.IsFailure)
    {
      await transaction.RollbackAsync(cancellationToken);
      return moved;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      await transaction.RollbackAsync(cancellationToken);
      return Result.Failure(saved.Error);
    }

    // COMMIT RELEASES THE LOCK, because it is transaction-owned. There is no path where the write lands and
    // the lock outlives it.
    await transaction.CommitAsync(cancellationToken);

    return Result.Success();
  }

  // ---- THE VALIDATION ORDER IS THE APPROVED ORDER, AND IT IS DELIBERATE.
  //
  // Existence, then tenant and company, then status, then self, then the walk. The cheap local refusals
  // come first so the O(depth) database walk runs only for a move that is otherwise legal.
  internal static async Task<Result> ValidateParentAsync(
    IDepartmentRepository departments,
    Department department,
    Guid proposedParentId,
    CancellationToken cancellationToken)
  {
    // Checked before the lookup: a department is its own parent regardless of what the database says, and
    // this is the one cycle case that needs no I/O at all.
    if (proposedParentId == department.Id)
    {
      return Result.Failure(DepartmentErrors.ParentIsSelf);
    }

    if (proposedParentId == Guid.Empty)
    {
      return Result.Failure(DepartmentErrors.InvalidParent);
    }

    var parent = await departments.GetByIdAsync(proposedParentId, cancellationToken);
    if (parent is null)
    {
      return Result.Failure(DepartmentErrors.ParentNotFound);
    }

    if (parent.TenantId != department.TenantId || parent.CompanyId != department.CompanyId)
    {
      return Result.Failure(DepartmentErrors.ParentInDifferentCompany);
    }

    if (parent.Status != DepartmentStatus.Active)
    {
      return Result.Failure(DepartmentErrors.ParentInactive);
    }

    // ================================================================================================
    // THE CYCLE CHECK: WALK UP FROM THE PROPOSED PARENT.
    // ================================================================================================
    //
    // If the department being moved appears anywhere on the chain from the proposed parent to its root,
    // then the proposed parent is a DESCENDANT of the department, and hanging the department beneath it
    // would close a loop.
    //
    // UPWARD FROM THE PARENT, not downward over the department's descendants: the chain is O(depth) while
    // the subtree is O(size), and depth is the smaller number in every real hierarchy.
    //
    // The chain is read inside this transaction, under the lock taken above, so it is the chain the write
    // commits against rather than one that was true a moment ago.
    var ancestry = await departments.GetAncestryAsync(parent.Id, cancellationToken);
    foreach (var ancestor in ancestry)
    {
      if (ancestor.Id == department.Id)
      {
        return Result.Failure(DepartmentErrors.HierarchyCycle);
      }
    }

    return Result.Success();
  }
}

public sealed class MoveDepartmentToRootCommandHandler(
  IDepartmentRepository departments,
  IDepartmentScopeResolver scope,
  IDepartmentHierarchyLock hierarchyLock,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    MoveDepartmentToRootCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    var loaded = await DepartmentWriteContext.LoadAsync(
      departments, scope, currentTenant, command.DepartmentId,
      HrPermissionNames.UpdateDepartments, command.RowVersion, cancellationToken);
    if (loaded.IsFailure)
    {
      await transaction.RollbackAsync(cancellationToken);
      return Result.Failure(loaded.Error);
    }

    var department = loaded.Value;

    // ---- SAME LOCK, THOUGH THIS OPERATION CANNOT CREATE A CYCLE BY ITSELF.
    //
    // It can still be the other half of one: a concurrent re-parent validating its ancestry needs this
    // department's chain to be stable while it walks. Hierarchy writes have ONE mutation model.
    var acquired = await hierarchyLock.AcquireAsync(
      department.TenantId, department.CompanyId, cancellationToken);
    if (acquired.IsFailure)
    {
      await transaction.RollbackAsync(cancellationToken);
      return Result.Failure(acquired.Error);
    }

    // No ancestry traversal: the root has no parent, so no chain can lead back to this department.
    var moved = department.ChangeParent(null, Guid.NewGuid(), clock.UtcNow);
    if (moved.IsFailure)
    {
      await transaction.RollbackAsync(cancellationToken);
      return moved;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      await transaction.RollbackAsync(cancellationToken);
      return Result.Failure(saved.Error);
    }

    await transaction.CommitAsync(cancellationToken);

    return Result.Success();
  }
}
