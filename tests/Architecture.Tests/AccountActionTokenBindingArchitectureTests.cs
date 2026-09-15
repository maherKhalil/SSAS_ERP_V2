using System.Reflection;
using SSAS.Platform.Domain.Authentication;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// THE OWNERSHIP BINDING A UNIQUE INDEX RESTS ON (item 181).
// ==================================================================================================
//
// `AccountActionTokenConfiguration` declares:
//
//     HasIndex(token => new { token.Purpose, token.TenantId, token.TenantUserId })
//       .IsUnique()
//       .HasFilter("[ConsumedUtc] IS NULL AND [RevokedUtc] IS NULL AND [TenantUserId] IS NOT NULL")
//
// ⚠ **`TenantId` IS NULLABLE AND THE FILTER NEVER MENTIONS IT.** SQL Server treats NULLs as EQUAL in a
// unique index, so two rows with `TenantId` null and the same `(Purpose, TenantUserId)` would collide and
// the second insert would be refused. The index is correct only because `TenantUserId IS NOT NULL`
// implies `TenantId IS NOT NULL`.
//
// ---- ⚠ WHAT ACTUALLY HOLDS THAT IMPLICATION, AND WHY THIS FILE EXISTS.
//
// Not a test, and not the runtime check inside the private constructor -- **the FACTORY SIGNATURES**.
// `CreateInvitation` takes `Guid tenantId` and `long tenantUserId`, both non-nullable; `CreatePasswordReset`
// takes neither; every other constructor is private. A mixed binding is therefore not EXPRESSIBLE, and a
// test asserting "a mixed binding throws" cannot be written through the public API (item 180).
//
// **So the index cannot check itself, and the property it depends on is a signature.** This guard checks
// the signature. It reddens on the one edit that would make the mixed binding constructible -- widening
// either parameter -- and its message names the index, because whoever hits it will be editing a domain
// factory with no reason to suspect an index rests on it.
//
// ---- BOTH HALVES, DELIBERATELY.
//
// The invariant is *Invitation sets both* AND *PasswordReset sets neither*. A guard over one half would
// pass while the other rotted -- the union-collapse shape. Both are asserted.
public sealed class AccountActionTokenBindingArchitectureTests
{
  private const string Because =
    "AccountActionTokenConfiguration's unique index on (Purpose, TenantId, TenantUserId) is filtered on " +
    "[TenantUserId] IS NOT NULL and never mentions TenantId, which is nullable. It is correct only " +
    "because these two identifiers are set together or not at all. Widening this parameter makes a mixed " +
    "binding constructible, and the index then admits a second row whose TenantId is null - refused at " +
    "insert. Change the index before changing this signature.";

  [Fact]
  public void Invitation_requires_both_tenant_identifiers_as_non_nullable_values()
  {
    var factory = Factory("CreateInvitation");

    foreach (var name in new[] { "tenantId", "tenantUserId" })
    {
      var parameter = factory.GetParameters()
        .SingleOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));

      // ⚠ A RENAMED PARAMETER WOULD OTHERWISE MAKE THIS PASS OVER NOTHING.
      Assert.True(parameter is not null, $"CreateInvitation has no parameter named '{name}'. {Because}");

      Assert.True(
        parameter!.ParameterType.IsValueType && Nullable.GetUnderlyingType(parameter.ParameterType) is null,
        $"CreateInvitation's '{name}' is {parameter.ParameterType.Name}, which admits a missing value. {Because}");
    }
  }

  // ---- THE OTHER HALF: A PASSWORD RESET CANNOT CARRY A TENANT BINDING AT ALL.
  [Fact]
  public void Password_reset_accepts_neither_tenant_identifier()
  {
    var factory = Factory("CreatePasswordReset");

    var carried = factory.GetParameters()
      .Where(item => item.Name is "tenantId" or "tenantUserId")
      .Select(item => item.Name!)
      .ToArray();

    Assert.True(
      carried.Length == 0,
      $"CreatePasswordReset accepts [{string.Join(", ", carried)}], so a reset token can now carry a " +
      $"partial tenant binding. {Because}");
  }

  // ---- ⚠ THE MATCHER CONTROL. A GUARD THAT RESOLVES NO METHOD PASSES.
  // Both tests route through here, so a renamed or removed factory fails loudly rather than silently
  // leaving the assertions above with nothing to inspect.
  private static MethodInfo Factory(string name)
  {
    var factory = typeof(AccountActionToken)
      .GetMethod(name, BindingFlags.Public | BindingFlags.Static);

    Assert.True(
      factory is not null,
      $"AccountActionToken has no public static '{name}'. This guard cannot see the binding it protects. {Because}");

    return factory!;
  }

  // ---- AND THAT THE FACTORIES ARE STILL THE ONLY WAY IN.
  // The signatures only hold the invariant while nothing else can construct one. A new public factory,
  // rehydrate or setter would route around both assertions above without reddening either.
  [Fact]
  public void The_two_factories_are_the_only_public_construction_points()
  {
    var publicFactories = typeof(AccountActionToken)
      .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
      .Where(method => method.ReturnType == typeof(AccountActionToken))
      .Select(method => method.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(["CreateInvitation", "CreatePasswordReset"], publicFactories);
    Assert.Empty(typeof(AccountActionToken).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
  }
}
