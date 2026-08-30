using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Localization;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Tests.Persistence;

// THE SEVEN OLDER PLATFORM GUARDS, ON THE ROUTE THAT USED TO GO ROUND THEM (T-015).
//
// Until 2026-08-25 these hung on an override of `SaveChangesAsync(CancellationToken)`. EF Core routes
// `SaveChangesAsync(ct)` to `SaveChangesAsync(bool, ct)` and `SaveChanges()` to `SaveChanges(bool)` by
// virtual dispatch, so a caller who named the inner overload committed straight past all seven —
// deletion guards on tenancy and authorization, plus the identity-ownership rule.
//
// ---- EVERY TEST HERE CALLS THE BYPASS ROUTE, AND THAT IS THE WHOLE POINT.
//
// A test calling `SaveChangesAsync(ct)` would have passed before the fence and after it, and proved
// nothing about the defect. Each test below names `SaveChangesAsync(acceptAllChangesOnSuccess: true, ...)`
// or the synchronous `SaveChanges()` explicitly. Remove `ApplyPlatformWriteRules` from either innermost
// overload in `PlatformDbContext` and these go red.
//
// ---- WHAT THESE TESTS DO NOT ESTABLISH, STATED SO NOBODY INFERS IT.
//
// They assert the guard's own exception and message. They do NOT create schema, so without the fence the
// call proceeds and fails against SQLite for a different reason rather than committing. The full Platform
// model does not build on SQLite — it needs a `Latin1_General_100_BIN2` collation SQLite has no notion of,
// and past that a T-SQL default it cannot parse — and standing a fake schema up to close that gap would
// make every test here hostage to the next migration that adds a provider-specific column.
//
// That the inner overload genuinely REACHES the database is already established, with a real table and a
// committed write, by `PlatformAppendOnlyGuardTests` (T-014). These seven then establish that each rule
// now sits on that route. The two together are the claim; neither is on its own.
public sealed class PlatformGuardFenceTests
{
  [Fact]
  public async Task Tenant_deletion_is_refused_on_the_inner_async_overload()
  {
    await using var scope = await FenceScope.CreateAsync();
    scope.MarkDeleted(Materialize<Tenant>());

    var error = await Assert.ThrowsAsync<InvalidOperationException>(
      () => scope.Context.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None));

    Assert.Equal(
      "Tenant rows cannot be physically deleted; use the Archive lifecycle transition.",
      error.Message);
  }

  // The synchronous route is checked once, on the guard whose bypass would be worst: deleting a tenant.
  // `SaveChanges()` routes to `SaveChanges(bool)` by the same dispatch, and nothing in this repository
  // writes synchronously today — which is exactly why an unfenced synchronous path would sit unnoticed.
  [Fact]
  public async Task Tenant_deletion_is_refused_on_the_synchronous_entry_point()
  {
    await using var scope = await FenceScope.CreateAsync();
    scope.MarkDeleted(Materialize<Tenant>());

    var error = Assert.Throws<InvalidOperationException>(() => scope.Context.SaveChanges());

    Assert.Equal(
      "Tenant rows cannot be physically deleted; use the Archive lifecycle transition.",
      error.Message);
  }

  [Fact]
  public async Task Platform_support_principal_deletion_is_refused_on_the_inner_async_overload()
  {
    await using var scope = await FenceScope.CreateAsync();
    scope.MarkDeleted(Materialize<PlatformSupportPrincipal>());

    var error = await Assert.ThrowsAsync<InvalidOperationException>(
      () => scope.Context.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None));

    Assert.Equal(
      "Platform-support principals are retained authority records and cannot be physically deleted.",
      error.Message);
  }

  [Fact]
  public async Task Platform_permission_assignment_deletion_is_refused_on_the_inner_async_overload()
  {
    await using var scope = await FenceScope.CreateAsync();
    scope.MarkDeleted(Materialize<PlatformPermissionAssignment>());

    var error = await Assert.ThrowsAsync<InvalidOperationException>(
      () => scope.Context.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None));

    Assert.Equal(
      "Platform permission assignments are retained authority history and cannot be physically deleted; use revoke instead.",
      error.Message);
  }

  [Fact]
  public async Task Authentication_history_deletion_is_refused_on_the_inner_async_overload()
  {
    await using var scope = await FenceScope.CreateAsync();
    scope.MarkDeleted(Materialize<AuthenticationSession>(
      ("Id", 1L), ("ClientId", "ssas-erp-web")));

    var error = await Assert.ThrowsAsync<InvalidOperationException>(
      () => scope.Context.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None));

    Assert.Equal(
      "Authentication session, refresh-token, and tenant-selection history cannot be physically deleted.",
      error.Message);
  }

  [Fact]
  public async Task Localization_override_version_mutation_is_refused_on_the_inner_async_overload()
  {
    await using var scope = await FenceScope.CreateAsync();
    scope.MarkDeleted(Materialize<TenantLocalizationOverrideVersion>());

    var error = await Assert.ThrowsAsync<InvalidOperationException>(
      () => scope.Context.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None));

    Assert.Equal(
      "Tenant localization override versions are immutable and cannot be updated or deleted.",
      error.Message);
  }

  // The only one of the seven that is not a deletion rule: it refuses a MODIFIED entry whose IdentityId
  // changed. Reproducing it needs the property genuinely marked modified, not merely a Modified state.
  [Fact]
  public async Task Identity_ownership_change_is_refused_on_the_inner_async_overload()
  {
    await using var scope = await FenceScope.CreateAsync();
    var membership = Materialize<TenantUser>(("Id", 1L));
    var entry = scope.Context.Attach(membership);
    entry.State = EntityState.Modified;
    entry.Property(user => user.IdentityId).IsModified = true;

    var error = await Assert.ThrowsAsync<InvalidOperationException>(
      () => scope.Context.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None));

    Assert.Equal(
      "Identity ownership cannot be changed after a tenant membership is created.",
      error.Message);
  }

  [Fact]
  public async Task Tenant_storage_registry_deletion_is_refused_on_the_inner_async_overload()
  {
    await using var scope = await FenceScope.CreateAsync();
    scope.MarkDeleted(Materialize<TenantDatabase>());

    var error = await Assert.ThrowsAsync<InvalidOperationException>(
      () => scope.Context.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None));

    Assert.Equal(
      "Tenant storage registry rows are retained routing history and cannot be physically deleted; end the assignment instead.",
      error.Message);
  }

  // ---- INSTANTIATION WITHOUT THE DOMAIN FACTORIES, AND WHY IT IS RIGHT HERE.
  //
  // Every one of these types has a private parameterless constructor, because that is how EF Core
  // materialises them when reading a row. Using it is the same path the runtime uses.
  //
  // The guards read `entry.State` and, in one case, whether a property is marked modified. They read no
  // domain field, so a valid aggregate built through `Tenant.Create` and friends would exercise exactly
  // the same code with more ceremony — and several of these types have no public factory at all, being
  // created by other aggregates. Constructing valid state to test a rule that does not read it would
  // suggest the rule depends on it.
  // Two of the seven need a key value before EF will TRACK them at all — `TenantUser.Id` is otherwise a
  // temporary value and `AuthenticationSession.ClientId` is an alternate key, which may not be null. Those
  // are EF's tracking preconditions, not the guards', and they are set here for exactly that reason: to
  // reach the rule under test. Nothing else is populated.
  private static T Materialize<T>(params (string Property, object? Value)[] keys) where T : class
  {
    var entity = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    foreach (var (property, value) in keys)
    {
      Assign(entity, property, value);
    }

    return entity;
  }

  // These entities expose their keys with no public setter — `Id` is set by the constructor and declared
  // on the `Entity<TKey>` base — so the value goes in the way EF's own materialiser does: through the
  // non-public setter if there is one, and otherwise the compiler-generated backing field, walking the
  // hierarchy because `Id` is not declared on the leaf type.
  private static void Assign(object entity, string property, object? value)
  {
    for (var type = entity.GetType(); type is not null; type = type.BaseType)
    {
      var setter = type.GetProperty(property, PropertyLookup)?.GetSetMethod(nonPublic: true);
      if (setter is not null)
      {
        setter.Invoke(entity, [value]);
        return;
      }

      var field = type.GetField($"<{property}>k__BackingField", FieldLookup);
      if (field is not null)
      {
        field.SetValue(entity, value);
        return;
      }
    }

    throw new InvalidOperationException(
      $"{entity.GetType().Name}.{property} has neither a setter nor a backing field to assign.");
  }

  private const BindingFlags PropertyLookup =
    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

  private const BindingFlags FieldLookup =
    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

  private sealed class FenceScope : IAsyncDisposable
  {
    private readonly SqliteConnection connection;

    private FenceScope(SqliteConnection connection, PlatformDbContext context)
    {
      this.connection = connection;
      Context = context;
    }

    public PlatformDbContext Context { get; }

    public static async Task<FenceScope> CreateAsync()
    {
      var connection = new SqliteConnection("Data Source=:memory:");
      await connection.OpenAsync();

      // No schema is created. Every guard under test throws from the change tracker before
      // `base.SaveChangesAsync` reaches the provider, so the connection exists only to satisfy the
      // context — see the note at the top of this file for why a real schema is not stood up.
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlite(connection)
        .Options;

      return new FenceScope(
        connection,
        new PlatformDbContext(options, new StubCurrentUser(), new StubCurrentTenant(), new StubClock()));
    }

    public void MarkDeleted(object entity) => Context.Entry(entity).State = EntityState.Deleted;

    public async ValueTask DisposeAsync()
    {
      await Context.DisposeAsync();
      await connection.DisposeAsync();
    }
  }

  private sealed class StubCurrentUser : ICurrentUser
  {
    public string? UserId => "guard-fence-tests";
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class StubCurrentTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class StubClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
  }
}
