using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.GL.Application.Abstractions;
using SSAS.GL.Application.Accounts;
using SSAS.GL.Application.Calendar;
using SSAS.GL.Application.Journals;
using SSAS.GL.Application.Reads;
using SSAS.GL.Infrastructure.Persistence;

namespace SSAS.GL.Infrastructure;

// GL'S PERSISTENCE COMPOSITION (ADR-012 r1.2).
//
// The Host is the ONE place permitted to see a module's Infrastructure, and module registration is
// EXPLICIT — never discovered by reflection. A module that is not registered here contributes nothing:
// its entities are absent from the tenant model, absent from the migration stream, and absent from
// Shared to Dedicated cutover. The last of those fails silently, which is the whole reason the
// contributor set is a registration rather than a scan.
public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddGlInfrastructure(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    // Singleton, matching HR: the contributor is stateless and deterministic, and it participates in the
    // EF model cache key. A scoped contributor would be constructed per request for a model built once.
    services.AddSingleton<ITenantModelContributor, GlTenantModelContributor>();

    // ---- WRITE-SIDE PORTS. Scoped, because each resolves the tenant's context per request.
    services.AddScoped<IAccountRepository, AccountRepository>();
    services.AddScoped<IFiscalCalendarRepository, FiscalCalendarRepository>();
    services.AddScoped<IJournalDraftRepository, JournalDraftRepository>();
    services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();

    // ---- THE SCOPE RESOLVER AND THE READ SIDE.
    //
    // `IGlScopeResolver` is the ONLY producer of a `GlReadScope`, and `GlReadScope`'s factory is internal to
    // the Application assembly. Registering a second implementation would not widen that: the type still
    // could not be constructed anywhere else.
    services.AddScoped<IGlScopeResolver, GlScopeResolver>();
    services.AddScoped<IGlReadService, GlReadService>();

    // ---- HANDLERS, REGISTERED EXPLICITLY.
    //
    // No assembly scan. A handler that is not named here is not resolvable, and its route fails at
    // composition rather than at the first request — which is the loud failure the Host's own composition
    // test (`EmployeeHostCompositionTests`' GL counterpart) is written to catch.
    services.AddScoped<CreateAccountCommandHandler>();
    services.AddScoped<RenameAccountCommandHandler>();
    services.AddScoped<SetAccountActivationCommandHandler>();

    services.AddScoped<DefineFiscalYearCommandHandler>();
    services.AddScoped<SetFiscalPeriodStateCommandHandler>();

    services.AddScoped<CreateJournalDraftCommandHandler>();
    services.AddScoped<UpdateJournalDraftCommandHandler>();
    services.AddScoped<DiscardJournalDraftCommandHandler>();

    services.AddScoped<PostJournalDraftCommandHandler>();
    services.AddScoped<ReverseJournalCommandHandler>();

    return services;
  }
}
