using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class EmergencyDetails : Entity<string>, ITenantOwnedEntity
{

    public EmergencyDetails(string id) : base(id) { }
    public EmergencyDetails() : base(Guid.NewGuid().ToString()) { }

    public string? PatientID { get; set; }
    public string? Code { get; set; }
    public string? VisiteDate { get; set; }
    public string? DischargeDate { get; set; }
    public string? DischargeStatuse { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? BranchId { get; set; }
    public string? EmergencyUnitId { get; set; }
    public string? wardId { get; set; }
    public string? BedId { get; set; }
    public string? AccommodationType { get; set; }
    public Guid TenantId { get; set; }

}
