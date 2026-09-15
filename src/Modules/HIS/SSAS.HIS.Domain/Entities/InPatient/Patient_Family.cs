using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Patient_Family : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PF_Id { get; set; }
        public string confirmeddiagnosis_understood { get; set; }
        public string confirmeddiagnosis_report { get; set; }
        public string safeeffective_understood { get; set; }
        public string safeeffective_report { get; set; }
        public string druginteraction_understood { get; set; }
        public string druginteraction_report { get; set; }
        public string drugfood_understood { get; set; }
        public string drugfood_report { get; set; }
        public string Nutrition_understood { get; set; }
        public string Nutrition_report { get; set; }
        public string Nutrition_explained { get; set; }
        public string safeequipment_understood { get; set; }
        public string safeequipment_report { get; set; }
        public string safeequipment_explained { get; set; }
        public string Painmanagement_understood { get; set; }
        public string Painmanagement_report { get; set; }
        public string rehabilitationtechnique_understood { get; set; }
        public string rehabilitationtechnique_report { get; set; }
        public string rehabilitationtechnique_explained { get; set; }
        public string dischargeplanhome_understood { get; set; }
        public string dischargeplanhome_report { get; set; }
        public string dischargeplanfollowup_understood { get; set; }
        public string dischargeplanfollowup_report { get; set; }
        public string preventivemeasureinfection_understood { get; set; }
        public string preventivemeasureinfection_report { get; set; }
        public string preventivemeasurepersonal_understood { get; set; }
        public string preventivemeasurepersonal_report { get; set; }
        public string othersImplans_understood { get; set; }
        public string othersImplans_report { get; set; }
        public string othersConsent_understood { get; set; }
        public string othersConsent_report { get; set; }
        public string othersFinancial_understood { get; set; }
        public string othersFinancial_report { get; set; }
        public string othersCommunity_understood { get; set; }
        public string othersCommunity_report { get; set; }
        public DateTime date_time { get; set; }
        public Guid TenantId { get; set; }
    }
}
