using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class RadReceptioniestSchedule : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int DayID { get; set; }
        public int EmpID { get; set; }
        public string Status { get; set; }
        public int ReceptionID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int SessionId { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
