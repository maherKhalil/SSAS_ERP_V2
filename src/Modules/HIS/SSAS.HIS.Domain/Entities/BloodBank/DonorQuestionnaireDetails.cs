using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class DonorQuestionnaireDetails : Entity<string>, ITenantOwnedEntity
{

    public DonorQuestionnaireDetails(string id) : base(id) { }
    public DonorQuestionnaireDetails() : base(Guid.NewGuid().ToString()) { }

    public string? QuestionnaireNumber { get; set; }
    public string? QuestionnaireDate { get; set; }
    public string? QuestionnaireResult { get; set; }
    public string? QuestionsURL { get; set; }
    public string? Pulse_BPH { get; set; }
    public string? Urine { get; set; }
    public string? Extremity { get; set; }
    public string? Glucose { get; set; }
    public string? Temp { get; set; }
    public string? TempMode { get; set; }
    public string? RespRate_MIN { get; set; }
    public string? Positions { get; set; }
    public string? Bowel { get; set; }
    public string? MEWs { get; set; }
    public string? PainScore { get; set; }
    public string? Systole_MM_Hg { get; set; }
    public string? LevelOfConsciouseness { get; set; }
    public string? OxygenSaturation { get; set; }
    public string? Diastole_MM_Hg { get; set; }
    public string? O2Amount { get; set; }
    public string? FallRisk { get; set; }
    public string? Height { get; set; }
    public string? Wight { get; set; }
    public string? Comment { get; set; }
    public string? BloodPressure_SYSTOLIC { get; set; }
    public string? BloodPressure_DIASTOLIC { get; set; }
    public string? CVP { get; set; }
    public string? DonorRegistrationId { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public Guid TenantId { get; set; }

}
