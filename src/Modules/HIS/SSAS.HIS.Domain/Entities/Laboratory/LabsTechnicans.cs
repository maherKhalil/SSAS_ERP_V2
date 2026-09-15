using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class LabsTechnicans : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string LabCode { get; set; }
        public int TechnicanId { get; set; }
        public int DayId { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
