using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class WardPatientPrescriptions : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int WardPharmacyID { get; set; }
        public int PatientID { get; set; }
        public int DrugID { get; set; }
        public string Dosage { get; set; }
        public int UnitID { get; set; }
        public string CurrentQty { get; set; }
        public int FerquencyID { get; set; }
        public string Period { get; set; }
        public int PeriodTypeID { get; set; }
        public string QTY { get; set; }
        public decimal Price { get; set; }
        public int SubStoreBatchId { get; set; }
        public DateTime ExpiryDate { get; set; }
        public DateTime TimeTake { get; set; }
        public int WitnessID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public DateTime PrescriptionTimeTake { get; set; }
        public string SkipReason { get; set; }
        public string PrescriptionStatus { get; set; }
        public Guid TenantId { get; set; }
    }
}
