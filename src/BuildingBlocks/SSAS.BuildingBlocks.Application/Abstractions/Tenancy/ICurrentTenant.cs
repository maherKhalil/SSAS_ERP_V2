namespace SSAS.BuildingBlocks.Application.Abstractions.Tenancy;

public interface ICurrentTenant
{
  Guid? TenantId { get; }
}
