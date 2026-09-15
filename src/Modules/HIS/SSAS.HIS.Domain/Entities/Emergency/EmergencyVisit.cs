using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class EmergencyVisit : Entity<string>, ITenantOwnedEntity
{

    public EmergencyVisit(string id) : base(id) { }
    public EmergencyVisit() : base(Guid.NewGuid().ToString()) { }

    public string? PatientID { get; set; }
    public string? VistNo { get; set; }
    public string? OPCase { get; set; }
    public string? ModeOfArrival { get; set; }
    public string? AccompaniedBy { get; set; }
    public string? RelativesNotifiedID { get; set; }
    public string? PriorityID { get; set; }
    public string? DateOfArrival { get; set; }
    public string? TimeOfArrival { get; set; }
    public string? DepartmentID { get; set; }
    public string? ReferralDepartmentID { get; set; }
    public string? DoctorID { get; set; }
    public string? ReferralDoctorID { get; set; }
    public string? ReferralClinic { get; set; }
    public string? CheifComplaint { get; set; }
    public string? OPNumber { get; set; }
    public string? WordToAdmintID { get; set; }
    public string? BedNoID { get; set; }
    public string? IPNumber { get; set; }
    public string? MedicoLegalDetails { get; set; }
    public string? InsuranceAuthorizationCode { get; set; }
    public string? Remarks { get; set; }
    public string? EnteredBy { get; set; }
    public string? ApprovedBy { get; set; }
    public string? ApprovedDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
