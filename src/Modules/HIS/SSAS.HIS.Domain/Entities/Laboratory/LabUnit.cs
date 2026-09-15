using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class LabUnit : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string UnitName { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
