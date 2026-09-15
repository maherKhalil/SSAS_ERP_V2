using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class NursingAssessment : Entity<string>, ITenantOwnedEntity
{

    public NursingAssessment(string id) : base(id) { }
    public NursingAssessment() : base(Guid.NewGuid().ToString()) { }

    public string? PatientID { get; set; }
    public string? AssessmentID { get; set; }
    public string? Date { get; set; }
    public string? Time { get; set; }
    public string? DoneBy { get; set; }
    public string? OPNumber { get; set; }
    public string? PainIntensily { get; set; }
    public string? TypeOfPainID { get; set; }
    public string? Location { get; set; }
    public string? FrequencyID { get; set; }
    public string? Duration { get; set; }
    public string? MentalStatusID { get; set; }
    public string? SpeechID { get; set; }
    public string? RespirationID { get; set; }
    public string? SkinColorID { get; set; }
    public string? SkinTemperatureID { get; set; }
    public string? SkinMoistureID { get; set; }
    public string? Remarks { get; set; }
    public string? ApprovedBy { get; set; }
    public string? ApprovedDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
