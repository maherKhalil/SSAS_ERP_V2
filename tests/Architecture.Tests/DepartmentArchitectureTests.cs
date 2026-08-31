using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Application.Departments;
using SSAS.HR.Domain.Departments;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Architecture.Tests;

// THE DEPARTMENT BOUNDARIES (FP-007 Phase 1, ADR-026).
//
// Department is the first entity that takes one ownership dimension while deliberately REFUSING another,
// and the first hierarchical aggregate in the product. Both facts are classifications rather than
// behaviours — invisible at the call site, and silent when they regress. These pin the ones with a real
// failure mode.
public sealed class DepartmentArchitectureTests
{
  // ================================================================================================
  // OWNERSHIP — THE CLASSIFICATION THAT IS EASIEST TO GET WRONG
  // ================================================================================================
  //
  // Employee, sitting beside these three, implements all three dimensions. A reader who sees two here will
  // assume the third was forgotten unless something says otherwise. This is that something.
  //
  // The consequence of getting it wrong is not cosmetic: a branch-owned Department would enter the branch
  // write boundary, and every ADR-024 branch transfer would strand the employee's department in a branch
  // where it does not exist — breaking BR-HR-0005 on an operation that has nothing to do with departments.

  [Fact]
  [Trait("Decision", "ADR-026")]
  // ⚠ CITED BY B18 pass 16, body-confirmed: `AC-DEP-0051` verbatim -- the two interfaces PRESENT and
  // `IBranchOwnedEntity` ABSENT, "asserted by an architecture guard so the absence reads as a decision".
  // The positive `Contains` assertions are its own anti-vacuity control: reflection returning nothing
  // fails here rather than passing the ban silently.
  [Trait("Criterion", "AC-DEP-0051")]
  public void Department_is_tenant_and_company_owned_but_never_branch_owned()
  {
    var interfaces = typeof(Department).GetInterfaces();

    Assert.Contains(typeof(ITenantOwnedEntity), interfaces);
    Assert.Contains(typeof(ICompanyOwnedEntity), interfaces);
    Assert.Contains(typeof(IAuditableEntity), interfaces);

    Assert.DoesNotContain(typeof(IBranchOwnedEntity), interfaces);

    Assert.Equal(typeof(AggregateRoot<Guid>), typeof(Department).BaseType);
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public void The_department_manager_is_tenant_and_company_owned_but_never_branch_owned()
  {
    var interfaces = typeof(DepartmentManager).GetInterfaces();

    Assert.Contains(typeof(ITenantOwnedEntity), interfaces);
    Assert.Contains(typeof(ICompanyOwnedEntity), interfaces);

    Assert.DoesNotContain(typeof(IBranchOwnedEntity), interfaces);
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public void The_department_history_is_tenant_and_company_owned_but_never_branch_owned()
  {
    var interfaces = typeof(EmployeeDepartmentAssignment).GetInterfaces();

    Assert.Contains(typeof(ITenantOwnedEntity), interfaces);
    Assert.Contains(typeof(ICompanyOwnedEntity), interfaces);
    Assert.Contains(typeof(IAppendOnlyEntity), interfaces);

    Assert.DoesNotContain(typeof(IBranchOwnedEntity), interfaces);
  }

  // ---- AND NONE OF THE THREE HAS A PROPERTY NAMED BranchId.
  //
  // The naming is defence, not style: a property called BranchId is what a future convention or interface
  // implementation would latch onto to reclassify a type as branch-owned. Same reasoning as TS-EMP-0113.
  [Theory]
  [InlineData(typeof(Department))]
  [InlineData(typeof(DepartmentManager))]
  [InlineData(typeof(EmployeeDepartmentAssignment))]
  [Trait("Decision", "ADR-026")]
  public void No_department_type_has_a_property_named_branch_id(Type type)
  {
    var properties = type
      .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
      .Select(property => property.Name)
      .ToArray();

    Assert.DoesNotContain("BranchId", properties);
  }

  // ---- AND NO BranchId COLUMN REACHES THE COMPOSED MODEL.
  //
  // Asserted from the MODEL rather than from a migration file. TEST-001 established why: a guard that
  // enumerates files or greps text can be green and blind, and this one has to survive a shadow property
  // that no C# property would reveal.
  [Theory]
  [InlineData(typeof(Department))]
  [InlineData(typeof(DepartmentManager))]
  [InlineData(typeof(EmployeeDepartmentAssignment))]
  [Trait("Decision", "ADR-026")]
  // ⚠ CITED BY B18 pass 16, body-confirmed: `AC-DEP-0052` verbatim, INCLUDING ITS INSTRUMENT --
  // "asserted from the composed EF model rather than from a migration file", and this reads
  // `ComposedTenantModel().FindEntityType(...)`. The sibling `No_department_type_has_a_property_named_
  // branch_id` asserts the CLASS, which is the half the criterion explicitly does not ask for.
  //
  // `Assert.Contains("TenantId")` and `("CompanyId")` are the anti-vacuity control: a model that stopped
  // building would fail here rather than satisfy the ban with an empty column list.
  [Trait("Criterion", "AC-DEP-0052")]
  public void No_department_table_has_a_branch_column(Type clrType)
  {
    var entity = ComposedTenantModel().FindEntityType(clrType);

    Assert.NotNull(entity);

    var columns = entity!.GetProperties().Select(property => property.Name).ToArray();

    Assert.DoesNotContain("BranchId", columns);
    Assert.Contains("TenantId", columns);
    Assert.Contains("CompanyId", columns);
  }

  // ================================================================================================
  // THE TENANT MODEL CONTRIBUTION — THE FAILURE THAT WOULD BE SILENT
  // ================================================================================================
  //
  // An entity absent from HrTenantModelContributor is absent from the tenant model, absent from the
  // migration stream, and — because TenantCutoverCopyPlan derives its manifest from the model — absent from
  // Shared→Dedicated cutover. That last one fails silently: the copy validates perfectly against the tables
  // it knows about and leaves the others behind. FP-006C6 exists because it already happened once.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void Every_new_department_entity_reaches_the_composed_tenant_model()
  {
    var model = ComposedTenantModel();

    Assert.NotNull(model.FindEntityType(typeof(Department)));
    Assert.NotNull(model.FindEntityType(typeof(DepartmentManager)));
    Assert.NotNull(model.FindEntityType(typeof(EmployeeDepartmentAssignment)));
  }

  // ---- AND A CONTRIBUTOR-FREE MODEL CONTAINS NONE OF THEM.
  //
  // The negative control for the assertion above. Without it, that test would pass against a model that
  // happened to include these types for some other reason, and would not be proving the contributor did
  // anything.
  [Fact]
  [Trait("Decision", "ADR-012")]
  public void A_contributor_free_model_contains_no_department_entity()
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=model-only;Database=model-only;Integrated Security=True")
      .Options;

    using var context = new TenantDbContext(
      options, new ModelOnlyUser(), new ModelOnlyTenant(), new ModelOnlyClock());

    Assert.Null(context.Model.FindEntityType(typeof(Department)));
    Assert.Null(context.Model.FindEntityType(typeof(DepartmentManager)));
    Assert.Null(context.Model.FindEntityType(typeof(EmployeeDepartmentAssignment)));
  }

  // ================================================================================================
  // THE FOREIGN-KEY GRAPH STAYS ACYCLIC — THE REASON DepartmentManagers IS A TABLE
  // ================================================================================================
  //
  // `Department.ManagerEmployeeId` plus `Employee.DepartmentId` would be a cycle, and
  // `TenantCutoverCopyPlan.Order` returns `CutoverCopyOrderUndecidable` on a cycle rather than resolving it.
  // Cutover would stop working for every tenant.
  //
  // Phase 1 cannot assert the full graph, because `Employee.DepartmentId` does not exist yet. What it CAN
  // assert — and what would have to be deliberately undone to reintroduce the cycle — is that Department
  // holds no foreign key to Employee at all.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public void Department_holds_no_foreign_key_to_employee()
  {
    var department = ComposedTenantModel().FindEntityType(typeof(Department));

    Assert.NotNull(department);

    var principals = department!.GetForeignKeys()
      .Select(foreignKey => foreignKey.PrincipalEntityType.ClrType)
      .ToArray();

    Assert.DoesNotContain(typeof(SSAS.HR.Domain.Employees.Employee), principals);

    var columns = department.GetProperties().Select(property => property.Name).ToArray();

    Assert.DoesNotContain("ManagerEmployeeId", columns);
  }

  // The association table is a dependent of BOTH and a principal of NEITHER, which is what keeps the graph
  // orderable.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public void The_manager_association_depends_on_both_and_is_depended_on_by_neither()
  {
    var model = ComposedTenantModel();
    var manager = model.FindEntityType(typeof(DepartmentManager));

    Assert.NotNull(manager);

    var principals = manager!.GetForeignKeys()
      .Select(foreignKey => foreignKey.PrincipalEntityType.ClrType)
      .ToArray();

    Assert.Contains(typeof(Department), principals);
    Assert.Contains(typeof(SSAS.HR.Domain.Employees.Employee), principals);

    // Nothing points AT it. If something ever did, the graph could cycle again.
    var dependents = model.GetEntityTypes()
      .SelectMany(entity => entity.GetForeignKeys())
      .Where(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(DepartmentManager))
      .ToArray();

    Assert.Empty(dependents);
  }

  // ---- EVERY DEPARTMENT FOREIGN KEY IS RESTRICTED.
  //
  // Departments are deactivated and employees are never deleted at all, so a cascade would describe an
  // event that cannot occur — and would silently erase organizational structure if it ever did.
  [Theory]
  [InlineData(typeof(Department))]
  [InlineData(typeof(DepartmentManager))]
  [InlineData(typeof(EmployeeDepartmentAssignment))]
  [Trait("Decision", "ADR-026")]
  public void No_department_foreign_key_cascades(Type clrType)
  {
    var entity = ComposedTenantModel().FindEntityType(clrType);

    Assert.NotNull(entity);

    foreach (var foreignKey in entity!.GetForeignKeys())
    {
      Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }
  }

  // ================================================================================================
  // NO PHYSICAL DELETE PATH
  // ================================================================================================
  //
  // A department is never physically deleted. The absence of a repository method is the first protection
  // and the RESTRICTED foreign keys are the second; this asserts the first, so it cannot be added back
  // without someone noticing.
  //
  // `ClearManagerAsync` is the one remove in the repository and is deliberately allowed: it removes an
  // ASSOCIATION, which is what "this department has no manager" means. The assertion names it rather than
  // pattern-matching loosely, so a future `DeleteDepartmentAsync` could not hide behind the exemption.
  [Fact]
  [Trait("Decision", "ADR-026")]
  // ⚠ CITED BY B18 pass 16, body-confirmed: `AC-DEP-0032`'s REPOSITORY-METHOD clause. The criterion
  // bans a physical delete by "API route, command, handler or repository method", and this is the only
  // one of the four asserted over MEMBERS rather than type names.
  //
  // ⚠ Two sibling tests carry the other three: `No_department_delete_command_or_handler_exists`
  // (commands and handlers) and `HrRouteInventoryTests.The_hr_surface_exposes_no_delete_verb` (the
  // route). **Three tests in three files for one criterion** -- and enumerating only the two files
  // named `Department*ArchitectureTests` would have recorded this as PARTLY PINNED.
  [Trait("Criterion", "AC-DEP-0032")]
  public void The_department_repository_offers_no_delete()
  {
    var methods = typeof(IDepartmentRepository)
      .GetMethods()
      .Select(method => method.Name)
      .ToArray();

    Assert.DoesNotContain("DeleteAsync", methods);
    Assert.DoesNotContain("RemoveAsync", methods);
    Assert.DoesNotContain("DeleteDepartmentAsync", methods);

    var removals = methods.Where(name =>
        name.Contains("Delete", StringComparison.Ordinal) ||
        name.Contains("Remove", StringComparison.Ordinal))
      .ToArray();

    Assert.Empty(removals);
    Assert.Contains(nameof(IDepartmentRepository.ClearManagerAsync), methods);
  }

  // ================================================================================================
  // MODULE BOUNDARIES ARE UNCHANGED
  // ================================================================================================
  //
  // HR maps foreign keys to Platform principals by TYPE NAME precisely because it cannot reference
  // Platform. Adding three entities is exactly the kind of change that would tempt someone to take the
  // reference for convenience.
  [Fact]
  [Trait("Decision", "ADR-012")]
  public void Hr_still_references_no_platform_assembly()
  {
    var referenced = typeof(HrTenantModelContributor).Assembly
      .GetReferencedAssemblies()
      .Select(assembly => assembly.Name ?? string.Empty)
      .ToArray();

    Assert.DoesNotContain(referenced, name => name.StartsWith("SSAS.Platform", StringComparison.Ordinal));

    var domainReferences = typeof(Department).Assembly
      .GetReferencedAssemblies()
      .Select(assembly => assembly.Name ?? string.Empty)
      .ToArray();

    Assert.DoesNotContain(
      domainReferences, name => name.StartsWith("SSAS.Platform", StringComparison.Ordinal));
  }

  // The composed tenant model — Platform's own entities plus HR's contribution, exactly as the Host builds
  // it. A contributor-free model would contain no Department at all, so every assertion above that looks
  // for one would pass by finding nothing.
  private static IModel ComposedTenantModel()
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=model-only;Database=model-only;Integrated Security=True")
      .Options;

    using var context = new TenantDbContext(
      options,
      new ModelOnlyUser(),
      new ModelOnlyTenant(),
      new ModelOnlyClock(),
      modelContributors: [new HrTenantModelContributor()]);

    return context.Model;
  }

  private sealed class ModelOnlyUser : SSAS.BuildingBlocks.Application.Abstractions.Identity.ICurrentUser
  {
    public string? UserId => "architecture-tests";

    public string? UserName => "architecture-tests";

    public string? Email => null;


    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class ModelOnlyTenant : SSAS.BuildingBlocks.Application.Abstractions.Tenancy.ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class ModelOnlyClock : SSAS.BuildingBlocks.Application.Abstractions.Time.IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}
