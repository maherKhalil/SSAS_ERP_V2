using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class NursingAdmissionAssessment : ITenantOwnedEntity
    {
        public int NursingAdmissionAssessmentID { get; set; }
        public string ChiefComplaint { get; set; }
        public string UrgentNeeds { get; set; }
        public string OrientedTo { get; set; }
        public string Valuables { get; set; }
        public string Clothing { get; set; }
        public string OwnMedication { get; set; }
        public string EyeGlasses { get; set; }
        public string Dentures { get; set; }
        public string OtherAids { get; set; }
        public string BriefHistoryOfChiefComplaintAndReasonForAdmission { get; set; }
        public string PreviousMajorRelated_IlnessOrSurgery { get; set; }
        public string Allergiesto { get; set; }
        public int DoctorID { get; set; }
        public string PainScreening { get; set; }
        public string NumericPainScale { get; set; }
        public string PainAssessment { get; set; }
        public string GeneralAppearance { get; set; }
        public string GeneralAppearanceDescription { get; set; }
        public string MentalStatus { get; set; }
        public string Anxious_relatedto { get; set; }
        public string Useeyeglassesfor { get; set; }
        public string Head_Ent { get; set; }
        public string Respiratory { get; set; }
        public string RespiratoryReferralInitiated { get; set; }
        public string Neuro_Muscular_Skeletal { get; set; }
        public string RespiratoryRemark { get; set; }
        public string LevelofConsciousnessis { get; set; }
        public string Cardiovascular { get; set; }
        public string Edemaof { get; set; }
        public int Ivfluid { get; set; }
        public string CardiovascularSite { get; set; }
        public string CardiovascularRate { get; set; }
        public string CardiovascularReferralInitiated { get; set; }
        public string CardiovascularRemarks { get; set; }
        public string Neuro_Muscular_SkeletalReferralInitiated { get; set; }
        public string Neuro_Muscular_Skeletal_Remark { get; set; }
        public string SkinAndHair { get; set; }
        public string SkinAndHairReferralInitiated { get; set; }
        public string SkinAndHair_Remark { get; set; }
        public string NutritionalAssessment { get; set; }
        public string Special_Diet { get; set; }
        public string Vitamin_Or_Mineral_Supplement { get; set; }
        public string Genitourinary { get; set; }
        public string Last_menstrual_period { get; set; }
        public string Gravida { get; set; }
        public string Para { get; set; }
        public string Abortion { get; set; }
        public string Gestational_diabetes { get; set; }
        public string GenitourinaryReferralInitiated { get; set; }
        public string GenitourinaryRemark { get; set; }
        public string Genitourinary_Frequency_Tiems { get; set; }
        public string Genitourinary_Frequency_hr { get; set; }
        public string Genitourinary_Frequency_day { get; set; }
        public string SPECIAL_ACTIVITIES_OF_DAILY_LIVING_ASSISTANCE { get; set; }
        public string SPECIAL_ACTIVITIES_ReferralInitiated { get; set; }
        public string SPECIAL_ACTIVITIES_Remarks { get; set; }
        public string SPECIAL_ACTIVITIES_OtherSpecify { get; set; }
        public string SocialAssessment { get; set; }
        public string SocialAssessment_ReferralInitiated { get; set; }
        public string SocialAssessment_Remark { get; set; }
        public string OTHER_OBSERVATIONS_FINDINGS_ACTION_TAKEN { get; set; }
        public string Signature { get; set; }
        public DateTime Date { get; set; }
        public string NAME_OF_ADMITTING_NURSE { get; set; }
        public DateTime Time_Notified { get; set; }
        public string Mode_Of_Admission { get; set; }
        public string PreviousMajorRelated_IlnessOrSurgery_OtherDisease { get; set; }
        public string Emergency_Admission { get; set; }
        public string Direct_Admission { get; set; }
        public string ACCOMPANIED_BY { get; set; }
        public string Primary_Language { get; set; }
        public string English { get; set; }
        public string Temp { get; set; }
        public string Pulse { get; set; }
        public string Resp { get; set; }
        public string Bp { get; set; }
        public string Ht { get; set; }
        public string Wt { get; set; }
        public int PatientID { get; set; }
        public string HearingDeficit { get; set; }
        public string VisionDefect { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
