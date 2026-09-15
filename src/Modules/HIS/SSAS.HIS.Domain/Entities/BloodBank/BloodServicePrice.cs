using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodServicePrice : Entity<string>, ITenantOwnedEntity
{

    public BloodServicePrice(string id) : base(id) { }
    public BloodServicePrice() : base(Guid.NewGuid().ToString()) { }

    public string? BloodGroupID { get; set; }
    public string? ServiceID { get; set; }
    public string? BloodProductID { get; set; }
    public string? Price { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? DonationServiceID { get; set; }
    public Guid TenantId { get; set; }

}
