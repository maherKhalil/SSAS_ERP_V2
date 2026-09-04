using System.Reflection;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// EVERY CLASS THAT TAKES THE HOST FACTORY IS IN THE COLLECTION THAT SERIALISES IT.
// ==================================================================================================
//
// `HostWebApplicationFactory` builds `WebApplicationFactory<Program>` — the real Host. **Two of them
// initialising concurrently race on process-wide logging state**, and the failure is
// `InvalidOperationException: The logger is already frozen`, thrown from whichever host loses.
//
// ---- ⚠ MEASURED, NOT ANTICIPATED. THIS RULE EXISTS BECAUSE THE RACE HAPPENED.
//
// A test class was added taking the factory as an `IClassFixture`. One other class already did.
// `IClassFixture` gives each class its own instance and xUnit runs separate collections in parallel, so
// there were suddenly two hosts building at once. **It passed when its own suite ran alone and failed under
// the full gate** — the shape that reads as a flaky assertion and is a harness defect.
//
// ⚠⚠ AND THE DIAGNOSIS WAS THE EXPENSIVE PART: the gate names the TEST that failed, so the report was "your
// new test fails", and the failure had nothing to do with what the test asserted. **A harness defect wears
// the costume of the assertion standing on it.**
//
// ---- WHY AN ASSERTION AND NOT A NOTE.
//
// The hazard is created by an ACT — someone adding the second class — and nobody is present at that moment.
// A comment saying "do not do this" is read after the race, if ever. **This fires on the act itself**, at
// the cost of one reflection walk and no serialisation.
//
// ---- ⚠ NO EXEMPTIONS, DELIBERATELY, AND THAT COST ONE CLASS'S PARALLELISM.
//
// `ApiContractRowGuardTests` was the one class outside the collection, and it was safe only because it was
// alone — a property of the tree, not of the class. **It was moved in rather than exempted.** An exemption
// list with one name records a fact ("it is currently the only one") that stops being true precisely when
// the rule is needed, and a reader cannot tell a legitimate exemption from a forgotten one.
public sealed class HostFixtureCollectionTests
{
  [Fact]
  public void Every_test_class_using_the_host_factory_is_in_the_serialising_collection()
  {
    var users = typeof(HostFixtureCollectionTests).Assembly.GetTypes()
      .Where(TakesTheHostFactory)
      .OrderBy(type => type.FullName, StringComparer.Ordinal)
      .ToArray();

    // ⚠ ANTI-VACUITY, AND IT GUARDS THE SPECIFIC WAY THIS WALK DIES: both detection routes below are
    // reflection over shapes that a refactor can change — a renamed fixture, a factory taken through a base
    // class, a helper that resolves it instead of receiving it. If `users` empties, every class is
    // trivially compliant and this passes loudest when it is judging nothing.
    //
    // ⚠ 20 users measured today, by raising this floor until the assertion printed the count. The floor
    // sits below with room for several to go away — it is an anti-vacuity control and NOT a membership
    // guard, because a class that leaves the collection stays in this population and fails BY NAME below.
    Assert.True(users.Length >= 15,
      $"only {users.Length} test classes were found taking {nameof(HostWebApplicationFactory)}; the " +
      "detection below has stopped matching and every class is trivially compliant.");

    var outside = users
      .Where(type => CollectionNameOf(type) != HostIntegrationTestGroup.Name)
      .Select(type => type.FullName!)
      .ToArray();

    Assert.True(outside.Length == 0,
      $"these classes take {nameof(HostWebApplicationFactory)} but are not in " +
      $"[Collection({nameof(HostIntegrationTestGroup)}.Name)], so xUnit may run them in parallel with the " +
      "classes that are. Two WebApplicationFactory<Program> instances initialising at once race on " +
      "process-wide logging state and one throws 'The logger is already frozen' — a failure that names " +
      "whichever test lost rather than the class that caused it:\n  " + string.Join("\n  ", outside));
  }

  // ⚠ THE NAME IS READ FROM THE ATTRIBUTE'S CONSTRUCTOR ARGUMENT, NOT FROM A PROPERTY: this xUnit's
  // `CollectionAttribute` exposes no public `Name`, so the value it was constructed with is the only place
  // it exists. `GetCustomAttributesData` reads what the compiler emitted.
  private static string? CollectionNameOf(Type type) =>
    type.GetCustomAttributesData()
      .Where(data => data.AttributeType == typeof(CollectionAttribute))
      .Select(data => data.ConstructorArguments.Count == 1
        ? data.ConstructorArguments[0].Value as string
        : null)
      .FirstOrDefault();

  // ---- BOTH ROUTES INTO THE HAZARD, BECAUSE EITHER ONE CREATES A SECOND HOST.
  //
  // A class can receive the factory by CONSTRUCTOR (how the collection's members take it, from the
  // collection fixture) or by declaring `IClassFixture<HostWebApplicationFactory>` (which mints its own).
  // **The second is the one that actually races**, but a class doing the first while outside the collection
  // would not compile against a fixture nothing supplies — so checking both is checking the hazard rather
  // than one spelling of it.
  private static bool TakesTheHostFactory(Type type) =>
    type.GetConstructors().Any(constructor => constructor.GetParameters()
      .Any(parameter => parameter.ParameterType == typeof(HostWebApplicationFactory)))
    || type.GetInterfaces().Any(contract => contract.IsGenericType
      && contract.GetGenericTypeDefinition() == typeof(IClassFixture<>)
      && contract.GetGenericArguments()[0] == typeof(HostWebApplicationFactory));
}
