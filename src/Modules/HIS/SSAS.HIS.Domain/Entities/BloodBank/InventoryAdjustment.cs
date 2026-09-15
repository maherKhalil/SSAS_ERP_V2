using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class InventoryAdjustment : Entity<string>, ITenantOwnedEntity
{

    public InventoryAdjustment(string id) : base(id) { }
    public InventoryAdjustment() : base(Guid.NewGuid().ToString()) { }

    public string? StockID { get; set; }
    public string? ReasonAdjusyId { get; set; }
    public string? Date { get; set; }
    public string? Status { get; set; }
    public string? Remarks { get; set; }
    public string? PhysiacalNO { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
