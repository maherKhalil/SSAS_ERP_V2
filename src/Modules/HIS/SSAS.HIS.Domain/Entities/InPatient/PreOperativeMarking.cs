using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class PreOperativeMarking : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PreOperativeId { get; set; }
        public string Mandatory { get; set; }
        public string Checked { get; set; }
        public int ScheduleSurgeryId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
