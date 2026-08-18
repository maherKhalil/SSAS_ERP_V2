using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Architecture.Tests;

// THE ACTIVE BRANCH SESSION BOUNDARIES (Branch foundation B1c).
//
// The property these protect is a single sentence: the stored active branch is CONTEXT, never
// AUTHORIZATION. Everything below exists because that sentence is easy to state and easy to erode — one
// cached lookup, one trusted claim, one header, and a revoked user keeps working.
public sealed class BranchSessionArchitectureTests
{
  private static readonly Assembly InfrastructureAssembly = typeof(PlatformDbContext).Assembly;

  // ---- A + B. THE ACTIVE BRANCH BELONGS TO THE SESSION, NOT THE USER.
  //
  // On TenantUser it would be a property of the person rather than of the sitting, so two concurrent
  // sessions would fight over one value and signing out of one would move the other.
  [Fact]
  public void The_active_branch_lives_on_the_session_and_not_on_the_user()
  {
    Assert.NotNull(typeof(AuthenticationSession).GetProperty(nameof(AuthenticationSession.ActiveBranchId)));

    Assert.DoesNotContain(
      typeof(TenantUser).GetProperties(BindingFlags.Public | BindingFlags.Instance),
      property => property.Name.Contains("Branch", StringComparison.Ordinal));
  }

  // ---- C + migration guard. NULLABLE, AND NO RELATIONSHIP ACROSS THE PLANE BOUNDARY.
  [Fact]
  public void The_active_branch_column_is_nullable_and_has_no_foreign_key()
  {
    var session = PlatformModel().FindEntityType(typeof(AuthenticationSession));
    Assert.NotNull(session);

    var property = session!.FindProperty(nameof(AuthenticationSession.ActiveBranchId));
    Assert.NotNull(property);

    // Nullable because "authenticated, no branch chosen" is a real state.
    Assert.True(property!.IsNullable);
    Assert.Equal(typeof(Guid?), property.ClrType);

    // The branch row lives in the tenant database. A foreign key here would cross catalogs and become
    // impossible the moment a tenant is promoted to dedicated storage.
    Assert.DoesNotContain(session.GetForeignKeys(), key =>
      key.Properties.Any(keyProperty =>
        keyProperty.Name == nameof(AuthenticationSession.ActiveBranchId)));

    // No index: it is read by session identity, never searched by branch.
    Assert.DoesNotContain(session.GetIndexes(), index =>
      index.Properties.Any(indexProperty =>
        indexProperty.Name == nameof(AuthenticationSession.ActiveBranchId)));
  }

  // ---- D. THE TOKEN CARRIES NO BRANCH. A claim would be a second source of truth that can go stale
  // between issue and use, and the durable session already answers authoritatively.
  [Fact]
  public void No_branch_identifier_is_carried_as_an_access_token_claim()
  {
    foreach (var claims in new[] { typeof(AccessTokenClaims), typeof(PlatformAccessTokenClaims) })
    {
      Assert.DoesNotContain(
        claims.GetProperties(BindingFlags.Public | BindingFlags.Instance),
        property => property.Name.Contains("Branch", StringComparison.Ordinal));
    }
  }

  // ---- E + F + H + I. THE CURRENT BRANCH COMES FROM THE DURABLE SESSION, AND IS RE-AUTHORIZED.
  //
  // Asserted by DEPENDENCY: the authorizer holds the platform context (to re-read the session), the access
  // resolver (to re-ask authorization) and a clock (to re-check expiry). It holds nothing that could carry
  // a client-supplied branch — no HTTP accessor, no options, no header abstraction.
  [Fact]
  public void The_write_authorizer_rereads_the_session_and_reasks_authorization()
  {
    var authorizer = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.Branches.BranchWriteAuthorizer");
    Assert.NotNull(authorizer);

    var dependencies = Dependencies(authorizer!);
    Assert.Contains(typeof(PlatformDbContext), dependencies);
    Assert.Contains(typeof(ITenantBranchAccessResolver), dependencies);
    Assert.Contains(typeof(ICurrentAuthenticationSession), dependencies);

    // NOTHING REQUEST-SHAPED. A dependency able to read headers, query strings or the raw request would be
    // the only way a caller-supplied branch could become authoritative.
    Assert.DoesNotContain(dependencies, type =>
      type.Name.Contains("HttpContext", StringComparison.Ordinal) ||
      type.Name.Contains("HttpRequest", StringComparison.Ordinal) ||
      type.Name.Contains("Accessor", StringComparison.Ordinal));
  }

  // ---- G. SELECTION AND SWITCHING GO THROUGH THE AUTHORITATIVE RESOLVER, not through a list captured at
  // login. Same dependency argument: it cannot revalidate without holding the thing that revalidates.
  [Fact]
  public void Branch_selection_depends_on_the_authoritative_access_resolver()
  {
    var sessions = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.Branches.BranchSessionService");
    Assert.NotNull(sessions);

    Assert.Contains(typeof(ITenantBranchAccessResolver), Dependencies(sessions!));

    // Selection and switching are ONE operation, so the two cannot disagree about what authorization means.
    var methods = typeof(IBranchSessionService).GetMethods().Select(method => method.Name).ToArray();
    Assert.Contains(nameof(IBranchSessionService.SelectActiveBranchAsync), methods);
    Assert.DoesNotContain(methods, name => name.Contains("Switch", StringComparison.OrdinalIgnoreCase));
  }

  // ---- J + K + L + M. THE WRITE BOUNDARY: authorization is reachable, conditional, once, and still ahead
  // of the Phase E fence.
  [Fact]
  public void The_tenant_context_authorizes_branch_writes_and_still_calls_the_phase_e_fence()
  {
    var constructorParameters = typeof(TenantDbContext).GetConstructors()
      .SelectMany(constructor => constructor.GetParameters())
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.Contains(typeof(IBranchWriteAuthorizer), constructorParameters);

    // Phase E's fence is still a dependency of the same context — branch enforcement composed with it
    // rather than replacing it.
    Assert.Contains(constructorParameters, type => type.Name == "ITenantWriteFence");

    var source = SourceOf("TenantDbContext.cs");

    // ONCE PER SAVE: exactly one authorization call, reached from exactly one call site inside the single
    // async funnel. Counting the AWAITED form deliberately — the method's own declaration is not a call.
    Assert.Equal(1, Occurrences(source, "AuthorizeCurrentBranchAsync"));
    Assert.Equal(1, Occurrences(source, "await ApplyBranchRulesAsync("));

    // ALL THREE MUTATION STATES. Authorizing only inserts would let a user edit or delete another branch's
    // rows, which is the same breach as creating one there.
    var rules = Between(source, "private async Task ApplyBranchRulesAsync", "private static void AssignBranch");
    Assert.Contains("EntityState.Added", rules, StringComparison.Ordinal);
    Assert.Contains("EntityState.Modified", rules, StringComparison.Ordinal);
    Assert.Contains("EntityState.Deleted", rules, StringComparison.Ordinal);

    // CONDITIONAL: no branch-owned entries means no branch required, which is what keeps first-branch
    // onboarding and tenant-global administration reachable with no branch selected.
    Assert.Contains("entries.Length == 0", rules, StringComparison.Ordinal);
  }

  // ---- N + O + P. NOTHING TENANT-GLOBAL OR PLATFORM-OWNED IS BRANCH-OWNED BY ACCIDENT.
  [Fact]
  public void No_tenant_global_or_routing_entity_is_branch_owned()
  {
    foreach (var type in new[]
      {
        typeof(Branch), typeof(Company), typeof(TenantDatabase),
        typeof(TenantDatabaseAssignment), typeof(TenantCutoverOperation), typeof(UserBranchAccess)
      })
    {
      Assert.DoesNotContain(typeof(IBranchOwnedEntity), type.GetInterfaces());
    }
  }

  private static Type[] Dependencies(Type type) =>
    type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
      .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
      .Distinct()
      .ToArray();

  private static IModel PlatformModel()
  {
    var options = new DbContextOptionsBuilder<PlatformDbContext>()
      .UseSqlServer("Server=(local);Database=ArchitectureGuard;Integrated Security=true")
      .Options;
    using var context = new PlatformDbContext(
      options, new GuardUser(), new GuardTenant(), new GuardClock());
    return context.Model;
  }

  private static string SourceOf(string fileName)
  {
    var path = Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence", "TenantErp",
      fileName);
    Assert.True(File.Exists(path), $"expected source file not found: {path}");
    return File.ReadAllText(path);
  }

  private static int Occurrences(string source, string token)
  {
    var count = 0;
    var cursor = 0;
    while ((cursor = source.IndexOf(token, cursor, StringComparison.Ordinal)) >= 0)
    {
      count++;
      cursor += token.Length;
    }

    return count;
  }

  private static string Between(string source, string start, string end)
  {
    var from = source.IndexOf(start, StringComparison.Ordinal);
    Assert.True(from >= 0, $"expected marker not found: {start}");
    var to = source.IndexOf(end, from, StringComparison.Ordinal);
    return to < 0 ? source[from..] : source[from..to];
  }

  private static string RepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
    {
      directory = directory.Parent;
    }

    return directory!.FullName;
  }

  private sealed class GuardUser : SSAS.BuildingBlocks.Application.Abstractions.Identity.ICurrentUser
  {
    public string? UserId => "guard";

    public string? UserName => "guard";

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class GuardTenant : SSAS.BuildingBlocks.Application.Abstractions.Tenancy.ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class GuardClock : SSAS.BuildingBlocks.Application.Abstractions.Time.IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}
