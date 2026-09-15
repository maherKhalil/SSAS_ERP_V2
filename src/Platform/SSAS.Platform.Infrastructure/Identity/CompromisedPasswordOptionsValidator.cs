using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SSAS.Platform.Infrastructure.Identity;

public sealed class CompromisedPasswordOptionsValidator(IHostEnvironment environment)
  : IValidateOptions<CompromisedPasswordOptions>
{
  public ValidateOptionsResult Validate(string? name, CompromisedPasswordOptions options)
  {
    if (!options.Enabled)
    {
      return environment.IsProduction()
        ? ValidateOptionsResult.Fail("Compromised-password checking cannot be disabled in Production.")
        : ValidateOptionsResult.Success;
    }

    if (string.IsNullOrWhiteSpace(options.DatasetPath) || string.IsNullOrWhiteSpace(options.DatasetVersion) ||
      string.IsNullOrWhiteSpace(options.LicenseName) || !IsValidLicenseUrl(options.LicenseUrl))
    {
      return ValidateOptionsResult.Fail("An enabled compromised-password dataset requires path, version, and license metadata.");
    }

    try
    {
      var path = ResolvePath(environment.ContentRootPath, options.DatasetPath);
      if (!File.Exists(path))
      {
        return ValidateOptionsResult.Fail("The configured compromised-password dataset does not exist.");
      }

      var count = OfflineCompromisedPasswordDataset.Validate(path);
      return count > 0
        ? ValidateOptionsResult.Success
        : ValidateOptionsResult.Fail("The compromised-password dataset contains no hashes.");
    }
    // ==============================================================================================
    // ⚠ THIS LOOKS LIKE FOUR SWALLOWED EXCEPTIONS AND IS THE OPPOSITE (T-241).
    // ==============================================================================================
    //
    // **Every arm below returns `ValidateOptionsResult.Fail`, so an unreadable dataset REFUSES STARTUP.**
    // Nothing is degraded and compromised-password checking is never silently disabled -- which is what a
    // reader scanning for discarded exceptions would reasonably assume was happening here.
    //
    // **The messages are generic on purpose: the exception text would carry the dataset PATH**, and an
    // options-validation failure is surfaced at startup and copied into tickets and logs. `InvalidData` is
    // the one exception whose own message is safe to pass through, because it describes the file's
    // CONTENTS rather than its location.
    //
    // So the exception object is dropped and the fact that it occurred is not. That is the distinction
    // worth writing down: the information lost is a path, and the information kept is the refusal.
    catch (InvalidDataException exception)
    {
      return ValidateOptionsResult.Fail(exception.Message);
    }
    catch (IOException)
    {
      return ValidateOptionsResult.Fail("The compromised-password dataset could not be read.");
    }
    catch (UnauthorizedAccessException)
    {
      return ValidateOptionsResult.Fail("The compromised-password dataset could not be read.");
    }
    catch (ArgumentException)
    {
      return ValidateOptionsResult.Fail("The compromised-password dataset path is invalid.");
    }
    catch (NotSupportedException)
    {
      return ValidateOptionsResult.Fail("The compromised-password dataset path is invalid.");
    }
  }

  internal static string ResolvePath(string contentRootPath, string datasetPath) =>
    Path.IsPathRooted(datasetPath) ? datasetPath : Path.GetFullPath(Path.Combine(contentRootPath, datasetPath));

  private static bool IsValidLicenseUrl(string? value) =>
    Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
    (StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, Uri.UriSchemeHttps) ||
      StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, Uri.UriSchemeHttp)) &&
    string.IsNullOrEmpty(uri.UserInfo);
}
