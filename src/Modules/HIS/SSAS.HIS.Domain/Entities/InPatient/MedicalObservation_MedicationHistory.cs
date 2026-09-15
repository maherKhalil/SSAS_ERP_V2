using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class MedicalObservation_MedicationHistory : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int MedicalObservationID { get; set; }
        public int DrugId { get; set; }
        public string Dosage { get; set; }
        public int FerquencyID { get; set; }
        public string ContinueDrugDuringAdmission { get; set; }
        public string Remarks { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
