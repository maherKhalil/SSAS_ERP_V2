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
