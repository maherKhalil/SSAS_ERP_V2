using System.Reflection;
using SSAS.HR.Application.Departments;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Application.Permissions;

namespace SSAS.Architecture.Tests;

// THE DEPARTMENT APPLICATION BOUNDARIES (FP-007 Phase 2, ADR-025, ADR-026).
//
// Most of what makes this slice correct is a SHAPE rather than a behaviour — which command can express
// which change, which operation takes a lock, which dimension a scope carries. A shape is invisible at the
// call site and silent when it regresses, so these pin the ones with a real failure mode.
public sealed class DepartmentApplicationArchitectureTests
{
  private static readonly Assembly HrApplicationAssembly = typeof(CreateDepartmentCommand).Assembly;

  // ================================================================================================
  // THE ORDINARY UPDATE CANNOT MUTATE PARENT, STATUS OR MANAGER
  // ================================================================================================
  //
  // Not "does not" — CANNOT. There is no field to set, so a caller cannot express the change and a reviewer
  // does not have to notice that they did. This is the guard that would catch someone "helpfully" adding a
  // nullable ParentDepartmentId to the update command.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public void The_ordinary_update_command_carries_only_code_and_name()
  {
    var properties = typeof(UpdateDepartmentCommand)
      .GetProperties()
      .Select(property => property.Name)
      .ToArray();

    Assert.Equal(["DepartmentId", "Code", "Name", "RowVersion"], properties);
  }

  // ---- AND NO COMMAND CARRIES A TENANT.
  //
  // Ownership comes from the trusted execution context and is stamped at the persistence boundary. A
  // caller-supplied TenantId would be confirmed rather than trusted anyway, so leaving it off means the
  // question never reaches the boundary.
  [Theory]
  [InlineData(typeof(CreateDepartmentCommand))]
  [InlineData(typeof(UpdateDepartmentCommand))]
  [InlineData(typeof(ChangeDepartmentParentCommand))]
  [InlineData(typeof(MoveDepartmentToRootCommand))]
  [InlineData(typeof(DeactivateDepartmentCommand))]
  [InlineData(typeof(ReactivateDepartmentCommand))]
  [InlineData(typeof(AssignDepartmentManagerCommand))]
  [InlineData(typeof(ClearDepartmentManagerCommand))]
  [Trait("Decision", "ADR-025")]
  public void No_department_command_carries_a_tenant_identifier(Type command)
  {
    var properties = command.GetProperties().Select(property => property.Name).ToArray();

    Assert.DoesNotContain("TenantId", properties);
  }

  // ---- EVERY MUTATION OF AN EXISTING DEPARTMENT CARRIES A CONCURRENCY TOKEN.
  //
  // Create is exempt: there is no prior version of a row that does not exist yet.
  [Theory]
  [InlineData(typeof(UpdateDepartmentCommand))]
  [InlineData(typeof(ChangeDepartmentParentCommand))]
  [InlineData(typeof(MoveDepartmentToRootCommand))]
  [InlineData(typeof(DeactivateDepartmentCommand))]
  [InlineData(typeof(ReactivateDepartmentCommand))]
  [InlineData(typeof(AssignDepartmentManagerCommand))]
  [InlineData(typeof(ClearDepartmentManagerCommand))]
  [Trait("Decision", "ADR-026")]
  // CITED BY B18 pass 20 as `AC-DEP-0048`'s POPULATION half, and bounded: the seven commands are
  // hand-named `[InlineData]`, so this test's name says *every* while its population is a list that
  // nothing checks against the commands that exist. See `A_stale_row_version_refuses_a_move` in the
  // department SQL suite for the full accounting of what this criterion still lacks.
  [Trait("Criterion", "AC-DEP-0048")]
  public void Every_department_mutation_requires_a_row_version(Type command)
  {
    Assert.Contains(command.GetProperties(), property =>
      property.Name == "RowVersion" && property.PropertyType == typeof(byte[]));
  }

  // ================================================================================================
  // HIERARCHY MUTATION IS SERIALIZED, AND THE LOCK IS NOT OPTIONAL
  // ================================================================================================
  //
  // Both hierarchy handlers TAKE the lock as a constructor dependency, so one cannot be built without it.
  // A handler that acquired the lock through a service locator, or skipped it on some branch, would not be
  // caught by a behavioural test that happened not to race.
  [Theory]
  [InlineData(typeof(ChangeDepartmentParentCommandHandler))]
  [InlineData(typeof(MoveDepartmentToRootCommandHandler))]
  [Trait("Decision", "ADR-026")]
  public void Both_hierarchy_handlers_require_the_company_hierarchy_lock(Type handler)
  {
    var parameters = handler.GetConstructors().Single().GetParameters()
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.Contains(typeof(IDepartmentHierarchyLock), parameters);

    // AND a unit of work, because the lock is transaction-owned and is meaningless without one.
    Assert.Contains(parameters, type => type.Name.Contains("UnitOfWork", StringComparison.Ordinal));
  }

  // ---- AND NOTHING ELSE TAKES IT.
  //
  // The lock protects the hierarchy. A lifecycle or manager handler holding it would serialise unrelated
  // work across a whole company for no reason, and would suggest the invariant is broader than it is.
  [Theory]
  [InlineData(typeof(CreateDepartmentCommandHandler))]
  [InlineData(typeof(UpdateDepartmentCommandHandler))]
  [InlineData(typeof(DeactivateDepartmentCommandHandler))]
  [InlineData(typeof(ReactivateDepartmentCommandHandler))]
  [InlineData(typeof(AssignDepartmentManagerCommandHandler))]
  [InlineData(typeof(ClearDepartmentManagerCommandHandler))]
  [Trait("Decision", "ADR-026")]
  public void No_other_handler_takes_the_hierarchy_lock(Type handler)
  {
    var parameters = handler.GetConstructors().Single().GetParameters()
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.DoesNotContain(typeof(IDepartmentHierarchyLock), parameters);
  }

  // ================================================================================================
  // AND THE TWO LISTS ABOVE ARE A PARTITION OF THE REAL POPULATION -- CHECKED, NOT TRUSTED
  // ================================================================================================
  //
  // The two theories above hand-name two handlers and six. Both lists are exactly right TODAY, which is
  // precisely why neither has ever failed and why neither can warn: a NINTH handler would join neither
  // list, be classified by nobody, and leave both theories green while "nothing else takes the lock"
  // quietly stopped being a claim about every handler.
  //
  // So this derives the population from the assembly, classifies each handler by its OBSERVED constructor
  // rather than by the list it appears in, and asserts the two enumerations cover that population exactly
  // and disjointly. Add a handler and this fails until somebody decides which side it belongs on -- which
  // is the decision the enumerations silently assume has already been made.
  //
  // The population keys on the NAMESPACE and not on the name. ChangeEmployeeDepartmentCommandHandler
  // contains "Department" and lives in SSAS.HR.Application.Employees: it is an employee operation, it does
  // not take this lock, and a name filter would pull a real handler in and break the partition with it.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public void The_two_hierarchy_lock_theories_partition_every_department_command_handler()
  {
    var handlers = DepartmentTypesEndingIn("CommandHandler");

    var takesLock = handlers
      .Where(handler => handler.GetConstructors().Single().GetParameters()
        .Any(parameter => parameter.ParameterType == typeof(IDepartmentHierarchyLock)))
      .Select(handler => handler.Name)
      .ToArray();

    // ADR-026: the hierarchy is the only thing this lock protects, so exactly two operations take it.
    Assert.Equal(2, takesLock.Length);

    var declaredTakers = TypesNamedBy(nameof(Both_hierarchy_handlers_require_the_company_hierarchy_lock));
    var declaredOthers = TypesNamedBy(nameof(No_other_handler_takes_the_hierarchy_lock));

    // Each enumeration must match what the constructors actually say -- not merely add up to the total.
    Assert.Equal(
      takesLock.OrderBy(name => name, StringComparer.Ordinal),
      declaredTakers.OrderBy(name => name, StringComparer.Ordinal));

    Assert.Equal(
      handlers.Select(handler => handler.Name)
        .Where(name => !takesLock.Contains(name, StringComparer.Ordinal))
        .OrderBy(name => name, StringComparer.Ordinal),
      declaredOthers.OrderBy(name => name, StringComparer.Ordinal));

    // And the two together are the whole population, with nothing counted twice.
    Assert.Empty(declaredTakers.Intersect(declaredOthers, StringComparer.Ordinal));

    Assert.Equal(
      handlers.Select(handler => handler.Name).OrderBy(name => name, StringComparer.Ordinal),
      declaredTakers.Concat(declaredOthers).OrderBy(name => name, StringComparer.Ordinal));
  }

  // ---- THE SAME PARTITION FOR THE CONCURRENCY TOKEN.
  //
  // Every_department_mutation_requires_a_row_version hand-names seven commands and its NAME SAYS EVERY.
  // Create is the one exemption and the reason is written beside it: there is no prior version of a row
  // that does not exist yet. That is a partition too, and it was equally unchecked -- an eighth mutation
  // would be exempted by nobody's decision and noticed by nobody's test.
  //
  // This is AC-DEP-0048's population clause, and it is what makes the citation on that theory mean what
  // the theory's name claims.
  [Fact]
  [Trait("Decision", "ADR-026")]
  [Trait("Criterion", "AC-DEP-0048")]
  public void Every_department_command_either_carries_a_row_version_or_is_the_exempt_create()
  {
    var commands = DepartmentTypesEndingIn("Command");

    var exempt = commands
      .Where(command => !command.GetProperties().Any(property =>
        property.Name == "RowVersion" && property.PropertyType == typeof(byte[])))
      .Select(command => command.Name)
      .ToArray();

    Assert.Equal(nameof(CreateDepartmentCommand), Assert.Single(exempt));

    // The enumeration in Every_department_mutation_requires_a_row_version is the complement, exactly.
    Assert.Equal(
      commands.Select(command => command.Name)
        .Where(name => name != nameof(CreateDepartmentCommand))
        .OrderBy(name => name, StringComparer.Ordinal),
      TypesNamedBy(nameof(Every_department_mutation_requires_a_row_version))
        .OrderBy(name => name, StringComparer.Ordinal));

    // A command with no handler would be a mutation nobody wrote, and would also make the partition above
    // meaningless -- so the two populations are asserted to be the same size rather than assumed to be.
    Assert.Equal(commands.Length, DepartmentTypesEndingIn("CommandHandler").Length);
  }

  // ---- AND THE THIRD ENUMERATION IN THIS FILE, over the same population and with no exempt side.
  //
  // No_department_command_carries_a_tenant_identifier hand-names all eight commands, so it is the
  // simplest of the three checks: its list IS the population, with nothing to subtract.
  //
  // This is its own [Fact] rather than another assertion inside the rowversion test above. A check about
  // tenant identifiers living in a method named for row versions is exactly the defect B20 is about --
  // a name that does not cover what the body asserts -- and adding one while fixing three would be a
  // poor joke to leave in the file.
  [Fact]
  [Trait("Decision", "ADR-025")]
  public void The_tenant_identifier_theory_enumerates_every_department_command()
  {
    var commands = DepartmentTypesEndingIn("Command");

    Assert.Equal(
      commands.Select(command => command.Name).OrderBy(name => name, StringComparer.Ordinal),
      TypesNamedBy(nameof(No_department_command_carries_a_tenant_identifier))
        .OrderBy(name => name, StringComparer.Ordinal));
  }

  // ---- THE LOCK IS NOT AN IN-PROCESS ONE.
  //
  // A static, a semaphore or a keyed mutex would close the race on one node and leave it wide open on two,
  // which is the failure mode ADR-026 decision 4 specifically rejects. The contract carries no such type,
  // and the SQL implementation is what satisfies it.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public void The_hierarchy_lock_contract_exposes_no_in_process_primitive()
  {
    var members = typeof(IDepartmentHierarchyLock).GetMembers()
      .Select(member => member.Name)
      .ToArray();

    Assert.DoesNotContain(members, name =>
      name.Contains("Semaphore", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Monitor", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Mutex", StringComparison.OrdinalIgnoreCase));

    // The key is derived from tenant AND company, never wider.
    var parameters = typeof(IDepartmentHierarchyLock)
      .GetMethod(nameof(IDepartmentHierarchyLock.AcquireAsync))!
      .GetParameters()
      .Select(parameter => parameter.Name)
      .ToArray();

    Assert.Contains("tenantId", parameters);
    Assert.Contains("companyId", parameters);
  }

  // ================================================================================================
  // THE READ SCOPE CARRIES NO BRANCH DIMENSION
  // ================================================================================================
  //
  // A department is not branch-owned, so branch scope does not decide whether one is VISIBLE. The resolver
  // takes no branch dependency at all, which is a stronger statement than "it does not call one".
  [Fact]
  [Trait("Decision", "ADR-026")]
  public void The_department_scope_carries_no_branch_dimension()
  {
    var scopeProperties = typeof(DepartmentReadScope)
      .GetProperties()
      .Select(property => property.Name)
      .ToArray();

    Assert.DoesNotContain(scopeProperties, name => name.Contains("Branch", StringComparison.Ordinal));

    var resolverParameters = typeof(DepartmentScopeResolver)
      .GetConstructors()
      .Single()
      .GetParameters()
      .Select(parameter => parameter.ParameterType.Name)
      .ToArray();

    Assert.DoesNotContain(resolverParameters, name => name.Contains("Branch", StringComparison.Ordinal));
  }

  // ---- AND THE SCOPE CANNOT BE FABRICATED.
  //
  // Private constructor, internal factory. A read that omitted a scope predicate must not be something a
  // reviewer has to notice, because it must not be something a caller can express — the same guarantee
  // EmployeeReadScope carries, and it is only a guarantee while the factory stays internal.
  [Fact]
  [Trait("Decision", "ADR-026")]
  // ⚠ CITED BY B18 pass 16, body-confirmed: `AC-DEP-0045` CLAUSE 1 -- "cannot be constructed outside
  // its resolver". No public constructors on the scope OR on `AuthorizedDepartmentCompanyScope`, and the
  // `Create` factory is asserted `internal` rather than merely non-public.
  [Trait("Criterion", "AC-DEP-0045")]
  public void The_department_read_scope_cannot_be_constructed_from_outside_the_application()
  {
    Assert.Empty(typeof(DepartmentReadScope).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    Assert.Empty(
      typeof(AuthorizedDepartmentCompanyScope).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

    var factory = typeof(DepartmentReadScope)
      .GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic);

    Assert.NotNull(factory);
    Assert.True(factory!.IsAssembly, "The scope factory must remain internal.");
  }

  // ---- EVERY READ REQUIRES ONE. There is no overload without it.
  [Fact]
  [Trait("Decision", "ADR-026")]
  // ⚠ CITED BY B18 pass 16, body-confirmed: `AC-DEP-0045` CLAUSE 2 -- "no query overload accepts a
  // request without one". Every method's FIRST parameter is the scope, which is stronger than merely
  // containing one.
  //
  // ⚠ And note what it does NOT say: taking a scope is not APPLYING it. `AC-DEP-0044` is the clause
  // about predicates actually being composed, and it is a different assertion in a different suite.
  [Trait("Criterion", "AC-DEP-0045")]
  public void Every_department_read_takes_a_scope()
  {
    foreach (var method in typeof(IDepartmentReadService).GetMethods())
    {
      Assert.Equal(typeof(DepartmentReadScope), method.GetParameters()[0].ParameterType);
    }
  }

  // ================================================================================================
  // PERMISSIONS
  // ================================================================================================

  // Contributed EXPLICITLY. No assembly scan, no attribute discovery — a permission that appears by
  // reflection is one nobody decided to grant.
  [Fact]
  [Trait("Decision", "ADR-012")]
  // CITED BY B18 pass 16, body-confirmed: the CONTRIBUTOR half of `AC-DEP-0043` -- every constant
  // declared in `HrPermissionNames` is offered, and the criterion's own parenthetical about
  // FP-006P's failure is this test's own comment.
  //
  // The criterion also says *the composed IPermissionCatalog*, which this does not read. That half
  // is `EndpointPermissionCatalogJoinTests.Every_permission_an_endpoint_requires_is_defined_by_the_
  // composed_catalog`, resolved from the real host container -- a different suite entirely.
  [Trait("Criterion", "AC-DEP-0043")]
  public void Department_permissions_are_contributed_explicitly_and_completely()
  {
    var offered = new HrPermissionCatalogContributor().Permissions
      .Select(permission => permission.Name)
      .ToArray();

    Assert.Contains(HrPermissionNames.ViewDepartments, offered);
    Assert.Contains(HrPermissionNames.CreateDepartments, offered);
    Assert.Contains(HrPermissionNames.UpdateDepartments, offered);
    Assert.Contains(HrPermissionNames.DeactivateDepartments, offered);

    // NO Delete, and no Manage catch-all.
    Assert.DoesNotContain(offered, name =>
      name.EndsWith(".Delete", StringComparison.Ordinal) ||
      name.EndsWith(".Manage", StringComparison.Ordinal));

    // Every constant named in HrPermissionNames is offered. FP-006P's failure was constants defined
    // nowhere the role-assignment path could see, and every endpoint refusing every caller as a result.
    var declared = typeof(HrPermissionNames)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.IsLiteral && field.FieldType == typeof(string))
      .Select(field => (string)field.GetRawConstantValue()!)
      .ToArray();

    Assert.Equal(
      declared.OrderBy(name => name, StringComparer.Ordinal),
      offered.OrderBy(name => name, StringComparer.Ordinal));
  }

  // ---- NO REFLECTION-BASED PERMISSION DISCOVERY ANYWHERE IN THE DEPARTMENT SLICE.
  [Fact]
  [Trait("Decision", "ADR-012")]
  public void The_department_slice_discovers_no_permission_by_reflection()
  {
    var source = string.Concat(
      ReadApplicationSource("Permissions", "HrPermissionCatalogContributor.cs"),
      ReadApplicationSource("Departments", "Reads", "DepartmentScopeResolver.cs"));

    foreach (var forbidden in new[] { "GetTypes()", "Assembly.Load", "Activator.CreateInstance" })
    {
      Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
    }
  }

  // ================================================================================================
  // NO DELETE PATH
  // ================================================================================================
  //
  // There is no delete command and no delete handler. The repository's absence of a delete method is the
  // other half, guarded in DepartmentArchitectureTests.
  [Fact]
  [Trait("Decision", "ADR-026")]
  // ⚠ CITED BY B18 pass 16, body-confirmed: `AC-DEP-0032`'s COMMAND and HANDLER clauses. It walks the
  // HR application assembly for `Department*` types named Delete/Remove. The repository-method and API-
  // route clauses are asserted elsewhere -- see the note on
  // `DepartmentArchitectureTests.The_department_repository_offers_no_delete`.
  [Trait("Criterion", "AC-DEP-0032")]
  public void No_department_delete_command_or_handler_exists()
  {
    var offenders = HrApplicationAssembly.GetTypes()
      .Where(type => type.Name.Contains("Department", StringComparison.Ordinal))
      .Where(type =>
        type.Name.Contains("Delete", StringComparison.Ordinal) ||
        type.Name.Contains("Remove", StringComparison.Ordinal))
      .Select(type => type.Name)
      .ToArray();

    Assert.Empty(offenders);
  }

  // ================================================================================================
  // MODULE BOUNDARIES
  // ================================================================================================
  //
  // Adding an application slice is exactly the kind of change that would tempt someone to reference
  // Platform for a resolver or an authorizer. HR reaches those through the module-facing tenancy contracts.
  [Fact]
  [Trait("Decision", "ADR-012")]
  public void The_hr_application_still_references_no_platform_assembly()
  {
    var referenced = HrApplicationAssembly
      .GetReferencedAssemblies()
      .Select(assembly => assembly.Name ?? string.Empty)
      .ToArray();

    Assert.DoesNotContain(referenced, name => name.StartsWith("SSAS.Platform", StringComparison.Ordinal));
  }

  // The department application namespace, keyed on rather than matched by name: see
  // The_two_hierarchy_lock_theories_partition_every_department_command_handler for why that matters.
  private static Type[] DepartmentTypesEndingIn(string suffix) =>
    HrApplicationAssembly.GetTypes()
      .Where(type => type.Namespace == "SSAS.HR.Application.Departments")
      .Where(type => type.Name.EndsWith(suffix, StringComparison.Ordinal))
      .ToArray();

  // The [InlineData] values are read as attribute DATA rather than through the xunit discovery API, so
  // this stays a plain reflection read and does not depend on the runner's version.
  private static string[] TypesNamedBy(string testMethod) =>
    typeof(DepartmentApplicationArchitectureTests)
      .GetMethod(testMethod)!
      .GetCustomAttributesData()
      .Where(attribute => attribute.AttributeType.Name == "InlineDataAttribute")
      .Select(attribute => NamedType(attribute).Name)
      .ToArray();

  // [InlineData(typeof(X))] binds to a params object[], and a Type boxed into an object-typed attribute
  // argument arrives wrapped in a further CustomAttributeTypedArgument. Unwrap both shapes.
  private static Type NamedType(CustomAttributeData attribute)
  {
    var argument = ((IReadOnlyList<CustomAttributeTypedArgument>)attribute.ConstructorArguments[0].Value!)
      .Single();

    return (Type)(argument.Value is CustomAttributeTypedArgument nested ? nested.Value! : argument.Value!);
  }

  private static string ReadApplicationSource(params string[] segments)
  {
    var path = Path.Combine(
      [RepositoryRootDirectory(), "src", "Modules", "HR", "SSAS.HR.Application", .. segments]);

    Assert.True(File.Exists(path), path);

    return File.ReadAllText(path);
  }

  // Located by walking up to the solution file rather than by a relative hop count, so the guard survives a
  // change in output directory depth — and by NAME rather than by enumeration, which TEST-001 showed can
  // differ between operating systems.
  private static string RepositoryRootDirectory()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
      directory is not null;
      directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}
