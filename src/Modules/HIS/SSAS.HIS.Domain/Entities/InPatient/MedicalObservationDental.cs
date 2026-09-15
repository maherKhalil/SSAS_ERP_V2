using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class MedicalObservationDental : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int MedicalObservationID { get; set; }
        public string ToothNo { get; set; }
        public int ServiceID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int insuranceId { get; set; }
        public string MissingToothReason { get; set; }
        public Guid TenantId { get; set; }
    }
}
