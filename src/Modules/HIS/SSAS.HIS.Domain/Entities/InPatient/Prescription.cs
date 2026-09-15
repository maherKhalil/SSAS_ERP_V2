using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Prescription : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public string OPNo { get; set; }
        public string IPNo { get; set; }
        public string PrescriptionNo { get; set; }
        public DateTime PrescriptionDate { get; set; }
        public string AlertName { get; set; }
        public int DoctorID { get; set; }
        public string Notes { get; set; }
        public decimal TotalAmount { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
