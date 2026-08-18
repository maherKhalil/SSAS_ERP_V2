using Microsoft.AspNetCore.Http;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Companies;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.Platform.Tests.Companies;

// THE COMPANY REQUEST CONTEXT (FP-006C1, ADR-025 decisions 2, 3 and 4).
//
// These cover the parts that are decidable without a database: how caller intent is read, and how the
// five-step validation behaves when its preconditions are absent. The database-backed steps — exists,
// belongs to this tenant, is Active, is authorized — are proven against real SQL Server, because an
// in-memory provider would agree with all of them and prove none.
public sealed class CompanyRequestContextTests
{
  private static readonly Guid Tenant = Guid.Parse("6f7c9f2a-1f31-4f7c-9f9e-8a1c2b3d4e5f");
  private static readonly Guid Company = Guid.Parse("11112222-3333-4444-5555-666677778888");

  // ---- A WELL-FORMED HEADER IS CARRIED THROUGH AS INTENT, and nothing more happens here.
  [Fact]
  public void A_well_formed_company_header_is_read_as_intent()
  {
    var selection = SelectionWith(Company.ToString("D"));

    var requested = selection.Requested;

    Assert.True(requested.IsSuccess);
    Assert.Equal(Company, requested.Value);
  }

  // ---- NO HEADER IS NOT AN ERROR. Tenant-global and branch-only work needs no company; the write boundary
  // is what turns the absence into a refusal, and only for company-owned data.
  [Fact]
  public void No_company_header_is_a_successful_absence_rather_than_a_failure()
  {
    var requested = SelectionWith(null).Requested;

    Assert.True(requested.IsSuccess);
    Assert.Null(requested.Value);
  }

  // ---- MALFORMED IS A SYNTAX FAILURE, and is safe to distinguish because it discloses nothing about any
  // company. Every AUTHORIZATION outcome collapses into one generic refusal instead.
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("not-a-guid")]
  [InlineData("00000000-0000-0000-0000-000000000000")]
  // Braced, parenthesised and hyphenless forms are rejected too: accepting them would give one company
  // several accepted spellings, and anything comparing the raw header text would see them as different.
  [InlineData("{11112222-3333-4444-5555-666677778888}")]
  [InlineData("11112222333344445555666677778888")]
  public void A_malformed_company_header_is_a_syntax_failure(string raw)
  {
    var requested = SelectionWith(raw).Requested;

    Assert.True(requested.IsFailure);
    Assert.Equal(CompanyAccessErrors.InvalidSelectionFormat.Code, requested.Error.Code);
  }

  // ---- MORE THAN ONE HEADER VALUE IS MALFORMED. Taking the first would let a caller smuggle a second
  // selection past anything that inspected only one of them.
  [Fact]
  public void Two_company_headers_are_refused_rather_than_resolved_to_the_first()
  {
    var context = new DefaultHttpContext();
    context.Request.Headers[RequestedCompanySelection.HeaderName] =
      new Microsoft.Extensions.Primitives.StringValues([Company.ToString("D"), Guid.NewGuid().ToString("D")]);

    var requested = new RequestedCompanySelection(Accessor(context)).Requested;

    Assert.True(requested.IsFailure);
    Assert.Equal(CompanyAccessErrors.InvalidSelectionFormat.Code, requested.Error.Code);
  }

  // ---- NO TRUSTED TENANT MEANS NO COMPANY CONTEXT. A company is only meaningful inside a tenant, and
  // inferring the tenant from the company would let a caller pick its own tenant by picking a company.
  [Fact]
  public async Task Without_a_trusted_tenant_no_company_context_is_established()
  {
    var resolver = new CompanyContextResolver(
      new StubTenant(null), new StubAccessResolver(), SelectionWith(Company.ToString("D")), Session());

    var resolved = await resolver.ResolveTrustedCompanyAsync();

    Assert.True(resolved.IsFailure);
    Assert.Equal(CompanyAccessErrors.ContextRequired.Code, resolved.Error.Code);
  }

  // ---- NO SIGNED-IN USER MEANS NO COMPANY CONTEXT. Background and maintenance compositions have no
  // session; there is nobody to authorize, so there is no company. Absence refuses — it never permits.
  [Fact]
  public async Task Without_a_session_no_company_context_is_established()
  {
    var resolver = new CompanyContextResolver(
      new StubTenant(Tenant), new StubAccessResolver(), SelectionWith(Company.ToString("D")));

    var resolved = await resolver.ResolveTrustedCompanyAsync();

    Assert.True(resolved.IsFailure);
    Assert.Equal(CompanyAccessErrors.ContextRequired.Code, resolved.Error.Code);
  }

  // ---- A SESSION FOR ANOTHER TENANT IS NOT A SESSION HERE. Otherwise a context routed to one tenant could
  // authorize a company using a user belonging to another.
  [Fact]
  public async Task A_session_belonging_to_another_tenant_establishes_nothing()
  {
    var resolver = new CompanyContextResolver(
      new StubTenant(Tenant),
      new StubAccessResolver(),
      SelectionWith(Company.ToString("D")),
      Session(Guid.NewGuid()));

    var resolved = await resolver.ResolveTrustedCompanyAsync();

    Assert.True(resolved.IsFailure);
    Assert.Equal(CompanyAccessErrors.ContextRequired.Code, resolved.Error.Code);
  }

  // ---- NO SELECTION MEANS "CHOOSE ONE", which is a different answer from "your context is broken".
  [Fact]
  public async Task Without_a_selection_the_caller_is_told_to_select_a_company()
  {
    var resolver = new CompanyContextResolver(
      new StubTenant(Tenant), new StubAccessResolver(), SelectionWith(null), Session());

    var resolved = await resolver.ResolveTrustedCompanyAsync();

    Assert.True(resolved.IsFailure);
    Assert.Equal(CompanyAccessErrors.SelectionRequired.Code, resolved.Error.Code);
  }

  // ---- THE SYNTAX FAILURE SURVIVES TO THE CALLER unchanged, rather than being flattened into the generic
  // authorization refusal. It says nothing about any company, so it is safe and more useful.
  [Fact]
  public async Task A_malformed_selection_is_reported_as_a_syntax_failure_not_an_authorization_refusal()
  {
    var resolver = new CompanyContextResolver(
      new StubTenant(Tenant), new StubAccessResolver(), SelectionWith("nonsense"), Session());

    var resolved = await resolver.ResolveTrustedCompanyAsync();

    Assert.True(resolved.IsFailure);
    Assert.Equal(CompanyAccessErrors.InvalidSelectionFormat.Code, resolved.Error.Code);
  }

  // ---- THE HEADER ALONE ESTABLISHES NOTHING. A perfectly well-formed identifier that the resolver refuses
  // yields no company context — which is the whole point of ADR-025 decision 4.
  [Fact]
  public async Task A_well_formed_but_unauthorized_company_establishes_no_context()
  {
    var resolver = new CompanyContextResolver(
      new StubTenant(Tenant),
      new StubAccessResolver(authorize: false),
      SelectionWith(Company.ToString("D")),
      Session());

    var resolved = await resolver.ResolveTrustedCompanyAsync();

    Assert.True(resolved.IsFailure);
    Assert.Equal(CompanyAccessErrors.InvalidSelection.Code, resolved.Error.Code);
  }

  // ---- AND WHEN EVERY STEP PASSES, the company becomes the trusted one — and only then.
  [Fact]
  public async Task An_authorized_company_becomes_the_trusted_company()
  {
    var resolver = new CompanyContextResolver(
      new StubTenant(Tenant), new StubAccessResolver(), SelectionWith(Company.ToString("D")), Session());

    var resolved = await resolver.ResolveTrustedCompanyAsync();

    Assert.True(resolved.IsSuccess);
    Assert.Equal(Company, resolved.Value);
  }

  // ---- ICurrentCompany STARTS EMPTY AND IS FILLED ONLY BY VALIDATION.
  [Fact]
  public async Task The_current_company_is_empty_until_validation_establishes_it()
  {
    var current = new CurrentCompany(new StubContextResolver(Company));

    Assert.Null(current.CompanyId);

    var established = await current.EstablishAsync();

    Assert.True(established.IsSuccess);
    Assert.Equal(Company, current.CompanyId);
  }

  // ---- AND A LATER REFUSAL CLEARS IT rather than leaving a stale company readable. A revocation observed
  // mid-request must not be survivable by anything that already asked.
  [Fact]
  public async Task A_refusal_clears_a_previously_established_company()
  {
    var contextResolver = new StubContextResolver(Company);
    var current = new CurrentCompany(contextResolver);

    Assert.True((await current.EstablishAsync()).IsSuccess);
    Assert.Equal(Company, current.CompanyId);

    contextResolver.Revoke();

    var second = await current.EstablishAsync();

    Assert.True(second.IsFailure);
    Assert.Null(current.CompanyId);
  }

  // ---- THE WRITE AUTHORIZER REFUSES WHEN THE TENANT IT IS ASKED ABOUT IS NOT THE TRUSTED ONE.
  //
  // Otherwise a context routed to one tenant could stamp rows with a company authorized inside another.
  [Fact]
  public async Task The_write_authorizer_refuses_a_tenant_other_than_the_trusted_one()
  {
    var authorizer = new CompanyWriteAuthorizer(
      new StubContextResolver(Company), new StubTenant(Tenant));

    var authorized = await authorizer.AuthorizeCurrentCompanyAsync(Guid.NewGuid());

    Assert.True(authorized.IsFailure);
    Assert.Equal(CompanyAccessErrors.ContextRequired.Code, authorized.Error.Code);
  }

  // ---- IT RE-ASKS THE VALIDATION ON EVERY CALL rather than reusing an earlier answer. This is the
  // property that makes an established company non-authoritative at save time.
  [Fact]
  public async Task The_write_authorizer_re_asks_the_validation_on_every_call()
  {
    var contextResolver = new StubContextResolver(Company);
    var authorizer = new CompanyWriteAuthorizer(contextResolver, new StubTenant(Tenant));

    Assert.True((await authorizer.AuthorizeCurrentCompanyAsync(Tenant)).IsSuccess);
    Assert.Equal(1, contextResolver.Calls);

    contextResolver.Revoke();

    var second = await authorizer.AuthorizeCurrentCompanyAsync(Tenant);

    Assert.True(second.IsFailure);
    Assert.Equal(2, contextResolver.Calls);
  }

  // ---- UserCompanyAccess REFUSES AN UNUSABLE ASSIGNMENT rather than persisting one that authorizes
  // nothing and cannot be found again.
  [Theory]
  [InlineData(false, 1, true)]
  [InlineData(true, 0, true)]
  [InlineData(true, 1, false)]
  public void An_unusable_company_assignment_is_refused(bool hasTenant, long tenantUserId, bool hasCompany)
  {
    var created = UserCompanyAccess.Create(
      hasTenant ? Tenant : Guid.Empty, tenantUserId, hasCompany ? Company : Guid.Empty);

    Assert.True(created.IsFailure);
    Assert.Equal(CompanyAccessErrors.AssignmentInvalid.Code, created.Error.Code);
  }

  [Fact]
  public void A_valid_company_assignment_carries_its_three_identifiers()
  {
    var created = UserCompanyAccess.Create(Tenant, 42, Company);

    Assert.True(created.IsSuccess);
    Assert.Equal(Tenant, created.Value.TenantId);
    Assert.Equal(42, created.Value.TenantUserId);
    Assert.Equal(Company, created.Value.CompanyId);
  }

  private static RequestedCompanySelection SelectionWith(string? header)
  {
    var context = new DefaultHttpContext();
    if (header is not null)
    {
      context.Request.Headers[RequestedCompanySelection.HeaderName] = header;
    }

    return new RequestedCompanySelection(Accessor(context));
  }

  private static HttpContextAccessor Accessor(HttpContext context) =>
    new HttpContextAccessor { HttpContext = context };

  private static StubSession Session(Guid? tenantId = null) =>
    new StubSession(tenantId ?? Tenant);

  private sealed class StubTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId => tenantId;
  }

  private sealed class StubSession(Guid tenantId) : ICurrentAuthenticationSession
  {
    public CurrentAuthenticationSession? Value => new(
      1, tenantId, 7, 1, AuthenticationClientId.Create("web").Value, 1);
  }

  private sealed class StubAccessResolver(bool authorize = true) : ITenantCompanyAccessResolver
  {
    public Task<Result<IReadOnlyList<CompanyAccessSummary>>> GetPermittedCompaniesAsync(
      Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success<IReadOnlyList<CompanyAccessSummary>>([]));

    public Task<Result> AuthorizeCompanyAsync(
      Guid tenantId, long tenantUserId, Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(authorize ? Result.Success() : Result.Failure(CompanyAccessErrors.InvalidSelection));
  }

  private sealed class StubContextResolver(Guid companyId) : ICompanyContextResolver
  {
    private bool revoked;

    public int Calls { get; private set; }

    public void Revoke() => revoked = true;

    public Task<Result<Guid>> ResolveTrustedCompanyAsync(CancellationToken cancellationToken = default)
    {
      Calls++;
      return Task.FromResult(revoked
        ? Result.Failure<Guid>(CompanyAccessErrors.InvalidSelection)
        : Result.Success(companyId));
    }
  }
}
