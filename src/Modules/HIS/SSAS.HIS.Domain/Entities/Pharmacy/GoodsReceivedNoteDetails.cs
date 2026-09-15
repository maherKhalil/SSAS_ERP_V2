using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class GoodsReceivedNoteDetails : Entity<string>, ITenantOwnedEntity
{

    public GoodsReceivedNoteDetails(string id) : base(id) { }
    public GoodsReceivedNoteDetails() : base(Guid.NewGuid().ToString()) { }

    public string? GRNId { get; set; }
    public string? DrugID { get; set; }
    public string? AcceptQTY { get; set; }
    public string? OrderedQTY { get; set; }
    public string? BonusQTY { get; set; }
    public string? PriceLC { get; set; }
    public string? AmountLC { get; set; }
    public string? DiscountAmount { get; set; }
    public string? DiscountPrecint { get; set; }
    public string? PriceFC { get; set; }
    public string? UnitConversionFactorId { get; set; }
    public string? AmountFC { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? SellPrice { get; set; }
    public string? LPODetID { get; set; }
    public Guid TenantId { get; set; }

}
