using SSAS.Platform.Domain.TenantStorage;
using Xunit;

namespace SSAS.Platform.Tests.TenantStorage;

// The reserved verification namespace and the guard that stands in front of destruction (ADR-022 §17).
//
// These matter more than their size suggests: the predicate exercised here is the one an automated
// `DROP DATABASE` will eventually consult, so every way it could be too permissive is worth a test now,
// while nothing can yet act on it.
[Trait("Decision", "ADR-022")]
public sealed class TenantDatabaseVerificationNamingTests
{
  [Fact]
  public void A_generated_name_carries_the_reserved_prefix_and_both_identities()
  {
    var name = TenantDatabaseVerificationNaming.ForRun(42, 7);

    Assert.Equal("SSAS_Verify_42_7", name);
    Assert.True(TenantDatabaseVerificationNaming.IsVerificationDatabaseName(name));
  }

  // Uniqueness by construction: the run identity is in the name, so two operations can never target the
  // same database.
  [Fact]
  public void Different_runs_never_generate_the_same_name()
  {
    Assert.NotEqual(
      TenantDatabaseVerificationNaming.ForRun(42, 7),
      TenantDatabaseVerificationNaming.ForRun(42, 8));
  }

  [Fact]
  public void The_generated_name_stays_within_the_sql_server_identifier_limit_at_extreme_identities()
  {
    var name = TenantDatabaseVerificationNaming.ForRun(long.MaxValue, long.MaxValue);

    Assert.True(name.Length <= TenantDatabaseVerificationNaming.MaximumLength);
    Assert.True(TenantDatabaseVerificationNaming.IsVerificationDatabaseName(name));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public void A_non_positive_identity_is_refused(long identity)
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => TenantDatabaseVerificationNaming.ForRun(identity, 1));
    Assert.Throws<ArgumentOutOfRangeException>(() => TenantDatabaseVerificationNaming.ForRun(1, identity));
  }

  // THE RESERVED NAMESPACE IS NOT A PREFIX MATCH. A name that merely starts with the marker is not one of
  // ours — otherwise a production database called `SSAS_Verify_Payroll` would fall inside a namespace that
  // authorises deletion.
  [Theory]
  [InlineData("SSAS_Verify_Payroll")]
  [InlineData("SSAS_Verify_42")]
  [InlineData("SSAS_Verify_42_")]
  [InlineData("SSAS_Verify_42_x")]
  [InlineData("SSAS_Verify__7")]
  [InlineData("SSAS_Verify_")]
  [InlineData("ssas_verify_42_7")]
  [InlineData("Prefix_SSAS_Verify_42_7")]
  [InlineData("TenantProduction")]
  [InlineData("")]
  [InlineData(null)]
  public void A_name_outside_the_reserved_vocabulary_is_not_recognised(string? candidate)
  {
    Assert.False(TenantDatabaseVerificationNaming.IsVerificationDatabaseName(candidate));
  }

  // Non-ASCII digits must not slip in: they would let a visually similar name into a namespace that
  // authorises deletion.
  [Fact]
  public void Unicode_digits_are_not_accepted_as_identities()
  {
    Assert.False(TenantDatabaseVerificationNaming.IsVerificationDatabaseName("SSAS_Verify_٤٢_٧"));
  }

  [Fact]
  public void A_name_matches_only_the_run_that_generated_it()
  {
    var name = TenantDatabaseVerificationNaming.ForRun(42, 7);

    Assert.True(TenantDatabaseVerificationNaming.MatchesRun(name, 42, 7));
    Assert.False(TenantDatabaseVerificationNaming.MatchesRun(name, 42, 8));
    Assert.False(TenantDatabaseVerificationNaming.MatchesRun(name, 43, 7));
  }

  // ---- Restore-target admission.

  [Fact]
  public void A_generated_name_may_be_restored_into_when_it_collides_with_nothing()
  {
    Assert.True(TenantDatabaseVerificationTargetGuard.CanRestoreInto(
      TenantDatabaseVerificationNaming.ForRun(42, 7), 42, 7, ["TenantProduction", "TenantShared"]));
  }

  // A REGISTERED AUTHORITATIVE DATABASE IS NEVER A RESTORE TARGET, however well-formed the name looks. The
  // comparison is case-insensitive because SQL Server names commonly are, and when the question is "might
  // this be production?" the answer must err toward yes.
  [Theory]
  [InlineData("SSAS_Verify_42_7")]
  [InlineData("ssas_verify_42_7")]
  [InlineData("  SSAS_Verify_42_7  ")]
  public void A_name_registered_as_a_tenant_database_is_refused_as_a_restore_target(string registered)
  {
    Assert.False(TenantDatabaseVerificationTargetGuard.CanRestoreInto(
      TenantDatabaseVerificationNaming.ForRun(42, 7), 42, 7, [registered]));
  }

  [Fact]
  public void A_name_from_another_run_is_refused_as_a_restore_target()
  {
    Assert.False(TenantDatabaseVerificationTargetGuard.CanRestoreInto(
      TenantDatabaseVerificationNaming.ForRun(42, 8), 42, 7, []));
  }

  [Fact]
  public void An_arbitrary_name_is_refused_as_a_restore_target()
  {
    Assert.False(TenantDatabaseVerificationTargetGuard.CanRestoreInto("TenantProduction", 42, 7, []));
  }

  // ---- Automated cleanup eligibility: a CONJUNCTION, never a pattern match (ADR-022 compliance rule 24).

  [Fact]
  public void A_correlated_aged_orphan_with_no_registration_is_eligible_for_cleanup()
  {
    Assert.True(TenantDatabaseVerificationTargetGuard.IsEligibleForAutomatedCleanup(Orphan()));
  }

  // Each of these falsifies exactly ONE condition, so a guard that ever drops a condition fails here rather
  // than in production.
  [Fact]
  public void A_name_outside_the_reserved_vocabulary_is_never_cleaned_up()
  {
    Assert.False(TenantDatabaseVerificationTargetGuard.IsEligibleForAutomatedCleanup(
      Orphan() with { DatabaseName = "TenantProduction" }));
  }

  [Fact]
  public void A_database_with_no_matching_verification_record_is_never_cleaned_up()
  {
    Assert.False(TenantDatabaseVerificationTargetGuard.IsEligibleForAutomatedCleanup(
      Orphan() with { HasMatchingVerificationRecord = false }));
  }

  [Fact]
  public void A_database_whose_record_names_a_different_database_is_never_cleaned_up()
  {
    Assert.False(TenantDatabaseVerificationTargetGuard.IsEligibleForAutomatedCleanup(
      Orphan() with { RecordedDatabaseName = "SSAS_Verify_42_8" }));
  }

  // A long legitimate restore must not be mistaken for an orphan — destroying work in progress is the
  // failure mode here.
  [Fact]
  public void A_database_targeted_by_an_active_verification_is_never_cleaned_up()
  {
    Assert.False(TenantDatabaseVerificationTargetGuard.IsEligibleForAutomatedCleanup(
      Orphan() with { IsTargetOfActiveVerification = true }));
  }

  [Fact]
  public void A_registered_tenant_database_is_never_cleaned_up()
  {
    Assert.False(TenantDatabaseVerificationTargetGuard.IsEligibleForAutomatedCleanup(
      Orphan() with { IsRegisteredTenantDatabase = true }));
  }

  [Fact]
  public void A_database_carrying_a_tenant_assignment_is_never_cleaned_up()
  {
    Assert.False(TenantDatabaseVerificationTargetGuard.IsEligibleForAutomatedCleanup(
      Orphan() with { HasTenantAssignment = true }));
  }

  [Fact]
  public void A_database_inside_the_grace_period_is_never_cleaned_up()
  {
    Assert.False(TenantDatabaseVerificationTargetGuard.IsEligibleForAutomatedCleanup(
      Orphan() with { Age = TimeSpan.FromMinutes(5) }));
  }

  private static TenantDatabaseVerificationCleanupCandidate Orphan() =>
    new(
      DatabaseName: "SSAS_Verify_42_7",
      HasMatchingVerificationRecord: true,
      RecordedDatabaseName: "SSAS_Verify_42_7",
      IsTargetOfActiveVerification: false,
      IsRegisteredTenantDatabase: false,
      HasTenantAssignment: false,
      Age: TimeSpan.FromHours(12),
      GracePeriod: TimeSpan.FromHours(6));
}
