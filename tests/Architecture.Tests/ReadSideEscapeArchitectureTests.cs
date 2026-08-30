using System.Reflection;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// AN EVENT-RAISING AGGREGATE NEVER ESCAPES THROUGH A READ-SIDE SERVICE (item 168, ADR-009).
// ==================================================================================================
//
// Domain-event dispatch collects from `ChangeTracker.Entries()` and nowhere else, so an aggregate that
// raised events while untracked is invisible to it -- no error, no warning, no event. Item 167 established
// that no production path reaches that: of 203 `AsNoTracking` sites, 17 are scalar existence checks, 5 are
// projections, and 8 are read services returning DTOs.
//
// ---- ⚠ WHY THIS GUARDS THE ESCAPE AND NOT THE QUERY.
//
// A ban on `AsNoTracking` would refuse 203 sites across 65 files to prevent a hazard occurring at none of
// them. **A guard whose false positives outnumber its true ones by two orders of magnitude gets deleted
// rather than fixed, and then protects nothing.** The hazard needs the aggregate to ESCAPE to a caller who
// mutates it, so the escape is what is guarded here.
//
// ---- ⚠ AND WHY THERE ARE THREE TESTS RATHER THAN ONE.
//
// A suffix matcher that matches NOTHING passes. `No_read_side_service_returns_an_event_raising_aggregate`
// would be perfectly green over an empty set, so it is worth nothing without a population control -- and a
// population validated by its own matches is the vacuity failure in a new costume.
//
// So the population is cross-checked against an INDEPENDENTLY DERIVED set: the read-side classes item
// 167's `AsNoTracking` census named while looking for something else entirely. And the third test asserts
// the same property from a different direction, keying on nothing this file classifies.
public sealed class ReadSideEscapeArchitectureTests
{
  private static readonly Assembly[] Infrastructure =
  [
    typeof(SSAS.HR.Infrastructure.Persistence.DepartmentConfiguration).Assembly,
    typeof(SSAS.Attendance.Infrastructure.Persistence.WorkingCalendarConfiguration).Assembly,
    typeof(SSAS.GL.Infrastructure.Persistence.AccountConfiguration).Assembly,
    typeof(SSAS.Payroll.Infrastructure.Persistence.PayElementConfiguration).Assembly,
    typeof(SSAS.Platform.Infrastructure.Identity.ActionTokenService).Assembly
  ];

  private static readonly Assembly[] Application =
  [
    typeof(SSAS.HR.Application.Departments.CreateDepartmentCommandHandler).Assembly,
    typeof(SSAS.Attendance.Application.Approval.LeaveApprovalRouter).Assembly,
    typeof(SSAS.GL.Application.Accounts.CreateAccountCommandHandler).Assembly,
    typeof(SSAS.Payroll.Application.Compensation.RecordCompensationCommandHandler).Assembly,
    typeof(SSAS.Platform.Application.Authentication.AuthenticationPolicy).Assembly
  ];

  private static readonly string[] ReadSideSuffixes = ["ReadService", "DirectoryService", "RosterService"];

  // ⚠ NOT the matcher's own output. These are read-side classes item 167's `AsNoTracking` census named
  // while enumerating something else, so they are an independent witness that the matcher below is
  // actually looking where read services live.
  private static readonly string[] CensusWitnesses =
  [
    "DepartmentReadService",
    "EmployeeApproverDirectoryService",
    "EmployeeReadService",
    "EmployeeRosterService",
    "PositionReadService"
  ];

  // ---- THE GUARD.
  [Fact]
  public void No_read_side_service_returns_an_event_raising_aggregate()
  {
    var escapes = ReadSideServices()
      .SelectMany(service => service.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
      .Where(method => Unwrap(method.ReturnType).Any(IsEventRaising))
      .Select(method => method.DeclaringType!.Name + "." + method.Name)
      .Distinct(StringComparer.Ordinal)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Empty(escapes);
  }

  // ---- ⚠ CONTROL ONE: THE POPULATION IS REAL, AND ITS MEMBERS COME FROM SOMEWHERE ELSE.
  [Fact]
  public void The_read_side_population_is_not_empty_and_contains_what_the_census_found()
  {
    var found = ReadSideServices().Select(service => service.Name).ToHashSet(StringComparer.Ordinal);

    Assert.True(found.Count >= 20, $"only {found.Count} read-side services were enumerated; the suffix convention has probably drifted");
    Assert.All(CensusWitnesses, witness => Assert.Contains(witness, found));
  }

  // ---- ⚠ CONTROL TWO: THE AGGREGATE SIDE IS REAL TOO.
  // `IsEventRaising` finding nothing would make the guard green over any return type at all.
  [Fact]
  public void The_aggregate_side_of_the_guard_recognises_known_aggregates()
  {
    Assert.True(IsEventRaising(typeof(SSAS.HR.Domain.Employees.Employee)));
    Assert.False(IsEventRaising(typeof(string)));
  }

  // ==================================================================================================
  // ⚠ THE INDEPENDENT LINE -- AND IT CONTRADICTED ITEM 167, WHICH IS WHY IT IS AN INVENTORY.
  // ==================================================================================================
  //
  // Item 167 closed on "no read service is injected into any command handler". **That was false.** The
  // search behind it looked for three interface names in files named `*CommandHandler*.cs`, and handlers
  // live in files named for their aggregate -- `LeaveCommandHandlers.cs`, plural -- so it enumerated a
  // subset and reported it as the whole.
  //
  // EIGHT command handlers take a read-side service. The hazard is still unreached, but by a DIFFERENT
  // guarantee than 167 claimed: not that read services never reach a command handler, but that
  // `No_read_side_service_returns_an_event_raising_aggregate` holds -- they hand over DTOs.
  //
  // ⚠ So this is pinned as an INVENTORY rather than asserted as an absence. Taking a read service for a
  // DTO is legitimate and a ban would fire on eight correct handlers. What is worth noticing is a NINTH,
  // because a new injection is exactly where an aggregate-returning read would first arrive -- and this
  // keys on CONSTRUCTOR PARAMETERS, classifying no return type, so it fails independently of the guard.
  private static readonly string[] KnownReadSideInjections =
  [
    "BeginTenantAccessCommandHandler(IIdentityTenantMembershipReadService)",
    "CreateTenantLocalizationOverrideCommandHandler(ITenantAuthenticationEligibilityReadService)",
    "RefreshAuthenticationSessionCommandHandler(IIdentityTenantMembershipReadService)",
    "RefreshPlatformAuthenticationSessionCommandHandler(IPlatformSupportPermissionReadService)",
    "RestoreTenantLocalizationDefaultCommandHandler(ITenantAuthenticationEligibilityReadService)",
    "SelectTenantCommandHandler(IIdentityTenantMembershipReadService)",
    "UndoTenantLocalizationOverrideCommandHandler(ITenantAuthenticationEligibilityReadService)",
    "UpdateTenantLocalizationOverrideCommandHandler(ITenantAuthenticationEligibilityReadService)"
  ];

  [Fact]
  public void Command_handlers_taking_a_read_side_service_are_exactly_the_known_inventory()
  {
    var actual = Application
      .SelectMany(assembly => assembly.GetTypes())
      .Where(type => type.IsClass && type.Name.EndsWith("CommandHandler", StringComparison.Ordinal))
      .SelectMany(handler => handler.GetConstructors()
        .SelectMany(constructor => constructor.GetParameters())
        .Where(parameter => IsReadSideName(parameter.ParameterType.Name))
        .Select(parameter => handler.Name + "(" + parameter.ParameterType.Name + ")"))
      .Distinct(StringComparer.Ordinal)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(KnownReadSideInjections.OrderBy(name => name, StringComparer.Ordinal).ToArray(), actual);
  }

  // ---- AND ITS OWN CONTROL: command handlers were actually found.
  [Fact]
  public void The_command_handler_population_is_not_empty()
  {
    var handlers = Application
      .SelectMany(assembly => assembly.GetTypes())
      .Count(type => type.IsClass && type.Name.EndsWith("CommandHandler", StringComparison.Ordinal));

    Assert.True(handlers >= 100, $"only {handlers} command handlers were enumerated");
  }

  private static Type[] ReadSideServices() => Infrastructure
    .SelectMany(assembly => assembly.GetTypes())
    .Where(type => type.IsClass && IsReadSideName(type.Name))
    .ToArray();

  private static bool IsReadSideName(string name) =>
    ReadSideSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal));

  private static bool IsEventRaising(Type type) => typeof(IHasDomainEvents).IsAssignableFrom(type);

  // A return type is rarely the aggregate itself: it arrives inside `Task<>`, `Result<>`,
  // `IReadOnlyList<>` or a combination. Every generic argument is inspected, at any depth.
  private static IEnumerable<Type> Unwrap(Type type)
  {
    yield return type;

    if (!type.IsGenericType)
    {
      yield break;
    }

    foreach (var argument in type.GetGenericArguments())
    {
      foreach (var inner in Unwrap(argument))
      {
        yield return inner;
      }
    }
  }
}
