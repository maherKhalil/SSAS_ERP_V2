using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class DevicesDefination : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string Code { get; set; }
        public string Serial { get; set; }
        public string NameEn { get; set; }
        public string NameAr { get; set; }
        public string Location { get; set; }
        public string DeviceType { get; set; }
        public string Emergency { get; set; }
        public bool ISActive { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public string CommSetting { get; set; }
        public string CommPort { get; set; }
        public string FormName { get; set; }
        public string IPAddres { get; set; }
        public string investigationgroup { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
