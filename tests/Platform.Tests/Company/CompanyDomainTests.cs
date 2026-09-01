using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Events;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.Companies;

public sealed class CompanyDomainTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);
  private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

  [Fact]
  [Trait("Acceptance", "AC-CMP-0001")]
  [Trait("Scenario", "TS-CMP-0001")]
  public void Creation_produces_inactive_company_with_created_reason_and_safe_event()
  {
    var company = CreateCompany();
    var created = Assert.IsType<CompanyCreated>(Assert.Single(company.DomainEvents));

    Assert.NotEqual(Guid.Empty, company.CompanyId);
    Assert.Equal(TenantId, company.TenantId);
    Assert.Equal(CompanyStatus.Inactive, company.Status);
    Assert.Equal(CompanyStatusChangeReason.Created, company.StatusChangeReasonCode);
    Assert.Equal(Now, company.StatusChangedUtc);
    Assert.Equal("platform-actor", company.StatusChangedBy);
    Assert.Equal("EGP", company.BaseCurrencyCode.Value);
    Assert.Equal("ACME-EG", company.CompanyCode.Value);
    Assert.Equal("ACME-EG", company.NormalizedCompanyCode);
    Assert.Equal(company.CompanyId, created.CompanyId);
    Assert.Equal(TenantId, created.TenantId);
    Assert.Equal(CompanyStatus.Inactive, created.NewStatus);
    Assert.Equal(CompanyStatusChangeReason.Created, created.StatusChangeReason);
    Assert.Equal(Now, created.OccurredUtc);
  }

  [Fact]
  [Trait("Decision", "DEC-CMP-0003")]
  [Trait("Scenario", "TS-CMP-0001")]
  public void Creation_generates_a_server_side_guid_and_never_accepts_a_company_id()
  {
    var factory = Assert.Single(typeof(Company).GetMethods()
      .Where(method => method is { Name: nameof(Company.Create), IsPublic: true, IsStatic: true }));

    Assert.DoesNotContain(factory.GetParameters(), parameter =>
      string.Equals(parameter.Name, "companyId", StringComparison.OrdinalIgnoreCase));
    Assert.NotEqual(CreateCompany().CompanyId, CreateCompany().CompanyId);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-CMP-0008")]
  [Trait("Acceptance", "AC-CMP-0002")]
  [Trait("Scenario", "TS-CMP-0002")]
  public void Company_code_trims_preserves_casing_and_normalizes_invariantly()
  {
    var code = CompanyCode.Create("  Acme-eg  ").Value;

    Assert.Equal("Acme-eg", code.Value);
    Assert.Equal("ACME-EG", code.NormalizedValue);
    Assert.Equal(code, CompanyCode.Create("acme-EG").Value);
    Assert.False(typeof(CompanyCode).GetProperty(nameof(CompanyCode.Value))!.CanWrite);
    Assert.False(typeof(CompanyCode).GetProperty(nameof(CompanyCode.NormalizedValue))!.CanWrite);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [Trait("Scenario", "TS-CMP-0002")]
  public void Company_code_rejects_missing_values(string? value)
  {
    Assert.True(CompanyCode.Create(value).IsFailure);
    Assert.Equal("Company.InvalidCode", CompanyCode.Create(value).Error.Code);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-CMP-0008")]
  [Trait("Scenario", "TS-CMP-0002")]
  public void Company_code_enforces_the_limit_on_input_and_on_the_normalized_value()
  {
    Assert.True(CompanyCode.Create(new string('A', 65)).IsFailure);
    Assert.True(CompanyCode.Create(new string('A', 64)).IsSuccess);

    // The 64-character limit is enforced on the normalized value as well as on the accepted input. .NET's
    // ToUpperInvariant applies simple (1:1) case mapping and does not lengthen the string, so an accepted
    // maximum-length code always yields a normalized value that is also within the limit; the normalized-length
    // guard remains as defence-in-depth.
    var maxLengthCode = CompanyCode.Create(new string('a', 64));
    Assert.True(maxLengthCode.IsSuccess);
    Assert.True(maxLengthCode.Value.NormalizedValue.Length <= CompanyCode.MaximumLength);
  }

  [Theory]
  [InlineData("AB\tC")]
  [InlineData("AB\nC")]
  [InlineData("AB\0C")]
  [Trait("BusinessRule", "BRULE-CMP-0008")]
  [Trait("Scenario", "TS-CMP-0002")]
  public void Company_code_rejects_control_characters(string value)
  {
    Assert.True(CompanyCode.Create(value).IsFailure);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-CMP-0008")]
  [Trait("Scenario", "TS-CMP-0002")]
  public void Company_code_accepts_unicode_and_applies_no_unicode_normalization()
  {
    const string precomposedInput = "Café-eg"; // precomposed e-acute (U+00E9)
    var accepted = CompanyCode.Create(precomposedInput);
    Assert.True(accepted.IsSuccess);
    Assert.Equal(precomposedInput, accepted.Value.Value);
    Assert.Equal("CAFÉ-EG", accepted.Value.NormalizedValue); // uppercased E-acute (U+00C9)

    // A precomposed (U+00E9) and a decomposed (e + U+0301) form are NOT collapsed: no NFC/NFD normalization.
    var precomposed = CompanyCode.Create("café").Value;
    var decomposed = CompanyCode.Create("café").Value;
    Assert.NotEqual(precomposed.NormalizedValue, decomposed.NormalizedValue);
    Assert.NotEqual(precomposed, decomposed);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [Trait("BusinessRule", "BRULE-CMP-0009")]
  public void Company_name_rejects_missing_values(string? value)
  {
    Assert.True(CompanyName.Create(value).IsFailure);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-CMP-0009")]
  [Trait("Acceptance", "AC-CMP-0004")]
  public void Company_name_trims_preserves_casing_and_bounds_length()
  {
    var name = CompanyName.Create("  Acme Egypt  ").Value;

    Assert.Equal("Acme Egypt", name.Value);
    Assert.True(CompanyName.Create(new string('N', 200)).IsSuccess);
    Assert.True(CompanyName.Create(new string('N', 201)).IsFailure);
  }

  [Theory]
  [InlineData("EGP", "EGP")]
  [InlineData("usd", "USD")]
  [InlineData("  eur  ", "EUR")]
  [Trait("BusinessRule", "BRULE-CMP-0010")]
  [Trait("Acceptance", "AC-CMP-0005")]
  [Trait("Scenario", "TS-CMP-0004")]
  public void Base_currency_accepts_iso4217_and_canonicalizes_to_uppercase(string input, string expected)
  {
    var currency = BaseCurrencyCode.Create(input);

    Assert.True(currency.IsSuccess);
    Assert.Equal(expected, currency.Value.Value);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("US")]
  [InlineData("USDD")]
  [InlineData("US1")]
  [InlineData("ZZZ")]
  [Trait("BusinessRule", "BRULE-CMP-0010")]
  [Trait("Scenario", "TS-CMP-0004")]
  public void Base_currency_rejects_invalid_or_unknown_codes(string? value)
  {
    Assert.True(BaseCurrencyCode.Create(value).IsFailure);
    Assert.Equal("Company.InvalidBaseCurrency", BaseCurrencyCode.Create(value).Error.Code);
  }

  [Fact]
  [Trait("Decision", "DEC-CMP-0010")]
  [Trait("Decision", "DEC-CMP-0026")]
  public void Status_and_reason_vocabularies_are_exact()
  {
    Assert.Equal(["Inactive", "Active", "Archived"], Enum.GetNames<CompanyStatus>());
    Assert.Equal(
      ["Created", "Administrative", "Operational", "Compliance", "CustomerRequest", "IssueResolved"],
      Enum.GetNames<CompanyStatusChangeReason>());
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-CMP-0005")]
  [Trait("Acceptance", "AC-CMP-0006")]
  [Trait("Scenario", "TS-CMP-0005")]
  public void Activate_and_deactivate_form_a_reversible_pair_and_raise_safe_events()
  {
    var company = CreateCompany();
    company.ClearDomainEvents();

    Assert.True(company.Activate(CompanyStatusChangeReason.Administrative, "actor-1", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
    Assert.Equal(CompanyStatus.Active, company.Status);
    var activated = Assert.IsType<CompanyActivated>(Assert.Single(company.DomainEvents));
    Assert.Equal(CompanyStatus.Inactive, activated.PreviousStatus);
    Assert.Equal(CompanyStatus.Active, activated.NewStatus);
    company.ClearDomainEvents();

    Assert.True(company.Deactivate(CompanyStatusChangeReason.Operational, "actor-2", Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
    Assert.Equal(CompanyStatus.Inactive, company.Status);
    Assert.IsType<CompanyDeactivated>(Assert.Single(company.DomainEvents));
    Assert.Equal("actor-2", company.StatusChangedBy);
    Assert.Equal(Now.AddMinutes(2), company.StatusChangedUtc);
  }

  [Fact]
  [Trait("Decision", "DEC-CMP-0024")]
  [Trait("Scenario", "TS-CMP-0005")]
  public void Activate_requires_inactive_deactivate_requires_active_and_no_reactivate_exists()
  {
    var inactive = CreateCompany();
    Assert.True(inactive.Deactivate(CompanyStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now).IsFailure);

    var active = CreateInStatus(CompanyStatus.Active);
    Assert.True(active.Activate(CompanyStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now).IsFailure);

    // ⚠ Bound to `Branch.Reactivate` (258): same context, same kind — a `Result`-returning reactivation on
    // a Platform aggregate. `GetMethod` returns null for a method that does not exist AND for a misspelt
    // one, so the bare string could not tell "Company has no reactivation" from "I typed it wrong".
    Assert.Null(typeof(Company).GetMethod(nameof(SSAS.Platform.Domain.Branches.Branch.Reactivate)));
  }

  [Theory]
  [InlineData(CompanyStatus.Inactive)]
  [InlineData(CompanyStatus.Active)]
  [Trait("Acceptance", "AC-CMP-0008")]
  [Trait("Scenario", "TS-CMP-0006")]
  public void Archive_is_allowed_from_inactive_and_active(CompanyStatus status)
  {
    var company = CreateInStatus(status);

    Assert.True(company.Archive(CompanyStatusChangeReason.CustomerRequest, "actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.Equal(CompanyStatus.Archived, company.Status);
    Assert.IsType<CompanyArchived>(Assert.Single(company.DomainEvents));
  }

  [Theory]
  [InlineData(CompanyStatus.Inactive, "Deactivate")]
  [InlineData(CompanyStatus.Active, "Activate")]
  [InlineData(CompanyStatus.Archived, "Activate")]
  [InlineData(CompanyStatus.Archived, "Deactivate")]
  [InlineData(CompanyStatus.Archived, "Archive")]
  [InlineData(CompanyStatus.Archived, "UpdateProfile")]
  [Trait("BusinessRule", "BRULE-CMP-0003")]
  [Trait("Acceptance", "AC-CMP-0007")]
  [Trait("Scenario", "TS-CMP-0005")]
  public void Every_unapproved_transition_preserves_state_and_raises_no_event(CompanyStatus status, string operation)
  {
    var company = CreateInStatus(status);
    var previousChangedUtc = company.StatusChangedUtc;
    var previousChangedBy = company.StatusChangedBy;
    var previousReason = company.StatusChangeReasonCode;

    var result = operation switch
    {
      "Activate" => company.Activate(CompanyStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now.AddHours(1)),
      "Deactivate" => company.Deactivate(CompanyStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now.AddHours(1)),
      "Archive" => company.Archive(CompanyStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now.AddHours(1)),
      "UpdateProfile" => company.UpdateProfile(CompanyName.Create("New Name").Value, "actor", Guid.NewGuid(), Now.AddHours(1)),
      _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown lifecycle operation.")
    };

    Assert.True(result.IsFailure);
    Assert.Equal(status, company.Status);
    Assert.Equal(previousChangedUtc, company.StatusChangedUtc);
    Assert.Equal(previousChangedBy, company.StatusChangedBy);
    Assert.Equal(previousReason, company.StatusChangeReasonCode);
    Assert.Empty(company.DomainEvents);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-CMP-0011")]
  [Trait("Scenario", "TS-CMP-0009")]
  public void Created_and_undefined_reasons_are_rejected_for_transitions()
  {
    var company = CreateCompany();

    Assert.True(company.Activate(CompanyStatusChangeReason.Created, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(company.Activate((CompanyStatusChangeReason)999, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.Equal(CompanyStatus.Inactive, company.Status);
    Assert.True(company.Activate(CompanyStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(company.Archive(CompanyStatusChangeReason.Created, "actor", Guid.NewGuid(), Now).IsFailure);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-CMP-0011")]
  public void Invalid_actor_is_rejected_without_mutation_or_event()
  {
    var company = CreateCompany();
    company.ClearDomainEvents();

    Assert.True(company.Activate(CompanyStatusChangeReason.Administrative, "   ", Guid.NewGuid(), Now).IsFailure);
    Assert.True(company.Activate(CompanyStatusChangeReason.Administrative, new string('A', Company.ActorMaximumLength + 1), Guid.NewGuid(), Now).IsFailure);
    Assert.Equal(CompanyStatus.Inactive, company.Status);
    Assert.Empty(company.DomainEvents);
  }

  [Fact]
  [Trait("Requirement", "FR-CMP-0104")]
  [Trait("Acceptance", "AC-CMP-0004")]
  public void Update_profile_changes_only_the_name_and_raises_a_safe_event()
  {
    var company = CreateCompany();
    var originalCode = company.CompanyCode;
    var originalCurrency = company.BaseCurrencyCode;
    company.ClearDomainEvents();

    Assert.True(company.UpdateProfile(CompanyName.Create("Acme Egypt LLC").Value, "actor", Guid.NewGuid(), Now.AddMinutes(5)).IsSuccess);

    Assert.Equal("Acme Egypt LLC", company.CompanyName.Value);
    Assert.Equal(originalCode, company.CompanyCode);
    Assert.Equal(originalCurrency, company.BaseCurrencyCode);
    Assert.Equal(CompanyStatus.Inactive, company.Status);
    var updated = Assert.IsType<CompanyProfileUpdated>(Assert.Single(company.DomainEvents));
    Assert.Equal(company.CompanyId, updated.CompanyId);
    Assert.Equal(TenantId, updated.TenantId);
  }

  [Fact]
  [Trait("Security", "SEC-CMP-0208")]
  [Trait("Acceptance", "AC-CMP-0017")]
  public void Identity_currency_and_tenant_have_no_public_setter()
  {
    foreach (var propertyName in new[]
    {
      nameof(Company.CompanyId),
      nameof(Company.TenantId),
      nameof(Company.CompanyCode),
      nameof(Company.BaseCurrencyCode),
      nameof(Company.Status)
    })
    {
      var setter = typeof(Company).GetProperty(propertyName)!.SetMethod;
      Assert.True(setter is null || !setter.IsPublic, $"{propertyName} must not expose a public setter.");
    }
  }

  [Fact]
  [Trait("Security", "SEC-CMP-0205")]
  [Trait("Acceptance", "AC-CMP-0015")]
  [Trait("Scenario", "TS-CMP-0008")]
  public void Company_events_expose_no_display_text_or_sensitive_data()
  {
    var eventTypes = new[]
    {
      typeof(CompanyCreated), typeof(CompanyActivated), typeof(CompanyDeactivated),
      typeof(CompanyArchived), typeof(CompanyProfileUpdated)
    };

    foreach (var eventType in eventTypes)
    {
      Assert.True(typeof(DomainEvent).IsAssignableFrom(eventType));
      Assert.DoesNotContain(eventType.GetProperties(), property =>
        property.Name.Contains("Name", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Contains("Code", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Contains("Currency", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Contains("Actor", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-CMP-0001")]
  [Trait("Decision", "DEC-CMP-0004")]
  [Trait("Scenario", "TS-CMP-0085")]
  public void Company_is_tenant_owned_and_not_company_owned_and_exposes_no_delete()
  {
    var interfaces = typeof(Company).GetInterfaces();

    Assert.Contains(typeof(ITenantOwnedEntity), interfaces);
    Assert.Contains(typeof(IAuditableEntity), interfaces);
    Assert.DoesNotContain(typeof(ICompanyOwnedEntity), interfaces);
    Assert.DoesNotContain(
      typeof(Company).GetMethods().Select(method => method.Name),
      name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
  }

  private static Company CreateCompany(string code = "ACME-EG", string name = "Acme Egypt", string currency = "EGP")
  {
    return Company.Create(
      TenantId,
      CompanyCode.Create(code).Value,
      CompanyName.Create(name).Value,
      BaseCurrencyCode.Create(currency).Value,
      "platform-actor",
      Guid.NewGuid(),
      Now).Value;
  }

  private static Company CreateInStatus(CompanyStatus status)
  {
    var company = CreateCompany();
    if (status is CompanyStatus.Active)
    {
      Assert.True(company.Activate(CompanyStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now).IsSuccess);
    }

    if (status is CompanyStatus.Archived)
    {
      Assert.True(company.Archive(CompanyStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now).IsSuccess);
    }

    company.ClearDomainEvents();
    return company;
  }
}
