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

  private static readonly Assembly[] AttendanceAssemblies =
  [
    typeof(SSAS.Attendance.Domain.Calendars.WorkingCalendar).Assembly,
    typeof(SSAS.Attendance.Application.Permissions.AttendancePermissionNames).Assembly
  ];

  private static readonly Assembly[] GlAssemblies =
  [
    typeof(SSAS.GL.Domain.Accounts.Account).Assembly,
    typeof(SSAS.GL.Application.Permissions.GlPermissionNames).Assembly
  ];

  private static readonly Assembly[] PayrollAssemblies =
  [
    typeof(SSAS.Payroll.Domain.Runs.PayrollRun).Assembly,
    typeof(SSAS.Payroll.Application.Permissions.PayrollPermissionNames).Assembly
  ];

  // ---- PLATFORM ENTERED THE REGISTER IN T-093, AND IT CHANGED WHAT THE GUARD COVERS.
  //
  // T-078 and T-079 built a per-site inventory **and stopped at the module boundary.** T-091 then mounted
  // two routes onto `IdentityAccessApiErrorMapper` — the one mapper outside it — and shipped a
  // `400 request.invalid` for a tenant user that does not exist. **Nothing went red, because nothing was
  // looking.**
  //
  // Both Platform assemblies, because `Pagination.Invalid` and the localization codes are declared in
  // Application while the rest are in Domain.
  private static readonly Assembly[] PlatformAssemblies =
  [
    typeof(SSAS.Platform.Domain.TenantUsers.TenantUser).Assembly,
    typeof(SSAS.Platform.Application.Permissions.PlatformPermissionNames).Assembly
  ];

  private static readonly Assembly[] HrAssemblies =
  [
    typeof(SSAS.HR.Domain.Employees.Employee).Assembly,
    typeof(SSAS.HR.Application.Permissions.HrPermissionNames).Assembly
  ];

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
      ], []),

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
      ],
      // ---- ONE CODE, AND IT CROSSES A MODULE BOUNDARY.
      //
      // `PostPayrollRunCommandHandler` posts through `IJournalPoster`, whose implementation returns
      // `Gl.AccountNotFound` when the mapped account is gone. **A GL code reaching a Payroll mapper is
      // exactly the cross-module case this file's header already describes** — and the family register,
      // which gave Payroll the `Payroll` family, could not reach it. Recorded (T-095).
      ["Gl.AccountNotFound"]
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
      // ---- TEN `Employee.*` CODES THE FAMILY-BASED REGISTER COULD NOT SEE.
      //
      // `ChangeEmployeeDepartmentCommandHandler` is invoked by this site's routes and returns `Employee.*`
      // refusals directly. Under `ResponsibleFamilies` this site owned `Department` and nothing else, so an
      // employee-shaped refusal on a department route answered **500** and no guard noticed.
      //
      // That is T-079's finding — a code mapped for one surface and not for the one that raises it —
      // recurring ACROSS families rather than within one, which is the class the old register was
      // structurally blind to. Recorded, not ruled: a status is the surface's decision (T-095).
      [
        "Employee.BranchScopeDenied",
        "Employee.CompanyScopeDenied",
        "Employee.ConcurrencyConflict",
        "Employee.DepartmentUnchanged",
        "Employee.InvalidActor",
        "Employee.InvalidReadScope",
        "Employee.InvalidTransition",
        "Employee.NotFound",
        "Employee.ReadPermissionDenied",
        "Employee.WritePermissionDenied"
      ]
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
      // Ten more, the same shape: `ChangeEmployeePositionCommandHandler` and the position-history read are
      // this site's routes, and both return `Employee.*`. Recorded, not ruled (T-095).
      [
        "Employee.BranchScopeDenied",
        "Employee.CompanyScopeDenied",
        "Employee.ConcurrencyConflict",
        "Employee.InvalidActor",
        "Employee.InvalidReadScope",
        "Employee.InvalidTransition",
        "Employee.NotFound",
        "Employee.PositionUnchanged",
        "Employee.ReadPermissionDenied",
        "Employee.WritePermissionDenied"
      ]
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
      // ---- SIX SCOPE AND ACTOR REFUSALS THE IMPORT SURFACE INHERITS.
      //
      // The import and export handlers resolve an employee read scope, so a caller with no company or
      // branch scope is refused with an `Employee.*` code this site has no arm for. The family register
      // gave this site three `EmployeeImport*` families and could not express that. Recorded (T-095).
      [
        "Employee.BranchScopeDenied",
        "Employee.CompanyScopeDenied",
        "Employee.InvalidActor",
        "Employee.InvalidPagination",
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
