using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class Service_ResultValueHeader : ITenantOwnedEntity
    {
        public int id { get; set; }
        public int Service_Id { get; set; }
        public int ResultValueHeader_Id { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
