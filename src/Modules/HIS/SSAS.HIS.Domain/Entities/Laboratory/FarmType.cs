using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class FarmType : ITenantOwnedEntity
    {
        public int id { get; set; }
        public string NameAr { get; set; }
        public string NameEn { get; set; }
        public string Active { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
