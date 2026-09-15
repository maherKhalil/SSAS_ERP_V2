using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class MedicalObservation_Obs_Gyn : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int MedicalObservationID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public string GH_LMP { get; set; }
        public string GH_PMP { get; set; }
        public string GH_MenstrualCycle { get; set; }
        public string GH_TypeofFlow { get; set; }
        public string GH_Contraception { get; set; }
        public string GH_OtherContraception { get; set; }
        public string GH_PapSmearStatus { get; set; }
        public DateTime GH_PapSmearDate { get; set; }
        public string GH_PapSmearReport { get; set; }
        public string GH_HPVVaccinationStatus { get; set; }
        public DateTime GH_HPVVaccinationDate { get; set; }
        public string GH_HPVVaccinationReport { get; set; }
        public string GH_Other { get; set; }
        public string GH_USGReport { get; set; }
        public string OH_LMP { get; set; }
        public string OH_EDD { get; set; }
        public string OH_EDDBYUSG { get; set; }
        public string OH_Gravida { get; set; }
        public string OH_Para { get; set; }
        public string OH_Abortion { get; set; }
        public string OH_LiveBirth { get; set; }
        public string OH_FoetalDeath { get; set; }
        public string OH_Cause { get; set; }
        public string OH_Other { get; set; }
        public string SP_GestationalDiabetes { get; set; }
        public string SP_DiabetesMellitus { get; set; }
        public string SP_Hypertension { get; set; }
        public string SP_Oligohydramnios { get; set; }
        public string SP_PlacentaPraevia { get; set; }
        public string SP_MedicalHistory { get; set; }
        public string SP_Asthma { get; set; }
        public string SP_SurgicalHistory { get; set; }
        public string SP_InfertilityTreatment { get; set; }
        public string SP_Ifyes { get; set; }
        public string InjectionTetanus1 { get; set; }
        public string InjectionTetanus2 { get; set; }
        public string InjectionTetanus3 { get; set; }
        public string booster { get; set; }
        public string others { get; set; }
        public bool IsGynecologicalSelected { get; set; }
        public bool IsObstetricHistorySelected { get; set; }
        public bool IsPastObstetricSelected { get; set; }
        public bool IsSignificantHistorySelected { get; set; }
        public Guid TenantId { get; set; }
    }
}
