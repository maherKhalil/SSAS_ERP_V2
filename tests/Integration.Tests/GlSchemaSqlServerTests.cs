using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.GL.Domain.Accounts;
using SSAS.GL.Domain.Calendar;
using SSAS.GL.Domain.Journals;
using SSAS.GL.Infrastructure.Persistence;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// GL AGAINST REAL SQL (FP-011).
//
// ================================================================================================
// THIS IS WHERE THE GUARANTEES LIVE. The API tests could not prove any of them.
// ================================================================================================
//
// `IAppendOnlyEntity` is enforced by `TenantDbContext.PreventAppendOnlyMutation`, not by `JournalEntry`.
// The posting transaction is real only against a real database. `nvarchar` and `decimal(19,4)` are
// properties of COLUMNS, asserted from `sys.columns` rather than from the EF model — because asserting
// from the model tests the model's opinion of the database, and FP-009 established that the catalog views
// are the only version that catches a hand-written migration.
//
// Deliberately NOT in `TenantBackupSerialSuites`: this class creates one Guid-named disposable catalog and
// shares nothing across databases. The admission rule is explicit that "it needs real SQL" is an argument
// for being an integration test, not for being serial.
public sealed class GlSchemaSqlServerTests
{
  // ================================================================================================
  // SCHEMA — asserted from the catalog views
  // ================================================================================================

  [Theory]
  [Trait("Decision", "DEC-GL-0006")]
  [InlineData("GlAccounts", "Code")]
  [InlineData("GlAccounts", "NormalizedCode")]
  [InlineData("GlAccounts", "Name")]
  [InlineData("GlAccounts", "NormalizedName")]
  [InlineData("GlFiscalYears", "Code")]
  [InlineData("GlFiscalPeriods", "Name")]
  [InlineData("GlFiscalPeriods", "Status")]
  [InlineData("GlJournalDrafts", "Description")]
  [InlineData("GlJournalDrafts", "Reference")]
  [InlineData("GlJournalEntries", "JournalNumber")]
  [InlineData("GlJournalEntries", "Description")]
  [InlineData("GlJournalLines", "Description")]
  public async Task Every_gl_string_column_is_nvarchar(string table, string column)
  {
    await using var fixture = await GlFixture.CreateAsync();

    var type = await fixture.StringAsync(
      "SELECT ty.name FROM sys.columns c " +
      "JOIN sys.tables t ON t.object_id = c.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "JOIN sys.types ty ON ty.user_type_id = c.user_type_id " +
      $"WHERE s.name = N'tenant' AND t.name = N'{table}' AND c.name = N'{column}'");

    Assert.Equal("nvarchar", type);
  }

  [Theory]
  [Trait("Decision", "DEC-GL-0001")]
  [InlineData("GlJournalLines", "Debit")]
  [InlineData("GlJournalLines", "Credit")]
  [InlineData("GlJournalDraftLines", "Debit")]
  [InlineData("GlJournalDraftLines", "Credit")]
  public async Task Every_monetary_column_is_decimal_19_4(string table, string column)
  {
    await using var fixture = await GlFixture.CreateAsync();

    var precision = await fixture.ScalarAsync(
      "SELECT c.precision FROM sys.columns c " +
      "JOIN sys.tables t ON t.object_id = c.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      $"WHERE s.name = N'tenant' AND t.name = N'{table}' AND c.name = N'{column}'");

    var scale = await fixture.ScalarAsync(
      "SELECT c.scale FROM sys.columns c " +
      "JOIN sys.tables t ON t.object_id = c.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      $"WHERE s.name = N'tenant' AND t.name = N'{table}' AND c.name = N'{column}'");

    Assert.Equal(19, precision);
    Assert.Equal(4, scale);
  }

  [Fact]
  [Trait("Decision", "OD-GL-0003")]
  public async Task The_accounts_table_has_no_company_column()
  {
    // The ruling, asserted where it would actually be broken — in the database, not in the model.
    await using var fixture = await GlFixture.CreateAsync();

    var columns = await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.columns c " +
      "JOIN sys.tables t ON t.object_id = c.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "WHERE s.name = N'tenant' AND t.name = N'GlAccounts' AND c.name = N'CompanyId'");

    Assert.Equal(0, columns);
  }

  [Fact]
  [Trait("Decision", "DEC-GL-0006")]
  public async Task No_gl_table_has_a_foreign_key_leaving_the_tenant_database()
  {
    // Cross-database foreign keys are not expressible in SQL Server, so this asserts the weaker thing that
    // IS checkable and is the real risk: every GL foreign key resolves inside this catalog.
    await using var fixture = await GlFixture.CreateAsync();

    var dangling = await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.foreign_keys fk " +
      "JOIN sys.tables t ON t.object_id = fk.parent_object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "WHERE s.name = N'tenant' AND t.name LIKE N'Gl%' " +
      "AND fk.referenced_object_id NOT IN (SELECT object_id FROM sys.tables)");

    Assert.Equal(0, dangling);
  }

  [Fact]
  [Trait("Decision", "BR-GL-0005")]
  public async Task Journal_numbers_are_unique_within_company_and_fiscal_year()
  {
    await using var fixture = await GlFixture.CreateAsync();

    var indexed = await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.indexes i " +
      "JOIN sys.tables t ON t.object_id = i.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "WHERE s.name = N'tenant' AND t.name = N'GlJournalEntries' " +
      "AND i.name = N'UX_GlJournalEntries_Tenant_Company_Year_Number' AND i.is_unique = 1");

    Assert.Equal(1, indexed);
  }

  [Fact]
  [Trait("Decision", "OD-GL-0006")]
  public async Task Only_one_reversal_per_original_is_permitted_by_the_database()
  {
    // The aggregate refuses the second reversal, but two concurrent requests can both read "not yet
    // reversed". The FILTERED unique index is what makes the race unwinnable.
    await using var fixture = await GlFixture.CreateAsync();

    var filtered = await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.indexes i " +
      "JOIN sys.tables t ON t.object_id = i.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "WHERE s.name = N'tenant' AND t.name = N'GlJournalEntries' " +
      "AND i.name = N'UX_GlJournalEntries_OneReversalPerOriginal' " +
      "AND i.is_unique = 1 AND i.has_filter = 1");

    Assert.Equal(1, filtered);
  }

  [Fact]
  [Trait("Decision", "DEC-GL-0007")]
  public async Task Posted_journal_tables_carry_no_row_version_column()
  {
    await using var fixture = await GlFixture.CreateAsync();

    foreach (var table in new[] { "GlJournalEntries", "GlJournalLines" })
    {
      var rowVersions = await fixture.ScalarAsync(
        "SELECT COUNT(*) FROM sys.columns c " +
        "JOIN sys.tables t ON t.object_id = c.object_id " +
        "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
        $"WHERE s.name = N'tenant' AND t.name = N'{table}' AND c.name = N'RowVersion'");

      Assert.Equal(0, rowVersions);
    }
  }

  // ================================================================================================
  // THE APPEND-ONLY GUARANTEE — THROUGH THE REAL WRITE BOUNDARY
  // ================================================================================================

  [Fact]
  [Trait("Decision", "BR-GL-0002")]
  public async Task A_posted_journal_cannot_be_modified_by_attaching_it_directly_to_the_context()
  {
    // ---- THIS IS THE TEST THAT MATTERS, AND IT IS WHY THE INTERFACE EXISTS.
    //
    // Going through a repository proves only that "there is no repository method for it" — which
    // `IAppendOnlyEntity`'s own comment says is insufficient, because it protects only the callers who go
    // through the repository. Attaching directly and calling SaveChangesAsync is the path a future
    // developer takes, and the write boundary must refuse it.
    await using var fixture = await GlFixture.CreateAsync();
    var journalId = await fixture.SeedPostedJournalAsync();

    await using var context = fixture.CreateContext();

    var entry = await context.Set<JournalEntry>().FirstAsync(candidate => candidate.Id == journalId);
    context.Entry(entry).State = EntityState.Modified;

    var refused = await Assert.ThrowsAsync<InvalidOperationException>(
      () => context.SaveChangesAsync());

    Assert.Contains("Append-only", refused.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  [Trait("Decision", "BR-GL-0002")]
  public async Task A_posted_journal_line_cannot_be_deleted()
  {
    await using var fixture = await GlFixture.CreateAsync();
    var journalId = await fixture.SeedPostedJournalAsync();

    await using var context = fixture.CreateContext();

    var line = await context.Set<JournalLine>().FirstAsync(candidate => candidate.JournalEntryId == journalId);
    context.Set<JournalLine>().Remove(line);

    var refused = await Assert.ThrowsAsync<InvalidOperationException>(
      () => context.SaveChangesAsync());

    Assert.Contains("Append-only", refused.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  [Trait("Decision", "OD-GL-0007")]
  public async Task A_draft_by_contrast_can_be_edited_and_deleted()
  {
    // The other half of the two-aggregate ruling. If this failed, the draft would be useless and
    // `OD-GL-0007` option 3 would have bought nothing.
    await using var fixture = await GlFixture.CreateAsync();

    Guid draftId;
    await using (var context = fixture.CreateContext())
    {
      var draft = JournalDraft.Create(fixture.EntryDate, "Draft", null).Value;
      draft.CompanyId = fixture.CompanyA;
      context.Set<JournalDraft>().Add(draft);
      await context.SaveChangesAsync();
      draftId = draft.Id;
    }

    await using (var context = fixture.CreateContext())
    {
      var draft = await context.Set<JournalDraft>().FirstAsync(candidate => candidate.Id == draftId);
      Assert.True(draft.Update(fixture.EntryDate, "Edited", "REF").IsSuccess);
      await context.SaveChangesAsync();
    }

    await using (var context = fixture.CreateContext())
    {
      var draft = await context.Set<JournalDraft>().FirstAsync(candidate => candidate.Id == draftId);
      Assert.Equal("Edited", draft.Description);

      context.Set<JournalDraft>().Remove(draft);
      await context.SaveChangesAsync();
    }

    await using (var context = fixture.CreateContext())
    {
      Assert.Empty(await context.Set<JournalDraft>().Where(candidate => candidate.Id == draftId).ToListAsync());
    }
  }

  // ================================================================================================
  // ROUND TRIPS
  // ================================================================================================

  [Fact]
  [Trait("Decision", "AC-GL-0003")]
  public async Task Amounts_round_trip_at_four_decimal_places()
  {
    await using var fixture = await GlFixture.CreateAsync();
    var journalId = await fixture.SeedPostedJournalAsync(debit: 1234.5678m);

    await using var context = fixture.CreateContext();
    var line = await context.Set<JournalLine>()
      .Where(candidate => candidate.JournalEntryId == journalId && candidate.Debit > 0m)
      .FirstAsync();

    Assert.Equal(1234.5678m, line.Debit);
  }

  [Fact]
  [Trait("Decision", "AC-GL-0019")]
  public async Task Arabic_text_round_trips_unchanged()
  {
    const string arabic = "حساب المدينون التجاريون";

    await using var fixture = await GlFixture.CreateAsync();

    Guid accountId;
    await using (var context = fixture.CreateContext())
    {
      var account = Account.Create("4100", arabic).Value;
      context.Set<Account>().Add(account);
      await context.SaveChangesAsync();
      accountId = account.Id;
    }

    await using (var context = fixture.CreateContext())
    {
      var account = await context.Set<Account>().FirstAsync(candidate => candidate.Id == accountId);
      Assert.Equal(arabic, account.Name.Value);
    }
  }

  [Fact]
  [Trait("Decision", "OD-GL-0003")]
  public async Task Two_accounts_cannot_share_a_code_within_a_tenant()
  {
    // Tenant-wide, not company-wide — the direct consequence of the chart being tenant-level. The index is
    // what makes the handler's check true under concurrency.
    await using var fixture = await GlFixture.CreateAsync();

    await using (var context = fixture.CreateContext())
    {
      context.Set<Account>().Add(Account.Create("4100", "First").Value);
      await context.SaveChangesAsync();
    }

    await using (var context = fixture.CreateContext())
    {
      context.Set<Account>().Add(Account.Create("4100", "Second").Value);

      await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
  }

  [Fact]
  public async Task Codes_differing_only_in_case_collide_because_the_column_is_binary_collated()
  {
    await using var fixture = await GlFixture.CreateAsync();

    await using (var context = fixture.CreateContext())
    {
      context.Set<Account>().Add(Account.Create("ab-100", "Lower").Value);
      await context.SaveChangesAsync();
    }

    await using (var context = fixture.CreateContext())
    {
      // The NORMALIZED column carries the upper-cased form, so these are the same code even though the
      // display values differ. The binary collation is what makes the comparison ordinal rather than
      // dependent on the server's default.
      context.Set<Account>().Add(Account.Create("AB-100", "Upper").Value);

      await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
  }

  // ================================================================================================
  // THE FIXTURE
  // ================================================================================================
}
