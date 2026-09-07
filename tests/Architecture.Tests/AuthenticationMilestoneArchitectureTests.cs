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
  public void No_file_under_src_names_a_symmetric_signing_algorithm()
  {
    // ---- ⚠⚠ WHAT THIS IS, SAID PLAINLY, BECAUSE THE OLD NAME SAID SOMETHING STRONGER.
    //
    // It was `No_symmetric_signing_path_remains_active_anywhere_under_src`. **This is a VOCABULARY over
    // file text, not the mechanism.** The mechanism is *a token is signed with a key the verifier also
    // holds*, and no string search can decide that. Specifically invisible to it:
    //
    //   * an algorithm chosen at RUNTIME — read from configuration into a variable, or selected by a
    //     switch, so that no literal algorithm name appears anywhere;
    //   * a key CONSTRUCTED rather than named — a `SecurityKey` subclass, or a symmetric key produced by a
    //     factory whose type name says nothing about symmetry;
    //   * a library DEFAULT, where the algorithm is never written down at all.
    //
    // ⚠ AND ONE HOLE FOUND BY MEASUREMENT RATHER THAN REASONING (T-090): THE MATCH IS CASE-SENSITIVE.
    // `HmacSha256` is caught; `HMACSHA256` — the .NET type name — is not. That is deliberate and not an
    // oversight: `src/Host/SSAS.Host.API/Authentication/AuthenticationTransportServices.cs:86` legitimately
    // constructs `new HMACSHA256(hmacKey)` to hash rate-limiter partition keys so raw IPs are not held in
    // memory. **That is a keyed hash, not a token signature.** Matching case-insensitively would redden on
    // correct code, and a guard that fires on correct code gets deleted rather than fixed. The consequence
    // is stated rather than closed: a hand-rolled JWT signed by constructing `HMACSHA256` directly would
    // pass this test.
    //
    // ⚠⚠ SEARCHED BEFORE WIDENING (T-090). Across every file `git ls-files src` reports — so `bin`/`obj`
    // could not contaminate it — the ONLY hit for `HS256|HS384|HS512|SymmetricSecurityKey|HmacSha`, case
    // INSENSITIVE, is that rate-limiter hash. `appsettings.json` carries `ActiveSigningCertificatePath`
    // and no algorithm setting, no key and no secret. **The old guard's green was a true green**; this
    // closes a hole in what it could see, not a breach in what it was watching.
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
    // ---- ⚠⚠⚠ EVERY FILE UNDER `src`, NOT EVERY `.cs` FILE UNDER `src` (T-090).
    //
    // The name of this test says *anywhere under src*. The walk read `*.cs` and skipped `Migrations`, so
    // **`src/Host/SSAS.Host.API/appsettings.json` was outside it** — and a JWT algorithm is exactly the
    // kind of thing that lives in configuration. `"Jwt": { "Algorithm": "HS256" }` would have been a
    // symmetric signing path, under `src`, invisible to a guard whose name promised to look there.
    //
    // ⚠ THE `Migrations` EXCLUSION IS GONE BECAUSE NOTHING JUSTIFIED IT. It was carried, not argued: no
    // comment here or at the sibling walk gave a reason, and generated migration files are as capable of
    // containing a literal as any other. An exclusion nobody can explain is indistinguishable from an
    // oversight, so it is removed rather than documented.
    //
    // The population is now genuinely every tracked file kind under `src` — 33 `.csproj`, 6 `.json`, 1
    // `.md`, 7 `.gitkeep` and the `.cs` tree — because the cost of scanning them is nothing and the cost
    // of a name that promises more than it inspects is what this whole exercise has been about.
    var sourceFiles = FilesUnderSrc();
    Assert.True(sourceFiles.Length >= 400,
      $"only {sourceFiles.Length} files were scanned; the walk has collapsed and the ban below " +
      "would pass over an empty set.");

    // ⚠ A POSITIVE CONTROL ON THE WIDENING ITSELF, because "now it reads config too" is a claim about the
    // walk that the floor above cannot make: a `*.cs`-only walk clears 400 comfortably.
    //
    // ⚠⚠⚠ REACH PROBE, BOTH COLOURS MEASURED (T-090). `"Algorithm": "HS256"` was planted in the `Jwt`
    // section of `src/Host/SSAS.Host.API/appsettings.json` — a real symmetric signing setting, in the file
    // it would really live in.
    //
    //   OLD `*.cs` WALK  -> GREEN. The plant sat in `src`, in configuration, and the guard whose name said
    //                       *anywhere under src* did not see it.
    //   WIDENED WALK     -> RED.
    //
    // ⚠ AND THIS CONTROL EARNED ITS PLACE IN THE SAME RUN: with the walk reverted to `*.cs` it failed
    // FIRST, before the ban was reached — so the narrowing is caught by name rather than by the ban
    // silently passing. The ban's own old colour had to be measured with this control disabled, because
    // ordered checks hide all but the first.
    Assert.Contains(sourceFiles, path => path.EndsWith("appsettings.json", StringComparison.Ordinal));

    // The matcher control. Each alternative is asserted against the form it would really appear in.
    Assert.Matches(SymmetricSigning, "var key = new SymmetricSecurityKey(secret);");
    Assert.Matches(SymmetricSigning, "SecurityAlgorithms.HmacSha256");
    Assert.Matches(SymmetricSigning, @"ValidAlgorithms = [""HS256""],");
    Assert.DoesNotMatch(SymmetricSigning, "SecurityAlgorithms.RsaSha256");

    // ⚠ THE CASE-INSENSITIVE ARM, ADDED IN T-092. `HMACSHA256` is the .NET TYPE NAME and the old
    // case-sensitive match could not see it — a hand-rolled JWT signed by constructing it directly would
    // have passed. This control is here rather than in prose because the widening is the whole change.
    Assert.Matches(SymmetricSigning, "using var hmac = new HMACSHA256(key);");

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
      .Where(path => Regex.IsMatch(WithExemptionsRemoved(path), SymmetricSigning))
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
  // ==================================================================================================
  // THE SYMMETRIC VOCABULARY, ITS ONE EXEMPTION, AND THE BIND THAT KEEPS THE EXEMPTION HONEST (T-092)
  // ==================================================================================================
  //
  // ⚠ CASE-INSENSITIVE, AND THAT IS THE WHOLE POINT OF T-092. It was case-SENSITIVE, so `HmacSha256` was
  // caught and `HMACSHA256` — the .NET type name — was not. **A hand-rolled JWT signed by constructing
  // `HMACSHA256` directly passed this guard**, and the only reason anyone noticed is that a `-i` grep run
  // for a different purpose surfaced the one existing use.
  //
  // ⚠⚠ THE TWO ERRORS DO NOT COST THE SAME HERE, WHICH IS WHY THE WIDENING WINS. A false RED is one
  // exemption entry, seen immediately, by whoever caused it. A false CLEAR is a symmetric signing path
  // shipping undetected — and a symmetric key means anyone who can read it can mint tokens. *A false flag
  // announces itself; a false clear does not.*
  private const string SymmetricSigning =
    @"(?i:SymmetricSecurityKey|HmacSha(?:256|384|512)|""HS(?:256|384|512)"")";

  // ---- THE EXEMPTIONS. GROUNDS ENFORCED BY THE TYPE, NOT BY GOOD INTENTIONS.
  //
  // The tuple shape is taken from `RouteConstraintArchitectureTests`, which is the strongest exemption form
  // in this suite: **an entry cannot be added without typing what it is and why**, because the compiler
  // will not let you. A bare route — or here, a bare filename — is a blanket hole with extra steps.
  //
  // ⚠ `Snippet` IS THE EXACT TEXT NEUTRALISED, NOT THE FILE. Exempting a whole file would pre-approve
  // every symmetric construct anyone adds to it later; this removes one expression and leaves the rest of
  // that file under the ban exactly as before.
  private static readonly (string File, string Snippet, string Why)[] SymmetricExemptions =
  [
    ("AuthenticationTransportServices.cs",
      "new HMACSHA256(hmacKey)",
      "A KEYED HASH, NOT A TOKEN SIGNATURE. It hashes rate-limiter partition keys — endpoint, partition " +
      "material and IP — so raw client addresses are never held in memory. Nothing is signed and nothing " +
      "is verified against it; the output is a dictionary key. Read the `Hash` method before widening this.")
  ];

  // ⚠⚠⚠ PLANTED THREE WAYS (T-092), AND THE OLD COLOUR NEEDED NO PLANT AT ALL.
  //
  //   OLD GUARD, no plant required -> GREEN with `new HMACSHA256(hmacKey)` sitting in the tree the whole
  //     time. That is a REAL old-green, not a simulated one: the guard ran for its whole life over a line
  //     it could not see, and its passes carried no information about that spelling.
  //   The SAME exempted snippet planted in a DIFFERENT file (`appsettings.json`) -> RED. The exemption is
  //     scoped to file AND text; it does not travel.
  //   The exemption's `Snippet` altered so it no longer matches real code -> BOTH tests RED.
  //
  // ⚠ THAT LAST RESULT IS A DESIGN PROPERTY WORTH KEEPING: a rotted exemption does not open a hole, it
  // makes NOISE. Because the entry stops neutralising the real line, the ban reddens too — so the failure
  // mode of this allow-list is a false RED, never a silent false clear.
  private static string WithExemptionsRemoved(string path)
  {
    var text = CodeOnly(path);

    foreach (var exemption in SymmetricExemptions)
    {
      if (path.EndsWith(exemption.File, StringComparison.Ordinal))
      {
        text = text.Replace(exemption.Snippet, string.Empty, StringComparison.Ordinal);
      }
    }

    return text;
  }

  // ---- ⚠⚠⚠ THE REVERSE BIND. AN EXEMPTION THAT STOPS DESCRIBING REAL CODE IS A LIVE HOLE.
  //
  // T-085 found a grounds-check that had never run because its list was empty. This one runs, and it
  // asserts the thing that actually rots: **that each entry still names a real line.** If
  // `AuthenticationTransportServices.cs` is renamed, or that expression is rewritten or deleted, the
  // exemption keeps silently neutralising text in whatever file matches next — pre-approving code nobody
  // examined. So an entry that stops being true must REDDEN rather than persist.
  [Fact]
  public void Every_symmetric_exemption_still_describes_a_real_line()
  {
    Assert.NotEmpty(SymmetricExemptions);

    var files = FilesUnderSrc();

    foreach (var (file, snippet, why) in SymmetricExemptions)
    {
      Assert.False(string.IsNullOrWhiteSpace(why), $"{file} is exempted without grounds.");

      // ⚠ THE EXEMPTION MUST ACTUALLY EXEMPT SOMETHING. A snippet that does not match the ban neutralises
      // nothing and is an approval with no subject — which reads, at a glance, exactly like a real one.
      Assert.Matches(SymmetricSigning, snippet);

      var matches = files.Where(path => path.EndsWith(file, StringComparison.Ordinal)).ToArray();
      Assert.True(matches.Length == 1,
        $"the exemption for {file} matches {matches.Length} files under src, not one. It neutralises " +
        "text in every one of them, so a symmetric construct in any file sharing that name is " +
        "pre-approved by an entry written about a different file.");

      Assert.True(File.ReadAllText(matches[0]).Contains(snippet, StringComparison.Ordinal),
        $"the exemption for {file} no longer describes a real line: `{snippet}` is not in that file any " +
        "more. The code it approved has been changed, moved or deleted, and the entry is now a standing " +
        "permission for whatever occupies that file next. Delete the entry, or update it to the line that " +
        "replaced it AND re-read whether that line is still a keyed hash rather than a signature.");
    }
  }

  private static string[] FilesUnderSrc() =>
    [.. Directory
      .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), "*", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))];

  private static string CodeOnly(string path) =>
    string.Join(
      "\n",
      File.ReadAllText(path).Split('\n').Select(line =>
      {
        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 ? line[..comment] : line;
      }));
}
