using SSAS.BuildingBlocks.Api.Transport;
using System.Reflection;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Architecture.Tests;

// Durable Phase-2 platform-support authority invariants (ADR-015 / DEC-TEN-0018).
public sealed class PlatformSupportAuthorityArchitectureTests
{
  // ⚠⚠⚠ NINE `Assert.Contains(assembly.GetTypes(), type => type.Name == …)` SITES BECAME THIS (T-118).
  //
  // Every one failed with xUnit's default — *"Assert.Contains() Failure: Filter not matched in collection"*
  // — **over several hundred types, naming neither the type sought nor the assembly searched.** A reader met
  // a red telling them that a filter did not match something, somewhere.
  //
  // ⚠ AND THEY WERE INVISIBLE TO THE CENSUS BUILT TO FIND THEM. The T-082 sweep counted `DoesNotContain`
  // only — **while the file recording that sweep documents, three paragraphs above its own numbers, that
  // `Assert.Contains` produces exactly the same silent failure.** *The rule and the measurement of the rule
  // sat on one page and disagreed.* Found by putting a positive control on a zero, on the last task of the
  // night, after the number had been committed twice.
  private static void AssertTypeExists(Assembly assembly, string typeName)
  {
    var types = assembly.GetTypes();

    Assert.True(
      types.Any(type => type.Name == typeName),
      $"`{typeName}` was not found in `{assembly.GetName().Name}` — searched type count: {types.Length}. " +
      "Either it was renamed or removed, or it has moved to another assembly — in which case this test is " +
      "now asserting the presence of a surface in the wrong place, which passes for the wrong reason the " +
      "day something with that name reappears here.");
  }

  [Fact]
  public void Platform_support_authority_is_global_and_not_tenant_or_company_owned()
  {
    foreach (var type in new[] { typeof(PlatformSupportPrincipal), typeof(PlatformPermissionAssignment) })
    {
      Assert.DoesNotContain(typeof(ITenantOwnedEntity), type.GetInterfaces());
      // ⚠ A LOOKUP WHOSE MISS IS THE ASSERTED VALUE (258). `GetProperty` returns null for a name that
      // does not exist AND for one that is misspelt. Bound to the ownership contracts that DEFINE these
      // dimensions, so the assertion says "this type carries neither dimension" in the vocabulary that
      // declares them.
      Assert.Null(type.GetProperty(nameof(SSAS.BuildingBlocks.Domain.ITenantOwnedEntity.TenantId)));
      Assert.Null(type.GetProperty(nameof(SSAS.BuildingBlocks.Domain.ICompanyOwnedEntity.CompanyId)));
    }
  }

  [Fact]
  public void No_platform_role_construct_is_introduced()
  {
    var forbidden = new[] { "PlatformRole", "SupportRole", "AdminRole", "SuperAdminRole", "PlatformSupportRole" };

    foreach (var assembly in new[] { typeof(PlatformSupportPrincipal).Assembly, typeof(PlatformSupportPermissionFilter).Assembly })
    {
      // ⚠ THE ASSEMBLY NAME IS IN THE MESSAGE BECAUSE THIS LOOP RUNS TWICE. The previous
      // `Assert.DoesNotContain(collection, predicate)` reported only "Filter matched in collection" — which
      // named neither the offending type NOR which of the two assemblies produced it (T-087).
      var constructs = assembly.GetTypes()
        .Where(type => forbidden.Contains(type.Name, StringComparer.Ordinal))
        .Select(type => type.Name)
        .ToArray();

      Assert.True(constructs.Length == 0,
        $"{assembly.GetName().Name} declares a platform role construct: {string.Join(", ", constructs)}. " +
        "Platform support is permission-based by decision, and a role type reintroduces exactly the " +
        "grouping that design removed. Grant the permissions directly rather than naming a role.");
    }
  }

  [Fact]
  public void Only_platform_support_permissions_can_be_granted_to_a_principal()
  {
    var catalog = new PlatformPermissionCatalog();
    var principal = PlatformSupportPrincipal.Register(1).Value;
    var occurred = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    Assert.True(catalog.TryGet(PlatformPermissionNames.ViewCompanies, out var tenantScoped));
    Assert.True(principal.GrantPermission(tenantScoped, "actor", occurred).IsFailure);

    Assert.True(catalog.TryGet(PlatformPermissionNames.ManageTenants, out var platformScoped));
    Assert.Equal(PermissionScope.PlatformSupport, platformScoped.Scope);
    Assert.True(principal.GrantPermission(platformScoped, "actor", occurred).IsSuccess);
  }

  [Fact]
  public void The_two_permission_planes_stay_structurally_separate()
  {
    var catalog = new PlatformPermissionCatalog();
    var occurred = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    // A tenant role cannot receive a PlatformSupport permission...
    var role = Role.CreateCustom(Guid.NewGuid(), RoleName.Create("Administrator").Value, null, Guid.NewGuid(), occurred);
    Assert.True(catalog.TryGet(PlatformPermissionNames.ManageTenants, out var platformPermission));
    Assert.True(role.AssignPermission(platformPermission, "actor", Guid.NewGuid(), occurred).IsFailure);

    // ...and a platform-support principal cannot receive a Tenant permission.
    var principal = PlatformSupportPrincipal.Register(1).Value;
    Assert.True(catalog.TryGet(PlatformPermissionNames.ViewCompanies, out var tenantPermission));
    Assert.True(principal.GrantPermission(tenantPermission, "actor", occurred).IsFailure);
  }

  [Fact]
  public void Platform_authority_read_query_surface_exists_but_is_not_http_exposed()
  {
    // Phase 4C adds the Application read/query surface (DEC-TEN-0025). It exists...
    var applicationAssembly = typeof(PlatformSupportPermissionFilter).Assembly;
    foreach (var typeName in new[]
    {
      "IPlatformSupportAuthorityReadService",
      "ListPlatformSupportPrincipalsQueryHandler",
      "GetPlatformSupportPrincipalQueryHandler",
      "ListPlatformPermissionAssignmentsQueryHandler",
      "GetActivePlatformSupportPermissionsQueryHandler",
    })
    {
      AssertTypeExists(applicationAssembly, typeName);
    }

    // ...and Phase 4D exposes it over HTTP through a single authority transport, which must project
    // transport-owned DTOs rather than leaking Application/EF read types onto the wire (DEC-TEN-0025).
    // Anchored on a PLATFORM-owned transport type. RowVersionCodec no longer identifies this assembly: it
    // moved to the shared API project in FP-006C5, and anchoring on it would silently retarget this test.
    var apiAssembly = typeof(ProblemResults).Assembly;
    AssertTypeExists(apiAssembly, "PlatformSupportAuthorityEndpointRouteBuilderExtensions");
    AssertTypeExists(apiAssembly, "PlatformSupportPrincipalResponse");
    AssertTypeExists(apiAssembly, "PlatformPermissionAssignmentResponse");
  }

  [Fact]
  public void Platform_authorization_primitives_exist_but_no_phase_4_http_exposure_yet()
  {
    // Phase 3C persistence/orchestration + Phase 4A authorization primitives exist now.
    var domainAssembly = typeof(PlatformSupportPrincipal).Assembly;
    AssertTypeExists(domainAssembly, "PlatformAuthenticationSession");
    AssertTypeExists(domainAssembly, "PlatformRefreshTokenRecord");

    var applicationAssembly = typeof(PlatformSupportPermissionFilter).Assembly;
    AssertTypeExists(applicationAssembly, "PlatformAuthenticationSessionCreator");
    AssertTypeExists(applicationAssembly, "RefreshPlatformAuthenticationSessionCommandHandler");

    // Phase 4A adds the platform authorization handler + RequirePlatformPermission convention.
    var hostAssembly = typeof(SSAS.Host.API.Authorization.PermissionAuthorizationHandler).Assembly;
    AssertTypeExists(hostAssembly, "PlatformPermissionAuthorizationHandler");

    // The convention itself is module-neutral and moved to the shared API project in FP-006C5 — the platform
    // PLANE is expressed by which helper an endpoint calls, not by which assembly owns the helper.
    var sharedApiAssembly = typeof(PermissionEndpointConventions).Assembly;
    var conventions = sharedApiAssembly.GetTypes()
      .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
      .ToArray();

    Assert.True(
      conventions.Any(method => method.Name == "RequirePlatformPermission"),
      $"searched method count in `{sharedApiAssembly.GetName().Name}`: {conventions.Length}, and none is " +
      "named `RequirePlatformPermission`. The convention has been renamed, moved to another assembly, or " +
      "removed — and the platform PLANE is expressed by which helper an endpoint calls, so its absence " +
      "means endpoints have no way to declare themselves platform-scoped.");

    // But no platform authentication/admin HTTP transport is exposed yet (Phase 4B/4D remain deferred): no
    // endpoint route builder maps the internal platform session creator or refresh handler. Checked in the
    // PLATFORM API assembly, which is where such a transport would have to live.
    var platformApiAssembly = typeof(ProblemResults).Assembly;

    // ⚠ ONE WALK, BOTH TERMS, AND THE OFFENDER NAMED (T-089). These were two `DoesNotContain(collection,
    // predicate)` calls over the same assembly, each reporting only "Filter matched in collection" — over
    // several hundred types, with no way to tell WHICH transport had appeared or which of the two terms
    // matched.
    var transports = platformApiAssembly.GetTypes()
      .Where(type => type.Name.Contains("PlatformAuthorityEndpoint", StringComparison.Ordinal) ||
        type.Name.Contains("PlatformSessionEndpoint", StringComparison.Ordinal))
      .Select(type => type.FullName ?? type.Name)
      .OrderBy(value => value, StringComparer.Ordinal)
      .ToArray();

    Assert.True(transports.Length == 0,
      $"a platform authentication or admin HTTP transport has appeared: {string.Join(", ", transports)}. " +
      "Phase 4B/4D are deferred, so this is either that work landing — in which case this test and the " +
      "deferral it records both need updating — or a transport reaching the Platform API assembly by a " +
      "route nobody intended.");
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0058")]
  [Trait("Acceptance", "AC-TEN-0057")]
  // ⚠ `AC-TEN-0057` AND `0058` ARE A MIRRORED PAIR AND THIS TEST CARRIES THE STRUCTURAL HALF OF BOTH.
  //
  // `0058` — *"A PLATFORM refresh token resolves only against PLATFORM-session persistence; it can never
  // continue into or mint a tenant token."* `0057` is the same sentence with the planes swapped. **The
  // assertion below is why neither is a matter of care at the call site: the platform creator and refresh
  // handler take `IPlatformAuthenticationSessionRepository` and are asserted NOT to take
  // `IAuthenticationSessionRepository` at all** — a handler cannot resolve against a store it has no way to
  // reach, so cross-plane resolution is refused by the constructor rather than by a check that could be
  // forgotten.
  //
  // ⚠⚠ WHAT IS NOT CARRIED, AND IT IS THE SAME GAP ON BOTH: *"can never … MINT a tenant token"* is about
  // ISSUANCE, not resolution. This test says nothing about which claims type the platform flow can hand to
  // the issuer. `AC-TEN-0075`'s typed-issuer assertion is the nearest thing — an `Issue` overload takes one
  // of exactly two claims records — but that constrains the ISSUER's API, not which record this flow builds.
  //
  // ⚠ AND `0058`'s LAST CLAUSE IS UNCARRIED TOO: *"NO REQUEST PARAMETER SELECTS A PLANE for an existing
  // refresh token."* That is a transport-shape claim about the refresh endpoint's contract, so its witness
  // is the platform-support authentication route contract, not this constructor walk.
  public void Platform_session_flow_depends_on_platform_persistence_not_tenant_session_persistence()
  {
    // The platform creation + refresh flow must resolve platform sessions only — never the tenant session
    // repository — so cross-plane isolation is structural (DEC-TEN-0022). Also: no bool/string plane selector.
    foreach (var type in new[]
    {
      typeof(SSAS.Platform.Application.Authentication.PlatformAuthenticationSessionCreator),
      typeof(SSAS.Platform.Application.Authentication.RefreshPlatformAuthenticationSessionCommandHandler)
    })
    {
      var parameters = type.GetConstructors().Single().GetParameters();
      Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(SSAS.Platform.Application.Abstractions.Persistence.IPlatformAuthenticationSessionRepository));
      Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(SSAS.Platform.Application.Abstractions.Persistence.IAuthenticationSessionRepository));
      Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(bool) || parameter.ParameterType == typeof(string));
    }
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0055")]
  // `AC-TEN-0055` — *"`PlatformAuthenticationSession` is not `ITenantOwnedEntity`/`ICompanyOwnedEntity`,
  // RECEIVES NO TENANT QUERY FILTER, and contains no `TenantId`, `TenantUserId`, or `CompanyId`."* All three
  // clauses are here, and the middle one is carried by the first rather than separately:
  // **`PersistenceDbContext.ConfigureTenantFilter<TEntity>` applies the global filter to every
  // `ITenantOwnedEntity`, so NOT IMPLEMENTING IT *IS* NOT HAVING THE FILTER** — the interface is the
  // selector, not a label. Same argument as `AC-TEN-0018` on `Tenant` itself.
  //
  // ⚠ FOUND BY ASKING WHERE A TYPE-SHAPE CLAIM COULD BE WITNESSED, NOT BY OPENING THE FILE THE CRITERION'S
  // SUBJECT NAMES. `0055` sits in the token-and-session block, so the obvious home was
  // `PlatformAccessTokenClaimsTests` — where it would have been an assertion about behaviour that this
  // criterion never makes. **The criterion is structural, so its witness is structural.**
  public void Platform_authentication_session_is_global_and_not_tenant_or_company_owned()
  {
    var session = typeof(PlatformAuthenticationSession);
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), session.GetInterfaces());
    Assert.DoesNotContain(typeof(ICompanyOwnedEntity), session.GetInterfaces());
    foreach (var forbidden in new[] { "TenantId", "TenantUserId", "CompanyId" })
    {
      Assert.Null(session.GetProperty(forbidden));
    }

    Assert.NotNull(session.GetProperty(nameof(PlatformAuthenticationSession.IdentityId)));
    Assert.NotNull(session.GetProperty(nameof(PlatformAuthenticationSession.PlatformSupportPrincipalId)));

    var refreshRecord = typeof(PlatformRefreshTokenRecord);
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), refreshRecord.GetInterfaces());
    Assert.Null(refreshRecord.GetProperty(nameof(SSAS.BuildingBlocks.Domain.ITenantOwnedEntity.TenantId)));
    Assert.NotNull(refreshRecord.GetProperty(nameof(PlatformRefreshTokenRecord.PlatformAuthenticationSessionId)));
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0069")]
  // ⚠ `AC-TEN-0069` CITED FOR ITS SECOND HALF ONLY, AND THE FIRST HALF IS NAMED SO THE ID DOES NOT BURY IT.
  //
  // *"`StrictAccessTokenValidator` SELECTS THE TENANT/PLATFORM PROFILE STRUCTURALLY BY `security_plane` ‖ and
  // PERFORMS NO DATABASE OR LIVE PRINCIPAL-STATUS LOOKUP."*
  //
  //   after the bar   CARRIED — no `DbContext`, repository or read-service field, on a static class.
  //   before the bar  NOT CARRIED — nothing here observes that the profile is chosen by the
  //                   `security_plane` claim rather than by any other route. A validator that picked the
  //                   profile from the issuer, the audience, or a hard-coded default would pass every line
  //                   of this test.
  //
  // **The two halves live in different layers — one is a field-shape claim and the other is a behavioural
  // one — and citing the whole id here would have put a green test over the untested half.** Same shape as
  // `AC-TEN-0007`, whose eligibility clause is asserted in the domain while its three-consumer clause is
  // not. The structural-selection half wants a behavioural test over the validator's profile choice.
  public void Strict_access_token_validator_is_stateless_and_holds_no_persistence_dependency()
  {
    // Phase 3C-2 adds a platform profile branch but the validator must remain structural/stateless:
    // no DbContext, repository, or read-service field may be introduced (DEC-TEN-0022 / ADR-016).
    var validator = typeof(SSAS.Host.API.Authentication.StrictAccessTokenValidator);
    Assert.True(validator is { IsAbstract: true, IsSealed: true }); // static class

    var fields = validator.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
    Assert.DoesNotContain(fields, field =>
      field.FieldType.Name.Contains("DbContext", StringComparison.Ordinal) ||
      field.FieldType.Name.Contains("Repository", StringComparison.Ordinal) ||
      field.FieldType.Name.Contains("ReadService", StringComparison.Ordinal));
  }

  // ==================================================================================================
  // ⚠⚠⚠ DO NOT DELETE THIS TEST. IT ASSERTS AN ENUM AGAINST ITS OWN NAMES AND `AC-TEN-0038` RESTS ON IT.
  // ==================================================================================================
  //
  // `AC-TEN-0038` — *"the ONLY transitions are `Active -> Disabled` and `Disabled -> Active`"* — is carried
  // by four tests in `PlatformSupportAuthorityTests` covering the four ordered pairs over two statuses, two
  // legal and two refused. **That is a COMPLETE CASE ANALYSIS only because this line fixes the statuses at
  // two.**
  //
  // **DELETING THIS CONVERTS THAT ANALYSIS INTO A SAMPLE AND NOTHING WILL REDDEN.** A third status arrives
  // with four new ordered pairs, none exercised, all four transition tests green, and their `AC-TEN-0038`
  // traits still claiming the criterion.
  //
  // ⚠ EDITING IS FINE AND EXPECTED — change the vocabulary here, then add the transition rows the new member
  // creates. **The failure mode is REMOVING it**, because editing forces the question and removing answers
  // it silently. Same shape, and the same warning, as `TenantLifecycleDomainTests
  // .Status_and_reason_vocabularies_are_exact`, on which three tenant criteria depend.
  [Fact]
  public void Principal_status_enum_has_exactly_active_and_disabled()
  {
    Assert.Equal(
      ["Active", "Disabled"],
      Enum.GetNames<PlatformSupportPrincipalStatus>().OrderBy(name => name, StringComparer.Ordinal));
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0034")]
  // `AC-TEN-0034` — *"Being present in bootstrap CONFIGURATION never authorizes ordinary platform
  // operations; it authorizes only the genesis/recovery operation."*
  //
  // The assertion below keeps every bootstrap-configuration type out of the DOMAIN and APPLICATION
  // assemblies. **Authorization is decided in those two layers — catalog, permissions, principal status — so
  // configuration cannot authorize anything there because those layers cannot SEE it.** Absent capability in
  // the product, and observed, which is the strong form.
  //
  // ⚠⚠⚠ BUT THE GUARD'S STATED REASON HAS ROTTED WHILE ITS EFFECT HAS NOT. Its comment says *"Genesis/
  // recovery bootstrap is Phase 3B, not Phase 3A"* — a PHASE-BOUNDARY marker. **Bootstrap landed in 3B and
  // this test still passes, because bootstrap went into INFRASTRUCTURE and the guard watches Domain and
  // Application.** ***THE RATIONALE IS STALE AND THE ASSERTION IS EXACTLY RIGHT FOR A REASON ITS AUTHOR DID
  // NOT WRITE DOWN*** — so a reader who checks whether the stated purpose still applies would conclude the
  // test is obsolete and delete a live guard.
  //
  // **This is the fifth rot kind (a superseded rationale) sitting on a control rather than on a criterion**,
  // and it is more dangerous here: a stale criterion misleads a reader, a stale rationale invites a deletion.
  public void No_bootstrap_configuration_is_introduced_in_this_phase()
  {
    // ⚠ RATIONALE CORRECTED. This previously read *"Genesis/recovery bootstrap (`DEC-TEN-0019`) is Phase 3B,
    // not Phase 3A"* — a PHASE-BOUNDARY marker. **Bootstrap LANDED in 3B and this test still passes, because
    // bootstrap went into INFRASTRUCTURE while this guard watches DOMAIN and APPLICATION.** The stated reason
    // had expired; the assertion had not. ***A READER CHECKING WHETHER THE STATED PURPOSE STILL APPLIED WOULD
    // HAVE FOUND IT DID NOT AND DELETED A LIVE GUARD*** — a stale criterion misleads, a stale RATIONALE
    // invites a deletion.
    //
    // THE LIVE REASON: authorization is decided in Domain and Application — catalog, permissions, principal
    // status — so keeping bootstrap CONFIGURATION out of both layers means configuration cannot authorize
    // anything, because the deciding layers cannot see it. That is `AC-TEN-0034`.
    var forbidden = new[] { "PlatformSupportBootstrapOptions", "PlatformSupportBootstrap", "PlatformSupportBootstrapGate" };

    // ⚠⚠⚠ TWO OF THOSE THREE NAMES RESOLVE TO NO TYPE ANYWHERE IN `src/` — only
    // `PlatformSupportBootstrapOptions` exists. **THAT IS RECORDED, NOT RESOLVED**, because the two readings
    // are indistinguishable from here:
    //   FORWARD-LOOKING   a name reserved in advance so such a type can never be created in these layers —
    //                     legitimate, and a ban whose purpose is to PREVENT a type cannot be required to
    //                     point at one.
    //   SILENTLY RETIRED  a name that matched something once and no longer does after a rename — in which
    //                     case that entry now watches nothing.
    // **Nothing in the source says which was intended, so no assertion here can separate them.**
    //
    // ***MAINTENANCE INSTRUCTION, WHICH IS WHAT MAKES THIS A CONTROL RATHER THAN A NOTE: IF YOU RENAME
    // `PlatformSupportBootstrapOptions`, UPDATE THIS LIST IN THE SAME COMMIT — after that rename the guard
    // watches nothing at all, and it will stay green while doing so.***
    foreach (var (assembly, anchor) in new[]
    {
      (typeof(PlatformSupportPrincipal).Assembly, nameof(PlatformSupportPrincipal)),
      (typeof(PlatformSupportPermissionFilter).Assembly, nameof(PlatformSupportPermissionFilter))
    })
    {
      var types = assembly.GetTypes();

      // FLOOR on the scanned population (the T-258 shape): an empty enumeration is this ban's success
      // condition, so the count must be asserted on what was SCANNED, never on what was found.
      Assert.True(types.Length >= 20,
        $"only {types.Length} types scanned in {assembly.GetName().Name}; the enumeration collapsed.");

      // ⚠⚠ KNOWN-POSITIVE CONTROL (the T-263 shape, borrowed rather than invented). The floor proves types
      // were scanned; it cannot prove the ORDINAL NAME COMPARISON still selects anything. **A ban whose
      // matcher matches nothing is green for the wrong reason**, so the same `Contains(..., Ordinal)` runs
      // over the same collection for a name that MUST be present in this assembly.
      //
      // ***THIS IS DELIBERATELY NOT "every forbidden name must resolve to a type" — that control would
      // redden on the two forward-looking entries above and outlaw a legitimate use of a ban list. WHEN TWO
      // CAUSES OF AN OBSERVATION CANNOT BE SEPARATED, VERIFY THE MECHANISM RATHER THAN ASSUMING A CAUSE.***
      Assert.Contains(types, type => new[] { anchor }.Contains(type.Name, StringComparer.Ordinal));

      Assert.DoesNotContain(types, type => forbidden.Contains(type.Name, StringComparer.Ordinal));
    }
  }
}
