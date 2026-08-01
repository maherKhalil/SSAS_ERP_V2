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

  [Fact]
  public void Platform_persistence_is_module_qualified_scoped_and_uses_one_context_per_scope()
  {
    var services = new ServiceCollection();
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
    AssertScoped<ITenantUserReadService>(services);
    AssertScoped<IRoleReadService>(services);
    AssertScoped<IPlatformUnitOfWork>(services);
    AssertScoped<IssueTenantUserInvitationCommandHandler>(services);
    AssertScoped<CompleteInvitationCommandHandler>(services);
    AssertScoped<VerifyPasswordCredentialsCommandHandler>(services);
    AssertScoped<IssuePasswordResetCommandHandler>(services);
    AssertScoped<CompletePasswordResetCommandHandler>(services);
    AssertSingleton<IPasswordHashingService>(services);
    AssertSingleton<IActionTokenService>(services);
    AssertSingleton<IAuthenticationDiagnostics>(services);
    AssertSingleton<ICompromisedPasswordChecker>(services);
    AssertSingleton<IPasswordPolicyValidator>(services);
    Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IUnitOfWork));

    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<IIdentityRepository>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<ITenantUserRepository>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<IRoleRepository>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<IAuthenticationAccountRepository>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<IAccountActionTokenRepository>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<ITenantUserReadService>(), context);
    AssertUsesContext(scope.ServiceProvider.GetRequiredService<IRoleReadService>(), context);
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
    public Guid? CompanyId => null;
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
