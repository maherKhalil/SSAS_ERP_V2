using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class PatientFamilyEducation : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public DateTime DateofAssessment { get; set; }
        public DateTime Time { get; set; }
        public string Dept { get; set; }
        public string Educationgivento { get; set; }
        public string Relationship { get; set; }
        public string LiteracyLevel { get; set; }
        public string Willingness { get; set; }
        public string PrimaryLanguage { get; set; }
        public string UnderstoodLanguage { get; set; }
        public string A_None { get; set; }
        public string A_Anxiety_Fear { get; set; }
        public string A_LanguageBarrier { get; set; }
        public string A_Denial { get; set; }
        public string A_Sensory_de_cit { get; set; }
        public string A_BeliefsandValues { get; set; }
        public string A_Literacy { get; set; }
        public string A_CulturalPractice { get; set; }
        public string A_PhysicalImpairment { get; set; }
        public string A_Pain_Discomfort { get; set; }
        public string A_Emotional { get; set; }
        public string A_Cognitive_impairment { get; set; }
        public string A_Lackofcon_dence { get; set; }
        public string A_Financial_Problems { get; set; }
        public string A_Others { get; set; }
        public string A_Others_text { get; set; }
        public string I_None { get; set; }
        public string I_Obtaintranslator { get; set; }
        public string I_TeachFamily { get; set; }
        public string I_Respectvalues { get; set; }
        public string I_Review_Repeat { get; set; }
        public string I_Reassurance { get; set; }
        public string I_RespectCultural { get; set; }
        public string I_Appropritatesubstitution { get; set; }
        public string I_Others { get; set; }
        public string I_Others_text { get; set; }
        public string B_Diagnosis { get; set; }
        public string B_Treatment { get; set; }
        public string B_Pain { get; set; }
        public string B_Self_care { get; set; }
        public string B_regimen { get; set; }
        public string B_Discharge { get; set; }
        public string B_hand { get; set; }
        public string B_stoma { get; set; }
        public string B_Injection { get; set; }
        public string B_Dietary { get; set; }
        public string B_Tube { get; set; }
        public string B_Rehabilitation { get; set; }
        public string B_Antenatal { get; set; }
        public string B_urinary { get; set; }
        public string B_SafeEffective { get; set; }
        public string B_tracheotomy { get; set; }
        public string B_Implants { get; set; }
        public string B_Coping { get; set; }
        public string TM_lecture { get; set; }
        public string TM_Demonstration { get; set; }
        public string TM_Discussion { get; set; }
        public string TM_Audio { get; set; }
        public string TM_Model { get; set; }
        public string TM_Verbal { get; set; }
        public string OPIP { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
