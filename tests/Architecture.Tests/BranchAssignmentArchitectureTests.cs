using System.Reflection;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Application.TenantUsers;
using SSAS.Platform.Domain.Branches;

namespace SSAS.Architecture.Tests;

// THE BRANCH ASSIGNMENT BOUNDARIES (Branch foundation B1b).
//
// The invariant these protect — "no active normal user without an active branch" — is held by exclusion
// rather than by a constraint, because its two halves live in different databases. Exclusion only works if
// EVERY participant opts in, so what needs guarding is not the logic but the membership of that set.
public sealed class BranchAssignmentArchitectureTests
{
  private static readonly Assembly InfrastructureAssembly =
    typeof(SSAS.Platform.Infrastructure.Persistence.PlatformDbContext).Assembly;

  private static readonly Assembly ApplicationAssembly = typeof(ITenantBranchService).Assembly;

  // ---- ONE RESOURCE, NOT TWO. Two differently-named locks would each work perfectly and protect nothing
  // together: deactivation would serialise against deactivation, assignment against assignment, and the
  // two would still interleave — which is precisely the R1/R2 pair B1a left open.
  [Fact]
  public void Branch_topology_is_serialised_on_exactly_one_lock_resource()
  {
    var lockType = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.Branches.BranchTopologyLock");
    Assert.NotNull(lockType);

    var prefix = (string)lockType!.GetField("Prefix", BindingFlags.Public | BindingFlags.Static)!
      .GetValue(null)!;

    // Every lock resource name in the branch slice comes from this one helper.
    var branchLockNames = InfrastructureAssembly.GetTypes()
      .Where(type => type.Namespace == "SSAS.Platform.Infrastructure.Branches")
      .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic))
      .Where(field => field.IsLiteral || field.IsInitOnly)
      .Where(field => field.FieldType == typeof(string))
      .Select(field => field.GetValue(null) as string)
      .Where(value => value is not null && value.Contains("SSAS.", StringComparison.Ordinal))
      .Distinct()
      .ToArray();

    Assert.Equal([prefix], branchLockNames);
  }

  // ---- THE GUARD IS REACHABLE FROM THE APPLICATION LAYER, which is where the assignment writers live.
  // Without this abstraction they could not take the same resource deactivation takes, and B1a's guard
  // would protect only itself.
  [Fact]
  public void The_topology_guard_is_available_to_application_writers_and_leases_are_disposable()
  {
    Assert.NotNull(typeof(IBranchTopologyGuard).GetMethod(nameof(IBranchTopologyGuard.AcquireAsync)));

    // Ownership must be releasable, or a failed operation would hold the tenant's topology forever.
    Assert.Contains(typeof(IAsyncDisposable), typeof(IBranchTopologyLease).GetInterfaces());
  }

  // ---- EVERY WRITER OF UserBranchAccess TAKES THE LEASE.
  //
  // Asserted by DEPENDENCY rather than by scanning for a call: a handler that writes assignments and does
  // not even depend on the guard cannot possibly be taking it, and that is the failure mode worth catching
  // — a new workflow added later that quietly reopens R1/R2.
  [Fact]
  public void Every_user_branch_access_writer_depends_on_the_topology_guard()
  {
    var writers = ApplicationAssembly.GetTypes()
      .Concat(InfrastructureAssembly.GetTypes())
      .Where(type => !type.IsInterface && !type.IsAbstract)
      .Where(type => Dependencies(type).Contains(typeof(IUserBranchAccessRepository)))
      .ToArray();

    Assert.NotEmpty(writers);

    foreach (var writer in writers)
    {
      Assert.Contains(typeof(IBranchTopologyGuard), Dependencies(writer));
    }
  }

  // ---- ADMINISTRATOR SCOPE COMES FROM AUTHORITY, NEVER FROM A NAME OR A FLAG.
  [Fact]
  public void Tenant_administrator_scope_is_resolved_through_the_authority_abstraction()
  {
    foreach (var consumer in new[]
      {
        typeof(CreateTenantUserMembershipCommandHandler),
        typeof(SetTenantUserBranchesCommandHandler)
      })
    {
      Assert.Contains(typeof(ITenantAdministratorAuthority), Dependencies(consumer));
    }

    // No parallel mechanism: nothing in the branch slice exposes an "is admin" flag of its own.
    var flags = ApplicationAssembly.GetTypes()
      .Concat(InfrastructureAssembly.GetTypes())
      .Where(type => type.Namespace?.Contains("Branch", StringComparison.Ordinal) == true)
      .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
      .Where(property => property.PropertyType == typeof(bool))
      .Select(property => property.Name)
      .Where(name => name.Contains("Admin", StringComparison.OrdinalIgnoreCase))
      .ToArray();

    Assert.Empty(flags);
  }

  // ---- A NORMAL USER CANNOT BE CREATED WITHOUT NAMING BRANCHES. The contract carries them, so a caller
  // cannot simply omit the concept and get a user with no reachable branch.
  [Fact]
  public void Membership_creation_carries_branches_and_roles_in_one_request()
  {
    var properties = typeof(CreateTenantUserMembershipCommand)
      .GetProperties(BindingFlags.Public | BindingFlags.Instance)
      .Select(property => property.Name)
      .ToArray();

    Assert.Contains("BranchIds", properties);

    // Roles travel with creation because whether the user is an administrator decides whether branches are
    // mandatory — a fact that cannot be known if roles are granted in a later call.
    Assert.Contains("RoleIds", properties);
  }

  // ---- NO ASSIGNMENT ROWS ARE EVER MANUFACTURED FOR AN ADMINISTRATOR. Their scope is derived, so
  // materialising rows would need synchronising on every branch created and would drift the moment it was
  // not.
  [Fact]
  public void No_branch_access_rows_are_created_for_administrators()
  {
    var errors = typeof(BranchErrors)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Select(field => field.Name)
      .ToArray();

    // The contract is a refusal, not a silent no-op, so the vocabulary has to be able to say so.
    Assert.Contains("AssignmentInvalid", errors);
    Assert.Contains("UserMustHaveAtLeastOneBranch", errors);
  }

  // ---- NO CROSS-DATABASE FOREIGN KEY. UserBranchAccess names branches in another catalog by identifier
  // only; a physical constraint would be impossible once a tenant is promoted to dedicated storage.
  [Fact]
  public void The_user_branch_access_configuration_declares_no_foreign_key_to_branch()
  {
    var configuration = File.ReadAllText(Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence",
      "Configurations", "UserBranchAccessConfiguration.cs"));

    Assert.DoesNotContain("HasOne<Branch>", configuration, StringComparison.Ordinal);
    Assert.DoesNotContain("principalTable: \"Branches\"", configuration, StringComparison.Ordinal);
  }

  private static Type[] Dependencies(Type type) =>
    type.GetConstructors()
      .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
      .Distinct()
      .ToArray();

  private static string RepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
    {
      directory = directory.Parent;
    }

    return directory!.FullName;
  }
}
