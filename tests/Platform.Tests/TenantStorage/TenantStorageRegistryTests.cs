using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// TS-1A domain invariants (ADR-017). The database enforces the same rules independently; these prove the
// application rejects them before a round trip and with a specific error code.
public sealed class TenantStorageRegistryTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 13, 11, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Register_creates_a_platform_managed_shared_database()
  {
    var result = TenantDatabase.Register(
      TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseStorageMode.Shared,
      "PrimarySqlServer", "SSAS_Shared_01", TenantDatabaseProvisioningStatus.Ready, "actor", Now);

    Assert.True(result.IsSuccess);
    Assert.Equal(TenantDatabaseHostingMode.PlatformManaged, result.Value.HostingMode);
    Assert.Equal(TenantDatabaseStorageMode.Shared, result.Value.StorageMode);
    Assert.Equal("PrimarySqlServer", result.Value.ServerKey);
    Assert.Equal("SSAS_Shared_01", result.Value.DatabaseName);
    Assert.Equal(TenantDatabaseProvisioningStatus.Ready, result.Value.ProvisioningStatus);
    Assert.Equal(Now, result.Value.CreatedUtc);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Customer_managed_storage_must_be_dedicated()
  {
    var result = TenantDatabase.Register(
      TenantDatabaseHostingMode.CustomerManaged, TenantDatabaseStorageMode.Shared,
      "CustomerServer", "CustomerERP", TenantDatabaseProvisioningStatus.Registered, "actor", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.CustomerManagedMustBeDedicated.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Customer_managed_dedicated_is_data_model_valid_but_not_runtime_supported()
  {
    // Architecture-ready only: no routing, endpoint, credential or connectivity code consumes this value
    // in this slice (guarded by the architecture tests).
    var result = TenantDatabase.Register(
      TenantDatabaseHostingMode.CustomerManaged, TenantDatabaseStorageMode.Dedicated,
      "CustomerServer", "CustomerERP", TenantDatabaseProvisioningStatus.Registered, "actor", Now);

    Assert.True(result.IsSuccess);
  }

  [Theory]
  [Trait("Decision", "ADR-017")]
  [InlineData("")]
  [InlineData("   ")]
  public void Server_key_is_required(string serverKey)
  {
    var result = TenantDatabase.Register(
      TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseStorageMode.Shared,
      serverKey, "SSAS_Shared_01", TenantDatabaseProvisioningStatus.Ready, "actor", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.ServerKeyRequired.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Server_key_is_length_bounded()
  {
    var result = TenantDatabase.Register(
      TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseStorageMode.Shared,
      new string('k', TenantDatabase.ServerKeyMaximumLength + 1), "SSAS_Shared_01",
      TenantDatabaseProvisioningStatus.Ready, "actor", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.ServerKeyRequired.Code, result.Error.Code);
  }

  [Theory]
  [Trait("Decision", "ADR-017")]
  [InlineData("")]
  [InlineData("   ")]
  public void Database_name_is_required(string databaseName)
  {
    var result = TenantDatabase.Register(
      TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseStorageMode.Shared,
      "PrimarySqlServer", databaseName, TenantDatabaseProvisioningStatus.Ready, "actor", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.DatabaseNameRequired.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Database_name_is_bounded_to_the_sql_catalog_limit()
  {
    var result = TenantDatabase.Register(
      TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseStorageMode.Shared,
      "PrimarySqlServer", new string('d', TenantDatabase.DatabaseNameMaximumLength + 1),
      TenantDatabaseProvisioningStatus.Ready, "actor", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.DatabaseNameRequired.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Initial_assignment_starts_active_at_routing_version_one()
  {
    var tenantId = Guid.NewGuid();
    var result = TenantDatabaseAssignment.CreateInitial(tenantId, 25, "bootstrap", "actor", Now);

    Assert.True(result.IsSuccess);
    Assert.Equal(tenantId, result.Value.TenantId);
    Assert.Equal(25, result.Value.TenantDatabaseId);
    Assert.Equal(TenantDatabaseAssignment.InitialRoutingVersion, result.Value.RoutingVersion);
    Assert.Null(result.Value.EndedUtc);
    Assert.True(result.Value.IsActive);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Assignment_requires_a_tenant_and_a_database()
  {
    Assert.Equal(
      TenantStorageErrors.TenantRequired.Code,
      TenantDatabaseAssignment.CreateInitial(Guid.Empty, 25, null, "actor", Now).Error.Code);
    Assert.Equal(
      TenantStorageErrors.TenantDatabaseRequired.Code,
      TenantDatabaseAssignment.CreateInitial(Guid.NewGuid(), 0, null, "actor", Now).Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Routing_version_must_be_positive()
  {
    var result = TenantDatabaseAssignment.Create(Guid.NewGuid(), 25, 0, null, "actor", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RoutingVersionInvalid.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Ending_an_assignment_retains_it_as_history()
  {
    var assignment = TenantDatabaseAssignment.CreateInitial(Guid.NewGuid(), 25, null, "actor", Now).Value;

    Assert.True(assignment.End("actor", Now.AddMinutes(5)).IsSuccess);
    Assert.False(assignment.IsActive);
    Assert.Equal(Now.AddMinutes(5), assignment.EndedUtc);

    // Ending twice is a conflict, not a silent no-op: the second caller's intent is unclear.
    Assert.Equal(
      TenantStorageErrors.AssignmentAlreadyEnded.Code,
      assignment.End("actor", Now.AddMinutes(6)).Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void An_assignment_cannot_end_before_it_was_assigned()
  {
    var assignment = TenantDatabaseAssignment.CreateInitial(Guid.NewGuid(), 25, null, "actor", Now).Value;

    var result = assignment.End("actor", Now.AddMinutes(-1));

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.AssignmentEndBeforeStart.Code, result.Error.Code);
    Assert.True(assignment.IsActive);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Reason_is_truncated_to_the_persisted_length()
  {
    var result = TenantDatabaseAssignment.CreateInitial(
      Guid.NewGuid(), 25, new string('r', TenantDatabaseAssignment.ReasonMaximumLength + 50), "actor", Now);

    Assert.True(result.IsSuccess);
    Assert.Equal(TenantDatabaseAssignment.ReasonMaximumLength, result.Value.Reason!.Length);
  }
}
