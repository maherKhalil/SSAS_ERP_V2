using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Resolves a trusted BackupDestinationKey to a physical directory (ADR-022 §11, compliance rule 23).
//
// THIS IS THE TRUST BOUNDARY for backup destinations. `BACKUP DATABASE` writes a complete copy of the
// database wherever it is told, so an attacker able to influence the destination would have exfiltration
// without reading a single row through the application. The key is the only input; resolution happens here,
// against configuration, entirely inside Infrastructure, and an unknown key fails closed.
//
// Nothing above Infrastructure ever sees the resolved value — the descriptor below is internal, and the
// Application layer's request carries only the key.
internal interface ITenantDatabaseBackupDestinationResolver
{
  Result<TenantDatabaseBackupDestination> Resolve(string? destinationKey);
}

// A resolved destination. Carries a location and NO CREDENTIAL: V1 destinations are filesystem or UNC
// directories reached by the SQL Server service identity's own Windows authentication, so there is no
// secret to carry and none may be added without revisiting ADR-022 §11.
internal sealed record TenantDatabaseBackupDestination(string Key, string DirectoryPath);

internal sealed class TenantDatabaseBackupDestinationResolver(IOptions<TenantStorageOptions> optionsAccessor)
  : ITenantDatabaseBackupDestinationResolver
{
  public Result<TenantDatabaseBackupDestination> Resolve(string? destinationKey)
  {
    if (string.IsNullOrWhiteSpace(destinationKey))
    {
      return Result.Failure<TenantDatabaseBackupDestination>(TenantStorageErrors.BackupDestinationKeyRequired);
    }

    // Exact, ordinal lookup. No default destination, no near-match, no "first configured" — absence is a
    // failure, for the same reason an unknown ServerKey is (ADR-017 "No automatic fallback").
    if (!optionsAccessor.Value.BackupDestinations.TryGetValue(destinationKey, out var destination) ||
      string.IsNullOrWhiteSpace(destination.DirectoryPath))
    {
      return Result.Failure<TenantDatabaseBackupDestination>(TenantStorageErrors.BackupDestinationNotConfigured);
    }

    return Result.Success(new TenantDatabaseBackupDestination(destinationKey, destination.DirectoryPath));
  }
}
