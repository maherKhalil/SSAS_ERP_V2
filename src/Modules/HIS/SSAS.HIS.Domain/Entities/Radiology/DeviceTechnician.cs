using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class DeviceTechnician : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int TechnicianID { get; set; }
        public string Device { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
