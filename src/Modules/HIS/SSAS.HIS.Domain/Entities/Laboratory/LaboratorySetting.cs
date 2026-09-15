using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class LaboratorySetting : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int EmpID { get; set; }
        public string Type { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
