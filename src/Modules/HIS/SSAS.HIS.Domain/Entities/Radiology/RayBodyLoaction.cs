using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class RayBodyLoaction : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string LocationAr { get; set; }
        public string LocationEn { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreationDate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
