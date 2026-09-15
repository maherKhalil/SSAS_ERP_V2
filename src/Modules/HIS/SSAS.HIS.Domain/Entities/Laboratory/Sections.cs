using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class Sections : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string SectionName { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int InvestigationGroupId { get; set; }
        public Guid TenantId { get; set; }
    }
}
