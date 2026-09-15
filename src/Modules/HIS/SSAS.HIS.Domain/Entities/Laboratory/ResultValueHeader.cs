using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class ResultValueHeader : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string NameEn { get; set; }
        public string NameAr { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
