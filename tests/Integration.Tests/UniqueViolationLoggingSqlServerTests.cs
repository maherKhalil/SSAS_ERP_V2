using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SSAS.Attendance.Domain.Calendars;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// DOES A UNIQUE VIOLATION LEAVE ANY TRACE AN OPERATOR CAN FIND? (T-247)
// ==================================================================================================
//
// ---- WHY THIS NEEDED A REAL DATABASE AND COULD NOT BE REASONED OUT.
//
// The application logs NOTHING of its own here. Neither `TenantUnitOfWork` nor `PlatformUnitOfWork` takes a
// logger, and `Error` is `record Error(string Code, string Message)` — so the `SqlException`, the only thing
// carrying the index name, dies at the catch and cannot be recovered downstream.
//
// **That left one question, and it is about EF Core's behaviour rather than our configuration.** Our Serilog
// levels say a `Microsoft.*` entry at `Error` would be written; they say nothing about whether EF raises one
// for a constraint violation, at what level, or with the exception attached. **Reading configuration cannot
// answer a question about someone else's code**, so this provokes a real 2627 against a real server and
// reads what actually arrives.
//
// ---- WHAT IT ASSERTS, AND WHY EACH PART MATTERS SEPARATELY.
//
// **The level** decides whether it survives our `Microsoft` → `Warning` override. An `Information`-level
// entry would be filtered out in production and the log would be empty there while green here.
//
// **The attached exception** is what carries the index name. An entry saying only "a command failed" leaves
// the operator exactly as unable to tell a duplicate account code from a duplicate journal number — the
// `AccessTokenIssuer` shape one layer down.
public sealed class UniqueViolationLoggingSqlServerTests
{
  [Fact]
  public async Task A_constraint_violation_is_logged_by_entity_framework_with_the_exception_attached()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();

    var recorder = new RecordingLoggerProvider();
    using var factory = LoggerFactory.Create(builder =>
    {
      builder.SetMinimumLevel(LogLevel.Trace);
      builder.AddProvider(recorder);
    });

    // The unique index on (TenantId, CompanyId, NormalizedName) -- a real constraint, violated the way a
    // race violates it, rather than a contrived primary-key clash.
    var first = WorkingCalendar.Create(fixture.CompanyA, "Standard", [DayOfWeek.Friday], isDefault: true).Value;
    first.TenantId = fixture.Tenant;

    await using (var seeding = fixture.CreateContext())
    {
      seeding.Set<WorkingCalendar>().Add(first);
      await seeding.SaveChangesAsync();
    }

    // A SECOND context with the SAME name. Two contexts because EF's identity map is not what is being
    // tested -- the point is to make the SERVER refuse, so the exception is a real 2627.
    var clash = WorkingCalendar.Create(fixture.CompanyA, "Standard", [DayOfWeek.Friday], isDefault: false).Value;
    clash.TenantId = fixture.Tenant;

    await using (var second = fixture.CreateContext(factory))
    {
      second.Set<WorkingCalendar>().Add(clash);

      var failure = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
      Assert.NotNull(failure.InnerException);
    }

    var errors = recorder.Entries
      .Where(entry => entry.Level >= LogLevel.Error)
      .ToArray();

    Assert.True(errors.Length > 0,
      "Entity Framework logged nothing at Error for a refused command, so a unique violation leaves no " +
      "trace an operator can find and the index name is lost entirely. Entries seen:\n  " +
      string.Join("\n  ", recorder.Entries.Select(entry => $"{entry.Level} {entry.Category}")));

    // ⚠ MEASURED: the category is `Microsoft.EntityFrameworkCore.Update`, NOT `Database.Command`.
    //
    // I predicted `Database.Command` from EF's `CommandError` event and was wrong -- exactly one Error
    // entry arrives and it comes from the update pipeline. Both begin `Microsoft.`, so the Serilog override
    // capping `Microsoft` at Warning lets either through, and the practical answer is unchanged. **But the
    // category is what an operator would filter on**, and a runbook naming the wrong one would find nothing.
    Assert.Contains(errors, entry =>
      entry.Category.StartsWith("Microsoft.EntityFrameworkCore.Update", StringComparison.Ordinal));

    // Exactly one, so this is a signal rather than noise an operator has to sift.
    Assert.Single(errors);

    // The exception itself, not merely a note that something failed.
    Assert.Contains(errors, entry => entry.Exception is not null);
  }

  private sealed record Entry(LogLevel Level, string Category, Exception? Exception);

  private sealed class RecordingLoggerProvider : ILoggerProvider
  {
    private readonly List<Entry> entries = [];

    internal IReadOnlyList<Entry> Entries
    {
      get
      {
        lock (entries)
        {
          return [.. entries];
        }
      }
    }

    public ILogger CreateLogger(string categoryName) => new Recorder(this, categoryName);

    public void Dispose()
    {
    }

    private void Add(Entry entry)
    {
      lock (entries)
      {
        entries.Add(entry);
      }
    }

    private sealed class Recorder(RecordingLoggerProvider owner, string category) : ILogger
    {
      public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

      public bool IsEnabled(LogLevel logLevel) => true;

      public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        owner.Add(new Entry(logLevel, category, exception));
    }
  }
}
