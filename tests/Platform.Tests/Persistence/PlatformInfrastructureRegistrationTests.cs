using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;
using SSAS.Platform.Application.Permissions;
using SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Localization;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Infrastructure;
using SSAS.Platform.Infrastructure.Identity;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Tests.Persistence;

public sealed class PlatformInfrastructureRegistrationTests
{
  [Fact]
  public void Password_hasher_iteration_count_below_approved_minimum_fails_options_validation()
  {
    var services = new ServiceCollection();
    var configuration = CreateConfiguration(new Dictionary<string, string?>
    {
      ["Authentication:PasswordHasher:IterationCount"] = "99999"
    });
    services.AddPlatformInfrastructure(configuration);
    using var provider = services.BuildServiceProvider();

    Assert.Throws<OptionsValidationException>(() =>
      provider.GetRequiredService<IOptions<PasswordHasherOptions>>().Value);
  }

  [Theory]
  [InlineData("11", "128")]
  [InlineData("12", "63")]
  [InlineData("129", "128")]
  public void Invalid_password_length_configuration_fails_options_validation(string minimum, string maximum)
  {
    var services = new ServiceCollection();
    var configuration = CreateConfiguration(new Dictionary<string, string?>
    {
      ["Authentication:Policy:MinimumPasswordLength"] = minimum,
      ["Authentication:Policy:MaximumPasswordLength"] = maximum
    });
    services.AddPlatformInfrastructure(configuration);
    using var provider = services.BuildServiceProvider();

    Assert.Throws<OptionsValidationException>(() =>
      provider.GetRequiredService<IOptions<AuthenticationPolicyOptions>>().Value);
  }

  [Theory]
  [InlineData("SSAS-ERP-WEB")]
  [InlineData("Ssas-Erp-Web")]
  [InlineData("ssas-erp-web-2")]
  [InlineData("unknown-client")]
  [Trait("Criterion", "AC-AUTH-0025")]
  // ==================================================================================================
  // `AC-AUTH-0025`'s allow-list, ON THE PRODUCTION REGISTRY. **No compile-scope test constructed it.**
  // ==================================================================================================
  //
  // ⚠⚠⚠ THE TEST THAT LOOKS LIKE THIS ONE ASSERTS A THREE-LINE TEST DOUBLE.
  // `AuthenticationSessionApplicationTests.V1_client_allowlist_is_ordinal_and_maximum_length_is_enforced`
  // reads `var registry = new AllowedClientRegistry();` — **and `AllowedClientRegistry` is a private
  // nested class in that test file whose whole body is `IsAllowed(clientId) => clientId == Client;`.**
  // Its casing assertions pass because a record's `==` is ordinal, and they say nothing whatever about
  // `AuthenticationClientRegistry`'s `HashSet<string>(…, StringComparer.Ordinal)`.
  //
  // ***THE DOUBLE IS NAMED LIKE THE PRODUCTION TYPE — `AllowedClientRegistry` against
  // `AuthenticationClientRegistry` — SO THE LINE READS AS THE REAL ALLOW-LIST AT A GLANCE.*** The
  // production type is constructed only in `Integration.Tests`, which `GATE_SCOPE=TASK` compiles and does
  // not run, so at compile scope the only registries that exist are two doubles.
  //
  // ⚠⚠ MEASURED, NOT INFERRED: `StringComparer.Ordinal` -> `StringComparer.OrdinalIgnoreCase` in the
  // production registry. **All seven suites green.** The allow-list could match case-insensitively — which
  // is precisely the *casing differences* the criterion names — and nothing said so.
  //
  // ⚠ `ssas-erp-web-2` is here for the same reason as in the token theory: it is the row that catches a
  // `StartsWith` or `Contains` rewrite, and a `HashSet` today does not stop that being written tomorrow.
  public void Production_client_registry_allows_only_the_exact_ordinal_client_id(string clientId)
  {
    var registry = new AuthenticationClientRegistry(Options.Create(new AuthenticationClientOptions
    {
      AllowedClientIds = [AuthenticationClientId.V1Web]
    }));

    // The positive is in the same test and against the same instance: without it, a registry that allowed
    // NOTHING would satisfy all four rows.
    Assert.True(registry.IsAllowed(AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value));
    Assert.False(registry.IsAllowed(AuthenticationClientId.Create(clientId).Value));
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0025")]
  // ==================================================================================================
  // `AC-AUTH-0025`'s DEPLOYMENT half — and the test I set out to write was UNREACHABLE, which is itself
  // the finding.
  // ==================================================================================================
  //
  // The options carry `.Validate(… AllowedClientIds.Contains("ssas-erp-web") …)` with the message *"The V1
  // production client ssas-erp-web must be allowlisted."* The obvious witness is a configuration that omits
  // it, asserting startup fails. **That test cannot be written.**
  //
  // ⚠⚠⚠ `AllowedClientIds` IS DECLARED `= ["ssas-erp-web"]`, AND THE CONFIGURATION BINDER **APPENDS** TO A
  // DEFAULTED ARRAY RATHER THAN REPLACING IT. Measured, not assumed: binding
  // `Authentication:Clients:AllowedClientIds:0 = "some-other-client"` produced
  // **`ssas-erp-web,some-other-client`**. ***So no deployment can remove the V1 client, and that
  // `.Validate` clause is unreachable from configuration — it guards a change to the DEFAULT, in code,
  // and nothing else.***
  //
  // ⚠ That is not a defect and the clause should stay: it is the assertion that fires the day someone
  // changes the initialiser to `= []`. **But a citation claiming the deployment case is covered would have
  // been false, and the test proving it would have been unwritable — a combination that normally ends with
  // the criterion quietly marked covered.**
  //
  // So this pins the property that IS reachable and IS load-bearing: **a configuration naming only another
  // client still admits the V1 browser.** It fails if the default is emptied, if the binder's append
  // semantics change under a framework upgrade, or if the section is renamed.
  public void Configuration_naming_another_client_cannot_remove_the_v1_client_from_the_allowlist()
  {
    var services = new ServiceCollection();
    var configuration = CreateConfiguration(new Dictionary<string, string?>
    {
      ["Authentication:Clients:AllowedClientIds:0"] = "some-other-client"
    });
    services.AddPlatformInfrastructure(configuration);
    using var provider = services.BuildServiceProvider();

    var options = provider.GetRequiredService<IOptions<AuthenticationClientOptions>>().Value;

    Assert.Contains(AuthenticationClientId.V1Web, options.AllowedClientIds, StringComparer.Ordinal);
    // ⚠ AND THE APPEND ITSELF IS ASSERTED, because it is the mechanism the line above depends on. Were the
    // binder to start REPLACING, the assertion above would fail and this one would name why.
    Assert.Contains("some-other-client", options.AllowedClientIds, StringComparer.Ordinal);
  }

  [Fact]
  [Trait("Criterion", "AC-SUB-0037")]
  // ==================================================================================================
  // `AC-SUB-0037`, pasted from the declaration — *"Every monetary column in the package is `decimal(19,4)`
  // and every monetary response field round-trips four decimal places without loss — asserted over the
  // model, not sampled"*
  // ==================================================================================================
  //
  // ⚠⚠⚠ MEASURED FIRST: `HasColumnType("decimal(19,4)")` changed to `decimal(18,2)` on the only monetary
  // column FP-014 has. **All seven suites green.** Money precision in the subscription package was
  // unguarded.
  //
  // ***THE CRITERION DICTATES ITS OWN INSTRUMENT — "ASSERTED OVER THE MODEL, NOT SAMPLED" — AND THAT IS THE
  // WHOLE POINT OF IT.*** A test naming `PlanPrice.Amount` would pass today and say nothing about the
  // second monetary column, which is the one that will be added under delivery pressure. Payroll's
  // `Every_monetary_column_is_decimal_19_4` is the same idiom; this is its Platform-side twin.
  //
  // ⚠⚠ AND THE FLOOR IS HONESTLY WEAK TODAY, WHICH IS SAID RATHER THAN HIDDEN. FP-014 has **exactly one**
  // decimal property, so the monetary floor is 1 — it proves the type filter still selects something, and
  // it cannot prove much more. **The entity and property floors above it are the real controls**: they
  // catch the namespace filter silently matching nothing, which is the way this assertion would go vacuous.
  // *The value is in the columns not yet written.*
  public void Every_monetary_column_in_the_subscription_package_is_decimal_19_4()
  {
    // The same composition the shipping host builds — the context takes the request-context services, so a
    // model read from a hand-rolled `DbContext` would be a model this product never uses.
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton<ICurrentUser, TestRequestContext>();
    services.AddSingleton<ICurrentTenant, TestRequestContext>();
    services.AddSingleton<ICorrelationContext, TestRequestContext>();
    services.AddSingleton<IRequestMetadata, TestRequestContext>();
    services.AddSingleton<IDateTimeProvider, TestRequestContext>();
    services.AddPlatformInfrastructure(CreateConfiguration(new Dictionary<string, string?>()));
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var model = scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Model;

    var entities = model.GetEntityTypes()
      .Where(entity => (entity.ClrType.FullName ?? string.Empty)
        .StartsWith("SSAS.Platform.Domain.Subscriptions", StringComparison.Ordinal))
      .ToArray();
    Assert.True(entities.Length >= 5,
      $"only {entities.Length} subscription entities were found in the model; the namespace filter has " +
      "stopped matching and 'every monetary column is 19,4' would be a claim about nothing.");

    var properties = entities.SelectMany(entity => entity.GetProperties().Select(property => (entity, property))).ToArray();
    Assert.True(properties.Length >= 30,
      $"only {properties.Length} properties across {entities.Length} subscription entities; the walk collapsed.");

    var monetary = properties
      .Where(pair => pair.property.ClrType == typeof(decimal) || pair.property.ClrType == typeof(decimal?))
      .ToArray();
    Assert.True(monetary.Length >= 1,
      $"no decimal property was found across {properties.Length} subscription properties; the type filter " +
      "has stopped matching.");

    // ⚠⚠ TWO CONFIGURATION IDIOMS EXIST IN THIS PRODUCT AND AN ASSERTION IN ONE IS BLIND TO THE OTHER.
    // Payroll configures money with `HasPrecision(19, 4)`, so `GetPrecision()`/`GetScale()` carry it.
    // **Platform configures it with `HasColumnType("decimal(19,4)")`, which leaves both NULL** — my first
    // version asserted on precision alone and reported the correctly-configured column as
    // `decimal(,)`. *A tree-wide money assertion written in either idiom alone would be vacuous over half
    // the model while looking authoritative.* So both are accepted, and a column satisfying NEITHER is the
    // offender.
    var offenders = monetary
      .Where(pair => !IsMoney(pair.property))
      .Select(pair => $"{pair.entity.ClrType.Name}.{pair.property.Name} is " +
        $"precision={pair.property.GetPrecision()?.ToString(CultureInfo.InvariantCulture) ?? "null"} " +
        $"scale={pair.property.GetScale()?.ToString(CultureInfo.InvariantCulture) ?? "null"} " +
        $"columnType={pair.property.GetColumnType() ?? "null"}")
      .ToArray();

    Assert.Empty(offenders);
  }

  private static bool IsMoney(IProperty property) =>
    (property.GetPrecision() == 19 && property.GetScale() == 4) ||
    string.Equals(
      (property.GetColumnType() ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal),
      "decimal(19,4)",
      StringComparison.OrdinalIgnoreCase);

  [Fact]
  [Trait("Criterion", "AC-SUB-0006")]
  // ==================================================================================================
  // `AC-SUB-0006`, pasted — *"The plan tables carry **no `TenantId` column**, and no route reachable on the
  // tenant plane can create, amend or retire a plan"*
  // ==================================================================================================
  //
  // TWO CLAUSES, AND ONLY THE FIRST IS REAL TODAY.
  //
  //   NO `TenantId` COLUMN   this test, over the model. **A plan is shared across tenants — that is the
  //                          entire commercial design — and a `TenantId` column would not merely be
  //                          redundant, it would make per-tenant plan rows EXPRESSIBLE**, which is the
  //                          thing the criterion exists to prevent.
  //   NO TENANT-PLANE ROUTE  ⚠⚠⚠ **VACUOUS: there are no plan routes at all, on either plane.**
  //                          `grep` over `src/` finds no `Map*` for any subscription or plan path. Guarded
  //                          separately in `PlatformRouteInventoryTests` so the vacuity self-revokes.
  //
  // ⚠⚠ AND THAT SECOND VACUITY IS **UNDECLARED**, WHICH IS THE DANGEROUS KIND. `AC-SUB-0008` says so in the
  // document — *"satisfied vacuously as at 2026-08-30"* — and a reader is warned. **This one reads as a
  // enforced separation and is an empty surface**, so a reader who checks that FP-014 keeps plans off the
  // tenant plane finds a criterion, finds no violation, and concludes it is held.
  //
  // ⚠ THE CONTROL IS A KNOWN TENANT-OWNED ENTITY IN THE SAME MODEL. Without it, "no plan entity has a
  // `TenantId` property" is equally satisfied by a property lookup that has stopped working — the same
  // failure as a namespace filter matching nothing, one level down.
  public void Plan_tables_carry_no_tenant_column()
  {
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton<ICurrentUser, TestRequestContext>();
    services.AddSingleton<ICurrentTenant, TestRequestContext>();
    services.AddSingleton<ICorrelationContext, TestRequestContext>();
    services.AddSingleton<IRequestMetadata, TestRequestContext>();
    services.AddSingleton<IDateTimeProvider, TestRequestContext>();
    services.AddPlatformInfrastructure(CreateConfiguration(new Dictionary<string, string?>()));
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var model = scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Model;

    var planEntities = model.GetEntityTypes()
      .Where(entity => PlanTypeNames.Contains(entity.ClrType.Name, StringComparer.Ordinal))
      .ToArray();
    Assert.Equal(PlanTypeNames.Length, planEntities.Length);

    // THE CONTROL: the instrument can find a tenant column when one exists.
    var tenantOwned = model.GetEntityTypes()
      .SingleOrDefault(entity => entity.ClrType.Name == "TenantUser");
    Assert.NotNull(tenantOwned);
    Assert.Contains(tenantOwned.GetProperties(), property => property.Name == "TenantId");

    var offenders = planEntities
      .SelectMany(entity => entity.GetProperties()
        .Where(property => property.Name.Contains("TenantId", StringComparison.Ordinal))
        .Select(property => $"{entity.ClrType.Name}.{property.Name}"))
      .ToArray();

    Assert.Empty(offenders);
  }

  // The plan side of FP-014: shared across tenants by design. `TenantSubscription` and
  // `TenantEntitlementGrant` are deliberately NOT here — those are per-tenant records and carry a tenant.
  private static readonly string[] PlanTypeNames =
    ["SubscriptionPlan", "PlanPrice", "PlanModuleGrant", "PlanLimit"];

  [Fact]
  // ⚠⚠⚠ DELIBERATELY NO `[Trait]`, AND THE MISSING TRAIT IS THE POINT. This test GUARDS a criterion's
  // declared vacuity; it does not WITNESS the criterion. **A trait here would enter the census as
  // coverage** — which is precisely what the guard exists to prevent being recorded.
  //
  // I put one here first, then reported the criterion as *not cited, guarded* in the same breath. ***THE
  // TRAIT AND THE BUCKET CONTRADICTED EACH OTHER, AND THE CENSUS BELIEVED THE TRAIT*** — a numerator that
  // counted the same id as cited AND as not-cited, which is a definitional error rather than a miscount.
  // *A bucket meaning "not cited" cannot be implemented with the thing that means "cited".*
  //
  // Same precedent as the architecture-guard retraction earlier in this loop: **the link is recorded in
  // comments at both ends, with no trait**, and it is worth writing down PRECISELY BECAUSE the coverage
  // claim would be false.
  //
  // ==================================================================================================
  // `AC-SUB-0008`, pasted — *"**No tenant-plane permission name for subscription administration exists in
  // the composed catalog.** The criterion is the absence — there is nothing to grant by mistake.
  // ⚠ **Satisfied vacuously as at 2026-08-30:** the package defines no subscription permissions on
  // **either** plane (all 28 platform names enumerated), **so this is met by there being nothing to
  // separate rather than by the separation being implemented**"*
  // ==================================================================================================
  //
  // ⚠⚠⚠ THIS TEST DOES NOT CITE THE CRITERION AS SATISFIED. **The criterion declares its own vacuity, with
  // a date**, and recording a vacuity as coverage is the failure the whole citation discipline exists to
  // prevent. What this guards is the vacuity itself.
  //
  // ***THE ENUMERATION "ALL 28 PLATFORM NAMES" IS A CLAIM WITH A TIMESTAMP, AND THE DAY SOMEONE ADDS A
  // SUBSCRIPTION PERMISSION THE CRITERION BECOMES LIVE AND NOTHING ANYWHERE WOULD NOTICE.*** Prose cannot
  // revoke itself. This test makes the vacuity SELF-REVOKING: the moment a subscription-shaped permission
  // appears on either plane, it reddens and whoever added it has to read `AC-SUB-0008` and decide.
  //
  // ⚠ A TERM BAN RATHER THAN A FULL MEMBER PIN, DELIBERATELY. Pinning all 28 names would redden on every
  // unrelated permission addition — **a guard whose false positives outnumber its true ones is one somebody
  // switches off**, and this one needs to survive until FP-014 ships. The ban is scoped to the vocabulary
  // the criterion is about, and its matcher is exercised below so it cannot rot into matching nothing.
  public void No_subscription_permission_exists_on_either_plane()
  {
    var catalog = new PlatformPermissionCatalog();
    var names = catalog.All.Select(definition => definition.Name.Value).ToArray();

    Assert.True(names.Length >= 20,
      $"only {names.Length} permissions in the composed catalog; the enumeration collapsed and the ban " +
      "below would be a claim about nothing.");

    // The matcher control: it must match what it is for, and not match what it is not for.
    Assert.True(IsSubscriptionShaped("Platform.Subscriptions.View"));
    Assert.True(IsSubscriptionShaped("Tenant.Billing.Manage"));
    Assert.False(IsSubscriptionShaped("Platform.Support.Administer"));

    Assert.Empty(names.Where(name => IsSubscriptionShaped(name)));
  }

  // The vocabulary `AC-SUB-0008` is about, on EITHER plane — the criterion's own words are "no tenant-plane
  // permission name", and its vacuity note widens that to "on either plane", which is the state being held.
  // Hoisted for CA1861; it is the SUBJECT of the ban rather than incidental data.
  private static readonly string[] SubscriptionVocabulary =
    ["Subscription", "Plan", "Invoice", "Entitlement", "Billing", "Seat"];

  private static bool IsSubscriptionShaped(string name) =>
    SubscriptionVocabulary.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase));

  [Fact]
  public void Platform_persistence_is_module_qualified_scoped_and_uses_one_context_per_scope()
  {
    var services = new ServiceCollection();
    // `DomainEventDispatcher` logs consumer failures (item 173), so the container needs logging —
    // every real host registers it, and without it this composition is not the one that ships.
    services.AddLogging();
    services.AddSingleton<ICurrentUser, TestRequestContext>();
    services.AddSingleton<ICurrentTenant, TestRequestContext>();
    services.AddSingleton<ICorrelationContext, TestRequestContext>();
    services.AddSingleton<IRequestMetadata, TestRequestContext>();
    services.AddSingleton<IDateTimeProvider, TestRequestContext>();
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["ConnectionStrings:Platform"] =
        "Server=localhost;Database=not-opened;Integrated Security=True;TrustServerCertificate=True;Encrypt=False"
    }).Build();

    services.AddPlatformInfrastructure(configuration);

    AssertScoped<PlatformDbContext>(services);
    AssertScoped<IIdentityRepository>(services);
    AssertScoped<ITenantUserRepository>(services);
    AssertScoped<IRoleRepository>(services);
    AssertScoped<IAuthenticationAccountRepository>(services);
    AssertScoped<IAccountActionTokenRepository>(services);
    AssertScoped<ITenantRepository>(services);
    AssertScoped<ITenantLocalizationSettingsRepository>(services);
    AssertScoped<ITenantLocalizationOverrideRepository>(services);
    AssertScoped<ITenantUserReadService>(services);
    AssertScoped<IRoleReadService>(services);
    AssertScoped<ITenantReadService>(services);
    AssertScoped<ITenantAuthenticationEligibilityReadService>(services);
    AssertScoped<IRequestTenantEligibility>(services);
    AssertScoped<ITenantLocalizationHistoryReadService>(services);
    AssertScoped<ITenantLocalizationOverrideReadService>(services);
    AssertScoped<ITenantLocalizationAdministrationReadService>(services);
    AssertScoped<ITenantLocalizationVersionReader>(services);
    AssertScoped<ILocalizationTextResolver>(services);
    AssertScoped<ILocalizationManagementAuditReadiness>(services);
    AssertScoped<ILocalizationCatalogActivationService>(services);
    AssertScoped<IPlatformUnitOfWork>(services);
    AssertScoped<IssueTenantUserInvitationCommandHandler>(services);
    AssertScoped<CompleteInvitationCommandHandler>(services);
    AssertScoped<VerifyPasswordCredentialsCommandHandler>(services);
    AssertScoped<IssuePasswordResetCommandHandler>(services);
    AssertScoped<CompletePasswordResetCommandHandler>(services);
    AssertScoped<CreateTenantCommandHandler>(services);
    AssertScoped<ActivateTenantCommandHandler>(services);
    AssertScoped<SuspendTenantCommandHandler>(services);
    AssertScoped<ReactivateTenantCommandHandler>(services);
    AssertScoped<ArchiveTenantCommandHandler>(services);
    AssertScoped<GetTenantQueryHandler>(services);
    AssertScoped<ListTenantsQueryHandler>(services);
    AssertScoped<GetTenantAuthenticationEligibilityQueryHandler>(services);
    AssertScoped<CreateTenantLocalizationOverrideCommandHandler>(services);
    AssertScoped<UpdateTenantLocalizationOverrideCommandHandler>(services);
    AssertScoped<UndoTenantLocalizationOverrideCommandHandler>(services);
    AssertScoped<RestoreTenantLocalizationDefaultCommandHandler>(services);
    AssertScoped<GetTenantLocalizationHistoryQueryHandler>(services);
    AssertScoped<ListTenantLocalizationResourcesQueryHandler>(services);
    AssertScoped<GetTenantLocalizationResourceQueryHandler>(services);
    AssertScoped<PreviewTenantLocalizationOverrideCommandHandler>(services);
    AssertSingleton<IPasswordHashingService>(services);
    AssertSingleton<IActionTokenService>(services);
    AssertSingleton<IAuthenticationDiagnostics>(services);
    AssertSingleton<ICompromisedPasswordChecker>(services);
    AssertSingleton<IPasswordPolicyValidator>(services);
    AssertSingleton<ILocalizationTenantCache>(services);
    AssertSingleton<ILocalizationDiagnostics>(services);
    Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IUnitOfWork));

    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<IIdentityRepository>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<ITenantUserRepository>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<IRoleRepository>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<IAuthenticationAccountRepository>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<IAccountActionTokenRepository>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<ITenantRepository>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<ITenantUserReadService>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<IRoleReadService>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<ITenantReadService>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<ITenantAuthenticationEligibilityReadService>(), context);
    Assert.Same(
      scope.ServiceProvider.GetRequiredService<IPlatformUnitOfWork>(),
      scope.ServiceProvider.GetRequiredService<IPlatformUnitOfWork>());
  }

  private static void AssertScoped<TService>(IEnumerable<ServiceDescriptor> services)
  {
    Assert.Contains(services, descriptor =>
      descriptor.ServiceType == typeof(TService) && descriptor.Lifetime == ServiceLifetime.Scoped);
  }

  private static void AssertSingleton<TService>(IEnumerable<ServiceDescriptor> services)
  {
    Assert.Contains(services, descriptor =>
      descriptor.ServiceType == typeof(TService) && descriptor.Lifetime == ServiceLifetime.Singleton);
  }

  private static void AssertUsesContext(object service, PlatformDbContext expectedContext)
  {
    var contextField = service.GetType()
      .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
      .Single(field => field.FieldType == typeof(PlatformDbContext));
    Assert.Same(expectedContext, contextField.GetValue(service));
  }

  private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> values)
  {
    var settings = new Dictionary<string, string?>(values, StringComparer.Ordinal)
    {
      ["ConnectionStrings:Platform"] =
        "Server=localhost;Database=not-opened;Integrated Security=True;TrustServerCertificate=True;Encrypt=False"
    };
    return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
  }

  private sealed class TestRequestContext :
    ICurrentUser,
    ICurrentTenant,
    ICorrelationContext,
    IRequestMetadata,
    IDateTimeProvider
  {
    public string? UserId => "actor";
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
    public Guid? TenantId => Guid.NewGuid();
    public string CorrelationId => "correlation";
    public string? RequestId => "request";
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}
