using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class PhysicalStockAdjustment : Entity<string>, ITenantOwnedEntity
{

    public PhysicalStockAdjustment(string id) : base(id) { }
    public PhysicalStockAdjustment() : base(Guid.NewGuid().ToString()) { }

    public string? SubStoreID { get; set; }
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
