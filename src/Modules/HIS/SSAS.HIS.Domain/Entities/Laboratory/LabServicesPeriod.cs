using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class LabServicesPeriod : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int ServiceID { get; set; }
        public string Period { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string PeriodType { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
