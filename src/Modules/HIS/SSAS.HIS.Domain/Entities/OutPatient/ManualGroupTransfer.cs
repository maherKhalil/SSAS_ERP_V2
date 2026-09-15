using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class ManualGroupTransfer : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int FromClinicID { get; set; }
        public int FromDocID { get; set; }
        public DateTime FromSchedualeDate { get; set; }
        public int PatientID { get; set; }
        public DateTime FromTime { get; set; }
        public int ToClinicID { get; set; }
        public int ToDocID { get; set; }
        public DateTime ToSchedualeDate { get; set; }
        public DateTime ToTime { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid TenantId { get; set; }
    }
}
