using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class LabsStores : ITenantOwnedEntity
    {
        public string LabCode { get; set; }
        public int StoreId { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
