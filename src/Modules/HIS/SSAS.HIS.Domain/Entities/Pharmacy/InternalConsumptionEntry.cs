using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class InternalConsumptionEntry : Entity<string>, ITenantOwnedEntity
{

    public InternalConsumptionEntry(string id) : base(id) { }
    public InternalConsumptionEntry() : base(Guid.NewGuid().ToString()) { }

    public string? InternalConsumptionID { get; set; }
    public string? DrugID { get; set; }
    public string? SubStoreBatchId { get; set; }
    public string? Date { get; set; }
    public string? UnitID { get; set; }
    public string? CurrentQty { get; set; }
    public string? QtyConsumed { get; set; }
    public string? SubStoreID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
