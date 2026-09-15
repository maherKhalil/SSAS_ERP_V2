using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class ReceptionDevicsSchedule : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int ReceptionID { get; set; }
        public int DeviceID { get; set; }
        public int DayID { get; set; }
        public int SessionID { get; set; }
        public bool IsActive { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
