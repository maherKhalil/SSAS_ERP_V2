using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class Farms : ITenantOwnedEntity
    {
        public int id { get; set; }
        public string NameAr { get; set; }
        public string NameEn { get; set; }
        public int FarmTypeId { get; set; }
        public int ServiceID { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
