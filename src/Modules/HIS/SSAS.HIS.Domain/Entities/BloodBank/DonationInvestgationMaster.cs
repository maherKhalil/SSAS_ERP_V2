using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class DonationInvestgationMaster : Entity<string>, ITenantOwnedEntity
{

    public DonationInvestgationMaster(string id) : base(id) { }
    public DonationInvestgationMaster() : base(Guid.NewGuid().ToString()) { }

    public string? InvestgationID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? DonorID { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
