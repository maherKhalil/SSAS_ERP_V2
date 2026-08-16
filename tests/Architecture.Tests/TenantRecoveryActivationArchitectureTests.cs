using System.Reflection;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Architecture.Tests;

// THE BOUNDARIES OF THE RECOVERY ACTIVATION GATE (TS-Storage Phase E).
//
// The gate sits between validation and the routing flip in a cutover, which makes it exactly the kind of
// component that accretes authority: it is already loading evidence and already being consulted at the
// decisive moment, so wiring the flip itself into it would look convenient. These tests keep it a decision.
public sealed class TenantRecoveryActivationArchitectureTests
{
  private static readonly Assembly ApplicationAssembly =
    typeof(TenantDatabaseRecoveryActivationGate).Assembly;

  // The decision is PURE and lives in the domain, so an activation verdict cannot vary by which
  // orchestration asked for it or how a query happened to be written.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_activation_decision_is_a_pure_domain_rule()
  {
    var decision = typeof(TenantDatabaseRecoveryActivation);

    Assert.True(decision.IsAbstract && decision.IsSealed, "the activation decision must be a static class");
    Assert.Equal("SSAS.Platform.Domain.TenantStorage", decision.Namespace);

    var methods = decision.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
    var decide = Assert.Single(methods);
    Assert.Equal(nameof(TenantDatabaseRecoveryActivation.Decide), decide.Name);

    // The clock is a parameter, never ambient: the whole matrix must be decidable without a clock service.
    Assert.Contains(decide.GetParameters(), parameter => parameter.ParameterType == typeof(DateTimeOffset));
  }

  // THE GATE GRANTS NO ROUTING AUTHORITY. It cannot flip an assignment, mint a RoutingVersion, or touch the
  // registry — which is what keeps "authorise the activation" from quietly becoming "perform it".
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_activation_gate_cannot_flip_routing_or_write_registry_state()
  {
    var forbidden = new[]
    {
      typeof(TenantDatabaseAssignment), typeof(TenantDatabase), typeof(TenantDatabaseBackupRun),
      typeof(TenantDatabaseRestoreVerificationRun)
    };

    var reachable = typeof(TenantDatabaseRecoveryActivationGate)
      .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
      .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
        .Append(method.ReturnType))
      .Concat(typeof(TenantDatabaseRecoveryActivationGate)
        .GetConstructors()
        .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType)))
      .ToArray();

    foreach (var aggregate in forbidden)
    {
      Assert.DoesNotContain(aggregate, reachable);
    }
  }

  // READ-ONLY EVIDENCE. A boundary consulted immediately before a routing flip must not be able to change
  // the facts it is reporting on.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_activation_evidence_boundary_exposes_only_reads()
  {
    var writeVerbs = new[] { "Save", "Update", "Add", "Delete", "Remove", "Record", "Write", "Set", "Begin" };

    var methods = typeof(SSAS.Platform.Application.Abstractions.Persistence
        .ITenantDatabaseRecoveryActivationReadRepository)
      .GetMethods()
      .Select(method => method.Name)
      .ToArray();

    Assert.All(methods, name => Assert.DoesNotContain(
      writeVerbs, verb => name.StartsWith(verb, StringComparison.Ordinal)));
    Assert.All(methods, name => Assert.StartsWith("Find", name, StringComparison.Ordinal));
  }

  // PROTECTED IS NOT REDEFINED. Phase E adds a second requirement; it does not change what Phase D decided
  // `Protected` means, because that would silently re-grade the whole fleet.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Protected_still_means_what_phase_d_decided_it_means()
  {
    // A policy with no verification interval still reaches Protected on backup evidence alone...
    var noVerificationObligation = Inputs(restoreVerificationIntervalDays: null);

    Assert.Equal(
      TenantDatabaseRecoveryReadinessStatus.Protected,
      TenantDatabaseRecoveryReadinessEvaluator.Evaluate(noVerificationObligation, Now));

    // ...and activation still refuses it, because the two requirements are independent.
    Assert.Equal(
      TenantDatabaseRecoveryActivationDecision.RefusedNeverRestoreVerified,
      TenantDatabaseRecoveryActivation.Decide(
        new TenantDatabaseRecoveryActivationInputs(
          noVerificationObligation with
          {
            HeldRecoveryReadinessStatus = TenantDatabaseRecoveryReadinessStatus.Protected
          },
          CurrentBaselineBackupRunId: 100,
          VerifiedVerificationRunId: null,
          VerifiedSourceBackupRunId: null,
          VerifiedDepth: null,
          VerificationCompletedUtc: null),
        Now));
  }

  private static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

  private static TenantDatabaseRecoveryReadinessInputs Inputs(int? restoreVerificationIntervalDays) =>
    new(
      TenantDatabaseHostingMode.PlatformManaged,
      PolicyExists: true,
      PolicyEnabled: true,
      TenantDatabaseBackupManagementMode.AutomaticByPlatform,
      FullBackupIntervalMinutes: 1_440,
      DifferentialBackupIntervalMinutes: null,
      TransactionLogBackupIntervalMinutes: null,
      restoreVerificationIntervalDays,
      MaximumBackupAgeMinutes: 2_880,
      LastSuccessfulFullBackupUtc: Now.AddHours(-1),
      LastSuccessfulDifferentialBackupUtc: null,
      LastSuccessfulLogBackupUtc: null,
      LastRestoreVerificationUtc: null);
}
