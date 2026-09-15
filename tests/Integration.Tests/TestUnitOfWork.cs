using Microsoft.Extensions.Logging.Abstractions;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// ONE PLACE THE UNIT-OF-WORK CONSTRUCTOR IS SPELLED OUT FOR TESTS (item 175).
// ==================================================================================================
//
// `EfUnitOfWork` gained an `ILogger` so a dispatch failure after a durable write can be recorded instead
// of failing the command. Threading it meant touching every construction in this suite -- 25 of them --
// and the next signature change would have meant the same again.
//
// ⚠ THE LOGGER IS NOT OPTIONAL WITH A DEFAULT, DELIBERATELY. An `ILogger` defaulting to none would let a
// production wiring mistake pass silently and the guard would then swallow failures with nowhere to
// report them -- vacuity bought to shrink a diff. Tests pass `NullLogger` explicitly through here;
// production resolves a real one from the container.
internal static class TestUnitOfWork
{
  public static PlatformUnitOfWork Platform(
    PlatformDbContext context,
    IDomainEventDispatcher dispatcher) =>
    new(context, dispatcher, NullLogger<EfUnitOfWork<PlatformDbContext>>.Instance);

  public static TenantUnitOfWork Tenant(
    ITenantDbContextProvider contextProvider,
    IDomainEventDispatcher dispatcher) =>
    new(contextProvider, dispatcher, NullLogger<EfUnitOfWork<TenantDbContext>>.Instance);
}
