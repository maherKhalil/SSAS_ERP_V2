using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class PermittedStaff : ITenantOwnedEntity
    {
        public int id { get; set; }
        public string InvestigationGroup { get; set; }
        public string ReportEntry { get; set; }
        public string ReportValidation { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
