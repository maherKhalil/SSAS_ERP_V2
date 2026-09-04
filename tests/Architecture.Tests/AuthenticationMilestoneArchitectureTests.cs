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
    // ================================================================================================
    // ⚠⚠ THE FOUR BANNED PREFIXES SPLIT INTO TWO KINDS, AND THE SPLIT IS STRUCTURAL (272)
    // ================================================================================================
    //
    // Item `272` converted the boundary guards to read DECLARED dependencies from the `.csproj`, because
    // `GetReferencedAssemblies()` omits a reference no type is taken from. **Which reading is stronger is a
    // function of HOW THE DEPENDENCY CAN ARRIVE**, and these four do not arrive the same way — so they are
    // separated here rather than annotated as a group. A note covering all four would say the wrong thing
    // about two of them whichever way it was written.
    //
    // DECLARABLE: both readings apply, and declared is the stronger one — it catches the capability the
    // moment the `.csproj` merges, before any type is used.
    var declarable = new[] { "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore" };

    // ⚠⚠⚠ TRANSITIVE OR FRAMEWORK ONLY: **DECLARED IS EMPTY BY CONSTRUCTION FOR THESE AND A DECLARED CHECK
    // WOULD PASS VACUOUSLY.** `Microsoft.Data.SqlClient` reaches this tree through `EntityFrameworkCore.
    // SqlServer` and appears in no `.csproj` of ours; `System.Security.Cryptography` is a framework assembly
    // and never a `PackageReference` at all. The whole repository declares ELEVEN package references and
    // neither of these is among them. **Emitted is the correct instrument here, and that is a decision.**
    var transitiveOnly = new[] { "Microsoft.Data.SqlClient", "System.Security.Cryptography" };

    var forbiddenPrefixes = declarable.Concat(transitiveOnly).ToArray();
    var assemblies = new[] { typeof(AuthenticationAccount).Assembly, typeof(AuthenticationPolicy).Assembly };

    // One exercise per DECLARABLE branch: a control proves a predicate can fire, and these two share
    // nothing, so one would leave the other holding over a parse that recognises it nowhere.
    //
    // ⚠ AND DERIVED FROM `declarable` RATHER THAN RESTATED BESIDE IT (278). Hardcoded control terms cannot
    // notice a term ADDED to the ban, which would then hold over a prefix nothing witnesses — silently.
    // The inline `StartsWith` stays; the control section in `DeclaredDependencies` says why widening a
    // match is loud and only narrowing is silent.
    var witnessOf = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      ["Microsoft.EntityFrameworkCore"] = "SSAS.BuildingBlocks.Infrastructure",
      ["Microsoft.AspNetCore"] = "SSAS.Host.API"
    };

    Assert.All(declarable, term =>
    {
      Assert.True(witnessOf.TryGetValue(term, out var witness),
        $"'{term}' is banned but no project is named as its declared witness. Add one, or move the term " +
        "to `transitiveOnly` with grounds — an unwitnessed term bans nothing and reads as coverage.");
      Assert.Contains(
        DeclaredDependencies.Of(witness!), name => name.StartsWith(term, StringComparison.Ordinal));
    });

    var violations = assemblies
      .SelectMany(assembly => assembly.GetReferencedAssemblies()
        .Where(reference => forbiddenPrefixes.Any(prefix => reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true))
        .Select(reference => $"{assembly.GetName().Name} -> {reference.Name}"))
      .ToArray();

    // The emitted read is shown to see SOMETHING on each assembly, so an empty violation set means "none of
    // the four" rather than "no references read at all" — the control the transitive branches depend on,
    // since no declared witness can exist for them.
    foreach (var assembly in assemblies)
    {
      Assert.NotEmpty(assembly.GetReferencedAssemblies());
    }

    Assert.Empty(violations);

    // And the declarable half, at the layer the emitted read cannot see.
    var declared = assemblies
      .SelectMany(assembly => DeclaredDependencies.Of(assembly)
        .Where(name => declarable.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
        .Select(name => $"{assembly.GetName().Name} DECLARES {name}"))
      .ToArray();

    Assert.Empty(declared);
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

    // ⚠⚠ `SymmetricSecurityKey` IS BANNED IN TWO PLACES IN THIS FILE AND THE OTHER ONE IS NOT A DUPLICATE.
    // This ban is about LAYERING: no token-framework type may appear in Platform Domain or Application,
    // whatever it is for. `No_symmetric_signing_path_remains_active_anywhere_under_src` is about an
    // ALGORITHM: no symmetric signing anywhere in `src/`, including the assemblies where JWT work is
    // legitimate. Neither contains the other — this one also bans `HttpContext` and `CookieOptions` in two
    // assemblies; that one also bans `HmacSha*` in every assembly.
    //
    // ⚠ TWO SITES ENFORCING ONE NAME IS THE REDUNDANCY TOPOLOGY, IN WHICH EACH SITE INDIVIDUALLY LOOKS
    // DEAD. Delete either and the other still refuses a `SymmetricSecurityKey` in Platform Application, so
    // no test reddens and the deletion reads as tidying — **but the two cover different scopes, and half of
    // that coin deletes the only guard over `SSAS.Host.API`.** Which is why both say which is which.
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

  [Fact]
  [Trait("Criterion", "AC-AUTH-0040")]
  public void No_symmetric_signing_path_remains_active_anywhere_under_src()
  {
    // `AC-AUTH-0040`'s LAST CLAUSE: *"no symmetric path remains active."* Every other clause of that
    // criterion is a claim about how one token is judged, and `JwtInfrastructureTests` witnesses each by
    // presenting a token. **THIS CLAUSE IS NOT ABOUT A TOKEN AT ALL — it is about what the tree contains**,
    // and no token can witness it: `Algorithm_substitution_is_rejected` proves the CONFIGURED validator
    // refuses HS256, which is compatible with a second, symmetric issuer sitting unused elsewhere in `src/`
    // waiting to be wired up. *Remains active* is a property of the code, so the guard reads the code.
    //
    // ⚠ THE BAN IN `Milestone_four_keeps_token_framework_types_out_of_domain_and_application` ALREADY NAMES
    // `SymmetricSecurityKey` AND DOES NOT COVER THIS. **That one is a LAYERING rule over two assemblies —
    // no token-framework type in Domain or Application, whatever it is for. This one is an ALGORITHM rule
    // over every assembly.** Neither subsumes the other, and the overlap on one type name is what makes
    // each look like the other's duplicate; the note there says the same thing from the other side. It scans Platform Domain and Application — the two
    // assemblies where a JWT type has no business existing. **The symmetric path would live where the
    // asymmetric one does, in `SSAS.Host.API`, which that walk never visits.** A guard naming the right
    // type over the wrong scope reads, at a glance, exactly like this one.
    var sourceFiles = Directory
      .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .ToArray();
    Assert.True(sourceFiles.Length >= 400,
      $"only {sourceFiles.Length} source files were scanned; the walk has collapsed and the ban below " +
      "would pass over an empty set.");

    const string symmetricSigning = @"(?:SymmetricSecurityKey|HmacSha(?:256|384|512)|""HS(?:256|384|512)"")";
    // The matcher control. Each alternative is asserted against the form it would really appear in.
    Assert.Matches(symmetricSigning, "var key = new SymmetricSecurityKey(secret);");
    Assert.Matches(symmetricSigning, "SecurityAlgorithms.HmacSha256");
    Assert.Matches(symmetricSigning, @"ValidAlgorithms = [""HS256""],");
    Assert.DoesNotMatch(symmetricSigning, "SecurityAlgorithms.RsaSha256");

    // ⚠⚠⚠ THE SCOPE IS `src/` AND WIDENING IT TO `tests/` WOULD BE A TRAP THAT LOOKS LIKE A TIGHTENING.
    // `JwtInfrastructureTests` FORGES symmetric tokens on purpose — `CreateToken` builds a
    // `SymmetricSecurityKey` with `HmacSha256` so that `Invalid_jwt_is_rejected_...` and
    // `Algorithm_substitution_is_rejected` have something to present. **A reader who widened this walk to
    // the test tree would get an immediate red whose obvious repair is deleting that helper — and deleting
    // it deletes the only fixtures proving the symmetric algorithm is refused.** The ban would then be
    // green, the criterion less covered than before, and nothing would say so.
    //
    // *The criterion says no symmetric path remains ACTIVE. A forgery in a test is not an active path; it
    // is the evidence that the path is closed.*
    var symmetric = sourceFiles
      .Where(path => Regex.IsMatch(CodeOnly(path), symmetricSigning, RegexOptions.CultureInvariant))
      .ToArray();
    Assert.Empty(symmetric);

    // ⚠⚠ AND THE POSITIVE HALF, WITHOUT WHICH THE BAN IS FREE. *No symmetric path remains active* is
    // perfectly satisfied by a tree that signs nothing at all — delete `AccessTokenIssuer` and the
    // assertion above goes green. **A ban states what must be absent and therefore cannot notice that the
    // thing it was protecting has gone**, which is the same shape as `AC-AUTH-0040`'s own *accepts only*:
    // the refusals need an acceptance beside them or they are satisfied vacuously. So the asymmetric path
    // is required to be present, in the same walk, by the same instrument.
    var asymmetric = sourceFiles
      .Where(path => Regex.IsMatch(CodeOnly(path), "SecurityAlgorithms.RsaSha256", RegexOptions.CultureInvariant))
      .ToArray();
    Assert.NotEmpty(asymmetric);
  }

  [Fact]
  [Trait("Criterion", "AC-SUB-0021")]
  // ==================================================================================================
  // `AC-SUB-0021`, pasted — *"A tenant with **no entitlement at all** can still authenticate, select its
  // tenant, refresh, log out, and reach platform support and the subscription surface"*
  // ==================================================================================================
  //
  // ⚠⚠ A "STILL WORKS" CRITERION HAS NO NATURAL PLANT, WHICH IS WHY THIS IS A STRUCTURAL GUARD RATHER THAN
  // A BEHAVIOURAL TEST. The happy path is already exercised — `PlatformAuthenticationEndToEndTests` seeds a
  // tenant with **no subscription row at all** and logs in successfully, so *authenticate* and *select* are
  // witnessed incidentally. **But a passing happy-path test cannot be planted against: the change that
  // would break this criterion is "make authentication consult entitlement", which is a feature, not an
  // edit.**
  //
  // ***SO THE GUARD ASSERTS THE MECHANISM THAT MAKES THE CRITERION TRUE: THE AUTHENTICATION SURFACE CANNOT
  // REFUSE FOR ENTITLEMENT BECAUSE IT CANNOT SEE IT.*** That is a claim about references, it is
  // plant-verifiable, and it fails on the FIRST line of the feature that would violate the criterion
  // rather than after somebody notices logins breaking.
  //
  // ⚠ THE BAN IS THE ENTITLEMENT VOCABULARY, ENUMERATED FROM `src/` RATHER THAN GUESSED:
  // `ITenantEntitlementCache`, `ITenantEntitlementReader` (Platform Application) and
  // `ITenantModuleEntitlement` (BuildingBlocks Api) are the whole of it — three interfaces, and the
  // unanchored `TenantEntitlement`/`ModuleEntitlement` stems catch every derived name.
  public void The_authentication_surface_cannot_see_entitlement()
  {
    var root = FindRepositoryRoot();
    var authenticationFiles = new[]
      {
        Path.Combine(root, "src", "Platform", "SSAS.Platform.Application", "Authentication"),
        Path.Combine(root, "src", "Platform", "SSAS.Platform.API", "Authentication")
      }
      .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
      .ToArray();

    // `EnumerateFiles` throws on a missing directory, so a renamed project is an exception rather than a
    // silent empty walk. The floor guards the FILTER, which is the part that can collapse quietly.
    Assert.True(authenticationFiles.Length >= 40,
      $"only {authenticationFiles.Length} authentication files were scanned; the walk has stopped matching " +
      "and 'authentication cannot see entitlement' would mean nothing.");

    const string entitlementVocabulary = @"(?:TenantEntitlement|ModuleEntitlement|IsEntitled)";
    // The matcher control: it must match the real names and not match its neighbours.
    Assert.Matches(entitlementVocabulary, "ITenantEntitlementReader reader");
    Assert.Matches(entitlementVocabulary, "ITenantModuleEntitlement entitlement");
    Assert.Matches(entitlementVocabulary, "if (!IsEntitled(module))");
    Assert.DoesNotMatch(entitlementVocabulary, "IAuthenticationSessionRepository sessions");

    var offenders = authenticationFiles
      .Where(path => Regex.IsMatch(CodeOnly(path), entitlementVocabulary, RegexOptions.CultureInvariant))
      .Select(path => Path.GetFileName(path))
      .ToArray();

    Assert.Empty(offenders);
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
