using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class PhysicalStockAdjustmentEntry : Entity<string>, ITenantOwnedEntity
{

    public PhysicalStockAdjustmentEntry(string id) : base(id) { }
    public PhysicalStockAdjustmentEntry() : base(Guid.NewGuid().ToString()) { }

    public string? PhysiacalAdjID { get; set; }
    public string? BatchID { get; set; }
    public string? QTYinHand { get; set; }
    public string? QTYAdj { get; set; }
    public string? QtYDifference { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? DrugID { get; set; }
    public Guid TenantId { get; set; }

}
