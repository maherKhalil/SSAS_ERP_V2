using System.Globalization;
using System.Reflection;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY ERROR A MAPPING SITE IS RESPONSIBLE FOR IS MAPPED, BECAUSE THE FALLTHROUGH IS A 500.
// ==================================================================================================
//
// ---- THE DEFECT.
//
// A handler returning `Result.Failure(SomeErrors.Whatever)` throws nothing and touches no logger. If the
// site that answers for that route has no arm for the code it falls through:
//
//     AttendanceApiErrorMapper.cs   _ => ApiErrors.WriteFailure
//     ApiErrors.cs                  WriteFailure = new(500, "request.failed")
//
// **The caller gets `500 request.failed` for a business refusal.** The handler reads correctly and the
// defect is an ABSENCE in a second file. `EmployeeApiErrorMapper.cs:108` says so in its own words —
// *"a code with no arm falls through to a 500 that would disclose by its own strangeness"* — six lines
// above six codes it does not map.
//
// ---- THE UNIT IS THE SITE, NOT THE MODULE. THIS FILE GOT THAT WRONG ONCE (T-078, corrected T-079).
//
// The first version concatenated a module's mapping sites and asked whether a code appeared in ANY of
// them. That treats a code mapped for one surface as mapped for all, and it **undercounted HR by half**:
// `Employee.PositionNotFound` is mapped in `EmployeeImportExportTransportContracts` for the import
// surface and NOT in `EmployeeApiErrorMapper`, which is what `POST /api/hr/employees` and
// `POST /{employeeId}/change-position` actually use. Both answer 500 today.
//
// *Is this code mapped?* is only meaningful relative to the surface that can raise it.
//
// ---- WHY RESPONSIBILITY IS DECLARED AND NOT DERIVED. THE ANSWER IS "IT CANNOT BE".
//
// Deriving route -> mapper -> reachable errors is not tractable here, and not for one reason:
//
//   1. `Microsoft.CodeAnalysis` is referenced by no test project. There is no call graph to walk.
//   2. The reachable set spans layers: `ChangeEmployeePositionCommandHandler` returns eight errors and
//      calls `Employee.ChangePosition`, which returns two more (`Employee.cs:571,579`).
//   3. It crosses interfaces — `IEmployeeRepository` — whose bindings are composition facts.
//   4. It crosses modules legitimately: `PayrollApiErrorMapper` maps `Company.*`, `GlApiErrorMapper`
//      maps `Company.*` and `Persistence.*`. A site's candidate set is not bounded by its module.
//
// So responsibility is DECLARED by code family. A wrong derivation accuses innocents; a declaration is
// reviewable, and a new family has to be added by a person — H9's argument for its exact inventory.
//
// ---- A SYMBOL SEARCH IS NOT A CODE SEARCH (T-093b).
//
// The CODES in this register are unique. **The C# member names that carry them are not.**
// `CompanyAccessErrors.AssignmentInvalid` and `BranchErrors.AssignmentInvalid` are different errors with
// the same member name, and a search for `AssignmentInvalid` returns both — with the branch sites, which
// validate a branch-id list, outnumbering the company one.
//
// **Ruling a status from those hits would have put a branch-list validation's answer onto a company-access
// code, and every hit would have looked confirming.** Qualify by class when you go looking for a code's
// raise sites. `DEC-L-069`'s family, and the third instance of it this week.
//
// ---- T-094 REPLACED THE OLD FLOOR. IT DID NOT REMOVE ONE.
//
// **The old floor:** responsibility was declared by code FAMILY, so errors raised inside services a handler
// INJECTED were uncounted. Company's five `CompanyAccessErrors` proved it — a manual walk said one, the
// register found five.
//
// **That floor is gone.** Responsibility is now DERIVED: the closure of each site's seed handlers over
// their constructor parameters, by reflection. `UnroutedFamilies` and the family-ownership test went with
// it — see below.
//
// **And a NEW floor took its place, which is the honest way to read this file:**
//
//   1. ROUTE -> MAPPER IS STILL DECLARED. Which mapper answers for which route is not derivable without a
//      call graph, and there is no Roslyn in this tree. `Seeds` is that declaration.
//
//   2. **A RETURNED ERROR IS NOT A REACHED ERROR.** This is the replacement for the old floor and the one
//      most likely to mislead. The walk proves a type in the closure RETURNS a code; it cannot prove the
//      code propagates to the mapper rather than being handled by an intermediate caller. The register can
//      therefore name a code no wire response ever carries.
//
//   3. INLINE-CONSTRUCTED CODES ARE INVISIBLE. Only `SomeErrors.Member` symbols are extracted.
//      `LocalizationApiErrorMapper`'s vocabulary is string literals, which is exactly this shape.
//
//   4. WHICH IMPLEMENTATION DI ACTUALLY BINDS. Reflection finds every implementor of an interface and the
//      walk follows all of them; only the composition root knows which one runs. The closure is therefore
//      a superset where an interface has several implementations.
//
// **Floors 1, 3 and 4 were true before T-094 and remain true. Floor 2 is new, and it exists because the old
// one was replaced rather than closed.**
//
// ---- A SITE MAY MAP CODES ITS CLOSURE DOES NOT REACH, AND THAT IS NOT PENALISED.
//
// Only what a site is RESPONSIBLE for is asserted; an extra arm is a site being helpful, or a site whose
// closure is a superset short of one implementation (floor 4).
public sealed class ModuleErrorMappingArchitectureTests
{
  // ---- RESPONSIBILITY IS DERIVED FROM THE SEEDS, NOT DECLARED BY FAMILY (T-094).
  //
  // `Seeds` are the handlers a site's routes invoke — **the route-to-mapper edge, which is still DECLARED
  // because it is still not derivable.** Everything downstream of them is walked.
  private sealed record MappingSite(
    string Site,
    string SourcePath,
    Type[] Seeds,
    string[] KnownUnmapped);






  // ---- ⚠ THE RECORDED EXEMPTIONS, EXPOSED SO A SECOND GUARD CITES THEM RATHER THAN RE-DECIDING (T-200).
  //
  // `PropagatedErrorMappingTests` walks the errors a module RETURNS rather than the errors a site NAMES,
  // which is how two live 500s were found on 2026-08-29. It therefore meets the same deliberately-unmapped
  // codes this file already argues about — `Payroll.OneOffPaymentConsumingRunRequired` among them — and a
  // second copy of that list would go stale against this one in exactly the way DEC-L-080 describes.
  internal static IReadOnlySet<string> RecordedUnmapped() =>
    Sites().SelectMany(site => site.KnownUnmapped).ToHashSet(StringComparer.Ordinal);

  private static MappingSite[] Sites() =>
  [
    new("AttendanceApiErrorMapper",
      Path.Combine("src", "Modules", "Attendance", "SSAS.Attendance.API", "AttendanceApiErrorMapper.cs"),
      [
        typeof(SSAS.Attendance.Application.Calendars.CreateWorkingCalendarCommandHandler),
        typeof(SSAS.Attendance.Application.Calendars.UpdateWorkingCalendarCommandHandler),
        typeof(SSAS.Attendance.Application.Calendars.AddHolidayCommandHandler),
        typeof(SSAS.Attendance.Application.Calendars.RemoveHolidayCommandHandler),
        typeof(SSAS.Attendance.Application.Periods.CreateAttendancePeriodCommandHandler),
        typeof(SSAS.Attendance.Application.Periods.CloseAttendancePeriodCommandHandler),
        typeof(SSAS.Attendance.Application.Periods.ReopenAttendancePeriodCommandHandler),
        typeof(SSAS.Attendance.Application.Records.RecordAttendanceCommandHandler),
        typeof(SSAS.Attendance.Application.Records.AdjustAttendanceCommandHandler),
        typeof(SSAS.Attendance.Application.Leave.CreateLeaveTypeCommandHandler),
        typeof(SSAS.Attendance.Application.Leave.UpdateLeaveTypeCommandHandler),
        typeof(SSAS.Attendance.Application.Leave.SetLeaveTypeActivationCommandHandler),
        typeof(SSAS.Attendance.Application.Leave.SubmitLeaveRequestCommandHandler),
        typeof(SSAS.Attendance.Application.Leave.ApproveLeaveRequestCommandHandler),
        typeof(SSAS.Attendance.Application.Leave.RejectLeaveRequestCommandHandler),
        typeof(SSAS.Attendance.Application.Leave.CancelLeaveRequestCommandHandler),
        typeof(SSAS.Attendance.Application.Leave.SetLeaveEntitlementCommandHandler),
        typeof(SSAS.Attendance.Application.Reads.AttendanceSelfServiceScopeResolver),
      ], []),

    new("GlApiErrorMapper",
      Path.Combine("src", "Modules", "Finance", "SSAS.GL.API", "GlApiErrorMapper.cs"),
      [
        typeof(SSAS.GL.Application.Accounts.CreateAccountCommandHandler),
        typeof(SSAS.GL.Application.Accounts.RenameAccountCommandHandler),
        typeof(SSAS.GL.Application.Accounts.SetAccountActivationCommandHandler),
        typeof(SSAS.GL.Application.Calendar.DefineFiscalYearCommandHandler),
        typeof(SSAS.GL.Application.Calendar.SetFiscalPeriodStateCommandHandler),
        typeof(SSAS.GL.Application.Journals.CreateJournalDraftCommandHandler),
        typeof(SSAS.GL.Application.Journals.UpdateJournalDraftCommandHandler),
        typeof(SSAS.GL.Application.Journals.DiscardJournalDraftCommandHandler),
        typeof(SSAS.GL.Application.Journals.PostJournalDraftCommandHandler),
        typeof(SSAS.GL.Application.Journals.ReverseJournalCommandHandler),
      ],
      // ---- ⚠ `Persistence.UniqueConstraint` WAS EXEMPT HERE AND NO LONGER IS — `DEC-DEP-0027` AMENDED BY
      // T-245. The original reasoning is kept, because a decision that was amended should read as amended
      // rather than as though it never said otherwise.
      //
      // **WHAT T-165 SAID, AND MOST OF IT STANDS.** GL has **six** unique indexes: account code,
      // fiscal-year code, draft line number, journal number, one-reversal-per-original, and entry line
      // number. **One generic arm answers the same message to all six** — a duplicate account code would
      // be told *"a journal with this number already exists in this fiscal year"*, and a double-reversal
      // race would be told it too, when that condition owns `Gl.JournalAlreadyReversed`. **That objection
      // was right, and it is why no arm here may ever NAME an index.**
      //
      // **`DEC-DEP-0027`: resolved by the caller who knows the operation** — unchanged and still primary.
      // `PostJournalDraftCommandHandler` translates it to `JournalErrors.NumberConflict` because it can
      // reach exactly one index: the reversal index is FILTERED to `ReversesJournalEntryId IS NOT NULL`,
      // which a posting never sets. **`ReverseJournalCommandHandler` can reach TWO and is therefore still
      // NOT translated** — it takes a journal number AND writes the reversal link, and
      // `IdentityAccessErrors.UniqueConstraintViolation` is a static error carrying no index name. **A
      // handler that cannot tell which index it hit must not name one.**
      //
      // ---- WHAT T-245 OVERTURNED IS ONE SENTENCE.
      //
      // T-165 also said *"the 500 default is not the bug here; it is the house rule working"*. **A 500 is
      // not a silence, it is a WRONG ASSERTION** — it tells the caller the server broke when nothing
      // broke, sends them to report a bug rather than examine their input, and inflates the single metric
      // an operator pages on.
      //
      // ⚠ **AND THE FORCING-FUNCTION READING IS REFUTED BY THIS REPOSITORY'S OWN HISTORY.** T-171, T-173
      // and T-176 each rediscovered this exact class and repaired one path apiece, each leaving a comment
      // recording that the loser *"answered 500 for a plain business conflict"*. **A wrong status kept as
      // a reminder reminded nobody** — it shipped 500s until someone tripped over it a fourth time.
      //
      // `GlApiErrorMapper.UniqueConflict` is `gl.unique_conflict`. **It names no index, so it satisfies
      // the principle above rather than contradicting it**, and it fires only where no handler resolved
      // the context: a floor under the unclassified, not a switch pretending to know.
      []),

    new("PayrollApiErrorMapper",
      Path.Combine("src", "Modules", "Payroll", "SSAS.Payroll.API", "PayrollApiErrorMapper.cs"),
      [
        typeof(SSAS.Payroll.Application.Elements.CreatePayElementCommandHandler),
        typeof(SSAS.Payroll.Application.Elements.UpdatePayElementCommandHandler),
        typeof(SSAS.Payroll.Application.Elements.SetPayElementActivationCommandHandler),
        typeof(SSAS.Payroll.Application.Compensation.RecordCompensationCommandHandler),
        typeof(SSAS.Payroll.Application.Runs.GeneratePayrollPeriodCommandHandler),
        typeof(SSAS.Payroll.Application.Runs.CreatePayrollRunCommandHandler),
        typeof(SSAS.Payroll.Application.Runs.CalculatePayrollRunCommandHandler),
        typeof(SSAS.Payroll.Application.Runs.ApprovePayrollRunCommandHandler),
        typeof(SSAS.Payroll.Application.Runs.PostPayrollRunCommandHandler),
        typeof(SSAS.Payroll.Application.Runs.ReversePayrollRunCommandHandler),
        typeof(SSAS.Payroll.Application.Reads.PayrollSelfServiceScopeResolver),

        // ---- A STATIC CLASS MUST BE SEEDED, BECAUSE THE WALK CANNOT REACH ONE (T-120).
        //
        // `Closure` follows CONSTRUCTOR PARAMETERS. A `static` class has no constructors and is nobody's
        // parameter, so `PayrollCalculator` — where every payroll refusal is returned — had never entered
        // this closure in any run. **`Payroll.OneOffPaymentElementNotPayable` was a 500 for three tasks
        // while this test reported twelve green** (T-117, T-118).
        //
        // ---- WHY A SEED RATHER THAN A WIDER WALK, AND THE ALTERNATIVE WAS MEASURED.
        //
        // T-120 first tried following statically-called types by name from each closure member's source.
        // **Transitively it was unbounded** — `CompanyApiErrorMapper` claimed 97 codes across eight
        // modules, because a name in source is not a call and everything reaches everything within three
        // hops. **Bounded to one hop and to `Name.` member-access syntax it still failed nine of twelve
        // sites**, because shared scope resolvers are called by every module and name errors from all of
        // them.
        //
        // **A seed is exact.** It says which static class this site answers for, in the same list that
        // already says which handlers it answers for, and it over-reports nothing.
        //
        // **The cost is that it is a list somebody must maintain** — a new static class returning errors is
        // invisible until it is added here. That is the floor, and it is stated rather than hidden:
        // `The_register_seeds_every_static_class_that_returns_an_error` holds it.
        typeof(SSAS.Payroll.Domain.Runs.PayrollCalculator),

        // ---- AND THE ONE-OFF ROUTE'S HANDLER AND AGGREGATE (T-125).
        //
        // **T-120 seeded a static class and this was still missed three tasks later.** `RecordOneOffPaymentCommandHandler`
        // is a route parameter at `PayrollEndpointRouteBuilderExtensions.cs:146`, added by T-110, and never
        // seeded — so its codes were outside every closure and `amount: 0` answered 500.
        //
        // **`OneOffPayment` is seeded too, and for a different reason:** the consumption refusals are
        // returned by the AGGREGATE, which no constructor walk reaches — a repository returns it, and the
        // closure follows parameter TYPES rather than return types.
        //
        // **The floor this exposes is wider than T-120 stated it.** `KnownUnseeded` fires only for static
        // classes; **an unseeded handler or aggregate fires nothing at all.** T-126 is that hole.
        typeof(SSAS.Payroll.Application.Compensation.RecordOneOffPaymentCommandHandler),
        typeof(SSAS.Payroll.Domain.Compensation.OneOffPayment),
      ],
      // ---- ONE CODE, AND IT CROSSES A MODULE BOUNDARY.
      //
      // `PostPayrollRunCommandHandler` posts through `IJournalPoster`, whose implementation returns
      // `Gl.AccountNotFound` when the mapped account is gone. **A GL code reaching a Payroll mapper is
      // exactly the cross-module case this file's header already describes** — and the family register,
      // which gave Payroll the `Payroll` family, could not reach it.
      //
      // ---- PAID IN T-095, AND IT KEEPS GL'S CODE STRING AS WELL AS GL'S STATUS.
      //
      // `DEC-L-079` fixes the status at GL's 404. The string stays `gl.not_found` rather than becoming
      // `payroll.not_found`, because **what was not found is the ledger account** — naming the wrong
      // missing thing would be a worse answer than the 500 it replaces.
      //
      // ---- ONE CODE STAYS UNMAPPED, AND IT IS A DEFENCE RATHER THAN A REFUSAL (T-125).
      //
      // `OneOffPayment.MarkConsumedBy` refuses a `Guid.Empty` run id. **No route can produce that**: the
      // only caller is `ApprovePayrollRunCommandHandler`, which passes `run.Id` from a run it has already
      // loaded from the database. **It is a guard against a programming error, not a business refusal**, and
      // mapping it would put a status on a path no request can take — a dead arm, which T-095 established
      // is worse than an honest gap.
      ["Payroll.OneOffPaymentConsumingRunRequired"]
      ),

    new("DepartmentApiErrorMapper",
      Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Departments", "DepartmentApiErrorMapper.cs"),
      [
        typeof(SSAS.HR.Application.Departments.CreateDepartmentCommandHandler),
        typeof(SSAS.HR.Application.Departments.UpdateDepartmentCommandHandler),
        typeof(SSAS.HR.Application.Departments.ChangeDepartmentParentCommandHandler),
        typeof(SSAS.HR.Application.Departments.MoveDepartmentToRootCommandHandler),
        typeof(SSAS.HR.Application.Departments.AssignDepartmentManagerCommandHandler),
        typeof(SSAS.HR.Application.Departments.ClearDepartmentManagerCommandHandler),
        typeof(SSAS.HR.Application.Departments.DeactivateDepartmentCommandHandler),
        typeof(SSAS.HR.Application.Departments.ReactivateDepartmentCommandHandler),
        typeof(SSAS.HR.Application.Departments.Reads.GetDepartmentQueryHandler),
        typeof(SSAS.HR.Application.Departments.Reads.GetDepartmentChildrenQueryHandler),
        typeof(SSAS.HR.Application.Departments.Reads.SearchDepartmentsQueryHandler),
        typeof(SSAS.HR.Application.Employees.ChangeEmployeeDepartmentCommandHandler),
      ],
      // ---- PAID IN T-095. All ten `Employee.*` codes now have arms.
      //
      // T-094's derived register found them; `DEC-L-079` settled them without a per-code ruling, because a
      // status is a property of the CODE rather than of the SITE. Each takes `EmployeeApiErrorMapper`'s
      // existing answer unchanged. **Until then every one answered `500 request.failed` on a department
      // route** while the same code answered 404, 403 or 400 on an employee route.
      []
      ),

    new("PositionApiErrorMapper",
      Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Positions", "PositionApiErrorMapper.cs"),
      [
        typeof(SSAS.HR.Application.Positions.CreatePositionCommandHandler),
        typeof(SSAS.HR.Application.Positions.UpdatePositionCommandHandler),
        typeof(SSAS.HR.Application.Positions.DeactivatePositionCommandHandler),
        typeof(SSAS.HR.Application.Positions.ReactivatePositionCommandHandler),
        typeof(SSAS.HR.Application.Positions.CreateJobGradeCommandHandler),
        typeof(SSAS.HR.Application.Positions.UpdateJobGradeCommandHandler),
        typeof(SSAS.HR.Application.Positions.DeactivateJobGradeCommandHandler),
        typeof(SSAS.HR.Application.Positions.ReactivateJobGradeCommandHandler),
        typeof(SSAS.HR.Application.Positions.CreateSalaryGradeCommandHandler),
        typeof(SSAS.HR.Application.Positions.UpdateSalaryGradeCommandHandler),
        typeof(SSAS.HR.Application.Positions.DeactivateSalaryGradeCommandHandler),
        typeof(SSAS.HR.Application.Positions.ReactivateSalaryGradeCommandHandler),
        typeof(SSAS.HR.Application.Positions.Reads.GetPositionQueryHandler),
        typeof(SSAS.HR.Application.Positions.Reads.SearchPositionsQueryHandler),
        typeof(SSAS.HR.Application.Positions.Reads.GetJobGradeQueryHandler),
        typeof(SSAS.HR.Application.Positions.Reads.SearchJobGradesQueryHandler),
        typeof(SSAS.HR.Application.Positions.Reads.GetSalaryGradeQueryHandler),
        typeof(SSAS.HR.Application.Positions.Reads.SearchSalaryGradesQueryHandler),
        typeof(SSAS.HR.Application.Employees.ChangeEmployeePositionCommandHandler),
        typeof(SSAS.HR.Application.Employees.Reads.GetEmployeePositionHistoryQueryHandler),
      ],
      // Ten more, paid the same way and from the same source of truth (T-095, `DEC-L-079`).
      []
      ),

    new("EmployeeApiErrorMapper",
      Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Employees", "EmployeeApiErrorMapper.cs"),
      [
        typeof(SSAS.HR.Application.Employees.CreateEmployeeCommandHandler),
        typeof(SSAS.HR.Application.Employees.UpdateEmployeeProfileCommandHandler),
        typeof(SSAS.HR.Application.Employees.ActivateEmployeeCommandHandler),
        typeof(SSAS.HR.Application.Employees.DeactivateEmployeeCommandHandler),
        typeof(SSAS.HR.Application.Employees.TerminateEmployeeCommandHandler),
        typeof(SSAS.HR.Application.Employees.TransferEmployeeCommandHandler),
        typeof(SSAS.HR.Application.Employees.Reads.GetEmployeeQueryHandler),
        typeof(SSAS.HR.Application.Employees.Reads.SearchEmployeesQueryHandler),
        typeof(SSAS.HR.Application.Employees.Reads.GetEmployeeBranchHistoryQueryHandler),
      ], []),

    new("EmployeeImportExportTransportContracts",
      Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Employees", "EmployeeImportExportTransportContracts.cs"),
      [
        typeof(SSAS.HR.Application.ImportExport.ImportEmployeesCommandHandler),
        typeof(SSAS.HR.Application.ImportExport.ExportEmployeesQueryHandler),
        typeof(SSAS.HR.Application.ImportExport.SearchImportRunsQueryHandler),
        typeof(SSAS.HR.Application.ImportExport.SearchExportRunsQueryHandler),
      ],
      // ================================================================================================
      // SIX, AND THEY ARE A MIS-DECLARATION RATHER THAN DEBT — CORRECTED IN T-095.
      // ================================================================================================
      //
      // T-094 recorded these as *"scope and actor refusals the import surface inherits"*. **That was wrong,
      // and reading the route before copying an answer into it is what caught it.**
      //
      // `EmployeeEndpointRouteBuilderExtensions` answers the import and export ROUTES through
      // `EmployeeApiErrorMapper.Map(...)`. `EmployeeImportRowErrorMapper` — the switch in this file —
      // renders PER-ROW errors inside the import report body and never sees a route-level refusal.
      // **All six are already mapped, at the site that actually answers for them.**
      //
      // ---- SO THIS IS A FIFTH FLOOR, AND IT BELONGS WITH THE OTHER FOUR.
      //
      // **ONE ROUTE CAN HAVE TWO MAPPERS** — one for the envelope and one for the items inside it — and the
      // site-to-mapper edge cannot express that. Both sites seed from the same handlers, so this site's
      // derived set is a superset by construction.
      //
      // Adding six arms to a row mapper that never receives them would have been the mechanical answer and
      // the wrong one: dead arms that read as coverage.
      [
        "Employee.BranchScopeDenied",
        "Employee.CompanyScopeDenied",
        "Employee.InvalidActor",
        "Employee.InvalidPageNumber",
        "Employee.InvalidPageSize",
        "Employee.InvalidExportCeiling",
        "Employee.InvalidReadScope",
        "Employee.ReadPermissionDenied"
      ]
      ),

    new("CompanyApiErrorMapper",
      Path.Combine("src", "Platform", "SSAS.Platform.API", "Companies", "CompanyApiErrorMapper.cs"),
      [
        typeof(SSAS.Platform.Application.Companies.CreateCompanyCommandHandler),
        typeof(SSAS.Platform.Application.Companies.UpdateCompanyProfileCommandHandler),
        typeof(SSAS.Platform.Application.Companies.ActivateCompanyCommandHandler),
        typeof(SSAS.Platform.Application.Companies.DeactivateCompanyCommandHandler),
        typeof(SSAS.Platform.Application.Companies.ArchiveCompanyCommandHandler),
        typeof(SSAS.Platform.Application.Companies.ListCompaniesQueryHandler),
        typeof(SSAS.Platform.Application.Companies.GetCompanyByIdQueryHandler),
      ], []),

    new("IdentityAccessApiErrorMapper",
      Path.Combine("src", "Platform", "SSAS.Platform.API", "IdentityAccess", "IdentityAccessApiErrorMapper.cs"),
      [
        typeof(SSAS.Platform.Application.Roles.ListRolesQueryHandler),
        typeof(SSAS.Platform.Application.TenantUsers.DeactivateTenantUserCommandHandler),
        typeof(SSAS.Platform.Application.TenantUsers.ReactivateTenantUserCommandHandler),
        typeof(SSAS.Platform.Application.TenantUsers.LinkEmployeeToTenantUserCommandHandler),
        typeof(SSAS.Platform.Application.TenantUsers.UnlinkEmployeeFromTenantUserCommandHandler),
      ], []),

    new("LocalizationApiErrorMapper",
      Path.Combine("src", "Platform", "SSAS.Platform.API", "Localization", "LocalizationApiErrorMapper.cs"),
      [
        typeof(SSAS.Platform.Application.Localization.CreateTenantLocalizationOverrideCommandHandler),
        typeof(SSAS.Platform.Application.Localization.UpdateTenantLocalizationOverrideCommandHandler),
        typeof(SSAS.Platform.Application.Localization.UndoTenantLocalizationOverrideCommandHandler),
        typeof(SSAS.Platform.Application.Localization.RestoreTenantLocalizationDefaultCommandHandler),
        typeof(SSAS.Platform.Application.Localization.PreviewTenantLocalizationOverrideCommandHandler),
        typeof(SSAS.Platform.Application.Localization.GetTenantLocalizationResourceQueryHandler),
        typeof(SSAS.Platform.Application.Localization.ListTenantLocalizationResourcesQueryHandler),
        typeof(SSAS.Platform.Application.Localization.GetTenantLocalizationHistoryQueryHandler),
      ], []),

    new("PlatformSupportAuthorityApiErrorMapper",
      Path.Combine("src", "Platform", "SSAS.Platform.API", "PlatformSupport", "PlatformSupportAuthorityApiErrorMapper.cs"),
      [
        typeof(SSAS.Platform.Application.PlatformSupport.RegisterPlatformSupportPrincipalCommandHandler),
        typeof(SSAS.Platform.Application.PlatformSupport.DisablePlatformSupportPrincipalCommandHandler),
        typeof(SSAS.Platform.Application.PlatformSupport.ReenablePlatformSupportPrincipalCommandHandler),
        typeof(SSAS.Platform.Application.PlatformSupport.GrantPlatformPermissionCommandHandler),
        typeof(SSAS.Platform.Application.PlatformSupport.RevokePlatformPermissionCommandHandler),
        typeof(SSAS.Platform.Application.PlatformSupport.ListPlatformSupportPrincipalsQueryHandler),
        typeof(SSAS.Platform.Application.PlatformSupport.GetPlatformSupportPrincipalQueryHandler),
        typeof(SSAS.Platform.Application.PlatformSupport.ListPlatformPermissionAssignmentsQueryHandler),
        typeof(SSAS.Platform.Application.PlatformSupport.GetActivePlatformSupportPermissionsQueryHandler),
      ], []),

    new("PlatformAuthentication",
      Path.Combine("src", "Platform", "SSAS.Platform.API", "Authentication", "AuthenticationEndpointRouteBuilderExtensions.cs"),
      [
        typeof(SSAS.Platform.Application.Authentication.VerifyPasswordCredentialsCommandHandler),
        typeof(SSAS.Platform.Application.Authentication.SelectTenantCommandHandler),
        typeof(SSAS.Platform.Application.Authentication.BeginTenantAccessCommandHandler),
        typeof(SSAS.Platform.Application.Authentication.RefreshAuthenticationSessionCommandHandler),
        typeof(SSAS.Platform.Application.Authentication.RevokeCurrentAuthenticationSessionCommandHandler),
      ],
      // ---- SIX, AND THE DELIBERATE COLLAPSE IS WHY.
      //
      // Every ordinary authority failure answers ONE generic 401 — asserted by
      // `Every_ordinary_authority_failure_returns_the_same_generic_401`, because distinguishing "no such
      // account" from "wrong password" is the disclosure the collapse exists to prevent.
      //
      // **These six are what the walk finds REACHABLE, against the eighteen T-093 listed by hand.** The
      // hand list was every code in four families; this is the subset a route can actually produce. The
      // difference is the register becoming honest, not the surface changing.
      //
      // `Persistence.ConcurrencyConflict` is in the set and is NOT part of the collapse ruling — it is a
      // save conflict on a session write. Recorded with the rest rather than assumed benign (T-095).
      [
        "Authentication.AccessTokenUnavailable",
        "Authentication.Failed",
        "Authentication.TenantSelectionFailed",
        "AuthenticationSession.Invalid",
        "AuthenticationSession.RefreshFailed",
        "Persistence.ConcurrencyConflict"
      ]
      )
  ];

  // ---- THE REGISTER IS AN EXACT SET, NOT A CEILING.
  //
  // A "no more than N" rule passes when one error is mapped and another added — the swap argument from
  // T-077. So mapping one of these thirteen must also fail: the register records DEBT, and paying it down
  // is a thing someone should have to write down.
  [Theory]
  [MemberData(nameof(SiteNames))]
  public void Every_error_a_site_is_responsible_for_is_mapped_rather_than_falling_through(string siteName)
  {
    var site = Sites().Single(candidate => candidate.Site == siteName);

    var responsible = ResponsibleCodes(site);

    // NOT VACUOUS, AND PER SITE RATHER THAN PER MODULE OR AGGREGATED. An aggregate is satisfied by the
    // largest site while the smallest goes unexamined — T-073's one-policy-wide plane. A site whose
    // enumeration comes back empty is a broken sweep or a misspelt family, not a clean site.
    Assert.True(
      responsible.Length > 0,
      $"{siteName}'s closure is {Closure(site).Length} type(s) from {site.Seeds.Length} seed(s) and names " +
      $"no declared Error at all. [symbols={ErrorFieldsBySymbol().Count} sources={SourceByTypeName().Count} " +
      $"located={Closure(site).Count(t => SourceByTypeName().ContainsKey(t.Name))}]. Either a seed is wrong, the walk terminated at the first hop, or the " +
      "source index could not locate the closure's types — an empty enumeration would make the assertion " +
      "below pass over nothing.");

    // ---- EVERY CLOSURE TYPE MUST RESOLVE TO A SOURCE FILE, AND THIS IS THE GUARD ON THE ONE REMAINING
    // ---- TEXT STEP.
    //
    // `DEC-L-078`: a text step that under-resolves reports a smaller answer, not an error. So the type
    // whose file cannot be found reddens HERE rather than contributing nothing and looking like a clean
    // walk.
    var unlocated = Closure(site)
      .Where(type => !SourceByTypeName().ContainsKey(type.Name))
      .Select(type => type.FullName ?? type.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      unlocated.Length == 0,
      $"{siteName}'s closure contains type(s) with no source file under `src/`, so the codes they name " +
      $"were never read and the responsible set is silently short:{Environment.NewLine}" +
      string.Join(Environment.NewLine, unlocated));

    var source = ReadRepositoryFile(site.SourcePath);

    var unmapped = responsible
      .Where(code => !source.Contains($"\"{code}\" =>", StringComparison.Ordinal))
      .OrderBy(code => code, StringComparer.Ordinal)
      .ToArray();

    // The message names the consequence, not a remedy. WHICH status a business error deserves is a
    // contract decision owned by the surface, and a test asserting one would be choosing it.
    Assert.True(
      unmapped.SequenceEqual(site.KnownUnmapped.OrderBy(code => code, StringComparer.Ordinal), StringComparer.Ordinal),
      $"{siteName}'s responsible errors and its arms have diverged. An unmapped error falls through to a " +
      "500 for what is usually a business refusal — no exception, no log entry, and a handler that reads " +
      "correctly. If one was just mapped, remove it from KnownUnmapped in the same commit." +
      $"{Environment.NewLine}expected unmapped: {Format(site.KnownUnmapped)}" +
      $"{Environment.NewLine}actual unmapped:   {Format(unmapped)}");
  }

  // ================================================================================================
  // THE SAME CODE ANSWERS THE SAME STATUS AT EVERY SITE THAT MAPS IT (`DEC-L-079`, T-095).
  // ================================================================================================
  //
  // ---- TWO REASONS, AND THE SECOND IS LOAD-BEARING.
  //
  // A status is a property of the CODE, not of the SITE. It is already what the product does when it thinks
  // about it: `Every_platform_site_answers_a_tenant_authorization_refusal_with_403` asserts the single-code
  // version, and `LocalizationApiErrorMapper` reuses the string `authorization.forbidden` rather than
  // minting its own, so one refusal does not read as two depending on which route answered.
  //
  // **And a caller must not be able to learn WHICH SURFACE refused them from the status code.**
  // `Employee.NotFound` answering 404 on an employee route and 500 on a department route — which is what
  // this product did until T-095 — is a disclosure and an inconsistency at once.
  //
  // ---- STATUS, NOT SPELLING.
  //
  // The code string may follow a site's own convention. `LocalizationApiErrorMapper` projects its own type
  // and cannot reuse the shared `ApiError` objects at all, and `Company.InvalidSelection` answers 403 as
  // `company.scope_denied` at three sites and `authorization.forbidden` at a fourth. **Those are the same
  // answer to the caller's real question** — may I proceed — so only the status is asserted.
  //
  // ---- IT READS SOURCE PLUS REFLECTION, WHICH IS WHAT MAKES IT CHEAP.
  //
  // Invoking every `Map` would need each site's type and a reference to all of them. The arms are
  // `"code" => Constant,` and a constant's status is recoverable, so the pairing needs no invocation.
  [Fact]
  [Trait("Decision", "DEC-L-079")]
  public void The_same_code_answers_the_same_status_at_every_site_that_maps_it()
  {
    var answers = new Dictionary<string, List<(string Site, int Status)>>(StringComparer.Ordinal);

    foreach (var site in Sites())
    {
      foreach (var (code, status) in ArmsIn(site))
      {
        if (!answers.TryGetValue(code, out var recorded))
        {
          answers[code] = recorded = [];
        }

        recorded.Add((site.Site, status));
      }
    }

    // NOT VACUOUS, TWICE. An arm pattern that stopped matching would leave the first empty; a run where no
    // code appeared at two sites would leave the second empty and the comparison below would agree with
    // itself over nothing. `DEC-L-078` applied to this guard rather than to the walk.
    Assert.NotEmpty(answers);

    var shared = answers
      .Where(entry => entry.Value.Select(answer => answer.Site).Distinct(StringComparer.Ordinal).Count() > 1)
      .ToArray();

    Assert.NotEmpty(shared);

    var disagreements = shared
      .Where(entry => entry.Value.Select(answer => answer.Status).Distinct().Count() > 1)
      .Select(entry => $"{entry.Key}: " + string.Join(", ", entry.Value
        .OrderBy(answer => answer.Site, StringComparer.Ordinal)
        .Select(answer => $"{answer.Site}={answer.Status}")))
      .OrderBy(line => line, StringComparer.Ordinal)
      .ToArray();

    // AN EXACT SET, like `KnownUnmapped` and for the same reason: resolving one must edit this list, and a
    // THIRD cannot arrive quietly.
    Assert.Equal(
      KnownDisagreements.OrderBy(line => line, StringComparer.Ordinal),
      disagreements,
      StringComparer.Ordinal);
  }

  // ================================================================================================
  // AND THE PROPERTY `DEC-L-079` WAS ACTUALLY ABOUT: ONE WIRE STRING, ONE STATUS (T-096).
  // ================================================================================================
  //
  // ---- THE ARM-LEVEL GUARD ABOVE IS THIS ONE'S WEAKER SHADOW, AND IT MISSED A REAL DEFECT.
  //
  // It compares which STATUS each error CODE gets, site by site. **It cannot see two sites mapping
  // DIFFERENT constants that carry the SAME wire string** — which is exactly what
  // `department.not_found` and `position.not_found` did: 400 in the import row mapper, 404 on the
  // resource route.
  //
  // **A caller sees strings. It has never seen a constant or an error code.** So the property that matters
  // is the one asserted here, and the arm-level version survives because it catches a different mistake —
  // the same *error* answered differently — one step earlier.
  //
  // ---- IT ENUMERATES EVERY `ApiError` DECLARATION, NOT ONLY THE ONES AN ARM USES.
  //
  // A constant declared and not yet used still fixes a string to a status, and the collision it creates is
  // real the moment something maps to it. Enumerating declarations rather than arms is what makes this
  // catch the defect BEFORE a route starts answering with it.
  [Fact]
  [Trait("Decision", "DEC-L-079")]
  public void A_wire_code_string_carries_one_status_across_the_whole_product()
  {
    var declarations = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

    foreach (var type in MapperTypes())
    {
      foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsInitOnly && field.FieldType.Name == "ApiError"))
      {
        if (field.GetValue(null) is not { } value)
        {
          continue;
        }

        var code = value.GetType().GetProperty("Code")?.GetValue(value) as string;
        var status = value.GetType().GetProperty("StatusCode")?.GetValue(value) as int?;

        if (code is null || status is not { } number)
        {
          continue;
        }

        if (!declarations.TryGetValue(code, out var statuses))
        {
          declarations[code] = statuses = new SortedSet<string>(StringComparer.Ordinal);
        }

        statuses.Add($"{number} ({type.Name}.{field.Name})");
      }
    }

    // NOT VACUOUS. An enumeration that stopped finding declarations would agree with itself over nothing.
    Assert.NotEmpty(declarations);

    var conflicting = declarations
      .Where(entry => entry.Value.Select(line => line.Split(' ')[0]).Distinct(StringComparer.Ordinal).Count() > 1)
      .Select(entry => $"{entry.Key}: {string.Join(", ", entry.Value)}")
      .OrderBy(line => line, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      conflicting.Length == 0,
      "A wire code string is declared with more than one HTTP status. A caller sees the string, so the same " +
      "code means two different outcomes depending on which surface answered — and no amount of agreement " +
      "between ARMS can fix it, because the disagreement is between the constants themselves. Either the " +
      $"two conditions are the same fact and must share a status, or they are not and need different " +
      $"strings:{Environment.NewLine}{string.Join(Environment.NewLine, conflicting)}");
  }

  // ================================================================================================
  // THE TWO DISAGREEMENTS THIS GUARD FOUND ON ITS FIRST RUN. BOTH RULED AND PAID IN T-096.
  // ================================================================================================
  //
  // **`Company.ContextRequired`** answered 400 at Payroll and 403 at four other sites. Ruled 403 in T-096,
  // on the distinction the product already draws: `Company.SelectionRequired` is 400 because the caller
  // must select one; this one is an authorization context that could not be established.
  //
  // **`EmployeeImportRun.InvalidActor`** answered 403 at the employee mapper and 500 at the import
  // contracts. Ruled 500 — which is what T-080 had already ruled at the other site, so one was right and
  // the other was never brought into line.
  //
  // **The list is empty and that is a measurement.** It stays here because a THIRD disagreement must be
  // recorded by a person rather than absorbed, and an empty exact set says so more clearly than no set.
  private static readonly string[] KnownDisagreements = [];

  private static IEnumerable<(string Code, int Status)> ArmsIn(MappingSite site)
  {
    var source = ReadRepositoryFile(site.SourcePath);

    foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex
      .Matches(source, RegexPatterns.MapperArm))
    {
      if (StatusOf(site, match.Groups[2].Value) is { } status)
      {
        yield return (match.Groups[1].Value, status);
      }
    }
  }

  // ---- A BARE CONSTANT IS RESOLVED IN THE SITE'S OWN FILE FIRST, AND THAT IS NOT A DETAIL.
  //
  // The first version resolved a bare `PositionNotFound` by member name across every transport type and
  // found the WRONG ONE: `EmployeeImportExportTransportContracts` declares it as 400 and
  // `PositionApiErrorMapper` declares it as 404, both carrying `position.not_found`. **The guard then
  // reported two disagreements that do not exist.**
  //
  // Same family as `DEC-L-069`'s symbol-versus-code collision, inside the guard rather than in a ruling:
  // the member names are not unique, so an unqualified lookup is not a lookup.
  private static int? StatusOf(MappingSite site, string constant)
  {
    if (!constant.Contains('.', StringComparison.Ordinal))
    {
      var declaration = System.Text.RegularExpressions.Regex.Match(
        ReadRepositoryFile(site.SourcePath),
        $@"readonly\s+ApiError\s+{System.Text.RegularExpressions.Regex.Escape(constant)}\s*=\s*new\(\s*(\d+)");

      return declaration.Success
        ? int.Parse(declaration.Groups[1].Value, CultureInfo.InvariantCulture)
        : null;
    }

    var member = constant[(constant.LastIndexOf('.') + 1)..];
    var owner = constant[..constant.LastIndexOf('.')];
    var ownerName = owner.Contains('.', StringComparison.Ordinal) ? owner[(owner.LastIndexOf('.') + 1)..] : owner;

    var field = MapperTypes()
      .Where(type => type.Name == ownerName)
      .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
      .FirstOrDefault(candidate => candidate.Name == member && candidate.FieldType.Name == "ApiError");

    return field?.GetValue(null) is { } value
      ? value.GetType().GetProperty("StatusCode")?.GetValue(value) as int?
      : null;
  }

  // ---- THE TRANSPORT LAYER, WHICH `ProductTypes()` DELIBERATELY DOES NOT REACH.
  //
  // The closure walks DOWN from the seed handlers, so it never sees an `*.API` assembly — correct for the
  // walk and useless for reading a mapper's own constants. Anchored on the mapper types themselves so the
  // set is deterministic rather than whatever the runtime happens to have loaded, which is the bug T-094
  // shipped and had to fix.
  private static Type[]? mapperTypes;

  // ⚠ EVERY MODULE THE BUILD SHIPS CONTRIBUTES MAPPER TYPES (item 171). The list below is hand-written,
  // so a new module would simply not be scanned and every mapping assertion would stay green over a
  // product it had stopped covering. "Which assemblies are modules" is read from `src/Modules/`.
  [Fact]
  public void Every_module_api_assembly_the_build_ships_contributes_mapper_types()
  {
    var shipped = DeployedProductAssemblies.ModuleProjectNames(".API");
    var scanned = DeployedProductAssemblies.NamesOf(MapperTypes().Select(type => type.Assembly));

    Assert.NotEmpty(shipped);
    Assert.Empty(shipped.Except(scanned, StringComparer.Ordinal));
  }

  private static Type[] MapperTypes() => mapperTypes ??=
  [
    .. new[]
      {
        typeof(SSAS.HR.API.Employees.EmployeeApiErrorMapper).Assembly,
        typeof(SSAS.GL.API.GlApiErrorMapper).Assembly,
        typeof(SSAS.Payroll.API.PayrollApiErrorMapper).Assembly,
        typeof(SSAS.Attendance.API.AttendanceApiErrorMapper).Assembly,
        typeof(SSAS.Platform.API.Companies.CompanyApiErrorMapper).Assembly,
        typeof(SSAS.BuildingBlocks.Api.Transport.ApiErrors).Assembly
      }
      .Distinct()
      .SelectMany(assembly => assembly.GetTypes())
      .Distinct()
  ];

  // Named rather than derived from `Sites()`, so a site silently dropping out of that list fails here
  // instead of reducing the theory to six cases nobody counts.
  public static TheoryData<string> SiteNames => new()
  {
    "AttendanceApiErrorMapper",
    "GlApiErrorMapper",
    "PayrollApiErrorMapper",
    "DepartmentApiErrorMapper",
    "PositionApiErrorMapper",
    "EmployeeApiErrorMapper",
    "EmployeeImportExportTransportContracts",
    "CompanyApiErrorMapper",
    "IdentityAccessApiErrorMapper",
    "LocalizationApiErrorMapper",
    "PlatformSupportAuthorityApiErrorMapper",
    "PlatformAuthentication"
  };

  [Fact]
  public void The_site_list_and_the_named_cases_are_the_same_set()
  {
    Assert.Equal(
      SiteNames.Select(row => (string)row[0]).OrderBy(name => name, StringComparer.Ordinal),
      Sites().Select(site => site.Site).OrderBy(name => name, StringComparer.Ordinal));
  }

  // ---- EVERY DECLARED ERROR IS SOMEBODY'S. THE HALF THAT CATCHES A FAMILY NOBODY CLAIMED.
  //
  // Without this, adding an error in a NEW family — `Timesheet.*` — would be covered by no site and
  // examined by nothing, and every per-site assertion above would stay green. That is the same shape as a
  // route outside an inventory, and it is the way this guard would quietly stop covering the module it
  // was written for.
  // ================================================================================================
  // `UnroutedFamilies` AND THE FAMILY-OWNERSHIP TEST ARE GONE (T-094). THEY WERE NOT PATCHED.
  // ================================================================================================
  //
  // T-093 declared four families as having no HTTP surface, and T-092 routed one of them — `UserEmployeeLink`
  // — one task later. **The only reason anyone noticed is that the task which broke the list also owned it.**
  // That is luck, not a mechanism, and the staleness check could never have caught it: it saw a family that
  // stopped DECLARING errors, never one that gained a ROUTE.
  //
  // **A derived register has no such list.** A family becomes a site's responsibility exactly when that
  // site's closure starts naming its codes, and it stops being one when the closure stops. Nothing to keep
  // current, nothing to go stale.
  //
  // `Every_declared_error_family_is_owned_by_exactly_one_site` went with it. Its job was to catch a family
  // no site claimed; under derivation an unclaimed family is one no route reaches, **which is not a mapping
  // gap and never was** — it was an artefact of declaring responsibility by family.

  // ================================================================================================
  // THE WALK. REFLECTION FOR THE CLOSURE, SOURCE TEXT ONLY FOR WHICH CODES A TYPE NAMES.
  // ================================================================================================
  //
  // ---- WHY REFLECTION AND NOT A SOURCE-TEXT PARSE (`DEC-L-078`).
  //
  // The prototype that measured this task was source text, and **it reported `IBranchWriteAuthorizer` as
  // having no implementation** — `BranchWriteAuthorizer.cs:23` implements it, but the declaration spans
  // lines and ends with a nullable-defaulted parameter, so the regex never saw the base list.
  //
  // **A source-text walk that under-resolves does not report an error. It reports a SMALLER CLOSURE, and
  // every miss looks like a clean stop.** `IsAssignableFrom` cannot do that, and
  // `GetConstructors().GetParameters()` cannot miss a parameter a regex could not parse.
  // ================================================================================================
  // THE FLOOR THE SEED LIST CREATES, HELD BY A TEST RATHER THAN BY A COMMENT (T-120).
  // ================================================================================================
  //
  // `Closure` follows constructor parameters, so a `static` class can never be reached and must be SEEDED.
  // **That makes the seed list a thing somebody has to maintain, and an unmaintained list is how
  // `Payroll.OneOffPaymentElementNotPayable` was a 500 for three tasks while this file reported green.**
  //
  // This does not check mapping. It checks that **every static class returning an error is named by some
  // site**, so the maintenance is visible when it is missed rather than silent.
  //
  // ---- ⚠ AND IT COVERS ONE OF AT LEAST THREE INVISIBLE SHAPES (T-126). READ THIS BEFORE TRUSTING IT.
  //
  // **T-120 stated this floor as "static classes are invisible". That was too narrow, and a floor stated
  // too narrowly is worse than no floor because it reads as complete.** Three shapes the closure cannot
  // reach, found one at a time and each by accident:
  //
  //   STATIC CLASSES     no constructors, never a parameter          T-117, covered by the test below
  //   HANDLERS           simply never added to a site's seeds        T-125, NOT covered
  //   RETURN-ONLY TYPES  a repository RETURNS an aggregate, and the
  //                      walk follows parameter types, not returns   T-125, NOT covered
  //
  // ---- WHY THE TEST BELOW IS NOT WIDENED TO ALL THREE, MEASURED IN T-126.
  //
  // **265 files in `src/` return an error. 195 of them declare no seeded type. The seed list holds 128.**
  // **A guard asserting the list is complete would start red at 195 entries or need a 195-entry exemption
  // list**, which is `UnroutedFamilies`' shape at ten times the scale.
  //
  // **The static-class rule works because that shape is SMALL — twenty-three — and structurally invisible.**
  // The other two are neither.
  //
  // ---- AND A FOURTH, WHICH IS NOT A SHAPE AT ALL. IT IS THIS TEST'S QUESTION NOT APPLYING EVERYWHERE.
  //
  // **"Unmapped" is only meaningful for a route that maps BY CODE.** Some routes answer by SHAPE — they
  // inspect the result and write the status directly, naming no error code anywhere:
  //
  //   PlatformSupportAuthenticationEndpointRouteBuilderExtensions.cs:115
  //     handler = ...GetRequiredService<RefreshPlatformAuthenticationSessionCommandHandler>()
  //     -> Problem(context, 503, "service.unavailable")  /  Problem(context, 401, "authentication.refresh_failed")
  //
  // **That handler is live, routed, unseeded, and both its codes are unmapped — and none of it is a defect,**
  // because there is no code-keyed mapper in the path for anything to fall through. **A route with nothing to
  // fall through cannot have a fall-through bug.**
  //
  // **So the register cannot be made complete even in principle** — not because the walk is weak, but because
  // **the question it asks does not apply uniformly across the product.** Widening the closure would make it
  // reach routes for which its verdict is meaningless.
  //
  // Two notes for whoever tries anyway. **That handler is resolved via `GetRequiredService`, not injected as a
  // route-delegate parameter**, so neither the closure walk nor a parameter scan can see it — being a handler
  // is not what hides it. And an unmapped code found near a shape-answering route is **not** evidence of a
  // 500; check how the route answers before reporting one (T-126 nearly did not).
  //
  // ---- SO WHAT DOES THIS REGISTER ACTUALLY ASSERT?
  //
  // **A property of the SEEDED sites, not of the product.** *For the closures named here, every error they
  // reach is mapped.* It has never asserted that the seeds are complete, and after T-126 it says so.
  //
  // ---- IT IS DELIBERATELY NOT A WIDER WALK.
  //
  // T-120 measured that alternative: following statically-called types by name was transitively unbounded
  // (`CompanyApiErrorMapper` claiming 97 codes across eight modules), and bounding it to one hop and
  // `Name.` syntax still failed nine of twelve sites. **A list that fails loudly beats a walk that
  // over-reports quietly.**
  // ---- THE TWENTY-THREE THAT ARE NOT SEEDED TODAY, NAMED RATHER THAN SILENT.
  //
  // **Each needs a judgement this task cannot make: which mapping site's surface does it answer behind?**
  // `EmployeeImportCsvParser` sits behind the import route; `PositionSearchCriteria` behind a read; the
  // `*ScopeErrors` helpers behind every route of their module. **Seeding one to the wrong site would make
  // that site responsible for codes it does not map, and the fix for a false red is a wrong `KnownUnmapped`
  // entry — which is how a register stops meaning anything.**
  //
  // **This list is the blind spot's measured size.** It is here so a NEW static class returning an error
  // fires immediately, and so the twenty-three are a backlog somebody can see rather than a silence.
  private static readonly string[] KnownUnseeded =
  [
    "SSAS.Attendance.Application.Reads.AttendanceScopeErrors",
    "SSAS.Attendance.Application.Records.EmploymentWindow",
    "SSAS.BuildingBlocks.Localization.LocalizationPlaceholderFormatter",
    "SSAS.BuildingBlocks.Localization.LocalizationPlaceholderParser",
    "SSAS.GL.Application.Reads.GlScopeErrors",
    "SSAS.HR.Application.Departments.DepartmentWriteContext",
    "SSAS.HR.Application.Departments.Reads.DepartmentSearchCriteria",
    "SSAS.HR.Application.Employees.EmployeeStatusTransition",
    "SSAS.HR.Application.ImportExport.EmployeeImportColumns",
    "SSAS.HR.Application.ImportExport.EmployeeImportCsvParser",
    "SSAS.HR.Application.Positions.JobGradeWriteContext",
    "SSAS.HR.Application.Positions.PositionGradeReference",
    "SSAS.HR.Application.Positions.PositionWriteContext",
    "SSAS.HR.Application.Positions.Reads.PositionPagination",
    "SSAS.HR.Application.Positions.Reads.PositionSearchCriteria",
    "SSAS.HR.Application.Positions.SalaryGradeWriteContext",
    "SSAS.Payroll.Application.Reads.PayrollScopeErrors",
    "SSAS.Platform.Application.Common.ApplicationExecutionContext",
    "SSAS.Platform.Application.Localization.LocalizationApplicationValidation",
    "SSAS.Platform.Application.Localization.LocalizationManagementAuditGuard",
    "SSAS.Platform.Application.Localization.LocalizationManagementErrors",
    "SSAS.Platform.Domain.Localization.LocalizationUndoLineage",
    "SSAS.Platform.Domain.TenantStorage.TenantDatabaseBackupChainSelector",
  ];

  [Fact]
  public void Every_static_class_that_returns_an_error_is_seeded_by_some_site()
  {
    var seeded = Sites().SelectMany(site => site.Seeds).ToHashSet();

    var unseeded = ProductTypes()
      .Where(type => type.IsClass && type.IsAbstract && type.IsSealed)   // C# `static class`
      .Where(type => !seeded.Contains(type))
      .Where(type => !KnownUnseeded.Contains(type.FullName, StringComparer.Ordinal))
      .Where(type => SourceByTypeName().TryGetValue(type.Name, out var path)
        && ReturnsAnError(File.ReadAllText(path)))
      .Select(type => type.FullName!)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      unseeded.Length == 0,
      "A static class returns an error and no mapping site seeds it. `Closure` follows CONSTRUCTOR " +
      "PARAMETERS, so a static class is unreachable and its errors are invisible to this register — which " +
      "is how a 500 stood for three tasks while twelve sites reported green. Add it to the seeds of the " +
      $"site whose surface it answers behind:{Environment.NewLine}" +
      string.Join(Environment.NewLine, unseeded));
  }

  // A source that both names an error symbol and fails with one. Either alone is not enough: a mapper names
  // dozens and returns none, and a helper may fail with an error it was handed.
  private static bool ReturnsAnError(string source) =>
    ErrorFieldsBySymbol().Keys.Any(symbol => source.Contains(symbol, StringComparison.Ordinal))
    && source.Contains("Result.Failure", StringComparison.Ordinal);

  private static Type[] Closure(MappingSite site)
  {
    var seen = new HashSet<Type>(site.Seeds);
    var frontier = new Queue<Type>(site.Seeds);

    while (frontier.Count > 0)
    {
      var current = frontier.Dequeue();

      foreach (var parameter in current.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
        .SelectMany(constructor => constructor.GetParameters())
        .Select(parameter => parameter.ParameterType))
      {
        // Only product types. A framework interface — `IHttpContextAccessor` — is where the walk stops,
        // and it stops because nothing of ours implements it, not because a parse failed.
        foreach (var implementation in ProductTypes()
          .Where(candidate => candidate.IsClass && !candidate.IsAbstract && parameter.IsAssignableFrom(candidate)))
        {
          if (seen.Add(implementation))
          {
            frontier.Enqueue(implementation);
          }
        }
      }
    }

    return [.. seen];
  }

  private static string[] ResponsibleCodes(MappingSite site) =>
  [
    .. Closure(site)
      .SelectMany(CodesNamedBy)
      .Distinct(StringComparer.Ordinal)
      .OrderBy(code => code, StringComparer.Ordinal)
  ];

  private static IEnumerable<string> CodesNamedBy(Type type)
  {
    if (!SourceByTypeName().TryGetValue(type.Name, out var path))
    {
      return [];
    }

    var source = File.ReadAllText(path);

    return ErrorFieldsBySymbol()
      .Where(entry => source.Contains(entry.Key, StringComparison.Ordinal))
      .Select(entry => entry.Value);
  }

  // ---- THE PRODUCT'S OWN TYPES, ONCE.
  //
  // Every assembly a seed lives in, plus everything those reference that is ours. Framework assemblies are
  // excluded by name prefix, which is the one place a name is trusted — and trusting it can only make the
  // closure SMALLER in a visible way, because a missing product assembly means a seed cannot be resolved
  // and the non-vacuity assertion fires.
  private static Type[]? productTypes;

  // ---- LOADED DETERMINISTICALLY, NOT WHATEVER HAPPENS TO BE IN THE APPDOMAIN.
  //
  // `AppDomain.CurrentDomain.GetAssemblies()` returns only what the runtime has loaded SO FAR, and .NET
  // loads lazily. Built that way, this guard produced a different closure depending on which tests had run
  // first — **five red sites under a filtered run and ten under the full suite, from the same source.**
  //
  // **`DEC-L-078` a third time, and in the tool again: a smaller closure is not an error, it is a quieter
  // answer.** So the assemblies are reached from the SEEDS and closed over their references explicitly.
  private static Type[] ProductTypes() => productTypes ??=
  [
    .. ProductAssemblies()
      .SelectMany(assembly =>
      {
        try
        {
          return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
          return exception.Types.Where(type => type is not null)!;
        }
      })
      .Distinct()
  ];

  private static Assembly[] ProductAssemblies()
  {
    var seen = new HashSet<Assembly>(Sites().SelectMany(site => site.Seeds).Select(seed => seed.Assembly));
    var frontier = new Queue<Assembly>(seen);

    while (frontier.Count > 0)
    {
      foreach (var reference in frontier.Dequeue().GetReferencedAssemblies()
        .Where(name => name.Name?.StartsWith("SSAS.", StringComparison.Ordinal) == true &&
          name.Name?.EndsWith(".Tests", StringComparison.Ordinal) != true))
      {
        var loaded = Assembly.Load(reference);

        if (seen.Add(loaded))
        {
          frontier.Enqueue(loaded);
        }
      }
    }

    return [.. seen];
  }

  // `SomeErrors.Member` -> the code it carries, resolved by REFLECTION rather than by parsing the literal.
  private static Dictionary<string, string>? errorFields;

  private static Dictionary<string, string> ErrorFieldsBySymbol() => errorFields ??= ProductTypes()
    .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.IsInitOnly && field.FieldType == typeof(Error))
      .Select(field => (Symbol: $"{type.Name}.{field.Name}", Code: ((Error)field.GetValue(null)!).Code)))
    .Where(entry => !string.IsNullOrWhiteSpace(entry.Code))
    .GroupBy(entry => entry.Symbol, StringComparer.Ordinal)
    .ToDictionary(group => group.Key, group => group.First().Code, StringComparer.Ordinal);

  // The declaration pattern, held as a named constant so an editing accident cannot leave a control
  // character inside a verbatim string where it reads as a word boundary. That happened once while this
  // file was being written, and the non-vacuity assertion above is what caught it — `DEC-L-078` applying
  // to the tool rather than to the product.
  private static class RegexPatterns
  {
    // `"Some.Code" => Something.Constant,` — the arm shape every mapper in the product uses.
    public const string MapperArm = @"""([A-Za-z][A-Za-z0-9_.]*)""\s*=>\s*([A-Za-z][A-Za-z0-9_.]*)";

    public const string TypeDeclaration = @"\b(?:class|record|interface|struct)\s+([A-Za-z_]\w*)";
  }

  // Type NAME -> source file. Built once over `src/`. A name declared twice keeps the first: the assertion
  // below reports any closure type that resolves to no file, so an ambiguity that matters shows up as a
  // wrong code set rather than as a silent gap.
  private static Dictionary<string, string>? sourceByTypeName;

  private static Dictionary<string, string> SourceByTypeName() => sourceByTypeName ??= Directory
    .EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
      !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
    .SelectMany(path => System.Text.RegularExpressions.Regex
      .Matches(File.ReadAllText(path), RegexPatterns.TypeDeclaration)
      .Select(match => (Name: match.Groups[1].Value, Path: path)))
    .GroupBy(entry => entry.Name, StringComparer.Ordinal)
    .ToDictionary(group => group.Key, group => group.First().Path, StringComparer.Ordinal);

  // Reflection over the fields themselves. A `static readonly Error` is how every module declares one and
  // the field's own `Code` is the value a mapper switches on — no regex over string literals, which would
  // also match a code quoted in a comment or repeated in a message.
  private static IEnumerable<string> ErrorCodesIn(Assembly assembly) =>
    assembly.GetTypes()
      .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
      .Where(field => field.IsInitOnly && field.FieldType == typeof(Error))
      .Select(field => ((Error)field.GetValue(null)!).Code)
      .Where(code => !string.IsNullOrWhiteSpace(code));


  private static string Format(IEnumerable<string> codes)
  {
    var listed = codes.ToArray();

    return listed.Length == 0 ? "(none)" : string.Join(", ", listed);
  }

  private static string ReadRepositoryFile(string relativePath) =>
    File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));

  private static string RepositoryRoot()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}
