using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Companies;

namespace SSAS.Platform.Infrastructure.RequestContext;

public static class RequestContextServiceCollectionExtensions
{
  public static IServiceCollection AddPlatformRequestContext(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddHttpContextAccessor();
    services.AddScoped<CurrentUser>();
    services.AddScoped<ICurrentUser>(serviceProvider => serviceProvider.GetRequiredService<CurrentUser>());
    services.AddScoped<CurrentTenant>();
    services.AddScoped<ICurrentTenant>(serviceProvider => serviceProvider.GetRequiredService<CurrentTenant>());

    // ---- FP-006C1: CALLER INTENT, AND ONLY INTENT (ADR-025 decision 4).
    //
    // ICompanySelection is the `X-Company-Id` header, parsed. It carries NO authority: it says which company
    // the caller is asking to act within, never which company they may act within. It reads a header and
    // nothing else, so it belongs here — this registration is the request-transport layer.
    //
    // ICurrentCompany — the TRUSTED value — is deliberately NOT registered here. See
    // AddPlatformCompanyContext below for why.
    services.AddScoped<ICompanySelection, RequestedCompanySelection>();
    services.AddScoped<ICurrentAuthenticationSession, CurrentAuthenticationSessionAccessor>();
    services.AddScoped<CorrelationContext>();
    services.AddScoped<ICorrelationContext>(serviceProvider => serviceProvider.GetRequiredService<CorrelationContext>());
    services.AddScoped<IRequestMetadata, RequestMetadata>();
    services.AddSingleton<IDateTimeProvider, UtcDateTimeProvider>();
    return services;
  }

  // ==================================================================================================
  // THE TRUSTED COMPANY CONTEXT — REGISTERED WHERE ITS DEPENDENCIES CAN ACTUALLY BE SATISFIED.
  // ==================================================================================================
  //
  // ---- WHY THIS IS NOT PART OF AddPlatformRequestContext.
  //
  // Everything in that method reads the request: the user, the tenant, the correlation id, the company
  // HEADER. `ICurrentCompany` is different in kind. It is empty until `CurrentCompany.EstablishAsync` runs a
  // five-step live validation, which means it depends on `ICompanyContextResolver`, which depends on
  // `ITenantCompanyAccessResolver`, which reads the platform and tenant databases.
  //
  // Registering it alongside the header accessors made `AddPlatformRequestContext` a method that LOOKED
  // independently usable and silently produced a service graph that could not be built — the failure only
  // appeared at `BuildServiceProvider`, and only for hosts that never called `AddPlatformInfrastructure`.
  // A registration extension must not promise a graph it cannot satisfy, so the company context now lives
  // with the persistence composition that supplies its resolver (FP-006C5).
  //
  // ---- WHY THE CHAIN WAS NOT MADE OPTIONAL INSTEAD.
  //
  // Giving `CurrentCompany` an optional resolver would have turned a composition error into a SILENT one:
  // every company-owned operation would refuse at runtime because the resolver was missing, which is
  // indistinguishable, from the outside, from the caller genuinely lacking company access. A host that
  // forgets its persistence registration should fail loudly at startup, not quietly deny every user.
  //
  // TryAdd, so a host that composes this more than once — directly and through the platform registration —
  // gets one registration and one lifetime rather than a last-one-wins surprise.
  public static IServiceCollection AddPlatformCompanyContext(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.TryAddScoped<CurrentCompany>();
    services.TryAddScoped<ICurrentCompany>(serviceProvider => serviceProvider.GetRequiredService<CurrentCompany>());

    return services;
  }
}
