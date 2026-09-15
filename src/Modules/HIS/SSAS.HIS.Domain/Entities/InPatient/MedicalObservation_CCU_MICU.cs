using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class MedicalObservation_CCU_MICU : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int MedicalObservationID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public string PrimaryConsultant { get; set; }
        public string ICUConsultant { get; set; }
        public string ICUSection { get; set; }
        public DateTime HospitalAdmissionDate { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ICUAdmissionDate { get; set; }
        public string TypeOfAdmission { get; set; }
        public string ProvisionalDiagnosis { get; set; }
        public Guid TenantId { get; set; }
    }
}
