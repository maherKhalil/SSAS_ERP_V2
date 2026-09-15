using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class DoctorRemarksAndComments : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int DoctorID { get; set; }
        public string OPNumber { get; set; }
        public int PatientID { get; set; }
        public string DoctorRemark { get; set; }
        public string PatientComment { get; set; }
        public bool IsPatientComment { get; set; }
        public DateTime Date { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreationDate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
