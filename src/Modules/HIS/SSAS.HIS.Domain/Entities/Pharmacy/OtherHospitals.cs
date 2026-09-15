using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class OtherHospitals : Entity<string>, ITenantOwnedEntity
{

    public OtherHospitals(string id) : base(id) { }
    public OtherHospitals() : base(Guid.NewGuid().ToString()) { }

    public string? HospitalName { get; set; }
    public string? POBox { get; set; }
    public string? City { get; set; }
    public string? Zip { get; set; }
    public string? Email { get; set; }
    public string? WebSite { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
