using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class RadResultImages : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int ResultEntryDetailsID { get; set; }
        public string ImageName { get; set; }
        public Guid TenantId { get; set; }
    }
}
