using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class DrugsWithInfusionRateHeader : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public DateTime PrescriptionDate { get; set; }
        public string PrescriptionNo { get; set; }
        public int DoctorID { get; set; }
        public string OPNumber { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string AlertName { get; set; }
        public int CompanyID { get; set; }
        public string Status { get; set; }
        public string Comorbidities { get; set; }
        public Guid TenantId { get; set; }
    }
}
