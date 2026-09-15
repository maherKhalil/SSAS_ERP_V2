using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class EmergencyUnit : Entity<string>, ITenantOwnedEntity
{

    public EmergencyUnit(string id) : base(id) { }
    public EmergencyUnit() : base(Guid.NewGuid().ToString()) { }

    public string? ArrivalTypeID { get; set; }
    public string? ComplainID { get; set; }
    public string? RiskID { get; set; }
    public string? PatientStatusID { get; set; }
    public string? DoctorID { get; set; }
    public string? PaymentTypeID { get; set; }
    public string? ReferralDoctorID { get; set; }
    public string? ReferralUnitID { get; set; }
    public string? CasePriortyID { get; set; }
    public string? PatientID { get; set; }
    public string? Date { get; set; }
    public string? ERcode { get; set; }
    public string? SeenDateTime { get; set; }
    public string? ComplainDuration { get; set; }
    public string? SpecialityID { get; set; }
    public string? Categoryitem { get; set; }
    public string? ItemID { get; set; }
    public string? Reason { get; set; }
    public string? CategoryitemOthers { get; set; }
    public string? CompanyID { get; set; }
    public string? BranchID { get; set; }
    public string? EncounterType { get; set; }
    public Guid TenantId { get; set; }

}
