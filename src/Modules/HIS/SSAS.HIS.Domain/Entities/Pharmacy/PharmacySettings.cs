using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class PharmacySettings : Entity<string>, ITenantOwnedEntity
{

    public PharmacySettings(string id) : base(id) { }
    public PharmacySettings() : base(Guid.NewGuid().ToString()) { }

    public string? MinQtyStatus { get; set; }
    public string? MinUserId { get; set; }
    public string? MaxQtyStatus { get; set; }
    public string? MaxUserId { get; set; }
    public string? ExchangePolicy { get; set; }
    public string? UnifiedPurchaseSupplierID { get; set; }
    public string? PharmacyMgr { get; set; }
    public string? PharmacySubDepartmentID { get; set; }
    public string? CompanyID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? Vat { get; set; }
    public string? DispenseQuantity { get; set; }
    public string? PhysicalStockAdjustment { get; set; }
    public string? ExpiaryPeriodDays { get; set; }
    public string? RecessionPeriodInDays { get; set; }
    public string? ReorderLvel { get; set; }
    public string? slowMove { get; set; }
    public Guid TenantId { get; set; }

}
