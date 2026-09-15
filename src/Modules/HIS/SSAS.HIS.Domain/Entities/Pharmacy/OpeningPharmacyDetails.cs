using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class OpeningPharmacyDetails : Entity<string>, ITenantOwnedEntity
{

    public OpeningPharmacyDetails(string id) : base(id) { }
    public OpeningPharmacyDetails() : base(Guid.NewGuid().ToString()) { }

    public string? OpeningPharmacyID { get; set; }
    public string? DrugID { get; set; }
    public string? BatchID { get; set; }
    public string? ExpiryDate { get; set; }
    public string? BatchNo { get; set; }
    public string? PhysicQTY { get; set; }
    public string? PharmacyInHand { get; set; }
    public string? DifferenceQTY { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? OpenBalance { get; set; }
    public Guid TenantId { get; set; }

}
