namespace SSAS.BuildingBlocks.Domain;

public interface ITenantOwnedEntity
{
  Guid TenantId { get; set; }
}
