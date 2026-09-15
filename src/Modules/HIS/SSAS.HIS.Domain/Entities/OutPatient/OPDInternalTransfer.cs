using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class OPDInternalTransfer : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public int FromDoctorID { get; set; }
        public int ToDoctorID { get; set; }
        public int OldPrepareVisitSlipID { get; set; }
        public int NewPrepareVisitSlipID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public Guid TenantId { get; set; }
    }
}
