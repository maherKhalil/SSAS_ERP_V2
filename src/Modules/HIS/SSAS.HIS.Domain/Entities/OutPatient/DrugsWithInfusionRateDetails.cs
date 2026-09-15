using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class DrugsWithInfusionRateDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int DrugsWithInfusionRateID { get; set; }
        public string PrescriptionNo { get; set; }
        public int PatientID { get; set; }
        public int DoctorID { get; set; }
        public int DrugID { get; set; }
        public int DosageUnitID { get; set; }
        public string Dosage { get; set; }
        public int FrequencyID { get; set; }
        public string Period { get; set; }
        public string DoseQty { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime StartTime { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int TypeId { get; set; }
        public int AdminModeId { get; set; }
        public string Remarks { get; set; }
        public string ContinueDrugDuringAdmission { get; set; }
        public int CompanyID { get; set; }
        public int GenericID { get; set; }
        public string SpecialInstruction { get; set; }
        public string PRN_Reason { get; set; }
        public string PRN_MaxFreq { get; set; }
        public string IFNeeded { get; set; }
        public string Strength { get; set; }
        public string ClinicalPharmacy_Accept { get; set; }
        public string Cannula { get; set; }
        public int TemplateId { get; set; }
        public string MainSolution { get; set; }
        public string Volume { get; set; }
        public string DosageBaseUnit { get; set; }
        public string RegularContinous { get; set; }
        public string InfusionRate { get; set; }
        public string InfusionRatePeriod { get; set; }
        public string AdministrationType { get; set; }
        public string Status { get; set; }
        public Guid TenantId { get; set; }
    }
}
