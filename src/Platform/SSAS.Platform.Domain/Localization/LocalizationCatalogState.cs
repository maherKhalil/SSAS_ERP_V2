using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;

namespace SSAS.Platform.Domain.Localization;

public sealed class LocalizationCatalogState : AggregateRoot<byte>, IAuditableEntity
{
  public const byte SingletonId = 1;

  private LocalizationCatalogState(CatalogSchemaVersion schemaVersion, CatalogVersion catalogVersion)
    : base(SingletonId)
  {
    CatalogSchemaVersion = schemaVersion;
    HighestActivatedCatalogVersion = catalogVersion;
  }

  private LocalizationCatalogState()
    : base(SingletonId)
  {
  }

  public CatalogSchemaVersion CatalogSchemaVersion { get; private set; }

  public CatalogVersion HighestActivatedCatalogVersion { get; private set; }

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public static LocalizationCatalogState Create(CatalogSchemaVersion schemaVersion, CatalogVersion catalogVersion) =>
    new(schemaVersion, catalogVersion);

  public void Activate(CatalogSchemaVersion schemaVersion, CatalogVersion catalogVersion)
  {
    if (catalogVersion.Value <= HighestActivatedCatalogVersion.Value)
    {
      return;
    }

    CatalogSchemaVersion = schemaVersion;
    HighestActivatedCatalogVersion = catalogVersion;
  }

  DateTimeOffset IAuditableEntity.CreatedUtc { get => CreatedUtc; set => CreatedUtc = value; }
  DateTimeOffset IAuditableEntity.ModifiedUtc { get => ModifiedUtc; set => ModifiedUtc = value; }
  string? IAuditableEntity.CreatedBy { get => CreatedBy; set => CreatedBy = value; }
  string? IAuditableEntity.ModifiedBy { get => ModifiedBy; set => ModifiedBy = value; }
}
