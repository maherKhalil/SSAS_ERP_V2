using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodTransfusion : Entity<string>, ITenantOwnedEntity
{

    public BloodTransfusion(string id) : base(id) { }
    public BloodTransfusion() : base(Guid.NewGuid().ToString()) { }

    public string? PatientId { get; set; }
    public string? DoctorId { get; set; }
    public string? NurseId { get; set; }
    public string? ReactionDate { get; set; }
    public string? ReactionTime { get; set; }
    public string? Reasonfortransfusion { get; set; }
    public string? Component { get; set; }
    public string? VolumeGiven { get; set; }
    public string? DonationNumberinUnits { get; set; }
    public string? IsChills { get; set; }
    public string? IsUrticaria { get; set; }
    public string? IsTachycadia { get; set; }
    public string? IsChestPain { get; set; }
    public string? IsNausea { get; set; }
    public string? IsDyspnoea { get; set; }
    public string? IsLumbarPain { get; set; }
    public string? IsBurningaroundveinhypotension { get; set; }
    public string? IsHaemoglobinuria { get; set; }
    public string? IsExcessiveBleeding { get; set; }
    public string? IsJaundice { get; set; }
    public string? IsShock { get; set; }
    public string? otherSymptoms { get; set; }
    public string? treatmentgiven { get; set; }
    public string? result { get; set; }
    public string? previoustransfusion { get; set; }
    public string? Reactions { get; set; }
    public string? Pregnancies { get; set; }
    public string? KnownAntiBodies { get; set; }
    public string? TransfusionDate { get; set; }
    public string? TransfusionTime { get; set; }
    public string? signture { get; set; }
    public string? BloodBankrequestrecivedDate { get; set; }
    public string? BloodBankrequestrecivedTime { get; set; }
    public string? LabellingError { get; set; }
    public string? pretransfusion { get; set; }
    public string? preAntibody { get; set; }
    public string? preDAT { get; set; }
    public string? Posttransfusion { get; set; }
    public string? PostAntibody { get; set; }
    public string? PostDAT { get; set; }
    public string? CompanyID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? InvestDtlID { get; set; }
    public string? ISPregnancies { get; set; }
    public string? otherDoctor { get; set; }
    public string? TimeOfInformDR { get; set; }
    public string? TransfusionStartTime { get; set; }
    public string? TransfusionEndTime { get; set; }
    public string? IsTransfusionAction { get; set; }
    public string? dispensedBag { get; set; }
    public Guid TenantId { get; set; }

}
