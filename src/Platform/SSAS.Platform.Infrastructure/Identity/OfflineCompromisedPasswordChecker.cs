using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SSAS.Platform.Application.Authentication;

namespace SSAS.Platform.Infrastructure.Identity;

public sealed class OfflineCompromisedPasswordChecker : ICompromisedPasswordChecker
{
  private readonly HashSet<string>? hashes;

  public OfflineCompromisedPasswordChecker(
    IOptions<CompromisedPasswordOptions> options,
    IHostEnvironment environment)
  {
    if (!options.Value.Enabled)
    {
      return;
    }

    var path = CompromisedPasswordOptionsValidator.ResolvePath(
      environment.ContentRootPath,
      options.Value.DatasetPath!);
    hashes = OfflineCompromisedPasswordDataset.Load(path);
    if (hashes.Count == 0)
    {
      throw new InvalidOperationException("The compromised-password dataset contains no hashes.");
    }
  }

  public Task<CompromisedPasswordCheckOutcome> CheckAsync(
    string password,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    if (hashes is null)
    {
      return Task.FromResult(CompromisedPasswordCheckOutcome.Safe);
    }

    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    return Task.FromResult(hashes.Contains(hash)
      ? CompromisedPasswordCheckOutcome.Compromised
      : CompromisedPasswordCheckOutcome.Safe);
  }
}
