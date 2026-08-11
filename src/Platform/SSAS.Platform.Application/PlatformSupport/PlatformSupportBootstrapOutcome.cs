namespace SSAS.Platform.Application.PlatformSupport;

// Outcome of one bootstrap convergence evaluation (ADR-016 / DEC-TEN-0019).
public enum PlatformSupportBootstrapOutcome
{
  // No bootstrap subjects configured; bootstrap performs no persistence access.
  NoCandidatesConfigured,

  // Usable platform authority already exists; bootstrap is inert.
  AuthorityAlreadyUsable,

  // Exactly one new genesis/recovery principal was established and granted its initial authority.
  GenesisEstablished,

  // No usable authority exists and no eligible configured subject could establish it (fail closed).
  NoEligibleCandidate
}
