using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class InternalConsumption : Entity<string>, ITenantOwnedEntity
{

    public InternalConsumption(string id) : base(id) { }
    public InternalConsumption() : base(Guid.NewGuid().ToString()) { }

    public string? InternalConsumptionNumber { get; set; }
    public string? Date { get; set; }
    public string? SubStoreID { get; set; }
    public string? Remarks { get; set; }
    public string? IsActive { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
