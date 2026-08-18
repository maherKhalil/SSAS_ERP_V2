using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Architecture.Tests;

// THE SANCTIONED BRANCH-TRANSFER CHANNEL (FP-006C2, ADR-024 decisions 2, 3, 11 and 12).
//
// What these protect is the NARROWNESS of the exception. The rule it excepts is one line of code; the
// property that makes the exception safe — that it authorizes one entity, cannot be opened from untrusted
// input, and does not widen anything else — lives in the shape of the contracts, where it is invisible at
// the call site and silent when it regresses.
public sealed class BranchTransferArchitectureTests
{
  private static readonly Assembly ApplicationAssembly = typeof(IBranchTransferScope).Assembly;

  private static readonly Assembly InfrastructureAssembly = typeof(PlatformDbContext).Assembly;

  private static readonly string TenantDbContextSource = ReadSource(
    "Persistence", "TenantErp", "TenantDbContext.cs");

  // ---- THE CONTRACTS LIVE IN THE APPLICATION LAYER, beside the branch authorization they extend.
  //
  // Putting them in Infrastructure would leave command handlers unable to declare a transfer without
  // depending on persistence, which is exactly the layering the write boundary exists above.
  [Fact]
  public void The_transfer_contracts_live_beside_the_other_branch_application_contracts()
  {
    Assert.Equal(typeof(IBranchWriteAuthorizer).Assembly, typeof(IBranchTransferScope).Assembly);
    Assert.Equal(typeof(IBranchWriteAuthorizer).Namespace, typeof(IBranchTransferScope).Namespace);
    Assert.Equal(typeof(IBranchWriteAuthorizer).Namespace, typeof(IBranchTransferAuthorizer).Namespace);
    Assert.Equal(typeof(IBranchWriteAuthorizer).Namespace, typeof(BranchTransferDeclaration).Namespace);
  }

  // ---- THE DECLARATION IS ENTITY-SPECIFIC, AND THERE IS NO WAY TO EXPRESS ANYTHING BROADER.
  //
  // A declaration carries exactly one entity, one source and one destination. If a future change added a
  // set, a wildcard, or a nullable entity, this is where it becomes visible.
  [Fact]
  public void A_transfer_declaration_names_exactly_one_entity_and_one_transition()
  {
    var declaration = typeof(BranchTransferDeclaration);

    var entity = declaration.GetProperty(nameof(BranchTransferDeclaration.Entity));
    Assert.NotNull(entity);
    Assert.False(typeof(System.Collections.IEnumerable).IsAssignableFrom(entity!.PropertyType));

    // Non-nullable Guids: there is no "any source" or "any destination".
    Assert.Equal(typeof(Guid), declaration.GetProperty(nameof(BranchTransferDeclaration.SourceBranchId))!.PropertyType);
    Assert.Equal(typeof(Guid), declaration.GetProperty(nameof(BranchTransferDeclaration.DestinationBranchId))!.PropertyType);

    // IMMUTABLE. A declaration that could be edited after the authorization that produced it would prove
    // nothing about what was authorized.
    foreach (var property in declaration.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
      Assert.Null(property.GetSetMethod());
    }
  }

  // ---- NO BROAD "ALLOW BRANCH CHANGE" SWITCH EXISTS ANYWHERE (ADR-024 decision 11).
  //
  // A boolean that can be turned on for convenience is the boundary's absence rather than its exception, so
  // the absence of one is itself the invariant.
  [Fact]
  public void No_general_allow_branch_change_flag_exists()
  {
    var suspicious = new[] { "AllowBranchChange", "CanChangeBranch", "SkipBranchRules", "BypassBranch" };

    var members = new[] { ApplicationAssembly, InfrastructureAssembly }
      .SelectMany(assembly => assembly.GetTypes())
      .SelectMany(type => type.GetMembers(
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
      .Select(member => member.Name)
      .ToArray();

    Assert.DoesNotContain(members, name => suspicious.Any(
      candidate => name.Contains(candidate, StringComparison.OrdinalIgnoreCase)));

    // And no boolean anywhere on the transfer contracts, which is where such a switch would most naturally
    // be smuggled in.
    var transferTypes = new[]
    {
      typeof(BranchTransferDeclaration), typeof(IBranchTransferScope), typeof(IBranchTransferAuthorizer)
    };

    foreach (var type in transferTypes)
    {
      Assert.DoesNotContain(
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
        property => property.PropertyType == typeof(bool));
    }
  }

  // ---- OPENING THE CHANNEL REQUIRES THE TRACKED ENTITY, which only server-side orchestration that has
  // already loaded and authorized the record can supply.
  //
  // That is what makes the channel unreachable from a request DTO, a header, a form field, a token claim, a
  // query string or a repository parameter forwarded from controller input: none of them is an entity.
  [Fact]
  public void A_transfer_can_only_be_declared_with_a_tracked_branch_owned_entity()
  {
    var create = typeof(BranchTransferDeclaration)
      .GetMethods(BindingFlags.Public | BindingFlags.Static)
      .Single(method => method.Name == nameof(BranchTransferDeclaration.Create));

    Assert.True(create.IsGenericMethodDefinition);

    // The entity parameter is constrained to IBranchOwnedEntity: a declaration cannot even be expressed for
    // something that is not branch-owned.
    var argument = create.GetGenericArguments().Single();
    Assert.Contains(typeof(IBranchOwnedEntity), argument.GetGenericParameterConstraints());

    // No primitive-only overload exists that would let a caller declare a transfer from identifiers alone.
    Assert.Single(typeof(BranchTransferDeclaration)
      .GetMethods(BindingFlags.Public | BindingFlags.Static)
      .Where(method => method.Name == nameof(BranchTransferDeclaration.Create)));
  }

  // ---- THE SCOPE IS INSTANCE STATE, NEVER STATIC OR AsyncLocal.
  //
  // Static state would be shared by every concurrent request in the process. AsyncLocal would flow into
  // background work that outlives the operation. Either would let a declaration authorize a save it was
  // never earned for.
  [Fact]
  public void The_transfer_scope_holds_no_static_or_ambient_state()
  {
    var scope = InfrastructureAssembly.GetType("SSAS.Platform.Infrastructure.Branches.BranchTransferScope");
    Assert.NotNull(scope);

    var staticFields = scope!
      .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
      .Where(field => !field.IsLiteral)
      .ToArray();

    Assert.Empty(staticFields);

    var ambient = scope.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
      .Where(field => field.FieldType.IsGenericType &&
        field.FieldType.GetGenericTypeDefinition() == typeof(System.Threading.AsyncLocal<>))
      .ToArray();

    Assert.Empty(ambient);

    // The declaration's lifetime is a disposable handle, so it ends deterministically.
    var begin = typeof(IBranchTransferScope).GetMethod(nameof(IBranchTransferScope.Begin));
    Assert.NotNull(begin);
    Assert.Equal(typeof(Result<IDisposable>), begin!.ReturnType);
  }

  // ---- ORDINARY BranchId MODIFICATION IS STILL REFUSED, and the sanctioned path is the only exception.
  //
  // Asserted against the source because the rule is one branch of a switch: a reflection test cannot see it,
  // and its removal would be silent everywhere else.
  [Fact]
  public void The_write_boundary_still_refuses_ordinary_branch_modification()
  {
    Assert.Contains(
      "Branch ownership cannot be changed after an entity is created.",
      TenantDbContextSource,
      StringComparison.Ordinal);

    Assert.Contains(
      "Branch ownership must match the trusted branch context.",
      TenantDbContextSource,
      StringComparison.Ordinal);

    // Exactly ONE place decides that an entry is a sanctioned transfer, so there is one exception rather
    // than a scattering of them.
    var sanctionedChecks = TenantDbContextSource.Split("IsSanctionedTransfer", StringSplitOptions.None).Length - 1;
    Assert.Equal(2, sanctionedChecks);
  }

  // ---- THE TRANSFER AUTHORIZER IS A SEPARATE CONTRACT FROM THE BRANCH AND COMPANY ONES, and the write
  // boundary takes all three independently. None can stand in for another.
  [Fact]
  public void The_write_boundary_takes_three_independent_authorizers()
  {
    var parameters = typeof(TenantDbContext)
      .GetConstructors()
      .Single()
      .GetParameters()
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.Contains(typeof(IBranchWriteAuthorizer), parameters);
    Assert.Contains(typeof(IBranchTransferAuthorizer), parameters);
    Assert.Contains(typeof(SSAS.Platform.Application.Companies.ICompanyWriteAuthorizer), parameters);

    // OPTIONAL, SO ABSENCE IS A REFUSAL. A context built without the transfer authorizer keeps the original
    // invariant in full rather than assuming a transfer is permitted.
    var transferParameter = typeof(TenantDbContext).GetConstructors().Single().GetParameters()
      .Single(parameter => parameter.ParameterType == typeof(IBranchTransferAuthorizer));
    Assert.True(transferParameter.IsOptional);
    Assert.Null(transferParameter.DefaultValue);
  }

  // ---- ITenantBranchAccessResolver REMAINS THE SOURCE OF DESTINATION AUTHORIZATION.
  //
  // The transfer authorizer must not re-implement it: a second opinion about what a reachable branch means
  // is how the read path and the write path come to disagree.
  [Fact]
  public void The_transfer_authorizer_delegates_destination_authorization_to_the_resolver()
  {
    var authorizer = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.Branches.BranchTransferAuthorizer");
    Assert.NotNull(authorizer);

    var dependencies = authorizer!.GetConstructors().Single()
      .GetParameters()
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.Contains(typeof(ITenantBranchAccessResolver), dependencies);
    Assert.Contains(typeof(SSAS.Platform.Application.Abstractions.Queries.ITenantAdministratorAuthority), dependencies);

    var source = ReadSource("Branches", "BranchTransferAuthorizer.cs");
    Assert.Contains("AuthorizeBranchAsync", source, StringComparison.Ordinal);
  }

  // ---- THE INACTIVE-SOURCE RECOVERY DOES NOT WIDEN THE RESOLVER'S GENERAL CONTRACT.
  //
  // ADR-024 decision 12 is an exception to ADR-023 decision 5, and it is implemented as one: the resolver
  // still intersects with ACTIVE branches for every caller, and the recovery is a separate, narrower check.
  // Teaching the resolver to return inactive branches would widen every other consumer at once.
  [Fact]
  public void The_branch_access_resolver_still_returns_active_branches_only()
  {
    var resolver = ReadSource("Branches", "TenantBranchAccessResolver.cs");

    Assert.Contains("branch.IsActive", resolver, StringComparison.Ordinal);
    Assert.DoesNotContain("!branch.IsActive", resolver, StringComparison.Ordinal);
    Assert.DoesNotContain("IsTenantAdministratorAsync", ReadSource("Branches", "TenantBranchValidator.cs"),
      StringComparison.Ordinal);

    // The inactive check lives with the transfer authorizer, where it applies only to a declared recovery.
    Assert.Contains("!branch.IsActive", ReadSource("Branches", "BranchTransferAuthorizer.cs"),
      StringComparison.Ordinal);
  }

  // ---- NO HR DEPENDENCY REACHES THE SHARED INFRASTRUCTURE. The channel is general mechanism for any
  // transferable branch-owned entity, not Employee support (ADR-024 decision 11).
  [Fact]
  public void No_hr_dependency_reaches_the_transfer_infrastructure()
  {
    foreach (var assembly in new[] { ApplicationAssembly, InfrastructureAssembly }.Distinct())
    {
      Assert.DoesNotContain(
        assembly.GetReferencedAssemblies(),
        reference => reference.Name?.Contains("SSAS.HR", StringComparison.OrdinalIgnoreCase) == true);
    }

    // And nothing in the transfer types names an HR concept.
    foreach (var type in new[]
      { typeof(BranchTransferDeclaration), typeof(IBranchTransferScope), typeof(IBranchTransferAuthorizer) })
    {
      Assert.DoesNotContain("Employee", type.Name, StringComparison.OrdinalIgnoreCase);
    }
  }

  // ---- SYNCHRONOUS SAVE REMAINS BLOCKED, so the channel cannot be reached through an unguarded path.
  [Fact]
  public void Synchronous_save_is_still_refused_on_the_tenant_context()
  {
    Assert.Contains(
      "Synchronous SaveChanges is not supported on TenantDbContext",
      TenantDbContextSource,
      StringComparison.Ordinal);
  }

  // ---- THIS SLICE INTRODUCED NO PERSISTENCE. The channel is authorization, not storage: no new entity, no
  // new table, and no new foreign key of any kind.
  [Fact]
  public void The_transfer_channel_introduced_no_persistence()
  {
    var options = new DbContextOptionsBuilder<PlatformDbContext>()
      .UseSqlServer("Server=model-only;Database=model-only;Integrated Security=True")
      .Options;

    using var platform = new PlatformDbContext(
      options, new ModelUser(), new ModelTenant(), new ModelClock());

    Assert.DoesNotContain(
      platform.Model.GetEntityTypes(),
      entity => entity.ClrType.Name.Contains("Transfer", StringComparison.OrdinalIgnoreCase));

    var tenantOptions = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=model-only;Database=model-only;Integrated Security=True")
      .Options;

    using var tenant = new TenantDbContext(
      tenantOptions, new ModelUser(), new ModelTenant(), new ModelClock());

    Assert.DoesNotContain(
      tenant.Model.GetEntityTypes(),
      entity => entity.ClrType.Name.Contains("Transfer", StringComparison.OrdinalIgnoreCase));
  }

  private static string ReadSource(params string[] segments)
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
    {
      directory = directory.Parent;
    }

    Assert.NotNull(directory);

    var path = Path.Combine(
      new[] { directory!.FullName, "src", "Platform", "SSAS.Platform.Infrastructure" }
        .Concat(segments).ToArray());

    Assert.True(File.Exists(path), $"Source not found: {path}");
    return File.ReadAllText(path);
  }

  private sealed class ModelUser : SSAS.BuildingBlocks.Application.Abstractions.Identity.ICurrentUser
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

  private sealed class ModelTenant : SSAS.BuildingBlocks.Application.Abstractions.Tenancy.ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class ModelClock : SSAS.BuildingBlocks.Application.Abstractions.Time.IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}
