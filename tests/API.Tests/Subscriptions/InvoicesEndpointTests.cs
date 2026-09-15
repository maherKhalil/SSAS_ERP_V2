using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Platform.API.Subscriptions;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Subscriptions.Invoices;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.Subscriptions;

public sealed class InvoicesApiEndpointGroup { }

[Collection(nameof(InvoicesApiEndpointGroup))]
public sealed class InvoicesEndpointTests : IAsyncLifetime
{
  private const string Issuer = "https://invoices.tests";
  private const string Audience = "invoices-tests";
  private const string RoutePrefix = "/api/platform/invoices";
  private static readonly Guid TenantId = Guid.Parse("b1c2d3e4-f5a6-7890-abcd-ef1234567890");
  private static readonly Guid KnownInvoiceId = Guid.Parse("11111111-2222-3333-4444-555555555555");

  private WebApplication? application;
  private HttpClient? client;
  private StubInvoiceRepository repository = new();
  private StubInvoiceQueries invoiceQueries = new();
  private StubPlatformUnitOfWork unitOfWork = new();

  // ==============================================================================================
  // GET /api/platform/invoices
  // ==============================================================================================

  [Fact]
  public async Task GetInvoices_without_token_returns_401()
  {
    var response = await Client.SendAsync(Get(RoutePrefix, null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task GetInvoices_without_permission_returns_403()
  {
    var response = await Client.SendAsync(Get(RoutePrefix, AdministerToken()));
    var content = await response.Content.ReadAsStringAsync();
    Assert.True(HttpStatusCode.Forbidden == response.StatusCode, $"Expected 403, got {response.StatusCode}. Content: {content}");
  }

  [Fact]
  public async Task GetInvoices_authorized_returns_200()
  {
    var response = await Client.SendAsync(Get(RoutePrefix, ViewToken()));
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  // ==============================================================================================
  // GET /api/platform/invoices/{invoiceId}
  // ==============================================================================================

  [Fact]
  public async Task GetInvoiceById_without_token_returns_401()
  {
    var response = await Client.SendAsync(Get($"{RoutePrefix}/{KnownInvoiceId}", null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task GetInvoiceById_authorized_returns_200()
  {
    var response = await Client.SendAsync(Get($"{RoutePrefix}/{KnownInvoiceId}", ViewToken()));
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  // ==============================================================================================
  // GET /api/platform/tenants/{tenantId}/invoices
  // ==============================================================================================

  [Fact]
  public async Task GetTenantInvoices_without_token_returns_401()
  {
    var response = await Client.SendAsync(Get($"/api/platform/tenants/{TenantId}/invoices", null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task GetTenantInvoices_authorized_returns_200()
  {
    var response = await Client.SendAsync(Get($"/api/platform/tenants/{TenantId}/invoices", ViewToken()));
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  // ==============================================================================================
  // POST /api/platform/invoices
  // ==============================================================================================

  [Fact]
  public async Task CreateInvoice_without_token_returns_401()
  {
    var response = await Client.SendAsync(Post(RoutePrefix, ValidCreateBody(), null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task CreateInvoice_without_administer_permission_returns_403()
  {
    var response = await Client.SendAsync(Post(RoutePrefix, ValidCreateBody(), ViewToken()));
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task CreateInvoice_authorized_returns_201()
  {
    var response = await Client.SendAsync(Post(RoutePrefix, ValidCreateBody(), AdministerToken()));
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.True(repository.AddCalled);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task CreateInvoice_missing_required_field_returns_400()
  {
    // Missing currencyCode
    var body = $"{{\"tenantId\":\"{TenantId}\",\"issuedUtc\":\"2026-01-01T00:00:00Z\"}}";
    var response = await Client.SendAsync(Post(RoutePrefix, body, AdministerToken()));
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ==============================================================================================
  // PUT /api/platform/invoices/{invoiceId}
  // ==============================================================================================

  [Fact]
  public async Task UpdateInvoice_without_token_returns_401()
  {
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownInvoiceId}", ValidUpdateBody(), null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task UpdateInvoice_authorized_on_draft_returns_204()
  {
    repository.SeedDraftInvoice(KnownInvoiceId);
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownInvoiceId}", ValidUpdateBody(), AdministerToken()));
    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task UpdateInvoice_on_issued_invoice_returns_409()
  {
    repository.SeedIssuedInvoice(KnownInvoiceId);
    var response = await Client.SendAsync(Put($"{RoutePrefix}/{KnownInvoiceId}", ValidUpdateBody(), AdministerToken()));
    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
  }

  // ==============================================================================================
  // POST /api/platform/invoices/{invoiceId}/issue
  // ==============================================================================================

  [Fact]
  public async Task IssueInvoice_without_token_returns_401()
  {
    var response = await Client.SendAsync(Post($"{RoutePrefix}/{KnownInvoiceId}/issue", ValidIssueBody(), null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task IssueInvoice_authorized_on_draft_returns_204()
  {
    repository.SeedDraftInvoice(KnownInvoiceId);
    var response = await Client.SendAsync(Post($"{RoutePrefix}/{KnownInvoiceId}/issue", ValidIssueBody(), AdministerToken()));
    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task IssueInvoice_on_already_issued_returns_409()
  {
    repository.SeedIssuedInvoice(KnownInvoiceId);
    var response = await Client.SendAsync(Post($"{RoutePrefix}/{KnownInvoiceId}/issue", ValidIssueBody(), AdministerToken()));
    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
  }

  // ==============================================================================================
  // POST /api/platform/invoices/{invoiceId}/void
  // ==============================================================================================

  [Fact]
  public async Task VoidInvoice_without_token_returns_401()
  {
    var response = await Client.SendAsync(Post($"{RoutePrefix}/{KnownInvoiceId}/void", "{}", null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task VoidInvoice_authorized_on_draft_returns_204()
  {
    repository.SeedDraftInvoice(KnownInvoiceId);
    var response = await Client.SendAsync(Post($"{RoutePrefix}/{KnownInvoiceId}/void", "{}", AdministerToken()));
    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  // ==============================================================================================
  // GET /api/platform/invoices/{invoiceId}/attempts
  // ==============================================================================================

  [Fact]
  public async Task GetInvoiceAttempts_without_token_returns_401()
  {
    var response = await Client.SendAsync(Get($"{RoutePrefix}/{KnownInvoiceId}/attempts", null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task GetInvoiceAttempts_authorized_returns_200()
  {
    var response = await Client.SendAsync(Get($"{RoutePrefix}/{KnownInvoiceId}/attempts", ViewToken()));
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  // ==============================================================================================
  // Lifecycle
  // ==============================================================================================

  public async Task InitializeAsync()
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
    builder.WebHost.UseTestServer();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["Jwt:Issuer"] = Issuer,
      ["Jwt:Audience"] = Audience,
      ["Jwt:ClockSkewSeconds"] = "30"
    });

    repository = new StubInvoiceRepository();
    invoiceQueries = new StubInvoiceQueries();
    unitOfWork = new StubPlatformUnitOfWork();

    builder.Services
      .AddPlatformRequestContext()
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostProblemDetails();

    builder.Services.AddSingleton<ITenantAuthenticationEligibilityReadService>(new ActiveTenantEligibility());
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();
    builder.Services.AddSingleton<ISubscriptionInvoiceRepository>(repository);
    builder.Services.AddSingleton<ISubscriptionInvoiceQueries>(invoiceQueries);
    builder.Services.AddSingleton<IPlatformUnitOfWork>(unitOfWork);

    builder.Services.AddScoped<InvoicesCommandHandler>();
    builder.Services.AddScoped<InvoicesQueryHandler>();

    application = builder.Build();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapPlatformInvoicesEndpoints();

    await application.StartAsync();
    client = application.GetTestClient();
  }

  public async Task DisposeAsync()
  {
    client?.Dispose();
    if (application is not null) await application.DisposeAsync();
  }

  // ==============================================================================================
  // Helpers
  // ==============================================================================================

  private HttpClient Client => client ?? throw new InvalidOperationException("Test host has not started.");

  private static HttpRequestMessage Get(string path, string? token)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    if (token is not null) request.Headers.Authorization = new("Bearer", token);
    return request;
  }

  private static HttpRequestMessage Post(string path, string? body, string? token)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, path);
    if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
    if (token is not null) request.Headers.Authorization = new("Bearer", token);
    return request;
  }

  private static HttpRequestMessage Put(string path, string? body, string? token)
  {
    var request = new HttpRequestMessage(HttpMethod.Put, path);
    if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
    if (token is not null) request.Headers.Authorization = new("Bearer", token);
    return request;
  }

  private string ViewToken() => Token(
    new Claim(JwtClaimTypes.SecurityPlane, "platform"),
    new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ViewInvoices));

  private string AdministerToken() => Token(
    new Claim(JwtClaimTypes.SecurityPlane, "platform"),
    new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.AdministerInvoices));

  private string Token(params Claim[] claims)
  {
    var keyProvider = application?.Services.GetRequiredService<ISigningKeyProvider>()
      ?? throw new InvalidOperationException("The test application is unavailable.");
    var credentials = new SigningCredentials(keyProvider.Snapshot.ActiveSigningKey, SecurityAlgorithms.RsaSha256);
    var now = DateTimeOffset.UtcNow;

    var requiredClaims = new List<Claim>
    {
      new Claim(JwtClaimTypes.Subject, "invoice-admin"),
      new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N")),
      new Claim("iat", now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
      new Claim(JwtClaimTypes.IdentityId, "1"),
      new Claim(JwtClaimTypes.SessionId, "3"),
      new Claim(JwtClaimTypes.ClientId, "ssas-erp-web"),
      new Claim(JwtClaimTypes.SecurityVersion, "1")
    };

    var token = new JwtSecurityToken(
      issuer: Issuer, audience: Audience, claims: requiredClaims.Concat(claims),
      notBefore: now.AddMinutes(-1).UtcDateTime, expires: now.AddMinutes(5).UtcDateTime,
      signingCredentials: credentials);
    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  private static string ValidCreateBody()
    => $"{{\"tenantId\":\"{TenantId}\",\"currencyCode\":\"USD\",\"issuedUtc\":\"2026-01-01T00:00:00Z\"}}";

  private static string ValidUpdateBody()
    => "{\"currencyCode\":\"EUR\",\"issuedUtc\":\"2026-06-01T00:00:00Z\"}";

  private static string ValidIssueBody()
    => "{\"invoiceNumber\":\"INV-2026-001\"}";

  // ==============================================================================================
  // Stubs
  // ==============================================================================================

  private sealed class StubInvoiceRepository : ISubscriptionInvoiceRepository
  {
    private SubscriptionInvoice? _seeded;
    public bool AddCalled { get; private set; }

    public void SeedDraftInvoice(Guid id)
    {
      _seeded = SubscriptionInvoice.CreateDraft(Guid.NewGuid(), "USD", DateTimeOffset.UtcNow).Value;
      typeof(SSAS.BuildingBlocks.Domain.Entity<Guid>)
        .GetField("<Id>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .SetValue(_seeded, id);
    }

    public void SeedIssuedInvoice(Guid id)
    {
      SeedDraftInvoice(id);
      _seeded!.Issue("INV-SEED");
    }

    public Task<SubscriptionInvoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
      => Task.FromResult(_seeded);

    public void Add(SubscriptionInvoice invoice) { AddCalled = true; }
  }

  private sealed class StubInvoiceQueries : ISubscriptionInvoiceQueries
  {
    public Task<Result<IReadOnlyCollection<InvoiceDto>>> GetInvoicesAsync(CancellationToken cancellationToken = default)
      => Task.FromResult(Result.Success<IReadOnlyCollection<InvoiceDto>>(new List<InvoiceDto>()));

    public Task<Result<InvoiceDto>> GetInvoiceByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
      var dto = new InvoiceDto(invoiceId, null, Guid.NewGuid(), "USD", DateTimeOffset.UtcNow, SubscriptionInvoiceState.Draft, new List<InvoiceLineDto>());
      return Task.FromResult(Result.Success(dto));
    }

    public Task<Result<IReadOnlyCollection<InvoiceDto>>> GetTenantInvoicesAsync(Guid tenantId, CancellationToken cancellationToken = default)
      => Task.FromResult(Result.Success<IReadOnlyCollection<InvoiceDto>>(new List<InvoiceDto>()));

    public Task<Result<IReadOnlyCollection<PaymentAttemptDto>>> GetInvoiceAttemptsAsync(Guid invoiceId, CancellationToken cancellationToken = default)
      => Task.FromResult(Result.Success<IReadOnlyCollection<PaymentAttemptDto>>(new List<PaymentAttemptDto>()));
  }

  private sealed class StubPlatformUnitOfWork : IPlatformUnitOfWork
  {
    public int SaveCount { get; private set; }
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveCount++;
      return Task.FromResult(Result.Success(1));
    }
    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
      => throw new NotSupportedException();
  }

  private sealed class ActiveTenantEligibility : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default)
      => Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default)
      => GetEligibilityAsync(tenantId, cancellationToken);
  }
}
