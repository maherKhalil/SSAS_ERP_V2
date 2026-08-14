using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.TenantStorage;

// Decides whether normal ERP traffic may be served against a routed tenant database (ADR-018).
//
// This is the component that stops a request reaching a database the application cannot correctly read or
// write. It is deliberately SEPARATE from the resolver: the resolver answers "which database", and
// migration tooling legitimately needs routes to databases that are not servable. Gating belongs to the
// request path only.
//
// It never falls back. A denial is a denial; there is no other database to try (ADR-017).
public interface ITenantDatabaseTrafficGate
{
  // Returns success when traffic may proceed, or a controlled TenantStorage.* failure describing why not.
  // The failure codes are operator-meaningful and safe to surface: none contains endpoint, credential or
  // infrastructure detail.
  Result Evaluate(TenantDatabaseRoute route, DateTimeOffset nowUtc);
}
