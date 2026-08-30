using SSAS.Attendance.Infrastructure.Persistence;
using SSAS.Payroll.Infrastructure.Persistence;
using SSAS.GL.Infrastructure.Persistence;
using SSAS.HR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Architecture.Tests;

// UNICODE STRING PERSISTENCE.
//
// Every string column this ERP persists must be Unicode. The system is multi-tenant and multi-language —
// Arabic company names, localized labels, operator names, error text from a server whose collation nobody
// chose — and a non-Unicode column does not fail loudly when it meets a character it cannot represent. It
// silently substitutes '?'. The data is gone at write time, and no later migration recovers it.
//
// The guard reads the EF CORE MODEL rather than migration text, so it covers every entity in both contexts
// including ones added after this test was written, and it catches the two distinct ways a non-Unicode
// mapping arrives: an explicit `HasColumnType("varchar(...)")`, and `IsUnicode(false)`.
public sealed class UnicodeStringPersistenceArchitectureTests
{
  // Store types that cannot represent the full character range. `nvarchar`, `nchar` and `ntext` are the
  // Unicode counterparts and pass — note that testing with StartsWith is safe precisely because the Unicode
  // spellings begin with 'n'.
  private static readonly string[] NonUnicodeStoreTypes = ["varchar", "char", "text"];

  private static readonly string[] UnicodeStoreTypes = ["nvarchar", "nchar", "ntext"];

  // PRE-EXISTING, DELIBERATE, AND OUT OF SCOPE FOR THE SLICE THAT ADDED THIS GUARD.
  //
  // The localization tables store BCP-47 culture codes, a closed text-format vocabulary and a change-type
  // enum, all under Latin1_General_100_BIN2 for deterministic ordinal comparison. Those values are ASCII by
  // definition — a culture code cannot contain Arabic — so the mapping is defensible rather than accidental.
  //
  // They are enumerated by exact identity so they are VISIBLE as debt rather than invisible as a pattern.
  // Anything NOT on this list must be Unicode, which is the point: this list may shrink, and must not grow
  // without a decision recorded alongside it.
  private static readonly HashSet<string> AcknowledgedNonUnicodeColumns = new(StringComparer.Ordinal)
  {
    "TenantLocalizationOverride.Culture",
    "TenantLocalizationOverride.TextFormat",
    "TenantLocalizationOverrideVersion.Culture",
    "TenantLocalizationOverrideVersion.TextFormat",
    "TenantLocalizationOverrideVersion.ChangeType",
    "TenantLocalizationSettings.TenantDefaultCulture",

    // Tenant ERP. An ISO 4217 currency code, mapped char(3) fixed-length under ordinal collation. Same
    // reasoning as the culture codes — the standard defines three uppercase ASCII letters — and the same
    // caveat: "it is always ASCII" is an argument that ages badly, and this one is nearer the ERP's own data
    // than a culture code is. Changing it needs a tenant migration, so it is recorded here for a decision
    // rather than altered by the slice that added this guard.
    "Company.BaseCurrencyCode"
  };

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Every_persisted_string_in_the_platform_model_is_unicode()
  {
    // ⚠ The floors prove the model was BUILT and its properties enumerated. They cannot prove
    // `IsStringProperty` and `IsNonUnicode` still select anything -- that is what the third test in
    // this file does, and its name now says so.
    var model = PlatformModel();

    ModelWalk.Properties(
      ModelWalk.Entities(model.GetEntityTypes(), "PlatformModel", 28), "PlatformModel", 350);

    Assert.Empty(NonUnicodeStringColumns(model));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Every_persisted_string_in_the_tenant_model_is_unicode()
  {
    // ⚠ The floors prove the model was BUILT and its properties enumerated. They cannot prove
    // `IsStringProperty` and `IsNonUnicode` still select anything -- that is what the third test in
    // this file does, and its name now says so.
    var model = TenantModel();

    ModelWalk.Properties(
      ModelWalk.Entities(model.GetEntityTypes(), "TenantModel", 28), "TenantModel", 350);

    Assert.Empty(NonUnicodeStringColumns(model));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Every_persisted_string_in_the_backup_and_recovery_schema_is_unicode()
  {
    // Stated separately from the sweep above so the backup schema's compliance is asserted by name. These
    // tables carry operator identities and provider error text — exactly the fields most likely to arrive
    // with non-ASCII content from somebody else's system.
    var backupEntities = PlatformModel().GetEntityTypes()
      .Where(entity =>
        entity.ClrType.Name.Contains("Backup", StringComparison.Ordinal) ||
        entity.ClrType.Name.Contains("TenantDatabase", StringComparison.Ordinal))
      .ToArray();

    Assert.NotEmpty(backupEntities);

    foreach (var entity in backupEntities)
    {
      foreach (var property in entity.GetProperties().Where(IsStringProperty))
      {
        Assert.False(IsNonUnicode(property),
          $"{entity.ClrType.Name}.{property.Name} persists text as a non-Unicode type");
      }
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  // ⚠ THIS TEST IS THE CONTROL FOR THE TWO BANS ABOVE, AND THE NAME SAYS SO BECAUSE THE NAME IS WHAT
  // A DELETER READS. Running the walk with exemptions OFF and requiring every acknowledged column to be
  // found proves that `GetEntityTypes`, `IsStringProperty` and `IsNonUnicode` all still select real
  // rows. **Delete this as a redundant test of the exemption list and the two `Assert.Empty` bans above
  // become unfalsifiable in the same commit, with nothing to show for it.**
  public void Every_acknowledged_column_is_still_found_which_is_what_makes_the_two_bans_above_meaningful()
  {
    // Keeps the exemption list honest in the other direction: an entry that no longer corresponds to a real
    // non-Unicode column is dead weight that would silently excuse a future regression at the same name.
    var offenders = NonUnicodeStringColumns(PlatformModel(), applyExemptions: false)
      .Concat(NonUnicodeStringColumns(TenantModel(), applyExemptions: false))
      .ToHashSet(StringComparer.Ordinal);

    foreach (var acknowledged in AcknowledgedNonUnicodeColumns)
    {
      Assert.Contains(acknowledged, offenders);
    }
  }

  private static string[] NonUnicodeStringColumns(IModel model, bool applyExemptions = true) =>
    [.. model.GetEntityTypes()
      .SelectMany(entity => entity.GetProperties()
        .Where(IsStringProperty)
        .Where(IsNonUnicode)
        .Select(property => $"{entity.ClrType.Name}.{property.Name}"))
      .Where(column => !applyExemptions || !AcknowledgedNonUnicodeColumns.Contains(column))
      .Distinct(StringComparer.Ordinal)
      .OrderBy(column => column, StringComparer.Ordinal)];

  // Classified by STORE TYPE, deliberately not by CLR type.
  //
  // A `property.ClrType == typeof(string)` check looks right and misses the most common case in this
  // codebase: enums and value objects persisted as text through HasConversion<string>(). Their CLR type is
  // the enum, so a CLR-type check walks straight past them — including every status, mode and code column in
  // the backup schema. What matters is what lands in SQL Server, so that is what this reads.
  private static bool IsStringProperty(IProperty property)
  {
    var columnType = property.GetColumnType();
    return !string.IsNullOrEmpty(columnType) &&
      (NonUnicodeStoreTypes.Any(type => StartsWithStoreType(columnType, type)) ||
        UnicodeStoreTypes.Any(type => StartsWithStoreType(columnType, type)));
  }

  private static bool IsNonUnicode(IProperty property)
  {
    // Explicitly declared non-Unicode.
    if (property.IsUnicode() == false)
    {
      return true;
    }

    // Or declared through a raw store type, which bypasses IsUnicode entirely.
    var columnType = property.GetColumnType();
    return !string.IsNullOrEmpty(columnType) &&
      NonUnicodeStoreTypes.Any(type => StartsWithStoreType(columnType, type));
  }

  // Matches the store type name, not a prefix of a longer word: `nvarchar` must never be read as `varchar`,
  // and `character varying` is not `char`.
  private static bool StartsWithStoreType(string columnType, string storeType) =>
    columnType.StartsWith(storeType, StringComparison.OrdinalIgnoreCase) &&
    (columnType.Length == storeType.Length || columnType[storeType.Length] is '(' or ' ');

  private static IModel PlatformModel()
  {
    var options = new DbContextOptionsBuilder<PlatformDbContext>()
      .UseSqlServer("Server=model;Database=model;Integrated Security=True")
      .Options;
    using var context = new PlatformDbContext(options, new ModelUser(), new ModelTenant(null), new ModelClock());
    return context.Model;
  }

  private static IModel TenantModel()
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=model;Database=model;Integrated Security=True")
      .Options;
    // ---- ⚠ THE MODULE CONTRIBUTORS ARE THE POINT (T-133).
    //
    // A directly-constructed `TenantDbContext` carries only the entities it configures itself — TWO of
    // them. Every module's tables arrive through an `ITenantModelContributor` registered in DI, so
    // without these four this guard inspected 2 entity types and reported on a tenant database that has
    // dozens. **It passed a planted `varchar(128)` on `Account.Name` twice before this line existed.**
    using var context = new TenantDbContext(
      options, new ModelUser(), new ModelTenant(Guid.NewGuid()), new ModelClock(),
      modelContributors:
      [
        new HrTenantModelContributor(), new GlTenantModelContributor(),
        new PayrollTenantModelContributor(), new AttendanceTenantModelContributor()
      ]);
    return context.Model;
  }

  private sealed class ModelUser : ICurrentUser
  {
    public string? UserId => null;

    public string? UserName => null;

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class ModelTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class ModelClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => new(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
  }
}
