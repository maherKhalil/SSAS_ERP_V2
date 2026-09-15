using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class StockControl : Entity<string>, ITenantOwnedEntity
{

    public StockControl(string id) : base(id) { }
    public StockControl() : base(Guid.NewGuid().ToString()) { }

    public string? DrugID { get; set; }
    public string? SubStoreID { get; set; }
    public string? StockOnHand { get; set; }
    public string? MaxQuantity { get; set; }
    public string? MinQuantity { get; set; }
    public string? RecordLevel { get; set; }
    public string? RecordQuantity { get; set; }
    public string? Accessability { get; set; }
    public string? UnitConversionFactorId_ForPurchase { get; set; }
    public string? UnitConversionFactorId_ForIssue { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? ReOrder { get; set; }
    public string? CriticalQTY { get; set; }
    public string? ReOrderQTY { get; set; }
    public string? ReorderUnit { get; set; }
    public Guid TenantId { get; set; }

}
