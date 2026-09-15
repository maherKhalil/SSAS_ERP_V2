using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class ExternalFacilityDetails : Entity<string>, ITenantOwnedEntity
{

    public ExternalFacilityDetails(string id) : base(id) { }
    public ExternalFacilityDetails() : base(Guid.NewGuid().ToString()) { }

    public string? ExternalFacilityID { get; set; }
    public string? BloodProductID { get; set; }
    public string? ProductNo { get; set; }
    public string? ExpiryDate { get; set; }
    public string? Quantity { get; set; }
    public string? Price { get; set; }
    public string? BagNo { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? a1 { get; set; }
    public Guid TenantId { get; set; }

}
