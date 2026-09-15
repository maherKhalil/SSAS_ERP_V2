using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class CurrentMedication : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public string PrescriptionNo { get; set; }
        public int DrugID { get; set; }
        public string Dosage { get; set; }
        public int DosageUnitID { get; set; }
        public int FrequencyID { get; set; }
        public string Period { get; set; }
        public int PeriodTypeID { get; set; }
        public string DoseQTY { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int AssesmentID { get; set; }
        public string Remarks { get; set; }
        public string ContinueDrugDuringAdmission { get; set; }
        public int NurseAdmissionAssessmentId { get; set; }
        public Guid TenantId { get; set; }
    }
}
