using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class CancelAdmitPatientInAnotherMedicalUnit : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PatientID { get; set; }
        public int DischargeReasonID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreationDate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
