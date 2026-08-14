using Microsoft.Data.SqlClient;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Turns trusted route metadata into a trusted physical connection (ADR-017).
//
// Deliberately Infrastructure-only, and deliberately NOT the component that resolves assignments: the
// resolver answers "which database", this answers "how do I reach it". Keeping them apart is what allows
// the route object to stay free of secrets while credential material never leaves this layer.
//
// It returns a SqlConnection rather than a connection string so credentialed strings do not travel through
// the Application layer, where they could reach logs or diagnostics.
public interface ITenantDatabaseConnectionFactory
{
  // Creates (but does not open) a connection to the route's database. Fails closed when the route's
  // ServerKey is absent from trusted configuration — it never falls back to the Platform connection.
  Result<SqlConnection> Create(TenantDatabaseRoute route);

  // Same trusted construction, addressed by PHYSICAL database rather than by tenant route (ADR-018).
  // Schema health and migration operate per physical database and have no tenant in hand, so they need
  // this overload — but they must NOT get a second, weaker connection path. Both overloads run the same
  // exact-ordinal ServerKey lookup and the same CustomerManaged refusal.
  Result<SqlConnection> Create(TenantDatabaseConnectionTarget target);
}

// A physical database addressed directly: the minimum trusted metadata needed to reach it. Carries no
// credential and no endpoint — ServerKey remains a lookup key into trusted configuration.
public sealed record TenantDatabaseConnectionTarget(
  string ServerKey,
  string DatabaseName,
  TenantDatabaseHostingMode HostingMode);
