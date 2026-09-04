using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
