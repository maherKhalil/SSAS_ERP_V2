using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodBankInventory : Entity<string>, ITenantOwnedEntity
{

    public BloodBankInventory(string id) : base(id) { }
    public BloodBankInventory() : base(Guid.NewGuid().ToString()) { }

    public string? Code { get; set; }
    public string? BagNo { get; set; }
    public string? Volume { get; set; }
    public string? BloodProductID { get; set; }
    public string? Price { get; set; }
    public string? ExpireDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? DonorID { get; set; }
    public string? DonationInfoID { get; set; }
    public string? ParentID { get; set; }
    public string? NoUnits { get; set; }
    public string? CompanyID { get; set; }
    public string? NameAr { get; set; }
    public string? NameEn { get; set; }
    public string? StoreID { get; set; }
    public string? BloodGroupID { get; set; }
    public Guid TenantId { get; set; }

}
