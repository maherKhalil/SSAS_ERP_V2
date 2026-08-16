using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure;

namespace SSAS.TestSupport.VerificationHost;

// A SEPARATE OS PROCESS that drives the real restore-verification path, so a test can kill it.
//
// WHY THIS EXISTS AT ALL. ADR-022's LOW-C gate asks what happens to a durable verification run, to SQL
// Server, and to the reserved verification database when the application process holding the operation
// DISAPPEARS. Every cheaper approximation answers a different question: cancelling a token, throwing, or
// disposing a scope all run the handled-failure path the real crash never reaches, and `KILL <spid>` is the
// server terminating the client rather than the client vanishing. The only faithful experiment is a real
// process that a parent can terminate without warning, which is what this is.
//
// IT IS NOT A PRODUCTION HOST. There is no endpoint, no debug switch and no pause hook: the composition
// below is the production `AddPlatformInfrastructure` graph, resolved from a plain ServiceCollection so the
// registered hosted services — including the verification scheduler — are never started. The parent drives
// every step explicitly.
internal static class Program
{
  private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

  // The stdout protocol the parent synchronises on. Deliberately line-oriented and boring.
  private const string AdmittedPrefix = "ADMITTED ";
  private const string WaitingLine = "WAITING";
  private const string RestoringLine = "RESTORING";

  private static async Task<int> Main(string[] args)
  {
    if (args.Length != 1)
    {
      await Console.Error.WriteLineAsync("usage: SSAS.TestSupport.VerificationHost <configuration.json>");
      return 64;
    }

    var configuration = JsonSerializer.Deserialize<HostConfiguration>(
      await File.ReadAllTextAsync(args[0]), Json);
    if (configuration is null)
    {
      await Console.Error.WriteLineAsync("the host configuration could not be read");
      return 64;
    }

    await using var provider = BuildProvider(configuration);

    long verificationRunId;
    await using (var admissionScope = provider.CreateAsyncScope())
    {
      var runStore = admissionScope.ServiceProvider
        .GetRequiredService<ITenantDatabaseRestoreVerificationRunStore>();

      // The REAL admission path, including its transaction, rechecks and name reservation.
      var admitted = await runStore.TryAdmitAsync(new TenantDatabaseRestoreVerificationAdmissionRequest(
        configuration.TenantDatabaseId,
        configuration.SourceBackupRunId,
        configuration.ExpectedPreviousSuccessfulVerificationRunId,
        TenantDatabaseRestoreDepth.Full,
        configuration.RestoreServerKey,
        configuration.Actor));

      if (admitted.IsFailure)
      {
        await Console.Error.WriteLineAsync("admission refused: " + admitted.Error.Code);
        return 65;
      }

      verificationRunId = admitted.Value;
    }

    Console.WriteLine(string.Create(
      CultureInfo.InvariantCulture, $"{AdmittedPrefix}{verificationRunId}"));

    if (string.Equals(configuration.Mode, "AdmitAndWait", StringComparison.Ordinal))
    {
      // Durably Admitted, no restore started, and now doing nothing at all until the parent kills it. The
      // wait is unbounded on purpose: a timeout would make the process capable of ending on its own, and the
      // experiment requires that the only way it stops is termination.
      Console.WriteLine(WaitingLine);
      await Task.Delay(Timeout.Infinite);
      return 0;
    }

    Console.WriteLine(RestoringLine);

    await using var executionScope = provider.CreateAsyncScope();
    var executor = executionScope.ServiceProvider
      .GetRequiredService<ITenantDatabaseRestoreVerificationExecutor>();

    // The real D7 executor: lifecycle CAS to Restoring, then the D6 provider's RESTORE sequence. The parent
    // is expected to terminate this process somewhere inside that RESTORE, so the lines below are reached
    // only when the restore outran the observer — which the parent treats as an inconclusive trial.
    var result = await executor.ExecuteAsync(
      configuration.TenantDatabaseId, verificationRunId, TenantDatabaseRestoreDepth.Full);

    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
      $"COMPLETED {(result.IsSuccess ? result.Value.Status.ToString() : result.Error.Code)}"));
    return 0;
  }

  private static ServiceProvider BuildProvider(HostConfiguration configuration)
  {
    var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
      ["ConnectionStrings:Platform"] = configuration.PlatformConnectionString,
      [$"TenantStorage:BackupServers:{configuration.SourceServerKey}:ConnectionString"] =
        configuration.ServerConnectionString,
      [$"TenantStorage:BackupDestinations:{configuration.BackupDestinationKey}:DirectoryPath"] =
        configuration.BackupDirectory,
      [$"TenantStorage:VerificationServers:{configuration.RestoreServerKey}:ConnectionString"] =
        configuration.ServerConnectionString,
      ["TenantStorage:BackupVerification:Enabled"] = "true",
      ["TenantStorage:BackupVerification:RestoreServerKey"] = configuration.RestoreServerKey,
      ["TenantStorage:BackupVerification:RestoreDataRoot"] = configuration.RestoreDataRoot,
      ["TenantStorage:BackupVerification:RestoreLogRoot"] = configuration.RestoreLogRoot,
      ["TenantStorage:BackupVerification:AllowSameInstanceVerification"] = "true"
    };

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddPlatformInfrastructure(
      new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    // Ambient request identity has no meaning in a background process; these are the same trivial
    // implementations the integration fixtures use, registered last so they win.
    services.AddSingleton<ICurrentUser>(new HostUser(configuration.Actor));
    services.AddSingleton<ICurrentTenant>(new HostTenant());
    services.AddSingleton<IDateTimeProvider>(new HostClock());

    // A PLAIN CONTAINER, never a Host: `AddPlatformInfrastructure` registers the verification scheduler's
    // hosted service, and building a real host would start it. Nothing here runs autonomously.
    return services.BuildServiceProvider();
  }

  private sealed record HostConfiguration(
    string Mode,
    string PlatformConnectionString,
    string ServerConnectionString,
    long TenantDatabaseId,
    long SourceBackupRunId,
    long? ExpectedPreviousSuccessfulVerificationRunId,
    string SourceServerKey,
    string RestoreServerKey,
    string BackupDestinationKey,
    string BackupDirectory,
    string RestoreDataRoot,
    string RestoreLogRoot,
    string Actor);

  private sealed class HostUser(string actor) : ICurrentUser
  {
    public string? UserId => actor;
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class HostTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class HostClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}
