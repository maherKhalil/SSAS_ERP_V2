using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// TS-1C hardening of the TS-1A/TS-1B LOW finding: the bootstrap's concurrent-backfill retry used to catch
// DbUpdateException broadly. Only recognised SQL Server uniqueness violations may be treated as the benign
// "a peer host won the race" outcome; anything else must propagate so a genuine persistence failure fails
// the bootstrap rather than being retried as if it were contention.
public sealed class TenantStorageBootstrapRetryClassificationTests
{
  private const int UniqueConstraintViolation = 2627;
  private const int UniqueIndexViolation = 2601;
  private const int DeadlockVictim = 1205;

  [Theory]
  [Trait("Decision", "ADR-017")]
  [InlineData(UniqueConstraintViolation)]
  [InlineData(UniqueIndexViolation)]
  public void Recognised_uniqueness_violations_are_classified_as_a_peer_race(int errorNumber)
  {
    Assert.True(Classify(SqlExceptionFactory.Create(errorNumber)));
  }

  [Theory]
  [Trait("Decision", "ADR-017")]
  [InlineData(DeadlockVictim)]      // transient, but NOT a uniqueness race
  [InlineData(547)]                  // FK/CHECK violation
  [InlineData(-2)]                   // timeout
  [InlineData(4060)]                 // cannot open database
  public void Other_sql_failures_are_not_classified_as_a_peer_race(int errorNumber)
  {
    // These must propagate. Retrying them as contention would spin instead of failing, turning a clear
    // startup failure into a hang.
    Assert.False(Classify(SqlExceptionFactory.Create(errorNumber)));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void A_non_sql_inner_exception_is_not_classified_as_a_peer_race()
  {
    Assert.False(Classify(new InvalidOperationException("not a SQL failure")));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void A_dbupdate_exception_without_an_inner_exception_is_not_classified_as_a_peer_race()
  {
    Assert.False(TenantStorageBootstrapService.IsUniquenessConflict(new DbUpdateException("no inner")));
  }

  private static bool Classify(Exception inner) =>
    TenantStorageBootstrapService.IsUniquenessConflict(new DbUpdateException("update failed", inner));

  // SqlException has no public constructor, so the framework's own internal factory is used. This keeps the
  // test exercising real SqlError numbers through the production classification path rather than a
  // stand-in type, which is the whole point: the classifier reads SqlError.Number.
  private static class SqlExceptionFactory
  {
    private static readonly Assembly SqlClient = typeof(Microsoft.Data.SqlClient.SqlConnection).Assembly;

    public static Exception Create(int number)
    {
      var collection = SqlClient.GetType("Microsoft.Data.SqlClient.SqlErrorCollection")!
        .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [], null)!
        .Invoke([]);

      var errorConstructor = SqlClient.GetType("Microsoft.Data.SqlClient.SqlError")!
        .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
        .First(candidate => candidate.GetParameters().Length >= 7);
      var parameters = errorConstructor.GetParameters();
      var arguments = new object?[parameters.Length];
      for (var index = 0; index < parameters.Length; index++)
      {
        arguments[index] = parameters[index].Name switch
        {
          "infoNumber" or "number" => number,
          "errorState" or "state" => (byte)1,
          "errorClass" or "class" => (byte)16,
          "server" => "test-server",
          "errorMessage" or "message" => "test failure",
          "procedure" => string.Empty,
          "lineNumber" => 0,
          _ => parameters[index].ParameterType.IsValueType
            ? Activator.CreateInstance(parameters[index].ParameterType)
            : null
        };
      }

      collection.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(collection, [errorConstructor.Invoke(arguments)]);

      return (Exception)SqlClient.GetType("Microsoft.Data.SqlClient.SqlException")!
        .GetMethod("CreateException", BindingFlags.Static | BindingFlags.NonPublic, null,
          [collection.GetType(), typeof(string)], null)!
        .Invoke(null, [collection, "11.0.0"])!;
    }
  }
}
