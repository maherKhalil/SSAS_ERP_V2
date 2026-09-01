using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// TS-Backup Phase A domain invariants (ADR-022). The database enforces the same rules independently through
// CHECK constraints; these prove the application rejects them before a round trip and with a specific error.
public sealed class TenantDatabaseBackupPolicyTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_platform_managed_policy_is_created_with_a_trusted_destination_key()
  {
    var result = Create();

    Assert.True(result.IsSuccess);
    Assert.Equal(TenantDatabaseBackupManagementMode.AutomaticByPlatform, result.Value.ManagementMode);
    Assert.Equal("PrimaryBackupVault", result.Value.DestinationKey);
    Assert.Equal(10_080, result.Value.FullBackupIntervalMinutes);
    Assert.Equal(35, result.Value.RetentionExpectationDays);
    Assert.Equal(Now, result.Value.CreatedUtc);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_policy_carries_no_recovery_readiness_state()
  {
    // The structural reason a policy cannot make a database look recoverable: there is no readiness field
    // on this type to set. Protection is derived from evidence on TenantDatabase, never from configuration
    // existing here (ADR-022 §4, compliance rule 11).
    var properties = typeof(TenantDatabaseBackupPolicy).GetProperties().Select(property => property.Name);

    // ⚠ THE WITNESS IS THE PHYSICAL DATABASE ROW (252). `TenantDatabase` carries recovery readiness; the
    // POLICY must not duplicate it. Compile-checking against it keeps the two halves of that rule together.
    Assert.DoesNotContain(nameof(TenantDatabase.RecoveryReadinessStatus), properties);
    Assert.DoesNotContain("Protected", properties);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void An_enabled_platform_executed_policy_requires_a_destination_key()
  {
    var result = Create(destinationKey: "   ");

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupDestinationKeyRequired.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_customer_dba_policy_needs_no_platform_destination_key()
  {
    // The platform never executes this backup, so demanding a platform destination would be asking for a
    // value with no meaning (ADR-022 §12).
    var result = Create(
      managementMode: TenantDatabaseBackupManagementMode.CustomerDba,
      destinationKey: null,
      fullIntervalMinutes: null,
      differentialIntervalMinutes: null,
      logIntervalMinutes: null);

    Assert.True(result.IsSuccess);
    Assert.Null(result.Value.DestinationKey);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Customer_managed_hosting_defaults_to_customer_dba_authority()
  {
    Assert.Equal(
      TenantDatabaseBackupManagementMode.CustomerDba,
      TenantDatabaseBackupPolicy.DefaultManagementModeFor(TenantDatabaseHostingMode.CustomerManaged));

    // Dedicated placement does NOT transfer durability ownership to the customer, so platform-managed
    // hosting defaults to platform execution regardless of storage mode.
    Assert.Equal(
      TenantDatabaseBackupManagementMode.AutomaticByPlatform,
      TenantDatabaseBackupPolicy.DefaultManagementModeFor(TenantDatabaseHostingMode.PlatformManaged));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Log_backups_alone_are_not_a_backup_strategy()
  {
    // A transaction-log schedule with no full baseline configures a chain with no base: files that look
    // like backups and restore nothing (ADR-022 §9).
    var result = Create(fullIntervalMinutes: null, differentialIntervalMinutes: null, logIntervalMinutes: 15);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupFullBaselineRequired.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Differential_backups_alone_are_not_a_backup_strategy()
  {
    var result = Create(fullIntervalMinutes: null, differentialIntervalMinutes: 1_440, logIntervalMinutes: null);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupFullBaselineRequired.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void An_enabled_platform_executed_policy_must_schedule_a_full_baseline()
  {
    // Enabled, platform-executed, and scheduling nothing at all: a policy that protects nothing.
    var result = Create(
      fullIntervalMinutes: null, differentialIntervalMinutes: null, logIntervalMinutes: null);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupFullBaselineRequired.Code, result.Error.Code);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-15)]
  [Trait("Decision", "ADR-022")]
  public void A_schedule_interval_must_be_positive(int minutes)
  {
    var result = Create(fullIntervalMinutes: minutes);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupScheduleInvalid.Code, result.Error.Code);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  [Trait("Decision", "ADR-022")]
  public void Retention_expectation_must_be_positive(int days)
  {
    var result = Create(retentionDays: days);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupRetentionInvalid.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Verification_interval_must_be_positive_when_supplied()
  {
    var result = Create(verificationIntervalDays: 0);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupVerificationIntervalInvalid.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_destination_key_longer_than_the_bound_is_rejected()
  {
    var result = Create(
      destinationKey: new string('k', TenantDatabaseBackupPolicy.DestinationKeyMaximumLength + 1));

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupDestinationKeyInvalid.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Provider_native_encryption_is_not_supported_in_this_version()
  {
    // A declared extension point, not a capability: it implies managed key material ADR-022 forbids the
    // Platform database from holding.
    var result = Create(encryptionMode: TenantDatabaseBackupEncryptionMode.ProviderNative);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupEncryptionModeNotSupported.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Compression_defaults_to_preferred_where_supported()
  {
    // An edition that cannot compress takes an approved uncompressed backup rather than failing the policy;
    // Required is what makes an unavailable capability a genuine failure (ADR-022 §9).
    Assert.Equal(TenantDatabaseBackupCompressionMode.PreferredWhereSupported, Create().Value.CompressionMode);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_policy_must_belong_to_a_physical_database()
  {
    var result = Create(tenantDatabaseId: 0);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.TenantDatabaseRequired.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Update_revalidates_every_durability_affecting_field()
  {
    var policy = Create().Value;

    // Switching a live policy to a blank destination is exactly the kind of quiet change that would leave a
    // database configured but unprotected, so it is refused rather than accepted and reported later.
    var invalid = policy.Update(
      true, TenantDatabaseBackupManagementMode.AutomaticByPlatform, null,
      10_080, 1_440, 15, 35, 90, 60, "actor", Now);

    Assert.True(invalid.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupDestinationKeyRequired.Code, invalid.Error.Code);
    Assert.Equal("PrimaryBackupVault", policy.DestinationKey);

    var valid = policy.Update(
      false, TenantDatabaseBackupManagementMode.CustomerDba, null,
      null, null, null, 14, null, null, "actor", Now.AddMinutes(5));

    Assert.True(valid.IsSuccess);
    Assert.False(policy.Enabled);
    Assert.Equal(TenantDatabaseBackupManagementMode.CustomerDba, policy.ManagementMode);
    Assert.Equal(Now.AddMinutes(5), policy.ModifiedUtc);
  }

  private static SSAS.BuildingBlocks.Domain.Result<TenantDatabaseBackupPolicy> Create(
    long tenantDatabaseId = 1,
    bool enabled = true,
    TenantDatabaseBackupManagementMode managementMode = TenantDatabaseBackupManagementMode.AutomaticByPlatform,
    string? destinationKey = "PrimaryBackupVault",
    int? fullIntervalMinutes = 10_080,
    int? differentialIntervalMinutes = 1_440,
    int? logIntervalMinutes = 15,
    int retentionDays = 35,
    int? verificationIntervalDays = 90,
    int? maximumBackupAgeMinutes = 60,
    TenantDatabaseBackupEncryptionMode encryptionMode = TenantDatabaseBackupEncryptionMode.StorageManaged) =>
    TenantDatabaseBackupPolicy.Create(
      tenantDatabaseId,
      enabled,
      managementMode,
      destinationKey,
      fullIntervalMinutes,
      differentialIntervalMinutes,
      logIntervalMinutes,
      retentionDays,
      verificationIntervalDays,
      maximumBackupAgeMinutes,
      "actor",
      Now,
      encryptionMode: encryptionMode);
}
