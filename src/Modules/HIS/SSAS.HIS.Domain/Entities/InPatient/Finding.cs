using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Finding : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int FindingFlagId { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
