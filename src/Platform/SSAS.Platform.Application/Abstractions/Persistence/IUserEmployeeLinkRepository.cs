using SSAS.Platform.Domain.TenantUsers;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// ==================================================================================================
// THE LINK'S WRITE PATH (T-092, ADR-030). THE FIRST ONE THERE HAS EVER BEEN.
// ==================================================================================================
//
// `UserEmployeeLink` was built in T-082 and read by two Platform services since, and **nothing in `src`
// has ever written one.** So every self-service surface — four permissions across two modules — resolves
// to "no linked employee" for every real caller, and T-090's and T-091's guards have had nothing to guard.
//
// ---- IT IS A REPOSITORY BECAUSE THAT IS PLATFORM'S SANCTIONED WRITE PATH.
//
// The two existing readers go through `PlatformDbContext` directly, which is right for a read. A handler
// writing through the context would be the first exception to the repository pattern in this layer, and
// the argument for being the first exception is never as strong as it looks.
//
// ---- NO `Update`, AND THAT IS THE WHOLE SHAPE OF THE TYPE.
//
// A link is CREATED and DELETED, never modified: it carries a tenant, a tenant user and an employee, and
// changing any of them makes it a different link. **That is also why the entity has no `RowVersion`** — a
// row version prevents a lost update, and there is no update to lose. The concurrency control is the two
// unique indexes (`ADR-030` Decision 3), and a race loses at the database with a unique violation the
// unit of work already translates.
//
// ---- REMOVAL IS PHYSICAL, FOLLOWING `UserBranchAccess`.
//
// Retaining removed rows would mean excluding them from every uniqueness test and every check thereafter.
// **The row is gone; the payslips it once attributed are not.**
public interface IUserEmployeeLinkRepository
{
  // By TENANT USER. Used to answer "is this user already linked" before creating, so the common mistake
  // gets a stated refusal rather than a unique-violation the caller has to interpret.
  Task<UserEmployeeLink?> GetByTenantUserAsync(
    Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default);

  // By EMPLOYEE. The other unique index, and the other half of "at most one live link each way" — an
  // employee already linked to somebody else must refuse rather than collide.
  Task<UserEmployeeLink?> GetByEmployeeAsync(
    Guid tenantId, Guid employeeId, CancellationToken cancellationToken = default);

  Task AddAsync(UserEmployeeLink link, CancellationToken cancellationToken = default);

  // Physical. See the type's own comment: a link is removed only by administrative correction, and
  // termination is deliberately NOT such an event (`REQ-SS-0006`).
  void Remove(UserEmployeeLink link);
}
