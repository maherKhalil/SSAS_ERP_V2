using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class DonationInfo : Entity<string>, ITenantOwnedEntity
{

    public DonationInfo(string id) : base(id) { }
    public DonationInfo() : base(Guid.NewGuid().ToString()) { }

    public string? DonationTypeCode { get; set; }
    public string? DonationTypeName { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? DonationTypeNameEn { get; set; }
    public Guid TenantId { get; set; }

}
