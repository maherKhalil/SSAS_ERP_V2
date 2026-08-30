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
      typeof(CompletePasswordResetCommandHandler),
      typeof(BeginTenantAccessCommandHandler),
      typeof(SelectTenantCommandHandler),
      typeof(RefreshAuthenticationSessionCommandHandler)
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
    // ⚠ THE SET IS DISCOVERED BY NAMESPACE, SO IT CAN SILENTLY BECOME EMPTY (T-248).
    //
    // The two checks above this one enumerate FIXED type arrays, so the compiler guarantees they are not
    // empty. **This one filters on a namespace string.** Rename `SSAS.Platform.Domain.Events` and the
    // filter matches nothing, no property is inspected, and "no event exposes secret material" passes by
    // examining no events at all — the same shape that let `PersistenceArchitectureTests` pass nine tests
    // over an empty file walk.
    var events = typeof(AuthenticationAccount).Assembly.GetTypes()
      .Where(type => typeof(DomainEvent).IsAssignableFrom(type) && type.Namespace == "SSAS.Platform.Domain.Events")
      .ToArray();

    Assert.True(events.Length >= 5,
      $"only {events.Length} authentication domain events were discovered, so the namespace filter has " +
      "stopped matching and the assertion below would pass by inspecting nothing.");

    // ⚠ THE CONTROL ON THE MATCHER (T-263). The floor above proves events were FOUND. It cannot prove the
    // property walk or the regex still LOOK for anything -- and for a ban those two failures are
    // indistinguishable from success. Both are exercised here against inputs they must match.
    const string SecretName = "Password|Secret|Hash|Raw";

    Assert.Matches(SecretName, "PasswordHash");
    Assert.Matches(SecretName, "RawToken");
    Assert.DoesNotMatch(SecretName, "OccurredUtc");

    var inspected = events
      .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
      .ToArray();

    Assert.True(inspected.Length >= 10,
      $"the {events.Length} events yielded only {inspected.Length} public instance properties, so the " +
      "member walk -- not the event walk -- is what has collapsed, and the ban below reads nothing.");

    var violations = events
      .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => Regex.IsMatch(property.Name, SecretName, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
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

  // ---- PLANT RECORD (T-248), kept here rather than only in the commit message.
  //
  // An audit found this file had no anti-vacuity protection at all. Two controls were added, and each was
  // observed to fail before it was trusted:
  //
  //   * namespace changed to `SSAS.Platform.Domain.EventsX` — the domain-events floor reddens at 0.
  //   * the `Migrations` exclusion widened to exclude every path — the file-scan floor reddens at 0.
  //
  // **The other checks in this file needed nothing, and that is as much the finding as the two that did.**
  // They enumerate FIXED type arrays — `typeof(AuthenticationAccount).Assembly`, an explicit list of
  // sensitive output types — which the compiler keeps non-empty. A set the compiler guarantees cannot
  // collapse silently; a set discovered by namespace or by file pattern can.
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
  public void Milestone_four_keeps_token_framework_types_out_of_domain_and_application()
  {
    var platformFiles = Directory
      .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src", "Platform", "SSAS.Platform.Domain"), "*.cs", SearchOption.AllDirectories)
      .Concat(Directory.EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src", "Platform", "SSAS.Platform.Application"), "*.cs", SearchOption.AllDirectories))
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .ToArray();
    // ⚠ THE ROOT CANNOT VANISH SILENTLY, BUT THE FILTER CAN — AND ONLY THE SECOND NEEDS GUARDING.
    //
    // `Directory.EnumerateFiles` on a directory that does not exist THROWS, so renaming either project is
    // caught by an exception rather than by an empty result. **That is not true of the filters.** Change
    // the search pattern, or widen the `Migrations` exclusion, and the walk returns an empty array from
    // directories that exist — which reads exactly like "no violations".
    //
    // So the floor sits on the POST-FILTER count, which is the only quantity that can collapse quietly.
    Assert.True(platformFiles.Length >= 50,
      $"only {platformFiles.Length} Platform Domain/Application files were scanned; the filters have " +
      "stopped matching and 'no deferred types' below would mean nothing.");

    const string deferredDeclaration =
      // ⚠ NO WORD ANCHORS, AND THE CONTROL BELOW IS WHY (T-263). This read `\b(?:...)\b`, and the
      // first known-positive assertion written against it FAILED: `\b` after `JwtSecurityToken` cannot
      // match `JwtSecurityTokenHandler`, because the next character is a word character. **The canonical
      // JWT type of this family was not banned by the ban.** The leading `\b` lost `IHttpContextAccessor`
      // the same way from the other side. Both are precisely what this rule exists to keep out of Domain
      // and Application, and both satisfied it.
      //
      // These names are distinctive enough that an unanchored search has no plausible false positive, and
      // a ban should catch a DERIVED name as readily as the bare one.
      @"(?:JwtSecurityToken|JsonWebToken|X509Certificate2|SymmetricSecurityKey|CookieOptions|HttpContext)";
    // ⚠ THE CONTROL ON THE MATCHER (T-263). Rename any type in that alternation and the regex matches
    // nothing, `deferred` is empty and this ban goes green having read fifty files and looked for nothing.
    // The floor above stays satisfied throughout it. So the pattern is made to prove it still matches.
    Assert.Matches(deferredDeclaration, "var handler = new JwtSecurityTokenHandler();");
    Assert.Matches(deferredDeclaration, "IHttpContextAccessor accessor");
    Assert.Matches(deferredDeclaration, "X509Certificate2 certificate");
    Assert.DoesNotMatch(deferredDeclaration, "var account = new AuthenticationAccount();");

    var deferred = platformFiles
      .Where(path => Regex.IsMatch(CodeOnly(path), deferredDeclaration, RegexOptions.CultureInvariant))
      .ToArray();
    Assert.Empty(deferred);
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

  // ⚠ THE BAN READS CODE, NOT PROSE (T-263). Widening the pattern to catch
  // `IHttpContextAccessor` produced an immediate false red on `ITenantDatabaseResolver.cs`, whose comment
  // explains at length WHY it must not depend on IHttpContextAccessor. **A file was going to fail this
  // rule for documenting the rule.**
  //
  // The blindness was always here; the old pattern simply could not match that name anywhere, so it never
  // reached prose either. `RepositoryPathPortabilityTests` strips comments for exactly this reason and
  // says so -- the second guard in this suite to need it is the point at which it stops being incidental.
  //
  // A false red is worse than a missing rule: it is what teaches people to weaken guards.
  private static string CodeOnly(string path) =>
    string.Join(
      "\n",
      File.ReadAllText(path).Split('\n').Select(line =>
      {
        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 ? line[..comment] : line;
      }));
}
