using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class LocalPurchaseOrderDetails : Entity<string>, ITenantOwnedEntity
{

    public LocalPurchaseOrderDetails(string id) : base(id) { }
    public LocalPurchaseOrderDetails() : base(Guid.NewGuid().ToString()) { }

    public string? DrugID { get; set; }
    public string? LPOID { get; set; }
    public string? OrderedQTY { get; set; }
    public string? BonusQuantity { get; set; }
    public string? Amount { get; set; }
    public string? Price { get; set; }
    public string? DiscountAmount { get; set; }
    public string? DiscountPrecint { get; set; }
    public string? UnitConversionFactorId { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? AcceptQTY { get; set; }
    public string? BonusQTY { get; set; }
    public string? AmountFC { get; set; }
    public string? PriceFC { get; set; }
    public string? SellPrice { get; set; }
    public string? PrevAcceptQTY { get; set; }
    public string? GenericID { get; set; }
    public Guid TenantId { get; set; }

}
