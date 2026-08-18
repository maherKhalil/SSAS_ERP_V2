using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Domain.Branches;

namespace SSAS.Platform.Infrastructure.Branches;

// The open branch-transfer declaration for one operation (FP-006C2, ADR-024 decision 3).
//
// ---- INSTANCE STATE, REGISTERED SCOPED. DELIBERATELY NOT STATIC, AND DELIBERATELY NOT AsyncLocal.
//
// Static state would be shared by every concurrent request in the process — one transfer would authorize
// another request's save. AsyncLocal would avoid that but flows into every task started inside the scope,
// so a declaration could leak into background work that outlives the operation. A scoped instance is
// reachable only by the components resolved from the same scope, which is precisely the boundary the
// authorization is meant to have.
//
// ---- DISPOSAL IS THE LIFETIME, AND IT IS EXCEPTION-SAFE.
//
// The handle clears the declaration whether the operation succeeded or threw, because a `using` unwinds on
// both paths. Disposing twice is a no-op, and a handle whose declaration has already been replaced clears
// nothing — so an out-of-order disposal cannot revoke a transfer it did not open.
internal sealed class BranchTransferScope : IBranchTransferScope
{
  public BranchTransferDeclaration? Current { get; private set; }

  public Result<IDisposable> Begin(BranchTransferDeclaration declaration)
  {
    ArgumentNullException.ThrowIfNull(declaration);

    // NESTING IS REFUSED RATHER THAN STACKED. Restoring a previous declaration on disposal would mean an
    // inner transfer silently re-enables an outer one, and the boundary can only ever act on one.
    if (Current is not null)
    {
      return Result.Failure<IDisposable>(BranchErrors.TransferAlreadyInProgress);
    }

    Current = declaration;
    return Result.Success<IDisposable>(new Handle(this, declaration));
  }

  private sealed class Handle(BranchTransferScope scope, BranchTransferDeclaration declaration) : IDisposable
  {
    private bool disposed;

    public void Dispose()
    {
      if (disposed)
      {
        return;
      }

      disposed = true;

      // Only clears the declaration this handle opened. Guards against a stale handle disposed after the
      // scope moved on, which would otherwise cancel an unrelated transfer.
      if (ReferenceEquals(scope.Current, declaration))
      {
        scope.Current = null;
      }
    }
  }
}
