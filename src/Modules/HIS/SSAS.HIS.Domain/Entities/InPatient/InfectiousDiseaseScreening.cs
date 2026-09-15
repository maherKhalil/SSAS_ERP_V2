using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class InfectiousDiseaseScreening : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public string Q1_TravelOutsideUS { get; set; }
        public string Q1_TravelLocation { get; set; }
        public string Q1_HouseholdTravel { get; set; }
        public string Q1_HouseholdTravelLocation { get; set; }
        public string Q2_CloseContact { get; set; }
        public string Q3_Fever { get; set; }
        public string Q4_CoughShortnessBreathSoreThroat { get; set; }
        public string Q5_VomitingDiarrhea { get; set; }
        public string Q6_Rash { get; set; }
        public string Q7_Fever { get; set; }
        public string Q8_SevereHeadache { get; set; }
        public string Q9_DiarrheaVomitingAbdominalPain { get; set; }
        public string Q10_RespiratoryIllness { get; set; }
        public string Q11_NewWorseningCough { get; set; }
        public string Q12_SoreThroat { get; set; }
        public string Q13_ShortnessOfBreath { get; set; }
        public string Q14_LossOfSmell { get; set; }
        public string Q15_LossOfTaste { get; set; }
        public string Q16_UnexplainedHemorrhage { get; set; }
        public string Q17_FatigueMuscleSkinChanges { get; set; }
        public string Comment { get; set; }
        public string AttachmentName { get; set; }
        public string AttachmentPath { get; set; }
        public string Identify_PutMaskGloves { get; set; }
        public string Identify_GivePatientMask { get; set; }
        public string Identify_ContactSupervisor { get; set; }
        public bool Isolate_SingleRoom { get; set; }
        public bool Isolate_SeparatePatient6Feet { get; set; }
        public bool Isolate_PPEEscort { get; set; }
        public bool Isolate_UrinalBedpan { get; set; }
        public bool Isolate_ProviderReview { get; set; }
        public string Inform_ContactInfectionPrevention { get; set; }
        public string Inform_RiskAssessment { get; set; }
        public string Inform_DoNotMovePatient { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string ModifiedBy { get; set; }
        public string CreatedBy { get; set; }
        public Guid TenantId { get; set; }
    }
}
