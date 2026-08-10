namespace SSAS.Platform.Application.PlatformSupport;

// Genesis/recovery bootstrap orchestration (ADR-016 / DEC-TEN-0019). Establishes at most one usable
// genesis/recovery principal per convergence when no usable authority exists. It is NOT request
// authorization and never re-enables a Disabled principal.
public interface IPlatformSupportBootstrapService
{
  Task<PlatformSupportBootstrapOutcome> RunAsync(CancellationToken cancellationToken = default);
}
