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
    Assert.DoesNotContain(interfaces, contract => contract.Name == "ICompanyOwnedEntity");
  }

  [Fact]
  [Trait("Decision", "DEC-CMP-0005")]
  [Trait("Scenario", "TS-CMP-0086")]
  public void ICompanyOwnedEntity_interface_is_not_introduced_in_milestone_one()
  {
    var domainTypes = typeof(Company).Assembly.GetTypes()
      .Concat(typeof(ITenantOwnedEntity).Assembly.GetTypes());

    Assert.DoesNotContain(domainTypes, type => type.Name == "ICompanyOwnedEntity");
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
