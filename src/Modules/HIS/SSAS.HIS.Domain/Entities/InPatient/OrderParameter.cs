using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class OrderParameter : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string Code { get; set; }
        public string Status { get; set; }
        public Guid TenantId { get; set; }
    }
}
