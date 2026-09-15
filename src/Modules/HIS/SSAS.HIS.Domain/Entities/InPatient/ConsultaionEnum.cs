using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class ConsultaionEnum : ITenantOwnedEntity
    {
        public int id { get; set; }
        public int ParentID { get; set; }
        public string Name { get; set; }
        public string NameAr { get; set; }
        public string ConsType { get; set; }
        public Guid TenantId { get; set; }
    }
}
