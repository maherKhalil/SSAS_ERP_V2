using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Events;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.TenantLifecycle;

public sealed class TenantLifecycleDomainTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("BusinessRule", "BRULE-TEN-0010")]
  [Trait("Acceptance", "AC-TEN-0002")]
  [Trait("Scenario", "TS-TEN-0002")]
  public void Tenant_code_trims_preserves_casing_and_normalizes_invariantly()
  {
    var code = TenantCode.Create("  Acme-tr  ").Value;

    Assert.Equal("Acme-tr", code.Value);
    Assert.Equal("ACME-TR", code.NormalizedValue);
    Assert.Equal(code, TenantCode.Create("acme-TR").Value);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [Trait("Decision", "DEC-TEN-0004")]
  public void Tenant_code_rejects_missing_values(string? value)
  {
    Assert.True(TenantCode.Create(value).IsFailure);
  }

  [Fact]
  [Trait("Scenario", "TS-TEN-0002")]
  public void Tenant_code_rejects_values_over_64_characters()
  {
    Assert.True(TenantCode.Create(new string('A', 65)).IsFailure);
    Assert.True(TenantCode.Create(new string('A', 64)).IsSuccess);
    Assert.False(typeof(TenantCode).GetProperty(nameof(TenantCode.Value))!.CanWrite);
    Assert.False(typeof(TenantCode).GetProperty(nameof(TenantCode.NormalizedValue))!.CanWrite);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [Trait("BusinessRule", "BRULE-TEN-0011")]
  public void Tenant_name_rejects_missing_values(string? value)
  {
    Assert.True(TenantName.Create(value).IsFailure);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-TEN-0011")]
  [Trait("Acceptance", "AC-TEN-0003")]
  [Trait("Scenario", "TS-TEN-0003")]
  public void Tenant_name_trims_preserves_casing_and_is_not_an_identity()
  {
    var first = CreateTenant("ONE", "  Shared Name  ");
    var second = CreateTenant("TWO", "Shared Name");

    Assert.Equal("Shared Name", first.TenantName.Value);
    Assert.Equal("Shared Name", second.TenantName.Value);
    Assert.NotEqual(first.TenantId, second.TenantId);
    Assert.True(TenantName.Create(new string('N', 201)).IsFailure);
  }

  [Fact]
  [Trait("Requirement", "FR-TEN-0101")]
  [Trait("Acceptance", "AC-TEN-0001")]
  [Trait("Scenario", "TS-TEN-0001")]
  public void Creation_uses_server_identifier_provisioning_created_reason_and_safe_event()
  {
    var tenant = CreateTenant();
    var created = Assert.IsType<TenantCreated>(Assert.Single(tenant.DomainEvents));

    Assert.NotEqual(Guid.Empty, tenant.TenantId);
    Assert.Equal(TenantStatus.Provisioning, tenant.Status);
    Assert.False(tenant.IsAuthenticationEligible);
    Assert.Equal(TenantStatusChangeReason.Created, tenant.StatusChangeReasonCode);
    Assert.Equal(Now, tenant.CreatedUtc);
    Assert.Equal(Now, tenant.StatusChangedUtc);
    Assert.Equal("platform-actor", tenant.CreatedBy);
    Assert.Equal("platform-actor", tenant.StatusChangedBy);
    Assert.Null(tenant.ModifiedUtc);
    Assert.Equal(tenant.TenantId, created.TenantId);
    Assert.Equal(TenantStatus.Provisioning, created.NewStatus);
    Assert.Equal(TenantStatusChangeReason.Created, created.StatusChangeReason);
    Assert.Equal(Now, created.OccurredUtc);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-TEN-0009")]
  [Trait("Decision", "DEC-TEN-0001")]
  [Trait("Scenario", "TS-TEN-0008")]
  public void Creation_factory_generates_immutable_identifier_and_rejects_null_value_objects()
  {
    var code = TenantCode.Create("ACME").Value;
    var name = TenantName.Create("Acme Trading").Value;

    var missingCode = Tenant.Create(null!, name, "actor", Guid.NewGuid(), Now);
    var missingName = Tenant.Create(code, null!, "actor", Guid.NewGuid(), Now);
    var factory = Assert.Single(typeof(Tenant).GetMethods()
      .Where(method => method is { Name: nameof(Tenant.Create), IsPublic: true, IsStatic: true }));

    Assert.Equal("Tenant.InvalidCode", missingCode.Error.Code);
    Assert.Equal("Tenant.InvalidName", missingName.Error.Code);
    Assert.DoesNotContain(factory.GetParameters(), parameter =>
      string.Equals(parameter.Name, "tenantId", StringComparison.OrdinalIgnoreCase));
    Assert.False(typeof(Tenant).GetProperty(nameof(Tenant.TenantId))!.CanWrite);
    Assert.NotEqual(CreateTenant().TenantId, CreateTenant().TenantId);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0002")]
  [Trait("Decision", "DEC-TEN-0009")]
  public void Status_and_reason_vocabularies_are_exact()
  {
    Assert.Equal(
      ["Provisioning", "Active", "Suspended", "Archived"],
      Enum.GetNames<TenantStatus>());
    Assert.Equal(
      ["Created", "ProvisioningCompleted", "Administrative", "Security", "Compliance", "Operational", "CustomerClosure", "IssueResolved"],
      Enum.GetNames<TenantStatusChangeReason>());
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-TEN-0003")]
  [Trait("Acceptance", "AC-TEN-0005")]
  [Trait("Scenario", "TS-TEN-0004")]
  public void Every_approved_transition_updates_trusted_metadata_and_raises_safe_event()
  {
    var tenant = CreateTenant();
    tenant.ClearDomainEvents();

    Assert.True(tenant.Activate("actor-1", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
    Assert.Equal(TenantStatus.Active, tenant.Status);
    Assert.True(tenant.IsAuthenticationEligible);
    Assert.IsType<TenantActivated>(Assert.Single(tenant.DomainEvents));
    tenant.ClearDomainEvents();

    Assert.True(tenant.Suspend(TenantStatusChangeReason.Security, "actor-2", Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
    Assert.Equal(TenantStatus.Suspended, tenant.Status);
    Assert.False(tenant.IsAuthenticationEligible);
    Assert.IsType<TenantSuspended>(Assert.Single(tenant.DomainEvents));
    tenant.ClearDomainEvents();

    Assert.True(tenant.Reactivate(TenantStatusChangeReason.IssueResolved, "actor-3", Guid.NewGuid(), Now.AddMinutes(3)).IsSuccess);
    Assert.Equal(TenantStatus.Active, tenant.Status);
    Assert.IsType<TenantReactivated>(Assert.Single(tenant.DomainEvents));
    tenant.ClearDomainEvents();

    Assert.True(tenant.Archive(TenantStatusChangeReason.CustomerClosure, "actor-4", Guid.NewGuid(), Now.AddMinutes(4)).IsSuccess);
    var archived = Assert.IsType<TenantArchived>(Assert.Single(tenant.DomainEvents));
    Assert.Equal(TenantStatus.Archived, tenant.Status);
    Assert.Equal("actor-4", tenant.ModifiedBy);
    Assert.Equal(Now.AddMinutes(4), tenant.ModifiedUtc);
    Assert.Equal(TenantStatusChangeReason.CustomerClosure, archived.StatusChangeReason);
  }

  [Theory]
  [InlineData(TenantStatus.Provisioning)]
  [InlineData(TenantStatus.Active)]
  [InlineData(TenantStatus.Suspended)]
  [Trait("Acceptance", "AC-TEN-0010")]
  public void Archive_is_allowed_from_every_nonterminal_status(TenantStatus status)
  {
    var tenant = CreateInStatus(status);

    Assert.True(tenant.Archive(TenantStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.Equal(TenantStatus.Archived, tenant.Status);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-TEN-0004")]
  [Trait("Acceptance", "AC-TEN-0006")]
  [Trait("Scenario", "TS-TEN-0005")]
  public void Invalid_repeated_and_archived_transitions_change_nothing()
  {
    var tenant = CreateTenant();
    Assert.True(tenant.Suspend(TenantStatusChangeReason.Security, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Activate("actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(tenant.Activate("actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Archive(TenantStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now).IsSuccess);
    tenant.ClearDomainEvents();

    Assert.True(tenant.Reactivate(TenantStatusChangeReason.IssueResolved, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Archive(TenantStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.Equal(TenantStatus.Archived, tenant.Status);
    Assert.Empty(tenant.DomainEvents);
  }

  [Theory]
  [InlineData(TenantStatus.Provisioning, "Suspend")]
  [InlineData(TenantStatus.Provisioning, "Reactivate")]
  [InlineData(TenantStatus.Active, "Activate")]
  [InlineData(TenantStatus.Active, "Reactivate")]
  [InlineData(TenantStatus.Suspended, "Activate")]
  [InlineData(TenantStatus.Suspended, "Suspend")]
  [InlineData(TenantStatus.Archived, "Activate")]
  [InlineData(TenantStatus.Archived, "Suspend")]
  [InlineData(TenantStatus.Archived, "Reactivate")]
  [InlineData(TenantStatus.Archived, "Archive")]
  [Trait("BusinessRule", "BRULE-TEN-0003")]
  [Trait("Acceptance", "AC-TEN-0006")]
  [Trait("Scenario", "TS-TEN-0005")]
  public void Every_unapproved_transition_preserves_state_metadata_and_events(TenantStatus status, string operation)
  {
    var tenant = CreateInStatus(status);
    var previousModifiedUtc = tenant.ModifiedUtc;
    var previousModifiedBy = tenant.ModifiedBy;
    var previousChangedUtc = tenant.StatusChangedUtc;
    var previousChangedBy = tenant.StatusChangedBy;
    var previousReason = tenant.StatusChangeReasonCode;

    var result = operation switch
    {
      "Activate" => tenant.Activate("actor", Guid.NewGuid(), Now.AddHours(1)),
      "Suspend" => tenant.Suspend(TenantStatusChangeReason.Security, "actor", Guid.NewGuid(), Now.AddHours(1)),
      "Reactivate" => tenant.Reactivate(TenantStatusChangeReason.IssueResolved, "actor", Guid.NewGuid(), Now.AddHours(1)),
      "Archive" => tenant.Archive(TenantStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now.AddHours(1)),
      _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown lifecycle operation.")
    };

    Assert.True(result.IsFailure);
    Assert.Equal(status, tenant.Status);
    Assert.Equal(previousModifiedUtc, tenant.ModifiedUtc);
    Assert.Equal(previousModifiedBy, tenant.ModifiedBy);
    Assert.Equal(previousChangedUtc, tenant.StatusChangedUtc);
    Assert.Equal(previousChangedBy, tenant.StatusChangedBy);
    Assert.Equal(previousReason, tenant.StatusChangeReasonCode);
    Assert.Empty(tenant.DomainEvents);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0009")]
  [Trait("Scenario", "TS-TEN-0007")]
  public void Created_reason_is_rejected_for_transitions_and_reactivation_uses_bounded_resolution_reasons()
  {
    var tenant = CreateInStatus(TenantStatus.Active);
    Assert.True(tenant.Suspend(TenantStatusChangeReason.Created, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Suspend(TenantStatusChangeReason.ProvisioningCompleted, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Suspend((TenantStatusChangeReason)999, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Suspend(TenantStatusChangeReason.Security, "actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(tenant.Reactivate(TenantStatusChangeReason.CustomerClosure, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Reactivate(TenantStatusChangeReason.ProvisioningCompleted, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Reactivate(TenantStatusChangeReason.Operational, "actor", Guid.NewGuid(), Now).IsSuccess);

    var archive = CreateTenant();
    Assert.True(archive.Archive(TenantStatusChangeReason.Created, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(archive.Archive((TenantStatusChangeReason)999, "actor", Guid.NewGuid(), Now).IsFailure);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-TEN-0012")]
  [Trait("Scenario", "TS-TEN-0007")]
  public void Invalid_actor_is_rejected_without_mutation_or_event()
  {
    var tenant = CreateTenant();
    tenant.ClearDomainEvents();

    Assert.True(tenant.Activate("   ", Guid.NewGuid(), Now.AddMinutes(1)).IsFailure);
    Assert.True(tenant.Activate(new string('A', Tenant.ActorMaximumLength + 1), Guid.NewGuid(), Now.AddMinutes(1)).IsFailure);
    Assert.Equal(TenantStatus.Provisioning, tenant.Status);
    Assert.Null(tenant.ModifiedUtc);
    Assert.Empty(tenant.DomainEvents);
  }

  [Fact]
  [Trait("Security", "SEC-TEN-0206")]
  [Trait("Scenario", "TS-TEN-0031")]
  public void Tenant_exposes_no_delete_or_tenant_owned_behavior()
  {
    var methods = typeof(Tenant).GetMethods().Select(method => method.Name).ToArray();

    Assert.DoesNotContain(methods, name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    Assert.DoesNotContain(typeof(SSAS.BuildingBlocks.Domain.ITenantOwnedEntity), typeof(Tenant).GetInterfaces());
    Assert.Null(typeof(Tenant).GetMethod("UpdateName"));
    Assert.Null(typeof(Tenant).GetMethod("Rename"));
  }

  private static Tenant CreateTenant(string code = "ACME", string name = "Acme Trading")
  {
    return Tenant.Create(
      TenantCode.Create(code).Value,
      TenantName.Create(name).Value,
      "platform-actor",
      Guid.NewGuid(),
      Now).Value;
  }

  private static Tenant CreateInStatus(TenantStatus status)
  {
    var tenant = CreateTenant();
    if (status is TenantStatus.Active or TenantStatus.Suspended)
    {
      Assert.True(tenant.Activate("actor", Guid.NewGuid(), Now).IsSuccess);
    }

    if (status == TenantStatus.Suspended)
    {
      Assert.True(tenant.Suspend(TenantStatusChangeReason.Security, "actor", Guid.NewGuid(), Now).IsSuccess);
    }

    if (status == TenantStatus.Archived)
    {
      Assert.True(tenant.Archive(TenantStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now).IsSuccess);
    }

    tenant.ClearDomainEvents();
    return tenant;
  }
}
