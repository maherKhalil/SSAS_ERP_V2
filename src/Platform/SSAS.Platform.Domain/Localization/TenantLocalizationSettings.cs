using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;

namespace SSAS.Platform.Domain.Localization;

public sealed class TenantLocalizationSettings : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity
{
  private TenantLocalizationSettings(Guid tenantId, LocalizationCulture tenantDefaultCulture)
    : base(tenantId)
  {
    TenantId = tenantId;
    TenantDefaultCulture = tenantDefaultCulture;
    TenantLocalizationVersion = TenantLocalizationVersion.Create(1).Value;
  }

  private TenantLocalizationSettings()
    : base(Guid.Empty)
  {
    TenantDefaultCulture = null!;
  }

  public Guid TenantId { get; private set; }

  public LocalizationCulture TenantDefaultCulture { get; private set; }

  public TenantLocalizationVersion TenantLocalizationVersion { get; private set; }

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public static TenantLocalizationSettings Create(Guid tenantId, LocalizationCulture tenantDefaultCulture)
  {
    ArgumentNullException.ThrowIfNull(tenantDefaultCulture);
    if (tenantId == Guid.Empty)
    {
      throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
    }

    return new TenantLocalizationSettings(tenantId, tenantDefaultCulture);
  }

  public Result IncrementVersion()
  {
    var next = TenantLocalizationVersion.Increment();
    if (next.IsFailure)
    {
      return Result.Failure(next.Error);
    }

    TenantLocalizationVersion = next.Value;
    return Result.Success();
  }

  Guid ITenantOwnedEntity.TenantId { get => TenantId; set => TenantId = value; }
  DateTimeOffset IAuditableEntity.CreatedUtc { get => CreatedUtc; set => CreatedUtc = value; }
  DateTimeOffset IAuditableEntity.ModifiedUtc { get => ModifiedUtc; set => ModifiedUtc = value; }
  string? IAuditableEntity.CreatedBy { get => CreatedBy; set => CreatedBy = value; }
  string? IAuditableEntity.ModifiedBy { get => ModifiedBy; set => ModifiedBy = value; }
}
