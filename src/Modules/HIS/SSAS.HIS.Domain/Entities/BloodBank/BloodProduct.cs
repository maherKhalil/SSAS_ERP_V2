using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodProduct : Entity<string>, ITenantOwnedEntity
{

    public BloodProduct(string id) : base(id) { }
    public BloodProduct() : base(Guid.NewGuid().ToString()) { }

    public string? BloodProductCode { get; set; }
    public string? ProductNameLatin { get; set; }
    public string? ProductNameLocal { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? BloodGroupId { get; set; }
    public string? Active { get; set; }
    public string? HasExpireDate { get; set; }
    public string? ExpireDateValue { get; set; }
    public string? ExpireDateType { get; set; }
    public string? IsDonation { get; set; }
    public string? NameKa { get; set; }
    public Guid TenantId { get; set; }

}
