using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class InventoryAdjustmentEntry : Entity<string>, ITenantOwnedEntity
{

    public InventoryAdjustmentEntry(string id) : base(id) { }
    public InventoryAdjustmentEntry() : base(Guid.NewGuid().ToString()) { }

    public string? PhysiacalAdjID { get; set; }
    public string? BloodGroupId { get; set; }
    public string? QTYinHand { get; set; }
    public string? QTYAdj { get; set; }
    public string? QtYDifference { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
