using System.Reflection;
using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Domain.Companies;

namespace SSAS.Architecture.Tests;

public sealed class CompanyArchitectureTests
{
  [Fact]
  [Trait("Decision", "DEC-CMP-0001")]
  [Trait("Decision", "DEC-CMP-0004")]
  [Trait("Scenario", "TS-CMP-0085")]
  public void Company_is_tenant_owned_and_auditable_but_not_company_owned()
  {
    var interfaces = typeof(Company).GetInterfaces();

    Assert.Contains(typeof(ITenantOwnedEntity), interfaces);
    Assert.Contains(typeof(IAuditableEntity), interfaces);
    // ⚠ TYPED, NOT NAMED (252). This was `contract.Name == "ICompanyOwnedEntity"`, which passes when the
    // predicate matches NOTHING — so a typo in the name asserted nothing and still reported PASSED. That
    // was measured on this pattern, not argued. As a `typeof` a wrong name is CS0246 at build time.
    //
    // ⚠⚠ DO NOT APPLY THIS TO THE `type.Name == "ICompanyOwnedEntity"` BELOW — IT IS DELIBERATE. That one
    // resolves the interface by REFLECTION OVER `ITenantOwnedEntity`'S ASSEMBLY in order to assert WHICH
    // ASSEMBLY DECLARES IT; a `typeof` there binds at compile time and would assert nothing about location.
    // Its `Assert.NotNull` is the companion proving that lookup can match.
    Assert.DoesNotContain(typeof(ICompanyOwnedEntity), interfaces);
  }

  // ---- SUPERSEDED PREMISE, RETAINED PROTECTION (FP-006C1).
  //
  // This assertion previously required that `ICompanyOwnedEntity` did NOT exist. That was correct for FP-005
  // Milestone 1 and only for it: `DEC-CMP-0005` and `ADR-014` decision 6 deferred the interface "until the
  // first real company-owned business record", and FP-006C1 is that moment — `ADR-025` decision 1 introduces
  // it as shared infrastructure ahead of Employee.
  //
  // So the deferral half is retired by approved decision rather than by convenience, and what remains is the
  // half that never expired: the interface is a SEPARATE, OPT-IN contract in the shared Domain layer, and
  // `Company` is the company ROOT and must never implement it. That is the assertion with a live failure
  // mode — a Company scoped by company would be self-referential nonsense that nothing else would catch.
  [Fact]
  [Trait("Decision", "DEC-CMP-0005")]
  [Trait("Decision", "DEC-EMP-0002")]
  [Trait("Scenario", "TS-CMP-0086")]
  public void ICompanyOwnedEntity_is_a_separate_opt_in_contract_that_company_does_not_implement()
  {
    var companyOwned = typeof(ITenantOwnedEntity).Assembly.GetTypes()
      .SingleOrDefault(type => type.Name == "ICompanyOwnedEntity");

    // It lives beside ITenantOwnedEntity in the shared Domain layer, not in Platform: otherwise every future
    // company-owned module would depend on Platform's Domain to declare its own ownership.
    Assert.NotNull(companyOwned);
    Assert.True(companyOwned!.IsInterface);

    // OPT-IN, NOT INHERITED. Adding CompanyId to ITenantOwnedEntity would force a company dimension onto
    // every tenant-wide record that has none (`ADR-014` decision 4).
    Assert.DoesNotContain(companyOwned, typeof(ITenantOwnedEntity).GetInterfaces());
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), companyOwned.GetInterfaces());

    // And the company root is still not company-owned.
    Assert.DoesNotContain(typeof(Company).GetInterfaces(), contract => contract == companyOwned);
  }

  [Fact]
  [Trait("Decision", "DEC-CMP-0003")]
  public void Company_uses_a_guid_aggregate_key_and_exposes_no_physical_delete()
  {
    Assert.Equal(typeof(AggregateRoot<Guid>), typeof(Company).BaseType);
    Assert.DoesNotContain(
      typeof(Company).GetMethods().Select(method => method.Name),
      name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  [Trait("Security", "SEC-CMP-0205")]
  [Trait("Scenario", "TS-CMP-0083")]
  public void Company_events_contain_only_safe_values()
  {
    var eventTypes = typeof(Company).Assembly.GetTypes()
      .Where(type => typeof(DomainEvent).IsAssignableFrom(type))
      .Where(type => type.Namespace == "SSAS.Platform.Domain.Events")
      .Where(type => type.Name.StartsWith("Company", StringComparison.Ordinal))
      .ToArray();
    var unsafeProperties = eventTypes.SelectMany(type => type.GetProperties()
      .Where(property => Regex.IsMatch(
        property.Name,
        "Name|Code|Currency|Http|Claim|Credential|Secret|Password|Token|Actor|Correlation|Request|Trace|ReasonText",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
      .Select(property => $"{type.Name}.{property.Name}")).ToArray();

    Assert.Equal(5, eventTypes.Length);
    Assert.Empty(unsafeProperties);
  }

  [Fact]
  [Trait("NonFunctional", "NFR-CMP-0305")]
  public void Company_repository_is_aggregate_specific_without_delete_or_queryable()
  {
    Assert.False(typeof(ICompanyRepository).IsGenericType);
    var methods = typeof(ICompanyRepository).GetMethods();
    Assert.DoesNotContain(methods, method =>
      Regex.IsMatch(method.Name, "Delete|Remove", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    Assert.DoesNotContain(methods, method => method.ReturnType.ToString().Contains("IQueryable", StringComparison.Ordinal));
  }

  [Fact]
  [Trait("NonFunctional", "NFR-CMP-0301")]
  public void Company_command_and_query_handlers_are_async_and_accept_cancellation()
  {
    var handlers = new[]
    {
      typeof(CreateCompanyCommandHandler), typeof(UpdateCompanyProfileCommandHandler),
      typeof(ActivateCompanyCommandHandler), typeof(DeactivateCompanyCommandHandler),
      typeof(ArchiveCompanyCommandHandler), typeof(GetCompanyByIdQueryHandler),
      typeof(ListCompaniesQueryHandler)
    };

    Assert.All(handlers, handler =>
    {
      var method = Assert.Single(handler.GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Where(candidate => candidate.Name == "HandleAsync"));
      Assert.True(typeof(Task).IsAssignableFrom(method.ReturnType));
      Assert.Contains(method.GetParameters(), parameter => parameter.ParameterType == typeof(CancellationToken));
    });
  }
}
