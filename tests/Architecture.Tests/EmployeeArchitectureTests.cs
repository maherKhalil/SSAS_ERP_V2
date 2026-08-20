using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Application.Employees;
using SSAS.HR.Domain.Employees;

namespace SSAS.Architecture.Tests;

// THE EMPLOYEE BOUNDARIES (FP-006C3, ADR-014 r1.1, ADR-023, ADR-024, ADR-025).
//
// Employee is the first business record owned along all three dimensions, so most of what makes it correct
// is a CLASSIFICATION rather than a behaviour — and a classification is invisible at the call site and
// silent when it regresses. These pin the ones with a real failure mode.
public sealed class EmployeeArchitectureTests
{
  private static readonly Assembly HrDomainAssembly = typeof(Employee).Assembly;

  private static readonly Assembly HrInfrastructureAssembly =
    typeof(SSAS.HR.Infrastructure.Persistence.HrTenantModelContributor).Assembly;

  // ---- EMPLOYEE CARRIES ALL THREE OWNERSHIP DIMENSIONS, and is the first production entity to do so.
  [Fact]
  public void Employee_is_tenant_company_and_branch_owned()
  {
    var interfaces = typeof(Employee).GetInterfaces();

    Assert.Contains(typeof(ITenantOwnedEntity), interfaces);
    Assert.Contains(typeof(ICompanyOwnedEntity), interfaces);
    Assert.Contains(typeof(IBranchOwnedEntity), interfaces);
    Assert.Contains(typeof(IAuditableEntity), interfaces);

    Assert.Equal(typeof(AggregateRoot<Guid>), typeof(Employee).BaseType);
  }

  // ================================================================================================
  // TS-EMP-0113 — THE CLASSIFICATION THAT IS EASIEST TO GET WRONG
  // ================================================================================================
  //
  // A transfer record spans a branch boundary and belongs to neither side. If it were branch-owned it would
  // enter the branch write boundary, where the trusted context during a transfer is the SOURCE while the
  // record's subject is the DESTINATION — making transfer unrepresentable.
  [Fact]
  public void The_branch_assignment_is_tenant_and_company_owned_but_never_branch_owned()
  {
    var interfaces = typeof(EmployeeBranchAssignment).GetInterfaces();

    Assert.Contains(typeof(ITenantOwnedEntity), interfaces);
    Assert.Contains(typeof(ICompanyOwnedEntity), interfaces);
    Assert.Contains(typeof(IAppendOnlyEntity), interfaces);

    Assert.DoesNotContain(typeof(IBranchOwnedEntity), interfaces);
  }

  // ---- AND NEITHER BRANCH COLUMN IS NAMED `BranchId`.
  //
  // The naming is defence, not style: a property called BranchId is what a future convention or interface
  // implementation would latch onto to reclassify the type as branch-owned.
  [Fact]
  public void The_branch_assignment_has_no_property_named_branch_id()
  {
    var properties = typeof(EmployeeBranchAssignment)
      .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
      .Select(property => property.Name)
      .ToArray();

    Assert.DoesNotContain("BranchId", properties);
    Assert.Contains(nameof(EmployeeBranchAssignment.SourceBranchId), properties);
    Assert.Contains(nameof(EmployeeBranchAssignment.DestinationBranchId), properties);
  }

  // ---- THE HISTORY HAS NO CONCURRENCY STATE, NO MODIFICATION METADATA AND NO CLOSING DATE.
  //
  // Each absence is a decision: a record that is never updated has nothing to protect, nothing to stamp, and
  // closing an interval would mean updating the previous row.
  [Fact]
  public void The_branch_assignment_has_no_rowversion_modification_or_closing_date()
  {
    var type = typeof(EmployeeBranchAssignment);

    Assert.Null(type.GetProperty("RowVersion"));
    Assert.Null(type.GetProperty("ModifiedUtc"));
    Assert.Null(type.GetProperty("ModifiedBy"));
    Assert.Null(type.GetProperty("EffectiveToUtc"));
    Assert.NotNull(type.GetProperty(nameof(EmployeeBranchAssignment.EffectiveFromUtc)));
  }

  // ================================================================================================
  // WHAT V1 DELIBERATELY DOES NOT HAVE
  // ================================================================================================

  // BR-HR-0005, BR-HR-0006 and BR-HR-0007 are retained as binding and deferred (DEC-EMP-0017/0018/0031).
  // No placeholder column stands in for them, because a placeholder is how a deferral quietly becomes a
  // design.
  //
  // ---- UPDATED BY FP-007 PHASE 1, AND ONLY WHERE THE APPROVED SCOPE CHANGED IT.
  //
  // The Employee half is untouched and still binding: Phase 1 introduces the Department AGGREGATE and adds
  // nothing whatsoever to Employee. `Employee.DepartmentId` arrives in a later phase, and until it does its
  // absence is exactly as load-bearing as it was.
  //
  // The second half used to assert that no Department type existed anywhere in HR. That was correct while
  // Department was deferred and is now superseded — the aggregate exists. Position is NOT superseded, so
  // that clause is kept in full: `BR-HR-0006` is still deferred, and no placeholder may stand in for it.
  [Fact]
  public void Employee_has_no_department_position_or_manager()
  {
    var properties = typeof(Employee)
      .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
      .Select(property => property.Name)
      .ToArray();

    Assert.DoesNotContain(properties, name =>
      name.Contains("Department", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Position", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Manager", StringComparison.OrdinalIgnoreCase));

    // POSITION IS STILL DEFERRED WHOLE. No type, anywhere in HR.
    Assert.DoesNotContain(HrDomainAssembly.GetTypes(), type =>
      type.Name.Contains("Position", StringComparison.OrdinalIgnoreCase));

    // AND NO EMPLOYEE REPORTING LINE. `BR-HR-0007` presumes an employee-to-manager relationship that no
    // authority defines; a department has a manager, an employee does not. `DepartmentManager` is the
    // department's, which is why it is excluded by name rather than by the loose pattern above.
    Assert.DoesNotContain(HrDomainAssembly.GetTypes(), type =>
      type.Name.Contains("Manager", StringComparison.OrdinalIgnoreCase) &&
      type.Name != nameof(SSAS.HR.Domain.Departments.DepartmentManager));
  }

  // Automatic per-company numbering is deferred (DEC-EMP-0011): the number is a required INPUT, so a future
  // generator is additive rather than a redesign.
  [Fact]
  public void No_employee_number_generator_exists()
  {
    var types = HrDomainAssembly.GetTypes().Concat(HrInfrastructureAssembly.GetTypes())
      .Select(type => type.Name)
      .ToArray();

    Assert.DoesNotContain(types, name =>
      name.Contains("Sequence", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("NumberGenerator", StringComparison.OrdinalIgnoreCase));

    // The create command REQUIRES the number: it is not nullable and not optional.
    var parameter = typeof(CreateEmployeeCommand).GetConstructors().Single()
      .GetParameters()
      .Single(candidate => candidate.Name == "EmployeeNumber");

    Assert.Equal(typeof(string), parameter.ParameterType);
    Assert.False(parameter.IsOptional);
  }

  // Rehire is deferred: there is no transition out of Terminated and no operation named for one.
  [Fact]
  public void No_rehire_operation_exists()
  {
    Assert.DoesNotContain(
      HrDomainAssembly.GetTypes().SelectMany(type => type.GetMethods()).Select(method => method.Name),
      name => name.Contains("Rehire", StringComparison.OrdinalIgnoreCase));
  }

  // ================================================================================================
  // WHAT THE CONTRACTS REFUSE TO EXPRESS
  // ================================================================================================

  // ---- THE UPDATE COMMAND CANNOT EXPRESS A RELOCATION, A COMPANY MOVE OR A LIFECYCLE CHANGE.
  //
  // Omission at the contract level is the first of two protections; the shared write boundaries are the
  // second. This pins the first, which is the one a reviewer cannot see from the boundary code.
  [Fact]
  public void The_update_command_carries_no_ownership_or_status()
  {
    var parameters = typeof(UpdateEmployeeProfileCommand).GetConstructors().Single()
      .GetParameters()
      .Select(parameter => parameter.Name!)
      .ToArray();

    Assert.DoesNotContain(parameters, name =>
      name.Contains("Tenant", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Company", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Branch", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Status", StringComparison.OrdinalIgnoreCase) ||
      name.Equals("EmployeeNumber", StringComparison.Ordinal));

    // And it carries the concurrency token, so an update cannot be applied to state the caller never saw.
    Assert.Contains("ExpectedRowVersion", parameters);
  }

  // The CREATE command carries no ownership either: tenant, company and branch all come from the trusted
  // execution context, so the question never reaches the boundary.
  [Fact]
  public void The_create_command_carries_no_ownership()
  {
    var parameters = typeof(CreateEmployeeCommand).GetConstructors().Single()
      .GetParameters()
      .Select(parameter => parameter.Name!)
      .ToArray();

    Assert.DoesNotContain(parameters, name =>
      name.Contains("Tenant", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Company", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Branch", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Status", StringComparison.OrdinalIgnoreCase));
  }

  // ---- ONLY THE TRANSFER COMMAND NAMES A BRANCH, and it names the DESTINATION only: the source is the
  // record's current branch, never something a request may assert.
  [Fact]
  public void Only_the_transfer_command_names_a_branch_and_only_the_destination()
  {
    var parameters = typeof(TransferEmployeeCommand).GetConstructors().Single()
      .GetParameters()
      .Select(parameter => parameter.Name!)
      .ToArray();

    Assert.Contains("DestinationBranchId", parameters);

    // NO SOURCE BRANCH. `InactiveSourceRecovery` names a MODE, not a branch: the source is always the
    // record's current branch, so the DESTINATION is the only branch the command may name.
    Assert.Equal(
      ["DestinationBranchId"],
      parameters.Where(name => name.Contains("Branch", StringComparison.OrdinalIgnoreCase)).ToArray());

    // No other Employee command mentions a branch at all.
    foreach (var command in new[]
      { typeof(CreateEmployeeCommand), typeof(UpdateEmployeeProfileCommand), typeof(TerminateEmployeeCommand) })
    {
      Assert.DoesNotContain(
        command.GetConstructors().Single().GetParameters(),
        parameter => parameter.Name?.Contains("Branch", StringComparison.OrdinalIgnoreCase) == true);
    }
  }

  // ---- NO PHYSICAL DELETE SURFACE ANYWHERE IN HR.
  [Fact]
  public void No_employee_delete_operation_is_exposed()
  {
    Assert.DoesNotContain(
      typeof(IEmployeeRepository).GetMethods().Select(method => method.Name),
      name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Remove", StringComparison.OrdinalIgnoreCase));

    var deleteSurface = HrDomainAssembly.GetTypes().Concat(HrInfrastructureAssembly.GetTypes())
      .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
      .Where(method => method.DeclaringType?.Assembly == HrDomainAssembly ||
        method.DeclaringType?.Assembly == HrInfrastructureAssembly)
      .Select(method => method.Name)
      .Where(name => Regex.IsMatch(name, @"^Delete(Employee)?(Async)?$", RegexOptions.CultureInvariant))
      .ToArray();

    Assert.Empty(deleteSurface);
  }

  // ================================================================================================
  // LAYERING
  // ================================================================================================

  // ---- HR DOMAIN AND APPLICATION STAY FREE OF PERSISTENCE, and Platform never depends on HR.
  [Fact]
  public void Hr_layers_stay_clean_and_platform_never_depends_on_hr()
  {
    foreach (var assembly in new[] { HrDomainAssembly, typeof(IEmployeeRepository).Assembly })
    {
      Assert.DoesNotContain(
        assembly.GetReferencedAssemblies(),
        reference => reference.Name?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true);

      // And never on Platform: HR reaches the tenant plane only through the shared contract set.
      Assert.DoesNotContain(
        assembly.GetReferencedAssemblies(),
        reference => reference.Name?.StartsWith("SSAS.Platform", StringComparison.Ordinal) == true);
    }

    // Platform, in both directions.
    foreach (var platform in new[]
      {
        typeof(SSAS.Platform.Domain.Companies.Company).Assembly,
        typeof(SSAS.Platform.Application.Branches.IBranchWriteAuthorizer).Assembly,
        typeof(SSAS.Platform.Infrastructure.Persistence.PlatformDbContext).Assembly
      })
    {
      Assert.DoesNotContain(
        platform.GetReferencedAssemblies(),
        reference => reference.Name?.StartsWith("SSAS.HR", StringComparison.Ordinal) == true);
    }
  }

  // ---- HR MAPS ITS OWN ENTITIES, through the contract neither side owns.
  [Fact]
  public void Hr_contributes_its_entities_through_the_shared_contributor_contract()
  {
    var contributor = typeof(SSAS.HR.Infrastructure.Persistence.HrTenantModelContributor);

    Assert.Contains(
      typeof(SSAS.BuildingBlocks.Infrastructure.Persistence.ITenantModelContributor),
      contributor.GetInterfaces());

    // The configurations are HR's own, not Platform's.
    Assert.Equal(HrInfrastructureAssembly, typeof(SSAS.HR.Infrastructure.Persistence.EmployeeConfiguration).Assembly);
  }

  // ---- NO CROSS-DATABASE FOREIGN KEY, AND NO MIGRATION MAY INTRODUCE ONE.
  //
  // Employee's principals — Company and Branch — both live in the TENANT catalog, so its foreign keys are
  // intra-catalog and legal. What must never appear is a platform-stream constraint pointing at either.
  [Fact]
  public void No_platform_migration_targets_a_tenant_owned_principal()
  {
    var platformMigrations = ReadMigrations("Persistence", "Migrations");

    Assert.DoesNotContain("principalTable: \"Companies\"", platformMigrations, StringComparison.Ordinal);
    Assert.DoesNotContain("principalTable: \"Branches\"", platformMigrations, StringComparison.Ordinal);
    Assert.DoesNotContain("principalTable: \"Employees\"", platformMigrations, StringComparison.Ordinal);

    // The tenant stream DOES carry them, intra-catalog, which is the whole point of the Company move.
    var tenantMigrations = ReadMigrations("Persistence", "TenantErp", "Migrations");
    Assert.Contains("principalTable: \"Companies\"", tenantMigrations, StringComparison.Ordinal);
    Assert.Contains("principalTable: \"Branches\"", tenantMigrations, StringComparison.Ordinal);
  }

  // ---- EVERY PERSISTED EMPLOYEE STRING IS nvarchar. A varchar column would silently narrow names and
  // identifiers that the domain permits to be Unicode.
  [Fact]
  public void Every_persisted_employee_string_is_nvarchar()
  {
    // ONE FILE, NOT A CONCATENATION (TEST-001). Slicing from a marker to the end of every joined migration
    // made the examined text depend on `Directory.EnumerateFiles` order, which is alphabetical on NTFS and
    // arbitrary on ext4 — the same order dependence that broke the index test on Linux. Reading the single
    // migration that creates the table is deterministic everywhere and is what this assertion was ever
    // about.
    var migration = ReadMigrationFile("AddHrEmployee");

    var employeeSection = migration[migration.IndexOf("name: \"Employees\"", StringComparison.Ordinal)..];

    Assert.DoesNotContain("type: \"varchar", employeeSection, StringComparison.Ordinal);
    Assert.DoesNotContain("type: \"text\"", employeeSection, StringComparison.Ordinal);
    Assert.Contains("nvarchar(64)", employeeSection, StringComparison.Ordinal);
    Assert.Contains("nvarchar(200)", employeeSection, StringComparison.Ordinal);
  }

  // ---- EMPLOYEE NUMBER UNIQUENESS IS COMPANY-WIDE AND EXCLUDES THE BRANCH.
  //
  // BR-HR-0001 scopes it to the company and ADR-023 forbids BranchId participating. Getting this wrong would
  // be invisible until two branches of one company disagreed about who holds a number.
  //
  // ---- ASSERTED FROM THE MODEL, NOT FROM CONCATENATED MIGRATION SOURCE (TEST-001).
  //
  // This previously joined every migration file, found the first occurrence of the index name, and sliced
  // forward to the next `"unique: true"`. Two things made that unsafe, and Linux exposed both:
  //
  //   * The index name appears TWICE — in the migration and in the model snapshot.
  //   * `Directory.EnumerateFiles` returns alphabetical order on NTFS and DIRECTORY order on ext4, so which
  //     of the two came first depended on the filesystem.
  //
  // When the snapshot sorted first the slice began there, found no `"unique: true"` (snapshots write
  // `.IsUnique()`), and ran on through unrelated content until it hit that text in another file — sweeping
  // up a `BranchId` that had nothing to do with this index.
  //
  // The model states the same invariant exactly and cannot be reordered. It is also STRONGER: the old test
  // could only prove a substring was absent, while this pins the precise column set, so an index that
  // gained a fourth column or lost `CompanyId` now fails too.
  [Fact]
  public void The_employee_number_index_is_company_scoped_and_excludes_the_branch()
  {
    var employee = ComposedTenantModel().FindEntityType(typeof(Employee));
    Assert.NotNull(employee);

    var index = employee!.GetIndexes().SingleOrDefault(candidate =>
      candidate.GetDatabaseName() == "UX_Employees_TenantId_CompanyId_NormalizedEmployeeNumber");

    Assert.NotNull(index);

    // Uniqueness is the point of the index: without it the per-company rule is a hint, not a constraint.
    Assert.True(index!.IsUnique);

    // COMPANY-SCOPED, AND DELIBERATELY NOT BRANCH-SCOPED. BR-HR-0001 makes the number unique within the
    // COMPANY, so adding BranchId would let the same number exist twice in one company (BRULE-EMP-0009).
    Assert.Equal(
      ["TenantId", "CompanyId", "NormalizedEmployeeNumber"],
      index.Properties.Select(property => property.Name));

    Assert.DoesNotContain(index.Properties, property => property.Name == nameof(Employee.BranchId));
  }


  // The composed tenant model — Platform's own entities plus HR's contribution, exactly as the Host builds
  // it. A contributor-free model would not contain Employee at all, so the index assertion above would pass
  // by finding nothing.
  private static Microsoft.EntityFrameworkCore.Metadata.IModel ComposedTenantModel()
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

    public Guid? CompanyId => null;

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

  // ---- ONE NAMED MIGRATION, DETERMINISTICALLY.
  //
  // `Directory.EnumerateFiles` does not promise an order, and the two operating systems disagree about what
  // it gives. Any assertion that slices concatenated migration text is therefore reading different input on
  // different machines. Selecting the single file by name removes the question.
  private static string ReadMigrationFile(string nameFragment)
  {
    var directory = Path.Combine(
      RepositoryRootDirectory(), "src", "Platform", "SSAS.Platform.Infrastructure",
      "Persistence", "TenantErp", "Migrations");

    var matches = Directory
      .EnumerateFiles(directory, "*.cs")
      .Where(file => Path.GetFileName(file).Contains(nameFragment, StringComparison.Ordinal) &&
        !file.EndsWith("Designer.cs", StringComparison.Ordinal))
      .ToArray();

    // Exactly one, or the fragment no longer identifies a single migration and the assertion below would be
    // reading whichever file happened to sort first — the defect this method exists to remove.
    var file = Assert.Single(matches);

    return File.ReadAllText(file);
  }

  private static string RepositoryRootDirectory()
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
  private static string ReadMigrations(params string[] segments)
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
    {
      directory = directory.Parent;
    }

    Assert.NotNull(directory);

    var path = Path.Combine(
      new[] { directory!.FullName, "src", "Platform", "SSAS.Platform.Infrastructure" }.Concat(segments).ToArray());

    Assert.True(Directory.Exists(path), $"Migration directory not found: {path}");

    return string.Join(
      Environment.NewLine,
      Directory.EnumerateFiles(path, "*.cs").Where(file => !file.EndsWith("Designer.cs", StringComparison.Ordinal))
        .Select(File.ReadAllText));
  }
}
