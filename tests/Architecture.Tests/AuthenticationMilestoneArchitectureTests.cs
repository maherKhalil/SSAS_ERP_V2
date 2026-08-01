using System.Reflection;
using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain.Authentication;

namespace SSAS.Architecture.Tests;

public sealed class AuthenticationMilestoneArchitectureTests
{
  [Fact]
  [Trait("NonFunctional", "NFR-AUTH-0302")]
  [Trait("Scenario", "TS-AUTH-0070")]
  public void Authentication_domain_and_application_have_no_persistence_http_or_crypto_framework_dependency()
  {
    var forbiddenPrefixes = new[]
    {
      "Microsoft.EntityFrameworkCore",
      "Microsoft.Data.SqlClient",
      "Microsoft.AspNetCore",
      "System.Security.Cryptography"
    };
    var assemblies = new[] { typeof(AuthenticationAccount).Assembly, typeof(AuthenticationPolicy).Assembly };

    var violations = assemblies
      .SelectMany(assembly => assembly.GetReferencedAssemblies()
        .Where(reference => forbiddenPrefixes.Any(prefix => reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true))
        .Select(reference => $"{assembly.GetName().Name} -> {reference.Name}"))
      .ToArray();

    Assert.Empty(violations);
  }

  [Fact]
  [Trait("NonFunctional", "NFR-AUTH-0301")]
  [Trait("Scenario", "TS-AUTH-0070")]
  public void Authentication_handlers_expose_async_cancellation_boundaries()
  {
    var handlers = new[]
    {
      typeof(IssueTenantUserInvitationCommandHandler),
      typeof(CompleteInvitationCommandHandler),
      typeof(VerifyPasswordCredentialsCommandHandler),
      typeof(IssuePasswordResetCommandHandler),
      typeof(CompletePasswordResetCommandHandler)
    };

    Assert.All(handlers, handler =>
    {
      var method = Assert.Single(handler.GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Where(candidate => candidate.Name == "HandleAsync"));
      Assert.True(typeof(Task).IsAssignableFrom(method.ReturnType));
      Assert.Contains(method.GetParameters(), parameter => parameter.ParameterType == typeof(CancellationToken));
    });
  }

  [Fact]
  [Trait("NonFunctional", "NFR-AUTH-0307")]
  [Trait("BusinessRequirement", "BR-AUTH-0009")]
  [Trait("Scenario", "TS-AUTH-0054")]
  [Trait("Scenario", "TS-AUTH-0073")]
  public void Authentication_domain_events_expose_no_password_secret_or_hash_material()
  {
    var violations = typeof(AuthenticationAccount).Assembly.GetTypes()
      .Where(type => typeof(DomainEvent).IsAssignableFrom(type) && type.Namespace == "SSAS.Platform.Domain.Events")
      .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => Regex.IsMatch(property.Name, "Password|Secret|Hash|Raw", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        .Select(property => $"{type.Name}.{property.Name}"))
      .ToArray();

    Assert.Empty(violations);
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0030")]
  [Trait("Scenario", "TS-AUTH-0056")]
  public void Raw_action_token_values_cannot_cross_an_ordinary_string_dto_property()
  {
    var sensitiveOutputTypes = new[]
    {
      typeof(GeneratedActionToken),
      typeof(PasswordResetIssuanceResult),
      typeof(SensitiveActionToken)
    };
    var violations = sensitiveOutputTypes
      .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => Regex.IsMatch(
            property.Name,
            "Raw|Secret|Token|Hash",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
          property.PropertyType != typeof(SensitiveActionToken))
        .Select(property => $"{type.Name}.{property.Name}"))
      .ToArray();

    Assert.Empty(violations);
    Assert.Equal(typeof(SensitiveActionToken), typeof(GeneratedActionToken).GetProperty("SensitiveToken")?.PropertyType);
  }

  [Fact]
  [Trait("Scenario", "TS-AUTH-0005")]
  [Trait("Scenario", "TS-AUTH-0006")]
  public void Invitation_input_exposes_neither_identity_subject_nor_role_assignment()
  {
    var names = typeof(IssueTenantUserInvitationCommand).GetProperties().Select(property => property.Name).ToArray();

    Assert.DoesNotContain(names, name => name.Contains("Subject", StringComparison.OrdinalIgnoreCase));
    Assert.DoesNotContain(names, name => name.Contains("Role", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  [Trait("Scenario", "TS-AUTH-0074")]
  public void Milestone_two_contains_no_deferred_authentication_implementation_or_http_endpoint()
  {
    var platformFiles = Directory
      .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src", "Platform"), "*.cs", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .ToArray();
    const string deferredDeclaration =
      @"\b(?:class|record|interface)\s+(?:\w)*(?:AuthenticationSession|RefreshToken|TenantSelection|JwtIssuer|AccessTokenIssuer|Logout|SigningKey|Csrf|Cookie)(?:\w)*\b";
    var deferred = platformFiles
      .Where(path => Regex.IsMatch(File.ReadAllText(path), deferredDeclaration, RegexOptions.CultureInvariant))
      .ToArray();
    var endpointExposure = platformFiles
      .Where(path => path.Contains($"{Path.DirectorySeparatorChar}SSAS.Platform.API{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Where(path => File.ReadAllText(path).Contains("SSAS.Platform.Application.Authentication", StringComparison.Ordinal))
      .ToArray();

    Assert.Empty(deferred);
    Assert.Empty(endpointExposure);
  }

  [Fact]
  [Trait("Requirement", "SEC-AUTH-0209")]
  [Trait("Scenario", "TS-AUTH-0073")]
  public void Production_configuration_contains_no_credentials_or_signing_keys()
  {
    var configuration = File.ReadAllText(Path.Combine(
      FindRepositoryRoot(),
      "src",
      "Host",
      "SSAS.Host.API",
      "appsettings.json"));

    Assert.DoesNotContain("SigningKey", configuration, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotMatch("(Password|User ID|ApiKey)\\s*=", configuration);
    Assert.DoesNotContain("PRIVATE KEY", configuration, StringComparison.Ordinal);
  }

  private static string FindRepositoryRoot()
  {
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln"))) return directory.FullName;
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}
