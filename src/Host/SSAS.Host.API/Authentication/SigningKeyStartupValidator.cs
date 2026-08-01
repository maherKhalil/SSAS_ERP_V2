namespace SSAS.Host.API.Authentication;

public sealed class SigningKeyStartupValidator(ISigningKeyProvider signingKeyProvider) : IHostedService
{
  public Task StartAsync(CancellationToken cancellationToken)
  {
    _ = signingKeyProvider.Snapshot;
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
