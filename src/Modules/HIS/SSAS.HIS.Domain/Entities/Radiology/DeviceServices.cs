using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class DeviceServices : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public int ServiceId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string Active { get; set; }
        public string HostCode { get; set; }
        public string DeviceValue { get; set; }
        public int CompanyID { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
