using System.Security.Cryptography;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Identity;

namespace SSAS.Platform.Tests.Authentication;

public sealed class AuthenticationSecurityTests
{
  private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("BusinessRule", "BRULE-AUTH-0008")]
  [Trait("Decision", "DEC-AUTH-0029")]
  [Trait("Requirement", "SEC-AUTH-0202")]
  [Trait("Requirement", "SEC-AUTH-0207")]
  [Trait("Acceptance", "AC-AUTH-0009")]
  [Trait("Scenario", "TS-AUTH-0062")]
  public void Action_tokens_use_public_selector_random_secret_exact_hash_and_fixed_purpose_binding()
  {
    var service = new ActionTokenService();
    var generated = service.Generate(AccountActionTokenPurpose.Invitation);
    var secretHash = ReadSecretHash(generated);
    var raw = generated.SensitiveToken.RevealOnce().Value;
    var token = AccountActionToken.CreateInvitation(
      generated.PublicId,
      secretHash,
      1,
      1,
      Guid.NewGuid(),
      1,
      Now,
      Now.AddHours(24),
      Guid.NewGuid());

    Assert.Equal(32, secretHash.Length);
    Assert.Matches("^[0-9a-f]{32}\\.[A-Za-z0-9_-]{43}$", raw);
    Assert.True(service.TryReadPublicId(raw, out var selector));
    Assert.Equal(generated.PublicId, selector);
    Assert.True(service.Verify(token, raw));
    Assert.False(service.Verify(token, raw[..^1] + (raw[^1] == 'A' ? "B" : "A")));

    var reset = AccountActionToken.CreatePasswordReset(
      generated.PublicId,
      secretHash,
      1,
      1,
      Now,
      Now.AddMinutes(30),
      Guid.NewGuid());
    Assert.False(service.Verify(reset, raw));
  }

  [Theory]
  [InlineData("")]
  [InlineData("not-a-token")]
  [InlineData("00000000000000000000000000000000.short")]
  [InlineData("00000000000000000000000000000000.invalid.secret")]
  [Trait("Requirement", "SEC-AUTH-0205")]
  [Trait("Scenario", "TS-AUTH-0018")]
  public void Malformed_action_tokens_are_rejected_without_throwing(string value)
  {
    var service = new ActionTokenService();

    Assert.False(service.TryReadPublicId(value, out _));
  }

  [Fact]
  [Trait("Requirement", "SEC-AUTH-0205")]
  [Trait("Scenario", "TS-AUTH-0018")]
  public void Oversized_action_token_is_rejected_before_base64_decoding()
  {
    var oversized = $"{Guid.NewGuid():N}.{new string('A', 1_000_000)}";

    Assert.False(new ActionTokenService().TryReadPublicId(oversized, out _));
  }

  [Fact]
  [Trait("Requirement", "SEC-AUTH-0201")]
  [Trait("Scenario", "TS-AUTH-0011")]
  public void Aspnet_hasher_preserves_three_state_verification_and_requests_upgrade()
  {
    var lowCost = new AspNetPasswordHashingService(new PasswordHasher<object>(Options.Create(new PasswordHasherOptions
    {
      IterationCount = 10_000
    })));
    var productionCost = new AspNetPasswordHashingService(new PasswordHasher<object>(Options.Create(new PasswordHasherOptions
    {
      IterationCount = 100_000
    })));
    var hash = lowCost.HashPassword("A sufficiently long password 42!");

    Assert.Equal(
      PasswordVerificationOutcome.SuccessRehashNeeded,
      productionCost.VerifyPassword(hash, "A sufficiently long password 42!"));
    Assert.Equal(PasswordVerificationOutcome.Failed, productionCost.VerifyPassword(hash, "wrong"));
    Assert.Equal(
      PasswordVerificationOutcome.Success,
      productionCost.VerifyPassword(
        productionCost.HashPassword("A sufficiently long password 42!"),
        "A sufficiently long password 42!"));
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0028")]
  [Trait("Requirement", "SEC-AUTH-0210")]
  [Trait("Scenario", "TS-AUTH-0015")]
  public async Task Offline_checker_uses_hash_only_dataset_and_detects_compromised_passwords()
  {
    var directory = Directory.CreateTempSubdirectory("ssas-password-dataset-");
    try
    {
      const string compromised = "known compromised password";
      var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(compromised)));
      var datasetPath = Path.Combine(directory.FullName, "hashes.txt");
      await File.WriteAllTextAsync(datasetPath, hash + Environment.NewLine);
      var options = Options.Create(new CompromisedPasswordOptions
      {
        Enabled = true,
        DatasetPath = datasetPath,
        DatasetVersion = "test-v1",
        LicenseName = "test-license",
        LicenseUrl = "https://example.test/license"
      });
      var checker = new OfflineCompromisedPasswordChecker(options, new TestHostEnvironment("Production", directory.FullName));

      Assert.Equal(CompromisedPasswordCheckOutcome.Compromised, await checker.CheckAsync(compromised));
      Assert.Equal(CompromisedPasswordCheckOutcome.Safe, await checker.CheckAsync("different long password"));
    }
    finally
    {
      directory.Delete(true);
    }
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0028")]
  [Trait("Requirement", "SEC-AUTH-0210")]
  [Trait("Scenario", "TS-AUTH-0015")]
  public void Production_cannot_disable_or_start_without_valid_compromised_password_dataset_metadata()
  {
    var validator = new CompromisedPasswordOptionsValidator(new TestHostEnvironment("Production", Directory.GetCurrentDirectory()));

    Assert.True(validator.Validate(null, new CompromisedPasswordOptions { Enabled = false }).Failed);
    Assert.True(validator.Validate(null, new CompromisedPasswordOptions { Enabled = true }).Failed);
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0028")]
  [Trait("Requirement", "SEC-AUTH-0210")]
  public async Task Dataset_validation_rejects_invalid_license_url_format_and_unbounded_file()
  {
    var directory = Directory.CreateTempSubdirectory("ssas-password-validation-");
    try
    {
      var validPath = Path.Combine(directory.FullName, "valid.txt");
      await File.WriteAllTextAsync(validPath, new string('A', 64));
      var validator = new CompromisedPasswordOptionsValidator(new TestHostEnvironment("Production", directory.FullName));
      var invalidLicense = CreateDatasetOptions(validPath);
      invalidLicense.LicenseUrl = "not-an-absolute-license-url";
      Assert.True(validator.Validate(null, invalidLicense).Failed);
      invalidLicense.LicenseUrl = "https://dataset-user:dataset-secret@example.test/license";
      Assert.True(validator.Validate(null, invalidLicense).Failed);

      var invalidPath = CreateDatasetOptions("invalid\0path");
      Assert.True(validator.Validate(null, invalidPath).Failed);

      var invalidFormatPath = Path.Combine(directory.FullName, "invalid.txt");
      await File.WriteAllTextAsync(invalidFormatPath, "not-a-sha256-hash");
      Assert.True(validator.Validate(null, CreateDatasetOptions(invalidFormatPath)).Failed);

      var oversizedPath = Path.Combine(directory.FullName, "oversized.txt");
      await using (var stream = new FileStream(oversizedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
      {
        stream.SetLength((64L * 1024 * 1024) + 1);
      }
      Assert.True(validator.Validate(null, CreateDatasetOptions(oversizedPath)).Failed);
    }
    finally
    {
      directory.Delete(true);
    }
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0028")]
  [Trait("Requirement", "SEC-AUTH-0210")]
  public async Task Dataset_validation_fails_when_the_configured_file_cannot_be_read()
  {
    var directory = Directory.CreateTempSubdirectory("ssas-password-unreadable-");
    try
    {
      var path = Path.Combine(directory.FullName, "locked.txt");
      await File.WriteAllTextAsync(path, new string('A', 64));
      await using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
      var validator = new CompromisedPasswordOptionsValidator(new TestHostEnvironment("Production", directory.FullName));

      Assert.True(validator.Validate(null, CreateDatasetOptions(path)).Failed);
    }
    finally
    {
      directory.Delete(true);
    }
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0030")]
  [Trait("Requirement", "SEC-AUTH-0209")]
  [Trait("Requirement", "SEC-AUTH-0203")]
  [Trait("Acceptance", "AC-AUTH-0020")]
  [Trait("Scenario", "TS-AUTH-0056")]
  public void Sensitive_result_is_one_time_redacted_and_does_not_serialize_raw_token_value()
  {
    const string raw = "0123456789abcdef0123456789abcdef.secret-material";
    var sensitive = new SensitiveActionToken(raw);

    Assert.DoesNotContain(raw, sensitive.ToString(), StringComparison.Ordinal);
    Assert.DoesNotContain(raw, JsonSerializer.Serialize(sensitive), StringComparison.Ordinal);
    Assert.Equal(raw, sensitive.RevealOnce().Value);
    Assert.True(sensitive.RevealOnce().IsFailure);
  }

  [Fact]
  [Trait("Requirement", "SEC-AUTH-0203")]
  [Trait("Scenario", "TS-AUTH-0054")]
  public void Authentication_commands_redact_login_password_and_action_token_values()
  {
    const string email = "sensitive.user@example.com";
    const string password = "Sensitive password 123";
    const string token = "0123456789abcdef0123456789abcdef.sensitive-token-secret";
    object[] commands =
    [
      new VerifyPasswordCredentialsCommand(email, password),
      new CompleteInvitationCommand(token, password),
      new CompletePasswordResetCommand(token, password),
      new IssuePasswordResetCommand(email),
      new IssueTenantUserInvitationCommand(email, "Sensitive Name")
    ];

    Assert.All(commands, command =>
    {
      var representation = command.ToString();
      Assert.DoesNotContain(email, representation, StringComparison.Ordinal);
      Assert.DoesNotContain(password, representation, StringComparison.Ordinal);
      Assert.DoesNotContain(token, representation, StringComparison.Ordinal);
      var debuggerDisplay = command.GetType().GetCustomAttribute<DebuggerDisplayAttribute>();
      Assert.NotNull(debuggerDisplay);
      Assert.DoesNotContain(email, debuggerDisplay.Value, StringComparison.Ordinal);
      Assert.DoesNotContain(password, debuggerDisplay.Value, StringComparison.Ordinal);
      Assert.DoesNotContain(token, debuggerDisplay.Value, StringComparison.Ordinal);
    });
  }

  private sealed class TestHostEnvironment(string environmentName, string contentRootPath) : IHostEnvironment
  {
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "SSAS.Platform.Tests";
    public string ContentRootPath { get; set; } = contentRootPath;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
  }

  private static byte[] ReadSecretHash(GeneratedActionToken generated)
  {
    var field = typeof(GeneratedActionToken).GetField("secretHash", BindingFlags.Instance | BindingFlags.NonPublic);
    return Assert.IsType<byte[]>(field?.GetValue(generated));
  }

  private static CompromisedPasswordOptions CreateDatasetOptions(string path) => new()
  {
    Enabled = true,
    DatasetPath = path,
    DatasetVersion = "test-v1",
    LicenseName = "test-license",
    LicenseUrl = "https://example.test/license"
  };
}
