using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class ExternalFacilityHeader : Entity<string>, ITenantOwnedEntity
{

    public ExternalFacilityHeader(string id) : base(id) { }
    public ExternalFacilityHeader() : base(Guid.NewGuid().ToString()) { }

    public string? FacilityID { get; set; }
    public string? ActionDate { get; set; }
    public string? ReciptNo { get; set; }
    public string? Actiontype { get; set; }
    public string? PatientID { get; set; }
    public string? ExternalFacilityCode { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
