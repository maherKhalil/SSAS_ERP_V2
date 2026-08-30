using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Architecture.Tests;

// THE COMPANY OWNERSHIP BOUNDARIES (FP-006C1, ADR-025).
//
// What these protect is not the logic but the SHAPE the logic depends on: which layer owns the ownership
// contract, which database owns the assignment rows, and the absence of a constraint that cannot exist.
// Each of those is invisible at the call site and silent when it regresses.
public sealed class CompanyOwnershipArchitectureTests
{
  private static readonly Assembly InfrastructureAssembly = typeof(PlatformDbContext).Assembly;

  private static readonly Assembly DomainAssembly = typeof(UserCompanyAccess).Assembly;

  // ---- THE OWNERSHIP CONTRACT LIVES IN THE SHARED DOMAIN LAYER, beside the tenant and branch contracts.
  //
  // Putting it in Platform would make every future company-owned module depend on Platform's Domain to
  // declare its own ownership, which is precisely the coupling BuildingBlocks exists to prevent.
  [Fact]
  public void The_company_ownership_contract_lives_in_the_shared_domain_layer()
  {
    var companyOwned = typeof(ICompanyOwnedEntity);

    Assert.Equal(typeof(ITenantOwnedEntity).Assembly, companyOwned.Assembly);
    Assert.Equal(typeof(ITenantOwnedEntity).Namespace, companyOwned.Namespace);

    // A settable CompanyId is what lets the write boundary STAMP it. The same shape TenantId and BranchId
    // have, and for the same reason.
    var property = companyOwned.GetProperty(nameof(ICompanyOwnedEntity.CompanyId));
    Assert.NotNull(property);
    Assert.Equal(typeof(Guid), property!.PropertyType);
    Assert.NotNull(property.GetSetMethod());
  }

  // ---- ICurrentCompany IS A SHARED ABSTRACTION, beside ICurrentTenant and ICurrentBranch, and exposes a
  // NULLABLE company. Null is the answer to "has a company been established yet", not an error.
  [Fact]
  public void The_current_company_abstraction_sits_beside_the_other_execution_context_abstractions()
  {
    Assert.Equal(typeof(ICurrentTenant).Assembly, typeof(ICurrentCompany).Assembly);
    Assert.Equal(typeof(ICurrentTenant).Namespace, typeof(ICurrentCompany).Namespace);

    var property = typeof(ICurrentCompany).GetProperty(nameof(ICurrentCompany.CompanyId));
    Assert.NotNull(property);
    Assert.Equal(typeof(Guid?), property!.PropertyType);

    // READ-ONLY. A settable current company would let any caller assert its own scope, which is exactly
    // what ADR-025 decision 2 forbids.
    Assert.Null(property.GetSetMethod());
  }

  // ---- UserCompanyAccess IS NOT TENANT-OWNED, deliberately, and for the same reason UserBranchAccess is
  // not: the global tenant query filter keys on the AMBIENT tenant and would hide these rows from the paths
  // that must read them. Every query states TenantId explicitly instead.
  //
  // If this ever regresses the failure is silent — company scope would simply resolve to nothing on any
  // path without an ambient tenant, and would look like an ordinary authorization refusal.
  [Fact]
  public void The_company_access_rows_are_not_tenant_owned()
  {
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(UserCompanyAccess).GetInterfaces());

    // It IS auditable, because who granted company access and when is a platform audit fact.
    Assert.Contains(typeof(IAuditableEntity), typeof(UserCompanyAccess).GetInterfaces());
  }

  // ---- THE ASSIGNMENT ROWS LIVE IN THE PLATFORM DATABASE, and the company rows do not.
  //
  // The relationship that actually needs enforcing is the one to TenantUser, which is in this catalog.
  [Fact]
  public void The_company_access_rows_are_platform_owned_and_the_company_rows_are_not()
  {
    var platformEntities = PlatformModel().GetEntityTypes().Select(entity => entity.ClrType).ToArray();

    Assert.Contains(typeof(UserCompanyAccess), platformEntities);

    // Company itself moved to the tenant catalog (ADR-014 revision 1.1, Correction A). A platform-side
    // Company would resurrect the cross-catalog reference this whole design exists to avoid.
    Assert.DoesNotContain(platformEntities, type => type == typeof(SSAS.Platform.Domain.Companies.Company));
  }

  // ---- NO CROSS-DATABASE FOREIGN KEY, AND NO MIGRATION MAY INTRODUCE ONE.
  //
  // Company lives in the tenant database; UserCompanyAccess lives in the platform one. A physical
  // constraint across catalogs is impossible the moment a tenant is promoted to dedicated storage
  // (ADR-017), so the model must not declare one and no migration may emit one — the same guard shape the
  // branch dimension already has for `principalTable: "Branches"`.
  [Fact]
  public void No_platform_foreign_key_targets_the_tenant_company_table()
  {
    var access = PlatformModel().FindEntityType(typeof(UserCompanyAccess));
    Assert.NotNull(access);

    foreach (var foreignKey in access!.GetForeignKeys())
    {
      Assert.NotEqual("Companies", foreignKey.PrincipalEntityType.GetTableName());

      // CompanyId must not participate in ANY foreign key, whatever its principal.
      Assert.DoesNotContain(
        foreignKey.Properties,
        property => property.Name == nameof(UserCompanyAccess.CompanyId));
    }

    var migrations = MigrationSource();
    Assert.DoesNotContain("principalTable: \"Companies\"", migrations, StringComparison.Ordinal);
  }

  // ---- THE ASSIGNMENT SET IS UNIQUE PER USER PER COMPANY. Duplicates would make "is this authorized" a
  // question with more than one row behind it, and a removal that deleted one of them would silently leave
  // access in place.
  [Fact]
  public void One_company_assignment_row_exists_per_user_per_company()
  {
    var access = PlatformModel().FindEntityType(typeof(UserCompanyAccess));
    Assert.NotNull(access);

    var unique = access!.GetIndexes().Single(index => index.IsUnique);

    Assert.Equal(
      [
        nameof(UserCompanyAccess.TenantId),
        nameof(UserCompanyAccess.TenantUserId),
        nameof(UserCompanyAccess.CompanyId)
      ],
      unique.Properties.Select(property => property.Name).ToArray());
  }

  // ---- COMPANY AND BRANCH ARE INDEPENDENT DIMENSIONS, and their write boundaries are separate types.
  //
  // One authorizer answering both would make it possible for a change in either dimension to widen the
  // other — the exact fusion ADR-025 decision 8 exists to prevent.
  [Fact]
  public void The_company_and_branch_write_boundaries_are_separate_contracts()
  {
    var companyAuthorizer = typeof(ICompanyWriteAuthorizer);
    var branchAuthorizer = typeof(SSAS.Platform.Application.Branches.IBranchWriteAuthorizer);

    Assert.NotEqual(companyAuthorizer, branchAuthorizer);
    Assert.DoesNotContain(branchAuthorizer, companyAuthorizer.GetInterfaces());
    Assert.DoesNotContain(companyAuthorizer, branchAuthorizer.GetInterfaces());

    // The tenant context write boundary consults BOTH, as separate optional dependencies, so neither can
    // stand in for the other.
    var parameters = typeof(SSAS.Platform.Infrastructure.Persistence.TenantErp.TenantDbContext)
      .GetConstructors()
      .Single()
      .GetParameters()
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.Contains(companyAuthorizer, parameters);
    Assert.Contains(branchAuthorizer, parameters);
  }

  // ---- NO HR DEPENDENCY REACHES PLATFORM OR BUILDINGBLOCKS.
  //
  // The infrastructure in this slice is general company-ownership machinery, not Employee support. If HR
  // ever appears in a Platform or BuildingBlocks reference, the dependency direction of the modular
  // monolith has inverted (ADR-001).
  [Fact]
  public void No_hr_dependency_reaches_the_platform_or_shared_assemblies()
  {
    Assembly[] mustNotReferenceHr =
    [
      InfrastructureAssembly,
      DomainAssembly,
      typeof(ICompanyWriteAuthorizer).Assembly,
      typeof(ICompanyOwnedEntity).Assembly,
      typeof(ICurrentCompany).Assembly
    ];

    foreach (var assembly in mustNotReferenceHr.Distinct())
    {
      Assert.DoesNotContain(
        assembly.GetReferencedAssemblies(),
        reference => reference.Name?.Contains("SSAS.HR", StringComparison.OrdinalIgnoreCase) == true);
    }
  }

  // ---- THE COMPANY DIMENSION INTRODUCES NO GLOBAL QUERY FILTER (ADR-025 decision 10).
  //
  // A filter pinned to one company would make authorized multi-company reads unexpressible, which is the
  // capability the explicit-predicate design exists to preserve. Only the TENANT dimension is filtered
  // globally.
  [Fact]
  public void The_company_dimension_adds_no_global_query_filter()
  {
    var access = PlatformModel().FindEntityType(typeof(UserCompanyAccess));
    Assert.NotNull(access);

    // UserCompanyAccess is not tenant-owned, so it carries no filter at all.
    Assert.Null(access!.GetQueryFilter());
  }

  private static IModel PlatformModel()
  {
    var options = new DbContextOptionsBuilder<PlatformDbContext>()
      .UseSqlServer("Server=model-only;Database=model-only;Integrated Security=True")
      .Options;

    using var context = new PlatformDbContext(
      options, new ModelUser(), new ModelTenant(), new ModelClock());
    return context.Model;
  }

  private static string MigrationSource()
  {
    var root = AppContext.BaseDirectory;
    var directory = new DirectoryInfo(root);
    while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
    {
      directory = directory.Parent;
    }

    Assert.NotNull(directory);

    var migrations = Path.Combine(
      directory!.FullName, "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence", "Migrations");

    Assert.True(Directory.Exists(migrations), $"Migration directory not found: {migrations}");

    return string.Join(
      Environment.NewLine,
      Directory.EnumerateFiles(migrations, "*.cs").Select(File.ReadAllText));
  }

  private sealed class ModelUser : SSAS.BuildingBlocks.Application.Abstractions.Identity.ICurrentUser
  {
    public string? UserId => "architecture-tests";

    public string? UserName => "architecture-tests";

    public string? Email => null;


    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class ModelTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class ModelClock : SSAS.BuildingBlocks.Application.Abstractions.Time.IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}
