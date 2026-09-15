using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class VitalTypeGroup : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
