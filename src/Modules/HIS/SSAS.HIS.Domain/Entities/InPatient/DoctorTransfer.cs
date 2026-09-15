using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class DoctorTransfer : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public int FromDepartmentID { get; set; }
        public int ToDepartmentID { get; set; }
        public int FromDoctorID { get; set; }
        public int ToDoctorID { get; set; }
        public DateTime TransferDate { get; set; }
        public DateTime Transfertime { get; set; }
        public string transferReason { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
