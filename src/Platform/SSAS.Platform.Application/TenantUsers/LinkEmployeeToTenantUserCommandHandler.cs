using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantUsers;

namespace SSAS.Platform.Application.TenantUsers;

public sealed record LinkEmployeeToTenantUserCommand(long TenantUserId, Guid EmployeeId);

// ==================================================================================================
// LINKING A TENANT USER TO AN EMPLOYEE (T-092, ADR-030). AN EXPLICIT ACT, NEVER AN INFERENCE.
// ==================================================================================================
//
// `ADR-030` rejects the shortcut by name: *"Deriving the link from matching attributes — email, employee
// number, name. Rejected outright."* **So there is no matcher here and there is not meant to be one.**
//
// ---- WHAT THIS MAKES TRUE, WHICH NOTHING DID BEFORE.
//
// Four self-service permissions across two modules resolve through `IUserEmployeeResolver`, and **no link
// has ever been written**, so every one of them answered "no linked employee" for every real caller.
// T-090's seam and T-091's account closure have both been in place with nothing to act on. This is the
// row that makes them do something.
//
// ---- AT MOST ONE LIVE LINK EACH WAY, REFUSED HERE RATHER THAN AT THE INDEX.
//
// The two unique indexes are the enforcement (`ADR-030` Decision 3) and they stay the authority — a race
// between two administrators loses at the database. **But losing at the database gives the caller a
// unique-violation to interpret**, and the two collisions mean different things: this user is already
// linked, or this employee is. Checked here so each gets its own answer.
//
// ---- IDENTICAL PAIR IS A SUCCESS. A DIFFERENT PAIR IS A REFUSAL.
//
// Re-linking the same user to the same employee is a no-op and answers success, so a retry after a lost
// response is safe. **Linking a user to a DIFFERENT employee is refused, not upserted** — an upsert would
// hide a destructive act inside a creative one, and reassigning which employee a login maps to would
// appear in an audit trail as "create a link." Given the link decides whose payslips a login can read,
// that act has to be nameable: it is `POST .../employee-link/remove` followed by a link.
//
// ---- THE STANDING DIRECTORY IS OPTIONAL, AND THE THIRD TIME THE CONTAINER HAS SAID SO.
//
// Required, it broke the two Platform-support end-to-end hosts, which mount Platform with NO module
// registered — so nothing implements an HR-owned contract. **Platform sits beneath the modules and has to
// stand up without them**; T-090 learned this for `UserEmployeeResolver` and T-091 for the deactivator.
//
// ---- BUT THE RESOLUTION IS NOT AUTOMATICALLY THE SAME, AND HERE IS WHY IT IS.
//
// T-091 made `ITenantUserDeactivator` REQUIRED, because an absent deactivator would have meant *skip the
// guard* — fail OPEN. **The question is never "what did the last task do"; it is what ABSENCE MEANS.**
//
// Here absence means *no HR module, therefore no employees, therefore nothing to link*. An absent
// directory is treated as `Unknown` and every link is refused — fail CLOSED, and the same answer a
// present directory gives for an employee that does not exist.
//
// **What it costs, named: a host misconfigured without HR reports every link attempt as "employee not
// found" rather than as a missing module.** Diagnosable — nothing can be linked at all — and the
// alternative was writing rows whose subject nobody verified.
public sealed class LinkEmployeeToTenantUserCommandHandler(
  IUserEmployeeLinkRepository links,
  ITenantUserRepository tenantUsers,
  IPlatformUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IEmploymentStandingDirectory? employmentStanding = null)
{
  public async Task<Result> HandleAsync(
    LinkEmployeeToTenantUserCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return execution;
    }

    var tenantId = execution.Value.TenantId;

    var tenantUser = await tenantUsers.GetByIdAsync(command.TenantUserId, cancellationToken);
    if (tenantUser is null || tenantUser.TenantId != tenantId)
    {
      return Result.Failure(IdentityAccessErrors.NotFound);
    }

    // A deactivated user CAN be linked. The link is a mapping, not a grant, and T-091 deactivates on
    // termination — so refusing here would make a link unrepairable exactly when someone noticed it was
    // missing. What a deactivated user cannot do is authenticate, which is a different guard.
    if (tenantUser.Status == TenantUserStatus.Pending)
    {
      return Result.Failure(IdentityAccessErrors.InvalidTenantUserTransition);
    }

    // ================================================================================================
    // THE EMPLOYEE MUST EXIST AND STILL BE EMPLOYED — AND THE TWO GET DIFFERENT ANSWERS HERE.
    // ================================================================================================
    //
    // `UserBranchAccessConfiguration` states the rule for exactly this case: no foreign key can span the
    // two databases, so *"validation is the application's job, performed against the tenant database
    // before any row here is written."*
    //
    // ---- THE DISCLOSURE INVERSION, AND IT IS DELIBERATE.
    //
    // `UserEmployeeResolver` collapses `Unknown` and `Ended` into one answer, because its caller is an END
    // USER and telling them apart would disclose that a record exists. **This caller is a tenant
    // administrator acting on an employee they named and can already read**, so distinguishing them
    // discloses nothing they do not already have — and merging them would leave an administrator unable to
    // tell a typo from a former employee.
    //
    // **Do NOT "fix" this to match the seam.** The collapse is right there and wrong here, and the reason
    // is who is asking.
    var standing = employmentStanding is null
      ? EmploymentStanding.Unknown
      : await employmentStanding.GetStandingAsync(command.EmployeeId, cancellationToken);

    if (standing == EmploymentStanding.Unknown)
    {
      return Result.Failure(IdentityAccessErrors.NotFound);
    }

    // ---- A TERMINATED EMPLOYEE IS REFUSED, AND THE COST OF THAT IS NAMED.
    //
    // T-090's seam refuses to resolve a terminated employee, so a link created now would be INERT FROM
    // BIRTH: the write's only observable effect would be a row, and the administrator would perform an
    // act that does nothing they can see.
    //
    // **What it costs: a former employee whose payslips are unattributable can no longer be linked.** That
    // matters only once something READS the link for attribution, and nothing does today — both readers
    // are access paths. **The refusal is the reversible direction**: relaxing it when an attribution
    // reader exists is one line with a decision behind it, while allowing it now and discovering that
    // operators created meaningless rows is a data cleanup.
    if (standing == EmploymentStanding.Ended)
    {
      return Result.Failure(IdentityAccessErrors.EmploymentEnded);
    }

    var existingForUser = await links.GetByTenantUserAsync(tenantId, command.TenantUserId, cancellationToken);
    if (existingForUser is not null)
    {
      // IDEMPOTENT on the identical pair, so a retry after a lost response does not refuse work already
      // done. Any OTHER employee is a reassignment and must be an explicit removal first.
      return existingForUser.EmployeeId == command.EmployeeId
        ? Result.Success()
        : Result.Failure(IdentityAccessErrors.TenantUserAlreadyLinked);
    }

    var existingForEmployee = await links.GetByEmployeeAsync(tenantId, command.EmployeeId, cancellationToken);
    if (existingForEmployee is not null)
    {
      return Result.Failure(IdentityAccessErrors.EmployeeAlreadyLinked);
    }

    var link = UserEmployeeLink.Create(tenantId, command.TenantUserId, command.EmployeeId);
    if (link.IsFailure)
    {
      return link;
    }

    await links.AddAsync(link.Value, cancellationToken);

    return await PersistenceResult.SaveAsync(unitOfWork, cancellationToken);
  }
}
