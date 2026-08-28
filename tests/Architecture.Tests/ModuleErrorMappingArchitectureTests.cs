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
// ---- THE RESPONSIBLE SET IS A STATIC FLOOR, NOT A TOTAL (T-093). T-094 CLOSES IT.
//
// Responsibility is declared by code FAMILY, and the families were chosen by reading the handlers each
// site's routes name. **Errors raised inside services those handlers INJECT are not counted** —
// `ITenantBranchValidator`, `ICompanyContextEstablisher`, `IBranchTopologyGuard` and their kind all
// return Results that flow straight through to these mappers.
//
// **This is not a theoretical limit; it has already been demonstrated twice.**
//
//   1. The manual reachability walk that preceded this register missed `TenantUser.InvalidTransition` at
//      the IdentityAccess site — a code already known to be reachable — because it arrives as
//      `domainResult` from the aggregate rather than through an `*Errors.*` symbol in the handler file.
//   2. That same walk put Company at ONE wrong code. **The register found five more**, all of them
//      `CompanyAccessErrors` raised by the company-context establisher — an injected service.
//
// **A floor that has been shown to be a floor is a stronger warning than one asserted in the abstract**,
// which is why both demonstrations are written down here rather than summarised.
//
// **T-094 replaces the statically-named handler set with a transitive walk.** When it lands, the exact-set
// registers below go RED and force the work — that is the guard doing its job, not a regression.
//
// ---- A SITE MAY MAP CODES OUTSIDE ITS FAMILIES, AND THAT IS NOT PENALISED.
//
// The import site maps three `Employee.Position*` codes because an import surfaces employee errors. Only
// the families a site is RESPONSIBLE for are asserted; anything extra is a site being helpful.
public sealed class ModuleErrorMappingArchitectureTests
{
  private sealed record MappingSite(
    string Site,
    Assembly[] DeclaringAssemblies,
    string SourcePath,
    string[] ResponsibleFamilies,
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
    new("AttendanceApiErrorMapper", AttendanceAssemblies,
      Path.Combine("src", "Modules", "Attendance", "SSAS.Attendance.API", "AttendanceApiErrorMapper.cs"),
      ["Attendance"], []),

    new("GlApiErrorMapper", GlAssemblies,
      Path.Combine("src", "Modules", "Finance", "SSAS.GL.API", "GlApiErrorMapper.cs"),
      ["Gl"], []),

    // ---- TWO OVERTIME-TIER ERRORS DECLARED AND NOT MAPPED. FOUND, DELIBERATELY NOT FIXED.
    //
    // A status code is a contract decision owned by the surface, not by the test that noticed.
    new("PayrollApiErrorMapper", PayrollAssemblies,
      Path.Combine("src", "Modules", "Payroll", "SSAS.Payroll.API", "PayrollApiErrorMapper.cs"),
      ["Payroll"], []),

    new("DepartmentApiErrorMapper", HrAssemblies,
      Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Departments", "DepartmentApiErrorMapper.cs"),
      ["Department"], []),

    new("PositionApiErrorMapper", HrAssemblies,
      Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Positions", "PositionApiErrorMapper.cs"),
      ["Position"], []),

    // ---- SIX. THE DEFECT THAT PROVED THE MODULE-LEVEL VERSION WRONG.
    //
    // `PositionEndpointRouteBuilderExtensions.cs:806-808` says *"The EMPLOYEE mapper ... Its
    // `Employee.Position*` arms are the ones that describe an unusable destination here."* **There are
    // none.** `POST /api/hr/employees` with an unknown position and every `Employee.Position*` refusal on
    // `change-position` answer 500 today.
    //
    // They are listed together because `BR-PLT-0002` requires them to share a wire answer: mapping some
    // while others fall through to 500 makes the two distinguishable, which is the disclosure the
    // collapse exists to prevent. **Ruling three of six would be worse than ruling none.**
    new("EmployeeApiErrorMapper", HrAssemblies,
      Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Employees", "EmployeeApiErrorMapper.cs"),
      ["Employee"], []),

    // ---- FIVE MORE, FOUND ONLY BY SPLITTING THE SITES. ALL REACHABLE, ALL VERIFIED.
    //
    // `ImportEmployeesCommandHandler.cs:624-626` returns the run factory's failure directly, and
    // `ExportEmployeesQueryHandler.cs:174-177` does the same. `ImportKey.Create`'s failure is returned at
    // `ImportEmployeesCommandHandler.cs:97-101`. So a bad import key, file name or row count answers 500.
    //
    // **These were nearly reported as unreachable.** A search for `EmployeeImportRun.Create` returned no
    // call sites — because the factories are `Validated`, `Applied` and `Refused`. An absence is only
    // evidence when the name searched for is the right one.
    new("EmployeeImportExportTransportContracts", HrAssemblies,
      Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Employees", "EmployeeImportExportTransportContracts.cs"),
      ["EmployeeImport", "EmployeeImportRun", "EmployeeExportRun"], []),

    // ================================================================================================
    // PLATFORM — FIVE SITES, ADDED IN T-093.
    // ================================================================================================

    new("CompanyApiErrorMapper", PlatformAssemblies,
      Path.Combine("src", "Platform", "SSAS.Platform.API", "Companies", "CompanyApiErrorMapper.cs"),
      ["Company"],
      // ---- FIVE `CompanyAccessErrors` WERE THE EVIDENCE FOR THE FLOOR WARNING ABOVE, AND ARE NOW MAPPED.
      //
      // All five are raised by the company-context establisher — an INJECTED SERVICE, exactly the class of
      // source the static reachability walk does not see. The manual estimate that preceded this register
      // put Company at one wrong code; **the register found five more, and T-093b ruled and mapped them.**
      //
      // The debt is empty, and that is a measurement rather than a default: it means every declared
      // `Company.*` code has an arm someone chose.
      []),

    // ---- THE SITE T-091 SHIPPED A DEFECT ONTO.
    //
    // It answers for the roles read AND for T-091's two tenant-user lifecycle routes. Its arms covered
    // three codes; five reachable ones fell through to a default of `400 request.invalid`.
    new("IdentityAccessApiErrorMapper", PlatformAssemblies,
      Path.Combine("src", "Platform", "SSAS.Platform.API", "IdentityAccess", "IdentityAccessApiErrorMapper.cs"),
      ["TenantUser", "Role", "Common", "Persistence", "Authorization", "Identity", "Invitation", "Permission", "Tenant", "UserEmployeeLink"],
      // ---- TWENTY-SIX, AND ALMOST ALL OF THEM ARE DECLARED FOR SURFACES THAT DO NOT EXIST.
      //
      // T-092's Part 1 measured it: **fifteen Platform permissions are declared and required by no route**,
      // and seven of the nine tenant-user handlers have no transport at all. Role management, tenant
      // lifecycle and invitation completion are all handlers-and-DI with nothing mounted.
      //
      // **So these are not a mapping backlog; they are the shadow of an unbuilt surface.** They are listed
      // rather than excused because the day one of those routes is mounted, this list is what has to be
      // edited — and the edit is where someone decides the status.
      [
        "Identity.Invalid",
        "Invitation.ActiveMembership",
        "Invitation.DeactivatedMembership",
        "Permission.Invalid",
        "Role.HasActiveUsers",
        "Role.InvalidName",
        "Role.InvalidTransition",
        "Role.NotAssignable",
        "Role.PermissionAlreadyAssigned",
        "Role.PermissionAssignmentNotFound",
        "Role.PlatformPermissionRejected",
        "Role.ProtectedSystemRole",
        "Tenant.CodeExists",
        "Tenant.InvalidActor",
        "Tenant.InvalidCode",
        "Tenant.InvalidName",
        "Tenant.InvalidTransition",
        "Tenant.InvalidTransitionReason",
        "Tenant.Mismatch",
        "Tenant.NotFound",
        "Tenant.Required",
        "TenantUser.Inactive",
        "TenantUser.InvalidDisplayName",
        "TenantUser.InvalidEmail",
        "TenantUser.RoleAlreadyAssigned",
        "TenantUser.RoleAssignmentNotFound"
      ]),

    // ---- ITS DEFAULT IS NOT IN THIS FILE, WHICH IS WHY READING THE MAPPER DOES NOT REVEAL IT.
    //
    // `TryMap` returns a bool with a `null!` sentinel; the fallback lives at
    // `LocalizationEndpointRouteBuilderExtensions.cs:179-181`. Two spellings of one decision.
    new("LocalizationApiErrorMapper", PlatformAssemblies,
      Path.Combine("src", "Platform", "SSAS.Platform.API", "Localization", "LocalizationApiErrorMapper.cs"),
      ["localization"],
      // Both were raised by the management audit guard and unruled; ruled and mapped in T-093b.
      []),

    new("PlatformSupportAuthorityApiErrorMapper", PlatformAssemblies,
      Path.Combine("src", "Platform", "SSAS.Platform.API", "PlatformSupport", "PlatformSupportAuthorityApiErrorMapper.cs"),
      ["PlatformSupport"],
      // Two more the static walk missed and the register found; ruled and mapped in T-093b.
      []),

    // ================================================================================================
    // AUTHENTICATION HAS NO MAPPER, AND THAT IS A RULING RATHER THAN A HOLE.
    // ================================================================================================
    //
    // **Every ordinary authority failure collapses to one generic 401** — asserted by
    // `Every_ordinary_authority_failure_returns_the_same_generic_401`, because distinguishing "no such
    // account" from "wrong password" is the disclosure the collapse exists to prevent.
    //
    // **So every code in these families is deliberately unmapped, and all of them are listed below.**
    // Registering the site rather than omitting it is the point: an omitted site is indistinguishable
    // from an oversight, and the next sweep rediscovers it as a hole. A NEW authentication error now
    // has to be added to `KnownUnmapped` by a person, who will meet this paragraph while doing it.
    //
    // The source path is the route file, which contains no arms at all — so the guard reads a real file
    // and finds nothing mapped, which is exactly the state being recorded.
    new("PlatformAuthentication", PlatformAssemblies,
      Path.Combine("src", "Platform", "SSAS.Platform.API", "Authentication", "AuthenticationEndpointRouteBuilderExtensions.cs"),
      ["Authentication", "AuthenticationAccount", "AuthenticationSession", "AccountActionToken"],
      [
        "AccountActionToken.Invalid",
        "AccountActionToken.InvalidHash",
        "AccountActionToken.SensitiveValueConsumed",
        "Authentication.AccessTokenUnavailable",
        "Authentication.CompromisedPassword",
        "Authentication.Failed",
        "Authentication.InvalidAccessTokenClaims",
        "Authentication.InvalidClientId",
        "Authentication.InvalidLoginEmail",
        "Authentication.InvalidPassword",
        "Authentication.NoEligibleMembership",
        "Authentication.PasswordCheckUnavailable",
        "Authentication.TenantSelectionFailed",
        "AuthenticationAccount.InvalidTransition",
        "AuthenticationAccount.PasswordNotAllowed",
        "AuthenticationAccount.PasswordRequired",
        "AuthenticationSession.Invalid",
        "AuthenticationSession.RefreshFailed"
      ])
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
      $"{siteName} is responsible for families [{string.Join(", ", site.ResponsibleFamilies)}] and no " +
      "declared Error matches any of them. Either the family is misspelt or the sweep is broken — an " +
      "empty enumeration would make the assertion below pass over nothing.");

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
  // ---- FOUR FAMILIES HAVE NO HTTP SURFACE AT ALL, AND THAT IS RECORDED RATHER THAN INFERRED (T-093).
  //
  // Widening the register to Platform surfaced these: every code in them is declared, none is reachable
  // from any route, and **no mapping site exists because there is nothing to map them for.**
  //
  //   Branch            no branch routes exist; `TenantBranchService` is invoked from other handlers
  //   Subscription      `Subscriptions/` holds one entitlement type and no route file
  //   TenantStorage     117 codes, all operational — backup, cutover, restore verification
  //
  // **`UserEmployeeLink` WAS ON THIS LIST FOR ONE TASK AND HAS LEFT IT.** T-093 recorded it as unrouted
  // because nothing called `UserEmployeeLink.Create`; T-092 built the routes, and it moved to
  // `IdentityAccessApiErrorMapper`'s responsible families in the same change.
  //
  // ---- AND THAT MOVE WAS MADE BY A PERSON, NOT BY THIS GUARD. THE LIMIT IS WORTH KNOWING.
  //
  // The staleness check below catches a family that stops DECLARING errors. **It cannot catch a family that
  // gains a ROUTE** — that would need the route-to-mapper derivation this file explains is not tractable
  // here. So an entry that becomes wrong stays green until someone notices, and the one time it happened
  // the someone was the task that caused it, one task later.
  //
  // **T-094's transitive walk is what would make this mechanical.** Until then the list is only as current
  // as the last person to touch the surface it describes.
  //
  // **This is a DECLARATION, on exactly the same footing as `ResponsibleFamilies`, and for the reason
  // this file already gives:** there is no call graph to walk, so "unrouted" cannot be derived. A wrong
  // declaration here hides a family instead of accusing one, which is the more dangerous direction — so
  // it is an EXACT set. A fifth family cannot join it without a person writing it down.
  //
  // **The day one of these gains a route, this list is what has to be edited**, and the edit is where
  // someone decides which site answers for it.
  private static readonly string[] UnroutedFamilies =
    ["Branch", "Subscription", "TenantStorage"];

  [Fact]
  public void Every_declared_error_family_is_owned_by_exactly_one_site()
  {
    var claimed = Sites()
      .SelectMany(site => site.ResponsibleFamilies)
      .Concat(UnroutedFamilies)
      .ToArray();

    Assert.Equal(claimed.Length, claimed.Distinct(StringComparer.Ordinal).Count());

    var declared = Sites()
      .SelectMany(site => site.DeclaringAssemblies)
      .Distinct()
      .SelectMany(ErrorCodesIn)
      .Select(Family)
      .Distinct(StringComparer.Ordinal)
      .OrderBy(family => family, StringComparer.Ordinal)
      .ToArray();

    var unclaimed = declared
      .Where(family => !claimed.Contains(family, StringComparer.Ordinal))
      .ToArray();

    Assert.True(
      unclaimed.Length == 0,
      "A declared error family is claimed by no mapping site, so nothing asserts its codes are mapped and " +
      "every arm of this guard passes over it. Give it to the site that answers for the routes raising " +
      "it, or — if it has no HTTP surface at all — to `UnroutedFamilies`, with the reason:" +
      $"{Environment.NewLine}{string.Join(Environment.NewLine, unclaimed)}");

    // NOT VACUOUS IN THE OTHER DIRECTION EITHER. A family listed as unrouted that no longer exists would
    // sit here forever, quietly excusing nothing — and would hide the day its name was reused.
    var stale = UnroutedFamilies
      .Where(family => !declared.Contains(family, StringComparer.Ordinal))
      .ToArray();

    Assert.True(
      stale.Length == 0,
      "A family is listed in `UnroutedFamilies` but no longer declares any error. Remove it: an excuse " +
      $"for something that does not exist is an excuse waiting to cover something that does:{Environment.NewLine}" +
      string.Join(Environment.NewLine, stale));
  }

  private static string[] ResponsibleCodes(MappingSite site) =>
  [
    .. site.DeclaringAssemblies
      .Distinct()
      .SelectMany(ErrorCodesIn)
      .Where(code => site.ResponsibleFamilies.Contains(Family(code), StringComparer.Ordinal))
      .Distinct(StringComparer.Ordinal)
      .OrderBy(code => code, StringComparer.Ordinal)
  ];

  // Reflection over the fields themselves. A `static readonly Error` is how every module declares one and
  // the field's own `Code` is the value a mapper switches on — no regex over string literals, which would
  // also match a code quoted in a comment or repeated in a message.
  private static IEnumerable<string> ErrorCodesIn(Assembly assembly) =>
    assembly.GetTypes()
      .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
      .Where(field => field.IsInitOnly && field.FieldType == typeof(Error))
      .Select(field => ((Error)field.GetValue(null)!).Code)
      .Where(code => !string.IsNullOrWhiteSpace(code));

  private static string Family(string code)
  {
    var separator = code.IndexOf('.', StringComparison.Ordinal);

    return separator <= 0 ? code : code[..separator];
  }

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
