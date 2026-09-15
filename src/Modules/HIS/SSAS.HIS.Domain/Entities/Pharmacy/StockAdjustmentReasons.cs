using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class StockAdjustmentReasons : Entity<string>, ITenantOwnedEntity
{

    public StockAdjustmentReasons(string id) : base(id) { }
    public StockAdjustmentReasons() : base(Guid.NewGuid().ToString()) { }

    public string? NameEN { get; set; }
    public string? NameAR { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
