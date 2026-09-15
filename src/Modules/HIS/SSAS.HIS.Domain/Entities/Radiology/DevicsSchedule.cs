using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class DevicsSchedule : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int DeviceID { get; set; }
        public int UserID { get; set; }
        public int HolidayID { get; set; }
        public DateTime WorkFromTime { get; set; }
        public DateTime WorkToTime { get; set; }
        public DateTime WorkFromDate { get; set; }
        public DateTime WorkToDate { get; set; }
        public DateTime OffFromTime { get; set; }
        public DateTime OffToTime { get; set; }
        public DateTime OffFromDate { get; set; }
        public DateTime OffToDate { get; set; }
        public string StatusEnum { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string Code { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
