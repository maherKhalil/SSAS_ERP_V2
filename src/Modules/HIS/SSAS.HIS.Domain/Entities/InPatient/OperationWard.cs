using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class OperationWard : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string Code { get; set; }
        public int FloorID { get; set; }
        public string Name { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
